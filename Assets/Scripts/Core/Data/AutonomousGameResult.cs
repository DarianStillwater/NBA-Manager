using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Complete result of an autonomously simulated game.
    /// Used in GM-Only mode where the AI coach runs the game.
    /// </summary>
    [Serializable]
    public class AutonomousGameResult
    {
        // ==================== BASIC INFO ====================
        public string GameId;
        public DateTime GameDate;
        public string HomeTeamId;
        public string AwayTeamId;
        public bool WasHomeGame;

        // ==================== FINAL SCORE ====================
        public int HomeScore;
        public int AwayScore;
        public int[] HomeQuarterScores = new int[4];
        public int[] AwayQuarterScores = new int[4];
        public bool WasOvertime;
        public int OvertimePeriods;

        public bool UserTeamWon => WasHomeGame ? HomeScore > AwayScore : AwayScore > HomeScore;
        public int UserTeamScore => WasHomeGame ? HomeScore : AwayScore;
        public int OpponentScore => WasHomeGame ? AwayScore : HomeScore;
        public string WinningTeamId => HomeScore > AwayScore ? HomeTeamId : AwayTeamId;

        // ==================== COACH SUMMARY ====================
        public string CoachName;
        public CoachPerformanceSummary CoachPerformance = new CoachPerformanceSummary();

        // ==================== KEY MOMENTS ====================
        public List<GameMoment> KeyMoments = new List<GameMoment>();

        // ==================== BOX SCORE ====================
        public List<PlayerBoxScore> HomeBoxScore = new List<PlayerBoxScore>();
        public List<PlayerBoxScore> AwayBoxScore = new List<PlayerBoxScore>();
        public TeamBoxScore HomeTeamStats = new TeamBoxScore();
        public TeamBoxScore AwayTeamStats = new TeamBoxScore();

        // ==================== NARRATIVE ====================
        public string GameNarrative;
        public string CoachPostGameComment;

        // ==================== HELPERS ====================

        public List<PlayerBoxScore> GetUserTeamBoxScore()
        {
            return WasHomeGame ? HomeBoxScore : AwayBoxScore;
        }

        public List<PlayerBoxScore> GetOpponentBoxScore()
        {
            return WasHomeGame ? AwayBoxScore : HomeBoxScore;
        }

        public TeamBoxScore GetUserTeamStats()
        {
            return WasHomeGame ? HomeTeamStats : AwayTeamStats;
        }

        public TeamBoxScore GetOpponentTeamStats()
        {
            return WasHomeGame ? AwayTeamStats : HomeTeamStats;
        }
    }

    /// <summary>
    /// Summary of how the AI coach performed during the game.
    /// </summary>
    [Serializable]
    public class CoachPerformanceSummary
    {
        // Overall rating for the game (1-10)
        public int OverallRating = 5;
        public string OverallAssessment;

        // Key decisions
        public int TimeoutsCalled;
        public int TimeoutsRemaining;
        public int SubstitutionsMade;
        public int AdjustmentsMade;

        // Decision quality
        public List<CoachDecision> NotableDecisions = new List<CoachDecision>();

        // Rotation notes
        public string RotationSummary;
        public string StarMinutesComment;

        // Areas of success/concern
        public List<string> PositiveAspects = new List<string>();
        public List<string> AreasOfConcern = new List<string>();
    }

    /// <summary>
    /// A notable coaching decision during the game.
    /// </summary>
    [Serializable]
    public class CoachDecision
    {
        public string Description;
        public int Quarter;
        public string GameClock;
        public DecisionQuality Quality;
        public string Outcome;

        public enum DecisionQuality
        {
            Excellent,
            Good,
            Neutral,
            Questionable,
            Poor
        }
    }

    /// <summary>
    /// A notable moment during the game.
    /// </summary>
    [Serializable]
    public class GameMoment
    {
        public string Description;
        public int Quarter;
        public string GameClock;
        public MomentType Type;
        public int ScoreDiff;
        public string InvolvedPlayerId;
        public string InvolvedPlayerName;

        public enum MomentType
        {
            BigPlay,
            LeadChange,
            Run,
            ClutchPlay,
            InjuryScare,
            TechnicalFoul,
            MomentumShift,
            CoachingAdjustment,
            RecordOrMilestone
        }
    }

    /// <summary>
    /// Individual player box score for autonomous game.
    /// </summary>
    [Serializable]
    public class PlayerBoxScore
    {
        public string PlayerId;
        public string PlayerName;
        public Position Position;
        public bool Started;

        // Time
        public int MinutesPlayed;

        // Scoring
        public int Points;
        public int FGM;
        public int FGA;
        public int ThreePM;
        public int ThreePA;
        public int FTM;
        public int FTA;

        // Rebounds
        public int ORB;
        public int DRB;
        public int TotalRebounds => ORB + DRB;

        // Playmaking
        public int Assists;
        public int Turnovers;

        // Defense
        public int Steals;
        public int Blocks;

        // Misc
        public int PersonalFouls;
        public int PlusMinus;

        // Calculated
        public float FGPct => FGA > 0 ? (float)FGM / FGA * 100 : 0;
        public float ThreePct => ThreePA > 0 ? (float)ThreePM / ThreePA * 100 : 0;
        public float FTPct => FTA > 0 ? (float)FTM / FTA * 100 : 0;

        // Performance notes
        public string PerformanceNote;
        public bool WasGameStar;
    }

    /// <summary>
    /// Team-level box score totals.
    /// </summary>
    [Serializable]
    public class TeamBoxScore
    {
        public int Points;
        public int FGM;
        public int FGA;
        public int ThreePM;
        public int ThreePA;
        public int FTM;
        public int FTA;
        public int ORB;
        public int DRB;
        public int TotalRebounds => ORB + DRB;
        public int Assists;
        public int Turnovers;
        public int Steals;
        public int Blocks;
        public int PersonalFouls;

        // Advanced
        public float Pace;
        public float OffensiveRating;
        public float DefensiveRating;
        public int PointsInPaint;
        public int FastBreakPoints;
        public int PointsOffTurnovers;
        public int SecondChancePoints;
        public int BenchPoints;

        // Calculated
        public float FGPct => FGA > 0 ? (float)FGM / FGA * 100 : 0;
        public float ThreePct => ThreePA > 0 ? (float)ThreePM / ThreePA * 100 : 0;
        public float FTPct => FTA > 0 ? (float)FTM / FTA * 100 : 0;
    }

    /// <summary>
    /// Quick game summary for display in lists.
    /// </summary>
    [Serializable]
    public class GameSummaryQuick
    {
        public string GameId;
        public DateTime Date;
        public string OpponentId;
        public string OpponentName;
        public bool WasHome;
        public int UserScore;
        public int OpponentScore;
        public bool Won;
        public string CoachRating;
        public string HighlightPlayer;
        public string HighlightStat;
    }
}
