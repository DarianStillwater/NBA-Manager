using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Components
{
    public class NextMatchWidget : DashboardWidget
    {
        [Header("Next Match UI")]
        public Text OpponentNameText;
        public Text RecordText;
        public Text DateText;
        public Text ScoutingReportText;
        public Image OpponentLogo;
        public Button StartMatchButton;

        public override void Refresh()
        {
            // Dummy data for now until ScheduleSystem is fully linked
            if (OpponentNameText) OpponentNameText.text = "VS Boston Celtics";
            if (RecordText) RecordText.text = "34-12 (1st East)";
            if (DateText) DateText.text = "Tonight, 7:30 PM";
            if (ScoutingReportText) ScoutingReportText.text = "SCOUTING REPORT:\n\nTatum is averaging 32.5 PPG. Their perimeter defense is elite (#1 in NBA). \n\nKEY TO VICTORY:\nForce the ball out of Tatum's hands and attack the paint when Horford sits.";
        }
        
        private void Start()
        {
            if (StartMatchButton)
            {
                StartMatchButton.onClick.AddListener(OnStartMatch);
            }
            Refresh();
        }

        private void OnStartMatch()
        {
            Debug.Log("Starting Match...");
            // Transition to Scene or Trigger Game Loop
        }
    }
}
