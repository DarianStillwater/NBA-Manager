using System;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.UI.Shell;
using B = NBAHeadCoach.UI.Shell.UIBuilder;

namespace NBAHeadCoach.UI.GamePanels
{
    /// <summary>
    /// The real inbox: renders InboxService messages newest-first. Clicking a message
    /// marks it read and follows its deep link (e.g. an injury message opens the
    /// Roster panel on that player).
    /// </summary>
    public class InboxGamePanel : IGamePanel
    {
        private readonly GameShell _shell;

        public InboxGamePanel(GameShell shell)
        {
            _shell = shell;
        }

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            var scroll = B.FixedArea(parent);
            var inbox = InboxService.Instance;
            int unread = inbox?.UnreadCount ?? 0;

            var title = B.Text(scroll, "Title",
                unread > 0 ? $"INBOX  <color=#{ColorUtility.ToHtmlStringRGB(UITheme.AccentPrimary)}>({unread} unread)</color>" : "INBOX",
                18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            var messages = inbox?.GetAll();
            if (messages == null || messages.Count == 0)
            {
                var empty = B.Text(scroll, "Empty",
                    "No messages yet.\nOwner updates, trade news, injury reports, and media stories will land here.",
                    14, FontStyle.Italic, UITheme.TextSecondary);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
                return;
            }

            foreach (var msg in messages)
            {
                var m = msg; // capture
                var card = B.Card(scroll, "", teamColor);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
                var header = card.Find("Header");
                if (header != null) header.gameObject.SetActive(false);

                string dateText = m.Date.Year > 1 ? m.Date.ToString("MMM dd") : "";
                string titleMarkup = m.IsRead ? m.Title : $"<b>{m.Title}</b>";
                var content = B.Text(card, "Content",
                    $"<b>{m.Sender}</b>  •  {dateText}\n{titleMarkup}\n<color=#{ColorUtility.ToHtmlStringRGB(UITheme.TextSecondary)}>{m.Body}</color>",
                    13, FontStyle.Normal, m.IsRead ? UITheme.TextSecondary : Color.white);
                var rt = content.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(12, 8); rt.offsetMax = new Vector2(-12, -8);
                content.alignment = TextAnchor.UpperLeft;
                content.horizontalOverflow = HorizontalWrapMode.Wrap;
                content.verticalOverflow = VerticalWrapMode.Truncate;
                content.raycastTarget = false;

                // Unread/priority indicator bar
                var indicator = B.Child(card, "Priority");
                var ir = indicator.GetComponent<RectTransform>();
                ir.anchorMin = Vector2.zero; ir.anchorMax = new Vector2(0, 1);
                ir.pivot = new Vector2(0, 0.5f); ir.sizeDelta = new Vector2(4, 0);
                var indicatorImg = indicator.AddComponent<Image>();
                indicatorImg.color = m.IsRead ? UITheme.FMNavHover
                    : m.IsHighPriority ? UITheme.Warning : UITheme.AccentSecondary;
                indicatorImg.raycastTarget = false;

                // Click: mark read, follow deep link (or just refresh the inbox)
                var btn = card.gameObject.GetComponent<Button>() ?? card.gameObject.AddComponent<Button>();
                if (card.gameObject.GetComponent<Image>() == null)
                {
                    var bg = card.gameObject.AddComponent<Image>();
                    bg.color = new Color(1f, 1f, 1f, 0.01f); // raycast target
                }
                btn.onClick.AddListener(() =>
                {
                    InboxService.Instance?.MarkRead(m.MessageId);
                    if (!string.IsNullOrEmpty(m.DeepLinkPanelId))
                        _shell?.ShowPanel(m.DeepLinkPanelId, m.DeepLinkPayload);
                    else
                        _shell?.ShowPanel("Inbox"); // rebuild to clear unread styling
                });
            }
        }
    }
}
