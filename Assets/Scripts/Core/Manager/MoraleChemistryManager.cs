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

        [Header("Captain Settings")]
        [SerializeField] private float _captainMoraleAmplifier = 0.20f;  // +20% morale effect for happy captain
        [SerializeField] private float _captainNegativeAmplifier = 0.10f; // -10% for unhappy captain
        [SerializeField] private int _captainLeadershipThreshold = 60;    // Min Leadership to be effective captain

        [Header("Escalation Settings")]
        [SerializeField] private int _unhappyThreshold = 30;    // Morale below this triggers discontent
        [SerializeField] private int _contentThreshold = 50;    // Morale above this allows de-escalation
        [SerializeField] private int _daysToEscalate = 7;       // Days unhappy before escalating
        [SerializeField] private int _daysToDeescalate = 14;    // Days content before de-escalating

        [Header("Meeting Settings")]
        [SerializeField] private int _meetingCooldownDays = 14;

        #endregion

        #region Meeting State

        private DateTime? _lastTeamMeetingDate;

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

            // Apply team-wide morale event with expectation-based amounts
            var playerIds = playerTeam.Roster.Select(p => p.PlayerId).ToList();
            int baseAmount = GetExpectationBasedMoraleChange(playerTeam, won, isPlayoff);

            // Apply with captain modifier
            float captainModifier = GetCaptainMoraleModifier(playerTeam);
            int adjustedAmount = Mathf.RoundToInt(baseAmount * (1f + captainModifier));

            foreach (var playerId in playerIds)
            {
                _personalityManager.ApplyPlayerMoraleEvent(playerId, teamEvent, adjustedAmount);
            }
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

                // Process contract satisfaction effects
                ProcessContractSatisfaction(player);

                // Process escalation/de-escalation
                ProcessEscalation(player, team);

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

        #region Expectation-Based Morale

        /// <summary>
        /// Gets morale change based on team expectations (contenders vs lottery).
        /// </summary>
        private int GetExpectationBasedMoraleChange(Team team, bool won, bool isPlayoff)
        {
            float expectedWinPct = GetProjectedWinPct(team);

            if (won)
            {
                // Contenders expect wins - less boost
                // Lottery teams celebrate more
                if (isPlayoff) return 8; // Playoff wins always big
                if (expectedWinPct > 0.6f) return 1;  // Contenders
                if (expectedWinPct > 0.4f) return 3;  // Playoff teams
                return 5;  // Lottery teams
            }
            else
            {
                // Contenders hurt more by losses
                // Lottery teams expect losses
                if (isPlayoff) return -8; // Playoff losses always hurt
                if (expectedWinPct > 0.6f) return -5; // Contenders
                if (expectedWinPct > 0.4f) return -3; // Playoff teams
                return -1; // Lottery teams
            }
        }

        /// <summary>
        /// Estimates team's expected win percentage based on roster strength.
        /// </summary>
        private float GetProjectedWinPct(Team team)
        {
            if (team?.Roster == null || team.Roster.Count == 0)
                return 0.5f;

            // Simple projection based on top 8 player ratings
            var topPlayers = team.Roster
                .OrderByDescending(p => p.GetOverallRating())
                .Take(8)
                .ToList();

            float avgRating = topPlayers.Average(p => p.GetOverallRating());

            // Convert rating to win pct (60 rating = 0.2, 80 rating = 0.7)
            return Mathf.Clamp01((avgRating - 50f) / 40f);
        }

        #endregion

        #region Captain System

        /// <summary>
        /// Gets morale modifier based on team captain's happiness.
        /// </summary>
        private float GetCaptainMoraleModifier(Team team)
        {
            var captain = team.Roster?.Find(p => p.IsCaptain);
            if (captain == null) return 0f;

            var personality = _personalityManager.GetPersonality(captain.PlayerId);
            if (personality == null) return 0f;

            // Check if captain has good leadership
            bool hasLeadership = captain.Leadership >= _captainLeadershipThreshold;
            float leadershipMultiplier = hasLeadership ? 1.5f : 1f;

            // Happy captain boosts team morale, unhappy captain spreads negativity
            if (personality.Morale >= 65)
            {
                return _captainMoraleAmplifier * leadershipMultiplier;
            }
            else if (personality.Morale <= 35)
            {
                return -_captainNegativeAmplifier * leadershipMultiplier;
            }

            return 0f;
        }

        /// <summary>
        /// Assigns a player as team captain.
        /// </summary>
        public CaptainAssignmentResult AssignCaptain(Team team, string playerId)
        {
            var result = new CaptainAssignmentResult { Success = false };

            var player = team.Roster?.Find(p => p.PlayerId == playerId);
            if (player == null)
            {
                result.Message = "Player not found on roster.";
                return result;
            }

            // Check leadership requirement
            if (player.Leadership < _captainLeadershipThreshold)
            {
                result.Message = $"{player.DisplayName}'s leadership ({player.Leadership}) is below the recommended threshold ({_captainLeadershipThreshold}). This may backfire.";
                result.IsRisky = true;
            }

            // Remove captain from previous captain
            foreach (var p in team.Roster)
            {
                if (p.IsCaptain && p.PlayerId != playerId)
                {
                    p.IsCaptain = false;
                    // Previous captain may be upset
                    var prevPersonality = _personalityManager.GetPersonality(p.PlayerId);
                    if (prevPersonality != null && prevPersonality.HasTrait(PersonalityTrait.Leader))
                    {
                        prevPersonality.AdjustMorale(MoraleEvent.GotBenched, -5);
                        result.PreviousCaptainUpset = true;
                    }
                }
            }

            player.IsCaptain = true;
            result.Success = true;
            result.Message = $"{player.DisplayName} has been named team captain.";

            // New captain gets morale boost
            var personality = _personalityManager.GetPersonality(playerId);
            if (personality != null)
            {
                personality.AdjustMorale(MoraleEvent.BecameStarter, 10);
            }

            // Team reacts based on choice quality
            if (player.Leadership >= 70)
            {
                // Good choice - small team chemistry boost
                foreach (var p in team.Roster)
                {
                    if (p.PlayerId != playerId)
                    {
                        var pPersonality = _personalityManager.GetPersonality(p.PlayerId);
                        pPersonality?.AdjustMorale(MoraleEvent.Win, 2);
                    }
                }
                result.TeamReaction = "The team approves of the choice.";
            }
            else if (player.Leadership < 50)
            {
                // Poor choice - some players may be upset
                result.TeamReaction = "Some players question the decision.";
            }

            return result;
        }

        /// <summary>
        /// Gets the current team captain.
        /// </summary>
        public Player GetTeamCaptain(Team team)
        {
            return team.Roster?.Find(p => p.IsCaptain);
        }

        #endregion

        #region Contract Satisfaction

        /// <summary>
        /// Updates contract satisfaction for a player who signed a new contract.
        /// </summary>
        public void OnContractSigned(string playerId, int salary, int marketValue)
        {
            var personality = _personalityManager.GetPersonality(playerId);
            if (personality == null) return;

            // Calculate satisfaction based on salary vs market value
            float ratio = (float)salary / Math.Max(1, marketValue);

            if (ratio >= 1.1f)
            {
                // Overpaid - very happy
                personality.ContractSatisfaction = 40;
            }
            else if (ratio >= 0.9f)
            {
                // Fair market value
                personality.ContractSatisfaction = 30;
            }
            else if (ratio >= 0.7f)
            {
                // Slightly underpaid
                personality.ContractSatisfaction = 10;
            }
            else
            {
                // Significantly underpaid
                personality.ContractSatisfaction = -10;
            }

            personality.DaysSinceContractBoost = 0;

            // Apply morale event
            _personalityManager.ApplyPlayerMoraleEvent(playerId, MoraleEvent.ContractSigned);
        }

        /// <summary>
        /// Updates contract satisfaction daily (decay and year-end effects).
        /// </summary>
        private void ProcessContractSatisfaction(Player player)
        {
            var personality = _personalityManager.GetPersonality(player.PlayerId);
            if (personality == null) return;

            personality.DaysSinceContractBoost++;

            // Contract satisfaction boost decays over time
            if (personality.ContractSatisfaction > 0 && personality.DaysSinceContractBoost > 30)
            {
                personality.ContractSatisfaction = Math.Max(0, personality.ContractSatisfaction - 1);
            }

            // Contract satisfaction affects morale
            int moraleEffect = personality.ContractSatisfaction / 10; // -5 to +5
            if (moraleEffect != 0)
            {
                personality.Morale = Mathf.Clamp(personality.Morale + moraleEffect / 30f, 0, 100);
            }
        }

        #endregion

        #region Escalation System

        /// <summary>
        /// Process daily escalation/de-escalation for all players.
        /// </summary>
        private void ProcessEscalation(Player player, Team team)
        {
            var personality = _personalityManager.GetPersonality(player.PlayerId);
            if (personality == null) return;

            // Track days unhappy/content
            if (personality.Morale < _unhappyThreshold)
            {
                personality.DaysUnhappy++;
                personality.DaysContent = 0;
            }
            else if (personality.Morale > _contentThreshold)
            {
                personality.DaysContent++;
                personality.DaysUnhappy = 0;
            }
            else
            {
                // Reset both when in neutral zone
                personality.DaysUnhappy = Math.Max(0, personality.DaysUnhappy - 1);
                personality.DaysContent = Math.Max(0, personality.DaysContent - 1);
            }

            // Escalate if unhappy too long
            if (personality.DaysUnhappy >= _daysToEscalate && personality.DiscontentLevel < 5)
            {
                personality.DiscontentLevel++;
                personality.DaysUnhappy = 0;
                HandleEscalation(player, personality, team);
            }

            // De-escalate if content long enough
            if (personality.DaysContent >= _daysToDeescalate && personality.DiscontentLevel > 0)
            {
                personality.DiscontentLevel--;
                personality.DaysContent = 0;
            }
        }

        /// <summary>
        /// Handle escalation effects based on level.
        /// </summary>
        private void HandleEscalation(Player player, Personality personality, Team team)
        {
            var evt = new LockerRoomEvent
            {
                InstigatorId = player.PlayerId,
                InstigatorName = player.DisplayName,
                Date = GameManager.Instance?.CurrentDate ?? DateTime.Now
            };

            switch (personality.DiscontentLevel)
            {
                case 1:
                    // Private complaint
                    evt.EventType = LockerRoomEventType.DemandsMoreTouches;
                    evt.Description = $"{player.DisplayName} has privately expressed concerns about their role.";
                    evt.Severity = EventSeverity.Minor;
                    break;

                case 2:
                    // Reduced effort - mental stats penalty handled in GetMentalStatModifier
                    evt.EventType = LockerRoomEventType.DemandsMoreTouches;
                    evt.Description = $"{player.DisplayName}'s effort and focus have noticeably declined.";
                    evt.Severity = EventSeverity.Moderate;
                    break;

                case 3:
                    // Media comments
                    evt.EventType = LockerRoomEventType.OutburstAtCoach;
                    evt.Description = $"{player.DisplayName} made critical comments to the media about their situation.";
                    evt.Severity = EventSeverity.Moderate;
                    break;

                case 4:
                    // Trade request
                    evt.EventType = LockerRoomEventType.DemandsTrade;
                    evt.Description = $"{player.DisplayName} has formally requested a trade.";
                    evt.Severity = EventSeverity.Severe;
                    break;

                case 5:
                    // Holdout
                    evt.EventType = LockerRoomEventType.DemandsTrade;
                    evt.Description = $"{player.DisplayName} is refusing to play until their situation is resolved.";
                    evt.Severity = EventSeverity.Severe;
                    break;
            }

            // Role-specific effects
            if (personality.DiscontentLevel >= 2)
            {
                if (player.Age >= 30 && personality.HasTrait(PersonalityTrait.Mentor))
                {
                    // Veterans stop mentoring
                    evt.Description += " They have stopped mentoring younger players.";
                }

                if (player.IsCaptain)
                {
                    // Unhappy captain hurts team
                    evt.Description += " As captain, their negativity is affecting the locker room.";
                }
            }

            OnLockerRoomEvent?.Invoke(evt);
        }

        /// <summary>
        /// Gets mental stat modifier based on discontent level.
        /// </summary>
        public float GetDiscontentStatModifier(string playerId)
        {
            var personality = _personalityManager.GetPersonality(playerId);
            if (personality == null) return 1f;

            // Level 2+: -5% per level
            return personality.DiscontentLevel switch
            {
                0 => 1f,
                1 => 1f,      // No stat penalty yet
                2 => 0.95f,   // -5%
                3 => 0.90f,   // -10%
                4 => 0.85f,   // -15%
                5 => 0.75f,   // -25% (holdout but still playing somehow)
                _ => 1f
            };
        }

        /// <summary>
        /// Gets discontent level for a player (0-5).
        /// </summary>
        public int GetDiscontentLevel(string playerId)
        {
            var personality = _personalityManager.GetPersonality(playerId);
            return personality?.DiscontentLevel ?? 0;
        }

        #endregion

        #region Player Interventions

        /// <summary>
        /// Call a team meeting to address morale issues.
        /// </summary>
        public TeamMeetingResult CallTeamMeeting(Team team, int coachLeadership)
        {
            var result = new TeamMeetingResult();
            var currentDate = GameManager.Instance?.CurrentDate ?? DateTime.Now;

            // Check cooldown
            if (_lastTeamMeetingDate.HasValue)
            {
                int daysSinceLast = (currentDate - _lastTeamMeetingDate.Value).Days;
                if (daysSinceLast < _meetingCooldownDays)
                {
                    result.Success = false;
                    result.Message = $"Team meeting on cooldown. Wait {_meetingCooldownDays - daysSinceLast} more days.";
                    return result;
                }
            }

            _lastTeamMeetingDate = currentDate;

            // Success chance based on coach leadership
            float successChance = 0.5f + (coachLeadership / 200f); // 50% base + up to 50% from leadership
            bool isSuccess = UnityEngine.Random.value < successChance;

            result.Success = true;

            if (isSuccess)
            {
                // Success: All players get morale boost
                foreach (var player in team.Roster)
                {
                    var personality = _personalityManager.GetPersonality(player.PlayerId);
                    if (personality != null)
                    {
                        personality.Morale = Math.Min(_maxMorale, personality.Morale + 5);
                        player.Morale = personality.Morale;
                    }
                }
                result.MoraleChange = 5;
                result.Message = "The team meeting was a success. Players seem more motivated.";
            }
            else
            {
                // Backfire: Volatile players get upset, others get small boost
                foreach (var player in team.Roster)
                {
                    var personality = _personalityManager.GetPersonality(player.PlayerId);
                    if (personality != null)
                    {
                        if (personality.HasTrait(PersonalityTrait.Volatile))
                        {
                            personality.Morale = Math.Max(_minMorale, personality.Morale - 5);
                        }
                        else
                        {
                            personality.Morale = Math.Min(_maxMorale, personality.Morale + 2);
                        }
                        player.Morale = personality.Morale;
                    }
                }
                result.MoraleChange = 2;
                result.Message = "The meeting didn't go as planned. Some players seemed annoyed.";
                result.BackfiredForVolatile = true;
            }

            return result;
        }

        /// <summary>
        /// Have an individual conversation with a player.
        /// </summary>
        public ConversationResult TalkToPlayer(string playerId, ConversationType conversationType)
        {
            var result = new ConversationResult { PlayerId = playerId };
            var personality = _personalityManager.GetPersonality(playerId);
            var player = GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);

            if (personality == null || player == null)
            {
                result.Success = false;
                result.Message = "Player not found.";
                return result;
            }

            result.Success = true;
            result.PlayerName = player.DisplayName;

            switch (conversationType)
            {
                case ConversationType.PromisePlayingTime:
                    personality.AdjustMorale(MoraleEvent.CoachPraise, 10);
                    personality.ExpectedRole = PlayerRole.Rotation; // Sets expectation
                    result.MoraleChange = 10;
                    result.Message = $"You promised {player.DisplayName} more playing time. They seem satisfied for now.";
                    break;

                case ConversationType.ExplainRole:
                    int change = personality.HasTrait(PersonalityTrait.TeamPlayer) ? 8 : 5;
                    personality.AdjustMorale(MoraleEvent.CoachPraise, change);
                    result.MoraleChange = change;
                    result.Message = $"You explained {player.DisplayName}'s role on the team. They understand better now.";
                    break;

                case ConversationType.Praise:
                    int praiseChange = personality.HasTrait(PersonalityTrait.Sensitive) ? 12 : 8;
                    personality.AdjustMorale(MoraleEvent.CoachPraise, praiseChange);
                    result.MoraleChange = praiseChange;
                    result.Message = $"You praised {player.DisplayName}'s contributions. They're feeling appreciated.";
                    break;

                case ConversationType.Constructive:
                    // No morale change but may improve development
                    if (personality.HasTrait(PersonalityTrait.Sensitive))
                    {
                        personality.AdjustMorale(MoraleEvent.CoachCriticism, -3);
                        result.MoraleChange = -3;
                        result.Message = $"{player.DisplayName} took the constructive feedback personally.";
                    }
                    else
                    {
                        result.MoraleChange = 0;
                        result.Message = $"{player.DisplayName} appreciated the honest feedback.";
                        result.DevelopmentBonus = true;
                    }
                    break;

                case ConversationType.OfferTrade:
                    if (personality.DiscontentLevel >= 3)
                    {
                        personality.AdjustMorale(MoraleEvent.Win, 5);
                        result.MoraleChange = 5;
                        result.Message = $"{player.DisplayName} is relieved you're working on finding them a new home.";
                    }
                    else
                    {
                        personality.AdjustMorale(MoraleEvent.TradeRumors, -8);
                        result.MoraleChange = -8;
                        result.Message = $"{player.DisplayName} is shocked and upset that you want to trade them.";
                    }
                    break;
            }

            // Sync morale
            player.Morale = personality.Morale;

            return result;
        }

        #endregion

        #region Enhanced Pair Chemistry

        /// <summary>
        /// Gets detailed pair chemistry bonuses for gameplay.
        /// </summary>
        public PairChemistryBonus GetDetailedPairChemistry(string player1Id, string player2Id)
        {
            float chemistry = _personalityManager.CalculatePairChemistry(player1Id, player2Id);

            var bonus = new PairChemistryBonus
            {
                RawChemistry = chemistry
            };

            if (chemistry >= 0.5f)
            {
                // High chemistry pair
                bonus.AssistBonus = 0.15f;      // +15% assist success
                bonus.ScreenBonus = 0.10f;      // +10% screen effectiveness
                bonus.DefenseBonus = 0.05f;     // +5% defensive rotation
                bonus.PassFrequency = 1.2f;     // 20% more likely to pass to each other
            }
            else if (chemistry >= 0.2f)
            {
                // Good chemistry
                bonus.AssistBonus = 0.08f;
                bonus.ScreenBonus = 0.05f;
                bonus.DefenseBonus = 0.02f;
                bonus.PassFrequency = 1.1f;
            }
            else if (chemistry <= -0.3f)
            {
                // Poor chemistry
                bonus.AssistBonus = -0.10f;     // -10% assist success
                bonus.ScreenBonus = -0.05f;
                bonus.DefenseBonus = -0.05f;
                bonus.PassFrequency = 0.8f;     // 20% less likely to pass
            }
            else
            {
                // Neutral chemistry
                bonus.AssistBonus = 0f;
                bonus.ScreenBonus = 0f;
                bonus.DefenseBonus = 0f;
                bonus.PassFrequency = 1f;
            }

            return bonus;
        }

        #endregion

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

    /// <summary>
    /// Result of assigning a team captain.
    /// </summary>
    [Serializable]
    public class CaptainAssignmentResult
    {
        public bool Success;
        public string Message;
        public bool IsRisky;
        public bool PreviousCaptainUpset;
        public string TeamReaction;
    }

    /// <summary>
    /// Result of calling a team meeting.
    /// </summary>
    [Serializable]
    public class TeamMeetingResult
    {
        public bool Success;
        public string Message;
        public int MoraleChange;
        public bool BackfiredForVolatile;
    }

    /// <summary>
    /// Types of individual conversations with players.
    /// </summary>
    public enum ConversationType
    {
        PromisePlayingTime,  // +10 morale, creates expectation
        ExplainRole,         // +5 morale, reduces expected role
        Praise,              // +8 morale (Sensitive: +12)
        Constructive,        // ±0 morale (improves development)
        OfferTrade           // Variable based on discontent
    }

    /// <summary>
    /// Result of an individual conversation with a player.
    /// </summary>
    [Serializable]
    public class ConversationResult
    {
        public bool Success;
        public string PlayerId;
        public string PlayerName;
        public string Message;
        public int MoraleChange;
        public bool DevelopmentBonus;
    }

    /// <summary>
    /// Detailed chemistry bonuses for a player pair.
    /// </summary>
    [Serializable]
    public class PairChemistryBonus
    {
        public float RawChemistry;      // -1 to +1
        public float AssistBonus;       // -0.15 to +0.15
        public float ScreenBonus;       // -0.10 to +0.10
        public float DefenseBonus;      // -0.05 to +0.05
        public float PassFrequency;     // 0.8 to 1.2 multiplier
    }

    #endregion
}
