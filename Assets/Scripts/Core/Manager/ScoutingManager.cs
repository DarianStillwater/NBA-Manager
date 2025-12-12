using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages scouting operations including scouts, assignments, and text-based reports.
    /// Each team can have up to 5 scouts with varying specializations.
    /// </summary>
    public class ScoutingManager
    {
        public const int MAX_SCOUTS_PER_TEAM = 5;
        public const int MIN_SCOUTS_PER_TEAM = 1;

        private Dictionary<string, List<Scout>> _teamScouts = new();
        private Dictionary<string, List<ScoutingReport>> _teamReports = new();
        private Dictionary<string, ScoutAssignment> _activeAssignments = new();  // ScoutId -> Assignment
        private Dictionary<string, ScoutingHistory> _scoutingHistory = new();    // PlayerId -> History

        private ScoutingReportGenerator _reportGenerator;
        private PlayerDatabase _playerDatabase;

        // ==================== INITIALIZATION ====================

        public ScoutingManager()
        {
            _reportGenerator = new ScoutingReportGenerator();
        }

        public ScoutingManager(PlayerDatabase playerDatabase)
        {
            _reportGenerator = new ScoutingReportGenerator();
            _playerDatabase = playerDatabase;
        }

        public void SetPlayerDatabase(PlayerDatabase db)
        {
            _playerDatabase = db;
        }

        // ==================== SCOUT MANAGEMENT ====================

        /// <summary>
        /// Gets all scouts for a team.
        /// </summary>
        public List<Scout> GetScouts(string teamId)
        {
            return _teamScouts.TryGetValue(teamId, out var scouts) ? scouts.ToList() : new List<Scout>();
        }

        /// <summary>
        /// Gets a specific scout by ID.
        /// </summary>
        public Scout GetScoutById(string scoutId)
        {
            foreach (var scouts in _teamScouts.Values)
            {
                var scout = scouts.FirstOrDefault(s => s.ScoutId == scoutId);
                if (scout != null) return scout;
            }
            return null;
        }

        /// <summary>
        /// Gets available (unassigned) scouts for a team.
        /// </summary>
        public List<Scout> GetAvailableScouts(string teamId)
        {
            return GetScouts(teamId).Where(s => s.IsAvailable && !_activeAssignments.ContainsKey(s.ScoutId)).ToList();
        }

        /// <summary>
        /// Hires a new scout for a team.
        /// Returns tuple of (success, errorMessage).
        /// </summary>
        public (bool success, string message) HireScout(string teamId, Scout scout)
        {
            if (!_teamScouts.ContainsKey(teamId))
                _teamScouts[teamId] = new List<Scout>();

            if (_teamScouts[teamId].Count >= MAX_SCOUTS_PER_TEAM)
                return (false, $"Team already has maximum {MAX_SCOUTS_PER_TEAM} scouts.");

            scout.TeamId = teamId;
            scout.IsAvailable = true;
            _teamScouts[teamId].Add(scout);

            Debug.Log($"[ScoutingManager] {teamId} hired scout {scout.FullName} ({scout.PrimarySpecialization})");
            return (true, $"Successfully hired {scout.FullName}.");
        }

        /// <summary>
        /// Fires a scout from a team.
        /// </summary>
        public (bool success, string message) FireScout(string teamId, string scoutId)
        {
            if (!_teamScouts.ContainsKey(teamId))
                return (false, "Team not found.");

            if (_teamScouts[teamId].Count <= MIN_SCOUTS_PER_TEAM)
                return (false, $"Cannot fire scout - team must have at least {MIN_SCOUTS_PER_TEAM} scout.");

            var scout = _teamScouts[teamId].FirstOrDefault(s => s.ScoutId == scoutId);
            if (scout == null)
                return (false, "Scout not found on this team.");

            // Cancel any active assignment
            if (_activeAssignments.ContainsKey(scoutId))
            {
                _activeAssignments.Remove(scoutId);
            }

            _teamScouts[teamId].Remove(scout);
            Debug.Log($"[ScoutingManager] {teamId} fired scout {scout.FullName}");
            return (true, $"Released {scout.FullName}.");
        }

        /// <summary>
        /// Generates starting scouts for a team.
        /// </summary>
        public void GenerateStartingScouts(string teamId, int count = 3)
        {
            if (!_teamScouts.ContainsKey(teamId))
                _teamScouts[teamId] = new List<Scout>();

            var rng = new System.Random(teamId.GetHashCode());

            // Ensure variety in specializations
            var desiredSpecs = new[] { ScoutSpecialization.Pro, ScoutSpecialization.College, ScoutSpecialization.International };

            for (int i = 0; i < count && _teamScouts[teamId].Count < MAX_SCOUTS_PER_TEAM; i++)
            {
                var scout = Scout.CreateRandom(teamId, rng);

                // Try to fill gaps in specializations
                if (i < desiredSpecs.Length)
                {
                    var spec = desiredSpecs[i];
                    if (!_teamScouts[teamId].Any(s => s.PrimarySpecialization == spec))
                    {
                        scout.PrimarySpecialization = spec;
                    }
                }

                HireScout(teamId, scout);
            }
        }

        /// <summary>
        /// Gets total scouting budget (salaries) for a team.
        /// </summary>
        public int GetScoutingBudget(string teamId)
        {
            return GetScouts(teamId).Sum(s => s.AnnualSalary);
        }

        /// <summary>
        /// Gets available scouting capacity for the week.
        /// </summary>
        public int GetWeeklyCapacity(string teamId)
        {
            return GetAvailableScouts(teamId).Sum(s => s.WeeklyCapacity);
        }

        // ==================== SCOUTING ASSIGNMENTS ====================

        /// <summary>
        /// Assigns a scout to evaluate a player.
        /// </summary>
        public (bool success, string message) AssignScoutToPlayer(string scoutId, string playerId, int durationDays = 7)
        {
            var scout = GetScoutById(scoutId);
            if (scout == null)
                return (false, "Scout not found.");

            if (_activeAssignments.ContainsKey(scoutId))
                return (false, $"{scout.FullName} is already on an assignment.");

            Player player = _playerDatabase?.GetPlayer(playerId);
            string playerName = player?.FullName ?? playerId;

            var targetType = player?.YearsPro > 0 ? ScoutingTargetType.NBAPlayer : ScoutingTargetType.CollegeProspect;

            var assignment = new ScoutAssignment
            {
                AssignmentId = $"ASN_{Guid.NewGuid().ToString().Substring(0, 8)}",
                ScoutId = scoutId,
                TargetPlayerId = playerId,
                TargetPlayerName = playerName,
                TargetType = targetType,
                StartDate = DateTime.Now,
                DurationDays = durationDays,
                GamesObserved = 0
            };

            _activeAssignments[scoutId] = assignment;
            scout.IsAvailable = false;
            scout.CurrentAssignmentId = assignment.AssignmentId;

            Debug.Log($"[ScoutingManager] {scout.FullName} assigned to scout {playerName} for {durationDays} days");
            return (true, $"{scout.FullName} assigned to scout {playerName}.");
        }

        /// <summary>
        /// Assigns a scout to evaluate an opponent team.
        /// </summary>
        public (bool success, string message) AssignScoutToTeam(string scoutId, string targetTeamId, int durationDays = 5)
        {
            var scout = GetScoutById(scoutId);
            if (scout == null)
                return (false, "Scout not found.");

            if (_activeAssignments.ContainsKey(scoutId))
                return (false, $"{scout.FullName} is already on an assignment.");

            var assignment = new ScoutAssignment
            {
                AssignmentId = $"ASN_{Guid.NewGuid().ToString().Substring(0, 8)}",
                ScoutId = scoutId,
                TargetTeamId = targetTeamId,
                TargetType = ScoutingTargetType.OpponentTeam,
                StartDate = DateTime.Now,
                DurationDays = durationDays,
                GamesObserved = 0
            };

            _activeAssignments[scoutId] = assignment;
            scout.IsAvailable = false;
            scout.CurrentAssignmentId = assignment.AssignmentId;

            Debug.Log($"[ScoutingManager] {scout.FullName} assigned to scout team {targetTeamId}");
            return (true, $"{scout.FullName} assigned to advance scout {targetTeamId}.");
        }

        /// <summary>
        /// Cancels a scout's current assignment.
        /// </summary>
        public void CancelAssignment(string scoutId)
        {
            if (_activeAssignments.Remove(scoutId))
            {
                var scout = GetScoutById(scoutId);
                if (scout != null)
                {
                    scout.IsAvailable = true;
                    scout.CurrentAssignmentId = null;
                }
                Debug.Log($"[ScoutingManager] Cancelled assignment for scout {scoutId}");
            }
        }

        /// <summary>
        /// Gets a scout's current assignment.
        /// </summary>
        public ScoutAssignment GetAssignment(string scoutId)
        {
            return _activeAssignments.TryGetValue(scoutId, out var a) ? a : null;
        }

        /// <summary>
        /// Gets all active assignments for a team.
        /// </summary>
        public List<ScoutAssignment> GetActiveAssignments(string teamId)
        {
            var teamScoutIds = GetScouts(teamId).Select(s => s.ScoutId).ToHashSet();
            return _activeAssignments.Values.Where(a => teamScoutIds.Contains(a.ScoutId)).ToList();
        }

        // ==================== SCOUTING PROCESSING ====================

        /// <summary>
        /// Processes daily scouting progress. Call this each game day.
        /// </summary>
        public List<ScoutingReport> ProcessDailyScouting(string teamId, DateTime gameDate)
        {
            var newReports = new List<ScoutingReport>();
            var completedAssignments = new List<string>();

            foreach (var scout in GetScouts(teamId))
            {
                if (!_activeAssignments.TryGetValue(scout.ScoutId, out var assignment))
                    continue;

                // Increment games observed (simplified - could be smarter about actual games)
                assignment.GamesObserved++;

                // Check if assignment is complete
                int daysElapsed = (int)(gameDate - assignment.StartDate).TotalDays;
                if (daysElapsed >= assignment.DurationDays)
                {
                    // Generate report for player scouting
                    if (!string.IsNullOrEmpty(assignment.TargetPlayerId))
                    {
                        var report = GeneratePlayerReport(scout, assignment);
                        if (report != null)
                        {
                            newReports.Add(report);
                            AddReport(teamId, report);
                        }
                    }
                    // TODO: Generate team scouting report for opponent teams

                    completedAssignments.Add(scout.ScoutId);
                }
            }

            // Clear completed assignments
            foreach (var scoutId in completedAssignments)
            {
                var scout = GetScoutById(scoutId);
                if (scout != null)
                {
                    scout.IsAvailable = true;
                    scout.CurrentAssignmentId = null;
                }
                _activeAssignments.Remove(scoutId);
            }

            return newReports;
        }

        /// <summary>
        /// Generates a scouting report for a player.
        /// </summary>
        private ScoutingReport GeneratePlayerReport(Scout scout, ScoutAssignment assignment)
        {
            if (_playerDatabase == null)
            {
                Debug.LogWarning("[ScoutingManager] PlayerDatabase not set - cannot generate report");
                return null;
            }

            var player = _playerDatabase.GetPlayer(assignment.TargetPlayerId);
            if (player == null)
            {
                Debug.LogWarning($"[ScoutingManager] Player {assignment.TargetPlayerId} not found");
                return null;
            }

            // Get scouting history
            int timesScoutedBefore = GetTimesScounted(assignment.TargetPlayerId);

            // Generate report
            var report = _reportGenerator.GenerateReport(player, scout, timesScoutedBefore, assignment.GamesObserved);

            // Update scouting history
            UpdateScoutingHistory(assignment.TargetPlayerId, scout.ScoutId);

            Debug.Log($"[ScoutingManager] Generated report for {player.FullName} (Confidence: {report.Confidence})");
            return report;
        }

        // ==================== SCOUTING HISTORY ====================

        /// <summary>
        /// Gets how many times a player has been scouted by this organization.
        /// </summary>
        public int GetTimesScounted(string playerId)
        {
            return _scoutingHistory.TryGetValue(playerId, out var history) ? history.TimesScounted : 0;
        }

        /// <summary>
        /// Updates scouting history for a player.
        /// </summary>
        private void UpdateScoutingHistory(string playerId, string scoutId)
        {
            if (!_scoutingHistory.ContainsKey(playerId))
            {
                _scoutingHistory[playerId] = new ScoutingHistory { PlayerId = playerId };
            }

            var history = _scoutingHistory[playerId];
            history.TimesScounted++;
            history.LastScoutedDate = DateTime.Now;
            history.ScoutIdsWhoScouted.Add(scoutId);
        }

        /// <summary>
        /// Gets full scouting history for a player.
        /// </summary>
        public ScoutingHistory GetScoutingHistory(string playerId)
        {
            return _scoutingHistory.TryGetValue(playerId, out var history) ? history : null;
        }

        // ==================== REPORT ACCESS ====================

        /// <summary>
        /// Adds a report to a team's collection.
        /// </summary>
        private void AddReport(string teamId, ScoutingReport report)
        {
            if (!_teamReports.ContainsKey(teamId))
                _teamReports[teamId] = new List<ScoutingReport>();

            // Remove old reports for same player (keep only latest)
            _teamReports[teamId].RemoveAll(r => r.PlayerId == report.PlayerId);
            _teamReports[teamId].Add(report);
        }

        /// <summary>
        /// Gets all reports for a team.
        /// </summary>
        public List<ScoutingReport> GetReports(string teamId)
        {
            return _teamReports.TryGetValue(teamId, out var reports) ? reports.ToList() : new List<ScoutingReport>();
        }

        /// <summary>
        /// Gets the most recent report for a specific player.
        /// </summary>
        public ScoutingReport GetReportForPlayer(string teamId, string playerId)
        {
            return GetReports(teamId).FirstOrDefault(r => r.PlayerId == playerId);
        }

        /// <summary>
        /// Gets all reports for a specific target type.
        /// </summary>
        public List<ScoutingReport> GetReportsByType(string teamId, ScoutingTargetType type)
        {
            return GetReports(teamId).Where(r => r.TargetType == type).ToList();
        }

        /// <summary>
        /// Gets outdated reports that need refreshing.
        /// </summary>
        public List<ScoutingReport> GetOutdatedReports(string teamId)
        {
            return GetReports(teamId).Where(r => r.IsOutdated).ToList();
        }

        /// <summary>
        /// Checks if we have a recent (non-outdated) report for a player.
        /// </summary>
        public bool HasRecentReport(string teamId, string playerId)
        {
            var report = GetReportForPlayer(teamId, playerId);
            return report != null && !report.IsOutdated;
        }

        // ==================== FREE AGENT SCOUT POOL ====================

        /// <summary>
        /// Generates a pool of available scouts to hire.
        /// </summary>
        public List<Scout> GenerateFreeAgentScouts(int count = 10)
        {
            var scouts = new List<Scout>();
            var rng = new System.Random();

            for (int i = 0; i < count; i++)
            {
                var scout = Scout.CreateRandom(null, rng);
                scout.ContractYearsRemaining = 0; // Free agent
                scouts.Add(scout);
            }

            // Add one elite scout to the pool (rare)
            if (rng.NextDouble() < 0.2)
            {
                scouts.Add(Scout.CreateElite(null));
            }

            return scouts.OrderByDescending(s => s.OverallRating).ToList();
        }
    }

    // ==================== SUPPORTING CLASSES ====================

    /// <summary>
    /// Represents an active scouting assignment.
    /// </summary>
    [Serializable]
    public class ScoutAssignment
    {
        public string AssignmentId;
        public string ScoutId;

        // Target (either player or team)
        public string TargetPlayerId;
        public string TargetPlayerName;
        public string TargetTeamId;
        public ScoutingTargetType TargetType;

        // Timing
        public DateTime StartDate;
        public int DurationDays;
        public int GamesObserved;

        public float Progress => Mathf.Clamp01((float)(DateTime.Now - StartDate).TotalDays / DurationDays);
        public bool IsComplete => Progress >= 1f;
        public int DaysRemaining => Math.Max(0, DurationDays - (int)(DateTime.Now - StartDate).TotalDays);

        public string TargetDisplay => !string.IsNullOrEmpty(TargetPlayerName) ? TargetPlayerName :
                                       !string.IsNullOrEmpty(TargetTeamId) ? TargetTeamId : "Unknown";
    }

    /// <summary>
    /// Tracks scouting history for a player.
    /// </summary>
    [Serializable]
    public class ScoutingHistory
    {
        public string PlayerId;
        public int TimesScounted;
        public DateTime LastScoutedDate;
        public List<string> ScoutIdsWhoScouted = new List<string>();

        public int DaysSinceLastScouted => (int)(DateTime.Now - LastScoutedDate).TotalDays;
    }
}
