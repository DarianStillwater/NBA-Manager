using System;
using System.Collections.Generic;
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
    /// The money screen: cap sheet with every contract's forward years, cap and
    /// luxury-tax situation, season revenue, and the owner — who they are, what
    /// they expect, and how they feel about the bill.
    /// </summary>
    public class FinancesPanel : IGamePanel
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

            var title = B.Text(rootRT, "Title", "FINANCES", 18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            var bodyGo = B.Child(rootRT, "Body");
            bodyGo.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scroll = B.FixedArea(bodyGo.GetComponent<RectTransform>());

            var gm = GameManager.Instance;
            if (gm?.SalaryCapManager == null)
            {
                var none = B.Text(scroll, "None", "Financial data unavailable.", 13, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
                return;
            }

            BuildCapSituation(scroll, gm);
            BuildOwnerCard(scroll, gm);
            BuildRevenueCard(scroll, gm);
            BuildCapSheet(scroll, gm);
        }

        // ==================== CAP SITUATION ====================

        private void BuildCapSituation(RectTransform scroll, GameManager gm)
        {
            var summary = gm.SalaryCapManager.GetFinancialSummary(_team.TeamId);
            long payroll = summary?.TotalPayroll ?? 0;
            long capSpace = summary?.CapSpace ?? 0;
            long taxRoom = summary?.TaxRoomRemaining ?? 0;
            long taxOwed = summary?.LuxuryTaxOwed ?? 0;

            var card = B.Card(scroll, "CAP SITUATION", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 122;

            string taxLine = taxOwed > 0
                ? $"<color=#EF4444>Luxury tax owed: <b>{Money(taxOwed)}</b>{(summary?.IsRepeater == true ? " (repeater rates)" : "")}</color>"
                : $"Room under the tax line: <b>{Money(Math.Max(0, taxRoom))}</b>";

            var text = B.Text(card, "Body",
                $"Payroll: <b>{Money(payroll)}</b>   ·   Salary cap: {Money(LeagueCBA.SALARY_CAP)}   ·   Tax line: {Money(LeagueCBA.LUXURY_TAX_LINE)}\n" +
                $"Cap space: <b>{Money(capSpace)}</b>   ·   Active contracts: {summary?.ActiveContracts ?? 0}\n" +
                taxLine,
                13, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(text);
            text.lineSpacing = 1.35f;
        }

        // ==================== OWNER ====================

        private void BuildOwnerCard(RectTransform scroll, GameManager gm)
        {
            var fin = gm.FinanceManager?.GetTeamFinances(_team.TeamId);
            var owner = fin?.TeamOwner;

            var card = B.Card(scroll, "OWNERSHIP", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 128;

            if (owner == null)
            {
                var none = B.Text(card, "Body", "Ownership information unavailable.", 12, FontStyle.Italic, UITheme.TextSecondary);
                B.FillCard(none);
                return;
            }

            bool playoffBound = _team.WinPercentage >= 0.5f;
            var mood = fin.GetOwnerMood(_team.Wins, _team.Losses, playoffBound);
            string expectations = owner.ExpectsChampionship ? "a championship"
                : owner.ExpectsDeepPlayoffRun ? "a deep playoff run"
                : owner.ExpectsPlayoffs ? "a playoff berth"
                : $"at least {owner.MinAcceptableWins} wins";

            var text = B.Text(card, "Body",
                $"<b>{owner.FullName}</b>  ·  {SpendingLabel(owner.SpendingType)}  ·  {PhilosophyLabel(owner.Philosophy)}\n" +
                $"Expects {expectations} this season.   Staff budget: {Money(fin.GetStaffBudget())}\n" +
                $"Mood: <b>{mood}</b> — \"{owner.GetOwnerQuote(mood)}\"",
                12, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(text);
            text.lineSpacing = 1.35f;
        }

        private static string SpendingLabel(OwnerType type) => type switch
        {
            OwnerType.Lavish => "spends freely",
            OwnerType.Competitive => "spends to win",
            OwnerType.Balanced => "balances the books",
            OwnerType.FrugalButFair => "frugal but fair",
            OwnerType.Cheap => "watches every dollar",
            _ => "owner"
        };

        private static string PhilosophyLabel(OwnerPhilosophy p) => p switch
        {
            OwnerPhilosophy.WinNow => "win-now mandate",
            OwnerPhilosophy.SustainedSuccess => "sustained success",
            OwnerPhilosophy.Development => "development-first",
            OwnerPhilosophy.Profit => "profit-minded",
            _ => "balanced outlook"
        };

        // ==================== REVENUE ====================

        private void BuildRevenueCard(RectTransform scroll, GameManager gm)
        {
            var revenue = gm.RevenueManager;
            var data = revenue?.GetSeasonData(_team.TeamId);

            var card = B.Card(scroll, "REVENUE", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;

            if (data == null)
            {
                var none = B.Text(card, "Body", "Revenue tracking starts with the season.", 12, FontStyle.Italic, UITheme.TextSecondary);
                B.FillCard(none);
                return;
            }

            float toDate = revenue.GetRevenueToDate(_team.TeamId);
            float projected = data.BaseProjection?.Total ?? 0f;

            var text = B.Text(card, "Body",
                $"Season to date: <b>${toDate:0.0}M</b> across {data.GamesPlayed} home dates\n" +
                $"Projected full season: ${projected:0.0}M  ·  {data.MarketSize} market",
                13, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(text);
            text.lineSpacing = 1.35f;
        }

        // ==================== CAP SHEET ====================

        private void BuildCapSheet(RectTransform scroll, GameManager gm)
        {
            var contracts = gm.SalaryCapManager.GetTeamContracts(_team.TeamId)
                .OrderByDescending(c => c.CurrentYearSalary)
                .ToList();

            var card = B.Card(scroll, "CAP SHEET", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(80, 52 + (contracts.Count + 1) * 24);

            var body = B.Child(card, "Body");
            var rt = body.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4, 8); rt.offsetMax = new Vector2(-4, -36);
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.spacing = 1;

            var header = B.TableRow(rt, 22, UITheme.FMCardHeaderBg);
            B.TableCell(header, "PLAYER", 210, FontStyle.Bold, UITheme.TextSecondary, 11);
            B.TableCell(header, "THIS YEAR", 90, FontStyle.Bold, UITheme.TextSecondary, 11);
            B.TableCell(header, "+1", 80, FontStyle.Bold, UITheme.TextSecondary, 11);
            B.TableCell(header, "+2", 80, FontStyle.Bold, UITheme.TextSecondary, 11);
            B.TableCell(header, "YRS", 44, FontStyle.Bold, UITheme.TextSecondary, 11);
            B.TableCell(header, "TERMS", 110, FontStyle.Bold, UITheme.TextSecondary, 11);

            if (contracts.Count == 0)
            {
                var none = B.Text(rt, "None", "No contracts on the books.", 12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
                return;
            }

            int i = 0;
            foreach (var contract in contracts)
            {
                var player = gm.PlayerDatabase?.GetPlayer(contract.PlayerId);
                string name = player?.FullName ?? contract.PlayerId;

                var row = B.TableRow(rt, 23, i++ % 2 == 0 ? UITheme.PanelSurface : UITheme.CardBackground);
                B.TableCell(row, name, 210, FontStyle.Normal, Color.white, 12);
                B.TableCell(row, Money(contract.CurrentYearSalary), 90);
                B.TableCell(row, YearOrDash(contract, 1), 80);
                B.TableCell(row, YearOrDash(contract, 2), 80);
                B.TableCell(row, contract.YearsRemaining.ToString(), 44);
                B.TableCell(row, Terms(contract), 110, FontStyle.Normal, UITheme.TextSecondary, 11);
            }
        }

        private static string YearOrDash(Contract c, int yearsFromNow)
        {
            long salary = c.GetSalaryForYear(yearsFromNow);
            return salary > 0 ? Money(salary) : "—";
        }

        private static string Terms(Contract c)
        {
            var terms = new List<string>();
            if (c.HasPlayerOption) terms.Add("PO");
            if (c.HasTeamOption) terms.Add("TO");
            if (c.HasNoTradeClause) terms.Add("NTC");
            if (c.IsExpiring) terms.Add("expiring");
            if (c.Type == ContractType.RookieScale) terms.Add("rookie");
            if (c.Type == ContractType.TwoWay) terms.Add("two-way");
            return terms.Count > 0 ? string.Join(", ", terms) : "guaranteed";
        }

        private static string Money(long amount) => $"${amount / 1_000_000f:0.0}M";
    }
}
