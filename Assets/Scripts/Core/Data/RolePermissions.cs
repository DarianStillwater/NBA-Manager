namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Single source of truth for what the user may do given their chosen role
    /// (GM-only / Head-Coach-only / Both). All UI gating and control-flow branches
    /// read these flags — never UserRoleConfiguration directly — so the rules live
    /// in one place. Null config (tests, legacy saves) defaults to Both: everything
    /// allowed.
    /// </summary>
    public static class RolePermissions
    {
        /// <summary>Test seam / explicit source. When null, falls back to GameManager.Instance.</summary>
        public static System.Func<UserRoleConfiguration> ConfigSource;

        private static UserRoleConfiguration Config =>
            ConfigSource != null ? ConfigSource() : GameManager.Instance?.UserRoleConfig;

        /// <summary>Coach power: edit the starting lineup, rotation, and team strategy.</summary>
        public static bool CanEditLineupAndStrategy => Config?.UserControlsGames ?? true;

        /// <summary>Coach power: take the sideline in an interactive match.</summary>
        public static bool CanPlayInteractiveMatch => Config?.UserControlsGames ?? true;

        /// <summary>Coach power: run practice plans, mentorship pairing, tendency coaching.</summary>
        public static bool CanRunDevelopment => Config?.UserControlsGames ?? true;

        /// <summary>GM power: trades, free agency, waivers, re-signings, draft picks.</summary>
        public static bool CanMakeRosterMoves => Config?.UserControlsRoster ?? true;

        /// <summary>GM power: hire/fire staff and assign scouts.</summary>
        public static bool CanManageStaffAndScouting => Config?.UserControlsRoster ?? true;

        /// <summary>The user's role, defaulting to Both when no config exists.</summary>
        public static UserRole Role => Config?.CurrentRole ?? UserRole.Both;

        /// <summary>Display name of the AI head coach running the bench in GM-only mode.</summary>
        public static string AICoachName =>
            GameManager.Instance?.GetAICoach()?.FullName ?? "your head coach";

        /// <summary>Display name of the AI general manager running the front office in coach-only mode.</summary>
        public static string AIGMName =>
            GameManager.Instance?.GetAIGM()?.FullName ?? "your general manager";

        /// <summary>
        /// One-line explanation for a locked feature, naming who handles it.
        /// Pass true for coach-side features (locked in GM-only mode),
        /// false for GM-side features (locked in coach-only mode).
        /// </summary>
        public static string LockedNote(bool coachFeature)
        {
            return coachFeature
                ? $"Head coach {AICoachName} handles this."
                : $"General manager {AIGMName} handles this.";
        }

        /// <summary>Panel ids hidden entirely for the current role (whole panel is foreign).</summary>
        public static bool IsPanelHidden(string panelId)
        {
            switch (Role)
            {
                case UserRole.HeadCoachOnly:
                    return panelId == "Staff";      // hiring/firing/scouting are GM powers
                case UserRole.GMOnly:
                    return panelId == "Development"; // practice/mentorship are coach powers
                default:
                    return false;
            }
        }
    }
}
