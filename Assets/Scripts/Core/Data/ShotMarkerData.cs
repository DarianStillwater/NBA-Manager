using System;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Data for shot attempt markers on the animated court view.
    /// </summary>
    [Serializable]
    public struct ShotMarkerData
    {
        public string ShooterId;
        public string ShooterName;
        public CourtPosition Position;
        public bool Made;
        public int Points;
        public float GameClock;
        public int Quarter;
        public ShotType ShotType;

        public ShotMarkerData(string shooterId, string shooterName, CourtPosition position,
                             bool made, int points, float gameClock, int quarter, ShotType shotType)
        {
            ShooterId = shooterId;
            ShooterName = shooterName;
            Position = position;
            Made = made;
            Points = points;
            GameClock = gameClock;
            Quarter = quarter;
            ShotType = shotType;
        }
    }

    /// <summary>
    /// Types of shots for visualization purposes.
    /// </summary>
    public enum ShotType
    {
        Layup,
        Dunk,
        Jumper,
        ThreePointer,
        FreeThrow
    }
}
