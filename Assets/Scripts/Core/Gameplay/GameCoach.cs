using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Gameplay
{
    /// <summary>
    /// Central controller for in-game coaching decisions.
    /// Manages tactics, substitutions, timeouts, play calls, and technical fouls.
    /// </summary>
    public class GameCoach
    {
        // Current game state
        private GameTactics _tactics;
        private SubstitutionPlan _subPlan;
        private List<Matchup> _matchups = new List<Matchup>();
        
        // Resources
        public int TimeoutsRemaining { get; private set; } = 7;
        public int TimeoutsUsedFirstHalf { get; private set; } = 0;
        public bool CoachEjected { get; private set; } = false;
        public int TechnicalFouls { get; private set; } = 0;
        
        // Game context
        private int _currentQuarter = 1;
        private float _gameClockSeconds = 720f; // 12:00
        private int _teamScore = 0;
        private int _opponentScore = 0;
        private int _opponentRunPoints = 0;

        public GameCoach()
        {
            _tactics = new GameTactics();
            _subPlan = new SubstitutionPlan();
        }

        // ==================== TACTICS ====================

        /// <summary>
        /// Gets current tactical settings.
        /// </summary>
        public GameTactics GetTactics() => _tactics;

        /// <summary>
        /// Sets offensive scheme.
        /// </summary>
        public void SetOffense(OffensiveScheme scheme)
        {
            _tactics.Offense = scheme;
            Debug.Log($"Offense set to: {scheme}");
        }

        /// <summary>
        /// Sets defensive scheme.
        /// </summary>
        public void SetDefense(DefensiveScheme scheme)
        {
            _tactics.Defense = scheme;
            Debug.Log($"Defense set to: {scheme}");
        }

        /// <summary>
        /// Sets game pace.
        /// </summary>
        public void SetPace(GamePace pace)
        {
            _tactics.Pace = pace;
            Debug.Log($"Pace set to: {pace}");
        }

        /// <summary>
        /// Sets intensity level (affects foul rate, hustle, energy burn).
        /// </summary>
        public void SetIntensity(IntensityLevel intensity)
        {
            _tactics.Intensity = intensity;
            Debug.Log($"Intensity set to: {intensity}");
        }

        // ==================== MATCHUPS ====================

        /// <summary>
        /// Assigns a specific defender to guard an opponent.
        /// </summary>
        public void SetDefensiveMatchup(string defenderId, string opponentId)
        {
            _matchups.RemoveAll(m => m.DefenderId == defenderId);
            _matchups.Add(new Matchup
            {
                DefenderId = defenderId,
                OpponentId = opponentId,
                Priority = MatchupPriority.Normal
            });
        }

        /// <summary>
        /// Sets up double team triggers.
        /// </summary>
        public void SetDoubleTeam(string opponentId, bool enable)
        {
            _tactics.DoubleTeamTarget = enable ? opponentId : null;
            Debug.Log(enable ? $"Double-teaming {opponentId}" : "No double teams");
        }

        /// <summary>
        /// Gets the assigned defender for an opponent.
        /// </summary>
        public string GetDefenderFor(string opponentId)
        {
            return _matchups.FirstOrDefault(m => m.OpponentId == opponentId)?.DefenderId;
        }

        // ==================== SUBSTITUTIONS ====================

        /// <summary>
        /// Makes immediate substitution.
        /// </summary>
        public SubstitutionResult Substitute(string playerOut, string playerIn, List<string> currentLineup)
        {
            if (!currentLineup.Contains(playerOut))
            {
                return new SubstitutionResult { Success = false, Reason = $"{playerOut} not in current lineup" };
            }
            
            var newLineup = currentLineup.ToList();
            newLineup.Remove(playerOut);
            newLineup.Add(playerIn);
            
            return new SubstitutionResult
            {
                Success = true,
                NewLineup = newLineup,
                PlayerOut = playerOut,
                PlayerIn = playerIn
            };
        }

        /// <summary>
        /// Sets automatic substitution threshold based on fatigue.
        /// </summary>
        public void SetFatigueThreshold(int threshold)
        {
            _subPlan.FatigueThreshold = Mathf.Clamp(threshold, 50, 100);
            Debug.Log($"Auto-sub at {threshold}% energy");
        }

        /// <summary>
        /// Checks if player should be auto-subbed.
        /// </summary>
        public bool ShouldAutoSub(string playerId, float currentEnergy)
        {
            return currentEnergy < _subPlan.FatigueThreshold;
        }

        /// <summary>
        /// Sets standard rotation and minutes distribution.
        /// </summary>
        public void SetRotation(Dictionary<string, int> targetMinutes)
        {
            _subPlan.TargetMinutes = targetMinutes;
        }

        // ==================== TIMEOUTS ====================

        /// <summary>
        /// Calls a timeout if available.
        /// </summary>
        public TimeoutResult CallTimeout(TimeoutReason reason)
        {
            if (TimeoutsRemaining <= 0)
                return new TimeoutResult { Success = false, Reason = "No timeouts remaining" };
            
            // Check mandatory timeout rules (simplified)
            if (_currentQuarter <= 2)
                TimeoutsUsedFirstHalf++;
            
            TimeoutsRemaining--;
            
            return new TimeoutResult
            {
                Success = true,
                TimeoutsLeft = TimeoutsRemaining,
                Reason = reason.ToString()
            };
        }

        /// <summary>
        /// Checks if timeout would be strategic (opponent on a run, need rest, etc).
        /// </summary>
        public bool ShouldCallTimeout()
        {
            // Opponent on 8+ point run
            if (_opponentRunPoints >= 8)
                return true;
            
            // Close game, late, need to set up play
            if (_currentQuarter == 4 && _gameClockSeconds < 60 && Math.Abs(_teamScore - _opponentScore) <= 5)
                return true;
            
            return false;
        }

        /// <summary>
        /// Updates opponent run tracking.
        /// </summary>
        public void RecordOpponentPoints(int points)
        {
            _opponentRunPoints += points;
            _opponentScore += points;
        }

        /// <summary>
        /// Resets run when we score or timeout.
        /// </summary>
        public void ResetRun()
        {
            _opponentRunPoints = 0;
        }

        // ==================== PLAY CALLS ====================

        /// <summary>
        /// Calls a specific play.
        /// </summary>
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

        /// <summary>
        /// Gets available plays based on lineup.
        /// </summary>
        public List<PlayType> GetAvailablePlays(List<string> currentLineup)
        {
            return new List<PlayType>
            {
                PlayType.PickAndRoll,
                PlayType.Isolation,
                PlayType.PostUp,
                PlayType.SpotUp3,
                PlayType.TransitionPush,
                PlayType.MotionOffense,
                PlayType.Handoff,
                PlayType.BackdoorCut,
                PlayType.ATOSpecial
            };
        }

        /// <summary>
        /// Calls an After-Timeout (ATO) play - typically higher success rate.
        /// </summary>
        public PlayCall CallATOPlay(string primaryPlayer)
        {
            return new PlayCall
            {
                Type = PlayType.ATOSpecial,
                PrimaryPlayerId = primaryPlayer,
                IsATO = true,
                Timestamp = _gameClockSeconds
            };
        }

        // ==================== TECHNICAL FOULS ====================

        /// <summary>
        /// Coach argues a call with the referee.
        /// Risk: Technical foul / ejection
        /// Reward: Rally team (morale boost)
        /// </summary>
        public TechnicalResult ArgueCall(int teamMorale, System.Random rng = null)
        {
            rng ??= new System.Random();
            
            if (CoachEjected)
                return new TechnicalResult { WasEjected = true, Message = "Coach already ejected!" };
            
            // Base 30% chance of technical
            float techChance = 0.3f;
            
            // Already have 1 tech = higher risk
            if (TechnicalFouls > 0)
                techChance = 0.6f;
            
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
                
                // First tech can rally team
                bool rallied = rng.NextDouble() < 0.6f;
                return new TechnicalResult
                {
                    GotTechnical = true,
                    WasEjected = false,
                    MoraleChange = rallied ? 8 : -3,
                    Message = rallied ? "Coach fired up! Team rallied!" : "Technical foul. No rally effect."
                };
            }
            
            // No tech, mild rally
            return new TechnicalResult
            {
                GotTechnical = false,
                WasEjected = false,
                MoraleChange = 3,
                Message = "Coach makes his point. Team appreciates the fire."
            };
        }

        // ==================== GAME STATE UPDATE ====================

        public void UpdateGameState(int quarter, float clockSeconds, int teamScore, int opponentScore)
        {
            _currentQuarter = quarter;
            _gameClockSeconds = clockSeconds;
            _teamScore = teamScore;
            _opponentScore = opponentScore;
        }

        public void ResetForNewGame()
        {
            TimeoutsRemaining = 7;
            TimeoutsUsedFirstHalf = 0;
            TechnicalFouls = 0;
            CoachEjected = false;
            _opponentRunPoints = 0;
            _currentQuarter = 1;
            _tactics = new GameTactics();
        }
    }

    // ==================== TACTICS DATA ====================

    [Serializable]
    public class GameTactics
    {
        public OffensiveScheme Offense = OffensiveScheme.Motion;
        public DefensiveScheme Defense = DefensiveScheme.ManToMan;
        public GamePace Pace = GamePace.Normal;
        public IntensityLevel Intensity = IntensityLevel.Normal;
        public string DoubleTeamTarget = null;
    }

    public enum OffensiveScheme
    {
        Motion,         // Balanced ball movement
        Isolation,      // Give to star and clear out
        PickAndRoll,    // Heavy P&R focus
        PostUp,         // Inside-out game
        ThreeHeavy,     // Shoot lots of 3s
        FastBreak       // Push pace, early offense
    }

    public enum DefensiveScheme
    {
        ManToMan,       // Standard man defense
        Zone23,         // 2-3 zone
        Zone32,         // 3-2 zone
        Zone131,        // 1-3-1 zone
        FullCourtPress, // Pressure all court
        HalfCourtTrap   // Trap at half court
    }

    public enum GamePace
    {
        Push,           // Fast pace, quick shots
        Normal,         // Standard pace
        Slow            // Grind it out, use clock
    }

    public enum IntensityLevel
    {
        Conservative,   // Fewer fouls, less energy burn
        Normal,
        Aggressive      // More fouls, more hustle
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
        Shadow,         // Follow everywhere
        DoubleTeam      // Help immediately
    }

    // ==================== SUBSTITUTIONS ====================

    [Serializable]
    public class SubstitutionPlan
    {
        public int FatigueThreshold = 70; // Sub at 70% energy
        public Dictionary<string, int> TargetMinutes = new Dictionary<string, int>();
    }

    public class SubstitutionResult
    {
        public bool Success;
        public string Reason;
        public List<string> NewLineup;
        public string PlayerOut;
        public string PlayerIn;
    }

    // ==================== TIMEOUTS ====================

    public enum TimeoutReason
    {
        StopRun,        // Opponent on a run
        RestPlayers,    // Players need rest
        DrawUpPlay,     // Need to set up specific play
        EndOfGame,      // Advance ball, strategy
        Mandatory       // Required by rules
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
        ATOSpecial      // After-Timeout set play
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

    // ==================== TECHNICAL FOULS ====================

    public class TechnicalResult
    {
        public bool GotTechnical;
        public bool WasEjected;
        public int MoraleChange;
        public string Message;
    }
}
