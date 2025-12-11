using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    public static class AwardManager
    {
        /// <summary>
        /// Calculates and assigns end-of-season awards.
        /// </summary>
        public static void VoteSeasonAwards(int year, List<Player> players, Dictionary<string, Team> teams)
        {
            Debug.Log($"Voting for Season {year} Awards...");
            
            // Filter eligible players (must have played games)
            var eligible = players.Where(p => p.CurrentSeasonStats != null && p.CurrentSeasonStats.GamesPlayed > 0).ToList();
            if (eligible.Count == 0) return;

            // 1. MVP
            VoteMVP(year, eligible, teams);
            
            // 2. DPOY
            VoteDPOY(year, eligible);
            
            // 3. Rookie of the Year
            VoteRookieOfTheYear(year, eligible);
            
            // 4. Sixth Man
            VoteSixthMan(year, eligible);
            
            // 5. All-NBA Teams (1st, 2nd, 3rd)
            VoteAllNBATeams(year, eligible);
            
            // 6. All-Defensive Teams
            VoteAllDefensiveTeams(year, eligible);
            
            // 7. All-Rookie Teams
            VoteAllRookieTeams(year, eligible);
        }

        private static void VoteMVP(int year, List<Player> players, Dictionary<string, Team> teams)
        {
            // Score = PER*0.3 + WS*0.3 + (PPG/2)*0.2 + (TeamWinPct*20)*0.2
            var candidates = players.OrderByDescending(p => CalculateMVPScore(p, teams)).Take(5).ToList();
            if (candidates.Count > 0)
            {
                var winner = candidates[0];
                winner.Awards.Add(new AwardHistory(year, AwardType.MVP, winner.TeamId));
                Debug.Log($"MVP: {winner.FullName} - Score: {CalculateMVPScore(winner, teams):F1}");
            }
        }

        private static float CalculateMVPScore(Player p, Dictionary<string, Team> teams)
        {
            var stats = p.CurrentSeasonStats;
            float winPct = teams.ContainsKey(p.TeamId) ? teams[p.TeamId].WinPercentage : 0.5f;
            
            return (stats.PER * 0.3f) + 
                   (stats.WinShares * 0.3f) + 
                   (stats.PPG * 0.5f * 0.2f) + 
                   (winPct * 20f * 0.2f);
        }

        private static void VoteDPOY(int year, List<Player> players)
        {
            // Score based on DBPM, DWS, Blocks, Steals
            var winner = players.OrderByDescending(p => 
                (p.CurrentSeasonStats.DefensiveBPM * 2) + 
                (p.CurrentSeasonStats.DefensiveWinShares * 3) +
                p.CurrentSeasonStats.BPG + 
                p.CurrentSeasonStats.SPG
            ).FirstOrDefault();

            if (winner != null)
            {
                winner.Awards.Add(new AwardHistory(year, AwardType.DefensivePlayerOfYear, winner.TeamId));
                Debug.Log($"DPOY: {winner.FullName}");
            }
        }

        private static void VoteRookieOfTheYear(int year, List<Player> players)
        {
            var rookies = players.Where(p => p.YearsPro == 0).ToList();
            var winner = rookies.OrderByDescending(p => 
                p.CurrentSeasonStats.PER + p.CurrentSeasonStats.PPG
            ).FirstOrDefault();

            if (winner != null)
            {
                winner.Awards.Add(new AwardHistory(year, AwardType.RookieOfYear, winner.TeamId));
                Debug.Log($"ROTY: {winner.FullName}");
            }
        }

        private static void VoteSixthMan(int year, List<Player> players)
        {
            // Must come off bench in >50% of games played
            var benchPlayers = players.Where(p => 
                (float)p.CurrentSeasonStats.GamesStarted / p.CurrentSeasonStats.GamesPlayed < 0.5f
            ).ToList();

            var winner = benchPlayers.OrderByDescending(p => 
                p.CurrentSeasonStats.PPG + p.CurrentSeasonStats.PER
            ).FirstOrDefault();

            if (winner != null)
            {
                winner.Awards.Add(new AwardHistory(year, AwardType.SixthManOfYear, winner.TeamId));
                Debug.Log($"6MOY: {winner.FullName}");
            }
        }

        private static void VoteAllNBATeams(int year, List<Player> players)
        {
            // Sort by MVP score
            // Need 2 Guards, 2 Forwards, 1 Center per team
            // 3 Teams total
            
            var sorted = players.OrderByDescending(p => p.CurrentSeasonStats.PER).ToList(); // Simplified ranking
            
            AssignTeam(year, AwardType.AllNBAFirstTeam, sorted, new HashSet<string>());
            AssignTeam(year, AwardType.AllNBASecondTeam, sorted, GetAwardWinners(year, AwardType.AllNBAFirstTeam, players));
            AssignTeam(year, AwardType.AllNBAThirdTeam, sorted, GetAwardWinners(year, AwardType.AllNBAFirstTeam, players).Union(GetAwardWinners(year, AwardType.AllNBASecondTeam, players)).ToHashSet());
        }

        private static void AssignTeam(int year, AwardType award, List<Player> ranked, HashSet<string> excludeIds)
        {
            int guards = 0, forwards = 0, centers = 0;
            
            foreach (var p in ranked)
            {
                if (excludeIds.Contains(p.PlayerId)) continue;
                if (guards >= 2 && forwards >= 2 && centers >= 1) break;

                bool added = false;
                if ((p.Position == NBAHeadCoach.Core.Data.Position.PointGuard || p.Position == NBAHeadCoach.Core.Data.Position.ShootingGuard) && guards < 2)
                {
                    guards++;
                    added = true;
                }
                else if ((p.Position == NBAHeadCoach.Core.Data.Position.SmallForward || p.Position == NBAHeadCoach.Core.Data.Position.PowerForward) && forwards < 2)
                {
                    forwards++;
                    added = true;
                }
                else if (p.Position == NBAHeadCoach.Core.Data.Position.Center && centers < 1)
                {
                    centers++;
                    added = true;
                }

                if (added)
                {
                    p.Awards.Add(new AwardHistory(year, award, p.TeamId));
                    excludeIds.Add(p.PlayerId);
                }
            }
        }
        
        private static HashSet<string> GetAwardWinners(int year, AwardType type, List<Player> players)
        {
            return players.Where(p => p.Awards.Any(a => a.Year == year && a.Type == type))
                          .Select(p => p.PlayerId)
                          .ToHashSet();
        }

        private static void VoteAllDefensiveTeams(int year, List<Player> players)
        {
            // Similar logic but sorting by Defensive Metrics
            var sorted = players.OrderByDescending(p => p.CurrentSeasonStats.DefensiveBPM + p.CurrentSeasonStats.DefensiveWinShares).ToList();
            AssignTeam(year, AwardType.AllDefensiveFirstTeam, sorted, new HashSet<string>());
            AssignTeam(year, AwardType.AllDefensiveSecondTeam, sorted, GetAwardWinners(year, AwardType.AllDefensiveFirstTeam, players));
        }

        private static void VoteAllRookieTeams(int year, List<Player> players)
        {
             var rookies = players.Where(p => p.YearsPro == 0)
                                  .OrderByDescending(p => p.CurrentSeasonStats.PER)
                                  .ToList();
             
             // All Rookie doesn't strictly follow position rules, usually best 5
             // But let's reuse AssignTeam for structure or just pick top 5
             for(int i=0; i<5 && i<rookies.Count; i++) rookies[i].Awards.Add(new AwardHistory(year, AwardType.AllRookieFirstTeam, rookies[i].TeamId));
             for(int i=5; i<10 && i<rookies.Count; i++) rookies[i].Awards.Add(new AwardHistory(year, AwardType.AllRookieSecondTeam, rookies[i].TeamId));
        }
    }
}
