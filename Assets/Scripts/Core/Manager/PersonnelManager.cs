using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Util;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Unified manager for all personnel (coaches, scouts, front office).
    /// Replaces CoachingStaffManager, ScoutingManager, and UnifiedCareerManager.
    /// This is the SINGLE source of truth for all career profiles and team assignments.
    /// </summary>
    public class PersonnelManager : MonoBehaviour
    {
        public static PersonnelManager Instance { get; private set; }

        // Limits
        public const int MAX_SCOUTS_PER_TEAM = 5;
        public const int MAX_ASSISTANT_COACHES = 3;
        public const int MAX_TOTAL_STAFF = 12;

        public const int BASE_COACH_POOL_SIZE = 30;
        public const int BASE_SCOUT_POOL_SIZE = 15;

        // ==================== DATABASE ====================
        [Header("Personnel Database")]
        [SerializeField] private List<UnifiedCareerProfile> allProfiles = new List<UnifiedCareerProfile>();
        
        // Caches for quick lookups
        private Dictionary<string, UnifiedCareerProfile> _profilesById = new();
        private Dictionary<string, List<UnifiedCareerProfile>> _teamStaffs = new();
        private List<UnifiedCareerProfile> _unemployedPool = new();

        // Assignments & Negotiations
        [SerializeField] private List<StaffTaskAssignment> _activeAssignments = new List<StaffTaskAssignment>();
        [SerializeField] private List<DevelopmentAssignment> _developmentAssignments = new List<DevelopmentAssignment>();
        [SerializeField] private List<StaffNegotiationSession> _activeNegotiations = new List<StaffNegotiationSession>();
        private Dictionary<string, List<ScoutingReport>> _teamReports = new();

        // ==================== EVENTS ====================
        public event Action<UnifiedCareerProfile, string, UnifiedRole> OnPersonnelHired;
        public event Action<UnifiedCareerProfile, string> OnPersonnelFired;
        public event Action<UnifiedCareerProfile, UnifiedRole, UnifiedRole> OnCareerTransition;
        public event Action<UnifiedCareerProfile> OnPersonnelRetired;
        public event Action<StaffTaskAssignment> OnStaffAssigned;
        public event Action<StaffTaskAssignment> OnStaffUnassigned;
        public event Action<StaffNegotiationSession> OnNegotiationStarted;
        public event Action<StaffNegotiationSession> OnNegotiationComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Initialize(UnifiedCareerSaveData saveData = null)
        {
            if (saveData != null)
            {
                allProfiles = saveData.AllProfiles ?? new List<UnifiedCareerProfile>();
            }

            RebuildCaches();
            Debug.Log($"[PersonnelManager] Initialized with {allProfiles.Count} profiles.");
        }

        private void RebuildCaches()
        {
            _profilesById.Clear();
            _teamStaffs.Clear();
            _unemployedPool.Clear();

            foreach (var profile in allProfiles)
            {
                _profilesById[profile.ProfileId] = profile;

                if (profile.CurrentTrack == UnifiedCareerTrack.Retired)
                    continue;

                if (profile.IsEmployed())
                {
                    if (!_teamStaffs.ContainsKey(profile.CurrentTeamId))
                        _teamStaffs[profile.CurrentTeamId] = new List<UnifiedCareerProfile>();
                    
                    _teamStaffs[profile.CurrentTeamId].Add(profile);
                }
                else
                {
                    _unemployedPool.Add(profile);
                }
            }
        }

        // ==================== QUERIES ====================

        public UnifiedCareerProfile GetProfile(string profileId)
        {
            _profilesById.TryGetValue(profileId, out var profile);
            return profile;
        }

        public List<UnifiedCareerProfile> GetTeamStaff(string teamId)
        {
            return _teamStaffs.TryGetValue(teamId, out var staff) ? new List<UnifiedCareerProfile>(staff) : new List<UnifiedCareerProfile>();
        }

        public UnifiedCareerProfile GetHeadCoach(string teamId)
        {
            return GetTeamStaff(teamId).FirstOrDefault(p => p.CurrentRole == UnifiedRole.HeadCoach);
        }

        public UnifiedCareerProfile GetGM(string teamId)
        {
            return GetTeamStaff(teamId).FirstOrDefault(p => p.CurrentRole == UnifiedRole.GeneralManager);
        }

        public List<UnifiedCareerProfile> GetScouts(string teamId)
        {
            return GetTeamStaff(teamId).Where(p => p.CurrentRole == UnifiedRole.Scout).ToList();
        }

        public List<UnifiedCareerProfile> GetUnemployedPool()
        {
            return new List<UnifiedCareerProfile>(_unemployedPool);
        }

        // ==================== OPERATIONS ====================

        public (bool success, string message) HirePersonnel(string profileId, string teamId, string teamName, UnifiedRole role, int salary = -1, int years = -1)
        {
            var profile = GetProfile(profileId);
            if (profile == null) return (false, "Profile not found.");

            if (profile.IsEmployed())
                return (false, "Person is already employed by another team.");

            // Check staff limits
            var teamStaff = GetTeamStaff(teamId);
            if (teamStaff.Count >= MAX_TOTAL_STAFF)
                return (false, "Total staff limit reached for this team.");

            // Position occupancy check
            if (role == UnifiedRole.HeadCoach && GetHeadCoach(teamId) != null)
                return (false, "Team already has a Head Coach.");
            
            if (role == UnifiedRole.GeneralManager && GetGM(teamId) != null)
                return (false, "Team already has a General Manager.");

            if (role == UnifiedRole.Scout && GetScouts(teamId).Count >= MAX_SCOUTS_PER_TEAM)
                return (false, "Maximum scouts reached.");

            // Update profile
            profile.HandleHired(GetCurrentYear(), teamId, teamName, role);
            if (salary > 0) profile.AnnualSalary = salary;
            if (years > 0) profile.ContractYearsRemaining = years;
            profile.ContractStartDate = DateTime.Now;

            // Update caches
            _unemployedPool.Remove(profile);
            if (!_teamStaffs.ContainsKey(teamId)) _teamStaffs[teamId] = new List<UnifiedCareerProfile>();
            _teamStaffs[teamId].Add(profile);

            OnPersonnelHired?.Invoke(profile, teamId, role);
            Debug.Log($"[PersonnelManager] {profile.PersonName} hired as {role} for {teamName}");
            return (true, "Successfully hired.");
        }

        public (bool success, string message) FirePersonnel(string profileId, string reason)
        {
            var profile = GetProfile(profileId);
            if (profile == null) return (false, "Profile not found.");
            if (!profile.IsEmployed()) return (false, "Person is not currently employed.");

            string teamId = profile.CurrentTeamId;
            
            // Handle buyout (simplified logic from Coach.cs)
            int buyout = profile.AnnualSalary * profile.ContractYearsRemaining;

            // Unassign tasks
            UnassignStaff(profileId);

            profile.HandleFired(GetCurrentYear(), reason);

            // Update caches
            if (_teamStaffs.ContainsKey(teamId))
                _teamStaffs[teamId].Remove(profile);
            _unemployedPool.Add(profile);

            OnPersonnelFired?.Invoke(profile, reason);
            Debug.Log($"[PersonnelManager] {profile.PersonName} fired from {teamId}. Reason: {reason}");
            return (true, $"Successfully fired. Buyout tracking: ${buyout:N0}");
        }

        // ==================== ASSIGNMENTS ====================

        public StaffTaskAssignment AssignScoutToTask(string scoutId, string teamId, ScoutTaskType taskType, List<string> targetPlayerIds = null, string targetTeamId = null)
        {
            UnassignStaff(scoutId);

            var assignment = StaffTaskAssignment.CreateScoutAssignment(scoutId, teamId, taskType, targetPlayerIds, targetTeamId);
            _activeAssignments.Add(assignment);
            
            OnStaffAssigned?.Invoke(assignment);
            return assignment;
        }

        public (bool success, string message) AssignCoachToPlayers(string coachId, string teamId, List<string> playerIds)
        {
            var profile = GetProfile(coachId);
            if (profile == null) return (false, "Coach not found");

            int capacity = DevelopmentAssignment.CalculateCoachCapacity(profile.PlayerDevelopment);
            if (playerIds.Count > capacity)
                return (false, $"Coach can only handle {capacity} players.");

            UnassignStaff(coachId);

            var posType = profile.CurrentRole.ToStaffPositionType();
            var assignment = StaffTaskAssignment.CreateCoachAssignment(coachId, teamId, posType, CoachTaskType.PlayerDevelopment, playerIds);
            _activeAssignments.Add(assignment);

            foreach (var playerId in playerIds)
            {
                var dev = new DevelopmentAssignment
                {
                    AssignmentId = $"DEV_{Guid.NewGuid().ToString().Substring(0, 8)}",
                    CoachId = coachId,
                    CoachName = profile.PersonName,
                    PlayerId = playerId,
                    TeamId = teamId,
                    CoachDevelopmentRating = profile.PlayerDevelopment,
                    DevelopmentBonus = DevelopmentAssignment.CalculateDevelopmentBonus(profile.PlayerDevelopment, false),
                    StartDate = DateTime.Now,
                    WeeksAssigned = 0
                };
                _developmentAssignments.Add(dev);
            }

            OnStaffAssigned?.Invoke(assignment);
            return (true, "Assigned successfully");
        }

        public void UnassignStaff(string staffId)
        {
            var assignment = _activeAssignments.FirstOrDefault(a => a.StaffId == staffId && a.IsActive);
            if (assignment != null)
            {
                assignment.IsActive = false;
                _activeAssignments.Remove(assignment);
                _developmentAssignments.RemoveAll(d => d.CoachId == staffId);
                OnStaffUnassigned?.Invoke(assignment);
            }
        }

        public List<StaffTaskAssignment> GetActiveAssignments(string teamId)
        {
            return _activeAssignments.Where(a => a.TeamId == teamId && a.IsActive).ToList();
        }

        // ==================== NEGOTIATIONS ====================

        public StaffNegotiationSession StartNegotiation(string teamId, string profileId)
        {
            if (_activeNegotiations.Any(n => n.CandidateId == profileId && n.Status == StaffNegotiationStatus.InProgress))
                return _activeNegotiations.First(n => n.CandidateId == profileId);

            var profile = GetProfile(profileId);
            if (profile == null) return null;

            var session = CreateNegotiationSession(teamId, profile);
            _activeNegotiations.Add(session);
            OnNegotiationStarted?.Invoke(session);
            return session;
        }

        private StaffNegotiationSession CreateNegotiationSession(string teamId, UnifiedCareerProfile profile)
        {
            var rng = new System.Random();
            int askingPrice = (int)(profile.MarketValue * (1.1f + rng.NextDouble() * 0.2f));
            int minAcceptable = (int)(profile.MarketValue * 0.85f);

            return new StaffNegotiationSession
            {
                NegotiationId = $"NEG_{Guid.NewGuid().ToString().Substring(0, 8)}",
                TeamId = teamId,
                CandidateId = profile.ProfileId,
                CandidateName = profile.PersonName,
                IsCoach = profile.CurrentRole != UnifiedRole.Scout,
                Position = profile.CurrentRole.ToStaffPositionType(),
                Status = StaffNegotiationStatus.InProgress,
                RoundNumber = 1,
                MaxRounds = 3 + rng.Next(3),
                AskingPrice = askingPrice,
                MinimumAcceptable = minAcceptable,
                PreferredYears = 2 + rng.Next(3)
            };
        }

        public NegotiationResponse MakeOffer(string negotiationId, int annualSalary, int years)
        {
            var session = _activeNegotiations.FirstOrDefault(n => n.NegotiationId == negotiationId);
            if (session == null) return new NegotiationResponse { Success = false, Message = "Session not found" };

            var offer = new StaffContractOffer { AnnualSalary = annualSalary, Years = years };
            var response = session.ProcessOffer(offer);

            if (response.NewStatus == StaffNegotiationStatus.Accepted)
            {
                FinalizeNegotiation(session);
            }
            else if (response.NewStatus == StaffNegotiationStatus.Rejected || response.NewStatus == StaffNegotiationStatus.WalkedAway)
            {
                _activeNegotiations.Remove(session);
            }

            return response;
        }

        public (bool success, string message) AcceptCounterOffer(string negotiationId)
        {
            var session = _activeNegotiations.FirstOrDefault(n => n.NegotiationId == negotiationId);
            if (session == null || session.Status != StaffNegotiationStatus.CounterReceived)
            {
                return (false, "No counter-offer to accept");
            }

            session.Status = StaffNegotiationStatus.Accepted;
            session.FinalSalary = session.CounterSalary;
            session.FinalYears = session.CounterYears;
            
            FinalizeNegotiation(session);
            return (true, "Counter-offer accepted.");
        }

        public void CancelNegotiation(string negotiationId)
        {
            var session = _activeNegotiations.FirstOrDefault(n => n.NegotiationId == negotiationId);
            if (session != null)
            {
                session.Status = StaffNegotiationStatus.WalkedAway;
                _activeNegotiations.Remove(session);
                Debug.Log($"[PersonnelManager] Negotiation with {session.CandidateName} cancelled");
            }
        }

        private void FinalizeNegotiation(StaffNegotiationSession session)
        {
            var teamName = GameManager.Instance.GetTeam(session.TeamId)?.Name ?? "Unknown";
            HirePersonnel(session.CandidateId, session.TeamId, teamName, session.Position.ToUnifiedRole(), session.FinalSalary, session.FinalYears);
            
            session.Status = StaffNegotiationStatus.Completed;
            OnNegotiationComplete?.Invoke(session);
            _activeNegotiations.Remove(session);
        }

        // ==================== STAFF QUALITY & SIMULATION ====================

        public int GetStaffQuality(string teamId)
        {
            var staff = GetTeamStaff(teamId);
            if (staff.Count == 0) return 0;

            float totalQuality = 0f;
            float totalWeight = 0f;

            foreach (var p in staff)
            {
                float weight = (p.CurrentRole == UnifiedRole.HeadCoach || p.CurrentRole == UnifiedRole.GeneralManager) ? 3f : 1f;
                totalQuality += p.OverallRating * weight;
                totalWeight += weight;
            }

            return Mathf.RoundToInt(totalQuality / totalWeight);
        }

        public float GetDevelopmentQuality(string teamId)
        {
            var staff = GetTeamStaff(teamId);
            if (staff.Count == 0) return 0.5f;

            float totalQuality = 0f;
            float totalWeight = 0f;

            foreach (var p in staff)
            {
                float weight = p.CurrentRole switch
                {
                    UnifiedRole.AssistantCoach => 2f, // Specific development roles?
                    UnifiedRole.HeadCoach => 1.5f,
                    _ => 0.5f
                };

                totalQuality += p.PlayerDevelopment / 100f * weight;
                totalWeight += weight;
            }

            return Mathf.Clamp01(totalQuality / totalWeight);
        }

        public float GetOffensiveQuality(string teamId)
        {
            var staff = GetTeamStaff(teamId);
            var hc = GetHeadCoach(teamId);
            var oc = staff.FirstOrDefault(p => p.CurrentRole == UnifiedRole.Coordinator && p.Specializations.Contains(CoachSpecialization.OffensiveSchemes));

            float quality = 0f;
            float weight = 0f;

            if (hc != null) { quality += hc.OffensiveScheme / 100f * 2f; weight += 2f; }
            if (oc != null) { quality += oc.OffensiveScheme / 100f * 3f; weight += 3f; }

            return weight > 0 ? quality / weight : 0.5f;
        }

        public float GetDefensiveQuality(string teamId)
        {
            var staff = GetTeamStaff(teamId);
            var hc = GetHeadCoach(teamId);
            var dc = staff.FirstOrDefault(p => p.CurrentRole == UnifiedRole.Coordinator && p.Specializations.Contains(CoachSpecialization.DefensiveSchemes));

            float quality = 0f;
            float weight = 0f;

            if (hc != null) { quality += hc.DefensiveScheme / 100f * 2f; weight += 2f; }
            if (dc != null) { quality += dc.DefensiveScheme / 100f * 3f; weight += 3f; }

            return weight > 0 ? quality / weight : 0.5f;
        }

        // ==================== SCOUTING OPERATIONS ====================

        public (bool success, string message) AssignScout(string scoutId, string playerId, int duration = 7)
        {
            var scout = GetProfile(scoutId);
            if (scout == null || scout.CurrentRole != UnifiedRole.Scout) return (false, "Invalid scout.");
            if (_activeAssignments.Any(a => a.StaffId == scoutId)) return (false, "Scout is already on assignment.");

            AssignScoutToTask(scoutId, scout.CurrentTeamId, ScoutTaskType.DraftProspectScouting, new List<string> { playerId });
            return (true, "Assigned.");
        }

        // ==================== HELPERS ====================

        private int GetCurrentYear()
        {
            return GameManager.Instance != null ? GameManager.Instance.CurrentSeason : DateTime.Now.Year;
        }

        public void RegisterProfile(UnifiedCareerProfile profile)
        {
            if (_profilesById.ContainsKey(profile.ProfileId)) return;
            allProfiles.Add(profile);
            _profilesById[profile.ProfileId] = profile;
            if (!profile.IsEmployed() && profile.CurrentTrack != UnifiedCareerTrack.Retired)
                _unemployedPool.Add(profile);
        }

        // ==================== POOL GENERATION ====================

        public void GenerateFreeAgentPool(int coachCount = BASE_COACH_POOL_SIZE, int scoutCount = BASE_SCOUT_POOL_SIZE)
        {
            _unemployedPool.Clear();
            var rng = new System.Random();

            // Generate coaches
            var coachRoles = new[] { UnifiedRole.HeadCoach, UnifiedRole.AssistantCoach, UnifiedRole.OffensiveCoordinator, UnifiedRole.DefensiveCoordinator };
            for (int i = 0; i < coachCount; i++)
            {
                var role = coachRoles[rng.Next(coachRoles.Length)];
                var profile = CreateRandomProfile(role, rng);
                RegisterProfile(profile);
            }

            // Generate scouts
            for (int i = 0; i < scoutCount; i++)
            {
                var profile = CreateRandomProfile(UnifiedRole.Scout, rng);
                RegisterProfile(profile);
            }

            _unemployedPool = _unemployedPool.OrderByDescending(p => p.OverallRating).ToList();
            Debug.Log($"[PersonnelManager] Generated FA pool: {_unemployedPool.Count} members");
        }

        private UnifiedCareerProfile CreateRandomProfile(UnifiedRole role, System.Random rng)
        {
            int age = 30 + rng.Next(40);
            var profile = new UnifiedCareerProfile
            {
                ProfileId = Guid.NewGuid().ToString(),
                PersonName = NameGenerator.GenerateStaffName().FullName,
                CurrentAge = age,
                BirthYear = GetCurrentYear() - age,
                CurrentTrack = UnifiedRoleExtensions.IsCoachingRole(role) ? UnifiedCareerTrack.Coaching : UnifiedCareerTrack.FrontOffice,
                CurrentRole = role,
                YearEnteredProfession = GetCurrentYear(),
                
                // Randomize attributes
                OffensiveScheme = 40 + rng.Next(50),
                DefensiveScheme = 40 + rng.Next(50),
                PlayerDevelopment = 40 + rng.Next(50),
                GameManagement = 40 + rng.Next(50),
                ScoutingAbility = 40 + rng.Next(50),
                TalentEvaluation = 40 + rng.Next(50)
            };

            return profile;
        }

        // ==================== PROCESSING ====================

        public void ProcessDailyWork(string teamId, DateTime date)
        {
            var assignments = GetActiveAssignments(teamId);
            foreach (var assignment in assignments)
            {
                assignment.AdvanceDay();
                if (assignment.IsComplete)
                {
                    // Handle completion logic (e.g. generate report)
                    UnassignStaff(assignment.StaffId);
                }
            }
        }

        public void ProcessEndOfSeason(int year)
        {
            foreach (var profile in allProfiles)
            {
                profile.ProcessEndOfSeason();
            }

            // Cleanup negotiations
            _activeNegotiations.Clear();
            
            // Advance year in database
            // saveData.LastProcessedYear = year;
        }

        // ==================== SAVE/LOAD ====================

        public UnifiedCareerSaveData GetSaveData()
        {
            return new UnifiedCareerSaveData
            {
                AllProfiles = new List<UnifiedCareerProfile>(allProfiles),
                LastProcessedYear = GetCurrentYear()
            };
        }

        public void LoadSaveData(UnifiedCareerSaveData data)
        {
            if (data == null) return;

            allProfiles.Clear();
            _teamsStaff.Clear();
            _unemployedPool.Clear();
            _profileCache.Clear();

            foreach (var profile in data.AllProfiles)
            {
                RegisterProfile(profile);
            }

            Debug.Log($"[PersonnelManager] Loaded {allProfiles.Count} profiles from save data.");
        }
    }
}
