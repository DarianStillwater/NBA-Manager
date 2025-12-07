using System;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Comprehensive season statistics tracking matching Basketball-Reference columns.
    /// Tracks totals, per-game averages, and advanced metrics.
    /// </summary>
    [Serializable]
    public class SeasonStats
    {
        public int Year;
        public string TeamId;
        
        // ==================== TOTALS ====================
        public int GamesPlayed;
        public int GamesStarted;
        public int MinutesPlayed;
        
        // Scoring
        public int FG_Made;
        public int FG_Attempts;
        public int ThreeP_Made;
        public int ThreeP_Attempts;
        public int FT_Made;
        public int FT_Attempts;
        public int Points;
        
        // Rebounding
        public int OffensiveRebounds;
        public int DefensiveRebounds;
        public int TotalRebounds => OffensiveRebounds + DefensiveRebounds;
        
        // Playmaking & Defense
        public int Assists;
        public int Steals;
        public int Blocks;
        public int Turnovers;
        public int PersonalFouls;
        
        // ==================== ADVANCED METRICS ====================
        // These are calculated at end of season or periodically
        
        [Header("Advanced")]
        public float PER;             // Player Efficiency Rating
        public float TrueShootingPct; // TS%
        public float EffectiveFGPct;  // eFG%
        public float ThreePAr;        // 3P Attempt Rate
        public float FTr;             // Free Throw Rate
        
        public float OrbPct;          // Offensive Rebound %
        public float DrbPct;          // Defensive Rebound %
        public float TrbPct;          // Total Rebound %
        public float AstPct;          // Assist %
        public float StlPct;          // Steal %
        public float BlkPct;          // Block %
        public float TovPct;          // Turnover %
        public float UsgPct;          // Usage %
        
        [Header("Validation")]
        public float OffensiveWinShares;
        public float DefensiveWinShares;
        public float WinShares;       // WS
        public float WinSharesPer48;  // WS/48
        
        public float OffensiveBPM;    // OBPM
        public float DefensiveBPM;    // DBPM
        public float BoxPlusMinus;    // BPM
        public float VORP;            // Value Over Replacement Player
        
        // ==================== COMPUTED PER GAME ====================
        
        public float MPG => GamesPlayed > 0 ? (float)MinutesPlayed / GamesPlayed : 0;
        public float PPG => GamesPlayed > 0 ? (float)Points / GamesPlayed : 0;
        public float RPG => GamesPlayed > 0 ? (float)TotalRebounds / GamesPlayed : 0;
        public float APG => GamesPlayed > 0 ? (float)Assists / GamesPlayed : 0;
        public float SPG => GamesPlayed > 0 ? (float)Steals / GamesPlayed : 0;
        public float BPG => GamesPlayed > 0 ? (float)Blocks / GamesPlayed : 0;
        public float TPG => GamesPlayed > 0 ? (float)Turnovers / GamesPlayed : 0;
        
        // ==================== COMPUTED PERCENTAGES ====================
        
        public float FG_Pct => FG_Attempts > 0 ? (float)FG_Made / FG_Attempts : 0f;
        public float ThreeP_Pct => ThreeP_Attempts > 0 ? (float)ThreeP_Made / ThreeP_Attempts : 0f;
        public float FT_Pct => FT_Attempts > 0 ? (float)FT_Made / FT_Attempts : 0f;
        public float TwoP_Pct 
        {
            get 
            {
                int twoPA = FG_Attempts - ThreeP_Attempts;
                return twoPA > 0 ? (float)(FG_Made - ThreeP_Made) / twoPA : 0f;
            }
        }

        public SeasonStats(int year, string teamId)
        {
            Year = year;
            TeamId = teamId;
        }

        /// <summary>
        /// Adds a single game's stats to the totals.
        /// </summary>
        public void AddGame(int minutes, int pts, int fgm, int fga, int tpm, int tpa, 
                           int ftm, int fta, int orb, int drb, int ast, int stl, int blk, int tov, int pf, bool started)
        {
            GamesPlayed++;
            if (started) GamesStarted++;
            MinutesPlayed += minutes;
            
            Points += pts;
            FG_Made += fgm;
            FG_Attempts += fga;
            ThreeP_Made += tpm;
            ThreeP_Attempts += tpa;
            FT_Made += ftm;
            FT_Attempts += fta;
            
            OffensiveRebounds += orb;
            DefensiveRebounds += drb;
            Assists += ast;
            Steals += stl;
            Blocks += blk;
            Turnovers += tov;
            PersonalFouls += pf;
            
            // Recalculate simple advanced stats on the fly
            CalculateSimpleAdvanced();
        }

        private void CalculateSimpleAdvanced()
        {
            // Effective Field Goal Percentage
            // (FG + 0.5 * 3P) / FGA
            if (FG_Attempts > 0)
            {
                EffectiveFGPct = (FG_Made + 0.5f * ThreeP_Made) / FG_Attempts;
            }
            
            // True Shooting Percentage
            // PTS / (2 * (FGA + 0.44 * FTA))
            float tsa = FG_Attempts + 0.44f * FT_Attempts;
            if (tsa > 0)
            {
                TrueShootingPct = Points / (2 * tsa);
            }
        }
    }
}
