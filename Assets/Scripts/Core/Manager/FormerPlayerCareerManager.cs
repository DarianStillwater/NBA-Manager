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

        [Header("Settings")]
        [SerializeField, Range(1, 5)] private int minRetireesToPipeline = 1;
        [SerializeField, Range(1, 5)] private int maxRetireesToPipeline = 3;
        [SerializeField, Range(0f, 1f)] private float basePromotionChance = 0.30f;

        // Events
        public event Action<UnifiedCareerProfile> OnFormerPlayerBecameStaff;
        public event Action<UnifiedCareerProfile, UnifiedRole> OnFormerPlayerPromoted;
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
                CreateUnifiedCoachingCareer(playerData);
            }
            else if (path == PostPlayerCareerPath.Scouting)
            {
                CreateUnifiedScoutingCareer(playerData);
            }
            else if (path == PostPlayerCareerPath.FrontOffice)
            {
                CreateUnifiedFrontOfficeCareer(playerData);
            }
        }

        /// <summary>
        /// Create coaching progression for a retired player
        /// </summary>
        public UnifiedCareerProfile CreateUnifiedCoachingCareer(PlayerCareerData playerData)
        {
            var profile = UnifiedCareerProfile.CreateForCoaching(
                playerData.FullName,
                currentYear,
                playerData.Age + 1,
                true,
                playerData.PlayerId,
                PlayerCareerReference.FromPlayerCareerData(playerData)
            );

            // Find a team
            var teamId = FindTeamForNewStaff(profile);
            if (!string.IsNullOrEmpty(teamId))
            {
                profile.CurrentTeamId = teamId;
            }

            PersonnelManager.Instance?.RegisterProfile(profile);
            Debug.Log($"[FormerPlayerCareer] {playerData.FullName} entered coaching pipeline as {profile.CurrentRole}");
            OnFormerPlayerBecameStaff?.Invoke(profile);

            return profile;
        }

        public UnifiedCareerProfile CreateUnifiedScoutingCareer(PlayerCareerData playerData)
        {
            var profile = UnifiedCareerProfile.CreateForFrontOffice(
                playerData.FullName,
                currentYear,
                playerData.Age + 1,
                true,
                playerData.PlayerId,
                PlayerCareerReference.FromPlayerCareerData(playerData)
            );

            var teamId = FindTeamForNewStaff(profile);
            if (!string.IsNullOrEmpty(teamId))
            {
                profile.CurrentTeamId = teamId;
            }

            PersonnelManager.Instance?.RegisterProfile(profile);
            Debug.Log($"[FormerPlayerCareer] {playerData.FullName} entered scouting pipeline");
            OnFormerPlayerBecameStaff?.Invoke(profile);

            return profile;
        }

        public UnifiedCareerProfile CreateUnifiedFrontOfficeCareer(PlayerCareerData playerData)
        {
            var profile = UnifiedCareerProfile.CreateForFrontOffice(
                playerData.FullName,
                currentYear,
                playerData.Age + 1,
                true,
                playerData.PlayerId,
                PlayerCareerReference.FromPlayerCareerData(playerData)
            );
            profile.CurrentRole = UnifiedRole.AssistantGM;

            var teamId = FindTeamForNewStaff(profile);
            if (!string.IsNullOrEmpty(teamId))
            {
                profile.CurrentTeamId = teamId;
            }

            PersonnelManager.Instance?.RegisterProfile(profile);
            Debug.Log($"[FormerPlayerCareer] {playerData.FullName} entered front office pipeline as Assistant GM");
            OnFormerPlayerBecameStaff?.Invoke(profile);

            return profile;
        }

        /// <summary>
        /// Process end of season updates - promotions, moves, etc.
        /// Call this at the end of each season.
        /// </summary>
        public void ProcessEndOfSeason(int year)
        {
            currentYear = year;

            // PersonnelManager already handles daily/seasonal processing for all profiles
            // We can just rely on it.
            
            Debug.Log($"[FormerPlayerCareer] Processed end of season {year}");
        }

        // Promotions and Job Changes are now handled by PersonnelManager

        /// <summary>
        /// Check if the user's team is playing against a team with a former player as head coach
        /// Call this before each game.
        /// </summary>
        public FormerPlayerMatchupInfo CheckForFormerPlayerMatchup(string userTeamId, string opponentTeamId)
        {
            var personnelManager = PersonnelManager.Instance;
            if (personnelManager == null) return null;

            var opponentStaff = personnelManager.GetTeamStaff(opponentTeamId);
            var headCoach = opponentStaff.FirstOrDefault(s => s.CurrentRole == UnifiedRole.HeadCoach);

            if (headCoach == null || !headCoach.IsFormerPlayer)
                return null;

            var matchup = new FormerPlayerMatchupInfo
            {
                FormerPlayerName = headCoach.PersonName,
                OpponentTeamId = opponentTeamId,
                OpponentTeamName = headCoach.CurrentTeamName,
                WasCoachedByUser = false, // TODO: Track user relationship
                SeasonsUnderUser = 0,
                CoachingCareerYears = headCoach.TotalCoachingYears,
                PlayingCareer = headCoach.PlayingCareer
            };

            OnFormerPlayerMatchup?.Invoke(matchup);
            return matchup;
        }

        /// <summary>
        /// Get hiring bonus for a former player coach based on user relationship
        /// </summary>
        public float GetHiringBonus(string formerPlayerId, string teamId)
        {
            var profile = PersonnelManager.Instance?.GetProfile(formerPlayerId);
            if (profile == null) return 0f;

            float bonus = 0f;
            // Simplified logic: +10% if they played for this team
            if (profile.PlayingCareer?.TeamsPlayedFor?.Contains(teamId) == true)
            {
                bonus += 0.10f;
            }

            return Mathf.Min(bonus, 0.30f);
        }
    
        // Relationship tracking disabled for now during refactor

        /// <summary>
        /// Record that the user coached a player
        /// </summary>
        public void RecordUserCoachedPlayer(string playerId, string playerName, string teamId) { }
        private bool CheckIfCoachedByUser(string playerId) => false;
        private int GetSeasonsUnderUser(string playerId) => 0;

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

        private string FindTeamForNewStaff(UnifiedCareerProfile profile)
        {
            // Prefer former team if possible
            if (profile.PlayingCareer?.PrimaryTeam != null)
            {
                return profile.PlayingCareer.PrimaryTeam;
            }
            return null;
        }

        // ==================== GETTERS ====================

        public List<UnifiedCareerProfile> GetAllFormerPlayerStaff()
        {
            return PersonnelManager.Instance?.GetAllProfiles()
                .Where(p => p.IsFormerPlayer).ToList() ?? new List<UnifiedCareerProfile>();
        }

        /// <summary>
        /// Get data for save/load
        /// </summary>
        public FormerPlayerCareerSaveData GetSaveData()
        {
            return new FormerPlayerCareerSaveData
            {
                CurrentYear = currentYear
            };
        }

        public void LoadSaveData(FormerPlayerCareerSaveData data)
        {
            if (data == null) return;
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
        public int CurrentYear;
    }
}
