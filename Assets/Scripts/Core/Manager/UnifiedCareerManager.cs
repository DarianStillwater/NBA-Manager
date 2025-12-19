using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Central manager that coordinates career transitions between coaching and front office tracks,
    /// handles retirements, and maintains the unified career profile database.
    /// </summary>
    public class UnifiedCareerManager : MonoBehaviour
    {
        public static UnifiedCareerManager Instance { get; private set; }

        // ==================== PROFILES ====================
        [Header("Career Profiles")]
        [SerializeField] private List<UnifiedCareerProfile> allProfiles = new List<UnifiedCareerProfile>();
        [SerializeField] private List<UnifiedCareerProfile> unemployedPool = new List<UnifiedCareerProfile>();
        [SerializeField] private List<UnifiedCareerProfile> retiredProfiles = new List<UnifiedCareerProfile>();

        // ==================== OPEN POSITIONS ====================
        [Header("Open Positions")]
        [SerializeField] private List<string> teamsNeedingHeadCoach = new List<string>();
        [SerializeField] private List<string> teamsNeedingGM = new List<string>();
        [SerializeField] private List<string> teamsNeedingCoordinator = new List<string>();
        [SerializeField] private List<string> teamsNeedingAssistantGM = new List<string>();

        // ==================== SETTINGS ====================
        [Header("Retirement Settings")]
        [SerializeField] private NonPlayerRetirementSettings retirementSettings;

        // ==================== EVENTS ====================
        public event Action<UnifiedCareerProfile, UnifiedRole, UnifiedRole> OnCareerTransition;
        public event Action<UnifiedCareerProfile> OnTrackSwitch;
        public event Action<UnifiedCareerProfile> OnPersonRetired;
        public event Action<UnifiedCareerProfile, string> OnPersonFired;
        public event Action<UnifiedCareerProfile, string, UnifiedRole> OnPersonHired;
        public event Action<NonPlayerRetirementAnnouncement> OnRetirementAnnounced;

        // ==================== INITIALIZATION ====================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (retirementSettings == null)
            {
                retirementSettings = NonPlayerRetirementSettings.Default;
            }
        }

        /// <summary>
        /// Initialize the manager with existing data
        /// </summary>
        public void Initialize(UnifiedCareerSaveData saveData = null)
        {
            if (saveData != null)
            {
                allProfiles = saveData.AllProfiles ?? new List<UnifiedCareerProfile>();

                // Rebuild unemployed and retired lists
                unemployedPool = allProfiles.Where(p => p.CurrentTrack == UnifiedCareerTrack.Unemployed).ToList();
                retiredProfiles = allProfiles.Where(p => p.CurrentTrack == UnifiedCareerTrack.Retired).ToList();
            }

            Debug.Log($"[UnifiedCareerManager] Initialized with {allProfiles.Count} profiles " +
                     $"({unemployedPool.Count} unemployed, {retiredProfiles.Count} retired)");
        }

        // ==================== PROFILE MANAGEMENT ====================

        /// <summary>
        /// Register a new unified career profile
        /// </summary>
        public void RegisterProfile(UnifiedCareerProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.ProfileId))
            {
                Debug.LogError("[UnifiedCareerManager] Cannot register null or invalid profile");
                return;
            }

            // Check for duplicates
            var existing = allProfiles.Find(p => p.ProfileId == profile.ProfileId);
            if (existing != null)
            {
                Debug.LogWarning($"[UnifiedCareerManager] Profile already exists: {profile.PersonName}");
                return;
            }

            allProfiles.Add(profile);

            // Add to appropriate list
            if (profile.CurrentTrack == UnifiedCareerTrack.Unemployed)
            {
                unemployedPool.Add(profile);
            }

            Debug.Log($"[UnifiedCareerManager] Registered profile: {profile.PersonName} ({profile.CurrentRole})");
        }

        /// <summary>
        /// Get a profile by ID
        /// </summary>
        public UnifiedCareerProfile GetProfile(string profileId)
        {
            return allProfiles.Find(p => p.ProfileId == profileId);
        }

        /// <summary>
        /// Get a profile by former player ID
        /// </summary>
        public UnifiedCareerProfile GetProfileByFormerPlayerId(string formerPlayerId)
        {
            return allProfiles.Find(p => p.FormerPlayerId == formerPlayerId);
        }

        /// <summary>
        /// Get all profiles currently at a specific role
        /// </summary>
        public List<UnifiedCareerProfile> GetProfilesByRole(UnifiedRole role)
        {
            return allProfiles.Where(p => p.CurrentRole == role).ToList();
        }

        /// <summary>
        /// Get all profiles for a specific team
        /// </summary>
        public List<UnifiedCareerProfile> GetProfilesByTeam(string teamId)
        {
            return allProfiles.Where(p => p.CurrentTeamId == teamId).ToList();
        }

        // ==================== CAREER TRANSITIONS ====================

        /// <summary>
        /// Attempt a career transition for a profile
        /// </summary>
        public (bool success, string message) AttemptTransition(
            UnifiedCareerProfile profile,
            UnifiedRole targetRole,
            string targetTeamId = null,
            string targetTeamName = null)
        {
            if (profile == null)
                return (false, "Invalid profile");

            // Get transition requirements
            var (isValid, requirement) = CareerTransitionRules.IsValidTransition(profile.CurrentRole, targetRole);

            if (!isValid)
                return (false, $"No valid transition from {profile.CurrentRole} to {targetRole}");

            // Check eligibility
            var (eligible, reason) = requirement.CheckEligibility(profile);
            if (!eligible)
                return (false, reason);

            // Check open position if required
            if (requirement.RequiresOpenPosition)
            {
                bool hasOpenPosition = CheckOpenPosition(targetRole, targetTeamId);
                if (!hasOpenPosition)
                    return (false, $"No open {targetRole} position available");
            }

            // Process the transition
            return ExecuteTransition(profile, targetRole, requirement, targetTeamId, targetTeamName);
        }

        /// <summary>
        /// Execute a validated career transition
        /// </summary>
        private (bool success, string message) ExecuteTransition(
            UnifiedCareerProfile profile,
            UnifiedRole targetRole,
            TransitionRequirement requirement,
            string targetTeamId,
            string targetTeamName)
        {
            var previousRole = profile.CurrentRole;
            var previousTrack = profile.CurrentTrack;
            int currentYear = GetCurrentYear();

            // Handle based on transition type
            switch (requirement.Type)
            {
                case TransitionType.Promotion:
                    profile.HandlePromotion(currentYear, targetRole);
                    break;

                case TransitionType.TrackSwitch:
                case TransitionType.Bridge:
                    // Must resign first to switch tracks
                    if (profile.IsEmployed())
                    {
                        profile.HandleResignation(currentYear, $"Pursuing {targetRole} opportunity");
                    }
                    profile.HandleHired(currentYear, targetTeamId, targetTeamName, targetRole);
                    profile.TrackSwitches++;
                    OnTrackSwitch?.Invoke(profile);
                    break;

                case TransitionType.Hiring:
                    profile.HandleHired(currentYear, targetTeamId, targetTeamName, targetRole);
                    // Remove from unemployed pool
                    unemployedPool.Remove(profile);
                    break;

                default:
                    profile.HandleHired(currentYear, targetTeamId, targetTeamName, targetRole);
                    break;
            }

            // Claim the position
            if (requirement.RequiresOpenPosition)
            {
                ClaimPosition(targetRole, targetTeamId);
            }

            // Fire events
            OnCareerTransition?.Invoke(profile, previousRole, targetRole);

            if (requirement.Type == TransitionType.Hiring)
            {
                OnPersonHired?.Invoke(profile, targetTeamId, targetRole);
            }

            Debug.Log($"[UnifiedCareerManager] {profile.PersonName}: {previousRole} → {targetRole}");

            return (true, $"Successfully transitioned to {targetRole}");
        }

        /// <summary>
        /// Process a resignation
        /// </summary>
        public void ProcessResignation(UnifiedCareerProfile profile, string reason)
        {
            if (profile == null || !profile.IsEmployed())
                return;

            int currentYear = GetCurrentYear();
            var previousRole = profile.CurrentRole;
            var previousTeamId = profile.CurrentTeamId;

            profile.HandleResignation(currentYear, reason);

            // Add to unemployed pool
            if (!unemployedPool.Contains(profile))
            {
                unemployedPool.Add(profile);
            }

            // Open up the position
            OpenPosition(previousRole, previousTeamId);

            Debug.Log($"[UnifiedCareerManager] {profile.PersonName} resigned: {reason}");
        }

        /// <summary>
        /// Fire a person from their position
        /// </summary>
        public void FirePerson(UnifiedCareerProfile profile, string reason)
        {
            if (profile == null || !profile.IsEmployed())
                return;

            int currentYear = GetCurrentYear();
            var previousRole = profile.CurrentRole;
            var previousTeamId = profile.CurrentTeamId;

            profile.HandleFired(currentYear, reason);

            // Add to unemployed pool
            if (!unemployedPool.Contains(profile))
            {
                unemployedPool.Add(profile);
            }

            // Open up the position
            OpenPosition(previousRole, previousTeamId);

            OnPersonFired?.Invoke(profile, reason);

            Debug.Log($"[UnifiedCareerManager] {profile.PersonName} fired: {reason}");
        }

        /// <summary>
        /// Hire a person for a position
        /// </summary>
        public (bool success, string message) HirePerson(
            UnifiedCareerProfile profile,
            string teamId,
            string teamName,
            UnifiedRole role)
        {
            if (profile == null)
                return (false, "Invalid profile");

            if (!CheckOpenPosition(role, teamId))
                return (false, $"No open {role} position at team");

            // Check if eligible for this role from current state
            var result = AttemptTransition(profile, role, teamId, teamName);

            return result;
        }

        // ==================== RETIREMENT PROCESSING ====================

        /// <summary>
        /// Evaluate retirement for a profile
        /// </summary>
        public NonPlayerRetirementFactors EvaluateRetirement(UnifiedCareerProfile profile)
        {
            if (profile == null || profile.CurrentTrack == UnifiedCareerTrack.Retired)
                return null;

            return NonPlayerRetirementFactors.CalculateFor(profile);
        }

        /// <summary>
        /// Process potential retirement for a profile
        /// Returns true if the person retired
        /// </summary>
        public bool ProcessRetirement(UnifiedCareerProfile profile)
        {
            if (profile == null || profile.CurrentTrack == UnifiedCareerTrack.Retired)
                return false;

            var factors = EvaluateRetirement(profile);

            // Forced retirement check
            if (factors.WouldForceRetirement)
            {
                ExecuteRetirement(profile, factors.PrimaryReason, true);
                return true;
            }

            // Age-based forced retirement
            if (profile.CurrentAge >= retirementSettings.ForcedRetirementAge)
            {
                ExecuteRetirement(profile, NonPlayerRetirementReason.Age, true);
                return true;
            }

            // Voluntary retirement roll
            if (profile.CurrentAge >= retirementSettings.RetirementAgeStart)
            {
                float roll = UnityEngine.Random.value;
                if (roll < factors.FinalRetirementChance)
                {
                    ExecuteRetirement(profile, factors.PrimaryReason, false);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Execute retirement for a profile
        /// </summary>
        private void ExecuteRetirement(UnifiedCareerProfile profile, NonPlayerRetirementReason reason, bool wasForced)
        {
            int currentYear = GetCurrentYear();

            // If employed, need to vacate position
            if (profile.IsEmployed())
            {
                OpenPosition(profile.CurrentRole, profile.CurrentTeamId);
            }

            // Generate announcement before updating profile
            var announcement = NonPlayerRetirementAnnouncement.Generate(profile, currentYear, reason, wasForced);

            // Update profile
            profile.HandleRetirement(currentYear, reason.ToString());

            // Remove from unemployed if there
            unemployedPool.Remove(profile);

            // Add to retired list
            if (!retiredProfiles.Contains(profile))
            {
                retiredProfiles.Add(profile);
            }

            // Fire events
            OnPersonRetired?.Invoke(profile);
            OnRetirementAnnounced?.Invoke(announcement);

            Debug.Log($"[UnifiedCareerManager] {profile.PersonName} retired ({reason}, forced={wasForced})");
        }

        /// <summary>
        /// Force retirement for a profile
        /// </summary>
        public void ForceRetirement(UnifiedCareerProfile profile, NonPlayerRetirementReason reason)
        {
            if (profile == null || profile.CurrentTrack == UnifiedCareerTrack.Retired)
                return;

            ExecuteRetirement(profile, reason, true);
        }

        // ==================== POSITION MANAGEMENT ====================

        /// <summary>
        /// Mark a position as open
        /// </summary>
        public void OpenPosition(UnifiedRole role, string teamId)
        {
            if (string.IsNullOrEmpty(teamId))
                return;

            switch (role)
            {
                case UnifiedRole.HeadCoach:
                    if (!teamsNeedingHeadCoach.Contains(teamId))
                        teamsNeedingHeadCoach.Add(teamId);
                    break;
                case UnifiedRole.GeneralManager:
                    if (!teamsNeedingGM.Contains(teamId))
                        teamsNeedingGM.Add(teamId);
                    break;
                case UnifiedRole.Coordinator:
                    if (!teamsNeedingCoordinator.Contains(teamId))
                        teamsNeedingCoordinator.Add(teamId);
                    break;
                case UnifiedRole.AssistantGM:
                    if (!teamsNeedingAssistantGM.Contains(teamId))
                        teamsNeedingAssistantGM.Add(teamId);
                    break;
            }
        }

        /// <summary>
        /// Check if a position is open
        /// </summary>
        public bool CheckOpenPosition(UnifiedRole role, string teamId = null)
        {
            // If no specific team, check if any position is open
            if (string.IsNullOrEmpty(teamId))
            {
                return role switch
                {
                    UnifiedRole.HeadCoach => teamsNeedingHeadCoach.Count > 0,
                    UnifiedRole.GeneralManager => teamsNeedingGM.Count > 0,
                    UnifiedRole.Coordinator => teamsNeedingCoordinator.Count > 0,
                    UnifiedRole.AssistantGM => teamsNeedingAssistantGM.Count > 0,
                    // Lower positions always have openings
                    UnifiedRole.AssistantCoach => true,
                    UnifiedRole.PositionCoach => true,
                    UnifiedRole.Scout => true,
                    _ => false
                };
            }

            return role switch
            {
                UnifiedRole.HeadCoach => teamsNeedingHeadCoach.Contains(teamId),
                UnifiedRole.GeneralManager => teamsNeedingGM.Contains(teamId),
                UnifiedRole.Coordinator => teamsNeedingCoordinator.Contains(teamId),
                UnifiedRole.AssistantGM => teamsNeedingAssistantGM.Contains(teamId),
                // Lower positions always have openings
                UnifiedRole.AssistantCoach => true,
                UnifiedRole.PositionCoach => true,
                UnifiedRole.Scout => true,
                _ => false
            };
        }

        /// <summary>
        /// Claim/fill an open position
        /// </summary>
        private void ClaimPosition(UnifiedRole role, string teamId)
        {
            if (string.IsNullOrEmpty(teamId))
                return;

            switch (role)
            {
                case UnifiedRole.HeadCoach:
                    teamsNeedingHeadCoach.Remove(teamId);
                    break;
                case UnifiedRole.GeneralManager:
                    teamsNeedingGM.Remove(teamId);
                    break;
                case UnifiedRole.Coordinator:
                    teamsNeedingCoordinator.Remove(teamId);
                    break;
                case UnifiedRole.AssistantGM:
                    teamsNeedingAssistantGM.Remove(teamId);
                    break;
            }
        }

        /// <summary>
        /// Get all teams with open positions for a role
        /// </summary>
        public List<string> GetTeamsWithOpenPosition(UnifiedRole role)
        {
            return role switch
            {
                UnifiedRole.HeadCoach => new List<string>(teamsNeedingHeadCoach),
                UnifiedRole.GeneralManager => new List<string>(teamsNeedingGM),
                UnifiedRole.Coordinator => new List<string>(teamsNeedingCoordinator),
                UnifiedRole.AssistantGM => new List<string>(teamsNeedingAssistantGM),
                _ => new List<string>()
            };
        }

        // ==================== SEASON PROCESSING ====================

        /// <summary>
        /// Process end of season for all profiles
        /// </summary>
        public void ProcessEndOfSeason(int year)
        {
            Debug.Log($"[UnifiedCareerManager] Processing end of season {year}");

            // Process all active profiles
            foreach (var profile in allProfiles.ToList())
            {
                if (profile.CurrentTrack == UnifiedCareerTrack.Retired)
                    continue;

                // Increment years
                profile.ProcessEndOfSeason();

                // Check for retirement
                ProcessRetirement(profile);
            }

            // Process promotions for eligible candidates
            ProcessPromotions(year);

            // Try to fill open positions
            ProcessHiring(year);

            Debug.Log($"[UnifiedCareerManager] Season {year} complete. " +
                     $"Active: {allProfiles.Count - retiredProfiles.Count}, " +
                     $"Retired: {retiredProfiles.Count}");
        }

        /// <summary>
        /// Process automatic promotions for eligible candidates
        /// </summary>
        private void ProcessPromotions(int year)
        {
            foreach (var profile in allProfiles.ToList())
            {
                if (!profile.IsEmployed())
                    continue;

                // Get possible promotions
                var transitions = CareerTransitionRules.GetAllEligibleTransitions(profile);
                var eligiblePromotions = transitions
                    .Where(t => t.eligible && t.req.Type == TransitionType.Promotion)
                    .ToList();

                if (eligiblePromotions.Count == 0)
                    continue;

                // Check each eligible promotion
                foreach (var (req, _, _) in eligiblePromotions)
                {
                    // Skip if requires open position and none available
                    if (req.RequiresOpenPosition && !CheckOpenPosition(req.ToRole))
                        continue;

                    // Roll for promotion chance
                    float promotionChance = CalculatePromotionChance(profile, req);
                    if (UnityEngine.Random.value < promotionChance)
                    {
                        AttemptTransition(profile, req.ToRole, profile.CurrentTeamId, profile.CurrentTeamName);
                        break;  // Only one promotion per season
                    }
                }
            }
        }

        /// <summary>
        /// Calculate promotion chance based on profile and requirements
        /// </summary>
        private float CalculatePromotionChance(UnifiedCareerProfile profile, TransitionRequirement req)
        {
            float baseChance = 0.15f;

            // Time bonus
            int yearsOver = profile.YearsInCurrentRole - req.MinYearsInCurrentRole;
            baseChance += yearsOver * 0.05f;

            // Reputation bonus
            int repOver = profile.OverallReputation - req.MinReputation;
            baseChance += repOver * 0.005f;

            // Success bonus
            if (profile.TotalChampionships > 0)
                baseChance += 0.15f;
            if (profile.TotalPlayoffAppearances > 0)
                baseChance += 0.05f * Mathf.Min(profile.TotalPlayoffAppearances, 5);

            // Former player bonus for visibility
            if (profile.IsFormerPlayer)
                baseChance += 0.05f;

            return Mathf.Clamp01(baseChance);
        }

        /// <summary>
        /// Process hiring unemployed candidates for open positions
        /// </summary>
        private void ProcessHiring(int year)
        {
            // Sort unemployed by reputation (best candidates first)
            var sortedUnemployed = unemployedPool
                .OrderByDescending(p => p.OverallReputation)
                .ThenByDescending(p => p.TotalPlayoffAppearances)
                .ToList();

            foreach (var profile in sortedUnemployed.ToList())
            {
                // Check each possible role they could be hired for
                var eligibleTransitions = CareerTransitionRules.GetAllEligibleTransitions(profile)
                    .Where(t => t.eligible && t.req.Type == TransitionType.Hiring)
                    .OrderByDescending(t => (int)t.req.ToRole)  // Prefer higher positions
                    .ToList();

                foreach (var (req, _, _) in eligibleTransitions)
                {
                    var openTeams = GetTeamsWithOpenPosition(req.ToRole);
                    if (openTeams.Count == 0)
                        continue;

                    // Calculate hiring chance
                    float hiringChance = CalculateHiringChance(profile, req.ToRole);

                    if (UnityEngine.Random.value < hiringChance)
                    {
                        // Pick a random team from those needing this position
                        string teamId = openTeams[UnityEngine.Random.Range(0, openTeams.Count)];
                        string teamName = teamId;  // TODO: Get actual team name

                        var result = AttemptTransition(profile, req.ToRole, teamId, teamName);
                        if (result.success)
                            break;  // Hired, stop looking
                    }
                }
            }
        }

        /// <summary>
        /// Calculate chance of being hired for a position
        /// </summary>
        private float CalculateHiringChance(UnifiedCareerProfile profile, UnifiedRole targetRole)
        {
            float baseChance = 0.20f;

            // Reputation factor
            baseChance += profile.OverallReputation * 0.005f;

            // Experience factor
            int totalExp = profile.TotalCoachingYears + profile.TotalFrontOfficeYears;
            baseChance += totalExp * 0.02f;

            // Success factor
            baseChance += profile.TotalChampionships * 0.10f;
            baseChance += profile.TotalPlayoffAppearances * 0.03f;

            // Former player factor
            if (profile.IsFormerPlayer)
                baseChance += 0.10f;

            // Unemployment penalty
            baseChance -= profile.YearsUnemployed * 0.10f;

            // Higher positions are harder to get
            if (targetRole == UnifiedRole.HeadCoach || targetRole == UnifiedRole.GeneralManager)
                baseChance *= 0.5f;

            return Mathf.Clamp(baseChance, 0.05f, 0.80f);
        }

        // ==================== SAVE/LOAD ====================

        /// <summary>
        /// Get save data for persistence
        /// </summary>
        public UnifiedCareerSaveData GetSaveData()
        {
            return new UnifiedCareerSaveData
            {
                AllProfiles = new List<UnifiedCareerProfile>(allProfiles),
                RetiredProfileIds = retiredProfiles.Select(p => p.ProfileId).ToList(),
                LastProcessedYear = GetCurrentYear()
            };
        }

        /// <summary>
        /// Load from save data
        /// </summary>
        public void LoadSaveData(UnifiedCareerSaveData saveData)
        {
            Initialize(saveData);
        }

        // ==================== HELPERS ====================

        private int GetCurrentYear()
        {
            // TODO: Get from game manager
            return DateTime.Now.Year;
        }

        /// <summary>
        /// Get statistics about the career system
        /// </summary>
        public (int total, int employed, int unemployed, int retired) GetStatistics()
        {
            int employed = allProfiles.Count(p => p.IsEmployed());
            int unemployed = unemployedPool.Count;
            int retired = retiredProfiles.Count;
            return (allProfiles.Count, employed, unemployed, retired);
        }

        /// <summary>
        /// Get all eligible candidates for a specific role
        /// </summary>
        public List<(UnifiedCareerProfile profile, string eligibilityReason)> GetCandidatesForRole(UnifiedRole targetRole)
        {
            var candidates = new List<(UnifiedCareerProfile, string)>();

            foreach (var profile in allProfiles)
            {
                if (profile.CurrentTrack == UnifiedCareerTrack.Retired)
                    continue;

                var (isValid, req) = CareerTransitionRules.IsValidTransition(profile.CurrentRole, targetRole);
                if (!isValid)
                    continue;

                var (eligible, reason) = req.CheckEligibility(profile);
                candidates.Add((profile, eligible ? "Eligible" : reason));
            }

            return candidates.OrderByDescending(c => c.profile.OverallReputation).ToList();
        }
    }
}
