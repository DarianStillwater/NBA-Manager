using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Brings the economy to life: generates owners and team finances at new
    /// game, accrues game revenue through the completion pipeline, syncs payroll
    /// weekly, raises the luxury-tax conversation with the owner, and closes the
    /// books each season. FinanceManager and RevenueManager hold the models;
    /// this system is the only thing that drives them.
    /// </summary>
    public class FinanceSystem : IDailyTickable, ISaveSection, INewGameInitializable
    {
        public string SystemId => "Finance";
        public int TickOrder => Manager.TickOrder.Personnel + 50;

        private readonly FinanceManager _finance;
        private readonly RevenueManager _revenue;
        private readonly SalaryCapManager _cap;
        private readonly Func<string, Team> _teamLookup;
        private readonly Func<List<Team>> _allTeams;

        private bool _taxApprovalRequested;
        private int _season;

        public FinanceSystem(
            FinanceManager finance,
            RevenueManager revenue,
            SalaryCapManager cap,
            Func<string, Team> teamLookup,
            Func<List<Team>> allTeams)
        {
            _finance = finance;
            _revenue = revenue;
            _cap = cap;
            _teamLookup = teamLookup;
            _allTeams = allTeams;
        }

        public static FinanceSystem CreateDefault(GameManager gm)
        {
            return new FinanceSystem(gm.FinanceManager, gm.RevenueManager,
                gm.SalaryCapManager, gm.GetTeam, () => gm.AllTeams);
        }

        /// <summary>Game revenue accrues from every completed game, any sim path.</summary>
        public void HookGameRevenue(GameCompletionPipeline pipeline)
        {
            if (pipeline != null) pipeline.OnGameCompleted += RecordGameRevenue;
        }

        // ==================== NEW GAME / NEW SEASON ====================

        public void InitializeForNewGame(in NewGameContext ctx)
        {
            _season = ctx.Season;
            _taxApprovalRequested = false;

            foreach (var team in _allTeams())
            {
                if (team == null || _finance.GetTeamFinances(team.TeamId) != null) continue;
                _finance.RegisterTeamFinances(CreateFinancesFor(team));
            }

            string careerId = GameManager.Instance?.Career?.ProfileId;
            if (!string.IsNullOrEmpty(careerId) && !string.IsNullOrEmpty(ctx.PlayerTeamId))
                _finance.InitializeOwnerRelationship(careerId, ctx.PlayerTeamId);

            _revenue.InitializeForSeason(ctx.Season, _allTeams());
        }

        /// <summary>Season rollover: fresh revenue ledger, tax conversation resets.</summary>
        public void OnNewSeason(int season)
        {
            _season = season;
            _taxApprovalRequested = false;
            _revenue.InitializeForSeason(season, _allTeams());
        }

        private TeamFinances CreateFinancesFor(Team team)
        {
            var owner = RandomOwner();
            int marketSize = team.MarketSize switch
            {
                MarketSize.Large => 8,
                MarketSize.Medium => 5,
                _ => 3
            };
            var fin = TeamFinances.CreateForTeam(team.TeamId, team.FullName, team.City,
                marketSize, owner, LeagueCBA.SALARY_CAP, LeagueCBA.LUXURY_TAX_LINE);
            if (team.ArenaCapacity > 0) fin.ArenaCapacity = team.ArenaCapacity;
            fin.ArenaName = team.ArenaName;
            return fin;
        }

        private static Owner RandomOwner()
        {
            var name = Util.NameGenerator.GenerateStaffName();

            float roll = UnityEngine.Random.value;
            if (roll < 0.2f) return Owner.CreateLavishOwner(name.FirstName, name.LastName);
            if (roll < 0.4f) return Owner.CreateCheapOwner(name.FirstName, name.LastName);
            return Owner.CreateBalancedOwner(name.FirstName, name.LastName);
        }

        // ==================== DAILY ====================

        public void DailyTick(in DailyTickContext ctx)
        {
            // Weekly bookkeeping on Mondays: payroll sync + revenue projections.
            if (ctx.Date.DayOfWeek == DayOfWeek.Monday)
            {
                foreach (var team in _allTeams())
                {
                    if (team == null) continue;
                    long staffSalaries = PersonnelManager.Instance?
                        .GetTeamStaff(team.TeamId)?.Sum(s => (long)s.AnnualSalary) ?? 0L;
                    _finance.UpdatePayroll(team.TeamId, _cap.GetTeamPayroll(team.TeamId), staffSalaries);
                    _revenue.UpdateProjection(team.TeamId, team.Wins, team.Losses, false);
                }
            }

            CheckLuxuryTaxConversation(ctx.PlayerTeamId);
        }

        /// <summary>
        /// The first time the player's payroll crosses the tax line each season,
        /// the owner wants a word. Auto-approval or a scheduled meeting both
        /// reach the inbox through FinanceManager.OnOwnerMessage.
        /// </summary>
        private void CheckLuxuryTaxConversation(string playerTeamId)
        {
            if (_taxApprovalRequested || string.IsNullOrEmpty(playerTeamId)) return;

            long payroll = _cap.GetTeamPayroll(playerTeamId);
            if (payroll <= LeagueCBA.LUXURY_TAX_LINE) return;

            _taxApprovalRequested = true;
            string careerId = GameManager.Instance?.Career?.ProfileId ?? "PLAYER";
            if (_finance.GetOwnerRelationship(careerId, playerTeamId) == null)
                _finance.InitializeOwnerRelationship(careerId, playerTeamId);

            _finance.RequestLuxuryTaxApproval(playerTeamId, careerId,
                payroll - LeagueCBA.LUXURY_TAX_LINE,
                "Roster payroll has crossed the luxury tax line.");
        }

        // ==================== GAME REVENUE ====================

        public void RecordGameRevenue(GameCompletionContext ctx)
        {
            var home = _teamLookup(ctx.GameEvent?.HomeTeamId);
            var away = _teamLookup(ctx.GameEvent?.AwayTeamId);
            if (home == null || away == null) return;

            _revenue.CalculateGameRevenue(home, away, EstimateAttendance(home), ctx.IsPlayoff);
        }

        private static int EstimateAttendance(Team home)
        {
            int capacity = home.ArenaCapacity > 0 ? home.ArenaCapacity : 18_500;
            float winFactor = home.TotalGames > 0 ? home.WinPercentage : 0.5f;
            float fill = Mathf.Clamp(0.72f + 0.25f * winFactor, 0.6f, 1f);
            return Mathf.RoundToInt(capacity * fill);
        }

        // ==================== SEASON CLOSE ====================

        /// <summary>
        /// Close the books: final financials per team, revenue reports, tax-year
        /// tracking. Called by the offseason engine's post-season stage.
        /// </summary>
        public void ProcessSeasonEnd(GameManager gm)
        {
            var awards = gm?.Awards?.GetForSeason(gm.CurrentSeason);
            string champion = awards?.ChampionTeamId;
            string runnerUp = awards?.RunnerUpTeamId;
            var playoffTeams = PlayoffTeamIds();

            foreach (var team in _allTeams())
            {
                if (team == null) continue;

                bool madePlayoffs = playoffTeams.Contains(team.TeamId);
                string resultText = team.TeamId == champion ? "NBA Champions"
                    : team.TeamId == runnerUp ? "Lost NBA Finals"
                    : madePlayoffs ? "Made playoffs" : "Missed playoffs";

                _finance.UpdatePayroll(team.TeamId, _cap.GetTeamPayroll(team.TeamId), 0);
                _finance.ProcessEndOfSeason(team.TeamId, team.Wins, team.Losses, madePlayoffs, resultText);
                _cap.RecordSeasonTaxStatus(team.TeamId,
                    _cap.GetTeamPayroll(team.TeamId) > LeagueCBA.LUXURY_TAX_LINE);

                var playoffResult = team.TeamId == champion ? PlayoffResult.Champion
                    : team.TeamId == runnerUp ? PlayoffResult.Finals
                    : madePlayoffs ? PlayoffResult.FirstRound
                    : PlayoffResult.MissedPlayoffs;
                _revenue.ProcessEndOfSeason(team.TeamId, playoffResult);
            }
        }

        private HashSet<string> PlayoffTeamIds()
        {
            var ids = new HashSet<string>();
            var playoffs = PlayoffManager.Instance;
            if (playoffs == null) return ids;

            foreach (var series in playoffs.AllSeries())
            {
                if (!string.IsNullOrEmpty(series?.HigherSeedTeamId)) ids.Add(series.HigherSeedTeamId);
                if (!string.IsNullOrEmpty(series?.LowerSeedTeamId)) ids.Add(series.LowerSeedTeamId);
            }
            return ids;
        }

        // ==================== SAVE ====================

        public void WriteSave(SaveData data)
        {
            var save = new FinanceSaveData
            {
                Season = _season,
                TaxApprovalRequested = _taxApprovalRequested,
                Teams = new List<TeamFinances>()
            };

            foreach (var team in _allTeams())
            {
                var fin = team != null ? _finance.GetTeamFinances(team.TeamId) : null;
                if (fin != null) save.Teams.Add(fin);
            }

            string careerId = GameManager.Instance?.Career?.ProfileId;
            string playerTeamId = GameManager.Instance?.PlayerTeamId;
            if (!string.IsNullOrEmpty(careerId) && !string.IsNullOrEmpty(playerTeamId))
                save.PlayerOwnerRelationship = _finance.GetOwnerRelationship(careerId, playerTeamId);

            data.FinancesData = save;
        }

        public void ReadSave(SaveData data, in SaveReadContext ctx)
        {
            var save = data.FinancesData;
            bool hasData = save?.Teams != null && save.Teams.Count > 0;

            if (hasData)
            {
                _season = save.Season;
                _taxApprovalRequested = save.TaxApprovalRequested;
                foreach (var fin in save.Teams)
                {
                    if (fin != null && !string.IsNullOrEmpty(fin.TeamId))
                        _finance.RegisterTeamFinances(fin);
                }
            }

            // Legacy saves (or gaps): bootstrap owners for any team without books.
            foreach (var team in _allTeams())
            {
                if (team == null || _finance.GetTeamFinances(team.TeamId) != null) continue;
                _finance.RegisterTeamFinances(CreateFinancesFor(team));
            }

            string careerId = GameManager.Instance?.Career?.ProfileId;
            string playerTeamId = GameManager.Instance?.PlayerTeamId;
            if (!string.IsNullOrEmpty(careerId) && !string.IsNullOrEmpty(playerTeamId))
            {
                _finance.InitializeOwnerRelationship(careerId, playerTeamId);
                var saved = save?.PlayerOwnerRelationship;
                var live = _finance.GetOwnerRelationship(careerId, playerTeamId);
                if (saved != null && live != null)
                {
                    live.Trust = saved.Trust;
                    live.Respect = saved.Respect;
                    live.Communication = saved.Communication;
                    live.MeetingsHeld = saved.MeetingsHeld;
                    live.PromisesMade = saved.PromisesMade;
                    live.PromisesKept = saved.PromisesKept;
                }
            }

            // Revenue ledger restarts for the season in progress; per-game revenue
            // to date is not persisted (projection-driven displays recover fast).
            if (_season <= 1) _season = ctx.CurrentSeason;
            _revenue.InitializeForSeason(_season, _allTeams());
        }
    }

    /// <summary>Save payload for the economy.</summary>
    [Serializable]
    public class FinanceSaveData
    {
        public int Season;
        public bool TaxApprovalRequested;
        public List<TeamFinances> Teams = new List<TeamFinances>();
        public CoachOwnerRelationship PlayerOwnerRelationship;
    }
}
