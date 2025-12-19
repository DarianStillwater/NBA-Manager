using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Types of tasks that can be assigned to scouts.
    /// </summary>
    [Serializable]
    public enum ScoutTaskType
    {
        /// <summary>Not currently assigned to any task</summary>
        Unassigned,

        /// <summary>Scouting draft prospects (college/international)</summary>
        DraftProspectScouting,

        /// <summary>Advance scouting of upcoming opponents</summary>
        OpponentAdvanceScouting,

        /// <summary>Evaluating potential trade targets</summary>
        TradeTargetEvaluation,

        /// <summary>Evaluating free agent players</summary>
        FreeAgentEvaluation
    }

    /// <summary>
    /// Types of tasks that can be assigned to coaches.
    /// </summary>
    [Serializable]
    public enum CoachTaskType
    {
        /// <summary>Not currently assigned to any task</summary>
        Unassigned,

        /// <summary>Assigned to develop specific players (Assistant Coaches)</summary>
        PlayerDevelopment,

        /// <summary>Provide in-game strategy suggestions (Coordinators)</summary>
        GameStrategy
    }

    /// <summary>
    /// Represents a task assignment for a staff member.
    /// </summary>
    [Serializable]
    public class StaffTaskAssignment
    {
        public string AssignmentId;
        public string StaffId;
        public string TeamId;
        public StaffPositionType StaffPosition;

        [Header("Scout Task")]
        public ScoutTaskType ScoutTask;
        public List<string> ScoutTargetPlayerIds = new List<string>();  // Players being scouted
        public string ScoutTargetTeamId;  // For opponent scouting

        [Header("Coach Task")]
        public CoachTaskType CoachTask;
        public List<string> AssignedPlayerIds = new List<string>();  // Players for development

        [Header("Assignment Info")]
        public DateTime AssignedDate;
        public int DurationDays;  // Expected duration
        public int DaysActive;    // Days since assignment started
        public bool IsActive;

        [Header("Progress")]
        [Range(0f, 1f)]
        public float Progress;  // 0-1 completion

        /// <summary>
        /// Create a scout assignment.
        /// </summary>
        public static StaffTaskAssignment CreateScoutAssignment(
            string scoutId,
            string teamId,
            ScoutTaskType taskType,
            List<string> targetPlayerIds = null,
            string targetTeamId = null)
        {
            return new StaffTaskAssignment
            {
                AssignmentId = $"ASSIGN_{Guid.NewGuid().ToString().Substring(0, 8)}",
                StaffId = scoutId,
                TeamId = teamId,
                StaffPosition = StaffPositionType.Scout,
                ScoutTask = taskType,
                ScoutTargetPlayerIds = targetPlayerIds ?? new List<string>(),
                ScoutTargetTeamId = targetTeamId,
                CoachTask = CoachTaskType.Unassigned,
                AssignedDate = DateTime.Now,
                DurationDays = GetDefaultDuration(taskType),
                DaysActive = 0,
                IsActive = true,
                Progress = 0f
            };
        }

        /// <summary>
        /// Create a coach assignment (player development).
        /// </summary>
        public static StaffTaskAssignment CreateCoachAssignment(
            string coachId,
            string teamId,
            StaffPositionType position,
            CoachTaskType taskType,
            List<string> assignedPlayerIds = null)
        {
            return new StaffTaskAssignment
            {
                AssignmentId = $"ASSIGN_{Guid.NewGuid().ToString().Substring(0, 8)}",
                StaffId = coachId,
                TeamId = teamId,
                StaffPosition = position,
                CoachTask = taskType,
                AssignedPlayerIds = assignedPlayerIds ?? new List<string>(),
                ScoutTask = ScoutTaskType.Unassigned,
                AssignedDate = DateTime.Now,
                DurationDays = 0,  // Ongoing until changed
                DaysActive = 0,
                IsActive = true,
                Progress = 0f
            };
        }

        /// <summary>
        /// Get default duration for a scout task type.
        /// </summary>
        public static int GetDefaultDuration(ScoutTaskType taskType)
        {
            return taskType switch
            {
                ScoutTaskType.DraftProspectScouting => 14,  // 2 weeks per prospect batch
                ScoutTaskType.OpponentAdvanceScouting => 5,  // 5 days before game
                ScoutTaskType.TradeTargetEvaluation => 7,    // 1 week per target
                ScoutTaskType.FreeAgentEvaluation => 7,      // 1 week per FA
                _ => 7
            };
        }

        /// <summary>
        /// Get display name for the current task.
        /// </summary>
        public string GetTaskDisplayName()
        {
            if (StaffPosition == StaffPositionType.Scout)
            {
                return ScoutTask switch
                {
                    ScoutTaskType.DraftProspectScouting => "Draft Scouting",
                    ScoutTaskType.OpponentAdvanceScouting => "Opponent Scouting",
                    ScoutTaskType.TradeTargetEvaluation => "Trade Targets",
                    ScoutTaskType.FreeAgentEvaluation => "Free Agent Eval",
                    _ => "Unassigned"
                };
            }
            else
            {
                return CoachTask switch
                {
                    CoachTaskType.PlayerDevelopment => "Player Development",
                    CoachTaskType.GameStrategy => "Game Strategy",
                    _ => "Unassigned"
                };
            }
        }

        /// <summary>
        /// Check if this assignment is complete.
        /// </summary>
        public bool IsComplete => DurationDays > 0 && DaysActive >= DurationDays;

        /// <summary>
        /// Advance the assignment by one day.
        /// </summary>
        public void AdvanceDay()
        {
            if (IsActive && !IsComplete)
            {
                DaysActive++;
                if (DurationDays > 0)
                {
                    Progress = Mathf.Clamp01((float)DaysActive / DurationDays);
                }
            }
        }
    }

    /// <summary>
    /// Result of a scout's evaluation (affected by scout quality).
    /// </summary>
    [Serializable]
    public class ScoutEvaluationResult
    {
        public string EvaluationId;
        public string ScoutId;
        public string TargetPlayerId;
        public ScoutTaskType TaskType;

        [Header("Scout Info")]
        public string ScoutName;
        public int ScoutRating;

        [Header("Evaluation")]
        public int EstimatedOverall;       // Scout's estimate (may be inaccurate)
        public int ActualOverall;          // True value (hidden from player)
        public int EstimatedPotential;     // For draft prospects
        public int ActualPotential;        // True potential (hidden)

        [Header("Accuracy")]
        [Range(0f, 1f)]
        public float OverallAccuracy;      // How close estimate is to actual
        [Range(0f, 1f)]
        public float PotentialAccuracy;    // How close potential estimate is

        [Header("Report")]
        public string Strengths;
        public string Weaknesses;
        public string RecommendedAction;   // "Sign", "Pass", "Monitor", etc.
        public int ConfidenceLevel;        // Scout's confidence in evaluation (0-100)

        public DateTime EvaluationDate;

        /// <summary>
        /// Calculate how far off the estimate was.
        /// </summary>
        public int GetOverallError() => Math.Abs(EstimatedOverall - ActualOverall);

        /// <summary>
        /// Get grade for the evaluation accuracy.
        /// </summary>
        public string GetAccuracyGrade()
        {
            int error = GetOverallError();
            if (error <= 2) return "A";
            if (error <= 5) return "B";
            if (error <= 8) return "C";
            if (error <= 12) return "D";
            return "F";
        }
    }

    /// <summary>
    /// Result of coordinator's in-game suggestion.
    /// </summary>
    [Serializable]
    public class CoordinatorSuggestion
    {
        public string SuggestionId;
        public string CoordinatorId;
        public string CoordinatorName;
        public bool IsOffensive;  // True = OC, False = DC

        [Header("Suggestion")]
        public string SuggestionType;      // "Switch to Zone", "Attack the Paint", etc.
        public string Reasoning;           // Why this is suggested
        public int ConfidenceLevel;        // How confident coordinator is (0-100)

        [Header("Expected Impact")]
        public float ExpectedImpact;       // Positive = good, negative = bad
        public float ActualImpact;         // After applied (for learning)

        [Header("Context")]
        public int Quarter;
        public int TimeRemainingSeconds;
        public int ScoreDifferential;      // Positive = winning
        public string OpponentTeamId;

        /// <summary>
        /// Whether the suggestion was effective (actual impact positive).
        /// </summary>
        public bool WasEffective => ActualImpact > 0;
    }

    /// <summary>
    /// Player development bonus from assistant coach assignment.
    /// </summary>
    [Serializable]
    public class DevelopmentAssignment
    {
        public string AssignmentId;
        public string CoachId;
        public string CoachName;
        public string PlayerId;
        public string PlayerName;
        public string TeamId;

        [Header("Coach Info")]
        public int CoachDevelopmentRating;
        public List<CoachSpecialization> CoachSpecializations;

        [Header("Development Bonus")]
        [Range(0f, 0.5f)]
        public float DevelopmentBonus;     // Percentage bonus to player development
        public string FocusArea;           // "Shooting", "Defense", etc.

        [Header("Duration")]
        public DateTime StartDate;
        public int WeeksAssigned;

        /// <summary>
        /// Calculate development bonus based on coach rating.
        /// Formula: 1 + floor(PlayerDevelopment / 30) gives 1-4 capacity
        /// </summary>
        public static int CalculateCoachCapacity(int playerDevelopmentRating)
        {
            return 1 + (playerDevelopmentRating / 30);
        }

        /// <summary>
        /// Calculate the development bonus percentage.
        /// Higher rated coaches provide larger bonuses.
        /// </summary>
        public static float CalculateDevelopmentBonus(int coachDevelopmentRating, bool hasMatchingSpecialization)
        {
            // Base bonus: 5-25% based on rating
            float baseBonus = 0.05f + (coachDevelopmentRating / 100f) * 0.20f;

            // Specialization bonus: +5%
            if (hasMatchingSpecialization)
            {
                baseBonus += 0.05f;
            }

            return Mathf.Clamp(baseBonus, 0f, 0.35f);
        }
    }

    /// <summary>
    /// Save data for staff assignments.
    /// </summary>
    [Serializable]
    public class StaffAssignmentSaveData
    {
        public List<StaffTaskAssignment> ActiveAssignments = new List<StaffTaskAssignment>();
        public List<ScoutEvaluationResult> RecentEvaluations = new List<ScoutEvaluationResult>();
        public List<DevelopmentAssignment> DevelopmentAssignments = new List<DevelopmentAssignment>();
    }
}
