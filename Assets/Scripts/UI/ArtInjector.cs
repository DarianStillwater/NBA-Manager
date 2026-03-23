using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Util;

namespace NBAHeadCoach.UI
{
    /// <summary>
    /// Football Manager-style UI overhaul. Rebuilds the Game scene dashboard
    /// at runtime with professional layout, large team branding, and rich cards.
    /// </summary>
    public class ArtInjector : MonoBehaviour
    {
        private string _lastTeamId;
        private string _lastScene;
        private float _injectDelay;
        private GameObject _fmRoot;
        private GameSceneController _sceneController;
        private Text _headerRecordText;
        private Text _headerDateText;
        private Transform _contentArea;

        private void Update()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            var team = GameManager.Instance.GetPlayerTeam();
            if (team == null) return;

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene != _lastScene)
            {
                _lastScene = currentScene;
                _lastTeamId = null;
                _injectDelay = 0.3f;
            }

            if (_injectDelay > 0) { _injectDelay -= Time.deltaTime; return; }

            if (_lastTeamId != team.TeamId)
            {
                BuildFMDashboard(team);
                _lastTeamId = team.TeamId;
            }

            // Live-update date and record
            RefreshLiveData(team);
        }

        // ═══════════════════════════════════════════════════════
        //  MAIN BUILD
        // ═══════════════════════════════════════════════════════

        private void BuildFMDashboard(Team team)
        {
            Debug.Log($"[ArtInjector] Building FM dashboard for {team.TeamId}");

            // Find canvas
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) { Debug.LogError("[ArtInjector] No Canvas found"); return; }

            _sceneController = FindAnyObjectByType<GameSceneController>();

            // Parse team colors
            Color teamPrimary = UITheme.ParseTeamColor(team.PrimaryColor, UITheme.AccentPrimary);
            Color teamSecondary = UITheme.ParseTeamColor(team.SecondaryColor, UITheme.AccentSecondary);

            // Hide existing UI
            foreach (Transform child in canvas.transform)
                child.gameObject.SetActive(false);

            // Create FM root
            if (_fmRoot != null) Destroy(_fmRoot);
            _fmRoot = new GameObject("FMDashboard", typeof(RectTransform));
            _fmRoot.transform.SetParent(canvas.transform, false);
            var rootRect = Stretch(_fmRoot);

            // Background
            var rootBg = _fmRoot.AddComponent<Image>();
            rootBg.color = UITheme.Background;

            // === HEADER BAR ===
            BuildHeader(rootRect, team, teamPrimary, teamSecondary);

            // === BODY (Sidebar + Content) ===
            var body = CreateChild(rootRect, "Body");
            var bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0, 0);
            bodyRect.anchorMax = new Vector2(1, 1);
            bodyRect.offsetMin = new Vector2(0, 0);
            bodyRect.offsetMax = new Vector2(0, -(UITheme.FMHeaderHeight + UITheme.FMAccentLineHeight));

            // Sidebar
            BuildSidebar(bodyRect, teamPrimary);

            // Content area
            _contentArea = CreateChild(bodyRect, "ContentArea").transform;
            var contentRect = _contentArea.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(UITheme.FMSidebarWidth, 0);
            contentRect.offsetMax = Vector2.zero;

            var contentBg = _contentArea.gameObject.AddComponent<Image>();
            contentBg.color = UITheme.Background;

            BuildDashboardContent(contentRect, team, teamPrimary);
        }

        // ═══════════════════════════════════════════════════════
        //  HEADER BAR
        // ═══════════════════════════════════════════════════════

        private void BuildHeader(RectTransform parent, Team team, Color primary, Color secondary)
        {
            var header = CreateChild(parent, "Header");
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, UITheme.FMHeaderHeight);

            // Team-colored gradient background
            var headerBg = header.AddComponent<Image>();
            headerBg.color = UITheme.DarkenColor(primary, 0.25f);

            // === Logo ===
            var logo = CreateChild(headerRect, "TeamLogo");
            var logoRect = logo.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0, 0);
            logoRect.anchorMax = new Vector2(0, 1);
            logoRect.pivot = new Vector2(0, 0.5f);
            logoRect.sizeDelta = new Vector2(UITheme.FMTeamLogoSize, 0);
            logoRect.anchoredPosition = new Vector2(16, 0);

            var logoImg = logo.AddComponent<Image>();
            logoImg.preserveAspect = true;
            var logoSprite = ArtManager.GetTeamLogo(team.TeamId);
            if (logoSprite != null) logoImg.sprite = logoSprite;
            else logoImg.color = new Color(1, 1, 1, 0.2f);

            // === Team Name ===
            var teamName = CreateText(headerRect, "TeamName",
                $"{team.City} {team.Name}".ToUpper(),
                24, FontStyle.Bold, Color.white);
            var tnRect = teamName.GetComponent<RectTransform>();
            tnRect.anchorMin = new Vector2(0, 0.35f);
            tnRect.anchorMax = new Vector2(0.6f, 0.85f);
            tnRect.offsetMin = new Vector2(16 + UITheme.FMTeamLogoSize + 16, 0);
            tnRect.offsetMax = Vector2.zero;
            teamName.alignment = TextAnchor.MiddleLeft;

            // === Coach Name ===
            var coachName = CreateText(headerRect, "CoachName",
                $"Coach {GameManager.Instance?.Career?.PersonName ?? "Player"}",
                13, FontStyle.Normal, UITheme.TextSecondary);
            var cnRect = coachName.GetComponent<RectTransform>();
            cnRect.anchorMin = new Vector2(0, 0.1f);
            cnRect.anchorMax = new Vector2(0.6f, 0.38f);
            cnRect.offsetMin = new Vector2(16 + UITheme.FMTeamLogoSize + 16, 0);
            cnRect.offsetMax = Vector2.zero;
            coachName.alignment = TextAnchor.MiddleLeft;

            // === Record ===
            _headerRecordText = CreateText(headerRect, "Record",
                $"{team.Wins}-{team.Losses}",
                22, FontStyle.Bold, Color.white);
            var recRect = _headerRecordText.GetComponent<RectTransform>();
            recRect.anchorMin = new Vector2(0.7f, 0.35f);
            recRect.anchorMax = new Vector2(1, 0.85f);
            recRect.offsetMin = Vector2.zero;
            recRect.offsetMax = new Vector2(-20, 0);
            _headerRecordText.alignment = TextAnchor.MiddleRight;

            // === Date ===
            var date = GameManager.Instance?.CurrentDate ?? DateTime.Now;
            _headerDateText = CreateText(headerRect, "Date",
                date.ToString("MMMM dd, yyyy"),
                13, FontStyle.Normal, UITheme.TextSecondary);
            var dtRect = _headerDateText.GetComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(0.7f, 0.1f);
            dtRect.anchorMax = new Vector2(1, 0.38f);
            dtRect.offsetMin = Vector2.zero;
            dtRect.offsetMax = new Vector2(-20, 0);
            _headerDateText.alignment = TextAnchor.MiddleRight;

            // === Accent Line ===
            var accent = CreateChild(parent, "AccentLine");
            var accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 1);
            accentRect.anchorMax = new Vector2(1, 1);
            accentRect.pivot = new Vector2(0.5f, 1);
            accentRect.sizeDelta = new Vector2(0, UITheme.FMAccentLineHeight);
            accentRect.anchoredPosition = new Vector2(0, -UITheme.FMHeaderHeight);
            var accentBg = accent.AddComponent<Image>();
            accentBg.color = secondary;
        }

        // ═══════════════════════════════════════════════════════
        //  SIDEBAR
        // ═══════════════════════════════════════════════════════

        private void BuildSidebar(RectTransform parent, Color teamColor)
        {
            var sidebar = CreateChild(parent, "Sidebar");
            var sideRect = sidebar.GetComponent<RectTransform>();
            sideRect.anchorMin = Vector2.zero;
            sideRect.anchorMax = new Vector2(0, 1);
            sideRect.pivot = new Vector2(0, 0.5f);
            sideRect.sizeDelta = new Vector2(UITheme.FMSidebarWidth, 0);

            var sideBg = sidebar.AddComponent<Image>();
            sideBg.color = UITheme.FMSidebarBg;

            var vlg = sidebar.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 8, 8);
            vlg.spacing = 2;

            // Nav items
            string[][] navItems = {
                new[] { "\u25A3", "Home",      "Dashboard" },
                new[] { "\u2630", "Squad",     "Roster" },
                new[] { "\u2637", "Schedule",  "Schedule" },
                new[] { "\u2261", "Standings", "Standings" },
                new[] { "\u2709", "Inbox",     "Inbox" },
                new[] { "\u2699", "Staff",     "Staff" },
            };

            foreach (var nav in navItems)
            {
                bool isActive = nav[2] == "Dashboard";
                CreateNavItem(sidebar.transform, nav[0], nav[1], nav[2], teamColor, isActive);
            }

            // Divider
            CreateDivider(sidebar.transform);

            // Action buttons
            CreateActionButton(sidebar.transform, "Advance Day", UITheme.Success, () =>
            {
                _sceneController?.SendMessage("OnAdvanceDayClicked", SendMessageOptions.DontRequireReceiver);
                GameManager.Instance?.AdvanceDay();
            });
            CreateActionButton(sidebar.transform, "Sim to Game", UITheme.AccentSecondary, () =>
            {
                _sceneController?.SendMessage("OnSimToNextGameClicked", SendMessageOptions.DontRequireReceiver);
            });
            CreateActionButton(sidebar.transform, "Save Game", UITheme.FMNavHover, () =>
            {
                _sceneController?.SendMessage("OnSaveGameClicked", SendMessageOptions.DontRequireReceiver);
            });
            CreateActionButton(sidebar.transform, "Main Menu", new Color(0.4f, 0.2f, 0.2f), () =>
            {
                GameManager.Instance?.ReturnToMainMenu();
            });
        }

        private void CreateNavItem(Transform parent, string icon, string label, string panelId, Color teamColor, bool active)
        {
            var item = new GameObject($"Nav_{label}", typeof(RectTransform));
            item.transform.SetParent(parent, false);

            var le = item.AddComponent<LayoutElement>();
            le.preferredHeight = UITheme.FMNavItemHeight;

            var bg = item.AddComponent<Image>();
            bg.color = active ? UITheme.FMNavActive : Color.clear;

            var btn = item.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = UITheme.FMNavHover;
            colors.pressedColor = UITheme.FMNavActive;
            btn.colors = colors;

            btn.onClick.AddListener(() =>
            {
                _sceneController?.SendMessage("ShowPanel", panelId, SendMessageOptions.DontRequireReceiver);
            });

            // Active indicator (team-colored left border)
            if (active)
            {
                var indicator = CreateChild(item.GetComponent<RectTransform>(), "ActiveBar");
                var indRect = indicator.GetComponent<RectTransform>();
                indRect.anchorMin = Vector2.zero;
                indRect.anchorMax = new Vector2(0, 1);
                indRect.pivot = new Vector2(0, 0.5f);
                indRect.sizeDelta = new Vector2(4, 0);
                var indBg = indicator.AddComponent<Image>();
                indBg.color = teamColor;
            }

            // Icon
            var iconText = CreateText(item.GetComponent<RectTransform>(), "Icon", icon,
                (int)UITheme.FMNavIconSize, FontStyle.Normal, active ? teamColor : UITheme.TextSecondary);
            var iconRect = iconText.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0);
            iconRect.anchorMax = new Vector2(0, 1);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.sizeDelta = new Vector2(40, 0);
            iconRect.anchoredPosition = new Vector2(14, 0);
            iconText.alignment = TextAnchor.MiddleCenter;

            // Label
            var labelText = CreateText(item.GetComponent<RectTransform>(), "Label", label,
                14, active ? FontStyle.Bold : FontStyle.Normal, active ? Color.white : UITheme.TextSecondary);
            var lblRect = labelText.GetComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0, 0);
            lblRect.anchorMax = new Vector2(1, 1);
            lblRect.offsetMin = new Vector2(50, 0);
            lblRect.offsetMax = new Vector2(-8, 0);
            labelText.alignment = TextAnchor.MiddleLeft;
        }

        private void CreateActionButton(Transform parent, string label, Color bgColor, Action onClick)
        {
            var item = new GameObject($"Btn_{label}", typeof(RectTransform));
            item.transform.SetParent(parent, false);

            var le = item.AddComponent<LayoutElement>();
            le.preferredHeight = 38;

            var bg = item.AddComponent<Image>();
            bg.color = UITheme.DarkenColor(bgColor, 0.4f);

            var btn = item.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = UITheme.DarkenColor(bgColor, 0.6f);
            colors.pressedColor = bgColor;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var text = CreateText(item.GetComponent<RectTransform>(), "Label", label,
                13, FontStyle.Bold, Color.white);
            var textRect = text.GetComponent<RectTransform>();
            Stretch(text.gameObject);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private void CreateDivider(Transform parent)
        {
            var div = new GameObject("Divider", typeof(RectTransform));
            div.transform.SetParent(parent, false);
            var le = div.AddComponent<LayoutElement>();
            le.preferredHeight = 12;
            var line = CreateChild(div.GetComponent<RectTransform>(), "Line");
            var lineRect = line.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.1f, 0.4f);
            lineRect.anchorMax = new Vector2(0.9f, 0.6f);
            lineRect.sizeDelta = Vector2.zero;
            var lineBg = line.AddComponent<Image>();
            lineBg.color = UITheme.FMDivider;
        }

        // ═══════════════════════════════════════════════════════
        //  CONTENT AREA — DASHBOARD CARDS
        // ═══════════════════════════════════════════════════════

        private void BuildDashboardContent(RectTransform parent, Team team, Color teamColor)
        {
            // Scrollable content
            var scroll = parent.gameObject.AddComponent<ScrollRect>();
            var viewport = CreateChild(parent, "Viewport");
            Stretch(viewport);
            viewport.AddComponent<RectMask2D>();

            var content = CreateChild(viewport.GetComponent<RectTransform>(), "Content");
            var contentRect = Stretch(content);
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;

            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;

            // Title
            var title = CreateText(contentRect, "Title", "DASHBOARD",
                20, FontStyle.Bold, UITheme.AccentPrimary);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 30;

            // Row 1: Next Game + Season Overview
            var row1 = CreateRow(contentRect, "Row1", 180);
            BuildNextGameCard(row1, team, teamColor);
            BuildSeasonOverviewCard(row1, team, teamColor);

            // Row 2: Recent Results + Roster Summary
            var row2 = CreateRow(contentRect, "Row2", 160);
            BuildRecentResultsCard(row2, teamColor);
            BuildRosterSummaryCard(row2, team, teamColor);
        }

        private RectTransform CreateRow(RectTransform parent, string name, float height)
        {
            var row = CreateChild(parent, name);
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            return row.GetComponent<RectTransform>();
        }

        private void BuildNextGameCard(RectTransform parent, Team team, Color teamColor)
        {
            var card = CreateCard(parent, "NEXT GAME", teamColor);

            var nextGame = GameManager.Instance?.SeasonController?.GetNextGame();
            if (nextGame != null)
            {
                string opponentId = nextGame.HomeTeamId == team.TeamId ? nextGame.AwayTeamId : nextGame.HomeTeamId;
                bool isHome = nextGame.HomeTeamId == team.TeamId;
                var opponent = GameManager.Instance?.GetTeam(opponentId);

                // Opponent logo
                var logoGo = CreateChild(card, "OpponentLogo");
                var logoRect = logoGo.GetComponent<RectTransform>();
                logoRect.anchorMin = new Vector2(0, 0.3f);
                logoRect.anchorMax = new Vector2(0, 0.9f);
                logoRect.pivot = new Vector2(0, 0.5f);
                logoRect.sizeDelta = new Vector2(48, 0);
                logoRect.anchoredPosition = new Vector2(12, 0);
                var logoImg = logoGo.AddComponent<Image>();
                logoImg.preserveAspect = true;
                var oppLogo = ArtManager.GetTeamLogo(opponentId);
                if (oppLogo != null) logoImg.sprite = oppLogo;

                // Opponent info
                string oppName = opponent != null ? $"{opponent.City} {opponent.Name}" : opponentId;
                string oppRecord = opponent != null ? $"({opponent.Wins}-{opponent.Losses})" : "";
                var info = CreateText(card, "Info",
                    $"vs {oppName} {oppRecord}\n{(isHome ? "HOME" : "AWAY")}  •  {nextGame.Date:MMM dd}",
                    14, FontStyle.Normal, UITheme.TextSecondary);
                var infoRect = info.GetComponent<RectTransform>();
                infoRect.anchorMin = new Vector2(0, 0.2f);
                infoRect.anchorMax = new Vector2(1, 0.9f);
                infoRect.offsetMin = new Vector2(68, 0);
                infoRect.offsetMax = new Vector2(-8, 0);
                info.alignment = TextAnchor.MiddleLeft;
            }
            else
            {
                var noGame = CreateText(card, "NoGame", "No games scheduled",
                    14, FontStyle.Normal, UITheme.TextSecondary);
                CenterText(noGame);
            }
        }

        private void BuildSeasonOverviewCard(RectTransform parent, Team team, Color teamColor)
        {
            var card = CreateCard(parent, "SEASON OVERVIEW", teamColor);

            var sc = GameManager.Instance?.SeasonController;
            int rank = sc?.GetConferenceRank(team.TeamId) ?? 0;
            string conf = team.Conference ?? "—";

            var info = CreateText(card, "Info",
                $"Conference:  {conf}\n" +
                $"Rank:  #{rank}\n" +
                $"Record:  {team.Wins}-{team.Losses}\n" +
                $"Win%:  {(team.Wins + team.Losses > 0 ? ((float)team.Wins / (team.Wins + team.Losses)).ToString(".000") : "—")}",
                14, FontStyle.Normal, UITheme.TextSecondary);
            var infoRect = info.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0, 0.05f);
            infoRect.anchorMax = new Vector2(1, 0.9f);
            infoRect.offsetMin = new Vector2(12, 0);
            infoRect.offsetMax = new Vector2(-8, 0);
            info.alignment = TextAnchor.UpperLeft;
            info.lineSpacing = 1.4f;
        }

        private void BuildRecentResultsCard(RectTransform parent, Color teamColor)
        {
            var card = CreateCard(parent, "RECENT RESULTS", teamColor);
            var text = CreateText(card, "Content", "No games played yet",
                14, FontStyle.Normal, UITheme.TextSecondary);
            CenterText(text);
        }

        private void BuildRosterSummaryCard(RectTransform parent, Team team, Color teamColor)
        {
            var card = CreateCard(parent, "ROSTER", teamColor);

            int rosterCount = team.RosterPlayerIds?.Count ?? 0;
            int injured = team.Roster?.Count(p => p.IsInjured) ?? 0;
            var topPlayer = team.Roster?.OrderByDescending(p => p.OverallRating).FirstOrDefault();

            string content = $"Players:  {rosterCount}\n" +
                             $"Injured:  {injured}\n" +
                             (topPlayer != null ? $"Star:  {topPlayer.FirstName} {topPlayer.LastName}" : "");

            var info = CreateText(card, "Info", content,
                14, FontStyle.Normal, UITheme.TextSecondary);
            var infoRect = info.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0, 0.05f);
            infoRect.anchorMax = new Vector2(1, 0.9f);
            infoRect.offsetMin = new Vector2(12, 0);
            infoRect.offsetMax = new Vector2(-8, 0);
            info.alignment = TextAnchor.UpperLeft;
            info.lineSpacing = 1.4f;
        }

        // ═══════════════════════════════════════════════════════
        //  CARD BUILDER
        // ═══════════════════════════════════════════════════════

        private RectTransform CreateCard(RectTransform parent, string title, Color accentColor)
        {
            var card = CreateChild(parent, $"Card_{title}");
            var cardBg = card.AddComponent<Image>();
            cardBg.color = UITheme.CardBackground;

            // Card header
            var header = CreateChild(card.GetComponent<RectTransform>(), "Header");
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, UITheme.FMCardHeaderHeight);

            var headerBg = header.AddComponent<Image>();
            headerBg.color = UITheme.FMCardHeaderBg;

            // Accent left border on header
            var accent = CreateChild(headerRect, "Accent");
            var accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = Vector2.zero;
            accentRect.anchorMax = new Vector2(0, 1);
            accentRect.pivot = new Vector2(0, 0.5f);
            accentRect.sizeDelta = new Vector2(3, 0);
            var accentBg = accent.AddComponent<Image>();
            accentBg.color = accentColor;

            // Title text
            var titleText = CreateText(headerRect, "Title", title,
                12, FontStyle.Bold, UITheme.AccentPrimary);
            var titleRect = titleText.GetComponent<RectTransform>();
            Stretch(titleText.gameObject);
            titleRect.offsetMin = new Vector2(12, 0);
            titleText.alignment = TextAnchor.MiddleLeft;

            return card.GetComponent<RectTransform>();
        }

        // ═══════════════════════════════════════════════════════
        //  LIVE DATA REFRESH
        // ═══════════════════════════════════════════════════════

        private void RefreshLiveData(Team team)
        {
            if (_headerRecordText != null)
                _headerRecordText.text = $"{team.Wins}-{team.Losses}";
            if (_headerDateText != null)
            {
                var date = GameManager.Instance?.CurrentDate ?? DateTime.Now;
                _headerDateText.text = date.ToString("MMMM dd, yyyy");
            }
        }

        // ═══════════════════════════════════════════════════════
        //  UTILITY HELPERS
        // ═══════════════════════════════════════════════════════

        private static GameObject CreateChild(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static Text CreateText(RectTransform parent, string name, string content,
            int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void CenterText(Text text)
        {
            var rt = Stretch(text.gameObject);
            rt.offsetMin = new Vector2(8, 0);
            rt.offsetMax = new Vector2(-8, -UITheme.FMCardHeaderHeight);
            text.alignment = TextAnchor.MiddleCenter;
        }
    }
}
