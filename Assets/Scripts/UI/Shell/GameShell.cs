using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.Util;
using NBAHeadCoach.UI.GamePanels;

namespace NBAHeadCoach.UI.Shell
{
    /// <summary>
    /// FM-style game scene shell. Manages header, sidebar, content area.
    /// Panels are registered via IGamePanel and swapped on nav clicks.
    /// Replaces ArtInjector's scaffold responsibilities.
    /// </summary>
    public class GameShell : MonoBehaviour
    {
        private string _lastTeamId;
        private string _lastScene;
        private float _injectDelay;
        private GameObject _fmRoot;
        private GameSceneController _sceneController;
        private Text _headerRecordText;
        private Text _headerDateText;
        private RectTransform _contentArea;
        private GameObject[] _originalUI;
        private string _currentPanel = "Dashboard";
        private Team _currentTeam;
        private Color _teamColor;

        // Panel registry
        private Dictionary<string, IGamePanel> _panels = new Dictionary<string, IGamePanel>();

        // Action button tracking for enable/disable during game day flow
        private List<Button> _actionButtons = new List<Button>();
        private bool _isGameDayFlow = false;
        private bool _continueRunning = false;

        // Nav item tracking for active state updates
        private List<(GameObject go, string panelId, Image bg, Transform indicator, Text iconText, Text labelText)> _navItemRefs
            = new List<(GameObject, string, Image, Transform, Text, Text)>();

        // Player detail state
        private Player _detailPlayer;
        private string _playerTab = "Overview";

        /// <summary>Register a panel by ID.</summary>
        public void RegisterPanel(string id, IGamePanel panel) => _panels[id] = panel;

        /// <summary>Access to content area for panels that need to swap content (e.g., player detail).</summary>
        public RectTransform ContentArea => _contentArea;
        public Team CurrentTeam => _currentTeam;
        public Color TeamColor => _teamColor;

        // ═══════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════════════════════

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
                BuildLayout(team);
                _lastTeamId = team.TeamId;
            }

            RefreshLiveData(team);
        }

        // ═══════════════════════════════════════════════════════
        //  LAYOUT BUILD
        // ═══════════════════════════════════════════════════════

        private void BuildLayout(Team team)
        {
            Debug.Log($"[GameShell] Building FM layout for {team.TeamId}");

            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            _sceneController = FindAnyObjectByType<GameSceneController>();

            Color teamPrimary = UITheme.ParseTeamColor(team.PrimaryColor, UITheme.AccentPrimary);
            Color teamSecondary = UITheme.ParseTeamColor(team.SecondaryColor, UITheme.AccentSecondary);

            // Hide existing UI
            _originalUI = new GameObject[canvas.transform.childCount];
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                _originalUI[i] = canvas.transform.GetChild(i).gameObject;
                _originalUI[i].SetActive(false);
            }

            _navItemRefs.Clear();
            _actionButtons.Clear();
            _isGameDayFlow = false;
            if (_fmRoot != null) Destroy(_fmRoot);
            _fmRoot = new GameObject("FMDashboard", typeof(RectTransform));
            _fmRoot.transform.SetParent(canvas.transform, false);
            var rootRect = UIBuilder.Stretch(_fmRoot);
            _fmRoot.AddComponent<Image>().color = UITheme.Background;

            BuildHeader(rootRect, team, teamPrimary, teamSecondary);

            var body = UIBuilder.Child(rootRect, "Body");
            var bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = new Vector2(0, -(UITheme.FMHeaderHeight + UITheme.FMAccentLineHeight));

            BuildSidebar(bodyRect, teamPrimary);

            var contentGo = UIBuilder.Child(bodyRect, "ContentArea");
            _contentArea = contentGo.GetComponent<RectTransform>();
            _contentArea.anchorMin = Vector2.zero;
            _contentArea.anchorMax = Vector2.one;
            _contentArea.offsetMin = new Vector2(UITheme.FMSidebarWidth, 0);
            _contentArea.offsetMax = Vector2.zero;
            contentGo.AddComponent<Image>().color = UITheme.Background;

            _currentTeam = team;
            _teamColor = teamPrimary;
            _currentPanel = "Dashboard";
            ShowPanel("Dashboard");
        }

        // ═══════════════════════════════════════════════════════
        //  HEADER
        // ═══════════════════════════════════════════════════════

        private void BuildHeader(RectTransform parent, Team team, Color primary, Color secondary)
        {
            var header = UIBuilder.Child(parent, "Header");
            var hr = header.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0, 1); hr.anchorMax = Vector2.one;
            hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(0, UITheme.FMHeaderHeight);
            var headerImg = header.AddComponent<Image>();
            headerImg.color = Color.white;
            UIBuilder.ApplyGradient(header, UITheme.TeamHeaderGradientTop(primary), UITheme.TeamHeaderGradientBottom(primary));

            // Logo
            var logo = UIBuilder.Child(hr, "TeamLogo");
            var lr = logo.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(0, 1);
            lr.pivot = new Vector2(0, 0.5f);
            lr.sizeDelta = new Vector2(UITheme.FMTeamLogoSize, 0);
            lr.anchoredPosition = new Vector2(16, 0);
            var logoImg = logo.AddComponent<Image>(); logoImg.preserveAspect = true;
            var logoSprite = ArtManager.GetTeamLogo(team.TeamId);
            if (logoSprite != null) logoImg.sprite = logoSprite;
            else logoImg.color = new Color(1, 1, 1, 0.2f);
            UIBuilder.ApplyShadow(logo, 1, -1, 0.5f);

            float textLeft = 16 + UITheme.FMTeamLogoSize + 16;

            // Team name
            var tn = UIBuilder.Text(hr, "TeamName", $"{team.City} {team.Name}".ToUpper(), 24, FontStyle.Bold, Color.white);
            var tnr = tn.GetComponent<RectTransform>();
            tnr.anchorMin = new Vector2(0, 0.35f); tnr.anchorMax = new Vector2(0.6f, 0.85f);
            tnr.offsetMin = new Vector2(textLeft, 0); tn.alignment = TextAnchor.MiddleLeft;

            // Coach name
            var cn = UIBuilder.Text(hr, "CoachName", $"Coach {GameManager.Instance?.Career?.PersonName ?? "Player"}", 13, FontStyle.Normal, UITheme.TextSecondary);
            var cnr = cn.GetComponent<RectTransform>();
            cnr.anchorMin = new Vector2(0, 0.1f); cnr.anchorMax = new Vector2(0.6f, 0.38f);
            cnr.offsetMin = new Vector2(textLeft, 0); cn.alignment = TextAnchor.MiddleLeft;

            // Record + Date
            var date = GameManager.Instance?.CurrentDate ?? DateTime.Now;
            _headerRecordText = UIBuilder.Text(hr, "Record", $"{team.Wins}-{team.Losses}", 22, FontStyle.Bold, Color.white);
            var rr = _headerRecordText.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.7f, 0.4f); rr.anchorMax = new Vector2(1, 0.95f);
            rr.sizeDelta = Vector2.zero;
            rr.offsetMin = Vector2.zero; rr.offsetMax = new Vector2(-20, 0);
            _headerRecordText.alignment = TextAnchor.MiddleRight;

            _headerDateText = UIBuilder.Text(hr, "Date", date.ToString("MMMM dd, yyyy"), 12, FontStyle.Normal, UITheme.TextSecondary);
            var dr = _headerDateText.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(0.7f, 0.05f); dr.anchorMax = new Vector2(1, 0.4f);
            dr.sizeDelta = Vector2.zero;
            dr.offsetMin = Vector2.zero; dr.offsetMax = new Vector2(-20, 0);
            _headerDateText.alignment = TextAnchor.MiddleRight;

            // Accent line
            var accent = UIBuilder.Child(parent, "AccentLine");
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0, 1); ar.anchorMax = Vector2.one;
            ar.pivot = new Vector2(0.5f, 1);
            ar.sizeDelta = new Vector2(0, UITheme.FMAccentLineHeight);
            ar.anchoredPosition = new Vector2(0, -UITheme.FMHeaderHeight);
            accent.AddComponent<Image>().color = UITheme.AccentPrimary;
        }

        // ═══════════════════════════════════════════════════════
        //  SIDEBAR
        // ═══════════════════════════════════════════════════════

        private void BuildSidebar(RectTransform parent, Color teamColor)
        {
            var sidebar = UIBuilder.Child(parent, "Sidebar");
            var sr = sidebar.GetComponent<RectTransform>();
            sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(0, 1);
            sr.pivot = new Vector2(0, 0.5f); sr.sizeDelta = new Vector2(UITheme.FMSidebarWidth, 0);
            var sidebarImg = sidebar.AddComponent<Image>();
            sidebarImg.color = Color.white;
            UIBuilder.ApplyGradient(sidebar, UITheme.TeamSidebarGradientTop(teamColor), UITheme.TeamSidebarGradientBottom(teamColor));

            var vlg = sidebar.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(0, 0, 4, 4); vlg.spacing = 1;

            string[][] navItems = {
                new[] { "\u25A3", "Home",      "Dashboard" },
                new[] { "\u2630", "Squad",     "Roster" },
                new[] { "\u2637", "Schedule",  "Schedule" },
                new[] { "\u2261", "Standings", "Standings" },
                new[] { "\u2709", "Inbox",     "Inbox" },
                new[] { "\u2699", "Staff",     "Staff" },
            };

            var navGOs = new Dictionary<string, GameObject>();
            foreach (var nav in navItems)
            {
                bool isActive = nav[2] == "Dashboard";
                var navGo = CreateNavItem(sidebar.transform, nav[0], nav[1], nav[2], teamColor, isActive);
                navGOs[nav[2]] = navGo;
            }

            // Activity indicators
            if (navGOs.ContainsKey("Inbox"))
                AddActivityDot(navGOs["Inbox"], UITheme.AccentPrimary);
            if (navGOs.ContainsKey("Schedule") && GameManager.Instance?.SeasonController?.GetTodaysGame() != null)
                AddActivityDot(navGOs["Schedule"], UITheme.Success);

            CreateDivider(sidebar.transform);

            CreateActionButton(sidebar.transform, "\u25B6  Continue", UITheme.Success, () =>
            {
                if (_continueRunning) return;
                StartCoroutine(ContinueCoroutine());
            });
            CreateActionButton(sidebar.transform, "\u2699  Menu", UITheme.FMNavHover, () => ShowPanel("Settings"));
        }

        private GameObject CreateNavItem(Transform parent, string icon, string label, string panelId, Color teamColor, bool active)
        {
            var item = new GameObject($"Nav_{label}", typeof(RectTransform));
            item.transform.SetParent(parent, false);
            item.AddComponent<LayoutElement>().preferredHeight = UITheme.FMNavItemHeight;
            var bg = item.AddComponent<Image>();
            bg.color = active ? UITheme.DarkenColor(teamColor, 0.5f) : Color.clear;

            var btn = item.AddComponent<Button>();
            var c = btn.colors;
            c.highlightedColor = new Color(1f, 1f, 1f, 0.1f);
            c.pressedColor = UITheme.DarkenColor(teamColor, 0.4f);
            btn.colors = c;
            btn.onClick.AddListener(() => OnNavClicked(panelId));

            // Active indicator bar (always created, toggled via color)
            var ind = UIBuilder.Child(item.GetComponent<RectTransform>(), "ActiveBar");
            var ir = ind.GetComponent<RectTransform>();
            ir.anchorMin = Vector2.zero; ir.anchorMax = new Vector2(0, 1);
            ir.pivot = new Vector2(0, 0.5f); ir.sizeDelta = new Vector2(5, 0);
            ind.AddComponent<Image>().color = active ? teamColor : Color.clear;

            var iconText = UIBuilder.Text(item.GetComponent<RectTransform>(), "Icon", icon,
                (int)UITheme.FMNavIconSize, FontStyle.Normal, active ? teamColor : UITheme.TextSecondary);
            var iconRect = iconText.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0); iconRect.anchorMax = new Vector2(0, 1);
            iconRect.pivot = new Vector2(0, 0.5f); iconRect.sizeDelta = new Vector2(40, 0);
            iconRect.anchoredPosition = new Vector2(14, 0);
            iconText.alignment = TextAnchor.MiddleCenter;

            var labelText = UIBuilder.Text(item.GetComponent<RectTransform>(), "Label", label,
                14, active ? FontStyle.Bold : FontStyle.Normal, active ? Color.white : UITheme.TextSecondary);
            var lr = labelText.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(50, 0); lr.offsetMax = new Vector2(-8, 0);
            labelText.alignment = TextAnchor.MiddleLeft;

            // Track for active state updates
            _navItemRefs.Add((item, panelId, bg, ind.transform, iconText, labelText));

            return item;
        }

        private void UpdateNavActiveStates(string activePanelId)
        {
            foreach (var nav in _navItemRefs)
            {
                bool isActive = nav.panelId == activePanelId;
                nav.bg.color = isActive ? UITheme.DarkenColor(_teamColor, 0.5f) : Color.clear;
                nav.indicator.GetComponent<Image>().color = isActive ? _teamColor : Color.clear;
                nav.iconText.color = isActive ? _teamColor : UITheme.TextSecondary;
                nav.labelText.color = isActive ? Color.white : UITheme.TextSecondary;
                nav.labelText.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void AddActivityDot(GameObject navItem, Color color)
        {
            var dot = UIBuilder.Child(navItem.GetComponent<RectTransform>(), "Dot");
            var dr = dot.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(1, 0.5f); dr.anchorMax = new Vector2(1, 0.5f);
            dr.pivot = new Vector2(1, 0.5f);
            dr.sizeDelta = new Vector2(8, 8);
            dr.anchoredPosition = new Vector2(-8, 0);
            dot.AddComponent<Image>().color = color;
        }

        private void CreateActionButton(Transform parent, string label, Color bgColor, Action onClick)
        {
            var item = new GameObject($"Btn_{label}", typeof(RectTransform));
            item.transform.SetParent(parent, false);
            item.AddComponent<LayoutElement>().preferredHeight = 38;
            var img = item.AddComponent<Image>();
            img.color = UITheme.DarkenColor(bgColor, 0.5f);  // direct visible color
            UIBuilder.ApplyOutline(item, UITheme.DarkenColor(bgColor, 0.3f), 1f);

            var btn = item.AddComponent<Button>();
            var c = btn.colors;
            c.highlightedColor = UITheme.LightenColor(bgColor, 0.3f);
            c.pressedColor = UITheme.DarkenColor(bgColor, 0.7f);
            btn.colors = c;
            btn.onClick.AddListener(() => onClick?.Invoke());
            _actionButtons.Add(btn);

            var text = UIBuilder.Text(item.GetComponent<RectTransform>(), "Label", label, 13, FontStyle.Bold, Color.white);
            UIBuilder.Stretch(text.gameObject);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private void CreateDivider(Transform parent)
        {
            var div = new GameObject("Divider", typeof(RectTransform));
            div.transform.SetParent(parent, false);
            div.AddComponent<LayoutElement>().preferredHeight = 12;
            var line = UIBuilder.Child(div.GetComponent<RectTransform>(), "Line");
            var lr = line.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.1f, 0.4f); lr.anchorMax = new Vector2(0.9f, 0.6f);
            lr.sizeDelta = Vector2.zero;
            line.AddComponent<Image>().color = UITheme.FMDivider;
        }

        // ═══════════════════════════════════════════════════════
        //  PANEL NAVIGATION
        // ═══════════════════════════════════════════════════════

        private void OnNavClicked(string panelId)
        {
            if (_currentPanel == panelId) return;
            _currentPanel = panelId;
            UpdateNavActiveStates(panelId);
            ShowPanel(panelId);
        }

        /// <summary>Show a registered panel by ID.</summary>
        public void ShowPanel(string panelId)
        {
            if (_contentArea == null) return;

            // Re-enable sidebar buttons when returning to a regular panel
            if (panelId != "PreGame" && panelId != "PostGame" && panelId != "PlayerDetail")
                EnableActionButtons();

            // Destroy and recreate content area to avoid stale components
            var parent = _contentArea.parent;
            Destroy(_contentArea.gameObject);

            var newContent = UIBuilder.Child(parent.GetComponent<RectTransform>(), "ContentArea");
            _contentArea = newContent.GetComponent<RectTransform>();
            _contentArea.anchorMin = Vector2.zero;
            _contentArea.anchorMax = Vector2.one;
            _contentArea.offsetMin = new Vector2(UITheme.FMSidebarWidth, 0);
            _contentArea.offsetMax = Vector2.zero;
            newContent.AddComponent<Image>().color = UITheme.TeamTintedBackground(_teamColor, 0.08f);

            // Subtle pattern overlay for visual texture
            var patternGo = UIBuilder.Child(newContent.GetComponent<RectTransform>(), "Pattern");
            UIBuilder.Stretch(patternGo);
            var patternImg = patternGo.AddComponent<Image>();
            var patternSprite = Resources.Load<Sprite>("Art/Backgrounds/panel_bg_dark");
            if (patternSprite != null)
            {
                patternImg.sprite = patternSprite;
                patternImg.type = Image.Type.Tiled;
                patternImg.color = new Color(1, 1, 1, 0.03f);
            }
            else
            {
                patternImg.color = Color.clear;
            }
            patternImg.raycastTarget = false;

            var team = _currentTeam ?? GameManager.Instance?.GetPlayerTeam();
            if (team == null) return;

            if (_panels.TryGetValue(panelId, out var panel))
            {
                panel.Build(_contentArea, team, _teamColor);
            }
            else
            {
                var placeholder = UIBuilder.Text(_contentArea, "Placeholder", $"{panelId}\n\nComing soon...",
                    18, FontStyle.Bold, UITheme.TextSecondary);
                UIBuilder.Stretch(placeholder.gameObject);
                placeholder.alignment = TextAnchor.MiddleCenter;
            }

            // Panel transition: fade in + scale up
            newContent.AddComponent<Components.PanelTransition>();
        }

        public void RefreshCurrentPanel()
        {
            var saved = _currentPanel;
            _currentPanel = "";
            OnNavClicked(saved);
        }

        // ═══════════════════════════════════════════════════════
        //  PRE-GAME / POST-GAME HOOKS
        // ═══════════════════════════════════════════════════════

        private void DisableActionButtons()
        {
            _isGameDayFlow = true;
            foreach (var btn in _actionButtons)
                if (btn != null) btn.interactable = false;
        }

        private void EnableActionButtons()
        {
            _isGameDayFlow = false;
            foreach (var btn in _actionButtons)
                if (btn != null) btn.interactable = true;
        }

        public void ShowPreGame(CalendarEvent gameEvent)
        {
            _currentPanel = "PreGame";
            DisableActionButtons();
            // Delegate to ArtInjector which registers the PreGame panel dynamically
            var artInjector = GetComponent<ArtInjector>();
            artInjector?.ShowPreGame(gameEvent);
        }

        // ═══════════════════════════════════════════════════════
        //  CONTINUE — advance day-by-day until stop trigger
        // ═══════════════════════════════════════════════════════

        private IEnumerator ContinueCoroutine()
        {
            _continueRunning = true;
            DisableActionButtons();

            var gm = GameManager.Instance;
            var sc = gm?.SeasonController;
            if (gm == null || sc == null) { _continueRunning = false; EnableActionButtons(); yield break; }

            // Track stop conditions via events
            bool phaseChanged = false;
            bool playerInjured = false;
            SeasonPhase newPhase = sc.CurrentPhase;

            Action<SeasonPhase> onPhase = p => { phaseChanged = true; newPhase = p; };
            sc.OnPhaseChanged += onPhase;

            // Subscribe to injury events if InjuryManager exists
            var injMgr = InjuryManager.Instance;
            Action<Player, InjuryEvent> onInjury = (p, e) => { playerInjured = true; };
            if (injMgr != null) injMgr.OnPlayerInjured += onInjury;

            int safety = 0;
            while (safety++ < 400)
            {
                // Check BEFORE advancing: is the next game tomorrow?
                // If so, advance to that day and show PreGame.
                var nextGame = sc.GetNextGame();
                if (nextGame != null)
                {
                    DateTime tomorrow = gm.CurrentDate.AddDays(1);
                    if (nextGame.Date.Date == tomorrow.Date)
                    {
                        gm.AdvanceDay();
                        RefreshLiveData(_currentTeam);
                        Debug.Log($"[Continue] Stopped — game day: {nextGame.HomeTeamId} vs {nextGame.AwayTeamId} on {gm.CurrentDate:MMM dd}");
                        // Re-fetch since AdvanceDay may have changed state
                        var todaysGame = sc.GetTodaysGame();
                        if (todaysGame != null)
                            ShowPreGame(todaysGame);
                        else
                            ShowPanel("Dashboard");
                        break;
                    }
                    // Also check: is there a game TODAY that hasn't been played?
                    if (nextGame.Date.Date == gm.CurrentDate.Date)
                    {
                        Debug.Log($"[Continue] Stopped — game today: {nextGame.HomeTeamId} vs {nextGame.AwayTeamId}");
                        ShowPreGame(nextGame);
                        break;
                    }
                }

                gm.AdvanceDay();
                RefreshLiveData(_currentTeam);
                yield return new WaitForSeconds(0.08f);

                // Stop: season phase changed (milestone)
                if (phaseChanged)
                {
                    Debug.Log($"[Continue] Stopped — phase changed to {newPhase}");
                    ShowPanel("Dashboard");
                    break;
                }

                // Stop: player injured
                if (playerInjured)
                {
                    Debug.Log("[Continue] Stopped — player injury");
                    ShowPanel("Dashboard");
                    break;
                }
            }

            // Cleanup event subscriptions
            sc.OnPhaseChanged -= onPhase;
            if (injMgr != null) injMgr.OnPlayerInjured -= onInjury;

            _continueRunning = false;
            // Don't re-enable if we entered game day flow (PreGame disables them)
            if (!_isGameDayFlow)
                EnableActionButtons();
        }

        // ═══════════════════════════════════════════════════════
        //  LIVE DATA
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
    }

    /// <summary>Optional interface for pre-game panels that need game event data.</summary>
    public interface IPreGamePanel
    {
        void SetGameEvent(CalendarEvent gameEvent);
    }
}
