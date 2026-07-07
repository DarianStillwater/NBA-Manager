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
            InjuryManager injuries,
            PlayoffManager playoffs = null,
            AITradeOfferGenerator tradeOffers = null)
        {
            if (inbox == null) return;

            string TeamName(string id) => gm?.GetTeam(id)?.Name ?? id;

            if (tradeOffers != null)
            {
                tradeOffers.OnNewOfferGenerated += offer =>
                {
                    if (offer == null) return;
                    var wanted = offer.Proposal?.AllAssets?
                        .FirstOrDefault(a => a.SendingTeamId == gm?.PlayerTeamId &&
                                             a.Type == TradeAssetType.Player);
                    string wantedName = wanted != null
                        ? gm?.PlayerDatabase?.GetPlayer(wanted.PlayerId)?.FullName ?? "one of your players"
                        : "one of your players";

                    inbox.Publish(InboxMessageType.Trade, $"{TeamName(offer.OfferingTeamId)} Front Office",
                        $"Trade offer: {TeamName(offer.OfferingTeamId)} want {wantedName}",
                        $"{offer.OfferMessage}\n\nThe offer expires in {offer.DaysUntilExpiry} days. Review it at your Front Office desk.",
                        highPriority: true,
                        deepLinkPanelId: "FrontOffice",
                        deepLinkPayload: $"offer:{offer.OfferId}");
                };
            }

            if (playoffs != null)
            {
                playoffs.OnNBAChampion += (champ, runnerUp) =>
                    inbox.Publish(InboxMessageType.League, "League Office",
                        $"{TeamName(champ)} are NBA Champions!",
                        $"The {TeamName(champ)} defeat the {TeamName(runnerUp)} to win the NBA Finals.",
                        highPriority: true,
                        deepLinkPanelId: "Playoffs");

                playoffs.OnConferenceChampion += (team, conference) =>
                    inbox.Publish(InboxMessageType.League, "League Office",
                        $"{TeamName(team)} win the {conference} Conference",
                        $"The {TeamName(team)} advance to the NBA Finals.",
                        deepLinkPanelId: "Playoffs");

                playoffs.OnSeriesCompleted += series =>
                {
                    string pid = gm?.PlayerTeamId;
                    if (string.IsNullOrEmpty(pid)) return;
                    if (series.HigherSeedTeamId != pid && series.LowerSeedTeamId != pid) return;

                    bool won = series.WinnerTeamId == pid;
                    inbox.Publish(InboxMessageType.League, "League Office",
                        won ? "Series won — advancing!" : "Season over — eliminated",
                        series.GetStatusString(TeamName),
                        highPriority: true,
                        deepLinkPanelId: "Playoffs");
                };
            }

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
