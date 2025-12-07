using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.View
{
    public class MatchViewer : MonoBehaviour
    {
        [Header("Configuration")]
        public float PlaybackSpeed = 1.0f;
        
        [Header("Scene References")]
        public Transform CourtCenter;
        public GameObject PlayerPrefab;
        public GameObject BallPrefab;
        public CameraDirector CamDirector;
        public SidelineManager Sidelines;

        private Dictionary<string, PlayerVisual> _activePlayerVisuals = new Dictionary<string, PlayerVisual>();
        private GameObject _ballVisual;
        
        // Queue of events from the simulator
        private Queue<PossessionEvent> _eventQueue = new Queue<PossessionEvent>();
        private bool _isPlaying = false;

        public void InitializeMatch(Team home, Team away, List<string> homeStarters, List<string> awayStarters)
        {
            // Spawn Court (if not present)
            
            // Spawn Ball
            if (BallPrefab != null && _ballVisual == null)
                _ballVisual = Instantiate(BallPrefab, Vector3.zero, Quaternion.identity);
            
            if (CamDirector != null) CamDirector.BallTransform = _ballVisual.transform;

            // Spawn Players
            SpawnTeam(home, homeStarters, Color.white); // Home color
            SpawnTeam(away, awayStarters, Color.red);   // Away color
            
            // Setup Sidelines
            if (Sidelines != null) Sidelines.InitializeSideLines(home, away, homeStarters, awayStarters);

            _isPlaying = true;
            StartCoroutine(PlaybackLoop());
        }

        private void SpawnTeam(Team team, List<string> starterIds, Color color)
        {
            foreach(var pid in starterIds)
            {
                // Lookup player details from Database (omitted for brevity, needed in real impl)
                Vector3 startPos = GetKickoffParams(pid); // Logic to place at jump ball or lineup
                var go = Instantiate(PlayerPrefab, startPos, Quaternion.identity);
                var visual = go.GetComponent<PlayerVisual>();
                visual.Initialize(pid, "Player", 1.0f); // Todo: Fetch name
                
                // Colorize (PropBlock or Material)
                // visual.SetJerseyColor(color);
                
                _activePlayerVisuals.Add(pid, visual);
            }
        }

        private Vector3 GetKickoffParams(string pid)
        {
            return new Vector3(Random.Range(-5, 5), 0, Random.Range(-5, 5)); // Placeholder
        }

        public void EnqueueEvent(PossessionEvent evt)
        {
            _eventQueue.Enqueue(evt);
        }

        private IEnumerator PlaybackLoop()
        {
            while(_isPlaying)
            {
                if (_eventQueue.Count > 0)
                {
                    var evt = _eventQueue.Dequeue();
                    yield return StartCoroutine(ProcessEvent(evt)); // Wait for event to finish visually
                }
                else
                {
                    yield return null;
                }
            }
        }

        private IEnumerator ProcessEvent(PossessionEvent evt)
        {
            // Calculate world position
            Vector3 targetPos = ConvertCourtToWorld(evt.ActorPosition);

            if (_activePlayerVisuals.ContainsKey(evt.ActorPlayerId))
            {
                var p = _activePlayerVisuals[evt.ActorPlayerId];
                
                // Movement
                if (evt.Type == EventType.Dribble || evt.Type == EventType.Cut)
                {
                    // Estimate duration (1.0s / playback speed default)
                    float duration = 1.0f / PlaybackSpeed; 
                    
                    p.MoveTo(targetPos, duration);
                    p.SetBallState(evt.Type == EventType.Dribble);
                    
                    yield return new WaitForSeconds(duration);
                }
                else if (evt.Type == EventType.Shot)
                {
                    p.PlayAnimation("Shoot");
                    yield return new WaitForSeconds(1.0f / PlaybackSpeed); 
                }
                else if (evt.Type == EventType.Pass)
                {
                    p.PlayAnimation("Pass");
                    yield return new WaitForSeconds(0.5f / PlaybackSpeed);
                }
            }
            else
            {
                 yield return null;
            }
        }

        private Vector3 ConvertCourtToWorld(CourtPosition cp)
        {
            // CourtPosition is already centered at 0,0 (-47 to +47, -25 to +25)
            // Just map to Unity World Space (scaled to meters if needed)
            // NBA Court: 94ft x 50ft
            // Unity 1 unit = 1 meter usually. 1 ft = 0.3048 m
            
            float scale = 0.3048f; 
            if (CourtCenter != null) scale = CourtCenter.localScale.x; // Optional: Use court scale
            else scale = 0.3048f;

            // X is Length (94), Z is Width (50) in Unity standard for fields usually
            // CourtPosition X is length, Y is width.
            
            return CourtCenter.position + new Vector3(cp.X, 0, cp.Y) * scale;
        }
    }
}
