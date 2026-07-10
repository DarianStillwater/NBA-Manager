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
        public float SpeedFeetPerSec;   // ground speed; drives the idle→walk→run blend
        public float VerticalOffset;    // jump height in feet above the floor (data drives height)
        public PlayerAction Action;     // scripted action (shot/dunk/rebound/…) → trigger clips
        public float ActionPhase;       // 0..1 progress through the current scripted action
        public bool DefensiveStance;    // crouched guarding pose
        public bool HasBall;
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

        /// <summary>Standing height in feet of the built visual, for label/ball placement.</summary>
        float VisualHeightFeet { get; }
    }
}
