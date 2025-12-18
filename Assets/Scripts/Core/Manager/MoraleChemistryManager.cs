using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages morale impacts and chemistry effects on gameplay.
    /// Connects PersonalityManager data to actual game simulation.
    /// </summary>
    public class MoraleChemistryManager : MonoBehaviour
    {
        public static MoraleChemistryManager Instance { get; private set; }

        #region Configuration

        [Header("Morale Settings")]
        [SerializeField] private float _moraleDecayPerDay = 0.5f;  // Daily morale decay toward 50
        [SerializeField] private int _minMorale = 0;
        [SerializeField] private int _maxMorale = 100;
        [SerializeField] private int _neutralMorale = 50;

        [Header("Chemistry Impact")]
        [SerializeField] private float _maxChemistryBonus = 0.15f;   // +15% at perfect chemistry
        [SerializeField] private float _maxChemistryPenalty = 0.15f; // -15% at terrible chemistry

        [Header("Streak Bonuses")]
        [SerializeField] private int _streakThreshold = 3;  // Games to trigger streak bonus

        #endregion

        #region State

        private PersonalityManager _personalityManager;
        private Dictionary<string, int> _teamWinStreaks = new Dictionary<string, int>();
        private Dictionary<string, int> _teamLossStreaks = new Dictionary<string, int>();
        private Dictionary<string, float> _cachedTeamChemistry = new Dictionary<string, float>();
        private Dictionary<string, List<LockerRoomEvent>> _recentEvents = new Dictionary<string, List<LockerRoomEvent>>();

        #endregion

        #region Events

        public event Action<LockerRoomEvent> OnLockerRoomEvent;
        public event Action<string, MoraleChangeInfo> OnMoraleChanged;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                _personalityManager = new PersonalityManager();
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            GameManager.Instance?.RegisterMoraleChemistryManager(this);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize personalities for all players on a team.
        /// </summary>
        public void InitializeTeamPersonalities(Team team)
        {
            if (team?.Roster == null) return;

            foreach (var player in team.Roster)
            {
                if (_personalityManager.GetPersonality(player.PlayerId) == null)
                {
                    var personality = Personality.GenerateRandom();
                    // Set expected role based on player rating
                    personality.ExpectedRole = DetermineExpectedRole(player);
                    _personalityManager.SetPersonality(player.PlayerId, personality);
                }
            }

            // Calculate initial team chemistry
            UpdateTeamChemistry(team);
        }

        private PlayerRole DetermineExpectedRole(Player player)
        {
            int overall = player.GetOverallRating();
            if (overall >= 85) return PlayerRole.Starter;
            if (overall >= 75) return PlayerRole.SixthMan;
            if (overall >= 65) return PlayerRole.Rotation;
            return PlayerRole.Bench;
        }

        #endregion

        #region Chemistry Calculations

        /// <summary>
        /// Gets the team chemistry modifier for gameplay (-0.15 to +0.15).
        /// </summary>
        public float GetTeamChemistryModifier(string teamId)
        {
            if (!_cachedTeamChemistry.TryGetValue(teamId, out float chemistry))
                chemistry = 50f;

            // Convert 0-100 chemistry to -maxPenalty to +maxBonus
            float normalized = (chemistry - 50f) / 50f; // -1 to +1
            return normalized > 0
                ? normalized * _maxChemistryBonus
                : normalized * _maxChemistryPenalty;
        }

        /// <summary>
        /// Gets chemistry between two specific players for passing/assists.
        /// </summary>
        public float GetPairChemistryModifier(string player1Id, string player2Id)
        {
            float pairChemistry = _personalityManager.CalculatePairChemistry(player1Id, player2Id);
            // -1 to +1 chemistry -> -10% to +10% modifier
            return pairChemistry * 0.10f;
        }

        /// <summary>
        /// Gets chemistry modifier for on-court lineup (5 players).
        /// </summary>
        public float GetLineupChemistryModifier(List<string> lineupPlayerIds)
        {
            if (lineupPlayerIds == null || lineupPlayerIds.Count < 2)
                return 0f;

            float total = 0f;
            int pairs = 0;

            for (int i = 0; i < lineupPlayerIds.Count; i++)
            {
                for (int j = i + 1; j < lineupPlayerIds.Count; j++)
                {
                    total += _personalityManager.CalculatePairChemistry(
                        lineupPlayerIds[i], lineupPlayerIds[j]);
                    pairs++;
                }
            }

            float avgChemistry = pairs > 0 ? total / pairs : 0f;
            return avgChemistry * 0.12f; // -12% to +12%
        }

        /// <summary>
        /// Updates and caches team chemistry.
        /// </summary>
        public float UpdateTeamChemistry(Team team)
        {
            if (team?.Roster == null) return 50f;

            var playerIds = team.Roster.Select(p => p.PlayerId).ToList();
            float chemistry = _personalityManager.CalculateTeamChemistry(playerIds);

            _cachedTeamChemistry[team.TeamId] = chemistry;
            team.TeamChemistry = chemistry;

            return chemistry;
        }

        #endregion

        #region Post-Game Processing

        /// <summary>
        /// Process game result and update all morale values.
        /// Call this after every completed game.
        /// </summary>
        public PostGameMoraleReport ProcessGameResult(GameResult result, Team playerTeam, bool isPlayoff = false)
        {
            var report = new PostGameMoraleReport();
            bool won = (result.HomeTeamId == playerTeam.TeamId && result.HomeScore > result.AwayScore) ||
                       (result.AwayTeamId == playerTeam.TeamId && result.AwayScore > result.HomeScore);

            int scoreDiff = Math.Abs(result.HomeScore - result.AwayScore);
            bool isBlowout = scoreDiff >= 20;
            bool isClose = scoreDiff <= 5;

            // Update streaks
            UpdateStreaks(playerTeam.TeamId, won);

            // Determine base morale event
            MoraleEvent teamEvent;
            if (won)
            {
                teamEvent = isBlowout || isPlayoff ? MoraleEvent.BigWin : MoraleEvent.Win;
            }
            else
            {
                teamEvent = isClose || isPlayoff ? MoraleEvent.ToughLoss : MoraleEvent.Loss;
            }

            // Apply team-wide morale event
            var playerIds = playerTeam.Roster.Select(p => p.PlayerId).ToList();
            _personalityManager.ApplyTeamMoraleEvent(playerIds, teamEvent);
            report.TeamMoraleEvent = teamEvent;

            // Process individual performance-based morale
            ProcessPerformanceMorale(result.BoxScore, playerTeam, report);

            // Process usage-based morale
            ProcessUsageMorale(result.BoxScore, playerTeam, report);

            // Streak-based morale bonus
            ProcessStreakMorale(playerTeam, report);

            // Check for potential conflicts
            CheckForConflicts(playerTeam, report);

            // Sync morale back to Player objects
            SyncMoraleToPlayers(playerTeam);

            return report;
        }

        private void UpdateStreaks(string teamId, bool won)
        {
            if (won)
            {
                _teamWinStreaks[teamId] = _teamWinStreaks.GetValueOrDefault(teamId, 0) + 1;
                _teamLossStreaks[teamId] = 0;
            }
            else
            {
                _teamLossStreaks[teamId] = _teamLossStreaks.GetValueOrDefault(teamId, 0) + 1;
                _teamWinStreaks[teamId] = 0;
            }
        }

        private void ProcessPerformanceMorale(BoxScore boxScore, Team team, PostGameMoraleReport report)
        {
            foreach (var player in team.Roster)
            {
                var stats = boxScore.GetPlayerStats(player.PlayerId);
                if (stats == null) continue;

                // Big performance bonus
                if (stats.Points >= 30 || stats.Assists >= 12 || stats.Rebounds >= 15)
                {
                    int change = _personalityManager.ApplyPlayerMoraleEvent(
                        player.PlayerId, MoraleEvent.HighUsage, 5);
                    report.IndividualChanges.Add(new MoraleChangeInfo
                    {
                        PlayerId = player.PlayerId,
                        PlayerName = player.DisplayName,
                        Change = change,
                        Reason = "Big performance"
                    });
                }

                // DNP-CD penalty (Did Not Play - Coach's Decision)
                if (stats.MinutesPlayed == 0 && !player.IsInjured)
                {
                    var personality = _personalityManager.GetPersonality(player.PlayerId);
                    if (personality?.ExpectedRole <= PlayerRole.Rotation)
                    {
                        int change = _personalityManager.ApplyPlayerMoraleEvent(
                            player.PlayerId, MoraleEvent.GotBenched);
                        report.IndividualChanges.Add(new MoraleChangeInfo
                        {
                            PlayerId = player.PlayerId,
                            PlayerName = player.DisplayName,
                            Change = change,
                            Reason = "DNP-CD"
                        });
                    }
                }
            }
        }

        private void ProcessUsageMorale(BoxScore boxScore, Team team, PostGameMoraleReport report)
        {
            // Calculate team total shots
            int totalShots = 0;
            foreach (var player in team.Roster)
            {
                var stats = boxScore.GetPlayerStats(player.PlayerId);
                if (stats != null)
                    totalShots += stats.FieldGoalsAttempted;
            }

            if (totalShots == 0) return;

            foreach (var player in team.Roster)
            {
                var stats = boxScore.GetPlayerStats(player.PlayerId);
                var personality = _personalityManager.GetPersonality(player.PlayerId);
                if (stats == null || personality == null) continue;

                float usageRate = (float)stats.FieldGoalsAttempted / totalShots;
                bool hasBallHog = personality.HasTrait(PersonalityTrait.BallHog);

                // Ball hogs need at least 20% usage to be happy
                if (hasBallHog && usageRate < 0.15f && stats.MinutesPlayed >= 20)
                {
                    int change = _personalityManager.ApplyPlayerMoraleEvent(
                        player.PlayerId, MoraleEvent.LowUsage);
                    report.IndividualChanges.Add(new MoraleChangeInfo
                    {
                        PlayerId = player.PlayerId,
                        PlayerName = player.DisplayName,
                        Change = change,
                        Reason = "Low usage (Ball Hog)"
                    });
                }
            }
        }

        private void ProcessStreakMorale(Team team, PostGameMoraleReport report)
        {
            int winStreak = _teamWinStreaks.GetValueOrDefault(team.TeamId, 0);
            int lossStreak = _teamLossStreaks.GetValueOrDefault(team.TeamId, 0);

            if (winStreak >= _streakThreshold)
            {
                // Team on a roll - bonus morale
                foreach (var player in team.Roster)
                {
                    var personality = _personalityManager.GetPersonality(player.PlayerId);
                    if (personality != null && personality.HasTrait(PersonalityTrait.Competitor))
                    {
                        personality.Morale = Math.Min(_maxMorale, personality.Morale + 2);
                    }
                }
                report.StreakBonus = $"{winStreak} game win streak! Competitors energized.";
            }
            else if (lossStreak >= _streakThreshold)
            {
                // Team struggling - volatile players may snap
                foreach (var player in team.Roster)
                {
                    var personality = _personalityManager.GetPersonality(player.PlayerId);
                    if (personality != null && personality.HasTrait(PersonalityTrait.Volatile))
                    {
                        personality.Morale = Math.Max(_minMorale, personality.Morale - 3);
                    }
                }
                report.StreakBonus = $"{lossStreak} game losing streak. Volatile players frustrated.";
            }
        }

        private void CheckForConflicts(Team team, PostGameMoraleReport report)
        {
            var playerIds = team.Roster.Select(p => p.PlayerId).ToList();
            var troublemakers = _personalityManager.GetPotentialTroublemakers(playerIds);

            foreach (var troublemakerId in troublemakers)
            {
                var player = team.Roster.Find(p => p.PlayerId == troublemakerId);
                if (player == null) continue;

                // 20% chance of locker room incident per troublemaker
                if (UnityEngine.Random.value < 0.20f)
                {
                    var lockerEvent = GenerateLockerRoomEvent(player, team);
                    report.LockerRoomEvents.Add(lockerEvent);
                    OnLockerRoomEvent?.Invoke(lockerEvent);
                }
            }
        }

        private void SyncMoraleToPlayers(Team team)
        {
            foreach (var player in team.Roster)
            {
                var personality = _personalityManager.GetPersonality(player.PlayerId);
                if (personality != null)
                {
                    player.Morale = personality.Morale;
                }
            }
        }

        #endregion

        #region Locker Room Events

        /// <summary>
        /// Generates a random locker room event.
        /// </summary>
        private LockerRoomEvent GenerateLockerRoomEvent(Player instigator, Team team)
        {
            var personality = _personalityManager.GetPersonality(instigator.PlayerId);
            LockerRoomEventType eventType;

            if (personality.HasTrait(PersonalityTrait.Volatile))
            {
                eventType = UnityEngine.Random.value < 0.5f
                    ? LockerRoomEventType.OutburstAtCoach
                    : LockerRoomEventType.TeammateArgument;
            }
            else if (personality.HasTrait(PersonalityTrait.BallHog))
            {
                eventType = LockerRoomEventType.DemandsMoreTouches;
            }
            else
            {
                eventType = LockerRoomEventType.TeammateArgument;
            }

            var evt = new LockerRoomEvent
            {
                EventType = eventType,
                InstigatorId = instigator.PlayerId,
                InstigatorName = instigator.DisplayName,
                Date = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                Description = GenerateEventDescription(eventType, instigator),
                Severity = personality.Morale < 20 ? EventSeverity.Severe : EventSeverity.Moderate
            };

            // Pick a random target for arguments
            if (eventType == LockerRoomEventType.TeammateArgument)
            {
                var otherPlayers = team.Roster.Where(p => p.PlayerId != instigator.PlayerId).ToList();
                if (otherPlayers.Count > 0)
                {
                    var target = otherPlayers[UnityEngine.Random.Range(0, otherPlayers.Count)];
                    evt.TargetId = target.PlayerId;
                    evt.TargetName = target.DisplayName;
                    evt.Description = $"{instigator.DisplayName} had a heated argument with {target.DisplayName} in the locker room.";

                    // Both players lose morale from conflict
                    _personalityManager.ApplyPlayerMoraleEvent(instigator.PlayerId, MoraleEvent.TeammateConflict);
                    _personalityManager.ApplyPlayerMoraleEvent(target.PlayerId, MoraleEvent.TeammateConflict);
                }
            }

            return evt;
        }

        private string GenerateEventDescription(LockerRoomEventType eventType, Player instigator)
        {
            return eventType switch
            {
                LockerRoomEventType.OutburstAtCoach =>
                    $"{instigator.DisplayName} had an angry outburst directed at the coaching staff.",
                LockerRoomEventType.DemandsMoreTouches =>
                    $"{instigator.DisplayName} complained about lack of touches in the offense.",
                LockerRoomEventType.PositiveSpeech =>
                    $"{instigator.DisplayName} gave a motivational speech to rally the team.",
                LockerRoomEventType.VeteranMentoring =>
                    $"{instigator.DisplayName} spent extra time working with young players.",
                _ => $"Locker room incident involving {instigator.DisplayName}."
            };
        }

        /// <summary>
        /// Generates positive locker room events from team leaders.
        /// </summary>
        public void ProcessPositiveLeadership(Team team)
        {
            var playerIds = team.Roster.Select(p => p.PlayerId).ToList();
            var leaderId = _personalityManager.GetTeamLeader(playerIds);

            if (string.IsNullOrEmpty(leaderId)) return;

            var leader = team.Roster.Find(p => p.PlayerId == leaderId);
            var leaderPersonality = _personalityManager.GetPersonality(leaderId);

            if (leader == null || leaderPersonality == null) return;

            // High-morale leader can boost team
            if (leaderPersonality.Morale >= 70 && UnityEngine.Random.value < 0.15f)
            {
                var evt = new LockerRoomEvent
                {
                    EventType = LockerRoomEventType.PositiveSpeech,
                    InstigatorId = leader.PlayerId,
                    InstigatorName = leader.DisplayName,
                    Date = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                    Description = $"{leader.DisplayName} rallied the team with a motivational speech.",
                    Severity = EventSeverity.Positive
                };

                // Small morale boost for whole team
                foreach (var playerId in playerIds)
                {
                    var personality = _personalityManager.GetPersonality(playerId);
                    if (personality != null)
                    {
                        personality.Morale = Math.Min(_maxMorale, personality.Morale + 3);
                    }
                }

                OnLockerRoomEvent?.Invoke(evt);
            }
        }

        #endregion

        #region Daily Processing

        /// <summary>
        /// Process daily morale decay/normalization.
        /// Call this each day advance.
        /// </summary>
        public void ProcessDailyMorale(Team team)
        {
            if (team?.Roster == null) return;

            foreach (var player in team.Roster)
            {
                var personality = _personalityManager.GetPersonality(player.PlayerId);
                if (personality == null) continue;

                // Morale slowly returns to neutral
                if (personality.Morale > _neutralMorale)
                {
                    personality.Morale = (int)Math.Max(_neutralMorale,
                        personality.Morale - _moraleDecayPerDay);
                }
                else if (personality.Morale < _neutralMorale)
                {
                    personality.Morale = (int)Math.Min(_neutralMorale,
                        personality.Morale + _moraleDecayPerDay);
                }

                // Sync to player
                player.Morale = personality.Morale;
            }
        }

        #endregion

        #region Coaching Actions

        /// <summary>
        /// Coach praises a player.
        /// </summary>
        public MoraleChangeInfo PraisePlayer(string playerId)
        {
            int change = _personalityManager.ApplyPlayerMoraleEvent(playerId, MoraleEvent.CoachPraise);
            var player = GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);

            var info = new MoraleChangeInfo
            {
                PlayerId = playerId,
                PlayerName = player?.DisplayName ?? playerId,
                Change = change,
                Reason = "Coach Praise"
            };

            OnMoraleChanged?.Invoke(playerId, info);
            return info;
        }

        /// <summary>
        /// Coach criticizes a player.
        /// </summary>
        public MoraleChangeInfo CriticizePlayer(string playerId)
        {
            int change = _personalityManager.ApplyPlayerMoraleEvent(playerId, MoraleEvent.CoachCriticism);
            var player = GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);

            var info = new MoraleChangeInfo
            {
                PlayerId = playerId,
                PlayerName = player?.DisplayName ?? playerId,
                Change = change,
                Reason = "Coach Criticism"
            };

            OnMoraleChanged?.Invoke(playerId, info);
            return info;
        }

        /// <summary>
        /// Player gets promoted to starter.
        /// </summary>
        public void PromoteToStarter(string playerId)
        {
            _personalityManager.ApplyPlayerMoraleEvent(playerId, MoraleEvent.BecameStarter);
        }

        /// <summary>
        /// Player gets demoted from rotation.
        /// </summary>
        public void DemotePlayer(string playerId)
        {
            _personalityManager.ApplyPlayerMoraleEvent(playerId, MoraleEvent.GotBenched);
        }

        #endregion

        #region Queries

        /// <summary>
        /// Gets morale level for a player.
        /// </summary>
        public MoraleLevel GetMoraleLevel(string playerId)
        {
            var personality = _personalityManager.GetPersonality(playerId);
            return personality?.MoraleLevel ?? MoraleLevel.Content;
        }

        /// <summary>
        /// Gets current morale value (0-100).
        /// </summary>
        public int GetMorale(string playerId)
        {
            var personality = _personalityManager.GetPersonality(playerId);
            return personality?.Morale ?? 50;
        }

        /// <summary>
        /// Gets team chemistry (0-100).
        /// </summary>
        public float GetTeamChemistry(string teamId)
        {
            return _cachedTeamChemistry.GetValueOrDefault(teamId, 50f);
        }

        /// <summary>
        /// Gets current win/loss streak for a team.
        /// </summary>
        public (int winStreak, int lossStreak) GetStreak(string teamId)
        {
            return (
                _teamWinStreaks.GetValueOrDefault(teamId, 0),
                _teamLossStreaks.GetValueOrDefault(teamId, 0)
            );
        }

        /// <summary>
        /// Gets the personality manager for direct access if needed.
        /// </summary>
        public PersonalityManager PersonalityManager => _personalityManager;

        #endregion
    }

    #region Supporting Types

    [Serializable]
    public class PostGameMoraleReport
    {
        public MoraleEvent TeamMoraleEvent;
        public List<MoraleChangeInfo> IndividualChanges = new List<MoraleChangeInfo>();
        public List<LockerRoomEvent> LockerRoomEvents = new List<LockerRoomEvent>();
        public string StreakBonus;
    }

    [Serializable]
    public class MoraleChangeInfo
    {
        public string PlayerId;
        public string PlayerName;
        public int Change;
        public string Reason;
    }

    [Serializable]
    public class LockerRoomEvent
    {
        public LockerRoomEventType EventType;
        public string InstigatorId;
        public string InstigatorName;
        public string TargetId;
        public string TargetName;
        public DateTime Date;
        public string Description;
        public EventSeverity Severity;
    }

    public enum LockerRoomEventType
    {
        TeammateArgument,
        OutburstAtCoach,
        DemandsMoreTouches,
        DemandsTrade,
        PositiveSpeech,
        VeteranMentoring,
        TeamBonding
    }

    public enum EventSeverity
    {
        Minor,
        Moderate,
        Severe,
        Positive
    }

    #endregion
}
