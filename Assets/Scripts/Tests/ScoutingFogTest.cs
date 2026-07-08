using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the scouting fog of war: the draft-class preview is deterministic
    /// (identical to the real June class), scout assignments mature into reports
    /// with a rising times-scouted counter, unscouted prospects read as gambles,
    /// and the whole thing survives a save.
    /// </summary>
    public class ScoutingFogTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestPreviewDeterminism();
            TestAssignmentProducesReport();
            TestSaveRoundTrip();

            return (_passed, _failed);
        }

        private UnifiedCareerProfile MakeScout(PersonnelManager pm, string id, string teamId)
        {
            var scout = new UnifiedCareerProfile
            {
                ProfileId = id,
                PersonName = $"Scout {id}",
                CurrentAge = 45,
                ScoutingAbility = 75,
                TalentEvaluation = 75,
                PotentialAssessment = 70,
                CharacterJudgment = 70
            };
            scout.HandleHired(2026, teamId, teamId, UnifiedRole.Scout);
            pm.RegisterProfile(scout);
            return scout;
        }

        private void TestPreviewDeterminism()
        {
            var pmA = new PersonnelManager();
            var sysA = new ScoutingSystem(new PlayerDatabase(), pmA, new ScoutingReportGenerator(42));
            var pmB = new PersonnelManager();
            var sysB = new ScoutingSystem(new PlayerDatabase(), pmB, new ScoutingReportGenerator(42));

            var previewA = sysA.GetProspectPreview(2026);
            var previewB = sysB.GetProspectPreview(2026);

            Assert(previewA.Count >= 60, $"Preview holds a full class ({previewA.Count})");
            AssertEqual(previewA.Count, previewB.Count, "Preview size is deterministic");
            Assert(previewA.Zip(previewB, (a, b) => a.ProspectId == b.ProspectId && a.FullName == b.FullName)
                    .All(match => match),
                "Same season produces the identical prospect class (the June draft's seed)");

            // The real June draft uses seed = season * 17 + 3 on year season+1
            var juneDraft = new DraftSystem(new SalaryCapManager(), null, seed: 2026 * 17 + 3);
            var juneClass = juneDraft.GenerateDraftClass(2027).OrderBy(p => p.MockDraftPosition).ToList();
            Assert(previewA.Zip(juneClass, (a, b) => a.ProspectId == b.ProspectId).All(m => m),
                "Preview matches the class the real draft will generate");
        }

        private void TestAssignmentProducesReport()
        {
            var pm = new PersonnelManager();
            var db = new PlayerDatabase();
            var sys = new ScoutingSystem(db, pm, new ScoutingReportGenerator(7));
            var scout = MakeScout(pm, "SC1", "AAA");

            var target = sys.GetProspectPreview(2026)[0];
            Assert(!sys.IsScouted(target.ProspectId), "Fresh prospect starts unscouted");
            Assert(sys.DescribeProspect(target.ProspectId).Contains("unscouted"),
                "Unscouted prospect reads as a gamble");

            var (ok, message) = sys.AssignScoutToProspect("SC1", target.ProspectId, 2026);
            Assert(ok, $"Assigning a scout to a prospect succeeds ({message})");

            // Scout can't take a second job while working
            var target2 = sys.GetProspectPreview(2026)[1];
            var (busy, _) = sys.AssignScoutToProspect("SC1", target2.ProspectId, 2026);
            Assert(!busy, "A working scout refuses a second assignment");

            // Two weeks of daily work complete the assignment
            var date = new DateTime(2026, 12, 1);
            for (int day = 0; day < 15; day++)
                pm.ProcessDailyWork("AAA", date.AddDays(day));

            Assert(sys.IsScouted(target.ProspectId), "Completed assignment scouts the prospect");
            AssertEqual(1, sys.GetTimesScouted(target.ProspectId), "Times-scouted counter increments");
            var report = sys.GetReport(target.ProspectId);
            Assert(report != null, "A scouting report was filed");
            Assert(!string.IsNullOrEmpty(report?.OverallSummary), "Report carries a summary");
            Assert(sys.DescribeProspect(target.ProspectId).StartsWith("scouted"),
                "Scouted prospect shows intel on the board");

            // Re-scouting deepens the book
            sys.AssignScoutToProspect("SC1", target.ProspectId, 2026);
            for (int day = 0; day < 15; day++)
                pm.ProcessDailyWork("AAA", date.AddDays(20 + day));
            AssertEqual(2, sys.GetTimesScouted(target.ProspectId), "Second pass raises the counter");
        }

        private void TestSaveRoundTrip()
        {
            var pm = new PersonnelManager();
            var db = new PlayerDatabase();
            var sys = new ScoutingSystem(db, pm, new ScoutingReportGenerator(7));
            MakeScout(pm, "SC1", "AAA");

            var target = sys.GetProspectPreview(2026)[2];
            sys.AssignScoutToProspect("SC1", target.ProspectId, 2026);
            for (int day = 0; day < 15; day++)
                pm.ProcessDailyWork("AAA", new DateTime(2027, 1, 5).AddDays(day));

            var data = new SaveData();
            sys.WriteSave(data);
            Assert(data.ScoutingData != null && data.ScoutingData.Reports.Count == 1,
                "Save captures the scouting book");

            var sys2 = new ScoutingSystem(new PlayerDatabase(), new PersonnelManager(),
                new ScoutingReportGenerator(7));
            sys2.ReadSave(data, new SaveReadContext("1.1.0", false, 2026));

            AssertEqual(1, sys2.GetTimesScouted(target.ProspectId), "Counter survives the save");
            Assert(sys2.GetReport(target.ProspectId) != null, "Report survives the save");
            Assert(sys2.GetProspectPreview(2026).Count >= 60, "Preview regenerates after load");
        }
    }
}
