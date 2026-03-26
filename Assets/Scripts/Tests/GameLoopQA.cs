using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Automated QA bot that programmatically plays through the game loop.
    /// Attach to any GameObject in the Boot scene. Runs automatically on Play.
    /// Outputs structured [QA] logs for diagnosis.
    /// </summary>
    public class GameLoopQA : MonoBehaviour
    {
        [Header("QA Settings")]
        public string TestTeamId = "OKC";
        public int GamesToSimulate = 5;
        public bool AutoRun = true;
        public float DelayBeforeStart = 2f; // Wait for GameManager to initialize

        private int _pass = 0;
        private int _fail = 0;
        private List<string> _failures = new List<string>();

        private void Start()
        {
            if (AutoRun) StartCoroutine(RunQA());
        }

        private IEnumerator RunQA()
        {
            Log("========== GAME LOOP QA START ==========");

            // Wait for GameManager to initialize
            yield return new WaitForSeconds(DelayBeforeStart);

            var gm = GameManager.Instance;
            if (gm == null) { Fail("GameManager.Instance is null"); yield break; }

            // Wait for game data to load
            while (gm.CurrentState == GameState.Booting)
                yield return new WaitForSeconds(0.5f);

            Log($"GameManager state: {gm.CurrentState}");

            // === PHASE 1: Start New Game ===
            Log("--- PHASE 1: Starting New Game ---");

            if (gm.CurrentState == GameState.MainMenu)
            {
                var difficulty = new DifficultySettings();
                gm.StartNewGame("QA", "Bot", 40, TestTeamId, difficulty, 50, 50, 50, UserRole.Both);
                yield return new WaitForSeconds(1f);
            }

            // Wait for Playing state
            int waitCount = 0;
            while (gm.CurrentState != GameState.Playing && waitCount++ < 20)
                yield return new WaitForSeconds(0.5f);

            if (gm.CurrentState != GameState.Playing)
            {
                Fail($"Game did not enter Playing state. Current: {gm.CurrentState}");
                PrintSummary();
                yield break;
            }
            Pass("Game started successfully, state = Playing");

            // === PHASE 2: Verify Team Setup ===
            Log("--- PHASE 2: Verify Team Setup ---");

            var allTeams = gm.AllTeams;
            if (allTeams == null || allTeams.Count == 0)
            {
                Fail("AllTeams is null or empty");
                PrintSummary();
                yield break;
            }
            Pass($"{allTeams.Count} teams loaded");

            int teamsWithRoster = allTeams.Count(t => t.Roster != null && t.Roster.Count >= 5);
            Check(teamsWithRoster == 30, $"{teamsWithRoster}/30 teams have roster >= 5 players");

            int teamsWithLineup = allTeams.Count(t => t.StartingLineupIds != null &&
                t.StartingLineupIds.Any(id => !string.IsNullOrEmpty(id)));
            Check(teamsWithLineup == 30, $"{teamsWithLineup}/30 teams have starting lineups set");

            int teamsWithCoach = allTeams.Count(t => t.CoachPersonality != null);
            Check(teamsWithCoach == 30, $"{teamsWithCoach}/30 teams have AI coach personality");

            int teamsWithStrategy = allTeams.Count(t => t.OffensiveStrategy != null && t.OffensiveStrategy.TargetPace > 0);
            Check(teamsWithStrategy >= 1, $"{teamsWithStrategy}/30 teams have non-default strategy");

            // === PHASE 3: Verify Player Stats Initialization ===
            Log("--- PHASE 3: Verify Player Stats ---");

            var playerTeam = gm.GetPlayerTeam();
            if (playerTeam == null) { Fail("GetPlayerTeam() returned null"); PrintSummary(); yield break; }
            Pass($"Player team: {playerTeam.City} {playerTeam.Name} ({playerTeam.TeamId})");

            var allPlayers = gm.PlayerDatabase?.GetAllPlayers();
            int totalPlayers = allPlayers?.Count ?? 0;
            int playersWithStats = allPlayers?.Count(p => p.CurrentSeasonStats != null) ?? 0;
            Check(playersWithStats == totalPlayers, $"{playersWithStats}/{totalPlayers} players have CurrentSeasonStats initialized");

            // Check player team roster specifically
            if (playerTeam.Roster != null)
            {
                foreach (var p in playerTeam.Roster.Take(5))
                {
                    bool hasStats = p.CurrentSeasonStats != null;
                    if (!hasStats)
                        Fail($"Player {p.FirstName} {p.LastName} ({p.PlayerId}) has NULL CurrentSeasonStats");
                    else
                        Pass($"Player {p.FirstName} {p.LastName} has SeasonStats (Year={p.CurrentSeasonStats.Year})");
                }
            }

            // Check attributes loaded
            var samplePlayer = playerTeam.Roster?.FirstOrDefault();
            if (samplePlayer != null)
            {
                Check(samplePlayer.Speed > 0, $"Sample player {samplePlayer.LastName} Speed={samplePlayer.Speed}");
                Check(samplePlayer.Shot_Three > 0, $"Sample player {samplePlayer.LastName} Shot_Three={samplePlayer.Shot_Three}");
                Check(samplePlayer.BallHandling > 0, $"Sample player {samplePlayer.LastName} BallHandling={samplePlayer.BallHandling}");
                Check(samplePlayer.FreeThrow > 0, $"Sample player {samplePlayer.LastName} FreeThrow={samplePlayer.FreeThrow}");
            }

            // === PHASE 4: Verify Schedule ===
            Log("--- PHASE 4: Verify Schedule ---");

            var sc = gm.SeasonController;
            Check(sc != null, "SeasonController exists");
            if (sc != null)
            {
                var schedule = sc.Schedule;
                Check(schedule != null && schedule.Count > 100, $"Schedule has {schedule?.Count ?? 0} total games (expect ~1000+)");

                var playerGames = schedule?.Where(g => g.HomeTeamId == TestTeamId || g.AwayTeamId == TestTeamId).ToList();
                Check(playerGames != null && playerGames.Count >= 40, $"Player team has {playerGames?.Count ?? 0} scheduled games");

                var nextGame = sc.GetNextGame();
                Check(nextGame != null, $"GetNextGame() returned: {nextGame?.HomeTeamId} vs {nextGame?.AwayTeamId} on {nextGame?.Date:MMM dd}");

                Log($"Current date: {gm.CurrentDate:yyyy-MM-dd}");
            }

            // === PHASE 5: Simulate Games ===
            Log($"--- PHASE 5: Simulate {GamesToSimulate} Games ---");

            for (int gameNum = 1; gameNum <= GamesToSimulate; gameNum++)
            {
                Log($"  --- GAME {gameNum} ---");

                var nextGame = sc?.GetNextGame();
                if (nextGame == null)
                {
                    Fail($"No next game found for game #{gameNum}");
                    break;
                }

                // Advance to game day
                int daysAdvanced = 0;
                while (gm.CurrentDate.Date < nextGame.Date.Date && daysAdvanced < 30)
                {
                    gm.AdvanceDay();
                    daysAdvanced++;
                }
                Log($"  Advanced {daysAdvanced} days to {gm.CurrentDate:MMM dd, yyyy}");

                // Get teams
                bool isHome = nextGame.HomeTeamId == TestTeamId;
                string oppId = isHome ? nextGame.AwayTeamId : nextGame.HomeTeamId;
                var homeTeam = gm.GetTeam(nextGame.HomeTeamId);
                var awayTeam = gm.GetTeam(nextGame.AwayTeamId);

                if (homeTeam == null || awayTeam == null)
                {
                    Fail($"Null team: home={homeTeam?.TeamId}, away={awayTeam?.TeamId}");
                    continue;
                }

                // Record pre-game stats for a sample player
                var sampleStarter = playerTeam.Roster?.FirstOrDefault(p =>
                    playerTeam.StartingLineupIds?.Contains(p.PlayerId) == true);
                int preGameGP = sampleStarter?.CurrentSeasonStats?.GamesPlayed ?? -1;
                int preGamePts = sampleStarter?.CurrentSeasonStats?.Points ?? -1;

                // Simulate
                int preWins = playerTeam.Wins;
                int preLosses = playerTeam.Losses;

                try
                {
                    var simulator = new GameSimulator(gm.PlayerDatabase);
                    var result = simulator.SimulateGame(homeTeam, awayTeam);
                    simulator.RecordGameToPlayerStats(result, nextGame.EventId, nextGame.Date);
                    sc.RecordGameResult(nextGame, result.HomeScore, result.AwayScore);

                    Check(result.HomeScore > 50, $"Home score realistic: {result.HomeScore}");
                    Check(result.AwayScore > 50, $"Away score realistic: {result.AwayScore}");
                    Check(result.HomeScore < 180, $"Home score not absurd: {result.HomeScore}");

                    bool playerWon = (isHome && result.HomeScore > result.AwayScore) ||
                                     (!isHome && result.AwayScore > result.HomeScore);

                    // Check W/L updated
                    if (playerWon)
                        Check(playerTeam.Wins == preWins + 1, $"Wins updated: {preWins} → {playerTeam.Wins}");
                    else
                        Check(playerTeam.Losses == preLosses + 1, $"Losses updated: {preLosses} → {playerTeam.Losses}");

                    // Check stats updated for sample player
                    if (sampleStarter != null)
                    {
                        int postGameGP = sampleStarter.CurrentSeasonStats?.GamesPlayed ?? -1;
                        int postGamePts = sampleStarter.CurrentSeasonStats?.Points ?? -1;

                        Check(postGameGP > preGameGP, $"  {sampleStarter.LastName} GamesPlayed: {preGameGP} → {postGameGP}");
                        Check(postGamePts > preGamePts || postGameGP > preGameGP,
                            $"  {sampleStarter.LastName} Points: {preGamePts} → {postGamePts}");

                        if (postGameGP > 0)
                        {
                            float ppg = sampleStarter.CurrentSeasonStats.PPG;
                            float rpg = sampleStarter.CurrentSeasonStats.RPG;
                            Log($"  {sampleStarter.LastName}: {ppg:0.0} PPG, {rpg:0.0} RPG after {postGameGP} games");
                        }
                    }

                    // Check box score has data
                    int playersWithMinutes = 0;
                    if (result.BoxScore != null)
                    {
                        foreach (var p in playerTeam.Roster ?? new List<Player>())
                        {
                            var pStats = result.BoxScore.GetPlayerStats(p.PlayerId);
                            if (pStats != null && pStats.Minutes > 0) playersWithMinutes++;
                        }
                    }
                    Check(playersWithMinutes >= 5, $"  {playersWithMinutes} players had minutes in box score");

                    Log($"  Result: {result.AwayScore}-{result.HomeScore} ({(playerWon ? "W" : "L")})");
                    Log($"  Record: {playerTeam.Wins}-{playerTeam.Losses}");
                }
                catch (Exception ex)
                {
                    Fail($"Game simulation threw exception: {ex.Message}");
                    Log($"  Stack: {ex.StackTrace?.Substring(0, Math.Min(300, ex.StackTrace.Length))}");
                }

                // Advance past game day
                gm.AdvanceDay();
            }

            // === PHASE 6: Post-Game Verification ===
            Log("--- PHASE 6: Post-Game Verification ---");

            // Check standings
            var standings = sc?.GetConferenceStandings(playerTeam.Conference);
            if (standings != null)
            {
                var teamStanding = standings.FirstOrDefault(s => s.TeamId == TestTeamId);
                if (teamStanding != null)
                {
                    Check(teamStanding.Wins + teamStanding.Losses > 0,
                        $"Standings: {TestTeamId} is {teamStanding.Wins}-{teamStanding.Losses} (#{standings.IndexOf(teamStanding) + 1})");
                }
                else
                {
                    Fail($"{TestTeamId} not found in standings");
                }

                int teamsWithGames = standings.Count(s => s.Wins + s.Losses > 0);
                Check(teamsWithGames > 10, $"{teamsWithGames} teams have played games in standings");
            }

            // Check season stats across roster
            if (playerTeam.Roster != null)
            {
                int rosterWithGP = playerTeam.Roster.Count(p =>
                    p.CurrentSeasonStats != null && p.CurrentSeasonStats.GamesPlayed > 0);
                Check(rosterWithGP >= 5, $"{rosterWithGP}/{playerTeam.Roster.Count} roster players have GamesPlayed > 0");

                // Top scorer
                var topScorer = playerTeam.Roster
                    .Where(p => p.CurrentSeasonStats?.GamesPlayed > 0)
                    .OrderByDescending(p => p.CurrentSeasonStats.PPG)
                    .FirstOrDefault();
                if (topScorer != null)
                {
                    Log($"Top scorer: {topScorer.FirstName} {topScorer.LastName} — {topScorer.CurrentSeasonStats.PPG:0.0} PPG");
                }
            }

            PrintSummary();
        }

        // === HELPERS ===

        private void Log(string msg) => Debug.Log($"[QA] {msg}");

        private void Pass(string msg)
        {
            _pass++;
            Debug.Log($"[QA] PASS: {msg}");
        }

        private void Fail(string msg)
        {
            _fail++;
            _failures.Add(msg);
            Debug.LogError($"[QA] FAIL: {msg}");
        }

        private void Check(bool condition, string msg)
        {
            if (condition) Pass(msg);
            else Fail(msg);
        }

        private void PrintSummary()
        {
            Log("========== QA SUMMARY ==========");
            Log($"PASS: {_pass}  |  FAIL: {_fail}");
            if (_failures.Count > 0)
            {
                Log("FAILURES:");
                for (int i = 0; i < _failures.Count; i++)
                    Log($"  #{i + 1}: {_failures[i]}");
            }
            else
            {
                Log("ALL CHECKS PASSED!");
            }
            Log("========== QA COMPLETE ==========");
        }
    }
}
