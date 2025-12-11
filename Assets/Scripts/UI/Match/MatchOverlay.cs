using UnityEngine;
using UnityEngine.UI;

namespace NBAHeadCoach.UI.Match
{
    public class MatchOverlay : BasePanel
    {
        [Header("Scoreboard")]
        public Text HomeDetailsText; // "LAL 105"
        public Text AwayDetailsText; // "BOS 102"
        public Text ClockText;       // "4th 10:24"
        
        [Header("Controls")]
        public Button TimeoutButton;
        public Button ClipboardButton;
        public Button PauseButton;
        
        [Header("Sub-Panels")]
        public CoachClipboard Clipboard;

        protected override void Awake()
        {
            base.Awake();
            if (ClipboardButton != null) 
                ClipboardButton.onClick.AddListener(OnClipboardClicked);
        }

        public void UpdateScore(int homeScore, int awayScore, string homeName, string awayName)
        {
            if (HomeDetailsText != null) HomeDetailsText.text = $"{homeName} {homeScore}";
            if (AwayDetailsText != null) AwayDetailsText.text = $"{awayName} {awayScore}";
        }

        public void UpdateClock(int quarter, float timeRemaining)
        {
            int min = (int)(timeRemaining / 60);
            int sec = (int)(timeRemaining % 60);
            if (ClockText != null) ClockText.text = $"Q{quarter} {min}:{sec:D2}";
        }

        private void OnClipboardClicked()
        {
            if (Clipboard != null) 
            {
                Clipboard.Show();
                // Pause simulation?
                Time.timeScale = 0;
            }
        }
    }
}
