using System.Linq;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Bridges existing producer events into the InboxService without touching the
    /// producers' internals. As features wire up, producers can publish directly and
    /// drop out of this bridge.
    /// </summary>
    public static class InboxWiring
    {
        public static void Wire(
            GameManager gm,
            InboxService inbox,
            JobSecurityManager jobSecurity,
            FinanceManager finance,
            TradeAnnouncementSystem tradeNews,
            MediaManager media,
            InjuryManager injuries)
        {
            if (inbox == null) return;

            if (jobSecurity != null)
            {
                jobSecurity.OnOwnerMessageReceived += msg =>
                {
                    if (msg == null) return;
                    inbox.Publish(InboxMessageType.OwnerMessage, "Team Owner",
                        msg.Subject, msg.Message,
                        highPriority: msg.RequiresResponse,
                        deepLinkPanelId: "Dashboard");
                };
            }

            if (finance != null)
            {
                finance.OnOwnerMessage += (title, message) =>
                    inbox.Publish(InboxMessageType.Finance, "Team Owner", title, message,
                        highPriority: true);
            }

            if (tradeNews != null)
            {
                tradeNews.OnTradeAnnounced += a =>
                {
                    if (a == null) return;
                    inbox.Publish(InboxMessageType.Trade, "League Office",
                        a.Headline, a.Summary,
                        highPriority: a.InvolvesPlayerTeam,
                        deepLinkPayload: a.TradeId);
                };
            }

            if (media != null)
            {
                media.OnHeadlineGenerated += h =>
                {
                    if (h == null || string.IsNullOrEmpty(h.HeadlineText)) return;
                    inbox.Publish(InboxMessageType.Media, "Media Relations",
                        h.HeadlineText, "",
                        deepLinkPanelId: h.IsAboutPlayer ? "Roster" : "",
                        deepLinkPayload: h.PlayerId);
                };
            }

            if (injuries != null)
            {
                injuries.OnPlayerInjured += (player, evt) =>
                {
                    // Player-team injuries only — league-wide injury spam would bury
                    // everything else.
                    if (player == null || evt == null) return;
                    if (gm == null || player.TeamId != gm.PlayerTeamId) return;

                    inbox.Publish(InboxMessageType.Injury, "Medical Staff",
                        $"{player.FullName} injured — out ~{evt.DaysOut} days",
                        evt.GetDescription(),
                        highPriority: true,
                        deepLinkPanelId: "Roster",
                        deepLinkPayload: player.PlayerId);
                };
            }
        }
    }
}
