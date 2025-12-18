using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
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

        // ==================== ROSTER ====================
        public List<string> RosterPlayerIds = new List<string>(); // Up to 15 players
        public string[] StartingLineupIds = new string[5];        // 5 starters by position index
        
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
        public float WinPercentage => (Wins + Losses) > 0 ? (float)Wins / (Wins + Losses) : 0f;
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
    }
}
