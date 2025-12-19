using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    public class TabGroup : MonoBehaviour
    {
        public List<TabButton> Tabs;
        public Color TabIdleColor = Color.gray;
        public Color TabHoverColor = Color.white;
        public Color TabSelectedColor = new Color(0.2f, 0.6f, 1.0f); // FM Blue

        private TabButton _selectedTab;

        public void Subscribe(TabButton button)
        {
            if (Tabs == null) Tabs = new List<TabButton>();
            Tabs.Add(button);
        }

        public void OnTabEnter(TabButton button)
        {
            ResetTabs();
            if (_selectedTab == null || button != _selectedTab)
                button.SetColor(TabHoverColor);
        }

        public void OnTabExit(TabButton button)
        {
            ResetTabs();
        }

        public void OnTabSelected(TabButton button)
        {
            if (_selectedTab != null) _selectedTab.Deselect();
            _selectedTab = button;
            _selectedTab.Select();
            ResetTabs();
            button.SetColor(TabSelectedColor);

            // Switch Panel Logic - call Show() directly on the target panel
            if (button.TargetPanel != null)
            {
                button.TargetPanel.Show();
            }
        }

        private void ResetTabs()
        {
            foreach(var tab in Tabs)
            {
                if (_selectedTab != null && tab == _selectedTab) continue;
                tab.SetColor(TabIdleColor);
            }
        }
    }
}
