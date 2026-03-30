using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Shell;

namespace NBAHeadCoach.UI.GamePanels
{
    public class PreGameGamePanel : IGamePanel
    {
        private readonly GameShell _shell;
        private CalendarEvent _gameEvent;

        // Mutable pregame state
        private string[] _lineup = new string[5];
        private OffensiveSystemType _offense;
        private DefensiveSchemeType _defense;
        private PacePreference _pace;

        public PreGameGamePanel(GameShell shell) { _shell = shell; }
        public void SetGameEvent(CalendarEvent gameEvent) { _gameEvent = gameEvent; }

        public void Build(RectTransform parent, Team team, Color teamColor)
        {
            if (_gameEvent == null) return;
            var gm = GameManager.Instance;
            var playerTeamId = gm?.PlayerTeamId;
            bool isHome = _gameEvent.HomeTeamId == playerTeamId;
            string oppId = isHome ? _gameEvent.AwayTeamId : _gameEvent.HomeTeamId;
            var playerTeam = gm?.GetTeam(playerTeamId);
            var oppTeam = gm?.GetTeam(oppId);
            if (playerTeam == null || oppTeam == null) return;

            // Copy current lineup/strategy as starting state
            Array.Copy(playerTeam.StartingLineupIds, _lineup, 5);
            _offense = playerTeam.Strategy?.OffensiveSystem?.PrimarySystem ?? OffensiveSystemType.MotionOffense;
            _defense = playerTeam.Strategy?.DefensiveSystem?.PrimaryScheme ?? DefensiveSchemeType.ManToManStandard;
            _pace = playerTeam.Strategy?.PacePreference ?? PacePreference.Balanced;

            // Card fills content area
            var card = UIBuilder.Card(parent, "GAME DAY", teamColor);
            card.anchorMin = Vector2.zero; card.anchorMax = Vector2.one; card.sizeDelta = Vector2.zero;

            var body = UIBuilder.Child(card, "Body");
            var br = body.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
            br.offsetMin = Vector2.zero; br.offsetMax = new Vector2(0, -UITheme.FMCardHeaderHeight);
            var vlg = body.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 4; vlg.padding = new RectOffset(16, 16, 8, 8);

            // ── Matchup header ──
            string homeLabel = isHome ? $"{playerTeam.City} {playerTeam.Name}" : $"{oppTeam.City} {oppTeam.Name}";
            string awayLabel = isHome ? $"{oppTeam.City} {oppTeam.Name}" : $"{playerTeam.City} {playerTeam.Name}";
            var matchRow = MkRow(br, 28);
            matchRow.gameObject.AddComponent<Image>().color = UITheme.PanelSurface;
            var mt = UIBuilder.Text(matchRow, "M", $"{awayLabel}  @  {homeLabel}  —  {(isHome ? "HOME" : "AWAY")}", 13, FontStyle.Bold, Color.white);
            UIBuilder.Stretch(mt.gameObject); mt.alignment = TextAnchor.MiddleCenter;

            Spacer(br, 4);

            // ── Tabs ──
            var tabRow = MkRow(br, 30);
            var tabHlg = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 4; tabHlg.childControlWidth = true; tabHlg.childControlHeight = true; tabHlg.childForceExpandWidth = true;

            // Tab content containers
            var lineupContent = MkRow(br, 0); lineupContent.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            var strategyContent = MkRow(br, 0); strategyContent.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scoutingContent = MkRow(br, 0); scoutingContent.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            var panels = new[] { lineupContent.gameObject, strategyContent.gameObject, scoutingContent.gameObject };

            // Tab buttons
            Image[] tabBgs = new Image[3];
            string[] tabNames = { "LINEUP", "STRATEGY", "SCOUTING" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var tgo = UIBuilder.Child(tabRow, tabNames[i]);
                var tbg = tgo.AddComponent<Image>(); tbg.color = i == 0 ? UITheme.FMNavActive : UITheme.PanelSurface;
                tabBgs[i] = tbg;
                tgo.AddComponent<Button>().onClick.AddListener(() => {
                    for (int j = 0; j < 3; j++) { panels[j].SetActive(j == idx); tabBgs[j].color = j == idx ? UITheme.FMNavActive : UITheme.PanelSurface; }
                });
                var tt = UIBuilder.Text(tgo.GetComponent<RectTransform>(), "T", tabNames[i], 11, FontStyle.Bold, Color.white);
                UIBuilder.Stretch(tt.gameObject); tt.alignment = TextAnchor.MiddleCenter;
            }

            // Show only first tab
            strategyContent.gameObject.SetActive(false);
            scoutingContent.gameObject.SetActive(false);

            // ── Build tab contents ──
            BuildLineupTab(lineupContent, playerTeam, oppTeam, gm);
            BuildStrategyTab(strategyContent, playerTeam);
            BuildScoutingTab(scoutingContent, gm, oppTeam);

            Spacer(br, 4);

            // ── Action buttons ──
            var btnRow = MkRow(br, 44);
            var btnHlg = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            btnHlg.spacing = 12; btnHlg.childControlWidth = true; btnHlg.childControlHeight = true; btnHlg.childForceExpandWidth = true;
            btnHlg.padding = new RectOffset(20, 20, 2, 2);

            var capturedEvent = _gameEvent;
            MkActionBtn(btnRow, "SIMULATE GAME", UITheme.Success, () => SimulateAndShowResult(gm, playerTeam, oppTeam, isHome, capturedEvent));
            MkActionBtn(btnRow, "PLAY GAME", UITheme.AccentSecondary, () => {
                ApplyPreGameChoices(playerTeam);
                gm.MatchController?.PrepareMatch(capturedEvent);
                gm.MatchController?.StartInteractiveMatch();
            });
        }

        private void ApplyPreGameChoices(Team playerTeam)
        {
            Array.Copy(_lineup, playerTeam.StartingLineupIds, 5);
            if (playerTeam.Strategy != null)
            {
                if (playerTeam.Strategy.OffensiveSystem != null)
                    playerTeam.Strategy.OffensiveSystem.PrimarySystem = _offense;
                if (playerTeam.Strategy.DefensiveSystem != null)
                    playerTeam.Strategy.DefensiveSystem.PrimaryScheme = _defense;
                playerTeam.Strategy.PacePreference = _pace;
            }
        }

        // ── LINEUP TAB ──

        private void BuildLineupTab(RectTransform parent, Team playerTeam, Team oppTeam, GameManager gm)
        {
            var vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3; vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(8, 8, 4, 4);

            string[] posLabels = { "PG", "SG", "SF", "PF", "C" };
            var roster = playerTeam.RosterPlayerIds ?? new List<string>();
            var oppLineup = oppTeam.StartingLineupIds ?? new string[5];

            for (int i = 0; i < 5; i++)
            {
                int posIdx = i;
                var row = MkRow(parent, 28);
                row.gameObject.AddComponent<Image>().color = i % 2 == 0 ? UITheme.PanelSurface : UITheme.CardBackground;
                var rhlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rhlg.childControlWidth = true; rhlg.childControlHeight = true; rhlg.spacing = 6;
                rhlg.childForceExpandHeight = true; rhlg.padding = new RectOffset(8, 8, 0, 0);

                // Position label
                var posGo = UIBuilder.Child(row, "Pos"); posGo.AddComponent<LayoutElement>().preferredWidth = 30;
                posGo.AddComponent<Image>().color = UITheme.AccentPrimary;
                var pt = UIBuilder.Text(posGo.GetComponent<RectTransform>(), "T", posLabels[i], 10, FontStyle.Bold, Color.white);
                UIBuilder.Stretch(pt.gameObject); pt.alignment = TextAnchor.MiddleCenter;

                // Player name (clickable spinner)
                var nameGo = UIBuilder.Child(row, "Name"); nameGo.AddComponent<LayoutElement>().flexibleWidth = 1;
                nameGo.AddComponent<Image>().color = UITheme.CardBackground;
                string playerName = GetPlayerName(gm, _lineup[posIdx]);
                var nameText = UIBuilder.Text(nameGo.GetComponent<RectTransform>(), "T", playerName, 11, FontStyle.Normal, Color.white);
                UIBuilder.Stretch(nameText.gameObject); nameText.alignment = TextAnchor.MiddleLeft;
                nameText.GetComponent<RectTransform>().offsetMin = new Vector2(8, 0);

                // Cycle arrows
                var leftGo = UIBuilder.Child(row, "L"); leftGo.AddComponent<LayoutElement>().preferredWidth = 24;
                leftGo.AddComponent<Image>().color = UITheme.CardBackground;
                var lt = UIBuilder.Text(leftGo.GetComponent<RectTransform>(), "T", "<", 12, FontStyle.Bold, UITheme.AccentPrimary);
                UIBuilder.Stretch(lt.gameObject); lt.alignment = TextAnchor.MiddleCenter;

                var rightGo = UIBuilder.Child(row, "R"); rightGo.AddComponent<LayoutElement>().preferredWidth = 24;
                rightGo.AddComponent<Image>().color = UITheme.CardBackground;
                var rt = UIBuilder.Text(rightGo.GetComponent<RectTransform>(), "T", ">", 12, FontStyle.Bold, UITheme.AccentPrimary);
                UIBuilder.Stretch(rt.gameObject); rt.alignment = TextAnchor.MiddleCenter;

                // Wire cycling through roster
                leftGo.AddComponent<Button>().onClick.AddListener(() => CyclePlayer(posIdx, -1, roster, nameText, gm));
                rightGo.AddComponent<Button>().onClick.AddListener(() => CyclePlayer(posIdx, 1, roster, nameText, gm));

                // "vs" label
                var vsGo = UIBuilder.Child(row, "VS"); vsGo.AddComponent<LayoutElement>().preferredWidth = 24;
                var vst = UIBuilder.Text(vsGo.GetComponent<RectTransform>(), "T", "vs", 9, FontStyle.Italic, UITheme.TextSecondary);
                UIBuilder.Stretch(vst.gameObject); vst.alignment = TextAnchor.MiddleCenter;

                // Opponent starter
                var oppGo = UIBuilder.Child(row, "Opp"); oppGo.AddComponent<LayoutElement>().flexibleWidth = 0.7f;
                oppGo.AddComponent<Image>().color = UITheme.PanelSurface;
                string oppName = i < oppLineup.Length ? GetPlayerName(gm, oppLineup[i]) : "—";
                var ot = UIBuilder.Text(oppGo.GetComponent<RectTransform>(), "T", oppName, 10, FontStyle.Normal, UITheme.TextSecondary);
                UIBuilder.Stretch(ot.gameObject); ot.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void CyclePlayer(int posIdx, int dir, List<string> roster, Text nameText, GameManager gm)
        {
            if (roster.Count == 0) return;
            string current = _lineup[posIdx];
            int curIdx = roster.IndexOf(current);
            int newIdx = (curIdx + dir + roster.Count) % roster.Count;

            // Skip players already in lineup at other positions
            int safety = 0;
            while (safety++ < roster.Count)
            {
                string candidate = roster[newIdx];
                bool alreadyUsed = false;
                for (int j = 0; j < 5; j++) { if (j != posIdx && _lineup[j] == candidate) { alreadyUsed = true; break; } }
                if (!alreadyUsed) break;
                newIdx = (newIdx + dir + roster.Count) % roster.Count;
            }

            _lineup[posIdx] = roster[newIdx];
            nameText.text = GetPlayerName(gm, roster[newIdx]);
        }

        // ── STRATEGY TAB ──

        private void BuildStrategyTab(RectTransform parent, Team playerTeam)
        {
            var vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(12, 12, 8, 8);

            // Offense
            string[] offNames = Enum.GetNames(typeof(OffensiveSystemType));
            string[] offDisplay = offNames.Select(FormatEnumName).ToArray();
            int offIdx = Array.IndexOf(offNames, _offense.ToString());
            MkCyclePref(parent, "Offense", offDisplay, Mathf.Max(0, offIdx), v => _offense = (OffensiveSystemType)v);

            // Defense
            string[] defNames = Enum.GetNames(typeof(DefensiveSchemeType));
            string[] defDisplay = defNames.Select(FormatEnumName).ToArray();
            int defIdx = Array.IndexOf(defNames, _defense.ToString());
            MkCyclePref(parent, "Defense", defDisplay, Mathf.Max(0, defIdx), v => _defense = (DefensiveSchemeType)v);

            // Pace
            string[] paceNames = Enum.GetNames(typeof(PacePreference));
            int paceIdx = (int)_pace;
            MkCyclePref(parent, "Pace", paceNames, paceIdx, v => _pace = (PacePreference)v);
        }

        // ── SCOUTING TAB ──

        private void BuildScoutingTab(RectTransform parent, GameManager gm, Team oppTeam)
        {
            var vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4; vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(12, 12, 8, 8);

            gm.MatchController?.PrepareMatch(_gameEvent);
            var report = gm.MatchController?.GetOpponentScouting();
            gm.MatchController?.PrepareMatch(_gameEvent); // re-prepare in case scouting mutated state

            if (report == null)
            {
                var nt = UIBuilder.Text(parent, "T", "No scouting data available.", 12, FontStyle.Italic, UITheme.TextSecondary);
                nt.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
                return;
            }

            MkInfoRow(parent, "Record", report.Record);
            MkInfoRow(parent, "Off Rating", $"{report.OffensiveRating:F1}");
            MkInfoRow(parent, "Def Rating", $"{report.DefensiveRating:F1}");
            MkInfoRow(parent, "Pace", $"{report.Pace}");
            if (report.Strengths != null && report.Strengths.Count > 0)
                MkInfoRow(parent, "Strengths", string.Join(", ", report.Strengths));
            if (report.Weaknesses != null && report.Weaknesses.Count > 0)
                MkInfoRow(parent, "Weaknesses", string.Join(", ", report.Weaknesses));
            if (report.RecentForm != null)
                MkInfoRow(parent, "Recent Form", report.RecentForm);
        }

        // ── HELPERS ──

        private string GetPlayerName(GameManager gm, string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return "—";
            var p = gm?.PlayerDatabase?.GetPlayer(playerId);
            return p != null ? $"{p.FirstName[0]}. {p.LastName}" : playerId;
        }

        private static string FormatEnumName(string name)
        {
            // "MotionOffense" → "Motion Offense", "Zone2_3" → "Zone 2-3"
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
                if (name[i] == '_') sb.Append('-');
                else sb.Append(name[i]);
            }
            return sb.ToString();
        }

        private void MkCyclePref(RectTransform parent, string label, string[] options, int currentIndex, Action<int> onChange)
        {
            var row = MkRow(parent, 28);
            row.gameObject.AddComponent<Image>().color = UITheme.PanelSurface;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(12, 8, 0, 0);

            var lblGo = UIBuilder.Child(row, "Lbl"); lblGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            lblGo.AddComponent<Image>().color = Color.clear;
            var lbl = UIBuilder.Text(lblGo.GetComponent<RectTransform>(), "T", label, 11, FontStyle.Bold, UITheme.TextSecondary);
            UIBuilder.Stretch(lbl.gameObject); lbl.alignment = TextAnchor.MiddleLeft;

            int idx = Mathf.Clamp(currentIndex, 0, options.Length - 1);

            var leftGo = UIBuilder.Child(row, "L"); leftGo.AddComponent<LayoutElement>().preferredWidth = 28;
            leftGo.AddComponent<Image>().color = UITheme.CardBackground;
            var lt = UIBuilder.Text(leftGo.GetComponent<RectTransform>(), "T", "<", 13, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.Stretch(lt.gameObject); lt.alignment = TextAnchor.MiddleCenter;

            var valGo = UIBuilder.Child(row, "V"); valGo.AddComponent<LayoutElement>().preferredWidth = 140;
            valGo.AddComponent<Image>().color = UITheme.CardBackground;
            var val = UIBuilder.Text(valGo.GetComponent<RectTransform>(), "T", options[idx], 11, FontStyle.Normal, Color.white);
            UIBuilder.Stretch(val.gameObject); val.alignment = TextAnchor.MiddleCenter;

            var rightGo = UIBuilder.Child(row, "R"); rightGo.AddComponent<LayoutElement>().preferredWidth = 28;
            rightGo.AddComponent<Image>().color = UITheme.CardBackground;
            var rt = UIBuilder.Text(rightGo.GetComponent<RectTransform>(), "T", ">", 13, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.Stretch(rt.gameObject); rt.alignment = TextAnchor.MiddleCenter;

            leftGo.AddComponent<Button>().onClick.AddListener(() => { idx = (idx - 1 + options.Length) % options.Length; val.text = options[idx]; onChange(idx); });
            rightGo.AddComponent<Button>().onClick.AddListener(() => { idx = (idx + 1) % options.Length; val.text = options[idx]; onChange(idx); });
        }

        private void MkInfoRow(RectTransform parent, string label, string value)
        {
            var row = MkRow(parent, 22);
            row.gameObject.AddComponent<Image>().color = UITheme.PanelSurface;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(10, 10, 0, 0);

            var lg = UIBuilder.Child(row, "L"); lg.AddComponent<LayoutElement>().preferredWidth = 90;
            lg.AddComponent<Image>().color = Color.clear;
            var lt = UIBuilder.Text(lg.GetComponent<RectTransform>(), "T", label, 10, FontStyle.Bold, UITheme.AccentPrimary);
            UIBuilder.Stretch(lt.gameObject); lt.alignment = TextAnchor.MiddleLeft;

            var vg = UIBuilder.Child(row, "V"); vg.AddComponent<LayoutElement>().flexibleWidth = 1;
            vg.AddComponent<Image>().color = Color.clear;
            var vt = UIBuilder.Text(vg.GetComponent<RectTransform>(), "T", value, 10, FontStyle.Normal, Color.white);
            UIBuilder.Stretch(vt.gameObject); vt.alignment = TextAnchor.MiddleLeft;
        }

        private static RectTransform MkRow(RectTransform parent, float height)
        {
            var go = UIBuilder.Child(parent, "Row");
            if (height > 0) go.AddComponent<LayoutElement>().preferredHeight = height;
            return go.GetComponent<RectTransform>();
        }

        private static void Spacer(RectTransform parent, float h)
        { UIBuilder.Child(parent, "Sp").AddComponent<LayoutElement>().preferredHeight = h; }

        private static void MkActionBtn(RectTransform parent, string label, Color color, Action onClick)
        {
            var go = UIBuilder.Child(parent, label);
            go.AddComponent<Image>().color = UITheme.DarkenColor(color, 0.5f);
            UIBuilder.ApplyOutline(go, UITheme.DarkenColor(color, 0.3f), 1f);
            go.AddComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var t = UIBuilder.Text(go.GetComponent<RectTransform>(), "T", label, 14, FontStyle.Bold, Color.white);
            UIBuilder.Stretch(t.gameObject); t.alignment = TextAnchor.MiddleCenter;
        }

        private void SimulateAndShowResult(GameManager gm, Team playerTeam, Team oppTeam, bool isHome, CalendarEvent gameEvent)
        {
            try
            {
                ApplyPreGameChoices(playerTeam);
                var homeTeam = isHome ? playerTeam : oppTeam;
                var awayTeam = isHome ? oppTeam : playerTeam;
                var simulator = new NBAHeadCoach.Core.Simulation.GameSimulator(gm.PlayerDatabase);
                var result = simulator.SimulateGame(homeTeam, awayTeam);
                simulator.RecordGameToPlayerStats(result, gameEvent.EventId, gameEvent.Date);
                gm?.SeasonController?.RecordGameResult(gameEvent, result.HomeScore, result.AwayScore);
                gm?.LeagueStats?.AddGameResult(result.BoxScore);
                gm?.LeagueStats?.Recalculate();
                var postGame = new PostGameGamePanel(_shell);
                postGame.SetResult(result, isHome);
                _shell?.RegisterPanel("PostGame", postGame);
                _shell?.ShowPanel("PostGame");
            }
            catch (Exception ex) { Debug.LogError($"[PreGame] Simulation failed: {ex.Message}\n{ex.StackTrace}"); }
        }
    }
}
