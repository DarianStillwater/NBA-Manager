using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NBAHeadCoach.UI.Components
{
    [RequireComponent(typeof(Image))]
    public class TabButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler
    {
        public TabGroup Group;
        public BasePanel TargetPanel; // Panel to open when clicked
        public Image Background;

        void Start()
        {
            Background = GetComponent<Image>();
            if (Group != null) Group.Subscribe(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Group != null) Group.OnTabEnter(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Group != null) Group.OnTabSelected(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Group != null) Group.OnTabExit(this);
        }

        public void SetColor(Color color)
        {
            if (Background != null) Background.color = color;
        }

        public void Select()
        {
            // Optional: trigger visuals
        }

        public void Deselect()
        {
            // Optional: reset visuals
        }
    }
}
