using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages the career progression of former NBA players who enter coaching or scouting.
    /// Tracks their journey through the ranks and handles promotions, hirings, and matchup notifications.
    /// </summary>
    public class FormerPlayerCareerManager : MonoBehaviour
    {
        public static FormerPlayerCareerManager Instance { get; private set; }

        [Header("Active Former Players")]
        [SerializeField] private List<FormerPlayerCoach> formerPlayerCoaches = new List<FormerPlayerCoach>();
        [SerializeField] private List<FormerPlayerScout> formerPlayerScouts = new List<FormerPlayerScout>();
        [SerializeField] private List<FormerPlayerProgressionData> activeProgressions = new List<FormerPlayerProgressionData>();

        [Header("Settings")]
        [SerializeField, Range(1, 5)] private int minRetireesToPipeline = 1;
        [SerializeField, Range(1, 5)] private int maxRetireesToPipeline = 3;
        [SerializeField, Range(0f, 1f)] private float basePromotionChance = 0.30f;

        [Header("User Coaching History")]
        [SerializeField] private string userCoachId;
        [SerializeField] private List<UserCoachingRelationship> userCoachingHistory = new List<UserCoachingRelationship>();

        [Header("Unified Career Integration")]
        [SerializeField] private bool useUnifiedCareerManager = true;

        // Events
        public event Action<FormerPlayerCoach> OnFormerPlayerBecameCoach;
        public event Action<FormerPlayerScout> OnFormerPlayerBecameScout;
        public event Action<FormerPlayerProgressionData, CoachingProgressionStage> OnFormerPlayerPromoted;
        public event Action<FormerPlayerCoach> OnFormerPlayerBecameHeadCoach;
        public event Action<FormerPlayerMatchupInfo> OnFormerPlayerMatchup;

        private int currentYear;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Subscribe to retirement events
            if (RetirementManager.Instance != null)
            {
                RetirementManager.Instance.OnPlayerEnteredCoachingPipeline += HandlePlayerEnteredPipeline;
            }
        }

        private void OnDestroy()
        {
            if (RetirementManager.Instance != null)
            {
                RetirementManager.Instance.OnPlayerEnteredCoachingPipeline -= HandlePlayerEnteredPipeline;
            }
        }

        /// <summary>
        /// Handle a player entering the coaching/scouting/front office pipeline from retirement
        /// </summary>
        private void HandlePlayerEnteredPipeline(PlayerCareerData playerData, PostPlayerCareerPath path)
        {
            if (path == PostPlayerCareerPath.Coaching)
            {
                CreateCoachingProgression(playerData);
            }
            else if (path == PostPlayerCareerPath.Scouting)
            {
                CreateScoutingCareer(playerData);
            }
            else if (path == PostPlayerCareerPath.FrontOffice)
            {
                // Delegate to GMJobSecurityManager for front office track
                if (GMJobSecurityManager.Instance != null)
                {
                    bool wasCoachedByUser = CheckIfCoachedByUser(playerData.PlayerId);
                    int seasonsUnderUser = GetSeasonsUnderUser(playerData.PlayerId);
                    GMJobSecurityManager.Instance.CreateFrontOfficeProgression(
                        playerData,
                        wasCoachedByUser,
                        seasonsUnderUser,
                        wasCoachedByUser ? userCoachingHistory.FirstOrDefault(h => h.PlayerId == playerData.PlayerId)?.TeamId : null
                    );
                    Debug.Log($"[FormerPlayerCareer] {playerData.FullName} entered front office pipeline");
                }
            }
        }

        /// <summary>
        /// Create coaching progression for a retired player
        /// </summary>
        public FormerPlayerProgressionData CreateCoachingProgression(PlayerCareerData playerData)
        {
            var progression = new FormerPlayerProgressionData
            {
                ProgressionId = Guid.NewGuid().ToString(),
                FormerPlayerId = playerData.PlayerId,
                FormerPlayerName = playerData.FullName,
                CareerPath = PostPlayerCareerPath.Coaching,
                CurrentStage = CoachingProgressionStage.AssistantCoach,
                YearsAtCurrentStage = 0,
                TotalCareerYears = 0,
                YearEnteredPipeline = currentYear,
                PlayingCareer = PlayerCareerReference.FromPlayerCareerData(playerData),
                WasCoachedByUser = CheckIfCoachedByUser(playerData.PlayerId),
                SeasonsUnderUserCoaching = GetSeasonsUnderUser(playerData.PlayerId)
            };

            // Create the FormerPlayerCoach data
            var traits = GeneratePersonalityTraits(playerData);
            var formerPlayerCoach = FormerPlayerCoach.CreateFromRetiredPlayer(playerData, progression, traits);
            formerPlayerCoach.Progression = progression;

            // Find a team to hire them as assistant
            var teamId = FindTeamForNewCoach(progression);
            if (!string.IsNullOrEmpty(teamId))
            {
                progression.CurrentTeamId = teamId;
                progression.TeamsWorkedFor.Add(teamId);
            }

            activeProgressions.Add(progression);
            formerPlayerCoaches.Add(formerPlayerCoach);

            // Create unified career profile
            if (useUnifiedCareerManager && UnifiedCareerManager.Instance != null)
            {
                var unifiedProfile = UnifiedCareerProfile.CreateForCoaching(
                    playerData.FullName,
                    currentYear,
                    playerData.Age + 1,  // Retired players typically take a year before coaching
                    true,
                    playerData.PlayerId,
                    PlayerCareerReference.FromPlayerCareerData(playerData)
                );
                unifiedProfile.FormerPlayerCoachId = formerPlayerCoach.FormerPlayerCoachId;
                unifiedProfile.CurrentTeamId = teamId;
                unifiedProfile.CurrentTeamName = progression.CurrentTeamName;
                progression.UnifiedProfileId = unifiedProfile.ProfileId;

                UnifiedCareerManager.Instance.RegisterProfile(unifiedProfile);
            }

            Debug.Log($"[FormerPlayerCareer] {playerData.FullName} entered coaching pipeline as Assistant Coach");
            OnFormerPlayerBecameCoach?.Invoke(formerPlayerCoach);

            return progression;
        }

        /// <summary>
        /// Create scouting career for a retired player
        /// </summary>
        public FormerPlayerScout CreateScoutingCareer(PlayerCareerData playerData)
        {
            var formerPlayerScout = FormerPlayerScout.CreateFromRetiredPlayer(playerData);
            formerPlayerScout.WasCoachedByUser = CheckIfCoachedByUser(playerData.PlayerId);
            formerPlayerScout.SeasonsUnderUserCoaching = GetSeasonsUnderUser(playerData.PlayerId);

            // Find a team
            var teamId = FindTeamForNewScout();
            if (!string.IsNullOrEmpty(teamId))
            {
                formerPlayerScout.CurrentTeamId = teamId;
                formerPlayerScout.TeamsWorkedFor.Add(teamId);
            }

            formerPlayerScouts.Add(formerPlayerScout);

            Debug.Log($"[FormerPlayerCareer] {playerData.FullName} became a scout");
            OnFormerPlayerBecameScout?.Invoke(formerPlayerScout);

            return formerPlayerScout;
        }

        /// <summary>
        /// Process end of season updates - promotions, moves, etc.
        /// Call this at the end of each season.
        /// </summary>
        public void ProcessEndOfSeason(int year)
        {
            currentYear = year;

            // Advance all progressions
            foreach (var progression in activeProgressions)
            {
                progression.YearsAtCurrentStage++;
                progression.TotalCareerYears++;
            }

            // Advance scout years
            foreach (var scout in formerPlayerScouts)
            {
                scout.AdvanceYear();
            }

            // Check for promotions
            ProcessPromotions();

            // Check for job changes
            ProcessJobChanges();

            // Process front office career progressions
            if (GMJobSecurityManager.Instance != null)
            {
                GMJobSecurityManager.Instance.ProcessEndOfSeason(year);
            }

            // Process unified career manager (handles cross-track transitions and retirements)
            if (useUnifiedCareerManager && UnifiedCareerManager.Instance != null)
            {
                UnifiedCareerManager.Instance.ProcessEndOfSeason(year);
            }

            Debug.Log($"[FormerPlayerCareer] End of season {year}: {activeProgressions.Count} coaches, {formerPlayerScouts.Count} scouts in pipeline");
        }

        /// <summary>
        /// Process potential promotions for coaches in the pipeline
        /// </summary>
        private void ProcessPromotions()
        {
            foreach (var progression in activeProgressions.Where(p => p.IsEligibleForPromotion()))
            {
                float promotionChance = progression.GetBasePromotionChance();

                // Add team success bonus if available
                // promotionChance += GetTeamSuccessBonus(progression.CurrentTeamId);

                if (UnityEngine.Random.value < promotionChance)
                {
                    PromoteCoach(progression);
                }
            }
        }

        /// <summary>
        /// Promote a coach to the next stage
        /// </summary>
        private void PromoteCoach(FormerPlayerProgressionData progression)
        {
            var previousStage = progression.CurrentStage;

            // Advance to next stage
            progression.CurrentStage = previousStage switch
            {
                CoachingProgressionStage.AssistantCoach => CoachingProgressionStage.PositionCoach,
                CoachingProgressionStage.PositionCoach => CoachingProgressionStage.Coordinator,
                CoachingProgressionStage.Coordinator => CoachingProgressionStage.HeadCoach,
                _ => progression.CurrentStage
            };

            progression.YearsAtCurrentStage = 0;

            // Record milestone
            progression.Milestones.Add(ProgressionMilestone.CreatePromotion(
                currentYear,
                progression.CurrentTeamId,
                progression.CurrentTeamName,
                progression.CurrentStage
            ));

            Debug.Log($"[FormerPlayerCareer] {progression.FormerPlayerName} promoted to {progression.CurrentStage}");
            OnFormerPlayerPromoted?.Invoke(progression, progression.CurrentStage);

            // Special handling for becoming Head Coach
            if (progression.CurrentStage == CoachingProgressionStage.HeadCoach)
            {
                var formerPlayerCoach = formerPlayerCoaches.FirstOrDefault(c => c.FormerPlayerId == progression.FormerPlayerId);
                if (formerPlayerCoach != null)
                {
                    OnFormerPlayerBecameHeadCoach?.Invoke(formerPlayerCoach);
                }
            }
        }

        /// <summary>
        /// Process potential job changes (moving between teams)
        /// </summary>
        private void ProcessJobChanges()
        {
            // Coaches who are ready for head coaching jobs may move teams
            foreach (var progression in activeProgressions.Where(p =>
                p.CurrentStage == CoachingProgressionStage.Coordinator &&
                p.YearsAtCurrentStage >= 2))
            {
                // Small chance to move to a new team for head coach opportunity
                if (UnityEngine.Random.value < 0.15f)
                {
                    // This would integrate with CoachJobMarketManager
                    Debug.Log($"[FormerPlayerCareer] {progression.FormerPlayerName} seeking head coaching opportunities");
                }
            }
        }

        /// <summary>
        /// Check if the user's team is playing against a team with a former player as head coach
        /// Call this before each game.
        /// </summary>
        public FormerPlayerMatchupInfo CheckForFormerPlayerMatchup(string userTeamId, string opponentTeamId)
        {
            // Check if opponent's head coach is a former player
            var opponentCoachProgression = activeProgressions.FirstOrDefault(p =>
                p.CurrentTeamId == opponentTeamId &&
                p.CurrentStage == CoachingProgressionStage.HeadCoach);

            if (opponentCoachProgression == null)
                return null;

            var formerPlayerCoach = formerPlayerCoaches.FirstOrDefault(c =>
                c.FormerPlayerId == opponentCoachProgression.FormerPlayerId);

            if (formerPlayerCoach == null)
                return null;

            var matchup = new FormerPlayerMatchupInfo
            {
                FormerPlayerName = opponentCoachProgression.FormerPlayerName,
                OpponentTeamId = opponentTeamId,
                OpponentTeamName = opponentCoachProgression.CurrentTeamName,
                WasCoachedByUser = opponentCoachProgression.WasCoachedByUser,
                SeasonsUnderUser = opponentCoachProgression.SeasonsUnderUserCoaching,
                CoachingCareerYears = opponentCoachProgression.TotalCareerYears,
                PlayingCareer = formerPlayerCoach.PlayingCareer
            };

            OnFormerPlayerMatchup?.Invoke(matchup);

            Debug.Log($"[FormerPlayerCareer] MATCHUP: User faces {matchup.FormerPlayerName}, " +
                     $"former player now coaching {matchup.OpponentTeamName}");

            return matchup;
        }

        /// <summary>
        /// Get hiring bonus for a former player coach based on user relationship
        /// </summary>
        public float GetHiringBonus(string formerPlayerId, string teamId)
        {
            var progression = activeProgressions.FirstOrDefault(p => p.FormerPlayerId == formerPlayerId);
            if (progression == null)
                return 0f;

            float bonus = 0f;

            // Base loyalty bonus if coached by user: +15%
            if (progression.WasCoachedByUser)
            {
                bonus += 0.15f;

                // +3% per season coached together, cap at +15% additional
                float seasonBonus = progression.SeasonsUnderUserCoaching * 0.03f;
                bonus += Mathf.Min(seasonBonus, 0.15f);
            }

            // +10% if they played for this team
            if (progression.PlayingCareer?.TeamsPlayedFor?.Contains(teamId) == true)
            {
                bonus += 0.10f;
            }

            // Cap total bonus at 30%
            return Mathf.Min(bonus, 0.30f);
        }

        /// <summary>
        /// Record that the user coached a player
        /// </summary>
        public void RecordUserCoachedPlayer(string playerId, string playerName, string teamId)
        {
            var existing = userCoachingHistory.FirstOrDefault(h => h.PlayerId == playerId);
            if (existing != null)
            {
                existing.SeasonsCoached++;
            }
            else
            {
                userCoachingHistory.Add(new UserCoachingRelationship
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    TeamId = teamId,
                    SeasonsCoached = 1,
                    FirstSeasonYear = currentYear
                });
            }
        }

        private bool CheckIfCoachedByUser(string playerId)
        {
            return userCoachingHistory.Any(h => h.PlayerId == playerId);
        }

        private int GetSeasonsUnderUser(string playerId)
        {
            var history = userCoachingHistory.FirstOrDefault(h => h.PlayerId == playerId);
            return history?.SeasonsCoached ?? 0;
        }

        private List<PlayerPersonalityTrait> GeneratePersonalityTraits(PlayerCareerData playerData)
        {
            var traits = new List<PlayerPersonalityTrait>();
            var rng = new System.Random(playerData.PlayerId.GetHashCode());

            // Leadership-based traits
            if (playerData.Leadership >= 70 || playerData.AllStarSelections >= 5)
            {
                traits.Add(PlayerPersonalityTrait.Leader);
            }

            // IQ-based traits
            if (playerData.BasketballIQ >= 75)
            {
                traits.Add(PlayerPersonalityTrait.StrategicMind);
                if (rng.NextDouble() < 0.5)
                    traits.Add(PlayerPersonalityTrait.FilmJunkie);
            }

            // Work ethic based traits
            if (playerData.WorkEthic >= 70)
            {
                traits.Add(PlayerPersonalityTrait.Professional);
            }

            // Championships suggest competitor
            if (playerData.Championships > 0)
            {
                traits.Add(PlayerPersonalityTrait.Competitor);
            }

            // Veteran mentor
            if (playerData.SeasonsPlayed >= 12)
            {
                traits.Add(PlayerPersonalityTrait.Mentor);
            }

            // Add random trait if few traits
            if (traits.Count < 2)
            {
                var allTraits = (PlayerPersonalityTrait[])Enum.GetValues(typeof(PlayerPersonalityTrait));
                traits.Add(allTraits[rng.Next(allTraits.Length)]);
            }

            return traits.Distinct().ToList();
        }

        private string FindTeamForNewCoach(FormerPlayerProgressionData progression)
        {
            // Prefer former team if possible
            if (progression.PlayingCareer?.PrimaryTeam != null)
            {
                return progression.PlayingCareer.PrimaryTeam;
            }

            // Otherwise assign to a random team needing assistant coaches
            // This would integrate with actual team data
            return null;
        }

        private string FindTeamForNewScout()
        {
            // Would integrate with team data to find teams needing scouts
            return null;
        }

        // ==================== GETTERS ====================

        public List<FormerPlayerCoach> GetAllFormerPlayerCoaches() => formerPlayerCoaches;

        public List<FormerPlayerScout> GetAllFormerPlayerScouts() => formerPlayerScouts;

        public List<FormerPlayerProgressionData> GetAllProgressions() => activeProgressions;

        public List<FormerPlayerCoach> GetFormerPlayerHeadCoaches()
        {
            return formerPlayerCoaches.Where(c =>
                c.Progression?.CurrentStage == CoachingProgressionStage.HeadCoach).ToList();
        }

        public FormerPlayerCoach GetFormerPlayerCoachByPlayerId(string playerId)
        {
            return formerPlayerCoaches.FirstOrDefault(c => c.FormerPlayerId == playerId);
        }

        public FormerPlayerCoach GetFormerPlayerCoachByTeam(string teamId)
        {
            return formerPlayerCoaches.FirstOrDefault(c =>
                c.Progression?.CurrentTeamId == teamId &&
                c.Progression?.CurrentStage == CoachingProgressionStage.HeadCoach);
        }

        public List<FormerPlayerCoach> GetUserFormerPlayersInCoaching()
        {
            return formerPlayerCoaches.Where(c => c.Progression?.WasCoachedByUser == true).ToList();
        }

        /// <summary>
        /// Get data for save/load
        /// </summary>
        public FormerPlayerCareerSaveData GetSaveData()
        {
            return new FormerPlayerCareerSaveData
            {
                FormerPlayerCoaches = formerPlayerCoaches,
                FormerPlayerScouts = formerPlayerScouts,
                ActiveProgressions = activeProgressions,
                UserCoachingHistory = userCoachingHistory,
                CurrentYear = currentYear
            };
        }

        /// <summary>
        /// Load from save data
        /// </summary>
        public void LoadSaveData(FormerPlayerCareerSaveData data)
        {
            if (data == null) return;

            formerPlayerCoaches = data.FormerPlayerCoaches ?? new List<FormerPlayerCoach>();
            formerPlayerScouts = data.FormerPlayerScouts ?? new List<FormerPlayerScout>();
            activeProgressions = data.ActiveProgressions ?? new List<FormerPlayerProgressionData>();
            userCoachingHistory = data.UserCoachingHistory ?? new List<UserCoachingRelationship>();
            currentYear = data.CurrentYear;
        }
    }

    /// <summary>
    /// Information about a matchup against a former player coach
    /// </summary>
    [Serializable]
    public class FormerPlayerMatchupInfo
    {
        public string FormerPlayerName;
        public string OpponentTeamId;
        public string OpponentTeamName;
        public bool WasCoachedByUser;
        public int SeasonsUnderUser;
        public int CoachingCareerYears;
        public PlayerCareerReference PlayingCareer;

        public string GetNotificationText()
        {
            if (WasCoachedByUser && SeasonsUnderUser > 0)
            {
                return $"Tonight you face {FormerPlayerName}, your former player who you coached for {SeasonsUnderUser} season{(SeasonsUnderUser > 1 ? "s" : "")}. " +
                       $"Now in his {GetOrdinal(CoachingCareerYears)} year as a coach, he leads the {OpponentTeamName}.";
            }

            return $"Tonight you face {FormerPlayerName}, a {PlayingCareer?.SeasonsPlayed ?? 0}-year NBA veteran " +
                   $"who is now in his {GetOrdinal(CoachingCareerYears)} year as a coach with the {OpponentTeamName}.";
        }

        private string GetOrdinal(int num)
        {
            if (num <= 0) return num.ToString();
            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }
            switch (num % 10)
            {
                case 1: return num + "st";
                case 2: return num + "nd";
                case 3: return num + "rd";
                default: return num + "th";
            }
        }
    }

    /// <summary>
    /// Tracks user's coaching relationship with players
    /// </summary>
    [Serializable]
    public class UserCoachingRelationship
    {
        public string PlayerId;
        public string PlayerName;
        public string TeamId;
        public int SeasonsCoached;
        public int FirstSeasonYear;
    }

    /// <summary>
    /// Save data for FormerPlayerCareerManager
    /// </summary>
    [Serializable]
    public class FormerPlayerCareerSaveData
    {
        public List<FormerPlayerCoach> FormerPlayerCoaches;
        public List<FormerPlayerScout> FormerPlayerScouts;
        public List<FormerPlayerProgressionData> ActiveProgressions;
        public List<UserCoachingRelationship> UserCoachingHistory;
        public int CurrentYear;
    }
}
