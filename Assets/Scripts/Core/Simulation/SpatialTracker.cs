using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Records and manages spatial tracking data for an entire game.
    /// Enables heatmaps, shot charts, and 3D replay.
    /// </summary>
    public class SpatialTracker
    {
        private List<SpatialState> _gameStates = new List<SpatialState>();
        private Dictionary<string, List<ShotEvent>> _shotsByPlayer = new Dictionary<string, List<ShotEvent>>();

        public IReadOnlyList<SpatialState> GameStates => _gameStates;
        public float TrackingIntervalSeconds { get; } = 0.5f;

        /// <summary>
        /// Records a spatial state snapshot.
        /// </summary>
        public void RecordState(SpatialState state)
        {
            _gameStates.Add(state.Clone());
        }

        /// <summary>
        /// Records a shot attempt for shot charts.
        /// </summary>
        public void RecordShot(string playerId, CourtPosition position, bool made, int points)
        {
            if (!_shotsByPlayer.ContainsKey(playerId))
            {
                _shotsByPlayer[playerId] = new List<ShotEvent>();
            }
            _shotsByPlayer[playerId].Add(new ShotEvent
            {
                Position = position,
                Made = made,
                Points = points,
                GameClock = _gameStates.Count > 0 ? _gameStates[^1].GameClock : 720f,
                Quarter = _gameStates.Count > 0 ? _gameStates[^1].Quarter : 1
            });
        }

        /// <summary>
        /// Generates heatmap data for a player.
        /// Returns a 2D array where each cell contains the time spent in that zone.
        /// Grid is 10x5 cells covering the court.
        /// </summary>
        public float[,] GenerateHeatmap(string playerId, int gridWidth = 10, int gridHeight = 5)
        {
            float[,] heatmap = new float[gridWidth, gridHeight];
            float cellWidth = 94f / gridWidth;
            float cellHeight = 50f / gridHeight;

            foreach (var state in _gameStates)
            {
                foreach (var player in state.Players)
                {
                    if (player.PlayerId == playerId)
                    {
                        // Convert position to grid coordinates
                        int gridX = Mathf.Clamp(Mathf.FloorToInt((player.X + 47f) / cellWidth), 0, gridWidth - 1);
                        int gridY = Mathf.Clamp(Mathf.FloorToInt((player.Y + 25f) / cellHeight), 0, gridHeight - 1);
                        heatmap[gridX, gridY] += TrackingIntervalSeconds;
                    }
                }
            }

            return heatmap;
        }

        /// <summary>
        /// Gets all shot events for a player (for shot charts).
        /// </summary>
        public List<ShotEvent> GetPlayerShots(string playerId)
        {
            return _shotsByPlayer.TryGetValue(playerId, out var shots) ? shots : new List<ShotEvent>();
        }

        /// <summary>
        /// Gets shot chart statistics by zone.
        /// </summary>
        public Dictionary<CourtZone, ZoneStats> GetShotChartByZone(string playerId)
        {
            var result = new Dictionary<CourtZone, ZoneStats>();
            
            foreach (CourtZone zone in Enum.GetValues(typeof(CourtZone)))
            {
                result[zone] = new ZoneStats();
            }

            var shots = GetPlayerShots(playerId);
            foreach (var shot in shots)
            {
                var zone = shot.Position.GetZone(true);
                result[zone].Attempts++;
                if (shot.Made)
                {
                    result[zone].Makes++;
                    result[zone].Points += shot.Points;
                }
            }

            return result;
        }

        /// <summary>
        /// Calculates average distance from basket when shooting.
        /// </summary>
        public float GetAverageShotDistance(string playerId)
        {
            var shots = GetPlayerShots(playerId);
            if (shots.Count == 0) return 0;

            float totalDistance = shots.Sum(s => s.Position.DistanceToBasket(true));
            return totalDistance / shots.Count;
        }

        /// <summary>
        /// Gets spatial states for a specific time range (for replay).
        /// </summary>
        public List<SpatialState> GetStatesInRange(int quarter, float startClock, float endClock)
        {
            return _gameStates
                .Where(s => s.Quarter == quarter && s.GameClock <= startClock && s.GameClock >= endClock)
                .ToList();
        }

        /// <summary>
        /// Clears all recorded data (for new game).
        /// </summary>
        public void Clear()
        {
            _gameStates.Clear();
            _shotsByPlayer.Clear();
        }

        /// <summary>
        /// Exports spatial data to JSON for external analysis.
        /// </summary>
        public string ExportToJson()
        {
            return JsonUtility.ToJson(new SpatialDataExport
            {
                States = _gameStates.ToArray(),
                TrackingInterval = TrackingIntervalSeconds
            }, true);
        }
    }

    [Serializable]
    public struct ShotEvent
    {
        public CourtPosition Position;
        public bool Made;
        public int Points;
        public float GameClock;
        public int Quarter;
    }

    [Serializable]
    public class ZoneStats
    {
        public int Attempts;
        public int Makes;
        public int Points;
        public float Percentage => Attempts > 0 ? (float)Makes / Attempts * 100f : 0f;
    }

    [Serializable]
    public class SpatialDataExport
    {
        public SpatialState[] States;
        public float TrackingInterval;
    }
}
