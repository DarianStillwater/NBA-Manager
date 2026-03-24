using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
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
            header.AddComponent<Image>().color = UITheme.DarkenColor(primary, 0.25f);

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

            // Record
            _headerRecordText = UIBuilder.Text(hr, "Record", $"{team.Wins}-{team.Losses}", 22, FontStyle.Bold, Color.white);
            var rr = _headerRecordText.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.7f, 0.35f); rr.anchorMax = new Vector2(1, 0.85f);
            rr.offsetMax = new Vector2(-20, 0); _headerRecordText.alignment = TextAnchor.MiddleRight;

            // Date
            var date = GameManager.Instance?.CurrentDate ?? DateTime.Now;
            _headerDateText = UIBuilder.Text(hr, "Date", date.ToString("MMMM dd, yyyy"), 13, FontStyle.Normal, UITheme.TextSecondary);
            var dr = _headerDateText.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(0.7f, 0.1f); dr.anchorMax = new Vector2(1, 0.38f);
            dr.offsetMax = new Vector2(-20, 0); _headerDateText.alignment = TextAnchor.MiddleRight;

            // Accent line
            var accent = UIBuilder.Child(parent, "AccentLine");
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0, 1); ar.anchorMax = Vector2.one;
            ar.pivot = new Vector2(0.5f, 1);
            ar.sizeDelta = new Vector2(0, UITheme.FMAccentLineHeight);
            ar.anchoredPosition = new Vector2(0, -UITheme.FMHeaderHeight);
            accent.AddComponent<Image>().color = secondary;
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
            sidebar.AddComponent<Image>().color = UITheme.FMSidebarBg;

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

            foreach (var nav in navItems)
            {
                bool isActive = nav[2] == "Dashboard";
                CreateNavItem(sidebar.transform, nav[0], nav[1], nav[2], teamColor, isActive);
            }

            CreateDivider(sidebar.transform);

            CreateActionButton(sidebar.transform, "Advance Day", UITheme.Success, () =>
            {
                GameManager.Instance?.AdvanceDay();
                var todaysGame = GameManager.Instance?.SeasonController?.GetTodaysGame();
                if (todaysGame != null)
                    ShowPreGame(todaysGame);
                else
                    RefreshCurrentPanel();
            });
            CreateActionButton(sidebar.transform, "Sim to Game", UITheme.AccentSecondary, () =>
            {
                var sc = GameManager.Instance?.SeasonController;
                var nextGame = sc?.GetNextGame();
                if (nextGame != null)
                {
                    while (GameManager.Instance.CurrentDate.Date < nextGame.Date.Date)
                        GameManager.Instance.AdvanceDay();
                    ShowPreGame(nextGame);
                }
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
            item.AddComponent<LayoutElement>().preferredHeight = UITheme.FMNavItemHeight;
            var bg = item.AddComponent<Image>();
            bg.color = active ? UITheme.FMNavActive : Color.clear;
            var btn = item.AddComponent<Button>();
            var c = btn.colors; c.highlightedColor = UITheme.FMNavHover; c.pressedColor = UITheme.FMNavActive; btn.colors = c;
            btn.onClick.AddListener(() => OnNavClicked(panelId));

            if (active)
            {
                var ind = UIBuilder.Child(item.GetComponent<RectTransform>(), "ActiveBar");
                var ir = ind.GetComponent<RectTransform>();
                ir.anchorMin = Vector2.zero; ir.anchorMax = new Vector2(0, 1);
                ir.pivot = new Vector2(0, 0.5f); ir.sizeDelta = new Vector2(4, 0);
                ind.AddComponent<Image>().color = teamColor;
            }

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
        }

        private void CreateActionButton(Transform parent, string label, Color bgColor, Action onClick)
        {
            var item = new GameObject($"Btn_{label}", typeof(RectTransform));
            item.transform.SetParent(parent, false);
            item.AddComponent<LayoutElement>().preferredHeight = 38;
            item.AddComponent<Image>().color = UITheme.DarkenColor(bgColor, 0.4f);
            var btn = item.AddComponent<Button>();
            var c = btn.colors; c.highlightedColor = UITheme.DarkenColor(bgColor, 0.6f); c.pressedColor = bgColor; btn.colors = c;
            btn.onClick.AddListener(() => onClick?.Invoke());
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
            ShowPanel(panelId);
        }

        /// <summary>Show a registered panel by ID.</summary>
        public void ShowPanel(string panelId)
        {
            if (_contentArea == null) return;

            // Destroy and recreate content area to avoid stale components
            var parent = _contentArea.parent;
            Destroy(_contentArea.gameObject);

            var newContent = UIBuilder.Child(parent.GetComponent<RectTransform>(), "ContentArea");
            _contentArea = newContent.GetComponent<RectTransform>();
            _contentArea.anchorMin = Vector2.zero;
            _contentArea.anchorMax = Vector2.one;
            _contentArea.offsetMin = new Vector2(UITheme.FMSidebarWidth, 0);
            _contentArea.offsetMax = Vector2.zero;
            newContent.AddComponent<Image>().color = UITheme.Background;

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

        public void ShowPreGame(CalendarEvent gameEvent)
        {
            _currentPanel = "PreGame";
            // Delegate to ArtInjector which registers the PreGame panel dynamically
            var artInjector = GetComponent<ArtInjector>();
            artInjector?.ShowPreGame(gameEvent);
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
