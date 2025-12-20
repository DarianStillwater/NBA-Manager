using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages end-of-season award voting with realistic NBA-style criteria.
    /// Includes MVP, DPOY, ROY, 6MOY, MIP, COY and team selections.
    /// Also handles monthly/weekly awards and playoff awards.
    /// </summary>
    public static class AwardManager
    {
        // Track previous season stats for MIP calculation
        private static Dictionary<string, PlayerSeasonStats> _previousSeasonStats = new Dictionary<string, PlayerSeasonStats>();

        #region Season End Awards

        /// <summary>
        /// Store current season stats before advancing to next season (for MIP calculation)
        /// </summary>
        public static void StorePreviousSeasonStats(List<Player> players)
        {
            _previousSeasonStats.Clear();
            foreach (var p in players)
            {
                if (p.CurrentSeasonStats != null && p.CurrentSeasonStats.GamesPlayed >= 20)
                {
                    _previousSeasonStats[p.PlayerId] = CloneStats(p.CurrentSeasonStats);
                }
            }
            Debug.Log($"[AwardManager] Stored previous season stats for {_previousSeasonStats.Count} players");
        }

        private static PlayerSeasonStats CloneStats(PlayerSeasonStats original)
        {
            return new PlayerSeasonStats
            {
                PlayerId = original.PlayerId,
                Season = original.Season,
                GamesPlayed = original.GamesPlayed,
                GamesStarted = original.GamesStarted,
                Minutes = original.Minutes,
                Points = original.Points,
                Rebounds = original.Rebounds,
                Assists = original.Assists,
                Steals = original.Steals,
                Blocks = original.Blocks,
                Turnovers = original.Turnovers,
                FGM = original.FGM,
                FGA = original.FGA,
                ThreePM = original.ThreePM,
                ThreePA = original.ThreePA,
                FTM = original.FTM,
                FTA = original.FTA,
                OffensiveRebounds = original.OffensiveRebounds,
                DefensiveRebounds = original.DefensiveRebounds,
                PersonalFouls = original.PersonalFouls,
                PlusMinus = original.PlusMinus
            };
        }

        /// <summary>
        /// Calculates and assigns end-of-season awards.
        /// </summary>
        public static AwardVotingResults VoteSeasonAwards(int year, List<Player> players, Dictionary<string, Team> teams)
        {
            Debug.Log($"[AwardManager] Voting for Season {year} Awards...");

            var results = new AwardVotingResults { Year = year };

            // Filter eligible players (must have played games)
            var eligible = players.Where(p => p.CurrentSeasonStats != null && p.CurrentSeasonStats.GamesPlayed >= 41).ToList();
            if (eligible.Count == 0)
            {
                Debug.LogWarning("[AwardManager] No eligible players for awards!");
                return results;
            }

            // 1. MVP
            results.MVP = VoteMVP(year, eligible, teams);

            // 2. DPOY
            results.DPOY = VoteDPOY(year, eligible);

            // 3. Rookie of the Year
            results.ROTY = VoteRookieOfTheYear(year, eligible);

            // 4. Sixth Man
            results.SixthMan = VoteSixthMan(year, eligible);

            // 5. Most Improved Player
            results.MIP = VoteMostImproved(year, eligible);

            // 6. All-NBA Teams (1st, 2nd, 3rd)
            results.AllNBAFirst = VoteAllNBATeams(year, eligible, AwardType.AllNBAFirstTeam, null);
            results.AllNBASecond = VoteAllNBATeams(year, eligible, AwardType.AllNBASecondTeam, results.AllNBAFirst);
            var combined = results.AllNBAFirst.Concat(results.AllNBASecond).ToList();
            results.AllNBAThird = VoteAllNBATeams(year, eligible, AwardType.AllNBAThirdTeam, combined);

            // 7. All-Defensive Teams
            results.AllDefenseFirst = VoteAllDefensiveTeams(year, eligible, AwardType.AllDefensiveFirstTeam, null);
            results.AllDefenseSecond = VoteAllDefensiveTeams(year, eligible, AwardType.AllDefensiveSecondTeam, results.AllDefenseFirst);

            // 8. All-Rookie Teams
            VoteAllRookieTeams(year, eligible);

            Debug.Log($"[AwardManager] Season {year} awards voting complete.");
            return results;
        }

        private static Player VoteMVP(int year, List<Player> players, Dictionary<string, Team> teams)
        {
            // Score = PER*0.3 + WS*0.3 + (PPG/2)*0.2 + (TeamWinPct*20)*0.2
            var candidates = players.OrderByDescending(p => CalculateMVPScore(p, teams)).Take(5).ToList();
            if (candidates.Count > 0)
            {
                var winner = candidates[0];
                winner.Awards.Add(new AwardHistory(year, AwardType.MVP, winner.TeamId));
                Debug.Log($"[AwardManager] MVP: {winner.FullName} - Score: {CalculateMVPScore(winner, teams):F1}");
                return winner;
            }
            return null;
        }

        private static float CalculateMVPScore(Player p, Dictionary<string, Team> teams)
        {
            var stats = p.CurrentSeasonStats;
            if (stats == null) return 0;

            float winPct = teams.ContainsKey(p.TeamId) ? teams[p.TeamId].WinPercentage : 0.5f;

            return (stats.PER * 0.3f) +
                   (stats.WinShares * 0.3f) +
                   (stats.PPG * 0.5f * 0.2f) +
                   (winPct * 20f * 0.2f);
        }

        private static Player VoteDPOY(int year, List<Player> players)
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
                Debug.Log($"[AwardManager] DPOY: {winner.FullName}");
            }
            return winner;
        }

        private static Player VoteRookieOfTheYear(int year, List<Player> players)
        {
            var rookies = players.Where(p => p.YearsPro == 0 && p.CurrentSeasonStats.GamesPlayed >= 30).ToList();
            var winner = rookies.OrderByDescending(p =>
                p.CurrentSeasonStats.PER + p.CurrentSeasonStats.PPG
            ).FirstOrDefault();

            if (winner != null)
            {
                winner.Awards.Add(new AwardHistory(year, AwardType.RookieOfYear, winner.TeamId));
                Debug.Log($"[AwardManager] ROTY: {winner.FullName}");
            }
            return winner;
        }

        private static Player VoteSixthMan(int year, List<Player> players)
        {
            // Must come off bench in >50% of games played
            var benchPlayers = players.Where(p =>
                p.CurrentSeasonStats.GamesPlayed > 0 &&
                (float)p.CurrentSeasonStats.GamesStarted / p.CurrentSeasonStats.GamesPlayed < 0.5f
            ).ToList();

            var winner = benchPlayers.OrderByDescending(p =>
                p.CurrentSeasonStats.PPG + p.CurrentSeasonStats.PER
            ).FirstOrDefault();

            if (winner != null)
            {
                winner.Awards.Add(new AwardHistory(year, AwardType.SixthManOfYear, winner.TeamId));
                Debug.Log($"[AwardManager] 6MOY: {winner.FullName}");
            }
            return winner;
        }

        private static Player VoteMostImproved(int year, List<Player> players)
        {
            // MIP: Greatest improvement from previous season
            // Must have played 50+ games last season and this season
            // Compare PPG, PER, and usage rate improvements

            var candidates = new List<(Player player, float improvement)>();

            foreach (var p in players)
            {
                if (!_previousSeasonStats.ContainsKey(p.PlayerId)) continue;

                var prevStats = _previousSeasonStats[p.PlayerId];
                var currStats = p.CurrentSeasonStats;

                if (prevStats.GamesPlayed < 50 || currStats.GamesPlayed < 50) continue;

                // Calculate improvement score
                float ppgImprovement = currStats.PPG - prevStats.PPG;
                float perImprovement = currStats.PER - prevStats.PER;
                float rpgImprovement = currStats.RPG - prevStats.RPG;
                float apgImprovement = currStats.APG - prevStats.APG;

                // Weighted improvement score
                float improvement = (ppgImprovement * 2f) + (perImprovement * 1.5f) +
                                   (rpgImprovement * 0.5f) + (apgImprovement * 0.5f);

                // Must show meaningful improvement
                if (ppgImprovement >= 3f || perImprovement >= 5f)
                {
                    candidates.Add((p, improvement));
                }
            }

            var winner = candidates.OrderByDescending(c => c.improvement).FirstOrDefault().player;

            if (winner != null)
            {
                winner.Awards.Add(new AwardHistory(year, AwardType.MostImprovedPlayer, winner.TeamId));
                var prev = _previousSeasonStats[winner.PlayerId];
                Debug.Log($"[AwardManager] MIP: {winner.FullName} ({prev.PPG:F1} → {winner.CurrentSeasonStats.PPG:F1} PPG)");
            }

            return winner;
        }

        private static List<Player> VoteAllNBATeams(int year, List<Player> players, AwardType teamType, List<Player> exclude)
        {
            var excludeIds = exclude?.Select(p => p.PlayerId).ToHashSet() ?? new HashSet<string>();
            var sorted = players.Where(p => !excludeIds.Contains(p.PlayerId))
                               .OrderByDescending(p => p.CurrentSeasonStats.PER)
                               .ToList();

            var team = new List<Player>();
            int guards = 0, forwards = 0, centers = 0;

            foreach (var p in sorted)
            {
                if (guards >= 2 && forwards >= 2 && centers >= 1) break;

                bool added = false;
                var pos = p.Position;

                if ((pos == Position.PointGuard || pos == Position.ShootingGuard) && guards < 2)
                {
                    guards++;
                    added = true;
                }
                else if ((pos == Position.SmallForward || pos == Position.PowerForward) && forwards < 2)
                {
                    forwards++;
                    added = true;
                }
                else if (pos == Position.Center && centers < 1)
                {
                    centers++;
                    added = true;
                }

                if (added)
                {
                    p.Awards.Add(new AwardHistory(year, teamType, p.TeamId));
                    team.Add(p);
                }
            }

            return team;
        }

        private static List<Player> VoteAllDefensiveTeams(int year, List<Player> players, AwardType teamType, List<Player> exclude)
        {
            var excludeIds = exclude?.Select(p => p.PlayerId).ToHashSet() ?? new HashSet<string>();
            var sorted = players.Where(p => !excludeIds.Contains(p.PlayerId))
                               .OrderByDescending(p => p.CurrentSeasonStats.DefensiveBPM + p.CurrentSeasonStats.DefensiveWinShares)
                               .ToList();

            var team = new List<Player>();
            int guards = 0, forwards = 0, centers = 0;

            foreach (var p in sorted)
            {
                if (guards >= 2 && forwards >= 2 && centers >= 1) break;

                bool added = false;
                var pos = p.Position;

                if ((pos == Position.PointGuard || pos == Position.ShootingGuard) && guards < 2)
                {
                    guards++;
                    added = true;
                }
                else if ((pos == Position.SmallForward || pos == Position.PowerForward) && forwards < 2)
                {
                    forwards++;
                    added = true;
                }
                else if (pos == Position.Center && centers < 1)
                {
                    centers++;
                    added = true;
                }

                if (added)
                {
                    p.Awards.Add(new AwardHistory(year, teamType, p.TeamId));
                    team.Add(p);
                }
            }

            return team;
        }

        private static void VoteAllRookieTeams(int year, List<Player> players)
        {
            var rookies = players.Where(p => p.YearsPro == 0)
                                 .OrderByDescending(p => p.CurrentSeasonStats.PER)
                                 .ToList();

            for (int i = 0; i < 5 && i < rookies.Count; i++)
            {
                rookies[i].Awards.Add(new AwardHistory(year, AwardType.AllRookieFirstTeam, rookies[i].TeamId));
            }
            for (int i = 5; i < 10 && i < rookies.Count; i++)
            {
                rookies[i].Awards.Add(new AwardHistory(year, AwardType.AllRookieSecondTeam, rookies[i].TeamId));
            }
        }

        #endregion

        #region Coach of the Year

        /// <summary>
        /// Vote for Coach of the Year (called separately, needs team performance data)
        /// </summary>
        public static string VoteCoachOfYear(int year, Dictionary<string, Team> teams, Dictionary<string, int> preseasonWinProjections = null)
        {
            // COY often goes to coaches who exceeded expectations
            // Score = (Actual Wins - Projected Wins) * 2 + Total Wins * 0.5

            string winnerTeamId = null;
            float bestScore = float.MinValue;

            foreach (var kvp in teams)
            {
                var team = kvp.Value;
                int projectedWins = preseasonWinProjections?.GetValueOrDefault(team.TeamId, 41) ?? 41;
                int actualWins = team.Wins;
                int overperformance = actualWins - projectedWins;

                float score = (overperformance * 2f) + (actualWins * 0.5f);

                // Bonus for making playoffs when not expected
                if (projectedWins < 36 && actualWins >= 42)
                {
                    score += 10f;
                }

                // Penalty for significant underperformance
                if (overperformance < -10)
                {
                    score -= 15f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    winnerTeamId = team.TeamId;
                }
            }

            if (winnerTeamId != null)
            {
                // Find the Head Coach for this team using PersonnelManager
                var staff = PersonnelManager.Instance?.GetTeamStaff(winnerTeamId);
                var coach = staff?.FirstOrDefault(s => s.CurrentRole == UnifiedRole.HeadCoach);

                if (coach != null)
                {
                    coach.AddAward(year, AwardType.CoachOfYear, winnerTeamId);
                    Debug.Log($"[AwardManager] COY: {coach.PersonName} of {winnerTeamId} (Score: {bestScore:F1})");
                    return coach.ProfileId;
                }
                
                Debug.Log($"[AwardManager] COY: Coach of {winnerTeamId} (Score: {bestScore:F1}) - Profile not found");
            }

            return null;
        }

        #endregion

        #region Playoff Awards

        /// <summary>
        /// Vote for Finals MVP after championship series
        /// </summary>
        public static Player VoteFinalsMVP(int year, List<Player> finalsPlayers, string winningTeamId,
            Dictionary<string, PlayerGameStats> finalsStats = null)
        {
            if (finalsPlayers == null || finalsPlayers.Count == 0) return null;

            // Filter to winning team players
            var winners = finalsPlayers.Where(p => p.TeamId == winningTeamId).ToList();
            if (winners.Count == 0) return null;

            Player mvp;

            if (finalsStats != null && finalsStats.Count > 0)
            {
                // Use actual Finals series stats
                mvp = winners.OrderByDescending(p =>
                {
                    if (!finalsStats.ContainsKey(p.PlayerId)) return 0f;
                    var stats = finalsStats[p.PlayerId];
                    return (stats.Points / Math.Max(1, stats.GamesPlayed)) * 1.5f +
                           (stats.Rebounds / Math.Max(1, stats.GamesPlayed)) * 1.0f +
                           (stats.Assists / Math.Max(1, stats.GamesPlayed)) * 1.2f;
                }).FirstOrDefault();
            }
            else
            {
                // Fall back to season stats
                mvp = winners.OrderByDescending(p =>
                    (p.CurrentSeasonStats?.PPG ?? 0) * 1.5f +
                    (p.CurrentSeasonStats?.RPG ?? 0) * 1.0f +
                    (p.CurrentSeasonStats?.APG ?? 0) * 1.2f +
                    (p.CurrentSeasonStats?.PER ?? 0) * 0.5f
                ).FirstOrDefault();
            }

            if (mvp != null)
            {
                mvp.Awards.Add(new AwardHistory(year, AwardType.FinalsMVP, mvp.TeamId));
                Debug.Log($"[AwardManager] Finals MVP: {mvp.FullName}");
            }

            return mvp;
        }

        public static void AwardChampionship(int year, Team championTeam, List<Player> players)
        {
            if (championTeam == null) return;

            var champions = players.Where(p => p.TeamId == championTeam.TeamId).ToList();
            foreach (var p in champions)
            {
                p.Awards.Add(new AwardHistory(year, AwardType.NBAChampion, championTeam.TeamId));
            }

            Debug.Log($"[AwardManager] {championTeam.Name} are the {year} NBA Champions! ({champions.Count} players)");

            // Also award to staff
            var staff = PersonnelManager.Instance?.GetTeamStaff(championTeam.TeamId);
            if (staff != null)
            {
                foreach (var s in staff)
                {
                    s.AddAward(year, AwardType.NBAChampion, championTeam.TeamId);
                    s.TotalChampionships++;
                    if (s.CurrentTrack == UnifiedCareerTrack.Coaching)
                        s.ChampionshipsAsCoach++;
                    else if (s.CurrentTrack == UnifiedCareerTrack.FrontOffice)
                        s.ChampionshipsAsGM++;
                }
                Debug.Log($"[AwardManager] {championTeam.Name} staff awarded championship medals.");
            }
        }

        #endregion

        #region Monthly/Weekly Awards

        /// <summary>
        /// Award Player of the Month (one per conference)
        /// </summary>
        public static (Player east, Player west) VotePlayerOfMonth(int year, int month, List<Player> players, Dictionary<string, Team> teams)
        {
            var eligible = players.Where(p => p.CurrentSeasonStats != null && p.CurrentSeasonStats.GamesPlayed > 0).ToList();

            var eastern = eligible.Where(p => teams.ContainsKey(p.TeamId) && teams[p.TeamId].Conference == "Eastern")
                                  .OrderByDescending(p => p.CurrentSeasonStats?.PER ?? 0)
                                  .FirstOrDefault();

            var western = eligible.Where(p => teams.ContainsKey(p.TeamId) && teams[p.TeamId].Conference == "Western")
                                  .OrderByDescending(p => p.CurrentSeasonStats?.PER ?? 0)
                                  .FirstOrDefault();

            if (eastern != null)
            {
                eastern.Awards.Add(new AwardHistory(year, AwardType.PlayerOfMonth, eastern.TeamId));
                Debug.Log($"[AwardManager] Eastern Player of Month: {eastern.FullName}");
            }

            if (western != null)
            {
                western.Awards.Add(new AwardHistory(year, AwardType.PlayerOfMonth, western.TeamId));
                Debug.Log($"[AwardManager] Western Player of Month: {western.FullName}");
            }

            return (eastern, western);
        }

        /// <summary>
        /// Award Rookie of the Month (one per conference)
        /// </summary>
        public static (Player east, Player west) VoteRookieOfMonth(int year, int month, List<Player> players, Dictionary<string, Team> teams)
        {
            var rookies = players.Where(p => p.YearsPro == 0 && p.CurrentSeasonStats != null).ToList();

            var eastern = rookies.Where(p => teams.ContainsKey(p.TeamId) && teams[p.TeamId].Conference == "Eastern")
                                 .OrderByDescending(p => (p.CurrentSeasonStats?.PPG ?? 0) + (p.CurrentSeasonStats?.APG ?? 0) + (p.CurrentSeasonStats?.RPG ?? 0))
                                 .FirstOrDefault();

            var western = rookies.Where(p => teams.ContainsKey(p.TeamId) && teams[p.TeamId].Conference == "Western")
                                 .OrderByDescending(p => (p.CurrentSeasonStats?.PPG ?? 0) + (p.CurrentSeasonStats?.APG ?? 0) + (p.CurrentSeasonStats?.RPG ?? 0))
                                 .FirstOrDefault();

            if (eastern != null)
            {
                eastern.Awards.Add(new AwardHistory(year, AwardType.RookieOfMonth, eastern.TeamId));
            }

            if (western != null)
            {
                western.Awards.Add(new AwardHistory(year, AwardType.RookieOfMonth, western.TeamId));
            }

            return (eastern, western);
        }

        /// <summary>
        /// Award Player of the Week (one per conference)
        /// </summary>
        public static (Player east, Player west) VotePlayerOfWeek(int year, int week, List<Player> players, Dictionary<string, Team> teams)
        {
            var eligible = players.Where(p => p.CurrentSeasonStats != null).ToList();

            // For weekly awards, use recent performance proxy (PER + PPG)
            var eastern = eligible.Where(p => teams.ContainsKey(p.TeamId) && teams[p.TeamId].Conference == "Eastern")
                                  .OrderByDescending(p => (p.CurrentSeasonStats?.PPG ?? 0) + (p.CurrentSeasonStats?.PER ?? 0) * 0.5f)
                                  .FirstOrDefault();

            var western = eligible.Where(p => teams.ContainsKey(p.TeamId) && teams[p.TeamId].Conference == "Western")
                                  .OrderByDescending(p => (p.CurrentSeasonStats?.PPG ?? 0) + (p.CurrentSeasonStats?.PER ?? 0) * 0.5f)
                                  .FirstOrDefault();

            if (eastern != null)
            {
                eastern.Awards.Add(new AwardHistory(year, AwardType.PlayerOfWeek, eastern.TeamId));
            }

            if (western != null)
            {
                western.Awards.Add(new AwardHistory(year, AwardType.PlayerOfWeek, western.TeamId));
            }

            return (eastern, western);
        }

        #endregion

        #region Award Queries

        /// <summary>
        /// Get all MVP winners in history
        /// </summary>
        public static List<(int year, Player player)> GetMVPHistory(List<Player> players)
        {
            return players.SelectMany(p => p.Awards.Where(a => a.Type == AwardType.MVP)
                                                   .Select(a => (a.Year, p)))
                          .OrderByDescending(x => x.Year)
                          .ToList();
        }

        /// <summary>
        /// Check if player is eligible for Supermax (All-NBA or MVP/DPOY in past 3 years)
        /// </summary>
        public static bool IsSupermaxEligible(Player player, int currentYear)
        {
            var recentAwards = player.Awards.Where(a => a.Year >= currentYear - 3).ToList();

            return recentAwards.Any(a =>
                a.Type == AwardType.MVP ||
                a.Type == AwardType.DefensivePlayerOfYear ||
                a.Type == AwardType.AllNBAFirstTeam ||
                a.Type == AwardType.AllNBASecondTeam ||
                a.Type == AwardType.AllNBAThirdTeam);
        }

        /// <summary>
        /// Get count of specific award type for a player
        /// </summary>
        public static int GetAwardCount(Player player, AwardType type)
        {
            return player.Awards.Count(a => a.Type == type);
        }

        #endregion
    }

    /// <summary>
    /// Results from season award voting
    /// </summary>
    public class AwardVotingResults
    {
        public int Year;
        public Player MVP;
        public Player DPOY;
        public Player ROTY;
        public Player SixthMan;
        public Player MIP;
        public List<Player> AllNBAFirst = new List<Player>();
        public List<Player> AllNBASecond = new List<Player>();
        public List<Player> AllNBAThird = new List<Player>();
        public List<Player> AllDefenseFirst = new List<Player>();
        public List<Player> AllDefenseSecond = new List<Player>();
    }
}
