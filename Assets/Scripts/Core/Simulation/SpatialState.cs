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

        public PlayerSnapshot(string playerId, float x, float y)
        {
            PlayerId = playerId;
            X = x;
            Y = y;
            HasBall = false;
            CurrentAction = PlayerAction.Idle;
            TargetPlayerId = null;
            FacingAngle = 0;
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

        public BallState(float x, float y, float height = 4f)
        {
            X = x;
            Y = y;
            Height = height;
            Status = BallStatus.Held;
            HeldByPlayerId = null;
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
        Fouled
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
