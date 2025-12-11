using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Components
{
    [RequireComponent(typeof(RectTransform))]
    public abstract class DashboardWidget : MonoBehaviour
    {
        [Header("Grid Config")]
        public int ColumnSpan = 1; // 1 = 1x1, 2 = 2 wide
        public int RowSpan = 1;    // 1 = 1x1, 2 = 2 tall
        
        [Header("UI")]
        public Text TitleText;
        public Image Background;

        public virtual void Setup()
        {
            // Base setup
        }

        public abstract void Refresh();
    }
}
