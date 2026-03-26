using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Tracks league-wide statistical totals and averages across all teams and games.
    /// Used as baseline for calculating advanced stats (PER, WS, BPM, VORP).
    /// Updated after each game day.
    /// </summary>
    public class LeagueStatsAggregator
    {
        // Raw league totals (accumulated from all games)
        public int TotalTeamGames;   // Each game counts as 2 team-games
        public int TotalMinutes;
        public int TotalPoints;
        public int TotalFGA, TotalFGM;
        public int Total3PA, Total3PM;
        public int TotalFTA, TotalFTM;
        public int TotalORB, TotalDRB;
        public int TotalAST, TotalSTL, TotalBLK, TotalTOV, TotalPF;

        // Derived league averages (per team per game)
        public float LeaguePPG;        // ~110
        public float LeaguePace;       // possessions per 48 min (~100)
        public float LeagueTS;         // ~0.57
        public float LeagueFGPct;      // ~0.46
        public float League3PPct;      // ~0.36
        public float LeagueFTPct;      // ~0.77
        public float LeagueORBPct;     // ~0.23
        public float LeagueDRBPct;     // ~0.77
        public float LeagueTOVRate;    // ~0.13
        public float LeagueASTRate;    // ~0.58 of made baskets are assisted

        // PER calculation factors
        public float LeagueFactor;     // ~1.0
        public float LeagueVOP;        // value of possession
        public float LeagueFTPerPF;    // league FTM / league PF

        /// <summary>
        /// Add a completed game's box score to league totals.
        /// Call once per game (counts both teams).
        /// </summary>
        public void AddGameResult(BoxScore bs)
        {
            if (bs == null) return;

            TotalTeamGames += 2; // Each game has 2 teams playing

            // Accumulate from all player stats in the game
            foreach (var kvp in bs.PlayerStats)
            {
                var s = kvp.Value;
                TotalMinutes += s.Minutes;
                TotalPoints += s.Points;
                TotalFGM += s.FieldGoalsMade;
                TotalFGA += s.FieldGoalAttempts;
                Total3PM += s.ThreePointMade;
                Total3PA += s.ThreePointAttempts;
                TotalFTM += s.FreeThrowsMade;
                TotalFTA += s.FreeThrowAttempts;
                TotalORB += s.OffensiveRebounds;
                TotalDRB += s.DefensiveRebounds;
                TotalAST += s.Assists;
                TotalSTL += s.Steals;
                TotalBLK += s.Blocks;
                TotalTOV += s.Turnovers;
                TotalPF += s.PersonalFouls;
            }
        }

        /// <summary>
        /// Recalculate all derived league averages from accumulated totals.
        /// Call after adding all games for the day.
        /// </summary>
        public void Recalculate()
        {
            if (TotalTeamGames == 0) return;

            float tg = TotalTeamGames;

            // Per-team-per-game averages
            LeaguePPG = TotalPoints / tg;
            LeagueFGPct = TotalFGA > 0 ? (float)TotalFGM / TotalFGA : 0.46f;
            League3PPct = Total3PA > 0 ? (float)Total3PM / Total3PA : 0.36f;
            LeagueFTPct = TotalFTA > 0 ? (float)TotalFTM / TotalFTA : 0.77f;

            // True Shooting %
            float lgTSA = TotalFGA + 0.44f * TotalFTA;
            LeagueTS = lgTSA > 0 ? TotalPoints / (2f * lgTSA) : 0.57f;

            // Possession estimate per team per game
            // Poss ≈ FGA - ORB + TOV + 0.44*FTA
            float totalPoss = TotalFGA - TotalORB + TotalTOV + 0.44f * TotalFTA;
            LeaguePace = totalPoss / tg; // possessions per team per game

            // Rebound percentages
            int totalReb = TotalORB + TotalDRB;
            LeagueORBPct = totalReb > 0 ? (float)TotalORB / totalReb : 0.23f;
            LeagueDRBPct = 1f - LeagueORBPct;

            // Turnover rate
            float totalPossForTOV = TotalFGA + 0.44f * TotalFTA + TotalTOV;
            LeagueTOVRate = totalPossForTOV > 0 ? (float)TotalTOV / totalPossForTOV : 0.13f;

            // Assist rate (assists per FGM)
            LeagueASTRate = TotalFGM > 0 ? (float)TotalAST / TotalFGM : 0.58f;

            // PER league factor
            // factor = (2/3) - (0.5 * (lgAST / lgFGM)) / (2 * (lgFGM / lgFTM))
            float lgAstFgm = TotalFGM > 0 ? (float)TotalAST / TotalFGM : 0.58f;
            float lgFgmFtm = TotalFTM > 0 ? (float)TotalFGM / TotalFTM : 2.0f;
            LeagueFactor = (2f / 3f) - (0.5f * lgAstFgm) / (2f * lgFgmFtm);
            LeagueFactor = Mathf.Clamp(LeagueFactor, 0.3f, 0.8f);

            // VOP = league points / (FGA - ORB + TOV + 0.44*FTA)
            LeagueVOP = totalPoss > 0 ? TotalPoints / totalPoss : 1.0f;

            // FT per PF
            LeagueFTPerPF = TotalPF > 0 ? (float)TotalFTM / TotalPF : 0.5f;
        }

        /// <summary>Reset for a new season.</summary>
        public void Reset()
        {
            TotalTeamGames = TotalMinutes = TotalPoints = 0;
            TotalFGA = TotalFGM = Total3PA = Total3PM = TotalFTA = TotalFTM = 0;
            TotalORB = TotalDRB = TotalAST = TotalSTL = TotalBLK = TotalTOV = TotalPF = 0;
            Recalculate();
        }

        /// <summary>
        /// Calculate advanced stats for a single player's season using league averages.
        /// </summary>
        public void CalculatePlayerAdvancedStats(SeasonStats s)
        {
            if (s == null || s.GamesPlayed == 0 || s.MinutesPlayed == 0 || TotalTeamGames == 0) return;

            float min = s.MinutesPlayed;
            float gp = s.GamesPlayed;
            float mpg = min / gp;

            // Rate stats (3PAr, FTr)
            s.ThreePAr = s.FG_Attempts > 0 ? (float)s.ThreeP_Attempts / s.FG_Attempts : 0;
            s.FTr = s.FG_Attempts > 0 ? (float)s.FT_Attempts / s.FG_Attempts : 0;

            // Possessions for this player (estimate)
            float playerPoss = s.FG_Attempts + 0.44f * s.FT_Attempts + s.Turnovers;

            // Estimate team possessions while player was on floor
            float teamPossPerMin = LeaguePace / 48f; // possessions per minute per team
            float teamPossWhileOn = min * teamPossPerMin;

            // ═══════ Usage Rate ═══════
            // USG% = 100 * ((FGA + 0.44*FTA + TOV) * (teamMin/5)) / (Min * teamPoss)
            if (teamPossWhileOn > 0)
                s.UsgPct = playerPoss / (teamPossWhileOn / 5f);
            s.UsgPct = Mathf.Clamp(s.UsgPct, 0f, 0.45f);

            // ═══════ Rebound Percentages ═══════
            float teamMinPerGame = 240f; // 5 players × 48 min
            float minPct = mpg / 48f;

            // Estimate available rebounds per game while on floor
            float lgRebPerGame = (float)(TotalORB + TotalDRB) / TotalTeamGames;
            s.OrbPct = lgRebPerGame > 0 ? (s.OffensiveRebounds / gp) / (lgRebPerGame * 0.5f * minPct) : 0;
            s.DrbPct = lgRebPerGame > 0 ? (s.DefensiveRebounds / gp) / (lgRebPerGame * 0.5f * minPct) : 0;
            s.TrbPct = lgRebPerGame > 0 ? (s.TotalRebounds / gp) / (lgRebPerGame * minPct) : 0;

            // ═══════ Assist, Steal, Block, Turnover Percentages ═══════
            float lgAstPerGame = (float)TotalAST / TotalTeamGames;
            float lgStlPerGame = (float)TotalSTL / TotalTeamGames;
            float lgBlkPerGame = (float)TotalBLK / TotalTeamGames;
            s.AstPct = lgAstPerGame > 0 ? (s.Assists / gp) / (lgAstPerGame * minPct) : 0;
            s.StlPct = lgStlPerGame > 0 ? (s.Steals / gp) / (lgStlPerGame * minPct) : 0;
            s.BlkPct = lgBlkPerGame > 0 ? (s.Blocks / gp) / (lgBlkPerGame * minPct) : 0;
            s.TovPct = playerPoss > 0 ? (float)s.Turnovers / playerPoss : 0;

            // ═══════ PER (Player Efficiency Rating) ═══════
            // Simplified Hollinger formula using league factors
            float lgAstFgm = TotalFGM > 0 ? (float)TotalAST / TotalFGM : 0.58f;
            float teamAstFgm = lgAstFgm; // Use league average as team proxy

            float uPER = (1f / min) * (
                s.ThreeP_Made
                + (2f / 3f) * s.Assists
                + (2f - LeagueFactor * teamAstFgm) * s.FG_Made
                + s.FT_Made * 0.5f * (1f + (1f - teamAstFgm) + (2f / 3f) * teamAstFgm)
                - LeagueVOP * s.Turnovers
                - LeagueVOP * LeagueDRBPct * (s.FG_Attempts - s.FG_Made)
                - LeagueVOP * 0.44f * (0.44f + 0.56f * LeagueDRBPct) * (s.FT_Attempts - s.FT_Made)
                + LeagueVOP * (1f - LeagueDRBPct) * (s.TotalRebounds - s.OffensiveRebounds)
                + LeagueVOP * LeagueDRBPct * s.OffensiveRebounds
                + LeagueVOP * s.Steals
                + LeagueVOP * LeagueDRBPct * s.Blocks
                - s.PersonalFouls * (LeagueFTPerPF - 0.44f * ((float)TotalFTA / Mathf.Max(TotalPF, 1)) * LeagueVOP)
            );
            s.PER = uPER * 48f; // Scale to per-48

            // Normalize: league average PER should be ~15
            // We'll calculate a normalization factor from all players later,
            // but for now clamp to reasonable range
            s.PER = Mathf.Clamp(s.PER, -5f, 40f);

            // ═══════ Win Shares ═══════
            float marginalPtsPerWin = 2f * LeaguePPG * 0.7f; // ~154 marginal pts per win
            if (marginalPtsPerWin < 1f) marginalPtsPerWin = 100f;

            // Offensive Win Shares
            float pointsProduced = s.Points + s.Assists * 0.5f * (LeaguePPG / (TotalAST > 0 ? (float)TotalAST / TotalTeamGames : 5f));
            float expectedOffense = teamPossWhileOn * LeagueVOP * 0.92f;
            s.OffensiveWinShares = Mathf.Max(0, (pointsProduced - expectedOffense) / marginalPtsPerWin);

            // Defensive Win Shares (simplified)
            float defContrib = s.Steals * 2f + s.Blocks * 1.5f + s.DefensiveRebounds * 0.5f;
            float expectedDef = min / 48f * gp * 3.5f; // ~3.5 defensive contributions per 48 min avg
            s.DefensiveWinShares = Mathf.Max(0, (defContrib - expectedDef * 0.8f) / marginalPtsPerWin);

            s.WinShares = s.OffensiveWinShares + s.DefensiveWinShares;
            s.WinSharesPer48 = min > 0 ? s.WinShares / min * 48f * gp : 0;

            // ═══════ BPM (Box Plus-Minus) ═══════
            // Regression-based estimate from box score stats
            float ppg = s.PPG, rpg = s.RPG, apg = s.APG, spg = s.SPG, bpg = s.BPG, tpg = s.TPG;
            float fgPctDiff = s.FG_Pct - LeagueFGPct;
            float tsPctDiff = s.TrueShootingPct - LeagueTS;

            s.BoxPlusMinus =
                (ppg - LeaguePPG / 5f) * 0.2f +    // scoring above average per player
                (rpg - 5f) * 0.25f +                 // rebounds
                (apg - 3f) * 0.35f +                 // assists
                (spg - 0.8f) * 2.0f +                // steals (high value)
                (bpg - 0.5f) * 1.5f +                // blocks
                -(tpg - 1.5f) * 0.8f +               // turnovers (negative)
                tsPctDiff * 15f;                       // efficiency

            s.BoxPlusMinus = Mathf.Clamp(s.BoxPlusMinus, -10f, 15f);
            s.OffensiveBPM = s.BoxPlusMinus * 0.55f;
            s.DefensiveBPM = s.BoxPlusMinus * 0.45f;

            // ═══════ VORP ═══════
            // (BPM - (-2.0)) * (% of team minutes) * (GP / 82)
            float teamMinPct = mpg / 48f;
            float seasonPct = gp / 82f;
            s.VORP = (s.BoxPlusMinus + 2.0f) * teamMinPct * seasonPct;
        }
    }
}
