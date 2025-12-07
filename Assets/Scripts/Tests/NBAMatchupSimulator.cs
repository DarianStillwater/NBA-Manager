using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Simulates games between real NBA teams using NBAPlayerData.
    /// </summary>
    public class NBAMatchupSimulator : MonoBehaviour
    {
        [Header("Matchup Settings")]
        [SerializeField] private string _homeTeamId = "LAL";
        [SerializeField] private string _awayTeamId = "GSW";
        [SerializeField] private int _gamesToSimulate = 10;
        [SerializeField] private bool _runOnStart = true;

        private void Start()
        {
            if (_runOnStart)
            {
                SimulateMatchup(_homeTeamId, _awayTeamId, _gamesToSimulate);
            }
        }

        [ContextMenu("Simulate Warriors vs Lakers")]
        public void SimulateWarriorsVsLakers()
        {
            SimulateMatchup("LAL", "GSW", 10);
        }

        public void SimulateMatchup(string homeTeamId, string awayTeamId, int games)
        {
            Debug.Log($"\n{'=',-50}");
            Debug.Log($"  {GetTeamName(awayTeamId)} @ {GetTeamName(homeTeamId)} - {games} Game Series");
            Debug.Log($"{'=',-50}\n");

            // Get real NBA players
            var allPlayers = NBAPlayerData.GetAllPlayers();
            var homePlayers = allPlayers.Where(p => p.TeamId == homeTeamId).ToList();
            var awayPlayers = allPlayers.Where(p => p.TeamId == awayTeamId).ToList();

            if (homePlayers.Count < 5 || awayPlayers.Count < 5)
            {
                Debug.LogError($"Not enough players! Home: {homePlayers.Count}, Away: {awayPlayers.Count}");
                return;
            }

            // Print rosters
            Debug.Log($"--- {GetTeamName(homeTeamId)} Roster ---");
            foreach (var p in homePlayers.OrderByDescending(x => x.OverallRating).Take(5))
            {
                Debug.Log($"  {p.FirstName} {p.LastName} ({GetPositionName(p.Position)}) - {p.OverallRating} OVR");
            }

            Debug.Log($"\n--- {GetTeamName(awayTeamId)} Roster ---");
            foreach (var p in awayPlayers.OrderByDescending(x => x.OverallRating).Take(5))
            {
                Debug.Log($"  {p.FirstName} {p.LastName} ({GetPositionName(p.Position)}) - {p.OverallRating} OVR");
            }

            // Create Player objects for simulation
            var homeRoster = CreateRoster(homePlayers);
            var awayRoster = CreateRoster(awayPlayers);

            // Create teams
            var homeTeam = new Team
            {
                TeamId = homeTeamId,
                City = GetTeamCity(homeTeamId),
                Nickname = GetTeamName(homeTeamId),
                RosterPlayerIds = homeRoster.Select(p => p.PlayerId).ToList()
            };
            // Set starters
            for (int i = 0; i < 5 && i < homeRoster.Count; i++)
            {
                homeTeam.StartingLineupIds[i] = homeRoster[i].PlayerId;
            }

            var awayTeam = new Team
            {
                TeamId = awayTeamId,
                City = GetTeamCity(awayTeamId),
                Nickname = GetTeamName(awayTeamId),
                RosterPlayerIds = awayRoster.Select(p => p.PlayerId).ToList()
            };
            // Set starters
            for (int i = 0; i < 5 && i < awayRoster.Count; i++)
            {
                awayTeam.StartingLineupIds[i] = awayRoster[i].PlayerId;
            }

            // Create player database with these players
            var playerDb = new PlayerDatabase();
            foreach (var p in homeRoster.Concat(awayRoster))
            {
                playerDb.AddPlayer(p);
            }

            Debug.Log($"\n--- Game Results ---\n");

            // Track stats
            int homeWins = 0;
            int totalHomePoints = 0;
            int totalAwayPoints = 0;
            int highestScore = 0;
            string highScoreGame = "";
            int overtimeGames = 0;

            for (int i = 0; i < games; i++)
            {
                var simulator = new GameSimulator(playerDb, seed: i);
                var result = simulator.SimulateGame(homeTeam, awayTeam);

                string venue = i % 2 == 0 ? "(Home)" : "(Away)";
                bool homeWon = result.WinnerTeamId == homeTeamId;
                
                Debug.Log($"Game {i + 1}: {GetTeamAbbr(awayTeamId)} {result.AwayScore} - {result.HomeScore} {GetTeamAbbr(homeTeamId)} " +
                         $"{(result.WentToOvertime ? "(OT) " : "")}" +
                         $"[{(homeWon ? GetTeamAbbr(homeTeamId) : GetTeamAbbr(awayTeamId))} Win]");

                totalHomePoints += result.HomeScore;
                totalAwayPoints += result.AwayScore;
                if (homeWon) homeWins++;
                if (result.WentToOvertime) overtimeGames++;

                int totalPoints = result.HomeScore + result.AwayScore;
                if (totalPoints > highestScore)
                {
                    highestScore = totalPoints;
                    highScoreGame = $"Game {i + 1}: {result.AwayScore}-{result.HomeScore}";
                }
            }

            // Summary
            Debug.Log($"\n{'=',-50}");
            Debug.Log($"  SERIES SUMMARY");
            Debug.Log($"{'=',-50}");
            Debug.Log($"  {GetTeamName(homeTeamId)}: {homeWins} wins");
            Debug.Log($"  {GetTeamName(awayTeamId)}: {games - homeWins} wins");
            Debug.Log($"\n  Avg {GetTeamAbbr(homeTeamId)} Score: {totalHomePoints / (float)games:F1}");
            Debug.Log($"  Avg {GetTeamAbbr(awayTeamId)} Score: {totalAwayPoints / (float)games:F1}");
            Debug.Log($"  Highest Scoring: {highScoreGame} ({highestScore} total)");
            Debug.Log($"  Overtime Games: {overtimeGames}");
            Debug.Log($"{'=',-50}\n");
        }

        private List<Player> CreateRoster(List<PlayerData> data)
        {
            return data
                .OrderByDescending(p => p.OverallRating)
                .Select(p => ConvertToPlayer(p))
                .ToList();
        }

        private Player ConvertToPlayer(PlayerData data)
        {
            return new Player
            {
                PlayerId = data.PlayerId,
                FirstName = data.FirstName,
                LastName = data.LastName,
                JerseyNumber = data.JerseyNumber,
                Position = (Position)data.Position,
                Age = data.Age,
                HeightInches = data.HeightInches,
                WeightLbs = data.WeightLbs,
                TeamId = data.TeamId,
                
                Finishing_Rim = data.Finishing_Rim,
                Finishing_PostMoves = data.Finishing_PostMoves,
                Shot_Close = data.Shot_Close,
                Shot_MidRange = data.Shot_MidRange,
                Shot_Three = data.Shot_Three,
                FreeThrow = data.FreeThrow,
                Passing = data.Passing,
                BallHandling = data.BallHandling,
                OffensiveIQ = data.OffensiveIQ,
                SpeedWithBall = data.SpeedWithBall,
                
                Defense_Perimeter = data.Defense_Perimeter,
                Defense_Interior = data.Defense_Interior,
                Defense_PostDefense = data.Defense_PostDefense,
                Steal = data.Steal,
                Block = data.Block,
                DefensiveIQ = data.DefensiveIQ,
                DefensiveRebound = data.DefensiveRebound,
                
                Speed = data.Speed,
                Acceleration = data.Acceleration,
                Strength = data.Strength,
                Vertical = data.Vertical,
                Stamina = data.Stamina,
                
                BasketballIQ = data.BasketballIQ,
                Clutch = data.Clutch,
                Leadership = data.Leadership,
                
                Energy = 100,
                Morale = 75,
                Form = 70
            };
        }

        private string GetTeamName(string teamId) => teamId switch
        {
            "LAL" => "Lakers",
            "GSW" => "Warriors",
            "BOS" => "Celtics",
            "MIL" => "Bucks",
            "DEN" => "Nuggets",
            "PHX" => "Suns",
            "MIA" => "Heat",
            "PHI" => "76ers",
            _ => teamId
        };

        private string GetTeamCity(string teamId) => teamId switch
        {
            "LAL" => "Los Angeles",
            "GSW" => "Golden State",
            "BOS" => "Boston",
            "MIL" => "Milwaukee",
            "DEN" => "Denver",
            _ => teamId
        };

        private string GetTeamAbbr(string teamId) => teamId;

        private string GetPositionName(int pos) => pos switch
        {
            1 => "PG",
            2 => "SG",
            3 => "SF",
            4 => "PF",
            5 => "C",
            _ => "?"
        };
    }
}
