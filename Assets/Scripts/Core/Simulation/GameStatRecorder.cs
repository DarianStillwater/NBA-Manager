using System;
using System.Collections.Generic;
using System.Linq;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Records a completed game's box score into player career/season stats as GameLogs.
    /// Extracted from GameSimulator so every sim path (league auto-sim, quick-sim,
    /// interactive match) records season stats through the same code.
    /// </summary>
    public static class GameStatRecorder
    {
        public static void Record(
            GameResult result,
            string gameId,
            DateTime gameDate,
            PlayerDatabase playerDatabase,
            Team homeTeam,
            Team awayTeam,
            bool isPlayoff = false,
            int playoffRound = 0)
        {
            if (result?.BoxScore == null || playerDatabase == null) return;

            RecordTeamGameLogs(result.BoxScore.HomeTeamId, result.BoxScore.AwayTeamId,
                result, gameId, gameDate, playerDatabase, homeTeam, isHome: true, isPlayoff, playoffRound);
            RecordTeamGameLogs(result.BoxScore.AwayTeamId, result.BoxScore.HomeTeamId,
                result, gameId, gameDate, playerDatabase, awayTeam, isHome: false, isPlayoff, playoffRound);
        }

        private static void RecordTeamGameLogs(
            string teamId,
            string opponentTeamId,
            GameResult result,
            string gameId,
            DateTime gameDate,
            PlayerDatabase playerDatabase,
            Team team,
            bool isHome,
            bool isPlayoff,
            int playoffRound)
        {
            int teamScore = isHome ? result.HomeScore : result.AwayScore;
            int opponentScore = isHome ? result.AwayScore : result.HomeScore;
            bool wasOvertime = result.WentToOvertime;
            int overtimePeriods = result.Quarters - 4;

            // Get the player stats list for this team
            var playerStatsList = isHome
                ? result.BoxScore.HomePlayerStats
                : result.BoxScore.AwayPlayerStats;

            // Also check the dictionary for any players not in the lists
            var allPlayerIds = new HashSet<string>();
            foreach (var ps in playerStatsList)
            {
                allPlayerIds.Add(ps.PlayerId);
            }
            foreach (var kvp in result.BoxScore.PlayerStats)
            {
                allPlayerIds.Add(kvp.Key);
            }

            var starterIds = team?.StartingLineupIds ?? Array.Empty<string>();

            foreach (var playerId in allPlayerIds)
            {
                var player = playerDatabase?.GetPlayer(playerId);
                if (player == null || player.TeamId != teamId) continue;

                var stats = result.BoxScore.PlayerStats.TryGetValue(playerId, out var ps)
                    ? ps
                    : playerStatsList.FirstOrDefault(p => p.PlayerId == playerId);

                if (stats == null) continue;

                // Skip players who didn't play (0 minutes)
                if (stats.Minutes <= 0) continue;

                bool started = starterIds.Contains(playerId);

                var gameLog = GameLog.Create(
                    gameId: gameId,
                    date: gameDate,
                    opponentTeamId: opponentTeamId,
                    isHome: isHome,
                    isPlayoff: isPlayoff,
                    playoffRound: playoffRound,
                    minutes: stats.Minutes,
                    started: started,
                    points: stats.Points,
                    fgm: stats.FieldGoalsMade,
                    fga: stats.FieldGoalAttempts,
                    threePm: stats.ThreePointMade,
                    threePa: stats.ThreePointAttempts,
                    ftm: stats.FreeThrowsMade,
                    fta: stats.FreeThrowAttempts,
                    orb: stats.OffensiveRebounds,
                    drb: stats.DefensiveRebounds,
                    assists: stats.Assists,
                    steals: stats.Steals,
                    blocks: stats.Blocks,
                    turnovers: stats.Turnovers,
                    fouls: stats.PersonalFouls,
                    plusMinus: stats.PlusMinus,
                    teamScore: teamScore,
                    opponentScore: opponentScore,
                    wasOvertime: wasOvertime,
                    overtimePeriods: overtimePeriods > 0 ? overtimePeriods : 0
                );

                // Add to player's current season stats (auto-create if missing).
                // Season year derives from the game date (Oct+ = season start year),
                // not the wall clock.
                if (player.CurrentSeasonStats == null)
                {
                    int seasonYear = gameDate.Month >= 8 ? gameDate.Year : gameDate.Year - 1;
                    player.StartNewSeason(seasonYear, player.TeamId);
                }
                player.CurrentSeasonStats?.AddGameFromLog(gameLog);
            }
        }
    }
}
