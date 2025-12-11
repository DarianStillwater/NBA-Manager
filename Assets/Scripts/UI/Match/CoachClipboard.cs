using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace NBAHeadCoach.UI.Match
{
    public class CoachClipboard : BasePanel
    {
        [Header("Tactics")]
        public Dropdown DefenseDropdown;
        public Dropdown OffenseDropdown;
        public Toggle FullCourtPressToggle;
        
        [Header("Subs")]
        public Transform BenchContent; // ScrollView for subs
        
        public Button ResumeButton;

        protected override void Awake()
        {
            base.Awake();
            if (ResumeButton != null)
                ResumeButton.onClick.AddListener(CloseClipboard);
        }

        public void CloseClipboard()
        {
            Hide();
            Time.timeScale = 1; // Resume
        }
        
        // Methods to populate substitution list...
    }
}
