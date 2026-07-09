using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 7-B commit 1: RolePermissions is the single source of truth
    /// for role gating — every flag per role, the null-config default (Both),
    /// panel hiding, and the save-shape round trip of UserRoleConfiguration.
    /// </summary>
    public class RolePermissionsTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            try
            {
                TestBothRoleAllowsEverything();
                TestGMOnlyFlags();
                TestCoachOnlyFlags();
                TestNullConfigDefaultsToBoth();
                TestPanelHiding();
                TestSaveRoundTrip();
            }
            finally
            {
                RolePermissions.ConfigSource = null;
            }

            return (_passed, _failed);
        }

        private void UseRole(UserRole role)
        {
            var cfg = new UserRoleConfiguration { CurrentRole = role, TeamId = "LAL" };
            RolePermissions.ConfigSource = () => cfg;
        }

        private void TestBothRoleAllowsEverything()
        {
            UseRole(UserRole.Both);
            Assert(RolePermissions.CanEditLineupAndStrategy, "Both: can edit lineup/strategy");
            Assert(RolePermissions.CanPlayInteractiveMatch, "Both: can play matches");
            Assert(RolePermissions.CanRunDevelopment, "Both: can run development");
            Assert(RolePermissions.CanMakeRosterMoves, "Both: can make roster moves");
            Assert(RolePermissions.CanManageStaffAndScouting, "Both: can manage staff");
        }

        private void TestGMOnlyFlags()
        {
            UseRole(UserRole.GMOnly);
            Assert(!RolePermissions.CanEditLineupAndStrategy, "GM-only: lineup/strategy locked");
            Assert(!RolePermissions.CanPlayInteractiveMatch, "GM-only: no interactive matches");
            Assert(!RolePermissions.CanRunDevelopment, "GM-only: development locked");
            Assert(RolePermissions.CanMakeRosterMoves, "GM-only: roster moves allowed");
            Assert(RolePermissions.CanManageStaffAndScouting, "GM-only: staff allowed");
        }

        private void TestCoachOnlyFlags()
        {
            UseRole(UserRole.HeadCoachOnly);
            Assert(RolePermissions.CanEditLineupAndStrategy, "Coach-only: lineup/strategy allowed");
            Assert(RolePermissions.CanPlayInteractiveMatch, "Coach-only: interactive matches allowed");
            Assert(RolePermissions.CanRunDevelopment, "Coach-only: development allowed");
            Assert(!RolePermissions.CanMakeRosterMoves, "Coach-only: roster moves locked");
            Assert(!RolePermissions.CanManageStaffAndScouting, "Coach-only: staff locked");
        }

        private void TestNullConfigDefaultsToBoth()
        {
            RolePermissions.ConfigSource = () => null;
            Assert(RolePermissions.CanEditLineupAndStrategy, "Null config: lineup defaults open");
            Assert(RolePermissions.CanMakeRosterMoves, "Null config: roster defaults open");
            AssertEqual(UserRole.Both, RolePermissions.Role, "Null config: role reads Both");
            Assert(!RolePermissions.IsPanelHidden("Staff"), "Null config: nothing hidden");
        }

        private void TestPanelHiding()
        {
            UseRole(UserRole.HeadCoachOnly);
            Assert(RolePermissions.IsPanelHidden("Staff"), "Coach-only hides Staff");
            Assert(!RolePermissions.IsPanelHidden("Development"), "Coach-only keeps Development");
            Assert(!RolePermissions.IsPanelHidden("FrontOffice"), "Coach-only keeps Front Office (read-only)");

            UseRole(UserRole.GMOnly);
            Assert(RolePermissions.IsPanelHidden("Development"), "GM-only hides Development");
            Assert(!RolePermissions.IsPanelHidden("Staff"), "GM-only keeps Staff");
            Assert(!RolePermissions.IsPanelHidden("Roster"), "GM-only keeps Roster");

            UseRole(UserRole.Both);
            Assert(!RolePermissions.IsPanelHidden("Staff") && !RolePermissions.IsPanelHidden("Development"),
                "Both hides nothing");
        }

        private void TestSaveRoundTrip()
        {
            var cfg = new UserRoleConfiguration
            {
                CurrentRole = UserRole.GMOnly,
                UserGMProfileId = "GM_1",
                AICoachProfileId = "COACH_AI_7",
                TeamId = "BOS"
            };

            var json = JsonUtility.ToJson(cfg);
            var back = JsonUtility.FromJson<UserRoleConfiguration>(json);

            AssertEqual(UserRole.GMOnly, back.CurrentRole, "Role survives JSON round trip");
            AssertEqual("COACH_AI_7", back.AICoachProfileId, "AI coach profile id survives");
            AssertEqual("BOS", back.TeamId, "Team id survives");
            Assert(back.HasAICoach, "HasAICoach recomputes after load");
            Assert(!back.UserControlsGames, "UserControlsGames recomputes after load");
        }
    }
}
