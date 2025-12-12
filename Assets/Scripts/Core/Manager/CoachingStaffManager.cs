using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages coaching staff hiring, firing, and assignments.
    /// Each team has a head coach and various assistant/specialist coaches.
    /// </summary>
    public class CoachingStaffManager
    {
        // Staff limits
        public const int MAX_ASSISTANT_COACHES = 3;
        public const int MAX_TOTAL_STAFF = 10;

        // Required positions (team must have these filled)
        private static readonly CoachPosition[] RequiredPositions = new[]
        {
            CoachPosition.HeadCoach,
            CoachPosition.AssistantCoach,
            CoachPosition.DefensiveCoordinator,
            CoachPosition.OffensiveCoordinator
        };

        private Dictionary<string, List<Coach>> _teamStaffs = new();
        private Dictionary<string, Coach> _coachesById = new();
        private List<Coach> _freeAgentCoaches = new();

        private readonly System.Random _rng;

        public CoachingStaffManager()
        {
            _rng = new System.Random();
        }

        // ==================== STAFF QUERIES ====================

        /// <summary>
        /// Gets all coaches for a team.
        /// </summary>
        public List<Coach> GetCoachingStaff(string teamId)
        {
            return _teamStaffs.TryGetValue(teamId, out var staff) ? staff.ToList() : new List<Coach>();
        }

        /// <summary>
        /// Gets a specific coach by ID.
        /// </summary>
        public Coach GetCoachById(string coachId)
        {
            return _coachesById.TryGetValue(coachId, out var coach) ? coach : null;
        }

        /// <summary>
        /// Gets the head coach for a team.
        /// </summary>
        public Coach GetHeadCoach(string teamId)
        {
            return GetCoachingStaff(teamId).FirstOrDefault(c => c.Position == CoachPosition.HeadCoach);
        }

        /// <summary>
        /// Gets coaches by position.
        /// </summary>
        public List<Coach> GetCoachesByPosition(string teamId, CoachPosition position)
        {
            return GetCoachingStaff(teamId).Where(c => c.Position == position).ToList();
        }

        /// <summary>
        /// Gets all assistant coaches.
        /// </summary>
        public List<Coach> GetAssistantCoaches(string teamId)
        {
            return GetCoachingStaff(teamId)
                .Where(c => c.Position != CoachPosition.HeadCoach)
                .ToList();
        }

        /// <summary>
        /// Gets unfilled positions for a team.
        /// </summary>
        public List<CoachPosition> GetUnfilledPositions(string teamId)
        {
            var currentPositions = GetCoachingStaff(teamId).Select(c => c.Position).ToHashSet();
            return RequiredPositions.Where(p => !currentPositions.Contains(p)).ToList();
        }

        // ==================== HIRING ====================

        /// <summary>
        /// Hires a coach for a team.
        /// </summary>
        public (bool success, string message) HireCoach(string teamId, Coach coach)
        {
            if (!_teamStaffs.ContainsKey(teamId))
                _teamStaffs[teamId] = new List<Coach>();

            var staff = _teamStaffs[teamId];

            // Check staff limit
            if (staff.Count >= MAX_TOTAL_STAFF)
                return (false, $"Team already has maximum {MAX_TOTAL_STAFF} staff members.");

            // Check position limits
            if (coach.Position == CoachPosition.HeadCoach)
            {
                if (staff.Any(c => c.Position == CoachPosition.HeadCoach))
                    return (false, "Team already has a head coach. Fire current HC first.");
            }
            else if (coach.Position == CoachPosition.AssistantCoach)
            {
                int currentAssistants = staff.Count(c => c.Position == CoachPosition.AssistantCoach);
                if (currentAssistants >= MAX_ASSISTANT_COACHES)
                    return (false, $"Team already has maximum {MAX_ASSISTANT_COACHES} assistant coaches.");
            }
            else
            {
                // Specialist positions - only one of each
                if (staff.Any(c => c.Position == coach.Position))
                    return (false, $"Team already has a {coach.Position}.");
            }

            // Add coach
            coach.TeamId = teamId;
            staff.Add(coach);
            _coachesById[coach.CoachId] = coach;

            // Remove from free agents if applicable
            _freeAgentCoaches.RemoveAll(c => c.CoachId == coach.CoachId);

            Debug.Log($"[CoachingStaffManager] {teamId} hired {coach.FullName} as {coach.Position}");
            return (true, $"Successfully hired {coach.FullName} as {coach.Position}.");
        }

        /// <summary>
        /// Fires a coach from a team.
        /// </summary>
        public (bool success, string message) FireCoach(string teamId, string coachId)
        {
            if (!_teamStaffs.ContainsKey(teamId))
                return (false, "Team not found.");

            var staff = _teamStaffs[teamId];
            var coach = staff.FirstOrDefault(c => c.CoachId == coachId);

            if (coach == null)
                return (false, "Coach not found on this team.");

            // Can't fire head coach during season without replacement
            // (This could be enforced at a higher level)

            staff.Remove(coach);
            _coachesById.Remove(coachId);

            // Add to free agents
            coach.TeamId = null;
            coach.ContractYearsRemaining = 0;
            _freeAgentCoaches.Add(coach);

            Debug.Log($"[CoachingStaffManager] {teamId} fired {coach.FullName}");
            return (true, $"Released {coach.FullName}.");
        }

        /// <summary>
        /// Promotes an assistant to head coach.
        /// </summary>
        public (bool success, string message) PromoteToHeadCoach(string teamId, string coachId)
        {
            var staff = GetCoachingStaff(teamId);
            var currentHC = staff.FirstOrDefault(c => c.Position == CoachPosition.HeadCoach);

            if (currentHC != null)
                return (false, "Must fire current head coach first.");

            var coach = staff.FirstOrDefault(c => c.CoachId == coachId);
            if (coach == null)
                return (false, "Coach not found on staff.");

            if (coach.Position == CoachPosition.HeadCoach)
                return (false, "Coach is already head coach.");

            coach.Position = CoachPosition.HeadCoach;
            Debug.Log($"[CoachingStaffManager] {teamId} promoted {coach.FullName} to Head Coach");
            return (true, $"Promoted {coach.FullName} to Head Coach.");
        }

        // ==================== STAFF QUALITY CALCULATIONS ====================

        /// <summary>
        /// Gets overall coaching staff quality (0-100).
        /// </summary>
        public int GetStaffQuality(string teamId)
        {
            var staff = GetCoachingStaff(teamId);
            if (staff.Count == 0) return 0;

            // Weight head coach more heavily
            float totalQuality = 0f;
            float totalWeight = 0f;

            foreach (var coach in staff)
            {
                float weight = coach.Position == CoachPosition.HeadCoach ? 3f : 1f;
                totalQuality += coach.OverallRating * weight;
                totalWeight += weight;
            }

            return Mathf.RoundToInt(totalQuality / totalWeight);
        }

        /// <summary>
        /// Gets player development quality for the staff (0-1).
        /// </summary>
        public float GetDevelopmentQuality(string teamId)
        {
            var staff = GetCoachingStaff(teamId);
            if (staff.Count == 0) return 0.5f;

            // Weight by position relevance
            float totalQuality = 0f;
            float totalWeight = 0f;

            foreach (var coach in staff)
            {
                float weight = coach.Position switch
                {
                    CoachPosition.PlayerDevelopment => 4f,
                    CoachPosition.ShootingCoach => 2f,
                    CoachPosition.BigManCoach => 2f,
                    CoachPosition.GuardSkillsCoach => 2f,
                    CoachPosition.HeadCoach => 1.5f,
                    CoachPosition.AssistantCoach => 1f,
                    _ => 0.5f
                };

                totalQuality += coach.PlayerDevelopment / 100f * weight;
                totalWeight += weight;
            }

            return Mathf.Clamp01(totalQuality / totalWeight);
        }

        /// <summary>
        /// Gets offensive coaching quality (0-1).
        /// </summary>
        public float GetOffensiveQuality(string teamId)
        {
            var staff = GetCoachingStaff(teamId);
            if (staff.Count == 0) return 0.5f;

            var hc = GetHeadCoach(teamId);
            var oc = staff.FirstOrDefault(c => c.Position == CoachPosition.OffensiveCoordinator);

            float quality = 0f;
            float weight = 0f;

            if (hc != null)
            {
                quality += hc.OffensiveScheme / 100f * 2f;
                weight += 2f;
            }
            if (oc != null)
            {
                quality += oc.OffensiveScheme / 100f * 3f;
                weight += 3f;
            }

            return weight > 0 ? quality / weight : 0.5f;
        }

        /// <summary>
        /// Gets defensive coaching quality (0-1).
        /// </summary>
        public float GetDefensiveQuality(string teamId)
        {
            var staff = GetCoachingStaff(teamId);
            if (staff.Count == 0) return 0.5f;

            var hc = GetHeadCoach(teamId);
            var dc = staff.FirstOrDefault(c => c.Position == CoachPosition.DefensiveCoordinator);

            float quality = 0f;
            float weight = 0f;

            if (hc != null)
            {
                quality += hc.DefensiveScheme / 100f * 2f;
                weight += 2f;
            }
            if (dc != null)
            {
                quality += dc.DefensiveScheme / 100f * 3f;
                weight += 3f;
            }

            return weight > 0 ? quality / weight : 0.5f;
        }

        /// <summary>
        /// Gets game management quality (0-1).
        /// </summary>
        public float GetGameManagementQuality(string teamId)
        {
            var hc = GetHeadCoach(teamId);
            if (hc == null) return 0.5f;

            return (hc.GameManagement + hc.ClockManagement + hc.RotationManagement) / 300f;
        }

        /// <summary>
        /// Gets position-specific development bonus.
        /// </summary>
        public float GetPositionDevelopmentBonus(string teamId, Position playerPosition)
        {
            var staff = GetCoachingStaff(teamId);
            float bonus = 0f;

            foreach (var coach in staff)
            {
                bonus += coach.GetDevelopmentBonus(playerPosition);
            }

            // Cap total bonus at 100%
            return Mathf.Min(bonus, 1f);
        }

        // ==================== STAFF BUDGET ====================

        /// <summary>
        /// Gets total staff salary for a team.
        /// </summary>
        public int GetTotalStaffSalary(string teamId)
        {
            return GetCoachingStaff(teamId).Sum(c => c.AnnualSalary);
        }

        /// <summary>
        /// Gets staff salary breakdown.
        /// </summary>
        public Dictionary<CoachPosition, int> GetSalaryBreakdown(string teamId)
        {
            return GetCoachingStaff(teamId)
                .GroupBy(c => c.Position)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.AnnualSalary));
        }

        // ==================== FREE AGENT POOL ====================

        /// <summary>
        /// Gets available coaches to hire.
        /// </summary>
        public List<Coach> GetFreeAgentCoaches()
        {
            return _freeAgentCoaches.OrderByDescending(c => c.OverallRating).ToList();
        }

        /// <summary>
        /// Gets free agent coaches by position.
        /// </summary>
        public List<Coach> GetFreeAgentCoachesByPosition(CoachPosition position)
        {
            return _freeAgentCoaches
                .Where(c => c.Position == position)
                .OrderByDescending(c => c.OverallRating)
                .ToList();
        }

        /// <summary>
        /// Generates a pool of free agent coaches.
        /// </summary>
        public void GenerateFreeAgentPool(int count = 30)
        {
            _freeAgentCoaches.Clear();

            var positions = (CoachPosition[])Enum.GetValues(typeof(CoachPosition));

            for (int i = 0; i < count; i++)
            {
                var position = positions[_rng.Next(positions.Length)];
                var coach = Coach.CreateRandom(null, position, _rng);
                coach.ContractYearsRemaining = 0;
                _freeAgentCoaches.Add(coach);
                _coachesById[coach.CoachId] = coach;
            }

            // Ensure some head coach candidates
            int hcCount = _freeAgentCoaches.Count(c => c.Position == CoachPosition.HeadCoach);
            while (hcCount < 5)
            {
                var coach = Coach.CreateRandom(null, CoachPosition.HeadCoach, _rng);
                coach.ContractYearsRemaining = 0;
                _freeAgentCoaches.Add(coach);
                _coachesById[coach.CoachId] = coach;
                hcCount++;
            }

            Debug.Log($"[CoachingStaffManager] Generated {_freeAgentCoaches.Count} free agent coaches");
        }

        // ==================== INITIALIZATION ====================

        /// <summary>
        /// Initializes coaching staff for a team.
        /// </summary>
        public void InitializeTeamStaff(string teamId, int quality = 0)
        {
            var staff = Coach.CreateCoachingStaff(teamId, quality);

            _teamStaffs[teamId] = staff;
            foreach (var coach in staff)
            {
                _coachesById[coach.CoachId] = coach;
            }

            Debug.Log($"[CoachingStaffManager] Initialized staff for {teamId} with {staff.Count} coaches");
        }

        /// <summary>
        /// Initializes all teams in the league.
        /// </summary>
        public void InitializeAllTeams(List<string> teamIds)
        {
            foreach (var teamId in teamIds)
            {
                // Vary quality by team (could be based on team budget/history)
                int quality = _rng.Next(0, 3);
                InitializeTeamStaff(teamId, quality);
            }

            // Generate free agent pool
            GenerateFreeAgentPool();
        }

        // ==================== CONTRACT MANAGEMENT ====================

        /// <summary>
        /// Processes end of season contract updates.
        /// </summary>
        public List<Coach> ProcessEndOfSeasonContracts()
        {
            var expiredContracts = new List<Coach>();

            foreach (var staff in _teamStaffs.Values)
            {
                foreach (var coach in staff.ToList())
                {
                    coach.ContractYearsRemaining--;

                    if (coach.ContractYearsRemaining <= 0)
                    {
                        expiredContracts.Add(coach);
                    }
                }
            }

            return expiredContracts;
        }

        /// <summary>
        /// Extends a coach's contract.
        /// </summary>
        public (bool success, string message) ExtendContract(string coachId, int years, int annualSalary)
        {
            var coach = GetCoachById(coachId);
            if (coach == null)
                return (false, "Coach not found.");

            coach.ContractYearsRemaining = years;
            coach.AnnualSalary = annualSalary;
            coach.ContractStartDate = DateTime.Now;

            return (true, $"Extended {coach.FullName}'s contract for {years} years at ${annualSalary:N0}/year.");
        }

        // ==================== COACH SEARCH ====================

        /// <summary>
        /// Searches for coaches matching criteria.
        /// </summary>
        public List<Coach> SearchCoaches(CoachSearchCriteria criteria)
        {
            var coaches = _freeAgentCoaches.AsEnumerable();

            if (criteria.Position.HasValue)
                coaches = coaches.Where(c => c.Position == criteria.Position.Value);

            if (criteria.MinOverall > 0)
                coaches = coaches.Where(c => c.OverallRating >= criteria.MinOverall);

            if (criteria.MaxSalary > 0)
                coaches = coaches.Where(c => c.MarketValue <= criteria.MaxSalary);

            if (criteria.Specializations?.Any() == true)
                coaches = coaches.Where(c => c.Specializations.Intersect(criteria.Specializations).Any());

            if (criteria.MinExperience > 0)
                coaches = coaches.Where(c => c.ExperienceYears >= criteria.MinExperience);

            return coaches.OrderByDescending(c => c.OverallRating).ToList();
        }
    }

    /// <summary>
    /// Criteria for searching coaches.
    /// </summary>
    public class CoachSearchCriteria
    {
        public CoachPosition? Position;
        public int MinOverall;
        public int MaxSalary;
        public List<CoachSpecialization> Specializations;
        public int MinExperience;
    }
}
