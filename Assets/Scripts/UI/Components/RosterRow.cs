using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Components
{
    public class RosterRow : MonoBehaviour
    {
        public Text NameText;
        public Text PositionText;
        public Text AgeText;
        public Text OverallText;
        public Text MoraleText;
        public Button ClickButton;
        
        private Player _player;

        public void Setup(Player player)
        {
            _player = player;
            if (NameText != null) NameText.text = player.FullName;
            if (PositionText != null) PositionText.text = player.Position.ToString(); // improved formatting later
            if (AgeText != null) AgeText.text = player.Age.ToString();
            if (OverallText != null) OverallText.text = player.Overall.ToString();
            
            // Morale
            if (MoraleText != null) 
            {
                MoraleText.text = $"{player.Morale:F0}";
                MoraleText.color = player.Morale < 50 ? Color.red : Color.green;
            }
        }
    }
}
