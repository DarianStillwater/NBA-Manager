using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using DraftProspect = NBAHeadCoach.Core.Manager.DraftProspect;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// O3 gate: the pre-draft file. Derived prospect intel (college stat line,
    /// consensus mock rank, hidden red flag) is a pure function of the ProspectId,
    /// so it survives a save without save fields; the mocks are wrong about some of
    /// the class on purpose; scouting narrows the projection RANGE instead of
    /// printing a number; a workout is worth two trips and usually shakes the flag
    /// loose; and the six workout invites round-trip through the save.
    /// </summary>
    public class DraftScoutingTest : BaseTest
    {
        private const int SEASON = 2026;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            // The rigs below construct OffseasonManager instances whose ctors grab the
            // static Instance and get armed (_engineActive). Later suite tests consult
            // OffseasonManager.Instance.EngineActive (e.g. the in-season FA wire), so
            // restore the prior singleton no matter what.
            var priorOffseason = OffseasonManager.Instance;
            try
            {
                TestIntelDeterminism();
                TestCareerSalt();
                TestOverhypedAndSleepers();
                TestProjectionRangeNarrows();
                TestRedFlagHiddenUntilWorkout();
                TestNoNumericAttributesInReports();
                TestWorkoutInviteFlow();
                TestSaveRoundTrip();
            }
            finally
            {
                typeof(OffseasonManager).GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static)
                    .SetValue(null, priorOffseason);
            }

            return (_passed, _failed);
        }

        // ==================== HELPERS ====================

        private static List<DraftProspect> GenerateClass() =>
            new DraftSystem(new SalaryCapManager(), null, seed: SEASON * 17 + 3)
                .GenerateDraftClass(SEASON + 1);

        private static ScoutingSystem NewScouting() =>
            new ScoutingSystem(new PlayerDatabase(), new PersonnelManager(),
                new ScoutingReportGenerator(11));

        private static void SetPrivate(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        // ==================== DETERMINISM ====================

        private void TestIntelDeterminism()
        {
            var classA = GenerateClass();
            var classB = GenerateClass();
            var byId = classB.ToDictionary(p => p.ProspectId);

            bool same = true;
            foreach (var a in classA)
            {
                var b = byId[a.ProspectId];
                var x = a.Intel; var y = b.Intel;
                same &= Mathf.Approximately(x.PPG, y.PPG) &&
                        Mathf.Approximately(x.RPG, y.RPG) &&
                        Mathf.Approximately(x.APG, y.APG) &&
                        Mathf.Approximately(x.FieldGoalPct, y.FieldGoalPct) &&
                        Mathf.Approximately(x.ThreePointPct, y.ThreePointPct) &&
                        x.ConsensusRank == y.ConsensusRank &&
                        x.MockHigh == y.MockHigh && x.MockLow == y.MockLow &&
                        x.RedFlag == y.RedFlag;
                if (!same) break;
            }
            Assert(same, "Same ProspectId derives identical stats, rank and red flag (no save fields needed)");

            Assert(classA.All(p => p.Intel.PPG > 0f && p.Intel.RPG > 0f),
                "Every prospect carries a college stat line");
            Assert(classA.All(p => p.Intel.MockHigh <= p.Intel.ConsensusRank &&
                                   p.Intel.ConsensusRank <= p.Intel.MockLow),
                "Consensus rank sits inside its own mock spread");

            // Regression: seeds derived from a StableHash beyond ~161803398 degrade
            // .NET's seeded Random (SeedArray goes negative), which used to produce
            // MockHigh > MockLow ("mock 54-50") for classes from 2030 onward.
            var class2030 = new DraftSystem(new SalaryCapManager(), null, seed: 2029 * 17 + 3)
                .GenerateDraftClass(2030);
            Assert(class2030.All(p => p.Intel.MockHigh >= 1 &&
                                      p.Intel.MockHigh <= p.Intel.ConsensusRank &&
                                      p.Intel.ConsensusRank <= p.Intel.MockLow &&
                                      p.Intel.MockLow <= 120),
                "2030 class: MockHigh >= 1, MockHigh <= ConsensusRank <= MockLow <= 120 for every prospect");
        }

        // ==================== CAREER SALT ====================

        private void TestCareerSalt()
        {
            try
            {
                DraftProspect.CareerSalt = 111;
                var classA = GenerateClass(); // fresh instances, so Intel is derived fresh
                foreach (var p in classA) { var _ = p.Intel; }   // derive NOW, while salt=111 (Intel is lazy+cached)

                DraftProspect.CareerSalt = 222;
                var classB = GenerateClass();
                foreach (var p in classB) { var _ = p.Intel; }

                var byId = classB.ToDictionary(p => p.ProspectId);
                bool anyDiffers = classA.Any(a =>
                {
                    var b = byId[a.ProspectId];
                    return a.Intel.ConsensusRank != b.Intel.ConsensusRank ||
                           a.Intel.RedFlag != b.Intel.RedFlag;
                });
                Assert(anyDiffers,
                    "Two different CareerSalt values produce different hidden intel for at least one prospect");
            }
            finally
            {
                DraftProspect.CareerSalt = 0;
            }
        }

        // ==================== HYPE ====================

        private void TestOverhypedAndSleepers()
        {
            var draftClass = GenerateClass();

            var hyped = draftClass.Where(p => p.Intel.Overhyped).ToList();
            var sleepers = draftClass
                .Where(p => p.Intel.ConsensusRank - p.MockDraftPosition >= 6).ToList();

            Assert(hyped.Count > 0, $"The class contains overhyped prospects ({hyped.Count})");
            Assert(sleepers.Count > 0, $"The class contains sleepers the mocks miss ({sleepers.Count})");
            Assert(hyped.All(p => p.Intel.ConsensusRank <= p.MockDraftPosition),
                "An overhyped prospect never ranks worse than his true talent order");

            // Inflated production is how "reports can be wrong" reads on the board:
            // scoring measured against the hype-free baseline for the same hidden skill
            float HypeRatio(IEnumerable<DraftProspect> set) =>
                set.Average(p => p.Intel.PPG / Mathf.Max(1f, p.ProjectedOverall * 0.30f - 4f));
            float hypedRatio = HypeRatio(hyped);
            float restRatio = HypeRatio(draftClass.Where(p => !p.Intel.Overhyped));
            Assert(hypedRatio > restRatio,
                $"Overhyped prospects post inflated stats ({hypedRatio:F2}x vs {restRatio:F2}x baseline)");

            int flagged = draftClass.Count(p => p.Intel.RedFlag != ProspectRedFlag.None);
            float share = flagged / (float)draftClass.Count;
            Assert(share > 0.08f && share < 0.30f,
                $"Roughly a sixth of the class carries a red flag ({flagged}/{draftClass.Count})");

            // The stability gate counts on the class size never moving
            AssertEqual(120, draftClass.Count, "Draft class size is unchanged (60 picks still fillable)");
        }

        // ==================== PROJECTION RANGES ====================

        private void TestProjectionRangeNarrows()
        {
            var sys = NewScouting();
            var prospect = sys.GetProspectPreview(SEASON)[0];

            AssertEqual("unscouted — drafting blind", sys.ProjectionRange(prospect),
                "An unscouted prospect gets no projection at all");

            int s1 = ScoutingSystem.ProjectionSpread(1);
            int s2 = ScoutingSystem.ProjectionSpread(2);
            int s4 = ScoutingSystem.ProjectionSpread(4);
            Assert(s1 > s2 && s2 > s4, $"Projection uncertainty narrows with scouting ({s1} → {s2} → {s4})");

            // A workout is worth two trips, so it tightens the range on its own
            sys.FileWorkoutReport(prospect);
            string afterOne = sys.ProjectionRange(prospect);
            Assert(afterOne.StartsWith("projects"), $"A scouted prospect gets a projection ({afterOne})");

            sys.FileWorkoutReport(prospect);
            string afterTwo = sys.ProjectionRange(prospect);
            Assert(afterTwo.StartsWith("projects"), $"A deeper book still reads as a range ({afterTwo})");
            AssertEqual(4, sys.GetTimesScouted(prospect.ProspectId),
                "Each workout is worth two scouting trips");
        }

        // ==================== RED FLAGS ====================

        private void TestRedFlagHiddenUntilWorkout()
        {
            var sys = NewScouting();
            var preview = sys.GetProspectPreview(SEASON);

            var flagged = preview.Where(p => p.Intel.RedFlag != ProspectRedFlag.None).Take(8).ToList();
            Assert(flagged.Count > 0, "The preview class carries hidden red flags");
            Assert(flagged.All(p => !sys.IsRedFlagRevealed(p.ProspectId)),
                "Red flags are invisible before anyone works him out");

            int revealed = 0;
            foreach (var p in flagged)
            {
                sys.FileWorkoutReport(p);
                if (sys.IsRedFlagRevealed(p.ProspectId)) revealed++;
            }
            Assert(revealed > 0, $"Workouts shake red flags loose ({revealed}/{flagged.Count})");

            // The reveal roll is seeded from the ProspectId, so a reload agrees
            var sys2 = NewScouting();
            var target = flagged[0];
            sys2.FileWorkoutReport(sys2.GetProspectPreview(SEASON)
                .First(p => p.ProspectId == target.ProspectId));
            AssertEqual(sys.IsRedFlagRevealed(target.ProspectId),
                sys2.IsRedFlagRevealed(target.ProspectId),
                "The workout reveal roll is deterministic per prospect");

            // A clean prospect has nothing to surface, workout or not
            var clean = preview.First(p => p.Intel.RedFlag == ProspectRedFlag.None);
            var sys3 = NewScouting();
            Assert(!sys3.IsRedFlagRevealed(clean.ProspectId), "Nothing surfaces on a clean prospect");
        }

        // ==================== NO NUMBERS ON ATTRIBUTES ====================

        private void TestNoNumericAttributesInReports()
        {
            var sys = NewScouting();
            var prospect = sys.GetProspectPreview(SEASON)[3];
            string workout = sys.FileWorkoutReport(prospect);
            string projection = sys.ProjectionRange(prospect);

            Assert(!workout.Any(char.IsDigit),
                $"Workout report never prints an attribute number ({workout})");
            Assert(!projection.Any(char.IsDigit),
                $"Projection range never prints an attribute number ({projection})");
            Assert(!sys.DescribeProspect(prospect.ProspectId).Any(char.IsDigit),
                "Board summary never prints an attribute number");
        }

        // ==================== WORKOUT INVITES ====================

        private void TestWorkoutInviteFlow()
        {
            var go = new GameObject("__DraftScoutingRig__");
            go.SetActive(false);                  // defers Awake: no scene load, no UI
            var gm = go.AddComponent<GameManager>();
            try
            {
                try
                {
                    typeof(GameManager)
                        .GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(gm, null);
                }
                catch (Exception)
                {
                    // Initialize's last line starts a coroutine an inactive object
                    // refuses; every manager is already constructed by then.
                }

                if (gm.Scouting == null || gm.Offseason == null)
                {
                    Assert(false, "Headless rig builds the scouting/offseason graph");
                    return;
                }

                var off = gm.Offseason;
                SetPrivate(off, "_engineActive", true);
                SetPrivate(off, "_seasonLabel", SEASON);
                SetPrivate(off, "_calendarYear", SEASON + 1);
                SetPrivate(off, "_postSeasonDone", true);   // skip the season-end stage
                SetPrivate(gm, "_currentDate", OffseasonDates.Workouts(SEASON + 1));

                Assert(off.WorkoutsOpen(gm.CurrentDate), "The invite window is open on Jun 10");
                AssertEqual(OffseasonManager.MaxWorkoutInvites, off.WorkoutInvitesRemaining,
                    "Six invites to spend");

                var pool = gm.Scouting.GetProspectPreview(SEASON)
                    .OrderBy(p => p.Intel.ConsensusRank).ToList();

                for (int i = 0; i < 3; i++)
                    Assert(off.InviteToWorkout(gm, pool[i].ProspectId, out _),
                        $"Invite {i + 1} accepted");
                AssertEqual(3, off.WorkoutInvitesRemaining, "Spent invites come off the count");
                Assert(!string.IsNullOrEmpty(gm.Scouting.GetWorkoutSummary(pool[0].ProspectId)),
                    "Each invite files a workout report");

                Assert(!off.InviteToWorkout(gm, pool[0].ProspectId, out _),
                    "The same prospect can't work out twice");

                for (int i = 3; i < 6; i++)
                    off.InviteToWorkout(gm, pool[i].ProspectId, out _);
                Assert(!off.InviteToWorkout(gm, pool[9].ProspectId, out string why),
                    $"A seventh invite is refused ({why})");

                // Deadline: draft day auto-fills whatever is left and closes the window
                var off2 = new OffseasonManager();
                SetPrivate(off2, "_engineActive", true);
                SetPrivate(off2, "_seasonLabel", SEASON);
                SetPrivate(off2, "_calendarYear", SEASON + 1);
                SetPrivate(off2, "_postSeasonDone", true);
                SetPrivate(gm, "_currentDate", OffseasonDates.Workouts(SEASON + 1));
                off2.InviteToWorkout(gm, pool[20].ProspectId, out _);

                var draftDay = OffseasonDates.Draft(SEASON + 1);
                SetPrivate(gm, "_currentDate", draftDay);
                off2.DailyTick(new DailyTickContext(draftDay, gm.PlayerTeamId, gm));

                AssertEqual(OffseasonManager.MaxWorkoutInvites, off2.WorkoutInvites.Count,
                    "Draft day auto-fills the unused invites");
                Assert(off2.WorkoutInvites.All(id =>
                        !string.IsNullOrEmpty(gm.Scouting.GetWorkoutSummary(id))),
                    "Every auto-filled invite has a report on file");
                Assert(!off2.WorkoutsOpen(draftDay), "The window closes on draft day");
                Assert(!off2.InviteToWorkout(gm, pool[30].ProspectId, out _),
                    "No invites accepted after the deadline");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ==================== SAVE ====================

        private void TestSaveRoundTrip()
        {
            // Scouting side: workout reports and revealed flags
            var sys = NewScouting();
            var preview = sys.GetProspectPreview(SEASON);
            var flagged = preview.First(p => p.Intel.RedFlag != ProspectRedFlag.None);
            sys.FileWorkoutReport(flagged);
            bool revealedBefore = sys.IsRedFlagRevealed(flagged.ProspectId);

            var data = new SaveData();
            sys.WriteSave(data);
            Assert(data.ScoutingData?.Workouts?.Count == 1, "Save captures the workout report");

            var sys2 = NewScouting();
            sys2.ReadSave(data, new SaveReadContext("1.1.0", false, SEASON));
            Assert(!string.IsNullOrEmpty(sys2.GetWorkoutSummary(flagged.ProspectId)),
                "Workout report survives the save");
            AssertEqual(revealedBefore, sys2.IsRedFlagRevealed(flagged.ProspectId),
                "Red-flag reveal survives the save");
            AssertEqual(2, sys2.GetTimesScouted(flagged.ProspectId),
                "Workout scout depth survives the save");

            // Offseason side: which invites were spent, and that the window closed
            var off = new OffseasonManager();
            SetPrivate(off, "_engineActive", true);
            SetPrivate(off, "_seasonLabel", SEASON);
            SetPrivate(off, "_calendarYear", SEASON + 1);
            SetPrivate(off, "_workoutsOpened", true);
            var invites = (List<string>)typeof(OffseasonManager)
                .GetField("_workoutInvites", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(off);
            invites.Add(flagged.ProspectId);

            var save = new SaveData();
            off.WriteSave(save);
            AssertEqual(1, save.Offseason.WorkoutInvites.Count, "Save captures the spent invites");

            var off2 = new OffseasonManager();
            off2.ReadSave(save, new SaveReadContext("1.1.0", false, SEASON));
            AssertEqual(1, off2.WorkoutInvites.Count, "Spent invites survive the save");
            AssertEqual(OffseasonManager.MaxWorkoutInvites - 1, off2.WorkoutInvitesRemaining,
                "Remaining invite count survives the save");

            // A pre-O3 save has no workout fields at all and must still load
            var legacy = new SaveData();
            off.WriteSave(legacy);
            legacy.Offseason.WorkoutInvites = null;
            var off3 = new OffseasonManager();
            off3.ReadSave(legacy, new SaveReadContext("1.0.0", false, SEASON));
            AssertEqual(0, off3.WorkoutInvites.Count, "Pre-O3 saves load with no invites spent");
        }
    }
}
