using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Assignment-based scouting fog of war. Scouts are assigned to draft
    /// prospects (or NBA players); two weeks later a report lands whose accuracy
    /// comes from the scout's ability and how often the target has been scouted.
    /// The upcoming draft class is previewed with the SAME deterministic seed the
    /// June draft uses, so reports filed in January describe the real prospects.
    /// Unscouted picks stay gambles.
    /// </summary>
    public class ScoutingSystem : IGameSystem, ISaveSection
    {
        public string SystemId => "Scouting";

        private readonly PlayerDatabase _db;
        private readonly PersonnelManager _personnel;
        private readonly ScoutingReportGenerator _generator;

        private readonly Dictionary<string, int> _timesScouted = new Dictionary<string, int>();
        private readonly Dictionary<string, ScoutingReport> _reports = new Dictionary<string, ScoutingReport>();
        private readonly Dictionary<string, string> _workouts = new Dictionary<string, string>();
        private readonly HashSet<string> _flagsRevealed = new HashSet<string>();

        private List<DraftProspect> _preview = new List<DraftProspect>();
        private int _previewSeason = -1;

        public ScoutingSystem(PlayerDatabase db, PersonnelManager personnel, ScoutingReportGenerator generator)
        {
            _db = db;
            _personnel = personnel;
            _generator = generator;

            if (_personnel != null)
                _personnel.OnScoutAssignmentCompleted += OnAssignmentCompleted;
        }

        public static ScoutingSystem CreateDefault(GameManager gm)
        {
            return new ScoutingSystem(gm.PlayerDatabase, gm.PersonnelManager, gm.ScoutingReportGenerator);
        }

        // ==================== PROSPECT PREVIEW ====================

        /// <summary>
        /// The upcoming draft class, generated with the same seed the real June
        /// draft will use — identical prospects, identical ids.
        /// </summary>
        public IReadOnlyList<DraftProspect> GetProspectPreview(int seasonLabel)
        {
            EnsurePreview(seasonLabel);
            return _preview;
        }

        private void EnsurePreview(int seasonLabel)
        {
            if (seasonLabel <= 0 || _previewSeason == seasonLabel) return;

            var draft = new DraftSystem(new SalaryCapManager(), null, seed: seasonLabel * 17 + 3);
            _preview = draft.GenerateDraftClass(seasonLabel + 1)
                .OrderBy(p => p.MockDraftPosition)
                .ToList();
            _previewSeason = seasonLabel;
        }

        // ==================== ASSIGNMENTS ====================

        /// <summary>Send a scout after a draft prospect (14-day assignment).</summary>
        public (bool success, string message) AssignScoutToProspect(string scoutId, string prospectId, int seasonLabel)
        {
            EnsurePreview(seasonLabel);
            if (_preview.All(p => p.ProspectId != prospectId))
                return (false, "Unknown prospect.");
            return _personnel?.AssignScout(scoutId, prospectId)
                ?? (false, "Scouting department unavailable.");
        }

        private void OnAssignmentCompleted(StaffTaskAssignment assignment)
        {
            if (assignment == null || assignment.ScoutTask == ScoutTaskType.Unassigned) return;

            var scout = _personnel?.GetProfile(assignment.StaffId);
            if (scout == null) return;

            foreach (var targetId in assignment.ScoutTargetPlayerIds ?? new List<string>())
            {
                var subject = ResolveSubject(targetId);
                if (subject == null) continue;

                int times = GetTimesScouted(targetId);
                ScoutingReport report;
                try
                {
                    report = _generator?.GenerateReport(subject, scout, times, gamesObserved: 10);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Scouting] Report generation failed for {targetId}: {ex.Message}");
                    continue;
                }
                if (report == null) continue;

                _timesScouted[targetId] = times + 1;
                _reports[targetId] = report;

                InboxService.Instance?.Publish(InboxMessageType.Scouting, scout.PersonName,
                    $"Scouting report filed: {subject.FullName}",
                    $"{report.OverallSummary}\n\nProjected role: {report.ProjectedRole}",
                    deepLinkPanelId: "Staff");
            }
        }

        /// <summary>Prospect targets become temporary Players for the generator.</summary>
        private Player ResolveSubject(string targetId)
        {
            var prospect = _preview.FirstOrDefault(p => p.ProspectId == targetId);
            if (prospect != null)
                return prospect.ToPlayer("", 0, prospect.MockDraftPosition, _previewSeason + 1);

            return _db?.GetPlayer(targetId);
        }

        // ==================== QUERIES ====================

        public int GetTimesScouted(string targetId) =>
            _timesScouted.TryGetValue(targetId ?? "", out int n) ? n : 0;

        public bool IsScouted(string targetId) => GetTimesScouted(targetId) > 0;

        public ScoutingReport GetReport(string targetId) =>
            _reports.TryGetValue(targetId ?? "", out var r) ? r : null;

        /// <summary>One-line fog-of-war summary for draft boards.</summary>
        public string DescribeProspect(string prospectId)
        {
            var report = GetReport(prospectId);
            if (report == null) return "unscouted — drafting blind";
            return $"scouted: {report.ProjectedRole}";
        }

        // ==================== PROJECTION RANGES / WORKOUTS ====================

        /// <summary>How sure the department is, from how often the target was seen.</summary>
        private static float DepthAccuracy(int timesScouted) =>
            Mathf.Clamp01(0.35f + timesScouted * 0.13f);

        /// <summary>Grade points of uncertainty either side of the projection.</summary>
        internal static int ProjectionSpread(int timesScouted) =>
            Mathf.RoundToInt((1f - DepthAccuracy(timesScouted)) * 28f);

        /// <summary>
        /// The projection the department will commit to — a RANGE in descriptor
        /// vocabulary that narrows every time the prospect is scouted again.
        /// </summary>
        public string ProjectionRange(DraftProspect prospect)
        {
            if (prospect == null) return "";
            int times = GetTimesScouted(prospect.ProspectId);
            if (times <= 0) return "unscouted — drafting blind";

            int spread = ProjectionSpread(times);
            int mid = (prospect.ProjectedOverall + prospect.Potential) / 2;
            string low = SkillGradeDescriptors.GetGrade(Mathf.Clamp(mid - spread, 0, 100));
            string high = SkillGradeDescriptors.GetGrade(Mathf.Clamp(mid + spread, 0, 100));
            return low == high ? $"projects {low}" : $"projects {low} → {high}";
        }

        /// <summary>
        /// Red flags stay buried until the book is deep or a workout shakes one out.
        /// </summary>
        public bool IsRedFlagRevealed(string prospectId) =>
            _flagsRevealed.Contains(prospectId ?? "") || GetTimesScouted(prospectId) >= 4;

        public string GetWorkoutSummary(string prospectId) =>
            _workouts.TryGetValue(prospectId ?? "", out var s) ? s : null;

        /// <summary>
        /// A private workout: worth two scouting visits, and a good (not certain)
        /// chance of surfacing whatever he's hiding. The reveal roll is seeded from
        /// the ProspectId so it's the same answer on a reload.
        /// </summary>
        public string FileWorkoutReport(DraftProspect prospect)
        {
            if (prospect == null || string.IsNullOrEmpty(prospect.ProspectId)) return null;
            string id = prospect.ProspectId;

            _timesScouted[id] = GetTimesScouted(id) + 2;

            var rng = new System.Random(DraftProspect.SeedFor(id, 0x5EED));
            bool revealed = rng.NextDouble() < 0.75 &&
                            prospect.Intel.RedFlag != ProspectRedFlag.None;
            if (revealed) _flagsRevealed.Add(id);

            string summary =
                $"shooting drills: {SkillGradeDescriptors.GetGrade(prospect.Shooting)}; " +
                $"movement: {SkillGradeDescriptors.GetGrade(prospect.Athleticism)}. " +
                (revealed
                    ? $"Concern surfaced — {prospect.Intel.RedFlagText}."
                    : "Competed hard; medical and interview checked out clean.");
            _workouts[id] = summary;
            return summary;
        }

        // ==================== SAVE ====================

        public void WriteSave(SaveData data)
        {
            var save = new ScoutingSaveData { PreviewSeason = _previewSeason };
            foreach (var kvp in _timesScouted)
                save.Counts.Add(new ScoutCountRecord { TargetId = kvp.Key, Count = kvp.Value });
            foreach (var kvp in _workouts)
                save.Workouts.Add(new WorkoutReportRecord
                {
                    ProspectId = kvp.Key,
                    Summary = kvp.Value,
                    FlagRevealed = _flagsRevealed.Contains(kvp.Key)
                });
            foreach (var kvp in _reports)
            {
                save.Reports.Add(new StoredScoutingReport
                {
                    TargetId = kvp.Key,
                    Report = kvp.Value,
                    GeneratedDateStr = kvp.Value.GeneratedDate.ToString("o")
                });
            }
            data.ScoutingData = save;
        }

        public void ReadSave(SaveData data, in SaveReadContext ctx)
        {
            _timesScouted.Clear();
            _reports.Clear();
            _workouts.Clear();
            _flagsRevealed.Clear();

            var save = data.ScoutingData;
            if (save != null)
            {
                foreach (var record in save.Counts ?? new List<ScoutCountRecord>())
                    _timesScouted[record.TargetId] = record.Count;

                // Pre-O3 saves have no workout list — nothing filed, nothing revealed
                foreach (var w in save.Workouts ?? new List<WorkoutReportRecord>())
                {
                    if (w == null || string.IsNullOrEmpty(w.ProspectId)) continue;
                    _workouts[w.ProspectId] = w.Summary;
                    if (w.FlagRevealed) _flagsRevealed.Add(w.ProspectId);
                }

                foreach (var stored in save.Reports ?? new List<StoredScoutingReport>())
                {
                    if (stored?.Report == null || string.IsNullOrEmpty(stored.TargetId)) continue;
                    if (DateTime.TryParse(stored.GeneratedDateStr, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var when))
                        stored.Report.GeneratedDate = when;
                    _reports[stored.TargetId] = stored.Report;
                }

                if (save.PreviewSeason > 0)
                {
                    _previewSeason = -1;           // force regeneration
                    EnsurePreview(save.PreviewSeason);
                }
            }
        }
    }

    // ==================== SAVE SHAPES ====================

    [Serializable]
    public class ScoutingSaveData
    {
        public int PreviewSeason = -1;
        public List<ScoutCountRecord> Counts = new List<ScoutCountRecord>();
        public List<StoredScoutingReport> Reports = new List<StoredScoutingReport>();
        /// <summary>O3 pre-draft workouts. Absent in older saves = none filed.</summary>
        public List<WorkoutReportRecord> Workouts = new List<WorkoutReportRecord>();
    }

    [Serializable]
    public class WorkoutReportRecord
    {
        public string ProspectId;
        public string Summary;
        public bool FlagRevealed;
    }

    [Serializable]
    public class ScoutCountRecord
    {
        public string TargetId;
        public int Count;
    }

    [Serializable]
    public class StoredScoutingReport
    {
        public string TargetId;
        public ScoutingReport Report;
        public string GeneratedDateStr;
    }
}
