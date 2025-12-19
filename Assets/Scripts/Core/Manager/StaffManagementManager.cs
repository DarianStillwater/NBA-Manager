using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Central manager for all staff operations including assignments, delegation, and coordination.
    /// Wraps existing CoachingStaffManager and ScoutingManager for unified interface.
    /// This is the ONLY entry point for staff operations - other code should go through this facade.
    /// </summary>
    public class StaffManagementManager : MonoBehaviour
    {
        public static StaffManagementManager Instance { get; private set; }

        // ==================== WRAPPED MANAGERS ====================
        private CoachingStaffManager _coachingStaffManager;
        private ScoutingManager _scoutingManager;

        /// <summary>
        /// Direct access to coaching staff manager (prefer facade methods when possible).
        /// </summary>
        public CoachingStaffManager CoachingStaff => _coachingStaffManager;

        /// <summary>
        /// Direct access to scouting manager (prefer facade methods when possible).
        /// </summary>
        public ScoutingManager Scouting => _scoutingManager;

        [Header("User Configuration")]
        [SerializeField] private UserRole currentUserRole = UserRole.Both;
        [SerializeField] private string userTeamId;

        [Header("Staff Tracking")]
        [SerializeField] private List<TeamStaffConfiguration> teamConfigurations = new List<TeamStaffConfiguration>();
        [SerializeField] private List<StaffTaskAssignment> activeAssignments = new List<StaffTaskAssignment>();
        [SerializeField] private List<DevelopmentAssignment> developmentAssignments = new List<DevelopmentAssignment>();

        [Header("Settings")]
        [SerializeField] private bool autoProcessDailyWork = true;

        // Events
        public event Action<UserRole> OnUserRoleChanged;
        public event Action<StaffTaskAssignment> OnStaffAssigned;
        public event Action<StaffTaskAssignment> OnStaffUnassigned;
        public event Action<ScoutEvaluationResult> OnScoutReportGenerated;
        public event Action<CoordinatorSuggestion> OnCoordinatorSuggestion;
        public event Action<string, string> OnStaffHired;  // staffId, teamId
        public event Action<string, string> OnStaffFired;  // staffId, teamId

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeManagers();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Initializes the wrapped managers.
        /// </summary>
        private void InitializeManagers()
        {
            _coachingStaffManager = new CoachingStaffManager();
            _scoutingManager = new ScoutingManager();
            Debug.Log("[StaffManagement] Initialized CoachingStaffManager and ScoutingManager");
        }

        /// <summary>
        /// Initialize managers with PlayerDatabase reference (call after PlayerDatabase is loaded).
        /// </summary>
        public void SetPlayerDatabase(PlayerDatabase playerDatabase)
        {
            _scoutingManager?.SetPlayerDatabase(playerDatabase);
            Debug.Log("[StaffManagement] PlayerDatabase set for ScoutingManager");
        }

        /// <summary>
        /// Initialize all teams with starting staff.
        /// </summary>
        public void InitializeAllTeamStaff(List<string> teamIds)
        {
            _coachingStaffManager?.InitializeAllTeams(teamIds);
            foreach (var teamId in teamIds)
            {
                _scoutingManager?.GenerateStartingScouts(teamId);
            }
            Debug.Log($"[StaffManagement] Initialized staff for {teamIds.Count} teams");
        }

        // ==================== USER ROLE MANAGEMENT ====================

        /// <summary>
        /// Set the user's role (GM-only, HC-only, or both).
        /// </summary>
        public void SetUserRole(UserRole role)
        {
            currentUserRole = role;
            OnUserRoleChanged?.Invoke(role);

            Debug.Log($"[StaffManagement] User role set to: {role}");

            // Check if user needs to hire a head coach
            if (role == UserRole.GMOnly && !string.IsNullOrEmpty(userTeamId))
            {
                var config = GetOrCreateConfiguration(userTeamId);
                if (config.NeedsHeadCoach)
                {
                    Debug.Log("[StaffManagement] User is GM-only and needs to hire a Head Coach");
                }
            }
        }

        /// <summary>
        /// Get the current user role.
        /// </summary>
        public UserRole GetUserRole() => currentUserRole;

        /// <summary>
        /// Check if the user acts as GM.
        /// </summary>
        public bool UserIsGM => currentUserRole == UserRole.GMOnly || currentUserRole == UserRole.Both;

        /// <summary>
        /// Check if the user acts as Head Coach.
        /// </summary>
        public bool UserIsHeadCoach => currentUserRole == UserRole.HeadCoachOnly || currentUserRole == UserRole.Both;

        /// <summary>
        /// Set the user's team.
        /// </summary>
        public void SetUserTeam(string teamId)
        {
            userTeamId = teamId;
            GetOrCreateConfiguration(teamId);
        }

        // ==================== TEAM CONFIGURATION ====================

        /// <summary>
        /// Get or create staff configuration for a team.
        /// </summary>
        public TeamStaffConfiguration GetOrCreateConfiguration(string teamId)
        {
            var config = teamConfigurations.FirstOrDefault(c => c.TeamId == teamId);
            if (config == null)
            {
                config = new TeamStaffConfiguration
                {
                    TeamId = teamId,
                    CurrentUserRole = teamId == userTeamId ? currentUserRole : UserRole.Both
                };
                teamConfigurations.Add(config);
            }
            return config;
        }

        /// <summary>
        /// Check if a team can hire for a specific position.
        /// </summary>
        public bool CanHireForPosition(string teamId, StaffPositionType position)
        {
            var config = GetOrCreateConfiguration(teamId);
            return config.CanHire(position);
        }

        /// <summary>
        /// Check if team needs a head coach (GM-only mode).
        /// </summary>
        public bool TeamNeedsHeadCoach(string teamId)
        {
            var config = GetOrCreateConfiguration(teamId);
            return config.NeedsHeadCoach;
        }

        // ==================== SCOUT ASSIGNMENT ====================

        /// <summary>
        /// Assign a scout to a specific task.
        /// </summary>
        public StaffTaskAssignment AssignScoutToTask(
            string scoutId,
            string teamId,
            ScoutTaskType taskType,
            List<string> targetPlayerIds = null,
            string targetTeamId = null)
        {
            // Remove any existing assignment for this scout
            UnassignStaff(scoutId);

            // Create new assignment
            var assignment = StaffTaskAssignment.CreateScoutAssignment(
                scoutId, teamId, taskType, targetPlayerIds, targetTeamId);

            activeAssignments.Add(assignment);
            OnStaffAssigned?.Invoke(assignment);

            Debug.Log($"[StaffManagement] Scout {scoutId} assigned to {taskType}");

            return assignment;
        }

        // ==================== COACH ASSIGNMENT ====================

        /// <summary>
        /// Assign an assistant coach to develop specific players.
        /// </summary>
        public (bool success, string message) AssignCoachToPlayers(
            string coachId,
            string teamId,
            List<string> playerIds,
            int coachDevelopmentRating)
        {
            // Check coach capacity
            int capacity = DevelopmentAssignment.CalculateCoachCapacity(coachDevelopmentRating);
            if (playerIds.Count > capacity)
            {
                return (false, $"Coach can only handle {capacity} players (rating: {coachDevelopmentRating})");
            }

            // Remove any existing assignment for this coach
            UnassignStaff(coachId);

            // Create coach assignment
            var assignment = StaffTaskAssignment.CreateCoachAssignment(
                coachId, teamId, StaffPositionType.AssistantCoach,
                CoachTaskType.PlayerDevelopment, playerIds);

            activeAssignments.Add(assignment);

            // Create development assignments for each player
            foreach (var playerId in playerIds)
            {
                var devAssignment = new DevelopmentAssignment
                {
                    AssignmentId = $"DEV_{Guid.NewGuid().ToString().Substring(0, 8)}",
                    CoachId = coachId,
                    PlayerId = playerId,
                    TeamId = teamId,
                    CoachDevelopmentRating = coachDevelopmentRating,
                    DevelopmentBonus = DevelopmentAssignment.CalculateDevelopmentBonus(coachDevelopmentRating, false),
                    StartDate = DateTime.Now,
                    WeeksAssigned = 0
                };
                developmentAssignments.Add(devAssignment);
            }

            OnStaffAssigned?.Invoke(assignment);

            Debug.Log($"[StaffManagement] Coach {coachId} assigned to develop {playerIds.Count} players");

            return (true, $"Assigned to develop {playerIds.Count} players");
        }

        /// <summary>
        /// Assign a coordinator to game strategy.
        /// </summary>
        public StaffTaskAssignment AssignCoordinatorToStrategy(
            string coordinatorId,
            string teamId,
            StaffPositionType coordinatorType)
        {
            if (coordinatorType != StaffPositionType.OffensiveCoordinator &&
                coordinatorType != StaffPositionType.DefensiveCoordinator)
            {
                Debug.LogError("[StaffManagement] Invalid coordinator type");
                return null;
            }

            // Remove any existing assignment
            UnassignStaff(coordinatorId);

            var assignment = StaffTaskAssignment.CreateCoachAssignment(
                coordinatorId, teamId, coordinatorType, CoachTaskType.GameStrategy);

            activeAssignments.Add(assignment);
            OnStaffAssigned?.Invoke(assignment);

            Debug.Log($"[StaffManagement] Coordinator {coordinatorId} assigned to game strategy");

            return assignment;
        }

        // ==================== UNASSIGNMENT ====================

        /// <summary>
        /// Remove any active assignment for a staff member.
        /// </summary>
        public void UnassignStaff(string staffId)
        {
            var assignment = activeAssignments.FirstOrDefault(a => a.StaffId == staffId && a.IsActive);
            if (assignment != null)
            {
                assignment.IsActive = false;
                activeAssignments.Remove(assignment);

                // Remove related development assignments
                developmentAssignments.RemoveAll(d => d.CoachId == staffId);

                OnStaffUnassigned?.Invoke(assignment);

                Debug.Log($"[StaffManagement] Staff {staffId} unassigned");
            }
        }

        // ==================== QUERY METHODS ====================

        /// <summary>
        /// Get all active assignments for a team.
        /// </summary>
        public List<StaffTaskAssignment> GetActiveAssignments(string teamId)
        {
            return activeAssignments.Where(a => a.TeamId == teamId && a.IsActive).ToList();
        }

        /// <summary>
        /// Get assignment for a specific staff member.
        /// </summary>
        public StaffTaskAssignment GetAssignment(string staffId)
        {
            return activeAssignments.FirstOrDefault(a => a.StaffId == staffId && a.IsActive);
        }

        /// <summary>
        /// Get development assignment for a player.
        /// </summary>
        public DevelopmentAssignment GetPlayerDevelopmentAssignment(string playerId)
        {
            return developmentAssignments.FirstOrDefault(d => d.PlayerId == playerId);
        }

        /// <summary>
        /// Get development bonus for a player from their assigned coach.
        /// </summary>
        public float GetPlayerDevelopmentBonus(string playerId)
        {
            var assignment = GetPlayerDevelopmentAssignment(playerId);
            return assignment?.DevelopmentBonus ?? 0f;
        }

        /// <summary>
        /// Check if a staff member is assigned to any task.
        /// </summary>
        public bool IsStaffAssigned(string staffId)
        {
            return activeAssignments.Any(a => a.StaffId == staffId && a.IsActive);
        }

        /// <summary>
        /// Get all scouts assigned to a specific task type.
        /// </summary>
        public List<StaffTaskAssignment> GetScoutsByTask(string teamId, ScoutTaskType taskType)
        {
            return activeAssignments.Where(a =>
                a.TeamId == teamId &&
                a.IsActive &&
                a.StaffPosition == StaffPositionType.Scout &&
                a.ScoutTask == taskType).ToList();
        }

        // ==================== FACADE: STAFF ACCESS ====================

        /// <summary>
        /// Gets all coaches for a team (facade for CoachingStaffManager).
        /// </summary>
        public List<Coach> GetCoachingStaff(string teamId)
        {
            return _coachingStaffManager?.GetCoachingStaff(teamId) ?? new List<Coach>();
        }

        /// <summary>
        /// Gets all scouts for a team (facade for ScoutingManager).
        /// </summary>
        public List<Scout> GetScouts(string teamId)
        {
            return _scoutingManager?.GetScouts(teamId) ?? new List<Scout>();
        }

        /// <summary>
        /// Gets a coach by ID.
        /// </summary>
        public Coach GetCoachById(string coachId)
        {
            return _coachingStaffManager?.GetCoachById(coachId);
        }

        /// <summary>
        /// Gets a scout by ID.
        /// </summary>
        public Scout GetScoutById(string scoutId)
        {
            return _scoutingManager?.GetScoutById(scoutId);
        }

        /// <summary>
        /// Gets the head coach for a team.
        /// </summary>
        public Coach GetHeadCoach(string teamId)
        {
            return _coachingStaffManager?.GetHeadCoach(teamId);
        }

        /// <summary>
        /// Gets all staff (coaches + scouts) for a team as a combined list.
        /// </summary>
        public List<object> GetAllStaff(string teamId)
        {
            var allStaff = new List<object>();
            allStaff.AddRange(GetCoachingStaff(teamId));
            allStaff.AddRange(GetScouts(teamId));
            return allStaff;
        }

        /// <summary>
        /// Gets total staff budget spent for a team.
        /// </summary>
        public int GetTotalStaffBudget(string teamId)
        {
            int coachSalaries = _coachingStaffManager?.GetTotalStaffSalary(teamId) ?? 0;
            int scoutSalaries = _scoutingManager?.GetScoutingBudget(teamId) ?? 0;
            return coachSalaries + scoutSalaries;
        }

        /// <summary>
        /// Gets free agent coaches available to hire.
        /// Uses StaffHiringManager's centralized pool.
        /// </summary>
        public List<Coach> GetFreeAgentCoaches()
        {
            return StaffHiringManager.Instance?.GetCoachesForPosition(StaffPositionType.AssistantCoach)
                   ?? new List<Coach>();
        }

        /// <summary>
        /// Gets free agent coaches for a specific position.
        /// Uses StaffHiringManager's centralized pool.
        /// </summary>
        public List<Coach> GetFreeAgentCoachesForPosition(StaffPositionType position)
        {
            return StaffHiringManager.Instance?.GetCoachesForPosition(position)
                   ?? new List<Coach>();
        }

        /// <summary>
        /// Gets free agent scouts available to hire.
        /// Uses StaffHiringManager's centralized pool.
        /// </summary>
        public List<Scout> GetFreeAgentScouts()
        {
            return StaffHiringManager.Instance?.GetAvailableScouts() ?? new List<Scout>();
        }

        // ==================== FACADE: HIRING/FIRING ====================

        /// <summary>
        /// Hires a coach for a team (facade for CoachingStaffManager).
        /// </summary>
        public (bool success, string message) HireCoach(string teamId, Coach coach)
        {
            var result = _coachingStaffManager?.HireCoach(teamId, coach) ?? (false, "CoachingStaffManager not initialized");
            if (result.success)
            {
                RecordHire(coach.CoachId, teamId, GetStaffPositionType(coach.Position));
            }
            return result;
        }

        /// <summary>
        /// Fires a coach from a team (facade for CoachingStaffManager).
        /// Fired coach is added to StaffHiringManager's free agent pool.
        /// </summary>
        public (bool success, string message) FireCoach(string teamId, string coachId)
        {
            var coach = GetCoachById(coachId);
            if (coach == null)
            {
                return (false, "Coach not found");
            }

            var position = coach.Position;
            var result = _coachingStaffManager?.FireCoach(teamId, coachId) ?? (false, "CoachingStaffManager not initialized");

            if (result.success)
            {
                // Add to StaffHiringManager's free agent pool
                StaffHiringManager.Instance?.AddFormerPlayerCoach(coach);

                RecordFiring(coachId, teamId, GetStaffPositionType(position));
            }
            return result;
        }

        /// <summary>
        /// Hires a scout for a team (facade for ScoutingManager).
        /// </summary>
        public (bool success, string message) HireScout(string teamId, Scout scout)
        {
            var result = _scoutingManager?.HireScout(teamId, scout) ?? (false, "ScoutingManager not initialized");
            if (result.success)
            {
                RecordHire(scout.ScoutId, teamId, StaffPositionType.Scout);
            }
            return result;
        }

        /// <summary>
        /// Fires a scout from a team (facade for ScoutingManager).
        /// Fired scout is added to StaffHiringManager's free agent pool.
        /// </summary>
        public (bool success, string message) FireScout(string teamId, string scoutId)
        {
            var scout = GetScoutById(scoutId);
            if (scout == null)
            {
                return (false, "Scout not found");
            }

            var result = _scoutingManager?.FireScout(teamId, scoutId) ?? (false, "ScoutingManager not initialized");

            if (result.success)
            {
                // Add to StaffHiringManager's free agent pool
                StaffHiringManager.Instance?.AddFormerPlayerScout(scout);

                RecordFiring(scoutId, teamId, StaffPositionType.Scout);
            }
            return result;
        }

        /// <summary>
        /// Converts CoachPosition to StaffPositionType.
        /// </summary>
        private StaffPositionType GetStaffPositionType(CoachPosition position)
        {
            return position switch
            {
                CoachPosition.HeadCoach => StaffPositionType.HeadCoach,
                CoachPosition.AssistantCoach => StaffPositionType.AssistantCoach,
                CoachPosition.OffensiveCoordinator => StaffPositionType.OffensiveCoordinator,
                CoachPosition.DefensiveCoordinator => StaffPositionType.DefensiveCoordinator,
                _ => StaffPositionType.AssistantCoach
            };
        }

        // ==================== DAILY PROCESSING ====================

        /// <summary>
        /// Process daily staff work (called by game day advancement).
        /// </summary>
        public void ProcessDailyStaffWork(string teamId, DateTime gameDate)
        {
            if (!autoProcessDailyWork) return;

            var teamAssignments = GetActiveAssignments(teamId);

            foreach (var assignment in teamAssignments)
            {
                assignment.AdvanceDay();

                // Check for completed scout assignments
                if (assignment.StaffPosition == StaffPositionType.Scout && assignment.IsComplete)
                {
                    CompleteScoutAssignment(assignment);
                }
            }

            // Update development assignment weeks
            foreach (var devAssignment in developmentAssignments.Where(d => d.TeamId == teamId))
            {
                // Increment weeks every 7 days
                devAssignment.WeeksAssigned++;
            }
        }

        /// <summary>
        /// Complete a scout assignment and generate report.
        /// </summary>
        private void CompleteScoutAssignment(StaffTaskAssignment assignment)
        {
            // This will integrate with StaffAIDecisionMaker to generate quality-affected reports
            Debug.Log($"[StaffManagement] Scout assignment completed: {assignment.ScoutTask}");

            // Mark as inactive
            assignment.IsActive = false;
            activeAssignments.Remove(assignment);
        }

        // ==================== END OF SEASON ====================

        /// <summary>
        /// Process end of season staff operations.
        /// </summary>
        public void ProcessEndOfSeason(int year)
        {
            Debug.Log($"[StaffManagement] Processing end of season {year}");

            // Clear temporary assignments
            activeAssignments.RemoveAll(a => !a.IsActive);

            // Reset development assignment weeks
            foreach (var devAssignment in developmentAssignments)
            {
                devAssignment.WeeksAssigned = 0;
            }
        }

        // ==================== HIRING/FIRING TRACKING ====================

        /// <summary>
        /// Record a staff hire.
        /// </summary>
        public void RecordHire(string staffId, string teamId, StaffPositionType position)
        {
            var config = GetOrCreateConfiguration(teamId);
            config.IncrementCount(position);
            OnStaffHired?.Invoke(staffId, teamId);
            Debug.Log($"[StaffManagement] Hired {position} for team {teamId}");
        }

        /// <summary>
        /// Record a staff firing.
        /// </summary>
        public void RecordFiring(string staffId, string teamId, StaffPositionType position)
        {
            var config = GetOrCreateConfiguration(teamId);
            config.DecrementCount(position);

            // Remove any active assignments
            UnassignStaff(staffId);

            OnStaffFired?.Invoke(staffId, teamId);
            Debug.Log($"[StaffManagement] Fired {position} from team {teamId}");
        }

        // ==================== SAVE/LOAD ====================

        /// <summary>
        /// Get save data for staff management.
        /// </summary>
        public StaffManagementSaveData GetSaveData()
        {
            return new StaffManagementSaveData
            {
                CurrentUserRole = currentUserRole,
                UserTeamId = userTeamId,
                TeamConfigurations = new List<TeamStaffConfiguration>(teamConfigurations),
                Assignments = new StaffAssignmentSaveData
                {
                    ActiveAssignments = new List<StaffTaskAssignment>(activeAssignments),
                    DevelopmentAssignments = new List<DevelopmentAssignment>(developmentAssignments)
                },
                CoachingData = _coachingStaffManager?.CreateSaveData(),
                ScoutingData = _scoutingManager?.CreateSaveData()
            };
        }

        /// <summary>
        /// Load save data for staff management.
        /// </summary>
        public void LoadSaveData(StaffManagementSaveData data)
        {
            if (data == null) return;

            currentUserRole = data.CurrentUserRole;
            userTeamId = data.UserTeamId;
            teamConfigurations = data.TeamConfigurations ?? new List<TeamStaffConfiguration>();

            if (data.Assignments != null)
            {
                activeAssignments = data.Assignments.ActiveAssignments ?? new List<StaffTaskAssignment>();
                developmentAssignments = data.Assignments.DevelopmentAssignments ?? new List<DevelopmentAssignment>();
            }

            // Load coaching staff data
            if (data.CoachingData != null)
            {
                _coachingStaffManager?.LoadSaveData(data.CoachingData);
            }

            // Load scouting data
            if (data.ScoutingData != null)
            {
                _scoutingManager?.LoadSaveData(data.ScoutingData);
            }

            Debug.Log($"[StaffManagement] Loaded save data: Role={currentUserRole}, Team={userTeamId}");
        }
    }

    /// <summary>
    /// Save data for staff management system.
    /// </summary>
    [Serializable]
    public class StaffManagementSaveData
    {
        public UserRole CurrentUserRole;
        public string UserTeamId;
        public List<TeamStaffConfiguration> TeamConfigurations;
        public StaffAssignmentSaveData Assignments;

        // Wrapped manager save data
        public CoachingStaffSaveData CoachingData;
        public ScoutingSaveData ScoutingData;
    }
}
