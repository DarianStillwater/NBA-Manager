using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    public class AwardSystemTest : MonoBehaviour
    {
        private List<Player> _players;
        private Dictionary<string, Team> _teams;

        void Start()
        {
            SetupTestEnvironment();
            RunTest();
        }

        private void SetupTestEnvironment()
        {
            _players = new List<Player>();
            _teams = new Dictionary<string, Team>();

            // Create a team
            var team = new Team { TeamId = "LAL", Nickname = "Lakers", Wins = 60, Losses = 22 };
            _teams.Add("LAL", team);
            
            var team2 = new Team { TeamId = "BOS", Nickname = "Celtics", Wins = 50, Losses = 32 };
            _teams.Add("BOS", team2);

            // 1. MVP Candidate (High Stats + Winning Team)
            var mvp = CreatePlayer("LeBron", "James", "LAL", 10, Position.SmallForward);
            SetStats(mvp, 82, 30.0f, 8.0f, 8.0f, 30.0f, 10.0f, 0.5f, 5.0f); // 30 PPG, 30 PER, 10 WS
            _players.Add(mvp);

            // 2. Good Player (Good Stats + Losing Team)
            var star = CreatePlayer("Jayson", "Tatum", "BOS", 5, Position.SmallForward);
            SetStats(star, 80, 28.0f, 7.0f, 5.0f, 25.0f, 8.0f, 0.5f, 4.0f);
            _players.Add(star);

            // 3. DPOY Candidate (High Def metrics)
            var dpoy = CreatePlayer("Rudy", "Gobert", "BOS", 8, Position.Center);
            SetStats(dpoy, 75, 12.0f, 14.0f, 1.0f, 18.0f, 7.0f, 3.5f, 6.0f); // High Blocks, High DBPM, High DWS
            _players.Add(dpoy);

            // 4. Rookie (Good stats)
            var rookie = CreatePlayer("Victor", "Wembanyama", "LAL", 0, Position.Center);
            SetStats(rookie, 70, 20.0f, 10.0f, 3.0f, 22.0f, 5.0f, 2.5f, 4.0f);
            _players.Add(rookie);
            
            // 5. Reserve (6th Man)
            var sixth = CreatePlayer("Manu", "Ginobili", "LAL", 12, Position.ShootingGuard);
            SetStats(sixth, 75, 18.0f, 4.0f, 5.0f, 20.0f, 6.0f, 0.5f, 2.0f, starts: 10); // Mostly off bench
            _players.Add(sixth);
        }

        private Player CreatePlayer(string first, string last, string teamId, int yearsPro, Position pos)
        {
            var p = new Player 
            { 
                PlayerId = first+last, 
                FirstName = first, 
                LastName = last, 
                TeamId = teamId, 
                YearsPro = yearsPro,
                Position = pos 
            };
            // Create stats object
            p.CareerStats.Add(new SeasonStats(2025, teamId));
            return p;
        }

        private void SetStats(Player p, int gp, float ppg, float rpg, float apg, float per, float ws, float dbpm, float dws, int starts = -1)
        {
            var s = p.CurrentSeasonStats;
            s.GamesPlayed = gp;
            if (starts == -1) s.GamesStarted = gp;
            else s.GamesStarted = starts;
            
            s.Points = (int)(ppg * gp);
            s.TotalRebounds = (int)(rpg * gp); // Hack: sets total, doesn't split O/D yet properly for this test
            s.Assists = (int)(apg * gp);
            
            s.PER = per;
            s.WinShares = ws;
            s.DefensiveBPM = dbpm;
            s.DefensiveWinShares = dws;
        }

        private void RunTest()
        {
            Debug.Log("Starting Award System Test...");
            
            AwardManager.VoteSeasonAwards(2025, _players, _teams);
            
            // Verification
            VerifyAward(AwardType.MVP, "LeBronJames");
            VerifyAward(AwardType.DefensivePlayerOfYear, "RudyGobert");
            VerifyAward(AwardType.RookieOfYear, "VictorWembanyama");
            VerifyAward(AwardType.SixthManOfYear, "ManuGinobili");
            
            // Check Supermax Eligibility for MVP
            var bron = _players.Find(p => p.PlayerId == "LeBronJames");
            bool isEligible = LeagueCBA.IsSuperMaxEligible(bron, 2026);
            Debug.Log($"LeBron Supermax Eligible (Expect True): {isEligible}"); // 10 years pro, just won MVP
            
            var wemby = _players.Find(p => p.PlayerId == "VictorWembanyama");
            bool wembyEligible = LeagueCBA.IsSuperMaxEligible(wemby, 2026);
            Debug.Log($"Wembanyama Supermax Eligible (Expect False): {wembyEligible}"); // Rookie
        }

        private void VerifyAward(AwardType type, string expectedId)
        {
            var winner = _players.Find(p => p.Awards.Exists(a => a.Type == type && a.Year == 2025));
            if (winner != null && winner.PlayerId == expectedId)
            {
                Debug.Log($"PASS: {type} awarded to {winner.FullName}");
            }
            else
            {
                Debug.LogError($"FAIL: {type} awarded to {winner?.FullName ?? "None"}, expected {expectedId}");
            }
        }
    }
}
