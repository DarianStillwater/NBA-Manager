using System;
using System.Linq;
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
    /// The player's career desk: job security and the owner's expectations while
    /// employed, the year-by-year record, the owner's messages — and when fired,
    /// the job market: openings around the league, applications, interviews, and
    /// offers that lead to a new franchise.
    /// </summary>
    public class CareerPanel : IGamePanel
    {
        private Team _team;
        private Color _teamColor;

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            _team = team;
            _teamColor = teamColor;

            var root = B.Child(parent, "Root");
            B.Stretch(root);
            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.padding = new RectOffset(12, 12, 8, 8);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            var rootRT = root.GetComponent<RectTransform>();

            var gm = GameManager.Instance;
            var career = gm?.Career;

            var title = B.Text(rootRT, "Title",
                career != null && career.CurrentRole == UnifiedRole.Unemployed ? "CAREER — JOB MARKET" : "CAREER",
                18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            var bodyGo = B.Child(rootRT, "Body");
            bodyGo.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scroll = B.ScrollArea(bodyGo.GetComponent<RectTransform>());

            if (gm == null || career == null)
            {
                var none = B.Text(scroll, "None", "Career data unavailable.", 13, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
                return;
            }

            if (career.CurrentRole == UnifiedRole.Unemployed)
            {
                BuildFiredBanner(scroll, gm, career);
                BuildJobMarket(scroll, gm);
            }
            else
            {
                BuildJobSecurityCard(scroll, gm, career);
            }

            BuildOwnerMessages(scroll, gm, career);
            BuildCareerRecord(scroll, career);
        }

        // ==================== EMPLOYED: JOB SECURITY ====================

        private void BuildJobSecurityCard(RectTransform scroll, GameManager gm, UnifiedCareerProfile career)
        {
            var card = B.Card(scroll, "JOB SECURITY", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 128;

            var exp = career.CurrentExpectations;
            string statusWord = SecurityWord(career.JobSecurity);
            string statusColor = SecurityColor(career.JobSecurity);

            string expLine = exp != null
                ? $"Owner's bar: <b>{exp.MinimumWins}</b> wins minimum, <b>{exp.TargetWins}</b> targeted" +
                  (exp.ExpectsPlayoffs ? ", playoffs expected" : "") +
                  (exp.ExpectsDevelopment ? ", develop the young core" : "")
                : "The owner hasn't set expectations yet.";

            string recordLine = _team != null
                ? $"Season so far: <b>{_team.Wins}-{_team.Losses}</b>" +
                  (_team.Wins + _team.Losses > 0
                      ? $"   ·   pace: {(int)Math.Round(_team.Wins / (float)Math.Max(1, _team.Wins + _team.Losses) * 82)} wins"
                      : "")
                : "";

            var text = B.Text(card, "Body",
                $"Standing with ownership: <color={statusColor}><b>{statusWord}</b></color>\n" +
                $"{expLine}\n{recordLine}\n" +
                $"Contract: ${career.CurrentSalary:N0}/yr, {Math.Max(0, career.ContractYears)} year(s) remaining",
                12, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(text);
            text.lineSpacing = 1.3f;
        }

        // ==================== UNEMPLOYED: JOB MARKET ====================

        private void BuildFiredBanner(RectTransform scroll, GameManager gm, UnifiedCareerProfile career)
        {
            var card = B.Card(scroll, "BETWEEN JOBS", UITheme.Danger);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;

            var firing = gm.JobMarketManager?.MarketState?.LastFiring;
            string line = firing != null
                ? $"Dismissed by the {firing.TeamName} — {firing.PrivateReason}.\n" +
                  $"Severance: {firing.SeveranceMonths} months.   "
                : "You're between jobs.\n";

            var text = B.Text(card, "Body",
                line + "The league moves on without you; days keep advancing. Apply below and land the next one.",
                12, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(text);
            text.lineSpacing = 1.3f;
        }

        private void BuildJobMarket(RectTransform scroll, GameManager gm)
        {
            var market = gm.JobMarketManager;
            var state = market?.MarketState;
            if (state == null) return;

            // Applications & offers first — they're actionable
            var apps = state.UserApplications;
            if (apps != null && apps.Count > 0)
            {
                var appCard = B.Card(scroll, "YOUR APPLICATIONS", _teamColor);
                appCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 40 + apps.Count * 34;
                var list = B.Child(appCard, "List");
                B.Stretch(list);
                var lv = list.AddComponent<VerticalLayoutGroup>();
                lv.spacing = 2; lv.padding = new RectOffset(10, 10, 34, 6);
                lv.childControlWidth = true; lv.childControlHeight = false; lv.childForceExpandWidth = true;
                var listRT = list.GetComponent<RectTransform>();

                foreach (var app in apps)
                {
                    var opening = state.OpenPositions.Find(o => o.OpeningId == app.OpeningId);
                    string where = opening != null ? $"{opening.TeamName} {opening.GetPositionTitle()}" : "Unknown";

                    var row = B.TableRow(listRT, 30, UITheme.PanelSurface);
                    B.TableCell(row, where, 220, FontStyle.Bold, UITheme.TextPrimary);
                    B.TableCell(row, StatusWord(app), 190, FontStyle.Normal, UITheme.TextSecondary);

                    if (app.HasOffer)
                    {
                        B.TableCell(row, $"${app.OfferedSalary:N0} x {app.OfferedYears}yr", 150,
                            FontStyle.Normal, UITheme.AccentPrimary);
                        RowButton(row, "Accept", "ACCEPT", UITheme.Success,
                            () => { market.AcceptJob(app.OpeningId); Reshow(); });
                        RowButton(row, "Decline", "DECLINE", UITheme.Danger,
                            () => { market.DeclineJob(app.OpeningId); Reshow(); });
                    }
                }
            }

            // Open positions
            var openings = state.GetOpenings();
            var card = B.Card(scroll, $"OPEN POSITIONS ({openings.Count})", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                40 + Math.Max(1, openings.Count) * 34;
            var body = B.Child(card, "List");
            B.Stretch(body);
            var bv = body.AddComponent<VerticalLayoutGroup>();
            bv.spacing = 2; bv.padding = new RectOffset(10, 10, 34, 6);
            bv.childControlWidth = true; bv.childControlHeight = false; bv.childForceExpandWidth = true;
            var bodyRT = body.GetComponent<RectTransform>();

            if (openings.Count == 0)
            {
                var none = B.Text(bodyRT, "None", "No openings right now — they appear as the league churns.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
                return;
            }

            foreach (var opening in openings)
            {
                bool applied = state.UserApplications.Any(a => a.OpeningId == opening.OpeningId);

                var row = B.TableRow(bodyRT, 30, UITheme.CardBackground);
                B.TableCell(row, $"{opening.TeamCity} {opening.TeamName}", 180, FontStyle.Bold, UITheme.TextPrimary);
                B.TableCell(row, opening.GetPositionTitle(), 110, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, $"${opening.OfferedSalary:N0} x {opening.ContractYears}yr", 140,
                    FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, opening.TeamSituation, 110, FontStyle.Normal, UITheme.TextSecondary);

                if (!applied)
                    RowButton(row, "Apply", "APPLY", UITheme.AccentSecondary,
                        () => { market.ApplyForJob(opening.OpeningId); Reshow(); });
                else
                    B.TableCell(row, "applied", 70, FontStyle.Italic, UITheme.TextSecondary);
            }
        }

        // ==================== OWNER MESSAGES ====================

        private void BuildOwnerMessages(RectTransform scroll, GameManager gm, UnifiedCareerProfile career)
        {
            var messages = career.OwnerMessages;
            if (messages == null || messages.Count == 0) return;

            var recent = messages.AsEnumerable().Reverse().Take(5).ToList();
            var card = B.Card(scroll, "FROM THE OWNER", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 + recent.Count * 44;

            var list = B.Child(card, "List");
            B.Stretch(list);
            var lv = list.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 4; lv.padding = new RectOffset(10, 10, 34, 6);
            lv.childControlWidth = true; lv.childControlHeight = false; lv.childForceExpandWidth = true;
            var listRT = list.GetComponent<RectTransform>();

            foreach (var msg in recent)
            {
                var entry = B.Text(listRT, "Msg",
                    $"<b>{msg.Subject}</b>{(msg.WasRead ? "" : "  <color=#FFD700>●</color>")}\n{msg.Message}",
                    11, FontStyle.Normal, UITheme.TextSecondary);
                entry.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
                entry.lineSpacing = 1.2f;

                string msgId = msg.MessageId;
                var btn = entry.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                    gm.JobSecurity?.RespondToMessage(career.ProfileId, msgId));
            }
        }

        // ==================== CAREER RECORD ====================

        private void BuildCareerRecord(RectTransform scroll, UnifiedCareerProfile career)
        {
            var history = career.CareerHistory ?? new System.Collections.Generic.List<CareerHistoryEntry>();
            var card = B.Card(scroll, "CAREER RECORD", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                70 + Math.Max(1, history.Count) * 26;

            var list = B.Child(card, "List");
            B.Stretch(list);
            var lv = list.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 2; lv.padding = new RectOffset(10, 10, 34, 6);
            lv.childControlWidth = true; lv.childControlHeight = false; lv.childForceExpandWidth = true;
            var listRT = list.GetComponent<RectTransform>();

            var summary = B.Text(listRT, "Summary",
                $"<b>{career.TotalWins}-{career.TotalLosses}</b> career   ·   " +
                $"{career.TotalPlayoffAppearances} playoff trip(s)   ·   {career.TotalChampionships} title(s)   ·   " +
                $"fired {career.TimesFired}x   ·   reputation: {ReputationWord(career.Reputation)}",
                12, FontStyle.Normal, UITheme.TextSecondary);
            summary.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            foreach (var entry in history.AsEnumerable().Reverse())
            {
                string result = entry.WonChampionship ? "  🏆 CHAMPIONS"
                    : entry.MadePlayoffs ? "  made playoffs"
                    : entry.WasFired ? "  <color=#EF4444>fired</color>" : "";

                var row = B.TableRow(listRT, 22, UITheme.PanelSurface);
                B.TableCell(row, entry.Year.ToString(), 60, FontStyle.Bold, UITheme.TextPrimary);
                B.TableCell(row, entry.TeamName ?? entry.TeamId, 170, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, entry.Role.ToString(), 130, FontStyle.Normal, UITheme.TextSecondary);
                B.TableCell(row, (entry.Wins + entry.Losses > 0 ? $"{entry.Wins}-{entry.Losses}" : "—") + result,
                    220, FontStyle.Normal, UITheme.TextSecondary);
            }
        }

        // ==================== HELPERS ====================

        private void Reshow()
        {
            var shell = UnityEngine.Object.FindAnyObjectByType<GameShell>();
            shell?.ShowPanel("Career");
        }

        private void RowButton(RectTransform row, string name, string label, Color color, Action onClick)
        {
            var btn = B.Child(row, name);
            btn.AddComponent<LayoutElement>().preferredWidth = 74;
            btn.AddComponent<Image>().color = UITheme.DarkenColor(color, 0.55f);
            btn.AddComponent<Button>().onClick.AddListener(() => onClick());
            var bt = B.Text(btn.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Bold, Color.white);
            B.Stretch(bt.gameObject); bt.alignment = TextAnchor.MiddleCenter;
        }

        private static string StatusWord(JobApplication app) => app.Status switch
        {
            JobApplicationStatus.Pending => "under review",
            JobApplicationStatus.Interviewing => "interview scheduled",
            JobApplicationStatus.Offered => "OFFER ON THE TABLE",
            JobApplicationStatus.Rejected => app.TeamResponse ?? "passed",
            JobApplicationStatus.Accepted => "accepted",
            JobApplicationStatus.Declined => "you declined",
            JobApplicationStatus.Withdrawn => "withdrawn",
            _ => app.Status.ToString()
        };

        private static string SecurityWord(JobSecurityStatus status) => status switch
        {
            JobSecurityStatus.Untouchable => "UNTOUCHABLE",
            JobSecurityStatus.Secure => "SECURE",
            JobSecurityStatus.Stable => "STABLE",
            JobSecurityStatus.Uncertain => "UNCERTAIN",
            JobSecurityStatus.HotSeat => "HOT SEAT",
            JobSecurityStatus.FinalWarning => "FINAL WARNING",
            JobSecurityStatus.Fired => "FIRED",
            _ => status.ToString()
        };

        private static string SecurityColor(JobSecurityStatus status) => status switch
        {
            JobSecurityStatus.Untouchable => "#4ADE80",
            JobSecurityStatus.Secure => "#4ADE80",
            JobSecurityStatus.Stable => "#94A3B8",
            JobSecurityStatus.Uncertain => "#FBBF24",
            JobSecurityStatus.HotSeat => "#FB923C",
            JobSecurityStatus.FinalWarning => "#EF4444",
            JobSecurityStatus.Fired => "#EF4444",
            _ => "#94A3B8"
        };

        private static string ReputationWord(int rep) =>
            rep >= 80 ? "elite" : rep >= 65 ? "respected" : rep >= 50 ? "established"
            : rep >= 35 ? "unproven" : "damaged";
    }
}
