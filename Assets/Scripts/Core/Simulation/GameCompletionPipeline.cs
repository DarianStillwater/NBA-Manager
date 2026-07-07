using System;
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
        AutonomousSim
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
        private readonly MoraleChemistryManager _morale;
        private readonly Func<string, Team> _teamLookup;

        /// <summary>
        /// Fires after all steps for a game. Future consumers: form updates,
        /// development credit, transaction/news publication.
        /// </summary>
        public event Action<GameCompletionContext> OnGameCompleted;

        public GameCompletionPipeline(
            PlayerDatabase playerDb,
            SeasonController season,
            LeagueStatsAggregator leagueStats,
            MoraleChemistryManager morale,
            Func<string, Team> teamLookup)
        {
            _db = playerDb;
            _season = season;
            _leagueStats = leagueStats;
            _morale = morale;
            _teamLookup = teamLookup;
        }

        public static GameCompletionPipeline CreateDefault(GameManager gm)
        {
            return new GameCompletionPipeline(
                gm.PlayerDatabase,
                gm.SeasonController,
                gm.LeagueStats,
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

            // 1. Season stats + game logs for every participant
            GameStatRecorder.Record(result, ctx.GameEvent.EventId, ctx.GameEvent.Date,
                _db, homeTeam, awayTeam, ctx.IsPlayoff, ctx.PlayoffRound);

            // 2. Standings + calendar completion (Team.Wins/Losses is the record authority)
            _season?.RecordGameResult(ctx.GameEvent, result.HomeScore, result.AwayScore);

            // 3. League aggregates
            if (result.BoxScore != null)
                _leagueStats?.AddGameResult(result.BoxScore);
            if (!deferAggregates)
                FinishBatch();

            // 4. Morale for both teams
            if (_morale != null)
            {
                if (homeTeam != null) _morale.ProcessGameResult(result, homeTeam, ctx.IsPlayoff);
                if (awayTeam != null) _morale.ProcessGameResult(result, awayTeam, ctx.IsPlayoff);
            }

            OnGameCompleted?.Invoke(ctx);
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
