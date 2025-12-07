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

        // ==================== COMPUTED ====================
        public string FullName => $"{City} {Nickname}";
        public float WinPercentage => (Wins + Losses) > 0 ? (float)Wins / (Wins + Losses) : 0f;

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

    /// <summary>
    /// Strategy sliders that affect simulation probabilities.
    /// </summary>
    [Serializable]
    public class TeamStrategy
    {
        [Header("Pace & Style")]
        [Range(1, 100)] public int Pace = 50;                    // Fast break vs half-court
        [Range(1, 100)] public int ThreePointFrequency = 50;     // Shoot 3s vs attack paint
        [Range(1, 100)] public int IsolationFrequency = 30;      // Iso plays vs motion offense
        [Range(1, 100)] public int PickAndRollFrequency = 50;    // PnR vs other actions
        [Range(1, 100)] public int PostUpFrequency = 30;         // Feed the post

        [Header("Rebounding & Transition")]
        [Range(1, 100)] public int CrashOffensiveBoards = 50;    // Send players to offensive glass
        [Range(1, 100)] public int FastBreakAggressiveness = 50; // Push in transition

        [Header("Defense")]
        [Range(1, 100)] public int DefensiveAggression = 50;     // Press vs protect paint
        [Range(1, 100)] public int TrapFrequency = 20;           // Double team ball handler
        [Range(1, 100)] public int SwitchEverything = 30;        // Switch all screens

        /// <summary>
        /// Calculates expected possessions per game based on pace.
        /// League average is about 100 possessions.
        /// </summary>
        public int GetExpectedPossessions()
        {
            // Pace 1 = 85 possessions, Pace 100 = 115 possessions
            return Mathf.RoundToInt(Mathf.Lerp(85, 115, Pace / 100f));
        }
    }
}
