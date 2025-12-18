using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages all injury-related logic including generation, recovery, and load management.
    /// </summary>
    public class InjuryManager : MonoBehaviour
    {
        public static InjuryManager Instance { get; private set; }

        // ==================== EVENTS ====================
        public event Action<Player, InjuryEvent> OnPlayerInjured;
        public event Action<Player> OnPlayerRecovered;
        public event Action<Player, InjuryStatus> OnInjuryStatusChanged;

        // ==================== CONFIGURATION ====================
        [Header("Injury Generation Settings")]
        [SerializeField] [Range(0.00001f, 0.001f)]
        private float _baseInjuryRiskPerPossession = 0.0001f;  // 0.01% base risk

        [SerializeField] [Range(1f, 5f)]
        private float _contactPlayMultiplier = 2.0f;

        [SerializeField] [Range(1f, 5f)]
        private float _lowEnergyMultiplier = 2.0f;  // When energy < 50%

        [SerializeField] [Range(1f, 10f)]
        private float _veryLowEnergyMultiplier = 4.0f;  // When energy < 25%

        [SerializeField] [Range(1f, 3f)]
        private float _backToBackMultiplier = 1.5f;

        [SerializeField] [Range(1f, 2f)]
        private float _playoffIntensityMultiplier = 1.25f;

        [Header("Recovery Settings")]
        [SerializeField] [Range(0.5f, 2f)]
        private float _baseRecoveryRate = 1.0f;  // Days healed per real day

        [Header("Load Management")]
        [SerializeField] private int _highLoadThreshold = 180;  // Minutes in 7 days
        [SerializeField] private int _criticalLoadThreshold = 220;  // Very high load

        // ==================== STATE ====================
        private System.Random _rng;
        private List<InjuryEvent> _recentInjuries = new List<InjuryEvent>();

        // ==================== LIFECYCLE ====================
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                _rng = new System.Random();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Register with GameManager if available
            GameManager.Instance?.RegisterInjuryManager(this);
        }

        // ==================== CORE INJURY METHODS ====================

        /// <summary>
        /// Check if an injury occurs given the context. Returns null if no injury.
        /// </summary>
        public InjuryEvent CheckForInjury(Player player, InjuryContext context)
        {
            if (player == null) return null;
            if (player.IsInjured) return null;  // Already injured

            float risk = GetInjuryRisk(player, context);
            float roll = (float)_rng.NextDouble();

            if (roll < risk)
            {
                // Injury occurred - determine type and severity
                return GenerateInjuryEvent(player, context);
            }

            return null;
        }

        /// <summary>
        /// Calculate the total injury risk for a player given context.
        /// </summary>
        public float GetInjuryRisk(Player player, InjuryContext context)
        {
            float risk = _baseInjuryRiskPerPossession;

            // Contact play multiplier
            if (context.IsContactPlay)
                risk *= _contactPlayMultiplier;

            // Fatigue multiplier (exponential as energy drops)
            if (context.PlayerEnergy < 25f)
                risk *= _veryLowEnergyMultiplier;
            else if (context.PlayerEnergy < 50f)
                risk *= _lowEnergyMultiplier;

            // Durability modifier (0-100 attribute)
            // High durability (100) = 0.5x risk, Low durability (0) = 1.5x risk
            float durabilityMod = Mathf.Lerp(1.5f, 0.5f, player.Durability / 100f);
            risk *= durabilityMod;

            // Injury proneness modifier
            float pronenessMod = 1f + (player.InjuryProneness / 100f);  // 1.0 to 2.0
            risk *= pronenessMod;

            // Back-to-back modifier
            if (context.IsBackToBack)
                risk *= _backToBackMultiplier;

            // Playoff intensity
            if (context.IsPlayoffs)
                risk *= _playoffIntensityMultiplier;

            // High load modifier (based on minutes in last 7 days)
            if (context.MinutesLast7Days > _criticalLoadThreshold)
                risk *= 2.0f;
            else if (context.MinutesLast7Days > _highLoadThreshold)
                risk *= 1.5f;

            // In-game fatigue (minutes this game)
            if (context.MinutesThisGame > 35)
                risk *= 1.5f;
            else if (context.MinutesThisGame > 30)
                risk *= 1.25f;

            // Apply facility prevention bonus if available
            var team = GetPlayerTeam(player);
            if (team?.TrainingFacility != null)
            {
                float preventionBonus = team.TrainingFacility.InjuryPreventionBonus;
                risk *= (1f - preventionBonus);  // Up to 30% reduction
            }

            return risk;
        }

        /// <summary>
        /// Generate the specific injury event (type, severity, days out).
        /// </summary>
        private InjuryEvent GenerateInjuryEvent(Player player, InjuryContext context)
        {
            // Get injury type weights based on play type
            var weights = context.IsContactPlay
                ? InjuryProbabilityWeights.GetContactPlayWeights()
                : InjuryProbabilityWeights.GetNonContactPlayWeights();

            // Select injury type
            InjuryType injuryType = SelectWeighted(weights);

            // Check if this is a re-injury
            bool isReInjury = player.InjuryHistoryList?.Any(h =>
                InjuryRecoveryTables.AffectsSameArea(h.BodyPart, injuryType)) ?? false;

            // Select severity (re-injuries tend to be worse)
            var severityWeights = InjuryProbabilityWeights.GetSeverityWeights(injuryType);
            if (isReInjury)
            {
                // Shift weights toward more severe
                var adjusted = new Dictionary<InjurySeverity, float>();
                foreach (var kvp in severityWeights)
                {
                    float weight = kvp.Value;
                    if (kvp.Key == InjurySeverity.Minor) weight *= 0.7f;
                    if (kvp.Key == InjurySeverity.Moderate) weight *= 1.1f;
                    if (kvp.Key == InjurySeverity.Major) weight *= 1.3f;
                    if (kvp.Key == InjurySeverity.Severe) weight *= 1.5f;
                    adjusted[kvp.Key] = weight;
                }
                severityWeights = adjusted;
            }

            InjurySeverity severity = SelectWeighted(severityWeights);

            // Calculate days out
            int daysOut = InjuryRecoveryTables.GetRecoveryDays(injuryType, severity, _rng);

            // Re-injuries take longer to heal
            if (isReInjury)
            {
                daysOut = (int)(daysOut * 1.25f);
            }

            var injuryEvent = new InjuryEvent
            {
                PlayerId = player.PlayerId,
                Type = injuryType,
                Severity = severity,
                DaysOut = daysOut,
                OriginalDaysOut = daysOut,
                DateOccurred = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                IsReInjury = isReInjury,
                OccurredInGame = context.IsGameAction
            };

            return injuryEvent;
        }

        /// <summary>
        /// Apply an injury to a player.
        /// </summary>
        public void ApplyInjury(Player player, InjuryEvent injury)
        {
            if (player == null || injury == null) return;

            // Update player state
            player.IsInjured = true;
            player.CurrentInjuryType = injury.Type;
            player.CurrentInjurySeverity = injury.Severity;
            player.InjuryDaysRemaining = injury.DaysOut;
            player.OriginalInjuryDays = injury.OriginalDaysOut;
            player.InjuryDate = injury.DateOccurred;

            // Add to injury history
            player.AddInjuryToHistory(injury.Type);

            // Track recent injuries
            _recentInjuries.Add(injury);

            // Fire event
            OnPlayerInjured?.Invoke(player, injury);

            Debug.Log($"[InjuryManager] {player.FullName} injured: {injury.GetDescription()} - Out {injury.DaysOut} days");
        }

        /// <summary>
        /// Apply injury using type and severity directly.
        /// </summary>
        public void ApplyInjury(Player player, InjuryType type, InjurySeverity severity)
        {
            int daysOut = InjuryRecoveryTables.GetRecoveryDays(type, severity, _rng);

            var injury = new InjuryEvent
            {
                PlayerId = player.PlayerId,
                Type = type,
                Severity = severity,
                DaysOut = daysOut,
                OriginalDaysOut = daysOut,
                DateOccurred = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                IsReInjury = player.GetReInjuryRisk(type) > 0,
                OccurredInGame = false
            };

            ApplyInjury(player, injury);
        }

        // ==================== RECOVERY METHODS ====================

        /// <summary>
        /// Process daily recovery for a player.
        /// </summary>
        public void ProcessDailyRecovery(Player player, float facilityBonus = 1f)
        {
            if (player == null || !player.IsInjured) return;

            var previousStatus = player.CurrentInjuryStatus;

            // Calculate recovery amount
            float recoveryDays = _baseRecoveryRate * facilityBonus;

            // Apply recovery
            player.InjuryDaysRemaining -= (int)Math.Ceiling(recoveryDays);

            // Check if recovered
            if (player.InjuryDaysRemaining <= 0)
            {
                RecoverPlayer(player);
            }
            else
            {
                // Check if status changed
                var newStatus = player.CurrentInjuryStatus;
                if (newStatus != previousStatus)
                {
                    OnInjuryStatusChanged?.Invoke(player, newStatus);
                }
            }
        }

        /// <summary>
        /// Mark a player as recovered from injury.
        /// </summary>
        private void RecoverPlayer(Player player)
        {
            player.IsInjured = false;
            player.InjuryDaysRemaining = 0;
            player.CurrentInjuryType = InjuryType.None;
            player.CurrentInjurySeverity = InjurySeverity.Minor;

            OnPlayerRecovered?.Invoke(player);

            Debug.Log($"[InjuryManager] {player.FullName} has recovered from injury");
        }

        /// <summary>
        /// Get the game day status for a player.
        /// </summary>
        public InjuryStatus GetGameDayStatus(Player player)
        {
            if (player == null) return InjuryStatus.Healthy;
            return player.CurrentInjuryStatus;
        }

        /// <summary>
        /// Determine if a questionable/doubtful player will actually play.
        /// Call this on game day to resolve uncertainty.
        /// </summary>
        public bool WillPlayerPlay(Player player)
        {
            if (player == null) return false;

            var status = player.CurrentInjuryStatus;

            return status switch
            {
                InjuryStatus.Healthy => true,
                InjuryStatus.Probable => _rng.NextDouble() < 0.90,
                InjuryStatus.Questionable => _rng.NextDouble() < 0.60,
                InjuryStatus.Doubtful => _rng.NextDouble() < 0.25,
                InjuryStatus.Out => false,
                _ => true
            };
        }

        // ==================== LOAD MANAGEMENT ====================

        /// <summary>
        /// Get load management advice for a player.
        /// </summary>
        public LoadManagementAdvice GetLoadAdvice(Player player)
        {
            if (player == null) return null;

            int minutesLast7Days = player.GetMinutesInLastDays(7);
            float injuryRiskLevel = CalculateLoadRiskLevel(player, minutesLast7Days);

            var advice = new LoadManagementAdvice
            {
                PlayerId = player.PlayerId,
                InjuryRiskLevel = injuryRiskLevel
            };

            // Determine recommendations based on load and injury history
            if (injuryRiskLevel > 0.7f)
            {
                advice.Urgency = LoadManagementUrgency.Critical;
                advice.ShouldRest = true;
                advice.RecommendedMaxMinutes = 0;
                advice.Reason = "Very high injury risk - rest strongly recommended";
            }
            else if (injuryRiskLevel > 0.5f)
            {
                advice.Urgency = LoadManagementUrgency.High;
                advice.ShouldRest = minutesLast7Days > _criticalLoadThreshold;
                advice.RecommendedMaxMinutes = 20;
                advice.Reason = $"High workload ({minutesLast7Days} min in 7 days) - limit minutes";
            }
            else if (injuryRiskLevel > 0.3f)
            {
                advice.Urgency = LoadManagementUrgency.Medium;
                advice.ShouldRest = false;
                advice.RecommendedMaxMinutes = 28;
                advice.Reason = $"Moderate workload ({minutesLast7Days} min in 7 days) - monitor fatigue";
            }
            else
            {
                advice.Urgency = LoadManagementUrgency.Low;
                advice.ShouldRest = false;
                advice.RecommendedMaxMinutes = 36;
                advice.Reason = "Normal workload - full minutes acceptable";
            }

            // Special handling for injury-prone players
            if (player.InjuryProneness > 70)
            {
                advice.RecommendedMaxMinutes = Math.Min(advice.RecommendedMaxMinutes, 28);
                if (advice.Urgency < LoadManagementUrgency.Medium)
                    advice.Urgency = LoadManagementUrgency.Medium;
                advice.Reason += " (injury-prone player)";
            }

            // Special handling for players returning from injury
            if (player.InjuryHistoryList?.Any(h =>
                (GameManager.Instance?.CurrentDate - h.LastInjuryDate)?.Days < 30) ?? false)
            {
                advice.RecommendedMaxMinutes = Math.Min(advice.RecommendedMaxMinutes, 24);
                advice.Reason += " (recently returned from injury)";
            }

            return advice;
        }

        /// <summary>
        /// Calculate a 0-1 risk level based on player load.
        /// </summary>
        private float CalculateLoadRiskLevel(Player player, int minutesLast7Days)
        {
            float loadRisk = 0f;

            // Minutes-based risk
            if (minutesLast7Days > _criticalLoadThreshold)
                loadRisk += 0.4f;
            else if (minutesLast7Days > _highLoadThreshold)
                loadRisk += 0.2f;

            // Age-based risk (older players need more rest)
            if (player.Age > 34)
                loadRisk += 0.2f;
            else if (player.Age > 30)
                loadRisk += 0.1f;

            // Injury proneness risk
            loadRisk += (player.InjuryProneness / 100f) * 0.2f;

            // Injury history risk
            loadRisk += Math.Min(0.2f, player.InjuryHistoryCount * 0.05f);

            // Current energy risk
            if (player.Energy < 50)
                loadRisk += 0.15f;
            else if (player.Energy < 70)
                loadRisk += 0.05f;

            return Mathf.Clamp01(loadRisk);
        }

        /// <summary>
        /// Check if a player should rest based on workload.
        /// </summary>
        public bool ShouldPlayerRest(Player player, int upcomingGames = 1)
        {
            var advice = GetLoadAdvice(player);
            return advice?.ShouldRest ?? false;
        }

        /// <summary>
        /// Record minutes played for a player.
        /// </summary>
        public void RecordMinutesPlayed(Player player, int minutes, DateTime date, bool isBackToBack = false)
        {
            player?.RecordMinutesPlayed(minutes, date);

            if (player != null)
            {
                player.MinutesPlayedThisSeason += minutes;
                player.GamesPlayedThisSeason++;
            }
        }

        // ==================== UTILITY METHODS ====================

        /// <summary>
        /// Select a random item based on weights.
        /// </summary>
        private T SelectWeighted<T>(Dictionary<T, float> weights)
        {
            float total = weights.Values.Sum();
            float roll = (float)_rng.NextDouble() * total;

            float cumulative = 0;
            foreach (var kvp in weights)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative)
                    return kvp.Key;
            }

            return weights.Keys.First();
        }

        /// <summary>
        /// Get the team a player belongs to.
        /// </summary>
        private Team GetPlayerTeam(Player player)
        {
            if (player == null || string.IsNullOrEmpty(player.TeamId))
                return null;

            return GameManager.Instance?.TeamDatabase?.GetTeam(player.TeamId);
        }

        /// <summary>
        /// Get all currently injured players.
        /// </summary>
        public List<Player> GetInjuredPlayers()
        {
            var db = GameManager.Instance?.PlayerDatabase;
            if (db == null) return new List<Player>();

            return db.GetAllPlayers()
                .Where(p => p.IsInjured)
                .ToList();
        }

        /// <summary>
        /// Get injured players for a specific team.
        /// </summary>
        public List<Player> GetInjuredPlayers(string teamId)
        {
            var db = GameManager.Instance?.PlayerDatabase;
            if (db == null) return new List<Player>();

            return db.GetPlayersByTeam(teamId)
                .Where(p => p.IsInjured)
                .ToList();
        }

        /// <summary>
        /// Get recent injury events (for news/notifications).
        /// </summary>
        public List<InjuryEvent> GetRecentInjuries(int days = 7)
        {
            var cutoff = (GameManager.Instance?.CurrentDate ?? DateTime.Now).AddDays(-days);
            return _recentInjuries
                .Where(i => i.DateOccurred >= cutoff)
                .OrderByDescending(i => i.DateOccurred)
                .ToList();
        }

        /// <summary>
        /// Clear old injury records from tracking.
        /// </summary>
        public void CleanupOldRecords(int daysToKeep = 30)
        {
            var cutoff = (GameManager.Instance?.CurrentDate ?? DateTime.Now).AddDays(-daysToKeep);
            _recentInjuries.RemoveAll(i => i.DateOccurred < cutoff);
        }

        // ==================== SAVE/LOAD ====================

        public InjuryManagerState CreateSaveState()
        {
            return new InjuryManagerState
            {
                RecentInjuries = _recentInjuries.ToList()
            };
        }

        public void RestoreFromSave(InjuryManagerState state)
        {
            if (state?.RecentInjuries != null)
            {
                _recentInjuries = state.RecentInjuries.ToList();
            }
        }
    }

    /// <summary>
    /// Serializable state for save/load.
    /// </summary>
    [Serializable]
    public class InjuryManagerState
    {
        public List<InjuryEvent> RecentInjuries = new List<InjuryEvent>();
    }
}
