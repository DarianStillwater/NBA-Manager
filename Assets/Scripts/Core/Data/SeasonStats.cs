using System;
using System.Collections.Generic;
using System.Linq;
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

        // Plus/Minus tracking
        public int TotalPlusMinus;

        // ==================== GAME LOGS ====================
        // Individual game records - only kept for current season
        [Header("Game Logs")]
        public List<GameLog> GameLogs = new List<GameLog>();

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
        public float AvgPlusMinus => GamesPlayed > 0 ? (float)TotalPlusMinus / GamesPlayed : 0;
        
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

        // ==================== GAME LOG METHODS ====================

        /// <summary>
        /// Adds a game from a GameLog object and updates season totals.
        /// </summary>
        public void AddGameFromLog(GameLog log)
        {
            if (log == null) return;

            // Add the game log to the list
            GameLogs.Add(log);

            // Update totals
            GamesPlayed++;
            if (log.Started) GamesStarted++;
            MinutesPlayed += log.Minutes;

            Points += log.Points;
            FG_Made += log.FGM;
            FG_Attempts += log.FGA;
            ThreeP_Made += log.ThreePM;
            ThreeP_Attempts += log.ThreePA;
            FT_Made += log.FTM;
            FT_Attempts += log.FTA;

            OffensiveRebounds += log.ORB;
            DefensiveRebounds += log.DRB;
            Assists += log.Assists;
            Steals += log.Steals;
            Blocks += log.Blocks;
            Turnovers += log.Turnovers;
            PersonalFouls += log.PersonalFouls;
            TotalPlusMinus += log.PlusMinus;

            // Recalculate simple advanced stats
            CalculateSimpleAdvanced();
        }

        /// <summary>
        /// Get the most recent game log.
        /// </summary>
        public GameLog GetLastGame()
        {
            return GameLogs.Count > 0 ? GameLogs[GameLogs.Count - 1] : null;
        }

        /// <summary>
        /// Get the most recent N games.
        /// </summary>
        public List<GameLog> GetRecentGames(int count)
        {
            if (GameLogs.Count == 0) return new List<GameLog>();

            int startIndex = Math.Max(0, GameLogs.Count - count);
            return GameLogs.Skip(startIndex).ToList();
        }

        /// <summary>
        /// Get all playoff games from this season.
        /// </summary>
        public List<GameLog> GetPlayoffGames()
        {
            return GameLogs.Where(g => g.IsPlayoff).ToList();
        }

        /// <summary>
        /// Get all regular season games from this season.
        /// </summary>
        public List<GameLog> GetRegularSeasonGames()
        {
            return GameLogs.Where(g => !g.IsPlayoff).ToList();
        }

        /// <summary>
        /// Clear all game logs (called at season end to save space).
        /// Totals and averages are preserved.
        /// </summary>
        public void ClearGameLogs()
        {
            GameLogs.Clear();
        }

        /// <summary>
        /// Get win/loss record from game logs.
        /// </summary>
        public (int wins, int losses) GetRecord()
        {
            int wins = GameLogs.Count(g => g.Won);
            int losses = GameLogs.Count - wins;
            return (wins, losses);
        }

        /// <summary>
        /// Creates a copy of this SeasonStats without game logs (for archiving).
        /// </summary>
        public SeasonStats CreateArchiveCopy()
        {
            var copy = new SeasonStats(Year, TeamId)
            {
                GamesPlayed = this.GamesPlayed,
                GamesStarted = this.GamesStarted,
                MinutesPlayed = this.MinutesPlayed,
                FG_Made = this.FG_Made,
                FG_Attempts = this.FG_Attempts,
                ThreeP_Made = this.ThreeP_Made,
                ThreeP_Attempts = this.ThreeP_Attempts,
                FT_Made = this.FT_Made,
                FT_Attempts = this.FT_Attempts,
                Points = this.Points,
                OffensiveRebounds = this.OffensiveRebounds,
                DefensiveRebounds = this.DefensiveRebounds,
                Assists = this.Assists,
                Steals = this.Steals,
                Blocks = this.Blocks,
                Turnovers = this.Turnovers,
                PersonalFouls = this.PersonalFouls,
                TotalPlusMinus = this.TotalPlusMinus,
                // Advanced stats
                PER = this.PER,
                TrueShootingPct = this.TrueShootingPct,
                EffectiveFGPct = this.EffectiveFGPct,
                ThreePAr = this.ThreePAr,
                FTr = this.FTr,
                OrbPct = this.OrbPct,
                DrbPct = this.DrbPct,
                TrbPct = this.TrbPct,
                AstPct = this.AstPct,
                StlPct = this.StlPct,
                BlkPct = this.BlkPct,
                TovPct = this.TovPct,
                UsgPct = this.UsgPct,
                OffensiveWinShares = this.OffensiveWinShares,
                DefensiveWinShares = this.DefensiveWinShares,
                WinShares = this.WinShares,
                WinSharesPer48 = this.WinSharesPer48,
                OffensiveBPM = this.OffensiveBPM,
                DefensiveBPM = this.DefensiveBPM,
                BoxPlusMinus = this.BoxPlusMinus,
                VORP = this.VORP
                // GameLogs intentionally NOT copied
            };
            return copy;
        }
    }
}
