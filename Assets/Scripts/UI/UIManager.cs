using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance;

        [Header("Main Containers")]
        public Transform MainCanvas;
        public Transform ContentParent; // Where panels are instantiated/shown

        private List<BasePanel> _activePanels = new List<BasePanel>();
        private Dictionary<string, BasePanel> _panelCache = new Dictionary<string, BasePanel>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        public void OpenPanel(string panelId)
        {
            // Placeholder for loading panel by ID or Prefab name
            // In a real implementation, this would instantiate from Resources or use a registry
            Debug.Log($"Opening Panel: {panelId}");
        }

        public void ShowPanel(BasePanel panel)
        {
            // Close others if single-view style? 
            // For FM style, usually one main content view active
            foreach(var p in _activePanels)
            {
                p.Hide();
            }
            _activePanels.Clear();

            panel.Show();
            _activePanels.Add(panel);
        }

        public void RegisterPanel(string id, BasePanel panel)
        {
            if (!_panelCache.ContainsKey(id))
                _panelCache.Add(id, panel);
        }
    }
}
