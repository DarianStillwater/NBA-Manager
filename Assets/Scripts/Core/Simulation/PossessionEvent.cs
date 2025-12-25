using System;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Represents a discrete event that occurred during a possession.
    /// Used to create play-by-play logs and trigger 3D animations.
    /// </summary>
    [Serializable]
    public class PossessionEvent
    {
        public EventType Type;
        public float GameClock;
        public float PossessionClock;
        public int Quarter;
        
        // Primary actor
        public string ActorPlayerId;
        public CourtPosition ActorPosition;
        
        // Target (for passes, screens, etc.)
        public string TargetPlayerId;
        public CourtPosition TargetPosition;
        
        // Defender involved (for shots, steals, blocks)
        public string DefenderPlayerId;
        public float ContestLevel;  // 0-1, how well defended
        
        // Outcome
        public EventOutcome Outcome;
        public int PointsScored;
        
        // Additional context
        public ShotType? ShotType;
        public bool IsFastBreak;
        public bool IsAndOne;
        public string Description;

        // Foul details (populated when Type == Foul)
        public FoulEventDetail FoulDetail;

        // Violation details (populated when Type == Turnover with violation)
        public ViolationEventDetail ViolationDetail;

        // Free throw details (populated when free throws awarded)
        public FreeThrowResult FreeThrowResult;

        /// <summary>
        /// Creates a text description for play-by-play.
        /// </summary>
        public string ToPlayByPlay(Func<string, string> getPlayerName)
        {
            string actor = getPlayerName(ActorPlayerId);
            string target = TargetPlayerId != null ? getPlayerName(TargetPlayerId) : "";
            string defender = DefenderPlayerId != null ? getPlayerName(DefenderPlayerId) : "";

            return Type switch
            {
                EventType.Pass => Outcome == EventOutcome.Success 
                    ? $"{actor} passes to {target}" 
                    : $"{actor}'s pass stolen by {defender}",
                    
                EventType.Dribble => $"{actor} drives to the {ActorPosition.GetSide().ToString().ToLower()}",
                
                EventType.Shot => Outcome == EventOutcome.Success
                    ? $"{actor} {GetShotDescription()} - SCORES! ({PointsScored} pts)"
                    : $"{actor} {GetShotDescription()} - MISS",
                    
                EventType.Block => $"{defender} BLOCKS {actor}'s shot!",
                
                EventType.Rebound => $"{actor} grabs the rebound",
                
                EventType.Steal => $"{actor} steals it from {target}!",
                
                EventType.Turnover => GetTurnoverDescription(actor),

                EventType.Foul => GetFoulDescription(actor, defender),
                
                EventType.FreeThrow => Outcome == EventOutcome.Success
                    ? $"{actor} makes the free throw"
                    : $"{actor} misses the free throw",
                    
                EventType.Screen => $"{actor} sets a screen",
                
                EventType.Substitution => $"{actor} checks in for {target}",
                
                EventType.Timeout => $"Timeout called",
                
                _ => Description ?? "..."
            };
        }

        private string GetShotDescription()
        {
            string shotDesc = ShotType switch
            {
                Simulation.ShotType.Dunk => "dunks it",
                Simulation.ShotType.Layup => "lays it in",
                Simulation.ShotType.Floater => "floats one up",
                Simulation.ShotType.Hookshot => "hooks it",
                Simulation.ShotType.Jumper => "pulls up for a jumper",
                Simulation.ShotType.StepBack => "steps back for a jumper",
                Simulation.ShotType.Fadeaway => "fades away",
                Simulation.ShotType.CatchAndShoot => "catches and shoots",
                Simulation.ShotType.Heave => "heaves it from half court",
                _ => "shoots"
            };

            var zone = ActorPosition.GetZone(true);
            string zoneDesc = zone switch
            {
                CourtZone.RestrictedArea => "at the rim",
                CourtZone.Paint => "in the paint",
                CourtZone.ShortMidRange => "from short range",
                CourtZone.LongMidRange => "from mid-range",
                CourtZone.ThreePoint => "from three",
                _ => ""
            };

            string contestDesc = ContestLevel > 0.7f ? " (heavily contested)"
                : ContestLevel > 0.4f ? " (contested)" : "";

            return $"{shotDesc} {zoneDesc}{contestDesc}";
        }

        private string GetTurnoverDescription(string actor)
        {
            if (ViolationDetail != null)
            {
                return ViolationDetail.ViolationType switch
                {
                    ViolationType.Traveling => $"Traveling on {actor}. Turnover.",
                    ViolationType.Backcourt => $"Backcourt violation on {actor}. Turnover.",
                    ViolationType.ThreeSecond => $"3-second violation on {actor}. Turnover.",
                    ViolationType.ShotClock => "Shot clock violation. Turnover.",
                    ViolationType.OutOfBounds => $"{actor} steps out of bounds. Turnover.",
                    ViolationType.DoubleDribble => $"Double dribble on {actor}. Turnover.",
                    _ => $"{actor} turns it over"
                };
            }
            return Description ?? $"{actor} turns it over";
        }

        private string GetFoulDescription(string fouled, string fouler)
        {
            if (FoulDetail == null)
                return $"Foul on {fouler}";

            string prefix = FoulDetail.IsBonusSituation ? "[BONUS] " : "";
            if (FoulDetail.IsDoubleBonusSituation)
                prefix = "[DOUBLE BONUS] ";

            string foulTypeDesc = FoulDetail.FoulType switch
            {
                FoulType.Shooting => "Shooting foul",
                FoulType.Offensive => "Offensive foul",
                FoulType.Technical => "TECHNICAL FOUL",
                FoulType.Flagrant1 => "FLAGRANT 1",
                FoulType.Flagrant2 => "FLAGRANT 2 - EJECTION",
                FoulType.Loose => "Loose ball foul",
                _ => "Personal foul"
            };

            string result = $"{prefix}{foulTypeDesc} on {fouler}";

            if (FoulDetail.FreeThrowsAwarded > 0)
                result += $". {fouled} shoots {FoulDetail.FreeThrowsAwarded}.";

            if (FoulDetail.FoulerFouledOut)
                result += $" {fouler} fouls out!";
            else if (FoulDetail.FoulerEjected)
                result += $" {fouler} is ejected!";

            return result;
        }
    }

    public enum EventType
    {
        // Offensive
        Pass,
        Dribble,
        Shot,
        Screen,
        Cut,
        PostUp,
        
        // Defensive
        Steal,
        Block,
        Deflection,
        
        // Neutral
        Rebound,
        Turnover,
        Foul,
        FreeThrow,
        JumpBall,
        
        // Game flow
        Substitution,
        Timeout,
        QuarterStart,
        QuarterEnd,
        GameEnd
    }

    public enum EventOutcome
    {
        Success,
        Fail,
        Pending  // For multi-step events
    }

    public enum ShotType
    {
        Dunk,
        Layup,
        Floater,
        Hookshot,
        Jumper,
        StepBack,
        Fadeaway,
        CatchAndShoot,
        PullUp,
        Heave,
        TipIn
    }
}
