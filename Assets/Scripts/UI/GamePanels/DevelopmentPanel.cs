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
    /// The development desk: weekly practice plan (focus + intensity), mentorship
    /// pairings (assign veterans to kids, watch the bond grow), and tendency
    /// coaching (up to three behavioral focuses per player). All effects flow
    /// through the weekly Wednesday development cycle.
    /// </summary>
    public class DevelopmentPanel : IGamePanel
    {
        private string _tab = "PRACTICE";
        private Team _team;
        private Color _teamColor;
        private string _status;

        // Mentorship pairing flow state
        private string _pickedMentorId;

        // Tendency coaching flow state
        private string _pickedPlayerId;

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

            var title = B.Text(rootRT, "Title", "DEVELOPMENT", 18, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>(); titleLE.preferredHeight = 24; titleLE.flexibleHeight = 0;

            var tabs = B.Child(rootRT, "Tabs");
            var tabsLE = tabs.AddComponent<LayoutElement>(); tabsLE.preferredHeight = 28; tabsLE.flexibleHeight = 0;
            var hlg = tabs.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            foreach (var name in new[] { "PRACTICE", "MENTORSHIP", "TENDENCIES" })
                BuildTabButton(tabs.GetComponent<RectTransform>(), name);

            var bodyGo = B.Child(rootRT, "Body");
            bodyGo.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scroll = B.FixedArea(bodyGo.GetComponent<RectTransform>());

            if (!string.IsNullOrEmpty(_status))
            {
                var note = B.Text(scroll, "Status", _status, 12, FontStyle.Italic, UITheme.Warning);
                note.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            switch (_tab)
            {
                case "MENTORSHIP": BuildMentorship(scroll); break;
                case "TENDENCIES": BuildTendencies(scroll); break;
                default: BuildPractice(scroll); break;
            }
        }

        private void BuildTabButton(RectTransform parent, string name)
        {
            var go = B.Child(parent, $"Tab_{name}");
            bool active = _tab == name;
            go.AddComponent<Image>().color = active
                ? UITheme.DarkenColor(_teamColor, 0.5f) : UITheme.FMCardHeaderBg;
            go.AddComponent<Button>().onClick.AddListener(() => { _tab = name; Refresh(); });
            var t = B.Text(go.GetComponent<RectTransform>(), "T", name, 12,
                active ? FontStyle.Bold : FontStyle.Normal,
                active ? Color.white : UITheme.TextSecondary);
            B.Stretch(t.gameObject); t.alignment = TextAnchor.MiddleCenter;
        }

        private void Refresh() =>
            GameManager.Instance?.GetComponent<GameShell>()?.ShowPanel("Development");

        // ==================== PRACTICE ====================

        private void BuildPractice(RectTransform scroll)
        {
            var desk = GameManager.Instance?.DevelopmentDesk;
            if (desk == null) { Empty(scroll, "Development desk unavailable."); return; }

            var plan = B.Card(scroll, "WEEKLY PRACTICE PLAN — RUNS EVERY WEDNESDAY", _teamColor);
            plan.gameObject.AddComponent<LayoutElement>().preferredHeight = 148;
            var rt = CardBody(plan);

            var focusHead = B.Text(rt, "FH", "Primary focus", 12, FontStyle.Bold, UITheme.AccentSecondary);
            focusHead.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            var focusRow = OptionRow(rt, 26);
            foreach (var focus in new[] { PracticeFocus.Development, PracticeFocus.GamePrep,
                                          PracticeFocus.TeamConcepts, PracticeFocus.Conditioning,
                                          PracticeFocus.Recovery })
            {
                var f = focus;
                OptionButton(focusRow, FocusLabel(f), desk.WeeklyFocus == f, () =>
                {
                    desk.WeeklyFocus = f;
                    _status = $"Practice focus set to {FocusLabel(f)}.";
                    Refresh();
                });
            }

            var intHead = B.Text(rt, "IH", "Intensity — harder practices develop faster but burn energy and risk knocks", 12, FontStyle.Bold, UITheme.AccentSecondary);
            intHead.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            var intRow = OptionRow(rt, 26);
            foreach (var intensity in new[] { PracticeIntensity.Light, PracticeIntensity.Normal,
                                              PracticeIntensity.High })
            {
                var i = intensity;
                OptionButton(intRow, i.ToString().ToUpper(), desk.WeeklyIntensity == i, () =>
                {
                    desk.WeeklyIntensity = i;
                    _status = $"Practice intensity set to {i}.";
                    Refresh();
                });
            }

            var last = B.Card(scroll, "LAST SESSION", _teamColor);
            last.gameObject.AddComponent<LayoutElement>().preferredHeight = 74;
            var text = B.Text(last, "Body",
                string.IsNullOrEmpty(desk.LastPracticeSummary)
                    ? "No practice run yet — the first session hits Wednesday."
                    : desk.LastPracticeSummary,
                12, FontStyle.Normal, UITheme.TextSecondary);
            B.FillCard(text);
        }

        private static string FocusLabel(PracticeFocus focus) => focus switch
        {
            PracticeFocus.Development => "DEVELOPMENT",
            PracticeFocus.GamePrep => "GAME PREP",
            PracticeFocus.TeamConcepts => "TEAM CONCEPTS",
            PracticeFocus.Conditioning => "CONDITIONING",
            PracticeFocus.Recovery => "RECOVERY",
            _ => focus.ToString().ToUpper()
        };

        // ==================== MENTORSHIP ====================

        private void BuildMentorship(RectTransform scroll)
        {
            var gm = GameManager.Instance;
            var mm = MentorshipManager.Instance;
            if (gm == null || mm == null) { Empty(scroll, "Mentorship unavailable."); return; }

            var roster = _team.RosterPlayerIds
                .Select(id => gm.PlayerDatabase.GetPlayer(id))
                .Where(p => p != null && !p.IsInjured || p != null)
                .ToList();

            // Active pairings
            var pairings = mm.GetTeamRelationships(_team.TeamId).Where(r => r.IsActive).ToList();
            var card = B.Card(scroll, "ACTIVE PAIRINGS", _teamColor);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + Mathf.Max(1, pairings.Count) * 26);
            var rt = CardBody(card);

            if (pairings.Count == 0)
            {
                var none = B.Text(rt, "None", "No mentorships yet — pair a veteran with a young player below.",
                    12, FontStyle.Italic, UITheme.TextSecondary);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            }

            foreach (var rel in pairings)
            {
                string mentorName = gm.PlayerDatabase.GetPlayer(rel.MentorPlayerId)?.FullName ?? rel.MentorPlayerId;
                string menteeName = gm.PlayerDatabase.GetPlayer(rel.MenteePlayerId)?.FullName ?? rel.MenteePlayerId;
                var row = TextRow(rt, $"Rel_{rel.RelationshipId}",
                    $"{mentorName} → {menteeName}  ·  {rel.PrimaryFocus}  ·  bond {BondWord(rel.RelationshipStrength)}  ·  {rel.SessionsCompleted} sessions");

                string relId = rel.RelationshipId;
                RowButton(row, "End", "DISSOLVE", UITheme.Danger, () =>
                {
                    mm.DissolveMentorship(relId, MentorshipEndReason.CoachDissolved);
                    _status = "Mentorship dissolved.";
                    Refresh();
                });
            }

            // Pairing builder
            var mentors = mm.GetAvailableMentors(roster);
            var mentees = mm.GetPotentialMentees(roster)
                .Where(p => mm.GetMenteeCurrentMentor(p.PlayerId) == null)
                .ToList();

            var build = B.Card(scroll, _pickedMentorId == null
                ? "ASSIGN A MENTOR — PICK THE VETERAN"
                : "NOW PICK THE YOUNG PLAYER", _teamColor);
            int rows = _pickedMentorId == null ? mentors.Count : mentees.Count;
            build.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + Mathf.Max(1, rows) * 26);
            var brt = CardBody(build);

            if (_pickedMentorId == null)
            {
                if (mentors.Count == 0)
                {
                    var none = B.Text(brt, "None", "No willing veteran mentors on the roster (needs 4+ years in the league).",
                        12, FontStyle.Italic, UITheme.TextSecondary);
                    none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                }
                foreach (var (mentor, profile) in mentors)
                {
                    var row = TextRow(brt, $"M_{mentor.PlayerId}",
                        $"{mentor.FullName}  ·  {mentor.YearsPro}y pro  ·  {profile.Tier} mentor  ·  can take {profile.MaxMentees - profile.CurrentMenteeCount} more");
                    string mid = mentor.PlayerId;
                    RowButton(row, "Pick", "SELECT", UITheme.AccentSecondary, () =>
                    {
                        _pickedMentorId = mid;
                        Refresh();
                    });
                }
            }
            else
            {
                var mentor = gm.PlayerDatabase.GetPlayer(_pickedMentorId);
                var back = TextRow(brt, "Back", $"Mentor: <b>{mentor?.FullName}</b>");
                RowButton(back, "Cancel", "CANCEL", UITheme.Danger, () =>
                {
                    _pickedMentorId = null;
                    Refresh();
                });

                if (mentees.Count == 0)
                {
                    var none = B.Text(brt, "None", "No unpaired young players (4 or fewer years in the league).",
                        12, FontStyle.Italic, UITheme.TextSecondary);
                    none.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
                }
                foreach (var mentee in mentees)
                {
                    if (mentee.PlayerId == _pickedMentorId) continue;
                    var row = TextRow(brt, $"T_{mentee.PlayerId}",
                        $"{mentee.FullName}  ·  {mentee.YearsPro}y pro  ·  age {mentee.Age}");
                    string tid = mentee.PlayerId;
                    RowButton(row, "Pair", "PAIR", UITheme.Success, () =>
                    {
                        var m = gm.PlayerDatabase.GetPlayer(_pickedMentorId);
                        var t = gm.PlayerDatabase.GetPlayer(tid);
                        var (ok, message, _) = mm.AssignMentor(m, t, _team.TeamId, MentorFocusArea.General);
                        _status = ok ? $"{m.FullName} is now mentoring {t.FullName}." : $"Pairing failed: {message}";
                        _pickedMentorId = null;
                        Refresh();
                    });
                }
            }
        }

        private static string BondWord(int strength)
        {
            if (strength >= 75) return "inseparable";
            if (strength >= 55) return "strong";
            if (strength >= 35) return "growing";
            if (strength >= 15) return "new";
            return "strained";
        }

        // ==================== TENDENCIES ====================

        private void BuildTendencies(RectTransform scroll)
        {
            var gm = GameManager.Instance;
            var desk = gm?.DevelopmentDesk;
            var tcm = TendencyCoachingManager.Instance;
            if (gm == null || tcm == null) { Empty(scroll, "Tendency coaching unavailable."); return; }

            // Recent results feed
            if (desk?.LastTrainingResults?.Count > 0)
            {
                var feed = B.Card(scroll, "THIS WEEK ON THE PRACTICE FLOOR", _teamColor);
                var shown = desk.LastTrainingResults.Take(6).ToList();
                feed.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 + shown.Count * 18;
                var lines = shown.Select(r => r.Improved
                    ? $"<color=white><b>{r.PlayerName}</b></color> improved {Pretty(r.TendencyName)}"
                    : r.Resisted
                        ? $"{r.PlayerName} pushed back on {Pretty(r.TendencyName)} coaching"
                        : $"{r.PlayerName} kept grinding on {Pretty(r.TendencyName)}");
                var text = B.Text(feed, "Feed", string.Join("\n", lines), 12, FontStyle.Normal, UITheme.TextSecondary);
                B.FillCard(text); text.lineSpacing = 1.2f;
            }

            // Player picker
            var roster = _team.RosterPlayerIds
                .Select(id => gm.PlayerDatabase.GetPlayer(id))
                .Where(p => p != null)
                .OrderByDescending(p => p.Tendencies?.CurrentTrainingFocus?.Count ?? 0)
                .ToList();

            var picker = B.Card(scroll, "PICK A PLAYER TO COACH", _teamColor);
            picker.gameObject.AddComponent<LayoutElement>().preferredHeight =
                Mathf.Max(70, 44 + roster.Count * 26);
            var rt = CardBody(picker);

            foreach (var p in roster)
            {
                int focusCount = p.Tendencies?.CurrentTrainingFocus?.Count ?? 0;
                bool selected = p.PlayerId == _pickedPlayerId;
                var row = TextRow(rt, $"P_{p.PlayerId}",
                    (selected ? "<color=white><b>▶ </b></color>" : "") +
                    $"{p.FullName}  ·  {p.Age}y" +
                    (focusCount > 0 ? $"  ·  training {focusCount} habit{(focusCount > 1 ? "s" : "")}" : ""));
                string pid = p.PlayerId;
                RowButton(row, "Pick", selected ? "CLOSE" : "COACH", UITheme.AccentSecondary, () =>
                {
                    _pickedPlayerId = selected ? null : pid;
                    Refresh();
                });
            }

            // Selected player's coachable habits
            if (string.IsNullOrEmpty(_pickedPlayerId)) return;
            var player = gm.PlayerDatabase.GetPlayer(_pickedPlayerId);
            if (player?.Tendencies == null) return;

            var habits = tcm.GetAllCoachableTendencies();
            var detail = B.Card(scroll, $"COACHING {player.FullName.ToUpper()} — MAX 3 FOCUSES", _teamColor);
            detail.gameObject.AddComponent<LayoutElement>().preferredHeight = 44 + habits.Count * 26;
            var drt = CardBody(detail);

            foreach (var habit in habits)
            {
                int value = tcm.GetTendencyValue(player.Tendencies, habit);
                bool training = player.Tendencies.CurrentTrainingFocus?.Contains(habit) ?? false;
                var row = TextRow(drt, $"H_{habit}",
                    $"{Pretty(habit)}  ·  {HabitWord(value)}" + (training ? "  ·  <color=white><b>in training</b></color>" : ""));

                string h = habit;
                RowButton(row, "Toggle", training ? "STOP" : "FOCUS",
                    training ? UITheme.Danger : UITheme.Success, () =>
                {
                    var (ok, message) = training
                        ? tcm.RemoveTrainingFocus(player.PlayerId, h)
                        : tcm.AssignTrainingFocus(player.PlayerId, h);
                    _status = ok
                        ? (training ? $"Stopped coaching {Pretty(h)}." : $"Now coaching {Pretty(h)} with {player.FullName}.")
                        : message;
                    Refresh();
                });
            }
        }

        /// <summary>Habit level as a descriptor — never raw numbers.</summary>
        private static string HabitWord(int value)
        {
            if (value >= 85) return "a real strength";
            if (value >= 70) return "reliable";
            if (value >= 55) return "decent";
            if (value >= 40) return "inconsistent";
            return "a bad habit";
        }

        private static string Pretty(string tendencyName)
        {
            if (string.IsNullOrEmpty(tendencyName)) return tendencyName;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < tendencyName.Length; i++)
            {
                if (i > 0 && char.IsUpper(tendencyName[i]) &&
                    (i + 1 >= tendencyName.Length || !char.IsUpper(tendencyName[i + 1])))
                    sb.Append(' ');
                sb.Append(i == 0 ? tendencyName[i] : char.ToLower(tendencyName[i]));
            }
            return sb.ToString().Replace("pn r", "pick-and-roll");
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

        private GameObject OptionRow(RectTransform parent, float height)
        {
            var row = B.Child(parent, "OptRow");
            row.AddComponent<LayoutElement>().preferredHeight = height;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            return row;
        }

        private void OptionButton(GameObject row, string label, bool selected, Action onClick)
        {
            var btn = B.Child(row.GetComponent<RectTransform>(), $"Opt_{label}");
            btn.AddComponent<Image>().color = selected
                ? UITheme.DarkenColor(_teamColor, 0.45f) : UITheme.FMCardHeaderBg;
            btn.AddComponent<Button>().onClick.AddListener(() => onClick());
            var t = B.Text(btn.GetComponent<RectTransform>(), "T", label, 11,
                selected ? FontStyle.Bold : FontStyle.Normal,
                selected ? Color.white : UITheme.TextSecondary);
            B.Stretch(t.gameObject); t.alignment = TextAnchor.MiddleCenter;
        }

        private GameObject TextRow(RectTransform parent, string name, string label)
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
            btn.AddComponent<LayoutElement>().preferredWidth = 84;
            btn.AddComponent<Image>().color = UITheme.DarkenColor(color, 0.55f);
            btn.AddComponent<Button>().onClick.AddListener(() => onClick());
            var bt = B.Text(btn.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Bold, Color.white);
            B.Stretch(bt.gameObject); bt.alignment = TextAnchor.MiddleCenter;
        }

        private void Empty(RectTransform scroll, string message)
        {
            var text = B.Text(scroll, "Empty", message, 13, FontStyle.Italic, UITheme.TextSecondary);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
        }
    }
}
