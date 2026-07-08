using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the development desk: the weekly practice cycle runs and costs
    /// energy, mentorship pairings assign/dissolve and survive saves, tendency
    /// coaching accrues real improvement, and desk settings round-trip.
    /// </summary>
    public class DevelopmentDeskTest : BaseTest
    {
        private PlayerDatabase _db;
        private Dictionary<string, Team> _teams;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestPracticeCycle();
            TestMentorshipPairing();
            TestTendencyCoaching();
            TestSaveRoundTrip();

            return (_passed, _failed);
        }

        // ==================== FIXTURE ====================

        private Team BuildTeam(string teamId, int count)
        {
            var team = new Team { TeamId = teamId, City = teamId, Nickname = teamId };
            for (int i = 0; i < count; i++)
            {
                string pid = $"{teamId.ToLower()}{i}";
                _db.AddPlayer(new Player
                {
                    PlayerId = pid,
                    FirstName = "D",
                    LastName = pid,
                    TeamId = teamId,
                    Position = (Position)(i % 5 + 1),
                    BirthDate = DateTime.Now.AddYears(-(i < 3 ? 32 : 22)),
                    YearsPro = i < 3 ? 9 : 1,
                    Shot_Three = 65, Finishing_Rim = 65, Defense_Perimeter = 65,
                    Defense_Interior = 65, Speed = 65, Strength = 65, Vertical = 65,
                    BallHandling = 65, Passing = 65, BasketballIQ = 70,
                    HiddenPotential = 85, Stamina = 70,
                    Energy = 100, Morale = 75, Form = 50,
                    Tendencies = new PlayerTendencies()
                });
                team.RosterPlayerIds.Add(pid);
            }
            for (int i = 0; i < 5 && i < count; i++) team.StartingLineupIds[i] = team.RosterPlayerIds[i];
            _teams[teamId] = team;
            return team;
        }

        private DevelopmentSystem FreshDesk(out PlayerDevelopmentManager dev)
        {
            _db = new PlayerDatabase();
            _teams = new Dictionary<string, Team>();
            BuildTeam("AAA", 10);
            dev = new PlayerDevelopmentManager(_db, 2026);
            return new DevelopmentSystem(_db, dev, new InjuryManager(),
                id => _teams.TryGetValue(id ?? "", out var t) ? t : null);
        }

        // ==================== TESTS ====================

        private void TestPracticeCycle()
        {
            var desk = FreshDesk(out _);
            new MentorshipManager(); // fresh singleton so shared state can't leak in

            UnityEngine.Random.InitState(55);
            desk.WeeklyFocus = PracticeFocus.Development;
            desk.WeeklyIntensity = PracticeIntensity.High;

            desk.RunWeeklyDevelopment("AAA", new DateTime(2026, 11, 4), null);

            Assert(!string.IsNullOrEmpty(desk.LastPracticeSummary), "Practice produces a summary");
            float avgEnergy = _teams["AAA"].RosterPlayerIds
                .Average(id => _db.GetPlayer(id).Energy);
            Assert(avgEnergy < 100f, $"Practice costs energy (avg {avgEnergy:0.0})");
        }

        private void TestMentorshipPairing()
        {
            FreshDesk(out _);
            var mm = new MentorshipManager();

            var mentor = _db.GetPlayer("aaa0");   // 9-year vet
            var mentee = _db.GetPlayer("aaa5");   // 1-year kid

            // Guarantee a viable, willing mentor regardless of generated personality
            var profile = mm.GetOrCreateMentorProfile(mentor);
            profile.Willingness = 85;
            profile.TeachingAbility = 80;

            var (ok, message, rel) = mm.AssignMentor(mentor, mentee, "AAA", MentorFocusArea.General);
            Assert(ok, $"Pairing a vet with a kid succeeds ({message})");
            AssertEqual(1, mm.GetTeamRelationships("AAA").Count(r => r.IsActive), "Pairing is active");
            Assert(mm.GetMenteeCurrentMentor("aaa5") != null, "Mentee lookup finds the mentor");

            // A mentee can't have two mentors
            var vet2Profile = mm.GetOrCreateMentorProfile(_db.GetPlayer("aaa1"));
            vet2Profile.Willingness = 85; vet2Profile.TeachingAbility = 80;
            var (dup, _, _) = mm.AssignMentor(_db.GetPlayer("aaa1"), mentee, "AAA", MentorFocusArea.Defense);
            Assert(!dup, "A mentee cannot be double-mentored");

            var (dissolved, _) = mm.DissolveMentorship(rel.RelationshipId, MentorshipEndReason.CoachDissolved);
            Assert(dissolved, "Dissolving the pairing succeeds");
            AssertEqual(0, mm.GetTeamRelationships("AAA").Count(r => r.IsActive), "No active pairings remain");
        }

        private void TestTendencyCoaching()
        {
            FreshDesk(out _);
            var tcm = TendencyCoachingManager.Instance;
            tcm.Initialize(id => _db.GetPlayer(id), id => null);

            var kid = _db.GetPlayer("aaa6");
            int start = tcm.GetTendencyValue(kid.Tendencies, "ScreenSetting");

            var (ok, message) = tcm.AssignTrainingFocus("aaa6", "ScreenSetting");
            Assert(ok, $"Assigning a training focus succeeds ({message})");
            tcm.AssignTrainingFocus("aaa6", "BoxOutDiscipline");
            tcm.AssignTrainingFocus("aaa6", "TransitionDefense");
            var (fourth, _) = tcm.AssignTrainingFocus("aaa6", "ShotDiscipline");
            Assert(!fourth, "A fourth simultaneous focus is refused");

            UnityEngine.Random.InitState(77);
            for (int week = 0; week < 40; week++)
                tcm.ProcessWeeklyTraining(new List<string> { "aaa6" }, null,
                    TendencyCoachingManager.INTENSITY_INTENSE);

            int after = tcm.GetTendencyValue(kid.Tendencies, "ScreenSetting");
            Assert(after > start, $"Sustained coaching improves the habit ({start} -> {after})");
            Assert(after <= 100, "Habit value stays in range");

            var (removed, _) = tcm.RemoveTrainingFocus("aaa6", "ScreenSetting");
            Assert(removed, "Removing a focus succeeds");
        }

        private void TestSaveRoundTrip()
        {
            var desk = FreshDesk(out _);
            var mm = new MentorshipManager();
            var tcm = TendencyCoachingManager.Instance;
            tcm.Initialize(id => _db.GetPlayer(id), id => null);

            desk.WeeklyFocus = PracticeFocus.Conditioning;
            desk.WeeklyIntensity = PracticeIntensity.Light;

            var profile = mm.GetOrCreateMentorProfile(_db.GetPlayer("aaa0"));
            profile.Willingness = 85; profile.TeachingAbility = 80;
            mm.AssignMentor(_db.GetPlayer("aaa0"), _db.GetPlayer("aaa5"), "AAA", MentorFocusArea.Shooting);
            mm.GetTeamRelationships("AAA")[0].RelationshipStrength = 62;

            tcm.AssignTrainingFocus("aaa7", "CloseoutControl");
            _db.GetPlayer("aaa7").Tendencies.CloseoutControl = 77;

            var data = new SaveData();
            desk.WriteSave(data);

            Assert(data.DevelopmentData != null, "Save captures the development desk");
            AssertEqual(1, data.DevelopmentData.Pairings.Count, "Pairing saved");
            Assert(data.DevelopmentData.TendencyStates.Any(t => t.PlayerId == "aaa7"),
                "Coached player's tendency block saved");

            // Fresh world: same players, blank managers
            var desk2 = new DevelopmentSystem(_db, null, new InjuryManager(),
                id => _teams.TryGetValue(id ?? "", out var t) ? t : null);
            var mm2 = new MentorshipManager();
            var kid = _db.GetPlayer("aaa7");
            kid.Tendencies.CloseoutControl = 40;
            kid.Tendencies.CurrentTrainingFocus.Clear();

            var mentorProfile = mm2.GetOrCreateMentorProfile(_db.GetPlayer("aaa0"));
            mentorProfile.Willingness = 85; mentorProfile.TeachingAbility = 80;

            desk2.ReadSave(data, new SaveReadContext("1.1.0", false, 2026));

            AssertEqual(PracticeFocus.Conditioning, desk2.WeeklyFocus, "Practice focus restored");
            AssertEqual(PracticeIntensity.Light, desk2.WeeklyIntensity, "Practice intensity restored");

            var restored = mm2.GetTeamRelationships("AAA").FirstOrDefault(r => r.IsActive);
            Assert(restored != null, "Pairing rebuilt on load");
            AssertEqual(62, restored?.RelationshipStrength ?? -1, "Bond strength survives the save");
            AssertEqual(MentorFocusArea.Shooting, restored?.PrimaryFocus ?? MentorFocusArea.General,
                "Pairing focus survives the save");

            AssertEqual(77, kid.Tendencies.CloseoutControl, "Trained habit value restored");
            Assert(kid.Tendencies.CurrentTrainingFocus.Contains("CloseoutControl"),
                "Training focus restored");
        }
    }
}
