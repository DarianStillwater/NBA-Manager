using UnityEngine;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// One interpolated presentation frame for a player body, assembled by Match3DView from the
    /// possession timeline's PlayerSnapshot. Position and facing are handled by the actor root;
    /// this carries only the fields a body's pose/animation needs.
    /// </summary>
    public struct ActorFrame
    {
        public float SpeedFeetPerSec;   // timeline (pre-lerp) ground speed; kept for reference
        public float VerticalOffset;    // jump height in feet above the floor (data drives height)
        public PlayerAction Action;     // scripted action (shot/dunk/rebound/…) → trigger clips
        // Shot variant that resolves WHICH shot state plays (Fadeaway vs Floater vs JumpShot…).
        // Populated only on the shooter across the windup→land window; null everywhere else, and
        // an unknown value falls through to the generic JumpShot state (see CharacterBody map).
        public ShotType? ShotStyle;
        public float ActionPhase;       // 0..1 progress through the current scripted action
        public bool DefensiveStance;    // crouched guarding pose
        public bool HasBall;

        // Measured from the actual rendered body motion (PlayerActor3D fills these each Update), so
        // the feet match what the eye sees rather than the pre-smoothing timeline speed. Default 0
        // is harmless (idle blend, no lean).
        public float MeasuredSpeedFeetPerSec;   // planar |Δposition|/dt — drives blend + MotionRate
        public float PlanarAccelFeetPerSec2;     // signed change in foot speed → accel/decel lean
        public float YawRateDegPerSec;           // turn rate → bank roll

        // Choreographed contact (Phase 2): set when this actor is making authored body contact
        // (screen brace / box-out / post-up) with a partner. ContactDir is a court-plane unit vector
        // (X→world X, Y→world Z) pointing INTO the partner, so the body leans against them. Default
        // false/zero → no contact tilt. CharacterBody honors it; CapsuleBody ignores it.
        public bool HasContact;
        public Vector2 ContactDir;
    }

    /// <summary>
    /// Swappable visual body for a <see cref="PlayerActor3D"/>. The actor owns position, yaw, the
    /// jersey label, and the ball-handler highlight ring; the body owns only the mesh + its pose.
    /// Two implementations: <see cref="CapsuleBody"/> (no-asset primitive fallback) and
    /// <see cref="CharacterBody"/> (rigged animated character loaded from Resources). The actor
    /// tries CharacterBody first and falls back to CapsuleBody if Build returns false, so a missing
    /// character asset can never break the match.
    /// </summary>
    public interface IActorBody
    {
        /// <summary>Build the visual under <paramref name="root"/>. Return false if this body type
        /// is unavailable (e.g. the character asset failed to load) so the actor can fall back.</summary>
        bool Build(Transform root, PlayerAppearance appearance);

        /// <summary>Per-frame pose/animation update.</summary>
        void Animate(in ActorFrame frame, float dt);

        /// <summary>Post-animator bone posing, called from PlayerActor3D.LateUpdate — after the
        /// Animator has evaluated (Unity runs it between Update and LateUpdate), so bone writes here
        /// survive into the render. CapsuleBody has no skeleton and ignores it; CharacterBody layers
        /// procedural shot/dunk/pass poses over the generic base clips.</summary>
        void PoseLate(in ActorFrame frame);

        /// <summary>Standing height in feet of the built visual, for label/ball placement.</summary>
        float VisualHeightFeet { get; }

        /// <summary>The right-hand bone the ball rides while held/dribbled, or null when this body
        /// has no skeleton (CapsuleBody) — callers fall back to fixed-offset carry math.</summary>
        Transform GetHandBone();
    }
}
