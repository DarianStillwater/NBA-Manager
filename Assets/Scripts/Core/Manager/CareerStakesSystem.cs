using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// The driving layer for career stakes. JobSecurityManager and JobMarketManager
    /// have modeled expectations, warnings, firings, and openings since day one —
    /// this system finally CALLS them: owner expectations at season start, weekly
    /// in-season evaluations (warnings land in the inbox, disasters end in a
    /// mid-season firing), and the end-of-season verdict that can put the player
    /// on the job market. Also drives the former-player GM pipeline's season
    /// processing and owns its persistence.
    /// </summary>
    public class CareerStakesSystem : IDailyTickable, ISaveSection
    {
        public string SystemId => "CareerStakes";
        public int TickOrder => Manager.TickOrder.JobMarket - 10; // evaluate before the market ticks

        private const int MIN_GAMES_FOR_EVALUATION = 15;
        private const int MIN_GAMES_FOR_MIDSEASON_FIRING = 25;

        private readonly JobSecurityManager _jobSecurity;
        private readonly GMJobSecurityManager _gmSecurity;
        private readonly Func<string, TeamFinances> _finances;
        private readonly Func<UnifiedCareerProfile> _career;
        private readonly Func<string, Team> _teams;
        private readonly Action<FiringReason> _onFirePlayer;

        public CareerStakesSystem(
            JobSecurityManager jobSecurity,
            GMJobSecurityManager gmSecurity,
            Func<string, TeamFinances> financeLookup,
            Func<UnifiedCareerProfile> careerProvider,
            Func<string, Team> teamLookup,
            Action<FiringReason> onFirePlayer)
        {
            _jobSecurity = jobSecurity;
            _gmSecurity = gmSecurity;
            _finances = financeLookup;
            _career = careerProvider;
            _teams = teamLookup;
            _onFirePlayer = onFirePlayer;
        }

        // ==================== EXPECTATIONS ====================

        /// <summary>
        /// Sets the owner's preseason expectations for the player's current team.
        /// Called at new game, at each season rollover, and when the player takes
        /// a new job. Projected strength is in expected-wins units, derived from
        /// the roster's top-eight talent.
        /// </summary>
        public SeasonExpectations SetPlayerSeasonExpectations()
        {
            var career = _career?.Invoke();
            if (career == null || string.IsNullOrEmpty(career.CurrentTeamId)) return null;

            var team = _teams?.Invoke(career.CurrentTeamId);
            var finances = _finances?.Invoke(career.CurrentTeamId);
            if (team == null || finances == null) return null;

            int projectedWins = ProjectTeamWins(team);
            return _jobSecurity?.SetSeasonExpectations(career.ProfileId, finances, projectedWins);
        }

        /// <summary>
        /// Rough preseason win projection from the roster's top-eight ratings.
        /// Internal so the sim never has to expose it as a player-facing number.
        /// </summary>
        public static int ProjectTeamWins(Team team)
        {
            var roster = team?.Roster;
            if (roster == null || roster.Count == 0) return 35;

            float avgTop8 = roster.Where(p => p != null)
                .OrderByDescending(p => p.OverallRating)
                .Take(8)
                .Select(p => (float)p.OverallRating)
                .DefaultIfEmpty(65f)
                .Average();

            return Mathf.Clamp(Mathf.RoundToInt((avgTop8 - 55f) * 1.5f + 20f), 15, 62);
        }

        /// <summary>Season rollover: fresh owner messages + fresh expectations.</summary>
        public void OnNewSeason()
        {
            var career = _career?.Invoke();
            if (career == null) return;

            if (career.CurrentRole == UnifiedRole.Unemployed)
            {
                career.YearsUnemployed++;
                return;
            }

            _jobSecurity?.InitializeForNewSeason(career, career.CurrentTeamId);
            SetPlayerSeasonExpectations();
        }

        // ==================== IN-SEASON EVALUATION ====================

        public void DailyTick(in DailyTickContext ctx)
        {
            if (ctx.Date.DayOfWeek != DayOfWeek.Monday) return;

            var career = _career?.Invoke();
            if (career == null || !career.IsUserControlled) return;
            if (career.CurrentRole == UnifiedRole.Unemployed) return;
            if (career.CurrentExpectations == null) return; // legacy saves: no expectations yet

            var team = _teams?.Invoke(career.CurrentTeamId);
            if (team == null) return;

            int gamesPlayed = team.Wins + team.Losses;
            if (gamesPlayed < MIN_GAMES_FOR_EVALUATION || gamesPlayed >= 82) return;

            var finances = _finances?.Invoke(team.TeamId);
            if (finances == null) return;

            bool playoffBound = team.Wins >= team.Losses;
            _jobSecurity?.EvaluateJobSecurity(career.ProfileId, team.Wins, team.Losses, playoffBound, finances);

            // A coach deep underwater doesn't always survive to April
            if (gamesPlayed >= MIN_GAMES_FOR_MIDSEASON_FIRING &&
                _jobSecurity != null && _jobSecurity.ShouldFireStaff(career.ProfileId, finances))
            {
                _jobSecurity.FireStaff(career.ProfileId, finances, "Mid-season change");
                _onFirePlayer?.Invoke(FiringReason.PoorRecord);
            }
        }

        // ==================== SEASON END ====================

        /// <summary>
        /// Full season-end pass, called from the offseason chain: records the
        /// player's season into their career history, ages every staff profile,
        /// runs the former-player GM pipeline, and delivers the owner's verdict —
        /// which can end in a firing.
        /// </summary>
        public void ProcessSeasonEnd(GameManager gm)
        {
            if (gm == null) return;

            var awards = gm.Awards?.GetForSeason(gm.CurrentSeason);
            var playoffTeams = new HashSet<string>();
            var series = PlayoffManager.Instance?.AllSeries();
            if (series != null)
            {
                foreach (var s in series)
                {
                    if (!string.IsNullOrEmpty(s?.HigherSeedTeamId)) playoffTeams.Add(s.HigherSeedTeamId);
                    if (!string.IsNullOrEmpty(s?.LowerSeedTeamId)) playoffTeams.Add(s.LowerSeedTeamId);
                }
            }

            ProcessSeasonEndCore(gm.CurrentSeason, awards?.ChampionTeamId, awards?.RunnerUpTeamId, playoffTeams);
        }

        /// <summary>Testable core of the season-end pass.</summary>
        public void ProcessSeasonEndCore(int season, string championTeamId, string runnerUpTeamId,
            HashSet<string> playoffTeamIds)
        {
            // Former-player GM pipeline + staff aging run regardless of the
            // player's employment
            try { _gmSecurity?.ProcessEndOfSeason(season); }
            catch (Exception ex) { Debug.LogWarning($"[CareerStakes] GM season processing failed: {ex.Message}"); }

            try { PersonnelManager.Instance?.ProcessEndOfSeason(season); }
            catch (Exception ex) { Debug.LogWarning($"[CareerStakes] Staff aging failed: {ex.Message}"); }

            var career = _career?.Invoke();
            if (career == null) return;

            if (career.CurrentRole == UnifiedRole.Unemployed) return;

            var team = _teams?.Invoke(career.CurrentTeamId);
            if (team == null) return;

            bool wonTitle = career.CurrentTeamId == championTeamId;
            bool madePlayoffs = wonTitle || (playoffTeamIds?.Contains(career.CurrentTeamId) ?? false);
            string playoffResult = wonTitle ? "Champion"
                : career.CurrentTeamId == runnerUpTeamId ? "Finals"
                : madePlayoffs ? "Made playoffs" : "Missed playoffs";

            // The season finally lands in the career record (this was never wired —
            // every career history entry read 0-0 before)
            career.UpdateCurrentSeasonResults(team.Wins, team.Losses, madePlayoffs, wonTitle);

            var finances = _finances?.Invoke(career.CurrentTeamId);
            if (finances == null || _jobSecurity == null) return;

            var verdict = _jobSecurity.EvaluateEndOfSeason(career.ProfileId, team.Wins, team.Losses,
                madePlayoffs, playoffResult, wonTitle, finances);

            // EvaluateEndOfSeason already sent the owner message and marked the
            // profile Fired — we handle the franchise-side fallout
            if (verdict?.WillBeFired == true)
            {
                var reason = career.CurrentExpectations?.ExpectsPlayoffs == true && !madePlayoffs
                    ? FiringReason.MissedPlayoffs
                    : madePlayoffs ? FiringReason.EarlyPlayoffExit : FiringReason.PoorRecord;
                _onFirePlayer?.Invoke(reason);
            }
        }

        // ==================== SAVE ====================

        public void WriteSave(SaveData data)
        {
            if (data == null) return;
            data.GMJobSecurity = _gmSecurity?.GetSaveData();
        }

        public void ReadSave(SaveData data, in SaveReadContext ctx)
        {
            if (data?.GMJobSecurity != null)
            {
                try { _gmSecurity?.LoadSaveData(data.GMJobSecurity); }
                catch (Exception ex) { Debug.LogWarning($"[CareerStakes] GM security restore failed: {ex.Message}"); }
            }
        }
    }
}
