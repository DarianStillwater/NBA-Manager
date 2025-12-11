using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using NBAHeadCoach.UI.Components;

namespace NBAHeadCoach.UI.Panels
{
    public class DashboardPanel : BasePanel
    {
        [Header("Layout Containers")]
        public Transform HeroContainer;
        public Transform GridContainer;

        private List<DashboardWidget> _widgets = new List<DashboardWidget>();

        protected override void OnShown()
        {
            base.OnShown();
            RefreshAllWidgets();
        }

        public void RegisterWidget(DashboardWidget widget, bool isHero = false)
        {
            _widgets.Add(widget);
            widget.transform.SetParent(isHero ? HeroContainer : GridContainer, false);
            
            // Allow layout groups to control size for grid, but force stretch for Hero
            if (isHero)
            {
                RectTransform rt = widget.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        public void RefreshAllWidgets()
        {
            foreach(var widget in _widgets)
            {
                widget.Refresh();
            }
        }
    }
}
