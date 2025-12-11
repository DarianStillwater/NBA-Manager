using UnityEngine;
using UnityEngine.EventSystems;
using NBAHeadCoach.Core.Gameplay; // Access to GameCoach actions

namespace NBAHeadCoach.View
{
    public class InteractionManager : MonoBehaviour
    {
        [Header("Dependencies")]
        public Camera MainCamera;
        public GameCoach UserCoach; // Reference to the logic controller

        [Header("UI")]
        public GameObject PlayerContextMenu; // Prefab or reference to UI panel
        
        private PlayerVisual _hoveredPlayer;
        private PlayerVisual _selectedPlayer;

        void Update()
        {
            HandleMouseInput();
        }

        private void HandleMouseInput()
        {
            if (MainCamera == null) return;

            Ray ray = MainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                var visual = hit.collider.GetComponentInParent<PlayerVisual>();
                if (visual != null)
                {
                    OnHover(visual);
                    
                    if (Input.GetMouseButtonDown(0))
                    {
                        OnSelect(visual);
                    }
                }
                else
                {
                    OnHoverExit();
                    if (Input.GetMouseButtonDown(0))
                    {
                        OnDeselect();
                    }
                }
            }
        }

        private void OnHover(PlayerVisual visual)
        {
            _hoveredPlayer = visual;
            // Highlight effect (e.g., enable a halo or change shader rim color)
            if (visual.EnergyRing != null) visual.EnergyRing.rectTransform.localScale = Vector3.one * 1.2f;
        }

        private void OnHoverExit()
        {
            if (_hoveredPlayer != null)
            {
                // Remove highlight
                if (_hoveredPlayer.EnergyRing != null) _hoveredPlayer.EnergyRing.rectTransform.localScale = Vector3.one;
                _hoveredPlayer = null;
            }
        }

        private void OnSelect(PlayerVisual visual)
        {
            _selectedPlayer = visual;
            Debug.Log($"Selected Player: {visual.PlayerId}");
            
            // Open Context Menu (Sub, Play Call)
            // Implementation: Move UI to screen position of player
            if (PlayerContextMenu != null)
            {
                PlayerContextMenu.SetActive(true);
                // Set position...
                // Populate options based on "Are we on offense? defense? is this my player?"
            }
        }

        private void OnDeselect()
        {
            _selectedPlayer = null;
            if (PlayerContextMenu != null) PlayerContextMenu.SetActive(false);
        }
        
        // Called by UI buttons
        public void CallTimeout()
        {
            if (UserCoach != null) UserCoach.CallTimeout(TimeoutReason.RestPlayers);
        }
        
        public void CallSub(string playerOutId, string playerInId)
        {
             // UserCoach.RequestSub(...);
        }
    }
}
