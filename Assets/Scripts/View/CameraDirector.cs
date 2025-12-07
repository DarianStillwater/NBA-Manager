using UnityEngine;

namespace NBAHeadCoach.View
{
    public enum CameraMode
    {
        Broadcast,  // TV style, sideline panning
        Tactical,   // Top-down, coach clipboard view
        Action      // 3rd person follow (NBA 2K style)
    }

    public class CameraDirector : MonoBehaviour
    {
        [Header("Targets")]
        public Transform BallTransform;
        public Transform FocusedPlayer; // For Action mode or replays

        [Header("Settings")]
        public float SmoothSpeed = 5f;
        public CameraMode CurrentMode = CameraMode.Broadcast;

        [Header("Broadcast Settings")]
        public Vector3 BroadcastOffset = new Vector3(0, 15, -25);
        public float BroadcastZoomFactor = 0.5f; // How much to zoom in when ball is near sideline

        [Header("Tactical Settings")]
        public Vector3 TacticalOffset = new Vector3(0, 40, -1);
        public float TacticalOrthographicSize = 15f; // If using Ortho camera

        [Header("Action Settings")]
        public Vector3 ActionOffset = new Vector3(0, 4, -6);
        public float ActionLookDamping = 10f;

        private Camera _cam;

        void Start()
        {
            _cam = GetComponent<Camera>();
            if (BallTransform == null)
                Debug.LogWarning("CameraDirector: BallTransform not assigned!");
        }

        void LateUpdate()
        {
            if (BallTransform == null) return;

            switch (CurrentMode)
            {
                case CameraMode.Broadcast:
                    UpdateBroadcastMode();
                    break;
                case CameraMode.Tactical:
                    UpdateTacticalMode();
                    break;
                case CameraMode.Action:
                    UpdateActionMode();
                    break;
            }
        }

        public void SetMode(CameraMode mode)
        {
            CurrentMode = mode;
            // Optional: Trigger transition effects
        }

        public void SetFocus(Transform target)
        {
            FocusedPlayer = target;
        }

        private void UpdateBroadcastMode()
        {
            // Panning logic: Follow ball X, keep relative Z/Y static but allow some zoom
            // Basic implementation:
            Vector3 targetPos = BallTransform.position + BroadcastOffset;
            targetPos.x *= 0.8f; // Dampen X movement slightly to keep court centered longer
            
            // Limit Z movement (width of court)
            targetPos.z = BroadcastOffset.z; 

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * SmoothSpeed);
            
            // Look at center of court or ball? Broadcast usually looks straight or slightly at ball
            Vector3 lookTarget = BallTransform.position;
            lookTarget.y = 0; // Look at floor level
            
            Quaternion targetRot = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * SmoothSpeed * 0.5f);
        }

        private void UpdateTacticalMode()
        {
            // Static or tracking top-down
            Vector3 targetPos = BallTransform.position + TacticalOffset;
            // Lock X/Z if we want full court static view, or follow ball?
            // Usually tactical follows ball with constraints
            
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * SmoothSpeed);
            transform.LookAt(BallTransform);
            // Ideally Tactical uses Orthographic projection for true "clipboard" feel
            // But checking/switching projection at runtime can be jarring. 
            // For now, assume Perspective with high FOV/Height.
        }

        private void UpdateActionMode()
        {
            // Follow behind the ball handler (or ball if loose)
            Transform target = FocusedPlayer != null ? FocusedPlayer : BallTransform;
            
            Vector3 desiredPos = target.position + target.TransformDirection(ActionOffset);
            // Simplified: just offset relative to world if direction is erratic
            // Or use simple offset behind ball
            if (FocusedPlayer == null) 
            {
                 // If following ball object directly, keep offset constant in World Z usually (TV angle)
                 // But Action cam rotates. Let's stick generic third person offset.
                 desiredPos = target.position + ActionOffset; 
            }

            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * SmoothSpeed * 2f);
            
            var rotation = Quaternion.LookRotation(target.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * ActionLookDamping);
        }
    }
}
