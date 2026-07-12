using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Snapshot of all player positions at a specific moment in game time.
    /// Recorded every 0.5 seconds of simulation time.
    /// </summary>
    [Serializable]
    public class SpatialState
    {
        public float GameClock;        // Seconds remaining in quarter (720 -> 0)
        public int Quarter;            // 1-4 (or 5+ for OT)
        public float PossessionClock;  // Shot clock (24 -> 0)

        /// <summary>Presentational phase hint set by the choreographer (additive; default Advance).</summary>
        public Choreography.PossessionPhase Phase;

        /// <summary>Seconds since possession playback start (additive; default 0). Unlike
        /// GameClock — which holds constant through a dead-ball lead-in — this always advances,
        /// so views/cameras can reconstruct true elapsed playback time even when the clock is held.</summary>
        public float PresentationTime;

        public PlayerSnapshot[] Players = new PlayerSnapshot[10];
        public BallState Ball;

        public SpatialState(float gameClock, int quarter, float possessionClock)
        {
            GameClock = gameClock;
            Quarter = quarter;
            PossessionClock = possessionClock;
        }

        /// <summary>
        /// Creates a deep copy of this state.
        /// </summary>
        public SpatialState Clone()
        {
            var clone = new SpatialState(GameClock, Quarter, PossessionClock);
            clone.Ball = Ball;
            clone.PresentationTime = PresentationTime;
            for (int i = 0; i < 10; i++)
            {
                clone.Players[i] = Players[i];
            }
            return clone;
        }
    }

    /// <summary>
    /// Position and state of a single player at a moment.
    /// </summary>
    [Serializable]
    public struct PlayerSnapshot
    {
        public string PlayerId;
        public float X;
        public float Y;
        public bool HasBall;
        public PlayerAction CurrentAction;
        public string TargetPlayerId;  // For passes/screens
        public float FacingAngle;      // Direction player is facing (radians)

        // ── Presentational enrichments (additive; all default 0/false so JsonUtility
        //    round-trips and the 2D view — which ignores them — is unaffected). Populated
        //    by the choreographer so a 3D animator (P3) can drive convincing motion.
        //    None of these fields influence simulated outcomes. ──

        /// <summary>Ground speed in feet/sec, differenced from adjacent timeline frames at
        /// emit time (0 for a still player, ~28 at a full sprint). Drives run/idle blends.</summary>
        public float SpeedFeetPerSec;

        /// <summary>True when this player is a defender actively guarding (man-tracking or
        /// contesting) — cue for a crouched defensive stance. Always false for the offense.</summary>
        public bool DefensiveStance;

        /// <summary>Jump height in feet above the floor for airborne moments
        /// (shot/dunk/layup/rebound/block); 0 while grounded. Derived from the same timing
        /// the ball-flight/dunk choreography uses.</summary>
        public float VerticalOffset;

        /// <summary>Normalized progress 0..1 through the current scripted action
        /// (e.g. shot windup→release→follow-through) so an animator can sync a clip.
        /// 0 when the player isn't mid-action.</summary>
        public float ActionPhase;

        /// <summary>Presentational: the shot type this player is taking, stamped on the SHOOTER
        /// only from windup start through the ball reaching the rim (null otherwise). The renderer
        /// pairs it with CurrentAction to pick a distinct shot animation (fadeaway/pull-up/etc.).</summary>
        public ShotType? ShotStyle;

        public PlayerSnapshot(string playerId, float x, float y)
        {
            PlayerId = playerId;
            X = x;
            Y = y;
            HasBall = false;
            CurrentAction = PlayerAction.Idle;
            TargetPlayerId = null;
            FacingAngle = 0;
            SpeedFeetPerSec = 0f;
            DefensiveStance = false;
            VerticalOffset = 0f;
            ActionPhase = 0f;
            ShotStyle = null;
        }

        public Data.CourtPosition GetPosition() => new Data.CourtPosition(X, Y);
    }

    /// <summary>
    /// Ball position and state.
    /// </summary>
    [Serializable]
    public struct BallState
    {
        public float X;
        public float Y;
        public float Height;           // For shot arcs, passes
        public BallStatus Status;
        public string HeldByPlayerId;  // null if loose

        /// <summary>Presentational: the shot type driving this flight (null when not shot-related).
        /// Lets the renderer differentiate a dunk's carried thrust from a rainbow three (spin, FX).</summary>
        public ShotType? ShotStyle;

        public BallState(float x, float y, float height = 4f)
        {
            X = x;
            Y = y;
            Height = height;
            Status = BallStatus.Held;
            HeldByPlayerId = null;
            ShotStyle = null;
        }
    }

    public enum PlayerAction
    {
        Idle,
        Running,
        Sprinting,
        Dribbling,
        Passing,
        Catching,
        Shooting,
        Dunking,
        Layup,
        Screening,
        Cutting,
        PostingUp,
        Defending,
        Contesting,
        Rebounding,
        BoxingOut,
        Celebrating,
        Fouled,
        Inbounding,   // holding the ball behind the line to inbound (continuous-flow lead-in)
        LayupAcrobatic // reverse/off-hand finish — a Layup shot flagged acrobatic (renderer picks the fancy clip)
    }

    public enum BallStatus
    {
        Held,
        Dribbled,
        InAir_Pass,
        InAir_Shot,
        Loose,
        DeadBall
    }
}
