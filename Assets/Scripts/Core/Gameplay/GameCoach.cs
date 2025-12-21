using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core.Gameplay
{
    /// <summary>
    /// Central controller for in-game coaching decisions.
    /// Manages tactics, substitutions, timeouts, play calls, matchups, and end-game situations.
    /// NBA-level tactical depth with granular control.
    /// </summary>
    public class GameCoach
    {
        // ==================== STRATEGY & TACTICS ====================
        private GameTactics _tactics;
        private TeamStrategy _teamStrategy;
        private PlayBook _playbook;
        private TeamGameInstructions _playerInstructions;

        // ==================== ROTATION & SUBSTITUTIONS ====================
        private SubstitutionPlan _subPlan;
        private List<string> _currentLineup = new List<string>();
        private Dictionary<string, float> _playerMinutes = new Dictionary<string, float>();
        private Dictionary<string, int> _playerFouls = new Dictionary<string, int>();
        private Dictionary<string, float> _playerEnergy = new Dictionary<string, float>();

        // ==================== MATCHUPS ====================
        private List<Matchup> _matchups = new List<Matchup>();
        private Dictionary<string, DoubleTeamTrigger> _doubleTeamTriggers = new Dictionary<string, DoubleTeamTrigger>();

        // ==================== RESOURCES ====================
        public int TimeoutsRemaining { get; private set; } = 7;
        public int TimeoutsUsedFirstHalf { get; private set; } = 0;
        public bool CoachEjected { get; private set; } = false;
        public int TechnicalFouls { get; private set; } = 0;
        public int ChallengePossession { get; private set; } = 1;  // 1 per game
        public bool ChallengeUsed { get; private set; } = false;

        // ==================== GAME CONTEXT ====================
        private int _currentQuarter = 1;
        private float _gameClockSeconds = 720f;
        private float _shotClockSeconds = 24f;
        private int _teamScore = 0;
        private int _opponentScore = 0;
        private int _opponentRunPoints = 0;
        private int _teamRunPoints = 0;
        private bool _hasPossession = false;
        private bool _isTimeout = false;
        private int _foulsToGive = 4;

        // ==================== MOMENTUM ====================
        private float _momentum = 50f;  // 0-100, 50 is neutral
        private int _consecutiveStops = 0;
        private int _consecutiveScores = 0;

        // ==================== ANALYTICS & ADVISOR ====================
        private GameAnalyticsTracker _analyticsTracker;
        private CoachingAdvisor _coachingAdvisor;
        private PlayEffectivenessTracker _playTracker;
        private MatchupEvaluator _matchupEvaluator;

        // ==================== EVENTS ====================
        public event Action<string> OnCoachDecision;
        public event Action<TimeoutResult> OnTimeoutCalled;
        public event Action<SubstitutionResult> OnSubstitution;
        public event Action<SetPlay> OnPlayCalled;

        // ==================== CONSTRUCTOR ====================

        public GameCoach()
        {
            _tactics = new GameTactics();
            _subPlan = new SubstitutionPlan();
            InitializeAnalytics();
        }

        public GameCoach(TeamStrategy strategy, PlayBook playbook, TeamGameInstructions instructions)
        {
            _teamStrategy = strategy;
            _playbook = playbook;
            _playerInstructions = instructions;
            _tactics = new GameTactics();
            _subPlan = new SubstitutionPlan();
            InitializeAnalytics();

            // Initialize tactics from strategy
            if (strategy != null)
            {
                ApplyTeamStrategy(strategy);
            }
        }

        private void InitializeAnalytics()
        {
            _analyticsTracker = new GameAnalyticsTracker();
            _playTracker = new PlayEffectivenessTracker();
        }

        /// <summary>
        /// Initializes analytics with player lookup functions for full functionality.
        /// </summary>
        public void InitializeAnalytics(Func<string, Player> getPlayer, Func<string, string> getPlayerName)
        {
            _analyticsTracker = new GameAnalyticsTracker();
            _playTracker = new PlayEffectivenessTracker();
            _matchupEvaluator = new MatchupEvaluator(getPlayer);
            _coachingAdvisor = new CoachingAdvisor(
                _analyticsTracker,
                _playTracker,
                _matchupEvaluator,
                getPlayer,
                getPlayerName
            );
        }

        // ==================== INITIALIZATION ====================

        public void SetTeamStrategy(TeamStrategy strategy)
        {
            _teamStrategy = strategy;
            ApplyTeamStrategy(strategy);
        }

        public void SetPlaybook(PlayBook playbook) => _playbook = playbook;
        public void SetPlayerInstructions(TeamGameInstructions instructions) => _playerInstructions = instructions;

        private void ApplyTeamStrategy(TeamStrategy strategy)
        {
            _tactics.Pace = strategy.PacePreference switch
            {
                PacePreference.Deliberate => GamePace.Slow,
                PacePreference.Balanced => GamePace.Normal,
                PacePreference.PushWhenPossible => GamePace.Push,
                PacePreference.AlwaysPush => GamePace.Push,
                _ => GamePace.Normal
            };

            _tactics.Offense = strategy.OffensiveSystem.PrimarySystem switch
            {
                OffensiveSystemType.MotionOffense => OffensiveScheme.Motion,
                OffensiveSystemType.PickAndRollHeavy => OffensiveScheme.PickAndRoll,
                OffensiveSystemType.IsoHeavy => OffensiveScheme.Isolation,
                OffensiveSystemType.PostUpFocused => OffensiveScheme.PostUp,
                OffensiveSystemType.ThreePointOriented => OffensiveScheme.ThreeHeavy,
                OffensiveSystemType.FastBreakTransition => OffensiveScheme.FastBreak,
                _ => OffensiveScheme.Motion
            };

            _tactics.Defense = strategy.DefensiveSystem.PrimaryScheme switch
            {
                DefensiveSchemeType.ManToManStandard => DefensiveScheme.ManToMan,
                DefensiveSchemeType.ManToManAggressive => DefensiveScheme.ManToMan,
                DefensiveSchemeType.Zone2_3 => DefensiveScheme.Zone23,
                DefensiveSchemeType.Zone3_2 => DefensiveScheme.Zone32,
                DefensiveSchemeType.Zone1_3_1 => DefensiveScheme.Zone131,
                DefensiveSchemeType.SwitchEverything => DefensiveScheme.SwitchAll,
                DefensiveSchemeType.FullCourtPress => DefensiveScheme.FullCourtPress,
                _ => DefensiveScheme.ManToMan
            };
        }

        // ==================== TACTICS ====================

        public GameTactics GetTactics() => _tactics;

        public void SetOffense(OffensiveScheme scheme)
        {
            _tactics.Offense = scheme;
            OnCoachDecision?.Invoke($"Offense: {scheme}");
            Debug.Log($"[GameCoach] Offense set to: {scheme}");
        }

        public void SetDefense(DefensiveScheme scheme)
        {
            _tactics.Defense = scheme;
            OnCoachDecision?.Invoke($"Defense: {scheme}");
            Debug.Log($"[GameCoach] Defense set to: {scheme}");
        }

        public void SetPace(GamePace pace)
        {
            _tactics.Pace = pace;
            OnCoachDecision?.Invoke($"Pace: {pace}");
            Debug.Log($"[GameCoach] Pace set to: {pace}");
        }

        public void SetIntensity(IntensityLevel intensity)
        {
            _tactics.Intensity = intensity;
            OnCoachDecision?.Invoke($"Intensity: {intensity}");
            Debug.Log($"[GameCoach] Intensity set to: {intensity}");
        }

        /// <summary>
        /// Sets pick and roll coverage scheme.
        /// </summary>
        public void SetPickAndRollCoverage(PnRCoverageScheme coverage)
        {
            _tactics.PnRCoverage = coverage;
            OnCoachDecision?.Invoke($"PnR Coverage: {coverage}");
            Debug.Log($"[GameCoach] PnR Coverage set to: {coverage}");
        }

        /// <summary>
        /// Sets transition defense strategy.
        /// </summary>
        public void SetTransitionDefense(TransitionDefenseLevel level)
        {
            _tactics.TransitionDefense = level;
            Debug.Log($"[GameCoach] Transition Defense set to: {level}");
        }

        // ==================== MATCHUPS ====================

        public void SetDefensiveMatchup(string defenderId, string opponentId, MatchupPriority priority = MatchupPriority.Normal)
        {
            _matchups.RemoveAll(m => m.DefenderId == defenderId);
            _matchups.Add(new Matchup
            {
                DefenderId = defenderId,
                OpponentId = opponentId,
                Priority = priority
            });
            Debug.Log($"[GameCoach] Matchup: {defenderId} guards {opponentId} ({priority})");
        }

        public void SetDoubleTeam(string opponentId, DoubleTeamTrigger trigger)
        {
            _doubleTeamTriggers[opponentId] = trigger;
            _tactics.DoubleTeamTarget = opponentId;
            Debug.Log($"[GameCoach] Double team {opponentId} on {trigger}");
        }

        public void ClearDoubleTeam(string opponentId)
        {
            _doubleTeamTriggers.Remove(opponentId);
            if (_tactics.DoubleTeamTarget == opponentId)
                _tactics.DoubleTeamTarget = null;
        }

        public string GetDefenderFor(string opponentId)
        {
            return _matchups.FirstOrDefault(m => m.OpponentId == opponentId)?.DefenderId;
        }

        public List<Matchup> GetAllMatchups() => _matchups.ToList();

        // ==================== SUBSTITUTIONS ====================

        public SubstitutionResult Substitute(string playerOut, string playerIn, List<string> currentLineup)
        {
            if (!currentLineup.Contains(playerOut))
            {
                return new SubstitutionResult { Success = false, Reason = $"{playerOut} not in lineup" };
            }

            var newLineup = currentLineup.ToList();
            newLineup.Remove(playerOut);
            newLineup.Add(playerIn);

            var result = new SubstitutionResult
            {
                Success = true,
                NewLineup = newLineup,
                PlayerOut = playerOut,
                PlayerIn = playerIn
            };

            _currentLineup = newLineup;
            OnSubstitution?.Invoke(result);
            Debug.Log($"[GameCoach] Substitution: {playerOut} out, {playerIn} in");

            return result;
        }

        /// <summary>
        /// Makes multiple substitutions at once.
        /// </summary>
        public SubstitutionResult SubstituteMultiple(List<string> playersOut, List<string> playersIn, List<string> currentLineup)
        {
            if (playersOut.Count != playersIn.Count)
                return new SubstitutionResult { Success = false, Reason = "Unequal substitution counts" };

            var newLineup = currentLineup.ToList();

            for (int i = 0; i < playersOut.Count; i++)
            {
                if (!newLineup.Contains(playersOut[i]))
                    return new SubstitutionResult { Success = false, Reason = $"{playersOut[i]} not in lineup" };

                newLineup.Remove(playersOut[i]);
                newLineup.Add(playersIn[i]);
            }

            _currentLineup = newLineup;

            return new SubstitutionResult
            {
                Success = true,
                NewLineup = newLineup,
                PlayerOut = string.Join(", ", playersOut),
                PlayerIn = string.Join(", ", playersIn)
            };
        }

        /// <summary>
        /// Gets substitution suggestion based on current game state.
        /// </summary>
        public List<SubstitutionSuggestion> GetSubstitutionSuggestions(List<string> currentLineup, Dictionary<string, float> playerEnergy)
        {
            var suggestions = new List<SubstitutionSuggestion>();

            foreach (var playerId in currentLineup)
            {
                if (playerEnergy.TryGetValue(playerId, out var energy))
                {
                    if (energy < _subPlan.FatigueThreshold)
                    {
                        suggestions.Add(new SubstitutionSuggestion
                        {
                            PlayerOut = playerId,
                            Reason = $"Low energy ({energy:F0}%)",
                            Urgency = SubstitutionUrgency.High
                        });
                    }
                }

                // Foul trouble
                if (_playerFouls.TryGetValue(playerId, out var fouls))
                {
                    int maxFouls = _currentQuarter <= 2 ? 2 : 4;
                    if (fouls >= maxFouls)
                    {
                        suggestions.Add(new SubstitutionSuggestion
                        {
                            PlayerOut = playerId,
                            Reason = $"Foul trouble ({fouls} fouls)",
                            Urgency = SubstitutionUrgency.Medium
                        });
                    }
                }
            }

            return suggestions.OrderByDescending(s => s.Urgency).ToList();
        }

        public void SetFatigueThreshold(int threshold)
        {
            _subPlan.FatigueThreshold = Mathf.Clamp(threshold, 50, 100);
        }

        public bool ShouldAutoSub(string playerId, float currentEnergy)
        {
            return currentEnergy < _subPlan.FatigueThreshold;
        }

        public void SetRotation(Dictionary<string, int> targetMinutes)
        {
            _subPlan.TargetMinutes = targetMinutes;
        }

        public void SetRotationDepth(int depth)
        {
            _subPlan.RotationDepth = Mathf.Clamp(depth, 7, 12);
        }

        // ==================== TIMEOUTS ====================

        public TimeoutResult CallTimeout(TimeoutReason reason)
        {
            if (TimeoutsRemaining <= 0)
                return new TimeoutResult { Success = false, Reason = "No timeouts remaining" };

            if (_currentQuarter <= 2)
                TimeoutsUsedFirstHalf++;

            TimeoutsRemaining--;
            _isTimeout = true;
            _opponentRunPoints = 0;  // Reset opponent run

            var result = new TimeoutResult
            {
                Success = true,
                TimeoutsLeft = TimeoutsRemaining,
                Reason = reason.ToString()
            };

            OnTimeoutCalled?.Invoke(result);
            Debug.Log($"[GameCoach] Timeout called ({reason}). {TimeoutsRemaining} remaining.");

            return result;
        }

        /// <summary>
        /// Determines if timeout should be called based on game situation.
        /// </summary>
        public (bool shouldCall, TimeoutReason reason) ShouldCallTimeout()
        {
            // Opponent on big run
            if (_opponentRunPoints >= 8)
                return (true, TimeoutReason.StopRun);

            // Close game, late, need to set up play
            if (_currentQuarter == 4 && _gameClockSeconds < 60 && Math.Abs(_teamScore - _opponentScore) <= 5)
                return (true, TimeoutReason.EndOfGame);

            // Advance ball in late game when behind
            if (_currentQuarter == 4 && _gameClockSeconds < 30 && _teamScore < _opponentScore && _hasPossession)
                return (true, TimeoutReason.AdvanceBall);

            // Mandatory timeout check
            if (_currentQuarter <= 2 && TimeoutsUsedFirstHalf == 0 && _gameClockSeconds < 180)
                return (true, TimeoutReason.Mandatory);

            return (false, TimeoutReason.StopRun);
        }

        public void RecordOpponentPoints(int points)
        {
            _opponentRunPoints += points;
            _opponentScore += points;
            _teamRunPoints = 0;
            _consecutiveStops = 0;
            _momentum = Mathf.Max(0, _momentum - points * 2);
        }

        public void RecordTeamPoints(int points)
        {
            _teamRunPoints += points;
            _teamScore += points;
            _opponentRunPoints = 0;
            _consecutiveScores++;
            _momentum = Mathf.Min(100, _momentum + points * 2);
        }

        public void RecordDefensiveStop()
        {
            _consecutiveStops++;
            _momentum = Mathf.Min(100, _momentum + 3);
        }

        public void ResetRun()
        {
            _opponentRunPoints = 0;
            _teamRunPoints = 0;
        }

        // ==================== PLAY CALLS ====================

        /// <summary>
        /// Calls a set play from the playbook.
        /// </summary>
        public SetPlay CallSetPlay(string playId)
        {
            if (_playbook == null)
            {
                Debug.LogWarning("[GameCoach] No playbook loaded");
                return null;
            }

            var play = _playbook.GetPlay(playId);
            if (play == null)
            {
                Debug.LogWarning($"[GameCoach] Play {playId} not found");
                return null;
            }

            OnPlayCalled?.Invoke(play);
            Debug.Log($"[GameCoach] Called play: {play.PlayName}");
            return play;
        }

        /// <summary>
        /// Gets recommended plays for current game situation.
        /// </summary>
        public List<SetPlay> GetRecommendedPlays()
        {
            if (_playbook == null)
                return new List<SetPlay>();

            var state = new PlaySelectionState
            {
                Quarter = _currentQuarter,
                ClockSeconds = _gameClockSeconds,
                ScoreDiff = _teamScore - _opponentScore,
                JustCalledTimeout = _isTimeout,
                ShotClock = _shotClockSeconds
            };

            return _playbook.GetRecommendedPlays(state);
        }

        /// <summary>
        /// Calls a quick action (simplified play).
        /// </summary>
        public PlayCall CallQuickAction(QuickActionType action, string primaryPlayer = null)
        {
            var playType = action switch
            {
                QuickActionType.Isolation => PlayType.Isolation,
                QuickActionType.PickAndRoll => PlayType.PickAndRoll,
                QuickActionType.PostUp => PlayType.PostUp,
                QuickActionType.ShooterAction => PlayType.SpotUp3,
                QuickActionType.Transition => PlayType.TransitionPush,
                QuickActionType.SlowDown => PlayType.MotionOffense,
                _ => PlayType.MotionOffense
            };

            return new PlayCall
            {
                Type = playType,
                PrimaryPlayerId = primaryPlayer,
                Timestamp = _gameClockSeconds
            };
        }

        public PlayCall CallPlay(PlayType type, string primaryPlayer = null, string secondaryPlayer = null)
        {
            return new PlayCall
            {
                Type = type,
                PrimaryPlayerId = primaryPlayer,
                SecondaryPlayerId = secondaryPlayer,
                Timestamp = _gameClockSeconds
            };
        }

        public PlayCall CallATOPlay(string primaryPlayer)
        {
            if (_playbook != null)
            {
                var atoPlay = _playbook.GetBestPlayForSituation(PlaySituation.AfterTimeout);
                if (atoPlay != null)
                {
                    OnPlayCalled?.Invoke(atoPlay);
                }
            }

            return new PlayCall
            {
                Type = PlayType.ATOSpecial,
                PrimaryPlayerId = primaryPlayer,
                IsATO = true,
                Timestamp = _gameClockSeconds
            };
        }

        public List<PlayType> GetAvailablePlays(List<string> currentLineup)
        {
            return new List<PlayType>
            {
                PlayType.PickAndRoll,
                PlayType.PickAndPop,
                PlayType.Isolation,
                PlayType.PostUp,
                PlayType.SpotUp3,
                PlayType.TransitionPush,
                PlayType.MotionOffense,
                PlayType.Handoff,
                PlayType.BackdoorCut,
                PlayType.LobPlay,
                PlayType.ATOSpecial,
                PlayType.SpainPnR,
                PlayType.HornsAction,
                PlayType.FlexCut,
                PlayType.Floppy
            };
        }

        // ==================== END OF GAME MANAGEMENT ====================

        /// <summary>
        /// Gets coaching action recommendation for end-game situation.
        /// </summary>
        public EndGameDecision GetEndGameRecommendation()
        {
            int scoreDiff = _teamScore - _opponentScore;
            float clock = _gameClockSeconds;

            // We have the ball
            if (_hasPossession)
            {
                if (scoreDiff == 0 && clock < 24)
                {
                    return new EndGameDecision
                    {
                        Action = EndGameAction.HoldForLastShot,
                        TargetShotClock = 4,
                        Explanation = "Tie game - hold for last shot"
                    };
                }
                if (scoreDiff < 0 && scoreDiff >= -3 && clock < 30)
                {
                    return new EndGameDecision
                    {
                        Action = EndGameAction.QuickTwoOrThree,
                        Explanation = scoreDiff == -3 ? "Down 3 - need quick three" : "Down 1-2 - quick score"
                    };
                }
                if (scoreDiff > 0 && clock < 24)
                {
                    return new EndGameDecision
                    {
                        Action = EndGameAction.RunClock,
                        Explanation = "Leading - run clock, get fouled"
                    };
                }
            }
            // Defense
            else
            {
                if (scoreDiff == 3 && clock < 15)
                {
                    return new EndGameDecision
                    {
                        Action = EndGameAction.FoulToPreventThree,
                        Explanation = "Up 3 - consider fouling to prevent tie"
                    };
                }
                if (scoreDiff > 3 && clock < 24)
                {
                    return new EndGameDecision
                    {
                        Action = EndGameAction.NoFoul,
                        Explanation = "Comfortable lead - play defense"
                    };
                }
                if (scoreDiff < 0 && clock < 24 && _foulsToGive > 0)
                {
                    return new EndGameDecision
                    {
                        Action = EndGameAction.FoulToStopClock,
                        Explanation = "Behind - foul to stop clock"
                    };
                }
            }

            return new EndGameDecision
            {
                Action = EndGameAction.PlayNormal,
                Explanation = "Continue normal play"
            };
        }

        /// <summary>
        /// Intentionally fouls to stop clock.
        /// </summary>
        public FoulResult IntentionalFoul(string foulingPlayerId)
        {
            if (_foulsToGive <= 0)
            {
                return new FoulResult
                {
                    Success = true,
                    FreeThrows = true,
                    Message = "Foul committed - opponent shoots free throws"
                };
            }

            _foulsToGive--;
            return new FoulResult
            {
                Success = true,
                FreeThrows = false,
                Message = $"Foul to give used. {_foulsToGive} remaining"
            };
        }

        // ==================== COACH'S CHALLENGE ====================

        /// <summary>
        /// Uses coach's challenge on a call.
        /// </summary>
        public ChallengeResult UseChallenge(ChallengeType challengeType, float successProbability = 0.4f)
        {
            if (ChallengeUsed)
                return new ChallengeResult { Success = false, Message = "Challenge already used" };

            if (TimeoutsRemaining <= 0)
                return new ChallengeResult { Success = false, Message = "No timeouts to risk" };

            ChallengeUsed = true;

            var rng = new System.Random();
            bool won = rng.NextDouble() < successProbability;

            if (!won)
            {
                TimeoutsRemaining--;  // Lose timeout if challenge fails
            }

            Debug.Log($"[GameCoach] Challenge {(won ? "WON" : "LOST")} ({challengeType})");

            return new ChallengeResult
            {
                Success = true,
                ChallengeWon = won,
                Message = won ? "Challenge successful!" : "Challenge unsuccessful. Timeout charged.",
                TimeoutCharged = !won
            };
        }

        // ==================== TECHNICAL FOULS ====================

        public TechnicalResult ArgueCall(int teamMorale, System.Random rng = null)
        {
            rng ??= new System.Random();

            if (CoachEjected)
                return new TechnicalResult { WasEjected = true, Message = "Coach already ejected!" };

            float techChance = TechnicalFouls > 0 ? 0.6f : 0.3f;
            bool gotTech = rng.NextDouble() < techChance;

            if (gotTech)
            {
                TechnicalFouls++;

                if (TechnicalFouls >= 2)
                {
                    CoachEjected = true;
                    return new TechnicalResult
                    {
                        GotTechnical = true,
                        WasEjected = true,
                        MoraleChange = -10,
                        Message = "Coach ejected! Team demoralized."
                    };
                }

                bool rallied = rng.NextDouble() < 0.6f;
                return new TechnicalResult
                {
                    GotTechnical = true,
                    WasEjected = false,
                    MoraleChange = rallied ? 8 : -3,
                    Message = rallied ? "Coach fired up! Team rallied!" : "Technical foul. No rally effect."
                };
            }

            return new TechnicalResult
            {
                GotTechnical = false,
                WasEjected = false,
                MoraleChange = 3,
                Message = "Coach makes his point. Team appreciates the fire."
            };
        }

        // ==================== GAME STATE UPDATE ====================

        public void UpdateGameState(int quarter, float clockSeconds, float shotClock, int teamScore, int opponentScore, bool hasPossession)
        {
            _currentQuarter = quarter;
            _gameClockSeconds = clockSeconds;
            _shotClockSeconds = shotClock;
            _teamScore = teamScore;
            _opponentScore = opponentScore;
            _hasPossession = hasPossession;
            _isTimeout = false;

            // Reset fouls to give each quarter
            if (quarter != _currentQuarter)
            {
                _foulsToGive = 4;
            }
        }

        public void UpdateGameState(int quarter, float clockSeconds, int teamScore, int opponentScore)
        {
            UpdateGameState(quarter, clockSeconds, 24f, teamScore, opponentScore, true);
        }

        public void RecordPlayerFoul(string playerId)
        {
            if (!_playerFouls.ContainsKey(playerId))
                _playerFouls[playerId] = 0;
            _playerFouls[playerId]++;
        }

        public int GetPlayerFouls(string playerId)
        {
            return _playerFouls.TryGetValue(playerId, out var fouls) ? fouls : 0;
        }

        public void ResetForNewGame()
        {
            TimeoutsRemaining = 7;
            TimeoutsUsedFirstHalf = 0;
            TechnicalFouls = 0;
            CoachEjected = false;
            ChallengeUsed = false;
            _opponentRunPoints = 0;
            _teamRunPoints = 0;
            _currentQuarter = 1;
            _gameClockSeconds = 720f;
            _shotClockSeconds = 24f;
            _momentum = 50f;
            _consecutiveStops = 0;
            _consecutiveScores = 0;
            _foulsToGive = 4;
            _playerFouls.Clear();
            _playerMinutes.Clear();
            _matchups.Clear();
            _doubleTeamTriggers.Clear();
            _tactics = new GameTactics();

            // Reset analytics systems
            _analyticsTracker?.ResetForNewGame();
            _coachingAdvisor?.ResetForNewGame();
            _playTracker?.ResetForNewGame();
            _matchupEvaluator?.ResetForNewGame();

            if (_teamStrategy != null)
                ApplyTeamStrategy(_teamStrategy);
        }

        // ==================== GETTERS ====================

        public int CurrentQuarter => _currentQuarter;
        public float GameClock => _gameClockSeconds;
        public float ShotClock => _shotClockSeconds;
        public int TeamScore => _teamScore;
        public int OpponentScore => _opponentScore;
        public int ScoreDiff => _teamScore - _opponentScore;
        public float Momentum => _momentum;
        public bool HasPossession => _hasPossession;
        public bool IsClutchTime => _currentQuarter >= 4 && _gameClockSeconds < 300 && Math.Abs(_teamScore - _opponentScore) <= 10;

        // ==================== ANALYTICS ACCESS ====================

        /// <summary>
        /// Gets the game analytics tracker for real-time statistics.
        /// </summary>
        public GameAnalyticsTracker GetAnalyticsTracker() => _analyticsTracker;

        /// <summary>
        /// Gets the coaching advisor for AI suggestions.
        /// </summary>
        public CoachingAdvisor GetCoachingAdvisor() => _coachingAdvisor;

        /// <summary>
        /// Gets the play effectiveness tracker for hot/cold play analysis.
        /// </summary>
        public PlayEffectivenessTracker GetPlayTracker() => _playTracker;

        /// <summary>
        /// Gets the matchup evaluator for defensive assignments.
        /// </summary>
        public MatchupEvaluator GetMatchupEvaluator() => _matchupEvaluator;

        /// <summary>
        /// Gets current game analytics snapshot.
        /// </summary>
        public GameAnalyticsSnapshot GetCurrentAnalytics()
        {
            return _analyticsTracker?.GetSnapshot() ?? new GameAnalyticsSnapshot();
        }

        /// <summary>
        /// Gets pending coaching suggestions.
        /// </summary>
        public List<CoachingSuggestion> GetAdvisorSuggestions()
        {
            return _coachingAdvisor?.GetPendingSuggestions() ?? new List<CoachingSuggestion>();
        }

        /// <summary>
        /// Gets high priority coaching suggestions only.
        /// </summary>
        public List<CoachingSuggestion> GetUrgentSuggestions()
        {
            return _coachingAdvisor?.GetHighPrioritySuggestions() ?? new List<CoachingSuggestion>();
        }

        /// <summary>
        /// Triggers analysis and returns new suggestions.
        /// </summary>
        public List<CoachingSuggestion> AnalyzeAndGetSuggestions()
        {
            return _coachingAdvisor?.AnalyzeAndSuggest(this, _currentLineup) ?? new List<CoachingSuggestion>();
        }

        /// <summary>
        /// Accepts a coaching suggestion (for tracking).
        /// </summary>
        public void AcceptSuggestion(CoachingSuggestion suggestion)
        {
            _coachingAdvisor?.AcceptSuggestion(suggestion);
        }

        /// <summary>
        /// Rejects a coaching suggestion.
        /// </summary>
        public void RejectSuggestion(CoachingSuggestion suggestion)
        {
            _coachingAdvisor?.RejectSuggestion(suggestion);
        }

        /// <summary>
        /// Gets play effectiveness recommendations.
        /// </summary>
        public PlayRecommendations GetPlayRecommendations()
        {
            return _playTracker?.GetPlayRecommendations() ?? new PlayRecommendations();
        }

        /// <summary>
        /// Evaluates a defensive matchup quality (0-100, higher = better for defender).
        /// </summary>
        public float EvaluateMatchup(string defenderId, string offenderId)
        {
            return _matchupEvaluator?.EvaluateDefensiveMatchup(defenderId, offenderId) ?? 50f;
        }

        /// <summary>
        /// Gets hunt/hide matchup recommendations.
        /// </summary>
        public (List<HuntHideRecommendation> hunt, List<HuntHideRecommendation> hide) GetMatchupRecommendations(
            List<string> ourLineup, List<string> opponentLineup)
        {
            if (_matchupEvaluator == null)
                return (new List<HuntHideRecommendation>(), new List<HuntHideRecommendation>());

            var hunt = _matchupEvaluator.GetHuntRecommendations(ourLineup, opponentLineup);
            var hide = _matchupEvaluator.GetHideRecommendations(ourLineup, opponentLineup);
            return (hunt, hide);
        }
    }

    // ==================== EXPANDED ENUMS ====================

    [Serializable]
    public class GameTactics
    {
        public OffensiveScheme Offense = OffensiveScheme.Motion;
        public DefensiveScheme Defense = DefensiveScheme.ManToMan;
        public GamePace Pace = GamePace.Normal;
        public IntensityLevel Intensity = IntensityLevel.Normal;
        public PnRCoverageScheme PnRCoverage = PnRCoverageScheme.DropCoverage;
        public TransitionDefenseLevel TransitionDefense = TransitionDefenseLevel.Normal;
        public string DoubleTeamTarget = null;
        public bool PressEnabled = false;
        public bool HackAShaqEnabled = false;
    }

    public enum OffensiveScheme
    {
        Motion,
        Isolation,
        PickAndRoll,
        PostUp,
        ThreeHeavy,
        FastBreak,
        Princeton,
        Triangle,
        FiveOut
    }

    public enum DefensiveScheme
    {
        ManToMan,
        Zone23,
        Zone32,
        Zone131,
        Zone122,
        BoxAndOne,
        TriangleAndTwo,
        SwitchAll,
        FullCourtPress,
        HalfCourtTrap,
        MatchupZone
    }

    public enum GamePace
    {
        Push,
        Normal,
        Slow
    }

    public enum IntensityLevel
    {
        Conservative,
        Normal,
        Aggressive
    }

    public enum PnRCoverageScheme
    {
        DropCoverage,
        ShowAndRecover,
        Hedge,
        Blitz,
        SwitchAll,
        Ice,
        UnderScreen
    }

    public enum TransitionDefenseLevel
    {
        SprintBack,
        Normal,
        Gambling
    }

    // ==================== MATCHUPS ====================

    [Serializable]
    public class Matchup
    {
        public string DefenderId;
        public string OpponentId;
        public MatchupPriority Priority;
    }

    public enum MatchupPriority
    {
        Normal,
        Shadow,
        DoubleTeam,
        Deny
    }

    public enum DoubleTeamTrigger
    {
        OnCatch,
        OnPost,
        OnDribble,
        OnPnR,
        Always
    }

    // ==================== SUBSTITUTIONS ====================

    [Serializable]
    public class SubstitutionPlan
    {
        public int FatigueThreshold = 70;
        public int RotationDepth = 9;
        public Dictionary<string, int> TargetMinutes = new Dictionary<string, int>();
        public List<string> ClosingLineup = new List<string>();
    }

    public class SubstitutionResult
    {
        public bool Success;
        public string Reason;
        public List<string> NewLineup;
        public string PlayerOut;
        public string PlayerIn;
    }

    public class SubstitutionSuggestion
    {
        public string PlayerOut;
        public string SuggestedReplacement;
        public string Reason;
        public SubstitutionUrgency Urgency;
    }

    public enum SubstitutionUrgency
    {
        Low,
        Medium,
        High,
        Immediate
    }

    // ==================== TIMEOUTS ====================

    public enum TimeoutReason
    {
        StopRun,
        RestPlayers,
        DrawUpPlay,
        EndOfGame,
        Mandatory,
        AdvanceBall,
        IcingShooter
    }

    public class TimeoutResult
    {
        public bool Success;
        public string Reason;
        public int TimeoutsLeft;
    }

    // ==================== PLAY CALLS ====================

    public enum PlayType
    {
        PickAndRoll,
        PickAndPop,
        Isolation,
        PostUp,
        SpotUp3,
        TransitionPush,
        MotionOffense,
        Handoff,
        BackdoorCut,
        LobPlay,
        ATOSpecial,
        SpainPnR,
        HornsAction,
        FlexCut,
        Floppy,
        PrincetonBackdoor,
        Delay,
        Press
    }

    [Serializable]
    public class PlayCall
    {
        public PlayType Type;
        public string PrimaryPlayerId;
        public string SecondaryPlayerId;
        public bool IsATO;
        public float Timestamp;
    }

    // ==================== END GAME ====================

    public class EndGameDecision
    {
        public EndGameAction Action;
        public int TargetShotClock;
        public string Explanation;
    }

    public enum EndGameAction
    {
        PlayNormal,
        HoldForLastShot,
        QuickTwoOrThree,
        RunClock,
        FoulToPreventThree,
        FoulToStopClock,
        NoFoul
    }

    public class FoulResult
    {
        public bool Success;
        public bool FreeThrows;
        public string Message;
    }

    // ==================== CHALLENGE ====================

    public enum ChallengeType
    {
        FoulCall,
        OutOfBounds,
        Goaltending,
        Other
    }

    public class ChallengeResult
    {
        public bool Success;
        public bool ChallengeWon;
        public bool TimeoutCharged;
        public string Message;
    }

    // ==================== TECHNICAL FOULS ====================

    public class TechnicalResult
    {
        public bool GotTechnical;
        public bool WasEjected;
        public int MoraleChange;
        public string Message;
    }
}
