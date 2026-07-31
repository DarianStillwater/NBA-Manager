using System;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>Which sim path produced a completed game.</summary>
    public enum GameSource
    {
        LeagueAutoSim,
        QuickSim,
        InteractiveMatch,
        AutonomousSim,
        /// <summary>GM-only mode: the AI head coach ran the game day; full box score via the live sim.</summary>
        AutonomousCoachSim
    }

    public readonly struct GameCompletionContext
    {
        public readonly CalendarEvent GameEvent;
        public readonly GameResult Result;
        public readonly GameSource Source;
        public readonly string PlayerTeamId;
        public readonly bool IsPlayoff;
        public readonly int PlayoffRound;

        public GameCompletionContext(
            CalendarEvent gameEvent,
            GameResult result,
            GameSource source,
            string playerTeamId,
            bool isPlayoff = false,
            int playoffRound = 0)
        {
            GameEvent = gameEvent;
            Result = result;
            Source = source;
            PlayerTeamId = playerTeamId ?? "";
            IsPlayoff = isPlayoff;
            PlayoffRound = playoffRound;
        }

        public bool IsPlayerGame =>
            GameEvent != null && PlayerTeamId.Length > 0 &&
            (GameEvent.HomeTeamId == PlayerTeamId || GameEvent.AwayTeamId == PlayerTeamId);
    }

    /// <summary>
    /// THE post-game processor. Every sim path — league auto-sim, quick-sim, interactive
    /// match — funnels its completed game through Complete() so season stats, standings,
    /// league aggregates, and morale are recorded identically regardless of how the game
    /// was played. Domain effects only: UI and state transitions stay with the callers.
    /// </summary>
    public sealed class GameCompletionPipeline
    {
        private readonly PlayerDatabase _db;
        private readonly SeasonController _season;
        private readonly LeagueStatsAggregator _leagueStats;
        private readonly InjuryManager _injuries;
        private readonly MoraleChemistryManager _morale;
        private readonly Func<string, Team> _teamLookup;

        /// <summary>Longest a camp exhibition can put someone out.</summary>
        public const int PreseasonMaxInjuryDays = 5;

        /// <summary>
        /// Fires after all steps for a game. Future consumers: form updates,
        /// development credit, transaction/news publication.
        /// </summary>
        public event Action<GameCompletionContext> OnGameCompleted;

        public GameCompletionPipeline(
            PlayerDatabase playerDb,
            SeasonController season,
            LeagueStatsAggregator leagueStats,
            InjuryManager injuries,
            MoraleChemistryManager morale,
            Func<string, Team> teamLookup)
        {
            _db = playerDb;
            _season = season;
            _leagueStats = leagueStats;
            _injuries = injuries;
            _morale = morale;
            _teamLookup = teamLookup;
        }

        public static GameCompletionPipeline CreateDefault(GameManager gm)
        {
            return new GameCompletionPipeline(
                gm.PlayerDatabase,
                gm.SeasonController,
                gm.LeagueStats,
                gm.InjuryManager,
                gm.MoraleChemistryManager,
                gm.GetTeam);
        }

        /// <summary>
        /// Run all post-game steps for one completed game.
        /// deferAggregates=true skips the league-wide recalculation — batch callers
        /// (the daily league sim) call FinishBatch() once after their loop instead.
        /// </summary>
        public void Complete(in GameCompletionContext ctx, bool deferAggregates = false)
        {
            var result = ctx.Result;
            if (result == null || ctx.GameEvent == null)
            {
                Debug.LogWarning("[GameCompletion] Complete called without result/event — skipped.");
                return;
            }

            var homeTeam = _teamLookup?.Invoke(result.HomeTeamId);
            var awayTeam = _teamLookup?.Invoke(result.AwayTeamId);

            // Camp exhibition: the tape counts, the box score doesn't. Nothing that
            // feeds a season number (stats, form, league averages, W/L) runs — the
            // physical cost does, at half price with capped injuries.
            bool preseason = ctx.GameEvent.IsPreseason;

            if (!preseason)
            {
                // 1. Season stats + game logs for every participant
                GameStatRecorder.Record(result, ctx.GameEvent.EventId, ctx.GameEvent.Date,
                    _db, homeTeam, awayTeam, ctx.IsPlayoff, ctx.PlayoffRound);

                // 1b. Form: recompute each participant's hot/cold streak from their
                //     freshly-recorded recent games.
                if (result.BoxScore?.PlayerStats != null && _db != null)
                {
                    foreach (var stats in result.BoxScore.PlayerStats.Values)
                    {
                        if (stats == null || stats.Minutes <= 0) continue;
                        FormTracker.Recompute(_db.GetPlayer(stats.PlayerId));
                    }
                }
            }

            // 2. Standings + calendar completion (Team.Wins/Losses is the record
            //    authority). Preseason lands the score on the calendar event only —
            //    RecordGameResult skips the W/L itself.
            _season?.RecordGameResult(ctx.GameEvent, result.HomeScore, result.AwayScore);

            // 3. League aggregates
            if (!preseason)
            {
                if (result.BoxScore != null)
                    _leagueStats?.AddGameResult(result.BoxScore);
                if (!deferAggregates)
                    FinishBatch();
            }

            // 4. Morale for both teams — preseason must feed NO season state, so an
            //    0-3 exhibition record can't seed a losing-streak morale penalty.
            if (!preseason && _morale != null)
            {
                if (homeTeam != null) _morale.ProcessGameResult(result, homeTeam, ctx.IsPlayoff);
                if (awayTeam != null) _morale.ProcessGameResult(result, awayTeam, ctx.IsPlayoff);
            }

            // 5. Injuries + minutes load for every participant — headless games roll
            //    the same post-hoc probabilistic model interactive matches use.
            ProcessInjuriesAndMinutes(ctx, result.BoxScore, preseason);

            // 6. Preseason minutes are lighter than the box score says: give back half
            //    of what the game cost each participant.
            if (preseason)
                RefundHalfEnergy(result.BoxScore);

            // 7. Energy sanity: post-game values persist to the next game (there is no
            //    tip-off reset); keep them in range.
            ClampParticipantEnergy(result.BoxScore);

            // 8. Camp progress: the exhibition feeds training camp the same signal the
            //    auto-scrimmage used to.
            if (preseason)
            {
                Manager.OffseasonManager.Instance?.NotifyPreseasonGamePlayed(ctx.GameEvent, result);
                // The hook's consumers are all season bookkeeping (franchise/league
                // record books, gate revenue) — an exhibition belongs in none of them.
                return;
            }

            OnGameCompleted?.Invoke(ctx);
        }

        /// <summary>Half the energy this game charged, back in the tank.</summary>
        private void RefundHalfEnergy(BoxScore box)
        {
            if (box == null || _db == null) return;
            foreach (var stats in box.PlayerStats.Values)
            {
                if (stats == null || stats.EnergyAtTipoff <= 0f) continue;
                var player = _db.GetPlayer(stats.PlayerId);
                if (player == null) continue;
                float spent = stats.EnergyAtTipoff - player.Energy;
                if (spent > 0f) player.Energy += spent * 0.5f;
            }
        }

        private void ProcessInjuriesAndMinutes(in GameCompletionContext ctx, BoxScore box,
            bool preseason = false)
        {
            if (_injuries == null || box == null || _db == null) return;

            DateTime gameDate = ctx.GameEvent.Date;
            bool homeB2B = HadGameYesterday(box.HomeTeamId, gameDate);
            bool awayB2B = HadGameYesterday(box.AwayTeamId, gameDate);

            // The PlayerStats dictionary is the canonical participant set — the
            // Home/AwayPlayerStats lists are not populated by every sim path.
            foreach (var stats in box.PlayerStats.Values)
            {
                if (stats == null || stats.Minutes <= 0) continue; // didn't play

                var player = _db.GetPlayer(stats.PlayerId);
                if (player == null) continue;

                bool isBackToBack = player.TeamId == box.HomeTeamId ? homeB2B : awayB2B;

                if (!player.IsInjured)
                {
                    var context = new InjuryContext
                    {
                        IsGameAction = true,
                        IsContactPlay = DetermineIfContactHeavy(stats),
                        PlayerEnergy = player.Energy,
                        MinutesThisGame = stats.Minutes,
                        MinutesLast7Days = player.GetMinutesInLastDays(7, gameDate),
                        IsBackToBack = isBackToBack,
                        IsPlayoffs = ctx.IsPlayoff
                    };

                    var injuryEvent = _injuries.CheckForInjury(player, context);
                    if (injuryEvent != null)
                    {
                        // Preseason knocks are knocks: keep the roll, cap the severity.
                        if (preseason && injuryEvent.DaysOut > PreseasonMaxInjuryDays)
                        {
                            injuryEvent.DaysOut = PreseasonMaxInjuryDays;
                            injuryEvent.OriginalDaysOut = PreseasonMaxInjuryDays;
                            injuryEvent.Severity = InjurySeverity.Minor;
                        }
                        _injuries.ApplyInjury(player, injuryEvent);
                    }
                }

                // Record minutes played for load management
                _injuries.RecordMinutesPlayed(player, stats.Minutes, gameDate, isBackToBack);
            }
        }

        /// <summary>
        /// Contact-heavy game heuristic: rebounds, fouls, and blocks are physical battles.
        /// </summary>
        private static bool DetermineIfContactHeavy(PlayerGameStats stats)
        {
            int contactIndicator = stats.TotalRebounds * 2 +
                                   stats.PersonalFouls * 3 +
                                   stats.Blocks * 2;
            return contactIndicator > 10;
        }

        /// <summary>
        /// Whether THIS team played yesterday — the second night of a back-to-back.
        /// </summary>
        private bool HadGameYesterday(string teamId, DateTime date)
        {
            var schedule = _season?.Schedule;
            if (schedule == null || string.IsNullOrEmpty(teamId)) return false;

            var yesterday = date.AddDays(-1).Date;
            return schedule.Any(e =>
                e.Type == CalendarEventType.Game &&
                e.Date.Date == yesterday &&
                (e.HomeTeamId == teamId || e.AwayTeamId == teamId));
        }

        private void ClampParticipantEnergy(BoxScore box)
        {
            if (box == null || _db == null) return;
            foreach (var stats in box.PlayerStats.Values)
            {
                var player = stats != null ? _db.GetPlayer(stats.PlayerId) : null;
                if (player != null)
                    player.Energy = Mathf.Clamp(player.Energy, 0f, 100f);
            }
        }

        /// <summary>
        /// League-average + advanced-stat recalculation. Once per game for single-game
        /// callers, once per day for the league sim batch.
        /// </summary>
        public void FinishBatch()
        {
            if (_leagueStats == null) return;
            _leagueStats.Recalculate();

            var allPlayers = _db?.GetAllPlayers();
            if (allPlayers == null) return;
            foreach (var player in allPlayers)
            {
                if (player.CurrentSeasonStats != null && player.CurrentSeasonStats.GamesPlayed > 0)
                    _leagueStats.CalculatePlayerAdvancedStats(player.CurrentSeasonStats);
            }
        }

        /// <summary>
        /// Degraded path for score-only results (league-sim fallback, autonomous
        /// GM-mode games): standings only — there is no box score to record.
        /// </summary>
        public void CompleteScoreOnly(CalendarEvent gameEvent, int homeScore, int awayScore)
        {
            if (gameEvent == null) return;
            _season?.RecordGameResult(gameEvent, homeScore, awayScore);
        }
    }
}
