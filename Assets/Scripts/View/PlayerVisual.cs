using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.View
{
    public class PlayerVisual : MonoBehaviour
    {
        [Header("Componenets")]
        public Animator Animator;
        public Canvas WorldCanvas;
        public Text NameText;
        public Image FatigueBar;
        public Image EnergyRing; // NBA 2K style ring under player

        [Header("Movement")]
        public float MoveSmoothTime = 0.1f;
        public float RotationSmoothTime = 0.1f;
        
        [Header("State")]
        public string PlayerId;
        private Vector3 _targetPosition;
        private Vector3 _currentVelocity;
        private float _targetSpeed;
        private bool _hasBall;

        // Animator Hashes for performance
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int HasBallHash = Animator.StringToHash("HasBall");
        private static readonly int DefenseStanceHash = Animator.StringToHash("IsDefending");
        private static readonly int ShotTriggerHash = Animator.StringToHash("Shoot");
        private static readonly int PassTriggerHash = Animator.StringToHash("Pass");

        public void Initialize(string id, string name, float initialEnergy)
        {
            PlayerId = id;
            if (NameText != null) NameText.text = name;
            UpdateEnergy(initialEnergy);
            _targetPosition = transform.position;
        }

        public void UpdateEnergy(float energy01)
        {
            if (FatigueBar != null) FatigueBar.fillAmount = energy01;
            if (EnergyRing != null) 
            {
                EnergyRing.fillAmount = energy01;
                EnergyRing.color = Color.Lerp(Color.red, Color.green, energy01);
            }
        }

        public void MoveTo(Vector3 position, float duration)
        {
            // The simulation gives discrete movement steps.
            // We set the target, and Update() handles the smoothing and animation param setting.
            _targetPosition = position;
            
            // Calculate speed based on distance / duration for animation
            float dist = Vector3.Distance(transform.position, position);
            _targetSpeed = duration > 0 ? dist / duration : 0;
            
            // Normalize speed for animator (0 = Idle, 1 = Jog, 2 = Sprint)
            // Assuming 1 unit = 1 meter approx. Jog ~ 3m/s, Sprint ~ 7m/s
            float animSpeed = _targetSpeed / 4.0f; 
            if (Animator != null) Animator.SetFloat(SpeedHash, animSpeed);
        }

        public void SetBallState(bool hasBall)
        {
            _hasBall = hasBall;
            if (Animator != null) Animator.SetBool(HasBallHash, hasBall);
        }

        public void PlayAnimation(string animationName)
        {
            if (Animator == null) return;
            
            switch(animationName)
            {
                case "Shoot": Animator.SetTrigger(ShotTriggerHash); break;
                case "Pass": Animator.SetTrigger(PassTriggerHash); break;
                // Add more specific clips here
            }
        }

        void Update()
        {
            // Smooth movement to target
            // Note: In a real deterministic sim, we might want to be exact.
            // But for visualization, smoothing is key.
            
            transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _currentVelocity, MoveSmoothTime);

            // Rotation
            if (_currentVelocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_currentVelocity.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationSmoothTime);
            }
            
            // Billboarding UI
            if (WorldCanvas != null && Camera.main != null)
            {
                WorldCanvas.transform.LookAt(WorldCanvas.transform.position + Camera.main.transform.rotation * Vector3.forward,
                                             Camera.main.transform.rotation * Vector3.up);
            }
        }
    }
}
