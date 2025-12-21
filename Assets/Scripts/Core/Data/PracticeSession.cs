using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Represents a single practice session with duration, focus, drills, and outcomes.
    /// Practice serves dual purpose: player development AND opponent-specific preparation.
    /// </summary>
    [Serializable]
    public class PracticeSession
    {
        // ==================== IDENTITY ====================
        public string SessionId;
        public DateTime Date;
        public string TeamId;

        // ==================== CONFIGURATION ====================

        /// <summary>Duration in minutes (60-150 typical)</summary>
        [Range(30, 180)] public int DurationMinutes = 90;

        /// <summary>Primary focus of this practice</summary>
        public PracticeFocus PrimaryFocus = PracticeFocus.Development;

        /// <summary>Secondary focus (optional)</summary>
        public PracticeFocus? SecondaryFocus;

        /// <summary>Overall intensity level (affects fatigue and results)</summary>
        public PracticeIntensity Intensity = PracticeIntensity.Normal;

        /// <summary>List of drills scheduled for this session</summary>
        public List<ScheduledDrill> Drills = new List<ScheduledDrill>();

        /// <summary>Players participating (empty = full roster)</summary>
        public List<string> ParticipatingPlayerIds = new List<string>();

        /// <summary>Players excused/resting</summary>
        public List<string> RestingPlayerIds = new List<string>();

        // ==================== OPPONENT PREPARATION ====================

        /// <summary>Opponent team ID if this is a game prep session</summary>
        public string OpponentTeamId;

        /// <summary>Whether to include film session for opponent</summary>
        public bool IncludeFilmSession;

        /// <summary>Specific plays to drill against opponent defense</summary>
        public List<string> PlaysToDrill = new List<string>();

        // ==================== RESULTS (Populated after execution) ====================

        /// <summary>Whether the session has been executed</summary>
        public bool IsCompleted;

        /// <summary>Results of the practice session</summary>
        public PracticeResults Results;

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>Effective duration accounting for intensity</summary>
        public float EffectiveDuration => DurationMinutes * GetIntensityMultiplier();

        /// <summary>Expected fatigue cost for players</summary>
        public float ExpectedFatigueCost => CalculateExpectedFatigue();

        /// <summary>Is this an opponent preparation session?</summary>
        public bool IsGamePrepSession => !string.IsNullOrEmpty(OpponentTeamId);

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Creates a standard development-focused practice.
        /// </summary>
        public static PracticeSession CreateDevelopmentPractice(string teamId, DateTime date)
        {
            return new PracticeSession
            {
                SessionId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                Date = date,
                DurationMinutes = 90,
                PrimaryFocus = PracticeFocus.Development,
                Intensity = PracticeIntensity.Normal
            };
        }

        /// <summary>
        /// Creates a game preparation session targeting a specific opponent.
        /// </summary>
        public static PracticeSession CreateGamePrepPractice(string teamId, DateTime date, string opponentTeamId)
        {
            return new PracticeSession
            {
                SessionId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                Date = date,
                DurationMinutes = 90,
                PrimaryFocus = PracticeFocus.GamePrep,
                SecondaryFocus = PracticeFocus.TeamConcepts,
                OpponentTeamId = opponentTeamId,
                IncludeFilmSession = true,
                Intensity = PracticeIntensity.Normal
            };
        }

        /// <summary>
        /// Creates a light shootaround (game day).
        /// </summary>
        public static PracticeSession CreateShootaround(string teamId, DateTime date, string opponentTeamId = null)
        {
            return new PracticeSession
            {
                SessionId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                Date = date,
                DurationMinutes = 45,
                PrimaryFocus = PracticeFocus.Shootaround,
                OpponentTeamId = opponentTeamId,
                Intensity = PracticeIntensity.Light
            };
        }

        /// <summary>
        /// Creates a recovery/rest day session.
        /// </summary>
        public static PracticeSession CreateRecoverySession(string teamId, DateTime date)
        {
            return new PracticeSession
            {
                SessionId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                Date = date,
                DurationMinutes = 60,
                PrimaryFocus = PracticeFocus.Recovery,
                Intensity = PracticeIntensity.VeryLight
            };
        }

        /// <summary>
        /// Creates an intense team chemistry building session.
        /// </summary>
        public static PracticeSession CreateTeamBuildingSession(string teamId, DateTime date)
        {
            return new PracticeSession
            {
                SessionId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                Date = date,
                DurationMinutes = 75,
                PrimaryFocus = PracticeFocus.TeamBuilding,
                SecondaryFocus = PracticeFocus.TeamConcepts,
                Intensity = PracticeIntensity.Normal
            };
        }

        // ==================== METHODS ====================

        /// <summary>
        /// Adds a drill to the practice schedule.
        /// </summary>
        public void AddDrill(PracticeDrill drill, int durationMinutes, List<string> focusPlayerIds = null)
        {
            Drills.Add(new ScheduledDrill
            {
                Drill = drill,
                DurationMinutes = durationMinutes,
                FocusPlayerIds = focusPlayerIds ?? new List<string>()
            });
        }

        /// <summary>
        /// Gets total drill time scheduled.
        /// </summary>
        public int GetTotalDrillTime()
        {
            int total = 0;
            foreach (var drill in Drills)
            {
                total += drill.DurationMinutes;
            }
            return total;
        }

        /// <summary>
        /// Gets remaining time available for drills.
        /// </summary>
        public int GetRemainingTime()
        {
            // Reserve time for warm-up (10 min) and cool-down (5 min)
            int overhead = 15;
            return Math.Max(0, DurationMinutes - overhead - GetTotalDrillTime());
        }

        /// <summary>
        /// Validates the practice session configuration.
        /// </summary>
        public (bool isValid, List<string> errors) Validate()
        {
            var errors = new List<string>();

            if (DurationMinutes < 30)
                errors.Add("Practice must be at least 30 minutes");

            if (DurationMinutes > 180)
                errors.Add("Practice cannot exceed 3 hours");

            if (GetTotalDrillTime() > DurationMinutes - 10)
                errors.Add("Not enough time for all scheduled drills");

            if (Drills.Count == 0 && PrimaryFocus != PracticeFocus.Recovery)
                errors.Add("No drills scheduled for this practice");

            if (IsGamePrepSession && !IncludeFilmSession && PlaysToDrill.Count == 0)
                errors.Add("Game prep session should include film or play drilling");

            return (errors.Count == 0, errors);
        }

        private float GetIntensityMultiplier()
        {
            return Intensity switch
            {
                PracticeIntensity.VeryLight => 0.5f,
                PracticeIntensity.Light => 0.75f,
                PracticeIntensity.Normal => 1.0f,
                PracticeIntensity.High => 1.25f,
                PracticeIntensity.Intense => 1.5f,
                _ => 1.0f
            };
        }

        private float CalculateExpectedFatigue()
        {
            float baseFatigue = DurationMinutes / 90f * 10f; // Base 10 fatigue for 90 min practice
            return baseFatigue * GetIntensityMultiplier();
        }
    }

    /// <summary>
    /// A drill scheduled within a practice session.
    /// </summary>
    [Serializable]
    public class ScheduledDrill
    {
        public PracticeDrill Drill;
        public int DurationMinutes;

        /// <summary>Specific players to focus on (empty = all participating)</summary>
        public List<string> FocusPlayerIds = new List<string>();

        /// <summary>Notes from coach about this drill</summary>
        public string CoachNotes;
    }

    /// <summary>
    /// Results from a completed practice session.
    /// </summary>
    [Serializable]
    public class PracticeResults
    {
        /// <summary>Overall quality of the practice (0-100)</summary>
        public int OverallQuality;

        /// <summary>Player engagement level (0-100)</summary>
        public int PlayerEngagement;

        /// <summary>Development gains per player</summary>
        public Dictionary<string, PlayerPracticeGains> PlayerGains = new Dictionary<string, PlayerPracticeGains>();

        /// <summary>Play familiarity increases</summary>
        public Dictionary<string, float> PlayFamiliarityGains = new Dictionary<string, float>();

        /// <summary>Opponent preparation bonus gained (0-100)</summary>
        public float OpponentPrepBonus;

        /// <summary>Team chemistry change</summary>
        public float ChemistryChange;

        /// <summary>Notable events during practice</summary>
        public List<PracticeEvent> Events = new List<PracticeEvent>();

        /// <summary>Injuries that occurred</summary>
        public List<PracticeInjury> Injuries = new List<PracticeInjury>();
    }

    /// <summary>
    /// Development gains for a single player from practice.
    /// </summary>
    [Serializable]
    public class PlayerPracticeGains
    {
        public string PlayerId;

        /// <summary>Skill progress gains (attribute name -> progress points)</summary>
        public Dictionary<string, float> SkillProgress = new Dictionary<string, float>();

        /// <summary>Tendency training progress</summary>
        public Dictionary<string, float> TendencyProgress = new Dictionary<string, float>();

        /// <summary>Fatigue incurred</summary>
        public float FatigueIncurred;

        /// <summary>Morale change from practice</summary>
        public float MoraleChange;

        /// <summary>Minutes of quality reps</summary>
        public int QualityMinutes;
    }

    /// <summary>
    /// Notable events that can occur during practice.
    /// </summary>
    [Serializable]
    public class PracticeEvent
    {
        public PracticeEventType Type;
        public string PlayerId;
        public string SecondPlayerId; // For interactions
        public string Description;
        public float Impact; // Positive or negative
    }

    /// <summary>
    /// Injury that occurred during practice.
    /// </summary>
    [Serializable]
    public class PracticeInjury
    {
        public string PlayerId;
        public InjuryType InjuryType;
        public InjurySeverity Severity;
        public int DaysOut;
        public string Description;
    }

    // ==================== ENUMS ====================

    /// <summary>
    /// Primary focus area for a practice session.
    /// </summary>
    public enum PracticeFocus
    {
        /// <summary>Focus on individual player skill development</summary>
        Development,

        /// <summary>Prepare for specific upcoming opponent</summary>
        GamePrep,

        /// <summary>Work on team offensive/defensive concepts</summary>
        TeamConcepts,

        /// <summary>Light game-day shootaround</summary>
        Shootaround,

        /// <summary>Recovery and rest</summary>
        Recovery,

        /// <summary>Team chemistry and bonding activities</summary>
        TeamBuilding,

        /// <summary>Conditioning and fitness</summary>
        Conditioning,

        /// <summary>Install new plays or schemes</summary>
        SchemeInstall
    }

    /// <summary>
    /// Intensity level of practice.
    /// </summary>
    public enum PracticeIntensity
    {
        /// <summary>Minimal exertion, focus on recovery</summary>
        VeryLight,

        /// <summary>Light work, limited contact</summary>
        Light,

        /// <summary>Standard practice intensity</summary>
        Normal,

        /// <summary>Higher intensity, competitive drills</summary>
        High,

        /// <summary>Maximum intensity, simulates game conditions</summary>
        Intense
    }

    /// <summary>
    /// Types of events that can occur during practice.
    /// </summary>
    public enum PracticeEventType
    {
        /// <summary>Player had an excellent practice</summary>
        StandoutPerformance,

        /// <summary>Player struggled in practice</summary>
        PoorPerformance,

        /// <summary>Positive interaction between players</summary>
        PositiveInteraction,

        /// <summary>Conflict or tension between players</summary>
        ConflictIncident,

        /// <summary>Veteran mentored young player</summary>
        MentorMoment,

        /// <summary>Player showed improved skill</summary>
        BreakthroughMoment,

        /// <summary>Player complained about role/minutes</summary>
        RoleComplaint,

        /// <summary>Team showed great chemistry</summary>
        TeamChemistryBoost,

        /// <summary>Practice got heated/competitive</summary>
        CompetitiveDrill,

        /// <summary>Coach had to intervene</summary>
        CoachIntervention
    }
}
