using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Shell;
using B = NBAHeadCoach.UI.Shell.UIBuilder;

namespace NBAHeadCoach.UI.GamePanels
{
    public class StaffGamePanel : IGamePanel
    {
        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            var scroll = B.FixedArea(parent);
            var title = B.Text(scroll, "Title", "COACHING STAFF", 18, FontStyle.Bold, UITheme.AccentPrimary);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            string coachName = GameManager.Instance?.Career?.PersonName ?? "Player";

            var card = B.Card(scroll, "HEAD COACH", teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
            var info = B.Text(card, "Info", $"{coachName}\nRole: Head Coach\nContract: Active",
                13, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(info); info.lineSpacing = 1.5f;

            var card2 = B.Card(scroll, "ASSISTANT COACHES", teamColor);
            card2.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            var info2 = B.Text(card2, "Info", "No assistant coaches assigned",
                13, FontStyle.Italic, UITheme.TextSecondary);
            B.FillCard(info2); info2.alignment = TextAnchor.MiddleCenter;
        }
    }
}
