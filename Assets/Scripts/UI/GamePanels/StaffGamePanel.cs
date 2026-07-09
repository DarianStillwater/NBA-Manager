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
    /// The staff office: your coaches and scouts (with what they actually do for
    /// you), the owner's staff budget, and a hiring market of candidates.
    /// Better development staff grows players faster, better medical staff
    /// shortens injuries, better scouts write sharper reports.
    /// </summary>
    public class StaffGamePanel : IGamePanel
    {
        private string _status;
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

            var title = B.Text(rootRT, "Title", "STAFF", 18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            var bodyGo = B.Child(rootRT, "Body");
            bodyGo.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scroll = B.FixedArea(bodyGo.GetComponent<RectTransform>());

            var gm = GameManager.Instance;
            var personnel = gm?.PersonnelManager;
            if (gm == null || personnel == null)
            {
                var none = B.Text(scroll, "None", "Staff data unavailable.", 13, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
                return;
            }

            if (!string.IsNullOrEmpty(_status))
            {
                var note = B.Text(scroll, "Status", _status, 12, FontStyle.Italic, UITheme.Warning);
                note.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            BuildEffectsSummary(scroll, gm, personnel);
            BuildCurrentStaff(scroll, gm, personnel);
            BuildScoutingDesk(scroll, gm, personnel);
            BuildHiringMarket(scroll, gm, personnel);
        }

        private void Refresh() =>
            GameManager.Instance?.GetComponent<GameShell>()?.ShowPanel("Staff");

        // ==================== WHAT STAFF DOES ====================

        private void BuildEffectsSummary(RectTransform scroll, GameManager gm, PersonnelManager personnel)
        {
            float devQuality = personnel.GetDevelopmentQuality(_team.TeamId);
            int staffQuality = personnel.GetStaffQuality(_team.TeamId);
            var bestScout = personnel.GetScouts(_team.TeamId)
                .OrderByDescending(s => s.ScoutingAbility).FirstOrDefault();

            var card = B.Card(scroll, "WHAT YOUR STAFF DELIVERS", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;

            string scoutLine = bestScout != null
                ? $"Lead scout {bestScout.PersonName} ({Grade(bestScout.OverallRating)}) sharpens report accuracy."
                : "<color=#EAB308>No scouts on staff — reports run on your own eye only.</color>";

            var text = B.Text(card, "Body",
                $"Player development: <b>{QualityWord(devQuality)}</b> — drives how much your players improve each season.\n" +
                $"Medical/support quality: <b>{Grade(staffQuality)}</b> — injury recovery up to +{staffQuality * 0.2f:0}% faster.\n" +
                scoutLine,
                12, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(text);
            text.lineSpacing = 1.35f;
        }

        // ==================== CURRENT STAFF ====================

        private void BuildCurrentStaff(RectTransform scroll, GameManager gm, PersonnelManager personnel)
        {
            var staff = personnel.GetTeamStaff(_team.TeamId)
                .OrderByDescending(s => (int)s.CurrentRole)
                .ToList();

            long staffSalaries = staff.Sum(s => (long)s.AnnualSalary);
            var fin = gm.FinanceManager?.GetTeamFinances(_team.TeamId);
            long budget = fin?.GetStaffBudget() ?? 15_000_000L;

            var budgetLine = B.Text(scroll, "Budget",
                $"Staff budget: <b>{Money(budget)}</b>   ·   Committed: <b>{Money(staffSalaries)}</b>   ·   Available: <b>{Money(Math.Max(0, budget - staffSalaries))}</b>",
                13, FontStyle.Normal, UITheme.TextSecondary);
            budgetLine.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            var card = B.Card(scroll, "YOUR STAFF", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + Mathf.Max(1, staff.Count) * 26);

            var rt = CardBody(card);
            if (staff.Count == 0)
            {
                var none = B.Text(rt, "None", "Nobody on staff yet — the hiring market is below.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            foreach (var member in staff)
            {
                var row = StaffRow(rt, $"S_{member.ProfileId}",
                    $"{member.PersonName}  ·  {RoleLabel(member.CurrentRole)}  ·  {Grade(member.OverallRating)}  ·  {Money(member.AnnualSalary)}/yr" +
                    (member.IsUserControlled ? "  ·  <color=white><b>you</b></color>" : ""));

                if (member.IsUserControlled) continue;

                string profileId = member.ProfileId;
                string personName = member.PersonName;
                RowButton(row, "Fire", "FIRE", UITheme.Danger, () =>
                {
                    var (ok, message) = personnel.FirePersonnel(profileId, "Front office restructuring");
                    _status = ok ? $"{personName} has been let go." : $"Can't do that: {message}";
                    Refresh();
                });
            }
        }

        // ==================== SCOUTING DESK ====================

        private void BuildScoutingDesk(RectTransform scroll, GameManager gm, PersonnelManager personnel)
        {
            var scouting = gm.Scouting;
            if (scouting == null) return;

            var scouts = personnel.GetScouts(_team.TeamId);
            var assignments = personnel.GetActiveAssignments(_team.TeamId);

            // Scouts and what they're working on
            var deskCard = B.Card(scroll, "SCOUTING DESK", _teamColor);
            deskCard.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + Mathf.Max(1, scouts.Count) * 24);
            var deskRt = CardBody(deskCard);

            if (scouts.Count == 0)
            {
                var none = B.Text(deskRt, "None",
                    "No scouts on staff — hire one below or you're drafting blind.",
                    12, FontStyle.Italic, UITheme.Warning);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            string idleScoutId = null;
            foreach (var scout in scouts)
            {
                var job = assignments.FirstOrDefault(a => a.StaffId == scout.ProfileId);
                string line;
                if (job != null)
                {
                    var preview = scouting.GetProspectPreview(gm.CurrentSeason);
                    string targetId = job.ScoutTargetPlayerIds?.FirstOrDefault();
                    string targetName = preview.FirstOrDefault(p => p.ProspectId == targetId)?.FullName
                        ?? gm.PlayerDatabase?.GetPlayer(targetId)?.FullName ?? "a target";
                    line = $"{scout.PersonName}  ·  watching {targetName}  ·  {job.Progress:P0} done";
                }
                else
                {
                    line = $"{scout.PersonName}  ·  <color=#EAB308>idle — pick a prospect below</color>";
                    idleScoutId = idleScoutId ?? scout.ProfileId;
                }
                StaffRow(deskRt, $"Sc_{scout.ProfileId}", line);
            }

            // Draft class preview — the fog of war
            var prospects = scouting.GetProspectPreview(gm.CurrentSeason).Take(15).ToList();
            if (prospects.Count == 0) return;

            var board = B.Card(scroll, "NEXT DRAFT CLASS — SCOUTED PROSPECTS STOP BEING GAMBLES", _teamColor);
            board.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 + prospects.Count * 26;
            var rt = CardBody(board);

            foreach (var prospect in prospects)
            {
                bool scouted = scouting.IsScouted(prospect.ProspectId);
                var row = StaffRow(rt, $"Pr_{prospect.ProspectId}",
                    $"Mock #{prospect.MockDraftPosition}  {prospect.FullName}  ·  {prospect.Position}  ·  {prospect.College}  ·  " +
                    (scouted
                        ? $"<color=white>{scouting.DescribeProspect(prospect.ProspectId)}</color>"
                        : "<color=#EAB308>unscouted</color>"));

                if (idleScoutId == null) continue;
                string pid = prospect.ProspectId;
                string sid = idleScoutId;
                RowButton(row, "Scout", scouted ? "RE-SCOUT" : "SCOUT", UITheme.AccentSecondary, () =>
                {
                    var (ok, message) = gm.Scouting.AssignScoutToProspect(sid, pid, gm.CurrentSeason);
                    _status = ok
                        ? $"Scout dispatched — the report lands in about two weeks."
                        : $"Assignment failed: {message}";
                    Refresh();
                });
            }
        }

        // ==================== HIRING MARKET ====================

        private void BuildHiringMarket(RectTransform scroll, GameManager gm, PersonnelManager personnel)
        {
            var pool = personnel.GetUnemployedPool()
                .Where(p => p != null && !p.IsUserControlled
                    && p.CurrentRole != UnifiedRole.Unemployed
                    && p.CurrentRole != UnifiedRole.Retired)
                .OrderByDescending(p => p.MarketValue)
                .Take(20)
                .ToList();

            var card = B.Card(scroll, "HIRING MARKET", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + Mathf.Max(1, pool.Count) * 26);

            var rt = CardBody(card);
            if (pool.Count == 0)
            {
                var none = B.Text(rt, "None", "Nobody credible is looking for work right now.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                return;
            }

            long staffSalaries = personnel.GetTeamStaff(_team.TeamId).Sum(s => (long)s.AnnualSalary);
            long budget = gm.FinanceManager?.GetTeamFinances(_team.TeamId)?.GetStaffBudget() ?? 15_000_000L;

            foreach (var candidate in pool)
            {
                var row = StaffRow(rt, $"C_{candidate.ProfileId}",
                    $"{candidate.PersonName}  ·  {RoleLabel(candidate.CurrentRole)}  ·  {Grade(candidate.OverallRating)}  ·  asks {Money(candidate.MarketValue)}/yr");

                string profileId = candidate.ProfileId;
                string personName = candidate.PersonName;
                UnifiedRole role = candidate.CurrentRole;
                int asking = candidate.MarketValue;

                RowButton(row, "Hire", "HIRE", UITheme.Success, () =>
                {
                    if (staffSalaries + asking > budget)
                    {
                        _status = $"The owner won't stretch the staff budget for {personName}.";
                        Refresh();
                        return;
                    }
                    var (ok, message) = personnel.HirePersonnel(profileId, _team.TeamId, _team.FullName, role, asking, 3);
                    _status = ok ? $"{personName} joins the staff as {RoleLabel(role)}." : $"Hire failed: {message}";
                    Refresh();
                });
            }
        }

        // ==================== HELPERS ====================

        private RectTransform CardBody(RectTransform card)
        {
            var body = B.Child(card, "Body");
            var rt = body.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 8); rt.offsetMax = new Vector2(-12, -36);
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.spacing = 2;
            return rt;
        }

        private GameObject StaffRow(RectTransform parent, string name, string label)
        {
            var row = B.Child(parent, name);
            row.AddComponent<LayoutElement>().preferredHeight = 24;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.spacing = 8;

            var text = B.Text(row.GetComponent<RectTransform>(), "L", label, 12, FontStyle.Normal, UITheme.TextSecondary);
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            return row;
        }

        private void RowButton(GameObject row, string name, string label, Color color, Action onClick)
        {
            var btn = B.Child(row.GetComponent<RectTransform>(), name);
            btn.AddComponent<LayoutElement>().preferredWidth = 70;
            btn.AddComponent<Image>().color = UITheme.DarkenColor(color, 0.55f);
            btn.AddComponent<Button>().onClick.AddListener(() => onClick());
            var bt = B.Text(btn.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Bold, Color.white);
            B.Stretch(bt.gameObject); bt.alignment = TextAnchor.MiddleCenter;
        }

        private static string RoleLabel(UnifiedRole role) => role switch
        {
            UnifiedRole.HeadCoach => "Head Coach",
            UnifiedRole.AssistantCoach => "Assistant Coach",
            UnifiedRole.PositionCoach => "Position Coach",
            UnifiedRole.OffensiveCoordinator => "Offensive Coordinator",
            UnifiedRole.DefensiveCoordinator => "Defensive Coordinator",
            UnifiedRole.Coordinator => "Coordinator",
            UnifiedRole.Scout => "Scout",
            UnifiedRole.GeneralManager => "General Manager",
            UnifiedRole.AssistantGM => "Assistant GM",
            _ => role.ToString()
        };

        /// <summary>Staff quality shows as grades, never raw numbers.</summary>
        private static string Grade(int rating)
        {
            if (rating >= 88) return "A+";
            if (rating >= 82) return "A";
            if (rating >= 75) return "B+";
            if (rating >= 68) return "B";
            if (rating >= 58) return "C";
            return "D";
        }

        private static string QualityWord(float quality01)
        {
            if (quality01 >= 0.75f) return "elite";
            if (quality01 >= 0.62f) return "strong";
            if (quality01 >= 0.48f) return "average";
            if (quality01 >= 0.35f) return "thin";
            return "poor";
        }

        private static string Money(long amount) =>
            amount >= 1_000_000 ? $"${amount / 1_000_000f:0.0}M" : $"${amount / 1_000f:0}K";
    }
}
