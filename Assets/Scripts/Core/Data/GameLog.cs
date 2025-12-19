using System;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Individual game performance record for a player.
    /// Tracks all box score stats for a single game.
    /// Only kept for current season - cleared at season end.
    /// </summary>
    [Serializable]
    public class GameLog
    {
        [Header("Game Info")]
        public string GameId;
        public DateTime Date;
        public string OpponentTeamId;
        public bool IsHome;
        public bool IsPlayoff;
        public int PlayoffRound;  // 1-4 for playoff rounds, 0 for regular season

        [Header("Playing Time")]
        public int Minutes;
        public bool Started;

        [Header("Scoring")]
        public int Points;
        public int FGM;  // Field Goals Made
        public int FGA;  // Field Goals Attempted
        public int ThreePM;  // Three Pointers Made
        public int ThreePA;  // Three Pointers Attempted
        public int FTM;  // Free Throws Made
        public int FTA;  // Free Throws Attempted

        [Header("Rebounding")]
        public int ORB;  // Offensive Rebounds
        public int DRB;  // Defensive Rebounds
        public int TotalRebounds => ORB + DRB;

        [Header("Playmaking & Defense")]
        public int Assists;
        public int Steals;
        public int Blocks;
        public int Turnovers;
        public int PersonalFouls;

        [Header("Impact")]
        public int PlusMinus;

        [Header("Game Result")]
        public int TeamScore;
        public int OpponentScore;
        public bool Won => TeamScore > OpponentScore;
        public bool WasOvertime;
        public int OvertimePeriods;

        // Computed shooting percentages
        public float FGPct => FGA > 0 ? (float)FGM / FGA : 0f;
        public float ThreePPct => ThreePA > 0 ? (float)ThreePM / ThreePA : 0f;
        public float FTPct => FTA > 0 ? (float)FTM / FTA : 0f;

        /// <summary>
        /// Create a GameLog from simulation results
        /// </summary>
        public static GameLog Create(
            string gameId,
            DateTime date,
            string opponentTeamId,
            bool isHome,
            bool isPlayoff,
            int playoffRound,
            int minutes,
            bool started,
            int points,
            int fgm,
            int fga,
            int threePm,
            int threePa,
            int ftm,
            int fta,
            int orb,
            int drb,
            int assists,
            int steals,
            int blocks,
            int turnovers,
            int fouls,
            int plusMinus,
            int teamScore,
            int opponentScore,
            bool wasOvertime = false,
            int overtimePeriods = 0)
        {
            return new GameLog
            {
                GameId = gameId,
                Date = date,
                OpponentTeamId = opponentTeamId,
                IsHome = isHome,
                IsPlayoff = isPlayoff,
                PlayoffRound = playoffRound,
                Minutes = minutes,
                Started = started,
                Points = points,
                FGM = fgm,
                FGA = fga,
                ThreePM = threePm,
                ThreePA = threePa,
                FTM = ftm,
                FTA = fta,
                ORB = orb,
                DRB = drb,
                Assists = assists,
                Steals = steals,
                Blocks = blocks,
                Turnovers = turnovers,
                PersonalFouls = fouls,
                PlusMinus = plusMinus,
                TeamScore = teamScore,
                OpponentScore = opponentScore,
                WasOvertime = wasOvertime,
                OvertimePeriods = overtimePeriods
            };
        }

        /// <summary>
        /// Get a display string for the game result
        /// </summary>
        public string GetResultString()
        {
            string result = Won ? "W" : "L";
            string ot = WasOvertime ? $" ({OvertimePeriods}OT)" : "";
            return $"{result} {TeamScore}-{OpponentScore}{ot}";
        }

        /// <summary>
        /// Get a short stat line for display
        /// </summary>
        public string GetStatLine()
        {
            return $"{Points} PTS, {TotalRebounds} REB, {Assists} AST";
        }
    }
}
