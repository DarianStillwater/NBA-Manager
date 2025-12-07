using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages scouting operations including scouts, assignments, and reports.
    /// Each team can have up to 5 scouts.
    /// </summary>
    public class ScoutingManager
    {
        public const int MAX_SCOUTS_PER_TEAM = 5;
        
        private Dictionary<string, List<Scout>> _teamScouts = new Dictionary<string, List<Scout>>();
        private Dictionary<string, List<ScoutingReport>> _teamReports = new Dictionary<string, List<ScoutingReport>>();
        private Dictionary<string, ScoutAssignment> _activeAssignments = new Dictionary<string, ScoutAssignment>();

        // ==================== SCOUT MANAGEMENT ====================

        /// <summary>
        /// Gets all scouts for a team.
        /// </summary>
        public List<Scout> GetScouts(string teamId)
        {
            return _teamScouts.TryGetValue(teamId, out var scouts) ? scouts.ToList() : new List<Scout>();
        }

        /// <summary>
        /// Hires a new scout for a team.
        /// </summary>
        public bool HireScout(string teamId, Scout scout)
        {
            if (!_teamScouts.ContainsKey(teamId))
                _teamScouts[teamId] = new List<Scout>();
            
            if (_teamScouts[teamId].Count >= MAX_SCOUTS_PER_TEAM)
            {
                Debug.LogWarning($"{teamId} already has {MAX_SCOUTS_PER_TEAM} scouts");
                return false;
            }
            
            scout.TeamId = teamId;
            _teamScouts[teamId].Add(scout);
            return true;
        }

        /// <summary>
        /// Fires a scout.
        /// </summary>
        public bool FireScout(string teamId, string scoutId)
        {
            if (!_teamScouts.ContainsKey(teamId))
                return false;
            
            // Cancel any active assignment
            var assignment = _activeAssignments.Values.FirstOrDefault(a => a.ScoutId == scoutId);
            if (assignment != null)
                _activeAssignments.Remove(scoutId);
            
            return _teamScouts[teamId].RemoveAll(s => s.ScoutId == scoutId) > 0;
        }

        /// <summary>
        /// Generates starting scouts for a team.
        /// </summary>
        public void GenerateStartingScouts(string teamId, int count = 3)
        {
            var rng = new System.Random();
            var specialties = Enum.GetValues(typeof(ScoutSpecialty)).Cast<ScoutSpecialty>().ToList();
            
            for (int i = 0; i < count; i++)
            {
                var scout = new Scout
                {
                    ScoutId = $"SCOUT_{teamId}_{i}",
                    Name = GenerateScoutName(rng),
                    Specialty = specialties[rng.Next(specialties.Count)],
                    SkillRating = 50 + rng.Next(30), // 50-79
                    Experience = rng.Next(15),
                    Salary = 50_000 + rng.Next(100_000)
                };
                
                HireScout(teamId, scout);
            }
        }

        private string GenerateScoutName(System.Random rng)
        {
            var firstNames = new[] { "Mike", "John", "Bob", "Steve", "Tom", "Bill", "Dave", "Jim", "Dan", "Rick" };
            var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor", "Anderson" };
            return $"{firstNames[rng.Next(firstNames.Length)]} {lastNames[rng.Next(lastNames.Length)]}";
        }

        // ==================== ASSIGNMENTS ====================

        /// <summary>
        /// Assigns a scout to evaluate a target.
        /// </summary>
        public bool AssignScout(string scoutId, ScoutingTarget target, int durationDays)
        {
            var scout = GetScoutById(scoutId);
            if (scout == null)
                return false;
            
            // Check if scout is already assigned
            if (_activeAssignments.ContainsKey(scoutId))
            {
                Debug.LogWarning($"Scout {scoutId} is already on an assignment");
                return false;
            }
            
            var assignment = new ScoutAssignment
            {
                ScoutId = scoutId,
                Target = target,
                StartDate = DateTime.Now,
                DurationDays = durationDays,
                GamesObserved = 0
            };
            
            _activeAssignments[scoutId] = assignment;
            return true;
        }

        /// <summary>
        /// Cancels a scout's current assignment.
        /// </summary>
        public void CancelAssignment(string scoutId)
        {
            _activeAssignments.Remove(scoutId);
        }

        /// <summary>
        /// Gets a scout's current assignment.
        /// </summary>
        public ScoutAssignment GetAssignment(string scoutId)
        {
            return _activeAssignments.TryGetValue(scoutId, out var a) ? a : null;
        }

        // ==================== REPORT GENERATION ====================

        /// <summary>
        /// Processes scouting for one day/game. Call this during game simulation.
        /// </summary>
        public List<ScoutingReport> ProcessScouting(string teamId, DateTime gameDate)
        {
            var newReports = new List<ScoutingReport>();
            var scouts = GetScouts(teamId);
            
            foreach (var scout in scouts)
            {
                if (!_activeAssignments.TryGetValue(scout.ScoutId, out var assignment))
                    continue;
                
                assignment.GamesObserved++;
                
                // Check if assignment is complete
                bool isComplete = (gameDate - assignment.StartDate).TotalDays >= assignment.DurationDays;
                
                if (isComplete)
                {
                    var report = GenerateReport(scout, assignment);
                    newReports.Add(report);
                    AddReport(teamId, report);
                    _activeAssignments.Remove(scout.ScoutId);
                }
            }
            
            return newReports;
        }

        private ScoutingReport GenerateReport(Scout scout, ScoutAssignment assignment)
        {
            var rng = new System.Random();
            
            // Report accuracy based on scout skill, time spent, and specialty match
            float baseAccuracy = scout.SkillRating / 100f;
            float timeBonus = Math.Min(assignment.GamesObserved * 0.1f, 0.3f); // Up to 30% from games
            float specialtyBonus = IsSpecialtyMatch(scout.Specialty, assignment.Target) ? 0.15f : 0f;
            
            float accuracy = Math.Min(baseAccuracy + timeBonus + specialtyBonus, 0.95f);
            
            // Add some randomness (scouts can have bad takes)
            float randomFactor = (float)rng.NextDouble() * 0.2f - 0.1f;
            accuracy = Math.Max(0.3f, Math.Min(0.95f, accuracy + randomFactor));
            
            return new ScoutingReport
            {
                ReportId = $"RPT_{DateTime.Now.Ticks}",
                ScoutId = scout.ScoutId,
                ScoutName = scout.Name,
                Target = assignment.Target,
                GeneratedDate = DateTime.Now,
                GamesObserved = assignment.GamesObserved,
                AccuracyRating = accuracy,
                
                // Report content generated based on target type
                Strengths = GenerateStrengths(assignment.Target, rng),
                Weaknesses = GenerateWeaknesses(assignment.Target, rng),
                Recommendation = GenerateRecommendation(assignment.Target, accuracy, rng),
                Grade = CalculateGrade(assignment.Target, accuracy)
            };
        }

        private bool IsSpecialtyMatch(ScoutSpecialty specialty, ScoutingTarget target)
        {
            return target.Type switch
            {
                ScoutingTargetType.DraftProspect => specialty == ScoutSpecialty.Amateur,
                ScoutingTargetType.NBAPlayer => specialty == ScoutSpecialty.Pro,
                ScoutingTargetType.OpponentTeam => specialty == ScoutSpecialty.Advance,
                ScoutingTargetType.InternationalProspect => specialty == ScoutSpecialty.International,
                _ => false
            };
        }

        private List<string> GenerateStrengths(ScoutingTarget target, System.Random rng)
        {
            var allStrengths = new[] {
                "Elite athleticism", "High basketball IQ", "Great court vision",
                "Excellent shooter", "Strong defender", "Good motor",
                "Leadership qualities", "Versatile skill set", "Good size for position",
                "Physical tools", "Quick first step", "Good hands"
            };
            
            int count = 2 + rng.Next(3);
            return allStrengths.OrderBy(x => rng.Next()).Take(count).ToList();
        }

        private List<string> GenerateWeaknesses(ScoutingTarget target, System.Random rng)
        {
            var allWeaknesses = new[] {
                "Inconsistent shooter", "Needs to add strength", "Below average defender",
                "Limited playmaking", "Questionable motor", "Injury concerns",
                "Poor shot selection", "Struggles vs length", "Limited range",
                "Turnover prone", "Needs to improve conditioning", "Raw offensive game"
            };
            
            int count = 1 + rng.Next(3);
            return allWeaknesses.OrderBy(x => rng.Next()).Take(count).ToList();
        }

        private string GenerateRecommendation(ScoutingTarget target, float accuracy, System.Random rng)
        {
            if (target.Type == ScoutingTargetType.DraftProspect)
            {
                var recs = new[] {
                    "Strong first-round value",
                    "Solid second-round pick",
                    "Worth a late flyer",
                    "Not recommended - pass",
                    "Could be a steal in the second round"
                };
                return recs[rng.Next(recs.Length)];
            }
            
            if (target.Type == ScoutingTargetType.OpponentTeam)
            {
                var recs = new[] {
                    "Key matchup advantage at PG",
                    "Attack their weak transition defense",
                    "Exploit mismatches in the post",
                    "Control the pace - they struggle in half court",
                    "Focus on their weak perimeter defense"
                };
                return recs[rng.Next(recs.Length)];
            }
            
            return "Continue monitoring";
        }

        private string CalculateGrade(ScoutingTarget target, float accuracy)
        {
            // A+ to F based on target quality (with accuracy affecting confidence)
            var grades = new[] { "A+", "A", "A-", "B+", "B", "B-", "C+", "C", "C-", "D", "F" };
            int gradeIdx = (int)((1 - accuracy) * grades.Length);
            return grades[Math.Max(0, Math.Min(gradeIdx, grades.Length - 1))];
        }

        // ==================== REPORTS ====================

        private void AddReport(string teamId, ScoutingReport report)
        {
            if (!_teamReports.ContainsKey(teamId))
                _teamReports[teamId] = new List<ScoutingReport>();
            
            _teamReports[teamId].Add(report);
        }

        public List<ScoutingReport> GetReports(string teamId)
        {
            return _teamReports.TryGetValue(teamId, out var reports) ? reports.ToList() : new List<ScoutingReport>();
        }

        public List<ScoutingReport> GetReportsForTarget(string teamId, string targetId)
        {
            return GetReports(teamId).Where(r => r.Target.TargetId == targetId).ToList();
        }

        private Scout GetScoutById(string scoutId)
        {
            foreach (var scouts in _teamScouts.Values)
            {
                var scout = scouts.FirstOrDefault(s => s.ScoutId == scoutId);
                if (scout != null) return scout;
            }
            return null;
        }
    }

    // ==================== DATA CLASSES ====================

    [Serializable]
    public class Scout
    {
        public string ScoutId;
        public string TeamId;
        public string Name;
        public ScoutSpecialty Specialty;
        public int SkillRating;     // 1-100
        public int Experience;       // Years
        public int Salary;
    }

    public enum ScoutSpecialty
    {
        Pro,            // Current NBA players
        Amateur,        // College prospects
        International,  // Overseas players
        Advance         // Next opponent preparation
    }

    [Serializable]
    public class ScoutingTarget
    {
        public string TargetId;
        public string TargetName;
        public ScoutingTargetType Type;
    }

    public enum ScoutingTargetType
    {
        DraftProspect,
        NBAPlayer,
        OpponentTeam,
        InternationalProspect
    }

    [Serializable]
    public class ScoutAssignment
    {
        public string ScoutId;
        public ScoutingTarget Target;
        public DateTime StartDate;
        public int DurationDays;
        public int GamesObserved;
        
        public float Progress => (float)(DateTime.Now - StartDate).TotalDays / DurationDays;
        public bool IsComplete => Progress >= 1f;
    }

    [Serializable]
    public class ScoutingReport
    {
        public string ReportId;
        public string ScoutId;
        public string ScoutName;
        public ScoutingTarget Target;
        public DateTime GeneratedDate;
        public int GamesObserved;
        public float AccuracyRating;  // 0-1, how reliable is this report
        
        public List<string> Strengths = new List<string>();
        public List<string> Weaknesses = new List<string>();
        public string Recommendation;
        public string Grade;
    }
}
