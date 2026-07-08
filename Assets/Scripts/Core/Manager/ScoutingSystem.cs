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

        // ==================== SAVE ====================

        public void WriteSave(SaveData data)
        {
            var save = new ScoutingSaveData { PreviewSeason = _previewSeason };
            foreach (var kvp in _timesScouted)
                save.Counts.Add(new ScoutCountRecord { TargetId = kvp.Key, Count = kvp.Value });
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

            var save = data.ScoutingData;
            if (save != null)
            {
                foreach (var record in save.Counts ?? new List<ScoutCountRecord>())
                    _timesScouted[record.TargetId] = record.Count;

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
