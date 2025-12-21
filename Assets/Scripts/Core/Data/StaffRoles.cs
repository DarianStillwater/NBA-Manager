using System;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Defines the user's role in team management.
    /// Determines what positions must be hired vs. controlled by user.
    /// </summary>
    [Serializable]
    public enum UserRole
    {
        /// <summary>
        /// User acts as GM only - must hire a Head Coach who makes in-game decisions
        /// </summary>
        GMOnly,

        /// <summary>
        /// User acts as Head Coach only - GM is separate (AI or existing)
        /// </summary>
        HeadCoachOnly,

        /// <summary>
        /// User controls both GM and Head Coach roles (default mode)
        /// </summary>
        Both
    }

    /// <summary>
    /// All staff position types available in the organization.
    /// </summary>
    [Serializable]
    public enum StaffPositionType
    {
        // Front Office
        GeneralManager,
        AssistantGM,

        // Coaching Staff
        HeadCoach,
        OffensiveCoordinator,
        DefensiveCoordinator,
        AssistantCoach,

        // Scouting
        Scout
    }

    /// <summary>
    /// Staff position limits per team.
    /// </summary>
    public static class StaffLimits
    {
        /// <summary>Maximum number of scouts per team</summary>
        public const int MaxScouts = 5;

        /// <summary>Maximum number of assistant coaches per team</summary>
        public const int MaxAssistantCoaches = 5;

        /// <summary>Maximum number of coordinators per team (1 offensive + 1 defensive)</summary>
        public const int MaxCoordinators = 2;

        /// <summary>Maximum GMs (always 1)</summary>
        public const int MaxGMs = 1;

        /// <summary>Maximum Assistant GMs</summary>
        public const int MaxAssistantGMs = 1;

        /// <summary>Maximum Head Coaches (always 1)</summary>
        public const int MaxHeadCoaches = 1;

        /// <summary>
        /// Get the maximum allowed for a given position type.
        /// </summary>
        public static int GetMaxForPosition(StaffPositionType position)
        {
            return position switch
            {
                StaffPositionType.GeneralManager => MaxGMs,
                StaffPositionType.AssistantGM => MaxAssistantGMs,
                StaffPositionType.HeadCoach => MaxHeadCoaches,
                StaffPositionType.OffensiveCoordinator => 1,
                StaffPositionType.DefensiveCoordinator => 1,
                StaffPositionType.AssistantCoach => MaxAssistantCoaches,
                StaffPositionType.Scout => MaxScouts,
                _ => 1
            };
        }

        /// <summary>
        /// Get the base salary for a position type.
        /// </summary>
        public static int GetBaseSalary(StaffPositionType position)
        {
            return position switch
            {
                StaffPositionType.GeneralManager => 3_000_000,
                StaffPositionType.AssistantGM => 800_000,
                StaffPositionType.HeadCoach => 4_000_000,
                StaffPositionType.OffensiveCoordinator => 800_000,
                StaffPositionType.DefensiveCoordinator => 800_000,
                StaffPositionType.AssistantCoach => 500_000,
                StaffPositionType.Scout => 300_000,
                _ => 300_000
            };
        }

        /// <summary>
        /// Check if a position is a coaching position (vs front office or scouting).
        /// </summary>
        public static bool IsCoachingPosition(StaffPositionType position)
        {
            return position switch
            {
                StaffPositionType.HeadCoach => true,
                StaffPositionType.OffensiveCoordinator => true,
                StaffPositionType.DefensiveCoordinator => true,
                StaffPositionType.AssistantCoach => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if a position is a front office position.
        /// </summary>
        public static bool IsFrontOfficePosition(StaffPositionType position)
        {
            return position switch
            {
                StaffPositionType.GeneralManager => true,
                StaffPositionType.AssistantGM => true,
                _ => false
            };
        }

        /// <summary>
        /// Get display name for a position.
        /// </summary>
        public static string GetDisplayName(StaffPositionType position)
        {
            return position switch
            {
                StaffPositionType.GeneralManager => "General Manager",
                StaffPositionType.AssistantGM => "Assistant GM",
                StaffPositionType.HeadCoach => "Head Coach",
                StaffPositionType.OffensiveCoordinator => "Offensive Coordinator",
                StaffPositionType.DefensiveCoordinator => "Defensive Coordinator",
                StaffPositionType.AssistantCoach => "Assistant Coach",
                StaffPositionType.Scout => "Scout",
                _ => position.ToString()
            };
        }

        /// <summary>
        /// Get abbreviated name for a position.
        /// </summary>
        public static string GetAbbreviation(StaffPositionType position)
        {
            return position switch
            {
                StaffPositionType.GeneralManager => "GM",
                StaffPositionType.AssistantGM => "AGM",
                StaffPositionType.HeadCoach => "HC",
                StaffPositionType.OffensiveCoordinator => "OC",
                StaffPositionType.DefensiveCoordinator => "DC",
                StaffPositionType.AssistantCoach => "AC",
                StaffPositionType.Scout => "SCT",
                _ => "?"
            };
        }
    }

    /// <summary>
    /// Configuration for the user's role in a specific game/career.
    /// Tracks which positions the user controls and which are AI-controlled.
    /// </summary>
    [Serializable]
    public class UserRoleConfiguration
    {
        /// <summary>The user's selected role</summary>
        public UserRole CurrentRole = UserRole.Both;

        /// <summary>User's GM profile ID (if user is GM or Both)</summary>
        public string UserGMProfileId;

        /// <summary>User's Coach profile ID (if user is Coach or Both)</summary>
        public string UserCoachProfileId;

        /// <summary>AI GM profile ID (if user is Coach-only)</summary>
        public string AIGMProfileId;

        /// <summary>AI Head Coach profile ID (if user is GM-only)</summary>
        public string AICoachProfileId;

        /// <summary>The team the user is associated with</summary>
        public string TeamId;

        /// <summary>Whether the user controls roster decisions (trades, signings, draft)</summary>
        public bool UserControlsRoster => CurrentRole == UserRole.GMOnly || CurrentRole == UserRole.Both;

        /// <summary>Whether the user controls in-game decisions (plays, subs, timeouts)</summary>
        public bool UserControlsGames => CurrentRole == UserRole.HeadCoachOnly || CurrentRole == UserRole.Both;

        /// <summary>Whether this configuration has an AI GM (Coach-only mode)</summary>
        public bool HasAIGM => CurrentRole == UserRole.HeadCoachOnly && !string.IsNullOrEmpty(AIGMProfileId);

        /// <summary>Whether this configuration has an AI Coach (GM-only mode)</summary>
        public bool HasAICoach => CurrentRole == UserRole.GMOnly && !string.IsNullOrEmpty(AICoachProfileId);

        /// <summary>
        /// Get the user's primary profile ID based on their role.
        /// </summary>
        public string GetUserPrimaryProfileId()
        {
            return CurrentRole switch
            {
                UserRole.GMOnly => UserGMProfileId,
                UserRole.HeadCoachOnly => UserCoachProfileId,
                UserRole.Both => UserCoachProfileId ?? UserGMProfileId, // Prefer coach profile for "Both"
                _ => null
            };
        }

        /// <summary>
        /// Get display name for the current role.
        /// </summary>
        public string GetRoleDisplayName()
        {
            return CurrentRole switch
            {
                UserRole.GMOnly => "General Manager",
                UserRole.HeadCoachOnly => "Head Coach",
                UserRole.Both => "GM & Head Coach",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Get description of the current role's responsibilities.
        /// </summary>
        public string GetRoleDescription()
        {
            return CurrentRole switch
            {
                UserRole.GMOnly => "Build the roster through trades, free agency, and the draft. Your hired Head Coach will handle all in-game decisions.",
                UserRole.HeadCoachOnly => "Control all in-game decisions including plays, substitutions, and timeouts. Request roster moves from the GM.",
                UserRole.Both => "Full control over both roster decisions and in-game coaching.",
                _ => ""
            };
        }
    }

    /// <summary>
    /// Tracks the current staff configuration for a team.
    /// </summary>
    [Serializable]
    public class TeamStaffConfiguration
    {
        public string TeamId;
        public UserRole CurrentUserRole;

        // Staff counts
        public int GMCount;
        public int AssistantGMCount;
        public int HeadCoachCount;
        public int OffensiveCoordinatorCount;
        public int DefensiveCoordinatorCount;
        public int AssistantCoachCount;
        public int ScoutCount;

        /// <summary>
        /// Check if there's room to hire for a position.
        /// </summary>
        public bool CanHire(StaffPositionType position)
        {
            int current = GetCurrentCount(position);
            int max = StaffLimits.GetMaxForPosition(position);
            return current < max;
        }

        /// <summary>
        /// Get current count for a position type.
        /// </summary>
        public int GetCurrentCount(StaffPositionType position)
        {
            return position switch
            {
                StaffPositionType.GeneralManager => GMCount,
                StaffPositionType.AssistantGM => AssistantGMCount,
                StaffPositionType.HeadCoach => HeadCoachCount,
                StaffPositionType.OffensiveCoordinator => OffensiveCoordinatorCount,
                StaffPositionType.DefensiveCoordinator => DefensiveCoordinatorCount,
                StaffPositionType.AssistantCoach => AssistantCoachCount,
                StaffPositionType.Scout => ScoutCount,
                _ => 0
            };
        }

        /// <summary>
        /// Increment count when hiring.
        /// </summary>
        public void IncrementCount(StaffPositionType position)
        {
            switch (position)
            {
                case StaffPositionType.GeneralManager: GMCount++; break;
                case StaffPositionType.AssistantGM: AssistantGMCount++; break;
                case StaffPositionType.HeadCoach: HeadCoachCount++; break;
                case StaffPositionType.OffensiveCoordinator: OffensiveCoordinatorCount++; break;
                case StaffPositionType.DefensiveCoordinator: DefensiveCoordinatorCount++; break;
                case StaffPositionType.AssistantCoach: AssistantCoachCount++; break;
                case StaffPositionType.Scout: ScoutCount++; break;
            }
        }

        /// <summary>
        /// Decrement count when firing.
        /// </summary>
        public void DecrementCount(StaffPositionType position)
        {
            switch (position)
            {
                case StaffPositionType.GeneralManager: GMCount = Math.Max(0, GMCount - 1); break;
                case StaffPositionType.AssistantGM: AssistantGMCount = Math.Max(0, AssistantGMCount - 1); break;
                case StaffPositionType.HeadCoach: HeadCoachCount = Math.Max(0, HeadCoachCount - 1); break;
                case StaffPositionType.OffensiveCoordinator: OffensiveCoordinatorCount = Math.Max(0, OffensiveCoordinatorCount - 1); break;
                case StaffPositionType.DefensiveCoordinator: DefensiveCoordinatorCount = Math.Max(0, DefensiveCoordinatorCount - 1); break;
                case StaffPositionType.AssistantCoach: AssistantCoachCount = Math.Max(0, AssistantCoachCount - 1); break;
                case StaffPositionType.Scout: ScoutCount = Math.Max(0, ScoutCount - 1); break;
            }
        }

        /// <summary>
        /// Check if user needs to hire a Head Coach (GM-only mode with no HC).
        /// </summary>
        public bool NeedsHeadCoach => CurrentUserRole == UserRole.GMOnly && HeadCoachCount == 0;

        /// <summary>
        /// Get total staff salary commitment (for budget checking).
        /// </summary>
        public int GetMinimumStaffSalary()
        {
            int total = 0;

            // Add base salaries for each position
            total += GMCount * StaffLimits.GetBaseSalary(StaffPositionType.GeneralManager);
            total += AssistantGMCount * StaffLimits.GetBaseSalary(StaffPositionType.AssistantGM);
            total += HeadCoachCount * StaffLimits.GetBaseSalary(StaffPositionType.HeadCoach);
            total += OffensiveCoordinatorCount * StaffLimits.GetBaseSalary(StaffPositionType.OffensiveCoordinator);
            total += DefensiveCoordinatorCount * StaffLimits.GetBaseSalary(StaffPositionType.DefensiveCoordinator);
            total += AssistantCoachCount * StaffLimits.GetBaseSalary(StaffPositionType.AssistantCoach);
            total += ScoutCount * StaffLimits.GetBaseSalary(StaffPositionType.Scout);

            return total;
        }
    }
}
