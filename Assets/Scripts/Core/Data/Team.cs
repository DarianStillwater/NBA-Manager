using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core.Data
{
    public enum MarketSize
    {
        Small,
        Medium,
        Large
    }

    /// <summary>
    /// Team data including roster, strategy, and performance tracking.
    /// </summary>
    [Serializable]
    public class Team
    {
        // ==================== IDENTITY ====================
        public string TeamId;
        public string City;
        public string Nickname;
        public string Abbreviation;  // e.g., "LAL", "BOS"
        public string PrimaryColor;  // Hex color
        public string SecondaryColor;
        public string ArenaName;
        public int ArenaCapacity;
        public int PremiumSeats;
        public MarketSize MarketSize = MarketSize.Medium;

        // ==================== STAFF ====================
        public string HeadCoachId;
        [NonSerialized] public AICoachPersonality CoachPersonality;

        // ==================== ROSTER ====================
        public List<string> RosterPlayerIds = new List<string>(); // Up to 15 players
        public string[] StartingLineupIds = new string[5];        // 5 starters by position index
        
        // ==================== FACILITIES ====================
        public TrainingFacility TrainingFacility;

        // ==================== STRATEGY ====================
        public TeamStrategy OffensiveStrategy = new TeamStrategy();
        public TeamStrategy DefensiveStrategy = new TeamStrategy();

        // ==================== TEAM DYNAMICS ====================
        [Range(0, 100)] public float TeamChemistry = 50f;  // Affects passing, help defense
        [Range(0, 100)] public float Momentum = 50f;       // Changes during game

        // ==================== SEASON STATS ====================
        public int Wins;
        public int Losses;
        public float PointsPerGame;
        public float PointsAllowedPerGame;

        // ==================== CONFERENCE / DIVISION ====================
        public string Conference;  // "Eastern" or "Western"
        public string Division;    // "Atlantic", "Central", "Southeast", "Northwest", "Pacific", "Southwest"

        // ==================== COMPUTED ====================
        public string FullName => $"{City} {Nickname}";
        public string Name  // Alias for UI compatibility
        {
            get => Nickname;
            set => Nickname = value;
        }
        public int TotalGames => Wins + Losses;
        public float WinPercentage => TotalGames > 0 ? (float)Wins / TotalGames : 0f;
        public string Arena => ArenaName;  // Alias for UI compatibility

        // ==================== UI HELPER PROPERTIES ====================

        /// <summary>
        /// Starting lineup as int array for UI compatibility
        /// Maps to roster indices
        /// </summary>
        public int[] StartingLineup
        {
            get
            {
                int[] lineup = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    if (i < StartingLineupIds.Length && !string.IsNullOrEmpty(StartingLineupIds[i]))
                    {
                        int idx = RosterPlayerIds.IndexOf(StartingLineupIds[i]);
                        lineup[i] = idx >= 0 ? idx : 0;
                    }
                    else
                    {
                        lineup[i] = i < RosterPlayerIds.Count ? i : 0;
                    }
                }
                return lineup;
            }
            set
            {
                if (value == null || value.Length != 5) return;
                for (int i = 0; i < 5; i++)
                {
                    if (value[i] >= 0 && value[i] < RosterPlayerIds.Count)
                    {
                        StartingLineupIds[i] = RosterPlayerIds[value[i]];
                    }
                }
            }
        }

        /// <summary>
        /// Get the team's primary strategy (offensive)
        /// </summary>
        public TeamStrategy Strategy
        {
            get => OffensiveStrategy;
            set => OffensiveStrategy = value ?? new TeamStrategy();
        }

        // Internal cached roster for direct assignment
        [NonSerialized]
        private List<Player> _cachedRoster;

        /// <summary>
        /// Gets/sets the roster as Player objects.
        /// Getter resolves from PlayerDatabase if no cached roster exists.
        /// </summary>
        public List<Player> Roster
        {
            get
            {
                if (_cachedRoster != null)
                    return _cachedRoster;

                var roster = new List<Player>();
                var playerDb = NBAHeadCoach.Core.GameManager.Instance?.PlayerDatabase;
                if (playerDb != null)
                {
                    foreach (var playerId in RosterPlayerIds)
                    {
                        if (playerDb.Players.TryGetValue(playerId, out var player))
                        {
                            roster.Add(player);
                        }
                    }
                }
                return roster;
            }
            set
            {
                _cachedRoster = value;
                // Also update the ID list
                RosterPlayerIds.Clear();
                if (value != null)
                {
                    foreach (var player in value)
                    {
                        if (!string.IsNullOrEmpty(player?.PlayerId))
                            RosterPlayerIds.Add(player.PlayerId);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a specific player from the roster by ID.
        /// </summary>
        public Player GetPlayer(string playerId)
        {
            var playerDb = NBAHeadCoach.Core.GameManager.Instance?.PlayerDatabase;
            if (playerDb != null && playerDb.Players.TryGetValue(playerId, out var player))
            {
                return player;
            }
            return null;
        }

        /// <summary>
        /// Gets the starting lineup player IDs in order: PG, SG, SF, PF, C
        /// </summary>
        public string[] GetStartingFive()
        {
            return StartingLineupIds;
        }

        /// <summary>
        /// Sets a starter at a specific position (0=PG, 1=SG, 2=SF, 3=PF, 4=C)
        /// </summary>
        public void SetStarter(int positionIndex, string playerId)
        {
            if (positionIndex >= 0 && positionIndex < 5)
            {
                StartingLineupIds[positionIndex] = playerId;
            }
        }

        /// <summary>
        /// Returns bench player IDs (everyone not in starting lineup).
        /// </summary>
        public List<string> GetBenchPlayerIds()
        {
            var bench = new List<string>();
            foreach (var playerId in RosterPlayerIds)
            {
                bool isStarter = false;
                foreach (var starterId in StartingLineupIds)
                {
                    if (playerId == starterId)
                    {
                        isStarter = true;
                        break;
                    }
                }
                if (!isStarter)
                {
                    bench.Add(playerId);
                }
            }
            return bench;
        }

        /// <summary>
        /// Adjusts team chemistry based on game events.
        /// </summary>
        public void AdjustChemistry(float delta)
        {
            TeamChemistry = Mathf.Clamp(TeamChemistry + delta, 0f, 100f);
        }

        /// <summary>
        /// Adjusts momentum during gameplay (big plays, runs, etc.)
        /// </summary>
        public void AdjustMomentum(float delta)
        {
            Momentum = Mathf.Clamp(Momentum + delta, 0f, 100f);
        }

        // ==================== AUTO-INITIALIZATION ====================

        /// <summary>
        /// Sets starting lineup: best player at each position, then coach-style adjustments.
        /// </summary>
        public void AutoSetStartingLineup(AICoachPersonality coach = null)
        {
            if (Roster == null || Roster.Count < 5) return;
            var used = new HashSet<string>();
            var starters = new List<Player>();

            // Pick best player at each standard position
            var positions = new[] { Position.PointGuard, Position.ShootingGuard,
                                    Position.SmallForward, Position.PowerForward, Position.Center };
            foreach (var pos in positions)
            {
                var best = Roster.Where(p => p != null && p.Position == pos && !string.IsNullOrEmpty(p.PlayerId) && !used.Contains(p.PlayerId))
                                 .OrderByDescending(p => p.OverallRating).FirstOrDefault();
                if (best != null) { starters.Add(best); used.Add(best.PlayerId); }
            }
            // Fill remaining with best available
            while (starters.Count < 5)
            {
                var next = Roster.Where(p => p != null && !string.IsNullOrEmpty(p.PlayerId) && !used.Contains(p.PlayerId))
                                 .OrderByDescending(p => p.OverallRating).FirstOrDefault();
                if (next == null) break;
                starters.Add(next); used.Add(next.PlayerId);
            }

            // Coach style adjustments
            if (coach != null && starters.Count == 5)
            {
                // Fast pace coach: swap slowest starter for faster bench player
                if (coach.PreferredPace > 95)
                    TrySwapForAttribute(starters, used, p => p.Speed, ascending: true, minGain: 10);

                // 3PT-heavy coach: swap worst shooter for better bench shooter
                if (coach.ThreePointEmphasis > 75)
                    TrySwapForAttribute(starters, used, p => p.Shot_Three, ascending: true, minGain: 10);

                // Defense-first coach: swap weakest defender for better bench defender
                if (coach.DefensiveAggression > 75)
                    TrySwapForAttribute(starters, used, p => p.Defense_Perimeter + p.Defense_Interior, ascending: true, minGain: 15);
            }

            StartingLineupIds = starters.Select(p => p.PlayerId).ToArray();
            if (StartingLineupIds.Length < 5) Array.Resize(ref StartingLineupIds, 5);
        }

        private void TrySwapForAttribute(List<Player> starters, HashSet<string> used,
            Func<Player, float> attribute, bool ascending, float minGain)
        {
            var worst = starters.OrderBy(attribute).First();
            var bestBench = Roster.Where(p => p != null && !string.IsNullOrEmpty(p.PlayerId) && !used.Contains(p.PlayerId)
                && attribute(p) > attribute(worst) + minGain)
                .OrderByDescending(attribute).FirstOrDefault();
            if (bestBench != null)
            {
                used.Remove(worst.PlayerId);
                starters.Remove(worst);
                starters.Add(bestBench);
                used.Add(bestBench.PlayerId);
            }
        }

        /// <summary>
        /// Sets team strategy based on AI coach personality traits.
        /// </summary>
        public void AutoSetStrategy(AICoachPersonality coach)
        {
            if (coach == null) return;

            OffensiveStrategy = new TeamStrategy
            {
                TargetPace = coach.PreferredPace,
                ThreePointFrequency = coach.ThreePointEmphasis,
                PostUpFrequency = coach.PostPlayEmphasis,
            };
            // Set the Pace property (maps to 0-100 scale)
            OffensiveStrategy.Pace = Mathf.RoundToInt((coach.PreferredPace - 80f) / 30f * 100f);

            DefensiveStrategy = new TeamStrategy();
            DefensiveStrategy.Pace = OffensiveStrategy.Pace;
        }
    }
}
