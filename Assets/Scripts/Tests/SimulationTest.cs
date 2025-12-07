using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Test script to verify the simulation engine produces realistic results.
    /// Attach to a GameObject and press Play to run tests in the Unity console.
    /// </summary>
    public class SimulationTest : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private int _gamesToSimulate = 10;
        [SerializeField] private bool _runOnStart = true;

        private PlayerDatabase _playerDatabase;

        private void Start()
        {
            if (_runOnStart)
            {
                RunAllTests();
            }
        }

        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            Debug.Log("=== NBA HEAD COACH SIMULATION TEST ===\n");

            // Initialize database
            _playerDatabase = new PlayerDatabase();
            _playerDatabase.LoadAllPlayers();
            Debug.Log($"Loaded {_playerDatabase.PlayerCount} players from database.");

            if (_playerDatabase.PlayerCount < 10)
            {
                Debug.LogError("Not enough players loaded! Need at least 10 for a game.");
                return;
            }

            // Create test teams
            var homeTeam = CreateTestTeam("TEAM_A", "Metro City", "Thunder");
            var awayTeam = CreateTestTeam("TEAM_B", "Bay Area", "Waves");

            Debug.Log($"\n--- Simulating {_gamesToSimulate} games ---\n");

            // Track aggregate stats
            int totalHomeWins = 0;
            int totalHomePoints = 0;
            int totalAwayPoints = 0;
            int totalOvertime = 0;

            for (int i = 0; i < _gamesToSimulate; i++)
            {
                var simulator = new GameSimulator(_playerDatabase, seed: i);
                var result = simulator.SimulateGame(homeTeam, awayTeam);

                Debug.Log($"Game {i + 1}: {result.HomeScore} - {result.AwayScore}" + 
                         (result.WentToOvertime ? " (OT)" : ""));

                totalHomePoints += result.HomeScore;
                totalAwayPoints += result.AwayScore;
                if (result.WinnerTeamId == homeTeam.TeamId) totalHomeWins++;
                if (result.WentToOvertime) totalOvertime++;

                // Print detailed box score for first game
                if (i == 0)
                {
                    Debug.Log("\n" + result.BoxScore.ToFormattedString(GetPlayerName));
                }
            }

            // Print summary
            Debug.Log("\n=== SUMMARY ===");
            Debug.Log($"Home Wins: {totalHomeWins} / {_gamesToSimulate}");
            Debug.Log($"Avg Home Score: {totalHomePoints / (float)_gamesToSimulate:F1}");
            Debug.Log($"Avg Away Score: {totalAwayPoints / (float)_gamesToSimulate:F1}");
            Debug.Log($"Overtime Games: {totalOvertime}");

            // Validate realistic ranges
            float avgScore = (totalHomePoints + totalAwayPoints) / (float)(_gamesToSimulate * 2);
            bool realisticScoring = avgScore >= 95 && avgScore <= 125;
            Debug.Log($"\nAverage Score: {avgScore:F1} - {(realisticScoring ? "✓ REALISTIC" : "✗ UNREALISTIC")}");

            // Test spatial tracking
            Debug.Log("\n--- Testing Spatial Tracking ---");
            var testSim = new GameSimulator(_playerDatabase, seed: 42);
            var testResult = testSim.SimulateGame(homeTeam, awayTeam);
            Debug.Log($"Spatial states recorded: {testSim.GameTracker.GameStates.Count}");
            
            // Generate heatmap for top scorer
            var topScorer = GetTopScorer(testResult);
            if (topScorer != null)
            {
                var heatmap = testSim.GameTracker.GenerateHeatmap(topScorer);
                Debug.Log($"Heatmap generated for top scorer (10x5 grid)");
                LogHeatmapVisualization(heatmap);
            }

            Debug.Log("\n=== TESTS COMPLETE ===");
        }

        private Team CreateTestTeam(string teamId, string city, string nickname)
        {
            var team = new Team
            {
                TeamId = teamId,
                City = city,
                Nickname = nickname,
                Abbreviation = teamId.Substring(teamId.Length - 1),
                OffensiveStrategy = new TeamStrategy
                {
                    Pace = 55,
                    ThreePointFrequency = 60,
                    PickAndRollFrequency = 50
                },
                DefensiveStrategy = new TeamStrategy
                {
                    DefensiveAggression = 50
                }
            };

            // Get players for this team from the database
            var players = new System.Collections.Generic.List<Player>();
            foreach (var player in _playerDatabase.Players.Values)
            {
                if (player.TeamId == teamId)
                {
                    players.Add(player);
                    team.RosterPlayerIds.Add(player.PlayerId);
                }
            }

            // Sort by position and set starters (PG, SG, SF, PF, C)
            players.Sort((a, b) => a.Position.CompareTo(b.Position));
            for (int i = 0; i < Mathf.Min(5, players.Count); i++)
            {
                team.StartingLineupIds[i] = players[i].PlayerId;
            }

            Debug.Log($"Created team {team.FullName} with {team.RosterPlayerIds.Count} players");
            return team;
        }

        private string GetPlayerName(string playerId)
        {
            var player = _playerDatabase.GetPlayer(playerId);
            return player != null ? player.FullName : playerId;
        }

        private string GetTopScorer(GameResult result)
        {
            string topScorer = null;
            int topPoints = 0;

            foreach (var stat in result.BoxScore.PlayerStats)
            {
                if (stat.Value.Points > topPoints)
                {
                    topPoints = stat.Value.Points;
                    topScorer = stat.Key;
                }
            }

            if (topScorer != null)
            {
                Debug.Log($"Top Scorer: {GetPlayerName(topScorer)} with {topPoints} points");
            }

            return topScorer;
        }

        private void LogHeatmapVisualization(float[,] heatmap)
        {
            int width = heatmap.GetLength(0);
            int height = heatmap.GetLength(1);
            float maxValue = 0;

            // Find max for normalization
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (heatmap[x, y] > maxValue)
                        maxValue = heatmap[x, y];
                }
            }

            // Print ASCII heatmap
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("\nHeatmap (time spent in zone):");
            
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    float normalized = maxValue > 0 ? heatmap[x, y] / maxValue : 0;
                    char c = normalized > 0.7f ? '█' : normalized > 0.4f ? '▓' : normalized > 0.1f ? '░' : ' ';
                    sb.Append(c);
                }
                sb.AppendLine();
            }
            
            Debug.Log(sb.ToString());
        }
    }
}
