using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Gameplay;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// Enhanced AI behavior for offensive and defensive coordinators.
    /// Provides sophisticated game analysis, contextual suggestions, and delegation support.
    /// Builds on StaffAIDecisionMaker with deeper game-state awareness.
    /// </summary>
    public class CoordinatorAI
    {
        // ==================== COORDINATOR PROFILES ====================
        private UnifiedCareerProfile _offensiveCoordinator;
        private UnifiedCareerProfile _defensiveCoordinator;
        private string _teamId;

        // ==================== GAME CONTEXT ====================
        private GameCoach _gameCoach;
        private GameAnalyticsTracker _analytics;
        private PlayEffectivenessTracker _playTracker;

        // ==================== SUGGESTION TRACKING ====================
        private List<CoordinatorGameSuggestion> _pendingSuggestions = new List<CoordinatorGameSuggestion>();
        private List<CoordinatorGameSuggestion> _acceptedSuggestions = new List<CoordinatorGameSuggestion>();
        private List<CoordinatorGameSuggestion> _rejectedSuggestions = new List<CoordinatorGameSuggestion>();
        private Dictionary<string, int> _suggestionAcceptanceByType = new Dictionary<string, int>();
        private Dictionary<string, int> _suggestionRejectionByType = new Dictionary<string, int>();

        // ==================== DELEGATION STATE ====================
        public bool OffenseDelegated { get; private set; }
        public bool DefenseDelegated { get; private set; }
        public bool SubstitutionsDelegated { get; private set; }
        public bool TimeoutsDelegated { get; private set; }

        // ==================== EVENTS ====================
        public event Action<CoordinatorGameSuggestion> OnSuggestionGenerated;
        public event Action<CoordinatorGameSuggestion, bool> OnSuggestionResolved;
        public event Action<string, string> OnDelegatedDecisionMade;

        // ==================== CONSTANTS ====================
        private const float MIN_SUGGESTION_INTERVAL_SECONDS = 90f;  // Don't suggest too frequently
        private const int MIN_SCORE_DIFF_FOR_ADJUSTMENT = 6;
        private const float HIGH_CONFIDENCE_THRESHOLD = 75f;

        // ==================== CONSTRUCTOR ====================

        public CoordinatorAI(string teamId)
        {
            _teamId = teamId;
        }

        public void Initialize(
            UnifiedCareerProfile offensiveCoordinator,
            UnifiedCareerProfile defensiveCoordinator,
            GameCoach gameCoach)
        {
            _offensiveCoordinator = offensiveCoordinator;
            _defensiveCoordinator = defensiveCoordinator;
            _gameCoach = gameCoach;
            _analytics = gameCoach?.GetAnalyticsTracker();
            _playTracker = gameCoach?.GetPlayTracker();
        }

        public void SetCoordinators(UnifiedCareerProfile offensive, UnifiedCareerProfile defensive)
        {
            _offensiveCoordinator = offensive;
            _defensiveCoordinator = defensive;
        }

        // ==================== DELEGATION CONTROL ====================

        /// <summary>
        /// Delegate offensive play-calling to the offensive coordinator.
        /// </summary>
        public void DelegateOffense(bool delegated)
        {
            OffenseDelegated = delegated;
            Debug.Log($"[CoordinatorAI] Offense delegation: {(delegated ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Delegate defensive scheme adjustments to the defensive coordinator.
        /// </summary>
        public void DelegateDefense(bool delegated)
        {
            DefenseDelegated = delegated;
            Debug.Log($"[CoordinatorAI] Defense delegation: {(delegated ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Delegate substitution decisions to coordinators.
        /// </summary>
        public void DelegateSubstitutions(bool delegated)
        {
            SubstitutionsDelegated = delegated;
            Debug.Log($"[CoordinatorAI] Substitutions delegation: {(delegated ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Delegate timeout decisions.
        /// </summary>
        public void DelegateTimeouts(bool delegated)
        {
            TimeoutsDelegated = delegated;
            Debug.Log($"[CoordinatorAI] Timeouts delegation: {(delegated ? "ON" : "OFF")}");
        }

        // ==================== SUGGESTION GENERATION ====================

        /// <summary>
        /// Analyze current game state and generate coordinator suggestions.
        /// </summary>
        public List<CoordinatorGameSuggestion> AnalyzeAndSuggest(GameState gameState)
        {
            var suggestions = new List<CoordinatorGameSuggestion>();

            // Offensive coordinator analysis
            if (_offensiveCoordinator != null)
            {
                var offensiveSuggestions = GenerateOffensiveSuggestions(gameState);
                suggestions.AddRange(offensiveSuggestions);
            }

            // Defensive coordinator analysis
            if (_defensiveCoordinator != null)
            {
                var defensiveSuggestions = GenerateDefensiveSuggestions(gameState);
                suggestions.AddRange(defensiveSuggestions);
            }

            // Add to pending
            foreach (var suggestion in suggestions)
            {
                _pendingSuggestions.Add(suggestion);
                OnSuggestionGenerated?.Invoke(suggestion);
            }

            return suggestions;
        }

        private List<CoordinatorGameSuggestion> GenerateOffensiveSuggestions(GameState state)
        {
            var suggestions = new List<CoordinatorGameSuggestion>();
            var oc = _offensiveCoordinator;
            if (oc == null) return suggestions;

            float quality = StaffAIDecisionMaker.CalculateDecisionQuality(oc.OffensiveScheme);
            int confidence = CalculateConfidence(oc, true);

            // Pace adjustment
            if (state.ScoreDifferential < -MIN_SCORE_DIFF_FOR_ADJUSTMENT)
            {
                // Behind - consider faster pace
                if (oc.OffensivePhilosophy == CoachingPhilosophy.FastPace || quality > 0.7f)
                {
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.PaceAdjustment,
                        true,
                        "Push the pace",
                        "We're down - attacking in transition could help us get quick buckets and disrupt their rhythm.",
                        confidence,
                        state
                    ));
                }
            }
            else if (state.ScoreDifferential > MIN_SCORE_DIFF_FOR_ADJUSTMENT && state.Quarter >= 4)
            {
                // Ahead late - slow it down
                suggestions.Add(CreateSuggestion(
                    CoordinatorSuggestionType.PaceAdjustment,
                    true,
                    "Control the tempo",
                    "We have the lead - let's run clock and get good shots rather than rushing.",
                    confidence + 10,
                    state
                ));
            }

            // Play type adjustments based on effectiveness
            if (_playTracker != null)
            {
                var recommendations = _playTracker.GetPlayRecommendations();

                if (recommendations.HotPlays.Count > 0)
                {
                    var hotPlay = recommendations.HotPlays[0];
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.PlayCallAdjustment,
                        true,
                        $"Keep running {hotPlay}",
                        $"The {hotPlay} has been working well - let's keep going to it while it's hot.",
                        confidence,
                        state
                    ));
                }

                if (recommendations.ColdPlays.Count > 0)
                {
                    var coldPlay = recommendations.ColdPlays[0];
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.PlayCallAdjustment,
                        true,
                        $"Move away from {coldPlay}",
                        $"The {coldPlay} isn't working today - they've adjusted to it. Let's try something else.",
                        confidence,
                        state
                    ));
                }
            }

            // Shot selection based on analytics
            if (_analytics != null)
            {
                var snapshot = _analytics.GetSnapshot();

                if (snapshot.TeamThreePointPercentage < 0.30f && snapshot.TeamThreePointAttempts > 10)
                {
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.ShotSelection,
                        true,
                        "Attack the paint",
                        "Our three-point shot isn't falling tonight. Let's focus on getting to the rim and the mid-range.",
                        confidence,
                        state
                    ));
                }
                else if (snapshot.TeamThreePointPercentage > 0.40f)
                {
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.ShotSelection,
                        true,
                        "Let it fly",
                        "We're shooting well from three - let's keep spacing the floor and taking open looks.",
                        confidence,
                        state
                    ));
                }
            }

            // Philosophy-based suggestions
            switch (oc.OffensivePhilosophy)
            {
                case CoachingPhilosophy.PickAndRollFocused:
                    if (UnityEngine.Random.value < quality * 0.3f)
                    {
                        suggestions.Add(CreateSuggestion(
                            CoordinatorSuggestionType.SchemeChange,
                            true,
                            "High pick and roll",
                            "Their big is struggling to contain our ball handler. Let's attack with more high PnR.",
                            confidence,
                            state
                        ));
                    }
                    break;

                case CoachingPhilosophy.IsoHeavy:
                    if (UnityEngine.Random.value < quality * 0.3f)
                    {
                        suggestions.Add(CreateSuggestion(
                            CoordinatorSuggestionType.PlayerFocus,
                            true,
                            "Iso our star",
                            "Let's clear out and let our best player work one-on-one. He's got a mismatch.",
                            confidence,
                            state
                        ));
                    }
                    break;

                case CoachingPhilosophy.BallMovement:
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.PlayCallAdjustment,
                        true,
                        "Extra pass offense",
                        "Let's move the ball and find the open man. Make them work on defense.",
                        confidence,
                        state
                    ));
                    break;
            }

            // Filter to most relevant suggestions
            return FilterSuggestions(suggestions, 2);
        }

        private List<CoordinatorGameSuggestion> GenerateDefensiveSuggestions(GameState state)
        {
            var suggestions = new List<CoordinatorGameSuggestion>();
            var dc = _defensiveCoordinator;
            if (dc == null) return suggestions;

            float quality = StaffAIDecisionMaker.CalculateDecisionQuality(dc.DefensiveScheme);
            int confidence = CalculateConfidence(dc, false);

            // Opponent on a run
            if (state.OpponentRunPoints >= 6)
            {
                suggestions.Add(CreateSuggestion(
                    CoordinatorSuggestionType.SchemeChange,
                    false,
                    "Change the look",
                    $"They've scored {state.OpponentRunPoints} straight. We need to show them something different - maybe switch to zone.",
                    confidence + 15,
                    state
                ));
            }

            // Pick and roll defense
            if (_analytics != null)
            {
                var snapshot = _analytics.GetSnapshot();

                if (snapshot.OpponentPaintPoints > 30)
                {
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.SchemeChange,
                        false,
                        "Protect the rim",
                        "They're killing us in the paint. Let's drop our big and wall off the rim.",
                        confidence,
                        state
                    ));
                }

                if (snapshot.OpponentThreePointPercentage > 0.40f && snapshot.OpponentThreePointAttempts > 8)
                {
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.DefensiveAssignment,
                        false,
                        "Close out harder",
                        "They're shooting lights out from three. We need to contest every shot.",
                        confidence,
                        state
                    ));
                }
            }

            // Matchup adjustments
            if (state.Quarter >= 3 && Math.Abs(state.ScoreDifferential) < 10)
            {
                suggestions.Add(CreateSuggestion(
                    CoordinatorSuggestionType.MatchupChange,
                    false,
                    "Switch matchups",
                    "Our current matchups aren't working. Let's try switching our wing defenders.",
                    confidence,
                    state
                ));
            }

            // Philosophy-based
            switch (dc.DefensivePhilosophy)
            {
                case CoachingPhilosophy.Aggressive:
                    if (state.ScoreDifferential < 0 || state.Quarter == 4)
                    {
                        suggestions.Add(CreateSuggestion(
                            CoordinatorSuggestionType.Pressure,
                            false,
                            "Full court pressure",
                            "Let's trap and force turnovers. Make them uncomfortable bringing the ball up.",
                            confidence,
                            state
                        ));
                    }
                    break;

                case CoachingPhilosophy.ZoneHeavy:
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.SchemeChange,
                        false,
                        "2-3 zone",
                        "Let's go zone and make them beat us from outside. They don't have great shooters.",
                        confidence,
                        state
                    ));
                    break;

                case CoachingPhilosophy.SwitchEverything:
                    suggestions.Add(CreateSuggestion(
                        CoordinatorSuggestionType.SchemeChange,
                        false,
                        "Switch everything",
                        "Let's switch all screens and take away their actions. We have the versatility.",
                        confidence,
                        state
                    ));
                    break;
            }

            // Double team their star
            if (state.OpponentStarScoring > 15)
            {
                suggestions.Add(CreateSuggestion(
                    CoordinatorSuggestionType.DefensiveAssignment,
                    false,
                    "Double team their star",
                    "Their guy is going off. Let's send help and make someone else beat us.",
                    confidence + 10,
                    state
                ));
            }

            return FilterSuggestions(suggestions, 2);
        }

        private CoordinatorGameSuggestion CreateSuggestion(
            CoordinatorSuggestionType type,
            bool isOffensive,
            string title,
            string reasoning,
            int confidence,
            GameState state)
        {
            var coordinator = isOffensive ? _offensiveCoordinator : _defensiveCoordinator;

            return new CoordinatorGameSuggestion
            {
                SuggestionId = $"COORD_{Guid.NewGuid().ToString().Substring(0, 8)}",
                CoordinatorId = coordinator?.ProfileId,
                CoordinatorName = coordinator?.PersonName ?? (isOffensive ? "Offensive Coordinator" : "Defensive Coordinator"),
                IsOffensive = isOffensive,
                Type = type,
                Title = title,
                Reasoning = reasoning,
                Confidence = Mathf.Clamp(confidence, 30, 95),
                Quarter = state.Quarter,
                GameClockSeconds = state.GameClockSeconds,
                ScoreDifferential = state.ScoreDifferential,
                Timestamp = DateTime.Now
            };
        }

        private int CalculateConfidence(UnifiedCareerProfile coordinator, bool isOffensive)
        {
            if (coordinator == null) return 50;

            int baseConfidence = 40;
            int rating = isOffensive ? coordinator.OffensiveScheme : coordinator.DefensiveScheme;
            int experience = coordinator.TotalCoachingYears;

            baseConfidence += rating / 3;  // Up to +33 from rating
            baseConfidence += Mathf.Min(experience, 15);  // Up to +15 from experience

            return Mathf.Clamp(baseConfidence, 30, 95);
        }

        private List<CoordinatorGameSuggestion> FilterSuggestions(List<CoordinatorGameSuggestion> suggestions, int maxCount)
        {
            return suggestions
                .OrderByDescending(s => s.Confidence)
                .Take(maxCount)
                .ToList();
        }

        // ==================== SUGGESTION HANDLING ====================

        /// <summary>
        /// Accept a coordinator suggestion.
        /// </summary>
        public void AcceptSuggestion(CoordinatorGameSuggestion suggestion)
        {
            suggestion.WasAccepted = true;
            suggestion.ResolvedTime = DateTime.Now;

            _pendingSuggestions.Remove(suggestion);
            _acceptedSuggestions.Add(suggestion);

            // Track acceptance patterns
            string typeKey = suggestion.Type.ToString();
            if (!_suggestionAcceptanceByType.ContainsKey(typeKey))
                _suggestionAcceptanceByType[typeKey] = 0;
            _suggestionAcceptanceByType[typeKey]++;

            OnSuggestionResolved?.Invoke(suggestion, true);
            Debug.Log($"[CoordinatorAI] Accepted: {suggestion.Title}");
        }

        /// <summary>
        /// Reject a coordinator suggestion.
        /// </summary>
        public void RejectSuggestion(CoordinatorGameSuggestion suggestion, string reason = null)
        {
            suggestion.WasAccepted = false;
            suggestion.RejectionReason = reason;
            suggestion.ResolvedTime = DateTime.Now;

            _pendingSuggestions.Remove(suggestion);
            _rejectedSuggestions.Add(suggestion);

            string typeKey = suggestion.Type.ToString();
            if (!_suggestionRejectionByType.ContainsKey(typeKey))
                _suggestionRejectionByType[typeKey] = 0;
            _suggestionRejectionByType[typeKey]++;

            OnSuggestionResolved?.Invoke(suggestion, false);
            Debug.Log($"[CoordinatorAI] Rejected: {suggestion.Title} ({reason ?? "No reason"})");
        }

        /// <summary>
        /// Get pending suggestions.
        /// </summary>
        public List<CoordinatorGameSuggestion> GetPendingSuggestions()
        {
            return new List<CoordinatorGameSuggestion>(_pendingSuggestions);
        }

        /// <summary>
        /// Get high-priority suggestions only.
        /// </summary>
        public List<CoordinatorGameSuggestion> GetHighPrioritySuggestions()
        {
            return _pendingSuggestions
                .Where(s => s.Confidence >= HIGH_CONFIDENCE_THRESHOLD)
                .OrderByDescending(s => s.Confidence)
                .ToList();
        }

        // ==================== DELEGATED DECISIONS ====================

        /// <summary>
        /// Make an offensive play call when delegated.
        /// </summary>
        public DelegatedPlayCall MakeDelegatedPlayCall(GameState state, List<string> currentLineup)
        {
            if (!OffenseDelegated || _offensiveCoordinator == null)
                return null;

            var oc = _offensiveCoordinator;
            float quality = StaffAIDecisionMaker.CalculateDecisionQuality(oc.OffensiveScheme);

            // Select play based on OC philosophy and quality
            PlayType selectedPlay = SelectPlayByPhilosophy(oc.OffensivePhilosophy, state, quality);
            string reasoning = GeneratePlayCallReasoning(selectedPlay, state);

            var playCall = new DelegatedPlayCall
            {
                PlayType = selectedPlay,
                CoordinatorId = oc.ProfileId,
                CoordinatorName = oc.PersonName,
                Reasoning = reasoning,
                Confidence = CalculateConfidence(oc, true),
                IsOptimalCall = UnityEngine.Random.value < quality
            };

            OnDelegatedDecisionMade?.Invoke("PlayCall", $"{oc.PersonName}: {selectedPlay}");
            return playCall;
        }

        private PlayType SelectPlayByPhilosophy(CoachingPhilosophy philosophy, GameState state, float quality)
        {
            // Weight plays based on philosophy
            var playWeights = new Dictionary<PlayType, float>
            {
                { PlayType.PickAndRoll, 1f },
                { PlayType.MotionOffense, 1f },
                { PlayType.Isolation, 0.5f },
                { PlayType.PostUp, 0.5f },
                { PlayType.SpotUp3, 0.8f },
                { PlayType.Handoff, 0.6f }
            };

            // Adjust weights by philosophy
            switch (philosophy)
            {
                case CoachingPhilosophy.PickAndRollFocused:
                    playWeights[PlayType.PickAndRoll] = 3f;
                    playWeights[PlayType.PickAndPop] = 2f;
                    break;
                case CoachingPhilosophy.IsoHeavy:
                    playWeights[PlayType.Isolation] = 3f;
                    break;
                case CoachingPhilosophy.ThreePointHeavy:
                    playWeights[PlayType.SpotUp3] = 3f;
                    playWeights[PlayType.Floppy] = 2f;
                    break;
                case CoachingPhilosophy.InsideOut:
                    playWeights[PlayType.PostUp] = 3f;
                    playWeights[PlayType.BackdoorCut] = 2f;
                    break;
                case CoachingPhilosophy.BallMovement:
                    playWeights[PlayType.MotionOffense] = 3f;
                    playWeights[PlayType.Handoff] = 2f;
                    break;
                case CoachingPhilosophy.FastPace:
                    playWeights[PlayType.TransitionPush] = 3f;
                    break;
            }

            // Situational adjustments
            if (state.Quarter == 4 && state.GameClockSeconds < 24 && state.ScoreDifferential == 0)
            {
                // Last shot - isolation or best play
                playWeights[PlayType.Isolation] *= 2f;
            }

            // Weighted random selection
            float totalWeight = playWeights.Values.Sum();
            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            foreach (var kvp in playWeights)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative)
                    return kvp.Key;
            }

            return PlayType.MotionOffense;
        }

        private string GeneratePlayCallReasoning(PlayType play, GameState state)
        {
            return play switch
            {
                PlayType.PickAndRoll => "Let's attack with the ball screen and create an advantage.",
                PlayType.Isolation => "Clear out and let our guy work one-on-one.",
                PlayType.PostUp => "Feed the post and let the big man work.",
                PlayType.SpotUp3 => "Swing it and find the open three.",
                PlayType.MotionOffense => "Move the ball and cut. Find the open man.",
                PlayType.TransitionPush => "Push it! Let's get an easy one.",
                PlayType.Handoff => "Run the handoff and create movement.",
                _ => "Run the offense and get a good shot."
            };
        }

        /// <summary>
        /// Make a defensive adjustment when delegated.
        /// </summary>
        public DelegatedDefensiveCall MakeDelegatedDefensiveCall(GameState state)
        {
            if (!DefenseDelegated || _defensiveCoordinator == null)
                return null;

            var dc = _defensiveCoordinator;
            float quality = StaffAIDecisionMaker.CalculateDecisionQuality(dc.DefensiveScheme);

            DefensiveScheme selectedScheme = SelectDefenseByPhilosophy(dc.DefensivePhilosophy, state, quality);
            string reasoning = GenerateDefenseReasoning(selectedScheme, state);

            var call = new DelegatedDefensiveCall
            {
                Scheme = selectedScheme,
                CoordinatorId = dc.ProfileId,
                CoordinatorName = dc.PersonName,
                Reasoning = reasoning,
                Confidence = CalculateConfidence(dc, false),
                IsOptimalCall = UnityEngine.Random.value < quality
            };

            OnDelegatedDecisionMade?.Invoke("Defense", $"{dc.PersonName}: {selectedScheme}");
            return call;
        }

        private DefensiveScheme SelectDefenseByPhilosophy(CoachingPhilosophy philosophy, GameState state, float quality)
        {
            // Situational overrides
            if (state.ScoreDifferential > 15 && state.Quarter >= 4)
            {
                return DefensiveScheme.ManToMan;  // Protect lead
            }

            if (state.OpponentRunPoints >= 8)
            {
                // Change look to stop run
                return quality > 0.7f ? DefensiveScheme.Zone23 : DefensiveScheme.ManToMan;
            }

            return philosophy switch
            {
                CoachingPhilosophy.Aggressive => DefensiveScheme.HalfCourtTrap,
                CoachingPhilosophy.Conservative => DefensiveScheme.ManToMan,
                CoachingPhilosophy.ZoneHeavy => DefensiveScheme.Zone23,
                CoachingPhilosophy.SwitchEverything => DefensiveScheme.SwitchAll,
                CoachingPhilosophy.TrapHeavy => DefensiveScheme.HalfCourtTrap,
                _ => DefensiveScheme.ManToMan
            };
        }

        private string GenerateDefenseReasoning(DefensiveScheme scheme, GameState state)
        {
            return scheme switch
            {
                DefensiveScheme.ManToMan => "Stick to your man and contest everything.",
                DefensiveScheme.Zone23 => "Let's pack the paint and make them shoot from outside.",
                DefensiveScheme.Zone32 => "Extend the zone and take away their threes.",
                DefensiveScheme.SwitchAll => "Switch every screen. No easy looks.",
                DefensiveScheme.HalfCourtTrap => "Trap on the catch and force turnovers.",
                DefensiveScheme.FullCourtPress => "Full court! Make them work to get the ball up.",
                _ => "Lock in and get a stop."
            };
        }

        // ==================== TIMEOUT/SUBSTITUTION DELEGATION ====================

        /// <summary>
        /// Evaluate whether to call a timeout when delegated.
        /// </summary>
        public DelegatedTimeoutDecision EvaluateTimeout(GameState state)
        {
            if (!TimeoutsDelegated) return null;

            bool shouldCall = false;
            string reason = null;

            // Opponent run
            if (state.OpponentRunPoints >= 8)
            {
                shouldCall = true;
                reason = "Stop the run - they've scored 8+ straight.";
            }
            // Close game, late
            else if (state.Quarter == 4 && state.GameClockSeconds < 60 && Math.Abs(state.ScoreDifferential) <= 5)
            {
                shouldCall = true;
                reason = "Set up a play for this crucial possession.";
            }
            // Advance ball
            else if (state.Quarter == 4 && state.GameClockSeconds < 30 && state.ScoreDifferential < 0 && state.HasPossession)
            {
                shouldCall = true;
                reason = "Advance the ball and save time.";
            }

            if (shouldCall)
            {
                var decision = new DelegatedTimeoutDecision
                {
                    ShouldCallTimeout = true,
                    Reason = reason,
                    CoordinatorName = _offensiveCoordinator?.PersonName ?? _defensiveCoordinator?.PersonName ?? "Staff"
                };
                OnDelegatedDecisionMade?.Invoke("Timeout", reason);
                return decision;
            }

            return null;
        }

        // ==================== GAME MANAGEMENT ====================

        /// <summary>
        /// Reset for a new game.
        /// </summary>
        public void ResetForNewGame()
        {
            _pendingSuggestions.Clear();
            _acceptedSuggestions.Clear();
            _rejectedSuggestions.Clear();
        }

        /// <summary>
        /// Get coordinator performance summary for the game.
        /// </summary>
        public CoordinatorGameSummary GetGameSummary()
        {
            return new CoordinatorGameSummary
            {
                TotalSuggestionsGenerated = _acceptedSuggestions.Count + _rejectedSuggestions.Count + _pendingSuggestions.Count,
                SuggestionsAccepted = _acceptedSuggestions.Count,
                SuggestionsRejected = _rejectedSuggestions.Count,
                AcceptanceRate = (_acceptedSuggestions.Count + _rejectedSuggestions.Count) > 0
                    ? (float)_acceptedSuggestions.Count / (_acceptedSuggestions.Count + _rejectedSuggestions.Count)
                    : 0f,
                OffensiveCoordinatorName = _offensiveCoordinator?.PersonName,
                DefensiveCoordinatorName = _defensiveCoordinator?.PersonName,
                AcceptedSuggestions = new List<CoordinatorGameSuggestion>(_acceptedSuggestions),
                RejectedSuggestions = new List<CoordinatorGameSuggestion>(_rejectedSuggestions)
            };
        }
    }

    // ==================== DATA CLASSES ====================

    /// <summary>
    /// Types of coordinator suggestions.
    /// </summary>
    public enum CoordinatorSuggestionType
    {
        PaceAdjustment,
        PlayCallAdjustment,
        SchemeChange,
        MatchupChange,
        DefensiveAssignment,
        PlayerFocus,
        ShotSelection,
        Pressure,
        SubstitutionSuggestion,
        TimeoutSuggestion
    }

    /// <summary>
    /// A suggestion from a coordinator during a game.
    /// </summary>
    [Serializable]
    public class CoordinatorGameSuggestion
    {
        public string SuggestionId;
        public string CoordinatorId;
        public string CoordinatorName;
        public bool IsOffensive;

        [Header("Suggestion Content")]
        public CoordinatorSuggestionType Type;
        public string Title;
        public string Reasoning;
        [Range(0, 100)] public int Confidence;

        [Header("Context")]
        public int Quarter;
        public float GameClockSeconds;
        public int ScoreDifferential;

        [Header("Resolution")]
        public bool? WasAccepted;
        public string RejectionReason;
        public DateTime Timestamp;
        public DateTime? ResolvedTime;

        public bool IsPending => !WasAccepted.HasValue;
        public bool IsHighPriority => Confidence >= 75;
    }

    /// <summary>
    /// A play call made by delegated coordinator.
    /// </summary>
    [Serializable]
    public class DelegatedPlayCall
    {
        public PlayType PlayType;
        public string CoordinatorId;
        public string CoordinatorName;
        public string Reasoning;
        public int Confidence;
        public bool IsOptimalCall;
    }

    /// <summary>
    /// A defensive call made by delegated coordinator.
    /// </summary>
    [Serializable]
    public class DelegatedDefensiveCall
    {
        public DefensiveScheme Scheme;
        public string CoordinatorId;
        public string CoordinatorName;
        public string Reasoning;
        public int Confidence;
        public bool IsOptimalCall;
    }

    /// <summary>
    /// Timeout decision by delegated coordinator.
    /// </summary>
    [Serializable]
    public class DelegatedTimeoutDecision
    {
        public bool ShouldCallTimeout;
        public string Reason;
        public string CoordinatorName;
    }

    /// <summary>
    /// Summary of coordinator contributions during a game.
    /// </summary>
    [Serializable]
    public class CoordinatorGameSummary
    {
        public int TotalSuggestionsGenerated;
        public int SuggestionsAccepted;
        public int SuggestionsRejected;
        public float AcceptanceRate;
        public string OffensiveCoordinatorName;
        public string DefensiveCoordinatorName;
        public List<CoordinatorGameSuggestion> AcceptedSuggestions;
        public List<CoordinatorGameSuggestion> RejectedSuggestions;
    }

    /// <summary>
    /// Game state snapshot for coordinator analysis.
    /// </summary>
    [Serializable]
    public class GameState
    {
        public int Quarter;
        public float GameClockSeconds;
        public int TeamScore;
        public int OpponentScore;
        public int ScoreDifferential => TeamScore - OpponentScore;
        public bool HasPossession;
        public int OpponentRunPoints;
        public int TeamRunPoints;
        public float Momentum;  // 0-100, 50 is neutral
        public int OpponentStarScoring;  // Points by opponent's best player
        public string OpponentTeamId;
    }
}
