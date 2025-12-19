using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Type of career transition
    /// </summary>
    [Serializable]
    public enum TransitionType
    {
        Promotion,      // Moving up within same track
        Demotion,       // Moving down within same track (usually after firing)
        TrackSwitch,    // Moving between coaching <-> front office
        Bridge,         // Coordinator <-> AGM lateral moves
        Hiring,         // Being hired from unemployment
        Firing,         // Being let go
        Resignation,    // Voluntarily leaving
        Retirement      // Leaving basketball entirely
    }

    /// <summary>
    /// Defines the requirements for a specific career transition
    /// </summary>
    [Serializable]
    public class TransitionRequirement
    {
        public UnifiedRole FromRole;
        public UnifiedRole ToRole;
        public TransitionType Type;

        [Header("Time Requirements")]
        public int MinYearsInCurrentRole;
        public int MinTotalExperience;  // Total years in profession

        [Header("Reputation Requirements")]
        public int MinReputation;
        public int MinTrackReputation;  // Specific to coaching or FO track

        [Header("Performance Requirements")]
        public int MinPlayoffAppearances;
        public float MinWinPercentage;  // 0.0 to 1.0
        public int MinChampionships;

        [Header("Position Requirements")]
        public bool RequiresOpenPosition;
        public string Notes;

        /// <summary>
        /// Check if a profile meets all requirements for this transition
        /// </summary>
        public (bool eligible, string reason) CheckEligibility(UnifiedCareerProfile profile)
        {
            // Time requirements
            if (profile.YearsInCurrentRole < MinYearsInCurrentRole)
            {
                return (false, $"Need {MinYearsInCurrentRole} years in current role (have {profile.YearsInCurrentRole})");
            }

            int totalExp = profile.TotalCoachingYears + profile.TotalFrontOfficeYears;
            if (totalExp < MinTotalExperience)
            {
                return (false, $"Need {MinTotalExperience} years total experience (have {totalExp})");
            }

            // Reputation requirements
            if (profile.OverallReputation < MinReputation)
            {
                return (false, $"Need {MinReputation} reputation (have {profile.OverallReputation})");
            }

            // Track-specific reputation
            if (MinTrackReputation > 0)
            {
                int trackRep = Type == TransitionType.TrackSwitch
                    ? (IsCoachingRole(ToRole) ? profile.CoachingReputation : profile.FrontOfficeReputation)
                    : (IsCoachingRole(FromRole) ? profile.CoachingReputation : profile.FrontOfficeReputation);

                if (trackRep < MinTrackReputation)
                {
                    return (false, $"Need {MinTrackReputation} track reputation (have {trackRep})");
                }
            }

            // Performance requirements
            if (profile.TotalPlayoffAppearances < MinPlayoffAppearances)
            {
                return (false, $"Need {MinPlayoffAppearances} playoff appearances (have {profile.TotalPlayoffAppearances})");
            }

            if (MinWinPercentage > 0 && profile.GetWinPercentage() < MinWinPercentage)
            {
                return (false, $"Need {MinWinPercentage:P0} win rate (have {profile.GetWinPercentage():P0})");
            }

            if (profile.TotalChampionships < MinChampionships)
            {
                return (false, $"Need {MinChampionships} championships (have {profile.TotalChampionships})");
            }

            return (true, "Eligible");
        }

        private static bool IsCoachingRole(UnifiedRole role)
        {
            return role == UnifiedRole.AssistantCoach ||
                   role == UnifiedRole.PositionCoach ||
                   role == UnifiedRole.Coordinator ||
                   role == UnifiedRole.HeadCoach;
        }
    }

    /// <summary>
    /// Static class containing all valid career transitions and their requirements
    /// </summary>
    public static class CareerTransitionRules
    {
        private static List<TransitionRequirement> _allTransitions;

        /// <summary>
        /// Get all defined transition requirements
        /// </summary>
        public static List<TransitionRequirement> GetAllTransitions()
        {
            if (_allTransitions == null)
            {
                InitializeTransitions();
            }
            return _allTransitions;
        }

        /// <summary>
        /// Get the requirement for a specific transition
        /// </summary>
        public static TransitionRequirement GetTransitionRequirement(UnifiedRole from, UnifiedRole to)
        {
            var transitions = GetAllTransitions();
            return transitions.Find(t => t.FromRole == from && t.ToRole == to);
        }

        /// <summary>
        /// Check if a transition is valid and get requirements
        /// </summary>
        public static (bool isValid, TransitionRequirement requirement) IsValidTransition(UnifiedRole from, UnifiedRole to)
        {
            var req = GetTransitionRequirement(from, to);
            return (req != null, req);
        }

        /// <summary>
        /// Get all possible transitions from a given role
        /// </summary>
        public static List<TransitionRequirement> GetPossibleTransitionsFrom(UnifiedRole role)
        {
            var transitions = GetAllTransitions();
            return transitions.FindAll(t => t.FromRole == role);
        }

        /// <summary>
        /// Check if a profile is eligible for any promotion or track switch
        /// </summary>
        public static List<(TransitionRequirement req, bool eligible, string reason)> GetAllEligibleTransitions(UnifiedCareerProfile profile)
        {
            var results = new List<(TransitionRequirement, bool, string)>();
            var possibleTransitions = GetPossibleTransitionsFrom(profile.CurrentRole);

            foreach (var transition in possibleTransitions)
            {
                var (eligible, reason) = transition.CheckEligibility(profile);
                results.Add((transition, eligible, reason));
            }

            return results;
        }

        private static void InitializeTransitions()
        {
            _allTransitions = new List<TransitionRequirement>
            {
                // ==================== COACHING TRACK PROMOTIONS ====================

                // Assistant Coach → Position Coach
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.AssistantCoach,
                    ToRole = UnifiedRole.PositionCoach,
                    Type = TransitionType.Promotion,
                    MinYearsInCurrentRole = 3,
                    MinTotalExperience = 0,
                    MinReputation = 0,
                    MinPlayoffAppearances = 0,
                    MinWinPercentage = 0f,
                    RequiresOpenPosition = false,
                    Notes = "Standard coaching promotion"
                },

                // Position Coach → Coordinator
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.PositionCoach,
                    ToRole = UnifiedRole.Coordinator,
                    Type = TransitionType.Promotion,
                    MinYearsInCurrentRole = 2,
                    MinTotalExperience = 0,
                    MinReputation = 45,
                    MinPlayoffAppearances = 0,
                    MinWinPercentage = 0f,
                    RequiresOpenPosition = false,
                    Notes = "Standard coaching promotion"
                },

                // Coordinator → Head Coach
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Coordinator,
                    ToRole = UnifiedRole.HeadCoach,
                    Type = TransitionType.Promotion,
                    MinYearsInCurrentRole = 2,
                    MinTotalExperience = 0,
                    MinReputation = 55,
                    MinPlayoffAppearances = 0,
                    MinWinPercentage = 0f,
                    RequiresOpenPosition = true,
                    Notes = "Top of coaching track - requires open HC position"
                },

                // ==================== FRONT OFFICE TRACK PROMOTIONS ====================

                // Scout → Assistant GM
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Scout,
                    ToRole = UnifiedRole.AssistantGM,
                    Type = TransitionType.Promotion,
                    MinYearsInCurrentRole = 3,
                    MinTotalExperience = 0,
                    MinReputation = 0,
                    MinPlayoffAppearances = 0,
                    MinWinPercentage = 0f,
                    RequiresOpenPosition = false,
                    Notes = "Standard front office promotion"
                },

                // Assistant GM → General Manager
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.AssistantGM,
                    ToRole = UnifiedRole.GeneralManager,
                    Type = TransitionType.Promotion,
                    MinYearsInCurrentRole = 3,
                    MinTotalExperience = 0,
                    MinReputation = 55,
                    MinPlayoffAppearances = 0,
                    MinWinPercentage = 0f,
                    RequiresOpenPosition = true,
                    Notes = "Top of front office track - requires open GM position"
                },

                // ==================== CROSS-TRACK TRANSITIONS ====================

                // Head Coach → General Manager
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.HeadCoach,
                    ToRole = UnifiedRole.GeneralManager,
                    Type = TransitionType.TrackSwitch,
                    MinYearsInCurrentRole = 3,
                    MinTotalExperience = 5,
                    MinReputation = 65,
                    MinPlayoffAppearances = 2,
                    MinWinPercentage = 0.50f,
                    MinChampionships = 0,
                    RequiresOpenPosition = true,
                    Notes = "HC to GM track switch - requires strong coaching record"
                },

                // General Manager → Head Coach
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.GeneralManager,
                    ToRole = UnifiedRole.HeadCoach,
                    Type = TransitionType.TrackSwitch,
                    MinYearsInCurrentRole = 2,
                    MinTotalExperience = 4,
                    MinReputation = 60,
                    MinPlayoffAppearances = 1,
                    MinWinPercentage = 0f,
                    RequiresOpenPosition = true,
                    Notes = "GM to HC track switch - requires some playoff success"
                },

                // Scout → Assistant Coach
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Scout,
                    ToRole = UnifiedRole.AssistantCoach,
                    Type = TransitionType.TrackSwitch,
                    MinYearsInCurrentRole = 2,
                    MinTotalExperience = 0,
                    MinReputation = 40,
                    MinPlayoffAppearances = 0,
                    MinWinPercentage = 0f,
                    RequiresOpenPosition = true,
                    Notes = "Scout to coaching track switch"
                },

                // ==================== BRIDGE TRANSITIONS (Coordinator <-> AGM) ====================

                // Coordinator → Assistant GM
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Coordinator,
                    ToRole = UnifiedRole.AssistantGM,
                    Type = TransitionType.Bridge,
                    MinYearsInCurrentRole = 2,
                    MinTotalExperience = 4,
                    MinReputation = 50,
                    MinPlayoffAppearances = 0,
                    MinWinPercentage = 0f,
                    RequiresOpenPosition = true,
                    Notes = "Bridge from coaching to front office at coordinator level"
                },

                // Assistant GM → Coordinator
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.AssistantGM,
                    ToRole = UnifiedRole.Coordinator,
                    Type = TransitionType.Bridge,
                    MinYearsInCurrentRole = 2,
                    MinTotalExperience = 4,
                    MinReputation = 50,
                    MinPlayoffAppearances = 0,
                    MinWinPercentage = 0f,
                    RequiresOpenPosition = true,
                    Notes = "Bridge from front office to coaching at coordinator level"
                },

                // ==================== RE-ENTRY FROM UNEMPLOYMENT ====================

                // Unemployed → Assistant Coach (re-entry to coaching)
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Unemployed,
                    ToRole = UnifiedRole.AssistantCoach,
                    Type = TransitionType.Hiring,
                    MinYearsInCurrentRole = 0,
                    MinTotalExperience = 0,
                    MinReputation = 0,
                    RequiresOpenPosition = true,
                    Notes = "Re-entry to coaching at assistant level"
                },

                // Unemployed → Position Coach (experienced coach re-entry)
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Unemployed,
                    ToRole = UnifiedRole.PositionCoach,
                    Type = TransitionType.Hiring,
                    MinYearsInCurrentRole = 0,
                    MinTotalExperience = 3,
                    MinReputation = 40,
                    RequiresOpenPosition = true,
                    Notes = "Experienced coach re-entry"
                },

                // Unemployed → Coordinator (very experienced coach re-entry)
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Unemployed,
                    ToRole = UnifiedRole.Coordinator,
                    Type = TransitionType.Hiring,
                    MinYearsInCurrentRole = 0,
                    MinTotalExperience = 5,
                    MinReputation = 50,
                    MinPlayoffAppearances = 1,
                    RequiresOpenPosition = true,
                    Notes = "Senior coach re-entry after firing"
                },

                // Unemployed → Head Coach (fired HC getting another chance)
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Unemployed,
                    ToRole = UnifiedRole.HeadCoach,
                    Type = TransitionType.Hiring,
                    MinYearsInCurrentRole = 0,
                    MinTotalExperience = 6,
                    MinReputation = 55,
                    MinPlayoffAppearances = 2,
                    RequiresOpenPosition = true,
                    Notes = "Former HC getting another head coaching job"
                },

                // Unemployed → Scout (re-entry to front office)
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Unemployed,
                    ToRole = UnifiedRole.Scout,
                    Type = TransitionType.Hiring,
                    MinYearsInCurrentRole = 0,
                    MinTotalExperience = 0,
                    MinReputation = 0,
                    RequiresOpenPosition = true,
                    Notes = "Re-entry to front office at scout level"
                },

                // Unemployed → Assistant GM (experienced FO re-entry)
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Unemployed,
                    ToRole = UnifiedRole.AssistantGM,
                    Type = TransitionType.Hiring,
                    MinYearsInCurrentRole = 0,
                    MinTotalExperience = 4,
                    MinReputation = 45,
                    RequiresOpenPosition = true,
                    Notes = "Experienced front office re-entry"
                },

                // Unemployed → General Manager (fired GM getting another chance)
                new TransitionRequirement
                {
                    FromRole = UnifiedRole.Unemployed,
                    ToRole = UnifiedRole.GeneralManager,
                    Type = TransitionType.Hiring,
                    MinYearsInCurrentRole = 0,
                    MinTotalExperience = 5,
                    MinReputation = 55,
                    MinPlayoffAppearances = 1,
                    RequiresOpenPosition = true,
                    Notes = "Former GM getting another GM job"
                }
            };
        }

        /// <summary>
        /// Get a description of the requirements for display
        /// </summary>
        public static string GetRequirementDescription(TransitionRequirement req)
        {
            var parts = new List<string>();

            if (req.MinYearsInCurrentRole > 0)
                parts.Add($"{req.MinYearsInCurrentRole}+ years in current role");

            if (req.MinTotalExperience > 0)
                parts.Add($"{req.MinTotalExperience}+ years experience");

            if (req.MinReputation > 0)
                parts.Add($"{req.MinReputation}+ reputation");

            if (req.MinPlayoffAppearances > 0)
                parts.Add($"{req.MinPlayoffAppearances}+ playoff appearances");

            if (req.MinWinPercentage > 0)
                parts.Add($"{req.MinWinPercentage:P0}+ win rate");

            if (req.MinChampionships > 0)
                parts.Add($"{req.MinChampionships}+ championships");

            if (req.RequiresOpenPosition)
                parts.Add("Open position required");

            return parts.Count > 0 ? string.Join(", ", parts) : "No special requirements";
        }

        /// <summary>
        /// Check if transitioning from one role to another would be a track switch
        /// </summary>
        public static bool IsTrackSwitch(UnifiedRole from, UnifiedRole to)
        {
            bool fromCoaching = IsCoachingRole(from);
            bool toCoaching = IsCoachingRole(to);

            // Ignore special states
            if (from == UnifiedRole.Unemployed || from == UnifiedRole.Retired)
                return false;
            if (to == UnifiedRole.Unemployed || to == UnifiedRole.Retired)
                return false;

            return fromCoaching != toCoaching;
        }

        private static bool IsCoachingRole(UnifiedRole role)
        {
            return role == UnifiedRole.AssistantCoach ||
                   role == UnifiedRole.PositionCoach ||
                   role == UnifiedRole.Coordinator ||
                   role == UnifiedRole.HeadCoach;
        }
    }
}
