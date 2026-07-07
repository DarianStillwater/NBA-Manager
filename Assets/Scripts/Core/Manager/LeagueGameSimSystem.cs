using System;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Simulates all non-player league games for the current date — the daily heartbeat
    /// that keeps the other 28 teams' seasons moving. Skips only the player's next game
    /// (the one they will play or quick-sim themselves).
    /// Extracted from GameManager.SimulateLeagueGamesForDate.
    /// </summary>
    public class LeagueGameSimSystem : IDailyTickable
    {
        public string SystemId => "LeagueSim";
        public int TickOrder => Manager.TickOrder.LeagueSim;

        private readonly GameManager _gm;

        public LeagueGameSimSystem(GameManager gm)
        {
            _gm = gm ?? throw new ArgumentNullException(nameof(gm));
        }

        public void DailyTick(in DailyTickContext ctx)
        {
            SimulateLeagueGamesForDate(ctx.Date);
        }

        private void SimulateLeagueGamesForDate(DateTime date)
        {
            var season = _gm.SeasonController;
            if (season == null || _gm.AllTeams == null || _gm.AllTeams.Count == 0) return;

            // Get all games for today from the master schedule (league-wide)
            var todaysGames = season.Schedule?
                .Where(g => g.Date.Date == date.Date && !g.IsCompleted && g.Type == CalendarEventType.Game)
                .ToList();

            if (todaysGames == null || todaysGames.Count == 0) return;

            var simulator = new Simulation.GameSimulator(_gm.PlayerDatabase);

            // Only skip the player's NEXT game (the one they'll manually play/sim)
            var nextPlayerGame = season.GetNextGame();

            foreach (var game in todaysGames)
            {
                // Only skip the specific game the player is about to play
                if (nextPlayerGame != null && game.EventId == nextPlayerGame.EventId)
                    continue;

                var homeTeam = _gm.GetTeam(game.HomeTeamId);
                var awayTeam = _gm.GetTeam(game.AwayTeamId);
                if (homeTeam == null || awayTeam == null) continue;

                try
                {
                    var result = simulator.SimulateGame(homeTeam, awayTeam);
                    simulator.RecordGameToPlayerStats(result, game.EventId, game.Date);
                    season.RecordGameResult(game, result.HomeScore, result.AwayScore);
                    _gm.LeagueStats.AddGameResult(result.BoxScore);
                    _gm.ProcessPostGameMorale(result, game.IsPlayoffGame);
                }
                catch (Exception ex)
                {
                    // Fallback: random result if simulation fails
                    Debug.LogWarning($"[LeagueGameSim] League sim failed for {game.AwayTeamId}@{game.HomeTeamId}: {ex.Message}");
                    int homeScore = UnityEngine.Random.Range(90, 120);
                    int awayScore = UnityEngine.Random.Range(90, 120);
                    if (homeScore == awayScore) homeScore += 2; // no ties
                    season.RecordGameResult(game, homeScore, awayScore);
                }
            }

            // Recalculate league averages and advanced stats for all players
            _gm.LeagueStats.Recalculate();
            RecalculateAllAdvancedStats();
        }

        /// <summary>
        /// Recalculate advanced stats for all players who have played games.
        /// </summary>
        private void RecalculateAllAdvancedStats()
        {
            var allPlayers = _gm.PlayerDatabase?.GetAllPlayers();
            if (allPlayers == null) return;
            foreach (var player in allPlayers)
            {
                if (player.CurrentSeasonStats != null && player.CurrentSeasonStats.GamesPlayed > 0)
                    _gm.LeagueStats.CalculatePlayerAdvancedStats(player.CurrentSeasonStats);
            }
        }
    }
}
