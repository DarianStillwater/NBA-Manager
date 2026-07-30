using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Multi-season stability gate: drives THREE consecutive offseasons through the
    /// real OffseasonManager pipeline (post-season bookkeeping → retirements →
    /// contract advancement → draft night → the July market → summer league → camp →
    /// roster compliance → rollover) and asserts league-wide invariants after each.
    ///
    /// Regular-season games are never simulated — the calendar jumps from rollover
    /// (Oct 21) straight to the next Jun 1, which is exactly what the offseason
    /// pipeline needs: RunPostSeason does the season-end bookkeeping itself
    /// (AdvanceContractYears, development/aging, retirements) and the stages that
    /// want season stats (awards, history) no-op naturally on empty stat lines.
    ///
    /// Rig note: OffseasonManager.DailyTick needs a live GameManager (every stage
    /// reaches through it for FreeAgents / SalaryCapManager / AllTeams / Development
    /// / TrainingCamp...). Player.Age and Team.Roster read GameManager.Instance too.
    /// So this test builds a real GameManager component on an INACTIVE GameObject —
    /// Awake never fires, so there is no scene load and no UI — invokes the private
    /// Initialize() to construct the manager graph, installs it as Instance for the
    /// duration, and restores the previous Instance on the way out.
    /// ponytail: the manager statics (InboxService.Instance, PersonnelManager.Instance,
    /// ...) end up pointing at this rig's managers, same as every other test in the
    /// suite that constructs a manager. Run the suite in a scratch scene, not on top
    /// of a live save.
    /// </summary>
    public class OffseasonStabilityTest : BaseTest
    {
        private const int TEAM_COUNT = 30;
        private const int CYCLES = 3;
        /// <summary>Season label of the first offseason driven (summer = label + 1).</summary>
        private const int FIRST_SEASON = 2026;
        private const long BIG_FA_VALUE = 5_000_000L;

        // 13 standard contracts + 1 two-way per team at kickoff — a legal league.
        private static readonly int[] SlotRating =
            { 88, 83, 79, 76, 74, 72, 70, 68, 66, 64, 62, 60, 58, 56 };
        private static readonly int[] SlotAge =
            { 29, 27, 31, 25, 33, 24, 28, 22, 35, 26, 23, 30, 21, 27 };
        private static readonly long[] SlotSalary =
        {
            34_000_000L, 24_000_000L, 15_000_000L, 9_000_000L, 7_000_000L, 5_000_000L,
            4_000_000L, 3_000_000L, 2_500_000L, 2_200_000L, 2_000_000L, 1_800_000L,
            1_600_000L, 1_400_000L
        };
        private const int TWO_WAY_SLOT = 13;

        private GameManager _gm;
        private GameManager _previousInstance;
        private bool _instanceSwapped;
        private readonly List<string> _pipelineErrors = new List<string>();
        private System.Random _rng;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            _rng = new System.Random(20270601);

            try
            {
                BuildRig();
                for (int i = 0; i < CYCLES; i++)
                    RunCycle(FIRST_SEASON + i);
            }
            finally
            {
                Teardown();
            }

            return (_passed, _failed);
        }

        // ==================== RIG ====================

        private void BuildRig()
        {
            var go = new GameObject("__OffseasonRig__");
            go.SetActive(false);                       // defers Awake: no Instance grab, no scene load
            _gm = go.AddComponent<GameManager>();

            try
            {
                typeof(GameManager)
                    .GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(_gm, null);
            }
            catch (Exception)
            {
                // Initialize's last line starts the data-load coroutine, which an
                // inactive GameObject refuses. Every manager is already constructed.
            }

            Assert(_gm.Offseason != null && _gm.SalaryCapManager != null && _gm.FreeAgents != null,
                "Headless GameManager rig builds the offseason manager graph");

            _previousInstance = GameManager.Instance;
            SetInstance(_gm);
            _instanceSwapped = true;

            Application.logMessageReceived += OnLog;

            BuildLeague();

            SetField(_gm, "_currentSeason", FIRST_SEASON);
            SetDate(new DateTime(FIRST_SEASON + 1, 6, 1));

            Assert(_gm.AllTeams.Count == TEAM_COUNT && _gm.GetTeam("T00") != null,
                "Synthetic 30-team league is wired into the rig");
            Assert(_gm.AllTeams.All(t => _gm.SalaryCapManager.GetStandardContractCount(t.TeamId) == 13),
                "Every team starts legal: 13 standard contracts");
        }

        private void BuildLeague()
        {
            var teams = new List<Team>();
            var byId = new Dictionary<string, Team>();
            string[] divisions = { "Atlantic", "Central", "Southeast", "Northwest", "Pacific", "Southwest" };
            int calYear = FIRST_SEASON + 1;

            for (int t = 0; t < TEAM_COUNT; t++)
            {
                string teamId = $"T{t:00}";
                var team = new Team
                {
                    TeamId = teamId,
                    City = $"City{t:00}",
                    Nickname = $"Squad{t:00}",
                    Abbreviation = teamId,
                    Conference = t < 15 ? "Eastern" : "Western",
                    Division = divisions[t / 5],
                    Wins = 20 + (t % 25),
                    Losses = 62 - (t % 25),
                    TeamChemistry = 55f
                };

                for (int s = 0; s < SlotRating.Length; s++)
                {
                    string pid = $"{teamId}_p{s:00}";
                    int rating = Mathf.Clamp(SlotRating[s] + (t % 5) - 2, 40, 95);
                    int age = SlotAge[s];
                    int service = Math.Max(0, age - 19);

                    var p = new Player
                    {
                        PlayerId = pid,
                        FirstName = teamId,
                        LastName = $"Player{s:00}",
                        TeamId = teamId,
                        Position = (Position)((s % 5) + 1),   // enum is 1-based
                        BirthDate = new DateTime(calYear - age, 1, 15),
                        DraftYear = calYear - service,
                        Energy = 100, Morale = 75, Form = 50
                    };
                    // Every attribute the position-weighted OverallRating formulas read,
                    // so the rating lands on `rating` whatever the position is.
                    p.BallHandling = p.Passing = p.Shot_Three = p.Shot_MidRange =
                        p.Finishing_Rim = p.Finishing_PostMoves = p.Speed = p.Vertical =
                        p.Strength = p.Defense_Perimeter = p.Defense_Interior =
                        p.DefensiveRebound = p.Block = p.BasketballIQ = rating;
                    _gm.PlayerDatabase.AddPlayer(p);

                    var contract = new Contract
                    {
                        PlayerId = pid,
                        TeamId = teamId,
                        // Staggered so roughly a quarter of each roster expires every summer
                        YearsRemaining = 1 + ((t + s) % 4),
                        CurrentYearSalary = SlotSalary[s],
                        ConsecutiveSeasonsWithTeam = 2,
                        Type = s == TWO_WAY_SLOT ? ContractType.TwoWay : ContractType.Standard
                    };
                    if (s == TWO_WAY_SLOT)
                        contract.CurrentYearSalary = LeagueCBA.GetMinimumSalary(0) / 2;
                    _gm.SalaryCapManager.RegisterContract(contract);

                    team.RosterPlayerIds.Add(pid);
                }

                team.CoachPersonality = AICoachPersonality.CreateRandom(
                    $"coach_{teamId}", $"Coach {teamId}", _rng);
                team.InvalidateRosterCache();
                teams.Add(team);
                byId[teamId] = team;
            }

            SetField(_gm, "_allTeams", teams);
            SetField(_gm, "_teamsById", byId);
            SetField(_gm, "_playerTeamId", teams[0].TeamId);

            foreach (var team in teams)
                team.AutoSetStartingLineup(team.CoachPersonality);
        }

        private void Teardown()
        {
            Application.logMessageReceived -= OnLog;
            if (_instanceSwapped) SetInstance(_previousInstance);
            if (_gm != null) DestroyImmediate(_gm.gameObject);
            _gm = null;
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception) &&
                message != null && message.Contains("[Offseason]"))
                _pipelineErrors.Add(message);
        }

        private static void SetInstance(GameManager gm)
        {
            typeof(GameManager)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .GetSetMethod(nonPublic: true)
                .Invoke(null, new object[] { gm });
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }

        private void SetDate(DateTime date) => SetField(_gm, "_currentDate", date);

        // ==================== THE DRIVE ====================

        /// <summary>
        /// One summer, day by day: exactly what GameManager.AdvanceDay does for the
        /// offseason (advance the date, then tick the offseason system), minus the
        /// systems that only matter in-season.
        /// </summary>
        private void RunCycle(int seasonLabel)
        {
            int calYear = seasonLabel + 1;
            _pipelineErrors.Clear();

            SetField(_gm, "_currentSeason", seasonLabel);
            SetDate(new DateTime(calYear, 6, 1));
            _gm.Offseason.BeginOffseason(seasonLabel, _gm.CurrentDate);

            int poolAtFaOpen = 0, bigAtFaOpen = 0, poolAtOct = 0, bigAtOct = 0;
            var faOpen = OffseasonDates.FreeAgency(calYear);
            var oct1 = new DateTime(calYear, 10, 1);
            var last = new DateTime(calYear, 10, 22);

            for (var day = new DateTime(calYear, 6, 1); day <= last; day = day.AddDays(1))
            {
                SetDate(day);
                _gm.Offseason.DailyTick(new DailyTickContext(day, _gm.PlayerTeamId, _gm));

                if (day == faOpen) { poolAtFaOpen = PoolSize(); bigAtFaOpen = BigFreeAgents(); }
                if (day == oct1) { poolAtOct = PoolSize(); bigAtOct = BigFreeAgents(); }
            }

            AssertCycle(seasonLabel, calYear, poolAtFaOpen, bigAtFaOpen, poolAtOct, bigAtOct);
        }

        private int PoolSize() => _gm.FreeAgents.GetFreeAgents().Count;

        private int BigFreeAgents() => _gm.FreeAgents.GetFreeAgents()
            .Select(fa => _gm.PlayerDatabase.GetPlayer(fa.PlayerId))
            .Count(p => p != null && p.RetirementYear == 0 &&
                        OffseasonManager.MarketValue(p) >= BIG_FA_VALUE);

        // ==================== INVARIANTS ====================

        private void AssertCycle(int seasonLabel, int calYear,
            int poolAtFaOpen, int bigAtFaOpen, int poolAtOct, int bigAtOct)
        {
            string tag = $"[summer {calYear}]";
            var cap = _gm.SalaryCapManager;
            var db = _gm.PlayerDatabase;
            var teams = _gm.AllTeams;

            // The pipeline ran at all (a silently-dead engine must not read as stable)
            Assert(_pipelineErrors.Count == 0,
                $"{tag} No stage threw — DailyTick logged no offseason error " +
                $"({(_pipelineErrors.Count > 0 ? _pipelineErrors[0] : "clean")})");
            AssertGreaterThan(poolAtFaOpen, 29f, $"{tag} Contract expiry filled a real free-agent class");
            AssertEqual(60, db.GetAllPlayers().Count(p => p.PlayerId.StartsWith($"draft_{calYear}_")),
                $"{tag} Draft night produced 60 signed picks");

            // ---- 1. Roster legality ----
            var overStandard = teams.Where(t =>
                cap.GetStandardContractCount(t.TeamId) > RosterManager.STANDARD_ROSTER_MAX).ToList();
            Assert(overStandard.Count == 0,
                $"{tag} No team carries more than 15 standard contracts " +
                $"({Worst(overStandard, t => cap.GetStandardContractCount(t.TeamId))})");

            var overTwoWay = teams.Where(t =>
                cap.GetTwoWayContractCount(t.TeamId) > RosterManager.TWO_WAY_SLOTS).ToList();
            Assert(overTwoWay.Count == 0,
                $"{tag} No team carries more than 3 two-way contracts " +
                $"({Worst(overTwoWay, t => cap.GetTwoWayContractCount(t.TeamId))})");

            var thin = teams.Where(t => t.RosterPlayerIds.Count < 13).ToList();
            Assert(thin.Count == 0,
                $"{tag} Every roster is at the 13-man minimum after camp compliance " +
                $"({Worst(thin, t => t.RosterPlayerIds.Count)})");

            // ---- 2. Cap sanity ----
            var negativePayroll = teams.Where(t => cap.GetTeamPayroll(t.TeamId) < 0).ToList();
            Assert(negativePayroll.Count == 0,
                $"{tag} No team has a negative payroll (in $M: " +
                $"{Worst(negativePayroll, t => (int)(cap.GetTeamPayroll(t.TeamId) / 1_000_000L))})");

            long ceiling = LeagueCBA.LUXURY_TAX_LINE * 2;
            var runaway = teams.Where(t => cap.GetTeamPayroll(t.TeamId) >= ceiling).ToList();
            Assert(runaway.Count == 0,
                $"{tag} No payroll runs away past 2x the tax line (in $M: " +
                $"{Worst(runaway, t => (int)(cap.GetTeamPayroll(t.TeamId) / 1_000_000L))})");

            var contracts = teams.SelectMany(t => cap.GetTeamContracts(t.TeamId)).ToList();
            Assert(contracts.All(c => c.CurrentYearSalary >= 0),
                $"{tag} No contract carries a negative salary");
            Assert(contracts.All(c => c.YearsRemaining >= 1),
                $"{tag} No zombie contract (0 or negative years remaining) stays registered");

            // ---- 3. The free-agent pool drains ----
            AssertLessThan(bigAtOct, 15f,
                $"{tag} Under 15 unsigned $5M+ free agents by Oct 1 (was {bigAtFaOpen} when the market opened)");
            AssertLessThan(poolAtOct, poolAtFaOpen,
                $"{tag} The pool shrank between Jul 6 and Oct 1");

            // ---- 4. Nobody on two teams; contracts and roster lists agree ----
            var duplicates = teams.SelectMany(t => t.RosterPlayerIds)
                .GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert(duplicates.Count == 0,
                $"{tag} No player appears on two rosters ({duplicates.Count} duplicate id(s))");

            int orphanContracts = contracts.Count(c =>
                _gm.GetTeam(c.TeamId)?.RosterPlayerIds.Contains(c.PlayerId) != true);
            AssertEqual(0, orphanContracts,
                $"{tag} Every contract's team lists the player on its roster");

            int uncontracted = teams.SelectMany(t => t.RosterPlayerIds.Select(id => (t, id)))
                .Count(x => cap.GetContract(x.id)?.TeamId != x.t.TeamId);
            AssertEqual(0, uncontracted,
                $"{tag} Every rostered player has a contract with that team");

            int wrongTeamField = teams.SelectMany(t => t.RosterPlayerIds.Select(id => (t, id)))
                .Count(x => db.GetPlayer(x.id)?.TeamId != x.t.TeamId);
            AssertEqual(0, wrongTeamField,
                $"{tag} Every rostered player's own TeamId points back at that team");

            // ---- 5. Market state clean at rollover ----
            Assert(!_gm.Offseason.EngineActive,
                $"{tag} The engine shut itself off at the Oct 21 rollover");
            AssertEqual(seasonLabel + 1, _gm.CurrentSeason,
                $"{tag} Rollover started the next season");
            var live = _gm.Offseason.Market?.ToSave() ?? new List<MarketBidRecord>();
            Assert(live.Count == 0,
                $"{tag} No live bids or offer sheets survive the rollover ({live.Count} record(s))");
            AssertEqual(0, _gm.Offseason.PendingQualifyingOffers.Count,
                $"{tag} No qualifying-offer decisions left hanging");
        }

        private static string Worst(List<Team> offenders, Func<Team, int> metric)
        {
            if (offenders.Count == 0) return "clean";
            var worst = offenders.OrderByDescending(metric).First();
            return $"{offenders.Count} team(s), worst {worst.TeamId} at {metric(worst)}";
        }
    }
}
