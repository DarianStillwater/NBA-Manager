using System;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Shell;
using B = NBAHeadCoach.UI.Shell.UIBuilder;

namespace NBAHeadCoach.UI.GamePanels
{
    public class InboxGamePanel : IGamePanel
    {
        private struct InboxMsg { public string sender, subject, preview, date, priority; }

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            var scroll = B.FixedArea(parent);
            var title = B.Text(scroll, "Title", "INBOX", 18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            var messages = GenerateMessages(team);
            foreach (var msg in messages)
            {
                var card = B.Card(scroll, "", teamColor);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
                var header = card.Find("Header");
                if (header != null) header.gameObject.SetActive(false);

                var content = B.Text(card, "Content",
                    $"<b>{msg.sender}</b>  •  {msg.date}\n{msg.subject}\n<color=#{ColorUtility.ToHtmlStringRGB(UITheme.TextSecondary)}>{msg.preview}</color>",
                    13, FontStyle.Normal, Color.white);
                var rt = content.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(12, 8); rt.offsetMax = new Vector2(-12, -8);
                content.alignment = TextAnchor.UpperLeft;
                content.horizontalOverflow = HorizontalWrapMode.Wrap;
                content.verticalOverflow = VerticalWrapMode.Truncate;

                var indicator = B.Child(card, "Priority");
                var ir = indicator.GetComponent<RectTransform>();
                ir.anchorMin = Vector2.zero; ir.anchorMax = new Vector2(0, 1);
                ir.pivot = new Vector2(0, 0.5f); ir.sizeDelta = new Vector2(4, 0);
                indicator.AddComponent<Image>().color = msg.priority == "high" ? UITheme.Warning : UITheme.AccentSecondary;
            }
        }

        private InboxMsg[] GenerateMessages(Team team)
        {
            var date = GameManager.Instance?.CurrentDate ?? DateTime.Now;
            string coachName = GameManager.Instance?.Career?.PersonName ?? "Player";
            return new[]
            {
                new InboxMsg { sender = "Team Owner", subject = "Welcome to the Organization",
                    preview = $"Welcome Coach {coachName}. We have high expectations for this season.",
                    date = date.ToString("MMM dd"), priority = "high" },
                new InboxMsg { sender = "League Office", subject = "Season Schedule Released",
                    preview = "The official 2026-27 NBA schedule has been finalized.",
                    date = date.ToString("MMM dd"), priority = "normal" },
                new InboxMsg { sender = "Media Relations", subject = "Press Conference Request",
                    preview = "Local media outlets have requested a pre-season press conference.",
                    date = date.ToString("MMM dd"), priority = "normal" },
                new InboxMsg { sender = "Scouting Dept", subject = "Upcoming Draft Class Preview",
                    preview = "Initial scouting reports for the upcoming draft class are available.",
                    date = date.AddDays(-1).ToString("MMM dd"), priority = "normal" },
            };
        }
    }
}
