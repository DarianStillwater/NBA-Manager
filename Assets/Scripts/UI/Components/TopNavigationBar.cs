using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Manages the two-tier navigation system (Category -> SubView) and the Global Ticker.
    /// </summary>
    public class TopNavigationBar : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform MainTabContainer; // Tier 1
        public RectTransform SubTabContainer;  // Tier 2
        public Text TickerDateText;
        public Button SimButton;

        [Header("Prefabs")]
        public GameObject TabButtonPrefab; // Prefab for Tier 1 tabs
        public GameObject SubTabButtonPrefab; // Prefab for Tier 2 tabs

        private Dictionary<string, List<TabDefinition>> _navigationStructure = new Dictionary<string, List<TabDefinition>>();
        private string _activeCategory;
        private List<GameObject> _activeSubTabs = new List<GameObject>();

        // Event for when a final sub-tab is clicked
        public event Action<string> OnViewChanged;

        public void Initialize()
        {
            // Define structure (hardcoded for now, could be config)
            _navigationStructure.Add("Team", new List<TabDefinition> 
            {
                new TabDefinition("Dashboard", true),
                new TabDefinition("Roster"),
                new TabDefinition("Depth Chart"),
                new TabDefinition("Tactics"),
                new TabDefinition("Staff")
            });
            
            _navigationStructure.Add("League", new List<TabDefinition> 
            {
                new TabDefinition("Standings", true),
                new TabDefinition("Schedule"),
                new TabDefinition("Stats"),
                new TabDefinition("Transactions")
            });

            _navigationStructure.Add("Scouting", new List<TabDefinition> 
            {
                new TabDefinition("Prospects", true),
                new TabDefinition("Draft Board")
            });

            _navigationStructure.Add("Office", new List<TabDefinition> 
            {
                new TabDefinition("Inbox", true),
                new TabDefinition("Finances"),
                new TabDefinition("Settings")
            });

            RenderMainTabs();
            SelectCategory("Team"); // Default
        }

        private void RenderMainTabs()
        {
            // Clear existing
            foreach(Transform child in MainTabContainer) Destroy(child.gameObject);

            foreach (var category in _navigationStructure.Keys)
            {
                var btnObj = Instantiate(TabButtonPrefab, MainTabContainer);
                btnObj.SetActive(true);
                var btn = btnObj.GetComponent<Button>();
                var txt = btnObj.GetComponentInChildren<Text>();
                txt.text = category;
                
                btn.onClick.AddListener(() => SelectCategory(category));
            }
        }

        private void SelectCategory(string category)
        {
            _activeCategory = category;
            RenderSubTabs(category);
            
            // Auto-select first sub-tab
            var firstSub = _navigationStructure[category][0];
            SelectView(firstSub.Id);
        }

        private void RenderSubTabs(string category)
        {
            // Clear existing sub tabs
            foreach(Transform child in SubTabContainer) Destroy(child.gameObject);
            _activeSubTabs.Clear();

            if (!_navigationStructure.ContainsKey(category)) return;

            foreach (var tabDef in _navigationStructure[category])
            {
                var btnObj = Instantiate(SubTabButtonPrefab, SubTabContainer);
                btnObj.SetActive(true);
                var btn = btnObj.GetComponent<Button>();
                var txt = btnObj.GetComponentInChildren<Text>();
                txt.text = tabDef.Name;
                txt.color = UITheme.TextSecondary; // Muted by default

                btn.onClick.AddListener(() => SelectView(tabDef.Id));
                _activeSubTabs.Add(btnObj);
            }
        }

        private void SelectView(string viewId)
        {
            Debug.Log($"Navigating to: {_activeCategory} -> {viewId}");
            OnViewChanged?.Invoke(viewId);
            
            // Visual feedback handling would go here (highlight active tab)
        }

        public void UpdateTicker(string dateString)
        {
            if (TickerDateText != null) 
            {
                TickerDateText.text = dateString;
                TickerDateText.color = UITheme.TextPrimary;
            }
        }
    }

    public class TabDefinition
    {
        public string Name;
        public string Id;
        public bool IsDefault;

        public TabDefinition(string name, bool isDefault = false)
        {
            Name = name;
            Id = name.Replace(" ", ""); // Simple ID generation
            IsDefault = isDefault;
        }
    }
}
