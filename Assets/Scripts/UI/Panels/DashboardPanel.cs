using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Components;
using CalendarEvent = NBAHeadCoach.Core.Data.CalendarEvent;
using SeasonPhase = NBAHeadCoach.Core.Data.SeasonPhase;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Main dashboard panel with ESPN broadcast-style visual treatment.
    /// Programmatically builds styled UI on first show, then refreshes data on subsequent shows.
    /// </summary>
    public class DashboardPanel : BasePanel
    {
        [Header("Layout Containers")]
        public Transform HeroContainer;
        public Transform GridContainer;

        [Header("Team Info")]
        [SerializeField] private Text _teamNameText;
        [SerializeField] private Text _recordText;
        [SerializeField] private Text _standingsText;
        [SerializeField] private Text _coachNameText;
        [SerializeField] private Image _teamLogo;

        [Header("Date & Season")]
        [SerializeField] private Text _currentDateText;
        [SerializeField] private Text _seasonText;
        [SerializeField] private Text _phaseText;

        [Header("Next Game")]
        [SerializeField] private GameObject _nextGamePanel;
        [SerializeField] private Text _nextGameDateText;
        [SerializeField] private Text _nextGameOpponentText;
        [SerializeField] private Text _nextGameLocationText;
        [SerializeField] private Button _playNextGameButton;
        [SerializeField] private Button _simNextGameButton;

        [Header("Last Game")]
        [SerializeField] private GameObject _lastGamePanel;
        [SerializeField] private Text _lastGameResultText;
        [SerializeField] private Text _lastGameScoreText;
        [SerializeField] private Text _lastGameHighlightText;

        [Header("Quick Stats")]
        [SerializeField] private Text _winsText;
        [SerializeField] private Text _lossesText;
        [SerializeField] private Text _winStreakText;
        [SerializeField] private Text _homeRecordText;
        [SerializeField] private Text _awayRecordText;

        [Header("Roster Summary")]
        [SerializeField] private Text _rosterCountText;
        [SerializeField] private Text _injuredPlayersText;
        [SerializeField] private Text _starPlayerText;

        [Header("Financial Summary")]
        [SerializeField] private Text _salaryCapText;
        [SerializeField] private Text _capSpaceText;

        [Header("Inbox Preview")]
        [SerializeField] private Transform _inboxPreviewContainer;
        [SerializeField] private Text _unreadMessagesText;

        [Header("Action Buttons")]
        [SerializeField] private Button _advanceDayButton;
        [SerializeField] private Button _simToNextGameButton;
        [SerializeField] private Button _viewCalendarButton;
        [SerializeField] private Button _viewRosterButton;
        [SerializeField] private Button _saveGameButton;

        private List<DashboardWidget> _widgets = new List<DashboardWidget>();

        // Events
        public event Action OnAdvanceDayClicked;
        public event Action OnSimToNextGameClicked;
        public event Action<CalendarEvent> OnPlayGameClicked;
        public event Action<CalendarEvent> OnSimGameClicked;
        public event Action OnViewCalendarClicked;
        public event Action OnViewRosterClicked;
        public event Action OnSaveGameClicked;

        // Financial constants (TODO: pull from league/season config data)
        private const long SalaryCapAmount = 136_000_000;
        private const long LuxuryTaxThreshold = 172_000_000;

        // ── ESPN Build State ──
        private bool _isBuilt;
        private GameObject _espnRoot;

        // Team color cache
        private Color _teamPrimary = UITheme.AccentPrimary;
        private Color _teamSecondary = UITheme.AccentSecondary;

        // Team Header refs
        private Image _headerBgImage;
        private Image _headerAccentStrip;
        private Text _headerTeamName;
        private Text _headerCoachName;
        private Text _headerSeasonPhase;
        private Text _headerDate;

        // Hero section refs
        private UICardContainer _heroCard;
        private Text _heroPlayerTeamText;
        private Text _heroOpponentText;
        private Text _heroPlayerRecordText;
        private Text _heroOpponentRecordText;
        private Text _heroGameDateText;
        private Text _heroVsText;
        private GameObject _heroMatchupContent;
        private Text _heroNoGameText;
        private Text _heroSubText;

        // Quick stats refs
        private UIStatBlock _statRecord;
        private UIStatBlock _statStreak;
        private UIStatBlock _statHome;
        private UIStatBlock _statAway;
        private UICardContainer _streakCard;

        // Last game refs
        private UICardContainer _lastGameCard;
        private Text _lastGameScoreDisplay;
        private Text _lastGameOpponentDisplay;
        private UIBadge _lastGameBadge;
        private GameObject _lastGameCardGo;

        // Roster & Financials refs
        private Text _rosterCountDisplay;
        private Text _injuredDisplay;
        private Text _starPlayerDisplay;
        private Text _payrollDisplay;
        private Text _capSpaceDisplay;
        private Text _luxuryTaxDisplay;

        protected override void Awake()
        {
            base.Awake();
            _useFadeTransition = true;
            SetupButtons();
        }

        private void SetupButtons()
        {
            if (_advanceDayButton != null)
                _advanceDayButton.onClick.AddListener(() => OnAdvanceDayClicked?.Invoke());

            if (_simToNextGameButton != null)
                _simToNextGameButton.onClick.AddListener(() => OnSimToNextGameClicked?.Invoke());

            if (_playNextGameButton != null)
                _playNextGameButton.onClick.AddListener(OnPlayNextGame);

            if (_simNextGameButton != null)
                _simNextGameButton.onClick.AddListener(OnSimNextGame);

            if (_viewCalendarButton != null)
                _viewCalendarButton.onClick.AddListener(() => OnViewCalendarClicked?.Invoke());

            if (_viewRosterButton != null)
                _viewRosterButton.onClick.AddListener(() => OnViewRosterClicked?.Invoke());

            if (_saveGameButton != null)
                _saveGameButton.onClick.AddListener(() => OnSaveGameClicked?.Invoke());
        }

        protected override void OnShown()
        {
            base.OnShown();
            Debug.Log($"[DashboardPanel] OnShown called. _isBuilt={_isBuilt}, childCount={transform.childCount}");

            if (!_isBuilt)
            {
                Debug.Log("[DashboardPanel] Building ESPN Dashboard...");
                BuildESPNDashboard();
                _isBuilt = true;
                Debug.Log($"[DashboardPanel] ESPN build complete. _espnRoot={_espnRoot != null}, childCount={transform.childCount}");
            }

            HideLegacyElements();
            Debug.Log($"[DashboardPanel] After HideLegacyElements. _espnRoot active={_espnRoot?.activeSelf}");
            RefreshDashboard();
            RefreshAllWidgets();
        }

        // ══════════════════════════════════════════════════════════════
        //  BUILD — Programmatic ESPN Dashboard Construction
        // ══════════════════════════════════════════════════════════════

        private void BuildESPNDashboard()
        {
            Debug.Log($"[DashboardPanel] BuildESPNDashboard START. Parent={transform.name}, rect={GetComponent<RectTransform>()?.rect}");
            // Cleanup any previous build
            if (_espnRoot != null) Destroy(_espnRoot);

            // Root container with vertical layout
            _espnRoot = new GameObject("ESPNDashboard", typeof(RectTransform));
            _espnRoot.transform.SetParent(transform, false);

            RectTransform rootRt = _espnRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = new Vector2(UITheme.CardGap, UITheme.CardGap);
            rootRt.offsetMax = new Vector2(-UITheme.CardGap, -UITheme.CardGap);

            VerticalLayoutGroup rootVlg = _espnRoot.AddComponent<VerticalLayoutGroup>();
            rootVlg.spacing = UITheme.CardGap;
            rootVlg.childControlWidth = true;
            rootVlg.childControlHeight = false;
            rootVlg.childForceExpandWidth = true;
            rootVlg.childForceExpandHeight = false;
            rootVlg.padding = new RectOffset(0, 0, 0, 0);

            // Add content size fitter for scroll support
            ContentSizeFitter csf = _espnRoot.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildTeamHeader(_espnRoot.transform);
            BuildHeroSection(_espnRoot.transform);
            BuildActionBar(_espnRoot.transform);
            BuildQuickStatsRow(_espnRoot.transform);
            BuildLastGameCard(_espnRoot.transform);
            BuildInfoRow(_espnRoot.transform);
            StyleActionButtons();
        }

        /// <summary>
        /// Hide ALL scene-wired legacy elements so the ESPN layout is visible.
        /// Iterates all children and hides everything that isn't the ESPN root.
        /// </summary>
        private void HideLegacyElements()
        {
            // Hide ALL direct children except the ESPN root
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child != _espnRoot)
                    child.SetActive(false);
            }

            // Ensure ESPN root is visible
            if (_espnRoot != null) _espnRoot.SetActive(true);
        }

        private void SetTextVisible(Text t, bool visible)
        {
            if (t != null) t.gameObject.SetActive(visible);
        }

        // ── Task 11: Team Header & Date Display ──

        private void BuildTeamHeader(Transform parent)
        {
            GameObject headerGo = new GameObject("TeamHeader", typeof(RectTransform));
            headerGo.transform.SetParent(parent, false);

            _headerBgImage = headerGo.AddComponent<Image>();
            _headerBgImage.color = UITheme.PanelHeader; // Updated with team color in refresh

            LayoutElement headerLE = headerGo.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 48f;

            HorizontalLayoutGroup hlg = headerGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.spacing = 16f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Team name (larger, bold)
            _headerTeamName = CreateText(headerGo.transform, "", UITheme.FontSizeHeader + 2, FontStyle.Bold, UITheme.TextPrimary, 300f);

            // Coach name
            _headerCoachName = CreateText(headerGo.transform, "", UITheme.FontSizeBody, FontStyle.Normal, UITheme.TextSecondary, 200f);

            // Spacer
            GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(headerGo.transform, false);
            LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1f;

            // Season + Phase
            _headerSeasonPhase = CreateText(headerGo.transform, "", UITheme.FontSizeBody, FontStyle.Bold, UITheme.AccentPrimary, 250f);
            _headerSeasonPhase.alignment = TextAnchor.MiddleRight;

            // Date
            _headerDate = CreateText(headerGo.transform, "", UITheme.FontSizeBody, FontStyle.Normal, UITheme.TextSecondary, 220f);
            _headerDate.alignment = TextAnchor.MiddleRight;

            // Team-colored accent strip below header
            GameObject accentStrip = new GameObject("AccentStrip", typeof(RectTransform));
            accentStrip.transform.SetParent(parent, false);
            _headerAccentStrip = accentStrip.AddComponent<Image>();
            _headerAccentStrip.color = UITheme.AccentPrimary; // Updated in refresh
            LayoutElement stripLE = accentStrip.AddComponent<LayoutElement>();
            stripLE.preferredHeight = 3f;
        }

        // ── Task 7: Hero Section (Next Game Matchup) ──

        private void BuildHeroSection(Transform parent)
        {
            _heroCard = UICardContainer.Create("HeroCard", parent);

            LayoutElement heroLE = _heroCard.gameObject.GetComponent<LayoutElement>();
            if (heroLE == null) heroLE = _heroCard.gameObject.AddComponent<LayoutElement>();
            heroLE.preferredHeight = UITheme.HeroHeight;

            // Matchup content (visible when game exists)
            _heroMatchupContent = new GameObject("MatchupContent", typeof(RectTransform));
            _heroMatchupContent.transform.SetParent(_heroCard.ContentArea, false);
            StretchFill(_heroMatchupContent.GetComponent<RectTransform>());

            VerticalLayoutGroup matchupVlg = _heroMatchupContent.AddComponent<VerticalLayoutGroup>();
            matchupVlg.childControlWidth = true;
            matchupVlg.childControlHeight = true;
            matchupVlg.childForceExpandWidth = true;
            matchupVlg.childForceExpandHeight = false;
            matchupVlg.spacing = 6f;
            matchupVlg.childAlignment = TextAnchor.MiddleCenter;
            matchupVlg.padding = new RectOffset(20, 20, 12, 12);

            // "NEXT GAME" label
            Text nextGameLabel = CreateLayoutText(_heroMatchupContent.transform, "NEXT GAME",
                UITheme.FontSizeSmall, FontStyle.Bold, UITheme.AccentPrimary, 22f);
            nextGameLabel.alignment = TextAnchor.MiddleCenter;

            // Team names row
            GameObject teamsRow = new GameObject("TeamsRow", typeof(RectTransform));
            teamsRow.transform.SetParent(_heroMatchupContent.transform, false);
            HorizontalLayoutGroup teamsHlg = teamsRow.AddComponent<HorizontalLayoutGroup>();
            teamsHlg.childControlWidth = true;
            teamsHlg.childControlHeight = true;
            teamsHlg.childForceExpandWidth = true;
            teamsHlg.childForceExpandHeight = true;
            teamsHlg.spacing = 12f;
            teamsHlg.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement teamsLE = teamsRow.AddComponent<LayoutElement>();
            teamsLE.preferredHeight = 48f;

            _heroPlayerTeamText = CreateLayoutText(teamsRow.transform, "", UITheme.FontSizeBroadcastTitle, FontStyle.Bold, UITheme.TextPrimary, 40f);
            _heroPlayerTeamText.alignment = TextAnchor.MiddleRight;

            // VS badge with gold background
            GameObject vsBadgeGo = new GameObject("VsBadge", typeof(RectTransform));
            vsBadgeGo.transform.SetParent(teamsRow.transform, false);
            Image vsBg = vsBadgeGo.AddComponent<Image>();
            vsBg.color = UITheme.AccentPrimary;
            LayoutElement vsLE = vsBadgeGo.AddComponent<LayoutElement>();
            vsLE.preferredWidth = 40f;
            vsLE.preferredHeight = 40f;
            vsLE.flexibleWidth = 0;

            _heroVsText = vsBadgeGo.AddComponent<Text>();
            _heroVsText.text = "VS";
            _heroVsText.font = Resources.GetBuiltinResource<Font>(UITheme.FontName);
            _heroVsText.fontSize = UITheme.FontSizeBody;
            _heroVsText.fontStyle = FontStyle.Bold;
            _heroVsText.color = UITheme.Background;
            _heroVsText.alignment = TextAnchor.MiddleCenter;
            _heroVsText.raycastTarget = false;

            _heroOpponentText = CreateLayoutText(teamsRow.transform, "", UITheme.FontSizeBroadcastTitle, FontStyle.Bold, UITheme.TextPrimary, 40f);
            _heroOpponentText.alignment = TextAnchor.MiddleLeft;

            // Records row
            GameObject recordsRow = new GameObject("RecordsRow", typeof(RectTransform));
            recordsRow.transform.SetParent(_heroMatchupContent.transform, false);
            HorizontalLayoutGroup recordsHlg = recordsRow.AddComponent<HorizontalLayoutGroup>();
            recordsHlg.childControlWidth = true;
            recordsHlg.childControlHeight = true;
            recordsHlg.childForceExpandWidth = true;
            recordsHlg.childForceExpandHeight = true;
            recordsHlg.spacing = 12f;
            recordsHlg.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement recordsLE = recordsRow.AddComponent<LayoutElement>();
            recordsLE.preferredHeight = 22f;

            _heroPlayerRecordText = CreateLayoutText(recordsRow.transform, "", UITheme.FontSizeBody, FontStyle.Normal, UITheme.TextSecondary, 20f);
            _heroPlayerRecordText.alignment = TextAnchor.MiddleRight;

            // Center spacer for records
            Text recordSpacer = CreateLayoutText(recordsRow.transform, "", UITheme.FontSizeBody, FontStyle.Normal, UITheme.TextSecondary, 20f);
            recordSpacer.GetComponent<LayoutElement>().flexibleWidth = 0;
            recordSpacer.GetComponent<LayoutElement>().preferredWidth = 40f;

            _heroOpponentRecordText = CreateLayoutText(recordsRow.transform, "", UITheme.FontSizeBody, FontStyle.Normal, UITheme.TextSecondary, 20f);
            _heroOpponentRecordText.alignment = TextAnchor.MiddleLeft;

            // Game date + location
            _heroGameDateText = CreateLayoutText(_heroMatchupContent.transform, "",
                UITheme.FontSizeBody, FontStyle.Bold, UITheme.AccentSecondary, 26f);
            _heroGameDateText.alignment = TextAnchor.MiddleCenter;

            // No game message (hidden by default)
            GameObject noGameContainer = new GameObject("NoGameContainer", typeof(RectTransform));
            noGameContainer.transform.SetParent(_heroCard.ContentArea, false);
            StretchFill(noGameContainer.GetComponent<RectTransform>());
            VerticalLayoutGroup noGameVlg = noGameContainer.AddComponent<VerticalLayoutGroup>();
            noGameVlg.childControlWidth = true;
            noGameVlg.childControlHeight = true;
            noGameVlg.childForceExpandWidth = true;
            noGameVlg.childForceExpandHeight = false;
            noGameVlg.childAlignment = TextAnchor.MiddleCenter;
            noGameVlg.spacing = 4f;

            _heroNoGameText = CreateLayoutText(noGameContainer.transform, "",
                UITheme.FontSizeHero, FontStyle.Bold, UITheme.AccentPrimary, 50f);
            _heroNoGameText.alignment = TextAnchor.MiddleCenter;

            _heroSubText = CreateLayoutText(noGameContainer.transform, "",
                UITheme.FontSizeBody, FontStyle.Normal, UITheme.TextSecondary, 24f);
            _heroSubText.alignment = TextAnchor.MiddleCenter;

            noGameContainer.SetActive(false);
            // Store the container reference so we can toggle it
            _heroNoGameText.gameObject.transform.parent.gameObject.name = "NoGameContainer";
        }

        // ── Action Bar (Play/Sim/Advance/Save buttons) ──

        private void BuildActionBar(Transform parent)
        {
            GameObject barGo = new GameObject("ActionBar", typeof(RectTransform));
            barGo.transform.SetParent(parent, false);

            Image barBg = barGo.AddComponent<Image>();
            barBg.color = UITheme.PanelHeader;

            LayoutElement barLE = barGo.AddComponent<LayoutElement>();
            barLE.preferredHeight = 44f;

            HorizontalLayoutGroup hlg = barGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 6, 6);
            hlg.spacing = 10f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            // Reparent existing buttons into the action bar
            ReparentButton(_playNextGameButton, barGo.transform);
            ReparentButton(_simNextGameButton, barGo.transform);
            ReparentButton(_advanceDayButton, barGo.transform);
            ReparentButton(_simToNextGameButton, barGo.transform);
            ReparentButton(_viewCalendarButton, barGo.transform);
            ReparentButton(_viewRosterButton, barGo.transform);
            ReparentButton(_saveGameButton, barGo.transform);
        }

        private void ReparentButton(Button btn, Transform newParent)
        {
            if (btn == null) return;
            btn.transform.SetParent(newParent, false);

            // Ensure it participates in layout
            LayoutElement le = btn.GetComponent<LayoutElement>();
            if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = 32f;
        }

        // ── Task 8: Quick Stats Row (4-column) ──

        private void BuildQuickStatsRow(Transform parent)
        {
            GameObject rowGo = new GameObject("QuickStatsRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            HorizontalLayoutGroup hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = UITheme.CardGap;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            LayoutElement rowLE = rowGo.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 90f;

            _statRecord = CreateStatCard(rowGo.transform, "RecordStat", "RECORD", "0-0", UITheme.AccentPrimary);
            _statStreak = CreateStatCard(rowGo.transform, "StreakStat", "STREAK", "-", UITheme.AccentPrimary, out _streakCard);
            _statHome = CreateStatCard(rowGo.transform, "HomeStat", "HOME", "0-0", UITheme.AccentPrimary);
            _statAway = CreateStatCard(rowGo.transform, "AwayStat", "AWAY", "0-0", UITheme.AccentPrimary);
        }

        private UIStatBlock CreateStatCard(Transform parent, string name, string label, string value, Color accentColor)
        {
            return CreateStatCard(parent, name, label, value, accentColor, out _);
        }

        private UIStatBlock CreateStatCard(Transform parent, string name, string label, string value, Color accentColor, out UICardContainer card)
        {
            card = UICardContainer.Create(name + "Card", parent);
            card.SetAccentBorder(accentColor);

            // Remove default VLG padding for compact look
            VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.padding = new RectOffset(12, 8, 4, 4);

            UIStatBlock block = UIStatBlock.Create(name, card.ContentArea, label, value);
            return block;
        }

        // ── Task 9: Last Game Card ──

        private void BuildLastGameCard(Transform parent)
        {
            _lastGameCard = UICardContainer.Create("LastGameCard", parent);

            LayoutElement le = _lastGameCard.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = _lastGameCard.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 100f;

            _lastGameCardGo = _lastGameCard.gameObject;

            // Header row with section header and badge
            GameObject headerRow = new GameObject("HeaderRow", typeof(RectTransform));
            headerRow.transform.SetParent(_lastGameCard.ContentArea, false);
            HorizontalLayoutGroup headerHlg = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerHlg.childControlWidth = false;
            headerHlg.childControlHeight = true;
            headerHlg.childForceExpandWidth = false;
            headerHlg.childForceExpandHeight = true;
            headerHlg.spacing = 8f;
            LayoutElement headerLE = headerRow.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 36f;

            UISectionHeader.Create("LastGameHeader", headerRow.transform, "LAST GAME");

            // Spacer
            GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(headerRow.transform, false);
            LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1f;

            // Badge (created but updated in refresh)
            _lastGameBadge = UIBadge.CreateWinBadge(headerRow.transform);

            // Large score display
            _lastGameScoreDisplay = CreateLayoutText(_lastGameCard.ContentArea, "",
                UITheme.FontSizeHeroNumber, FontStyle.Bold, UITheme.TextPrimary, 44f);
            _lastGameScoreDisplay.alignment = TextAnchor.MiddleCenter;

            // Opponent + date subtitle
            _lastGameOpponentDisplay = CreateLayoutText(_lastGameCard.ContentArea, "",
                UITheme.FontSizeBody, FontStyle.Normal, UITheme.TextSecondary, 20f);
            _lastGameOpponentDisplay.alignment = TextAnchor.MiddleCenter;
        }

        // ── Task 10: Roster & Financials Row (2-column) ──

        private void BuildInfoRow(Transform parent)
        {
            GameObject rowGo = new GameObject("InfoRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            HorizontalLayoutGroup hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = UITheme.CardGap;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            LayoutElement rowLE = rowGo.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 130f;

            // Roster Card
            UICardContainer rosterCard = UICardContainer.Create("RosterCard", rowGo.transform);
            rosterCard.SetAccentBorder(UITheme.AccentSecondary);
            UISectionHeader.Create("RosterHeader", rosterCard.ContentArea, "ROSTER");
            _rosterCountDisplay = CreateInfoLine(rosterCard.ContentArea, "Players");
            _injuredDisplay = CreateInfoLine(rosterCard.ContentArea, "Injured");
            _starPlayerDisplay = CreateInfoLine(rosterCard.ContentArea, "Star");

            // Financials Card
            UICardContainer financialsCard = UICardContainer.Create("FinancialsCard", rowGo.transform);
            financialsCard.SetAccentBorder(UITheme.AccentSecondary);
            UISectionHeader.Create("FinancialsHeader", financialsCard.ContentArea, "FINANCIALS");
            _payrollDisplay = CreateInfoLine(financialsCard.ContentArea, "Payroll");
            _capSpaceDisplay = CreateInfoLine(financialsCard.ContentArea, "Cap Space");
            _luxuryTaxDisplay = CreateInfoLine(financialsCard.ContentArea, "Luxury Tax");
        }

        private Text CreateInfoLine(Transform parent, string placeholder)
        {
            Text t = CreateLayoutText(parent, placeholder, UITheme.FontSizeBody, FontStyle.Normal, UITheme.TextPrimary, 22f);
            return t;
        }

        // ── Task 13: Style Action Buttons ──

        private void StyleActionButtons()
        {
            StyleButton(_advanceDayButton, UITheme.PanelHeader);
            StyleButton(_simToNextGameButton, UITheme.PanelHeader);
            StyleButton(_viewCalendarButton, UITheme.PanelHeader);
            StyleButton(_viewRosterButton, UITheme.PanelHeader);
            StyleButton(_saveGameButton, UITheme.PanelHeader);

            // Play/Sim get stronger colors
            var team = GameManager.Instance?.GetPlayerTeam();
            Color teamColor = UITheme.AccentSecondary;
            if (team != null)
                teamColor = UITheme.ParseTeamColor(team.PrimaryColor, UITheme.AccentSecondary);

            StyleButton(_playNextGameButton, teamColor);
            StyleButton(_simNextGameButton, UITheme.AccentSecondary);
        }

        private void StyleButton(Button btn, Color bgColor)
        {
            if (btn == null) return;

            Image img = btn.GetComponent<Image>();
            if (img != null) img.color = bgColor;

            // Set up hover color block
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = cb;

            // Style text children
            Text txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.color = UITheme.TextPrimary;
                txt.fontStyle = FontStyle.Bold;
            }

            // Add shadow if missing
            if (btn.GetComponent<Shadow>() == null)
            {
                Shadow shadow = btn.gameObject.AddComponent<Shadow>();
                shadow.effectDistance = new Vector2(2, -2);
                shadow.effectColor = new Color(0, 0, 0, 0.4f);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  REFRESH — Update all ESPN components with live data
        // ══════════════════════════════════════════════════════════════

        public void RefreshDashboard()
        {
            if (GameManager.Instance == null || !_isBuilt)
            {
                Debug.Log($"[DashboardPanel] RefreshDashboard skipped. GameManager={GameManager.Instance != null}, _isBuilt={_isBuilt}");
                return;
            }

            // Cache team colors for broadcast styling
            var team = GameManager.Instance.GetPlayerTeam();
            if (team != null)
            {
                _teamPrimary = UITheme.ParseTeamColor(team.PrimaryColor, UITheme.AccentPrimary);
                _teamSecondary = UITheme.ParseTeamColor(team.SecondaryColor, UITheme.AccentSecondary);
            }

            RefreshTeamInfo();
            RefreshDateInfo();
            RefreshNextGame();
            RefreshLastGame();
            RefreshQuickStats();
            RefreshRosterSummary();
            RefreshFinancialSummary();
        }

        #region Data Refresh Methods

        private void RefreshTeamInfo()
        {
            var team = GameManager.Instance.GetPlayerTeam();
            if (team == null) return;

            string teamFullName = $"{team.City} {team.Name}";

            // Legacy (kept for backward compat if scene-wired)
            if (_teamNameText != null) _teamNameText.text = teamFullName;
            if (_recordText != null) _recordText.text = $"{team.Wins}-{team.Losses}";

            if (_standingsText != null)
            {
                var seasonController = GameManager.Instance.SeasonController;
                if (seasonController != null)
                {
                    int rank = seasonController.GetConferenceRank(team.TeamId);
                    bool inPlayoffs = seasonController.IsPlayoffTeam(team.TeamId);
                    string playoffStatus = inPlayoffs ? "(Playoff Position)" : "";
                    _standingsText.text = $"#{rank} in {team.Conference} {playoffStatus}";
                }
            }

            if (_coachNameText != null)
            {
                var career = GameManager.Instance.Career;
                if (career != null) _coachNameText.text = $"Coach {career.PersonName}";
            }

            // ESPN header - team-colored broadcast bar
            if (_headerBgImage != null)
                _headerBgImage.color = UITheme.DarkenColor(_teamPrimary, 0.2f);
            if (_headerAccentStrip != null)
                _headerAccentStrip.color = _teamSecondary;

            if (_headerTeamName != null)
            {
                _headerTeamName.text = teamFullName.ToUpper();
                _headerTeamName.color = _teamPrimary;
            }

            if (_headerCoachName != null)
            {
                var career = GameManager.Instance.Career;
                if (career != null)
                    _headerCoachName.text = $"Coach {career.PersonName}";
            }

            // Hero card team-colored gradient
            if (_heroCard != null)
            {
                _heroCard.SetAccentBorder(_teamPrimary);
                _heroCard.SetGradient(
                    UITheme.DarkenColor(_teamPrimary, 0.25f),
                    UITheme.CardBackground
                );
            }
        }

        private void RefreshDateInfo()
        {
            var seasonController = GameManager.Instance.SeasonController;
            if (seasonController == null) return;

            string dateStr = seasonController.CurrentDate.ToString("dddd, MMMM d, yyyy");
            string seasonStr = $"{seasonController.CurrentSeason}-{seasonController.CurrentSeason + 1}";
            string phaseStr = GetPhaseDisplayName(seasonController.CurrentPhase);

            // Legacy
            if (_currentDateText != null) _currentDateText.text = dateStr;
            if (_seasonText != null) _seasonText.text = $"{seasonStr} Season";
            if (_phaseText != null) _phaseText.text = phaseStr;

            // ESPN header
            if (_headerSeasonPhase != null) _headerSeasonPhase.text = $"{seasonStr} | {phaseStr}";
            if (_headerDate != null) _headerDate.text = dateStr;
        }

        private string GetPhaseDisplayName(SeasonPhase phase)
        {
            return phase switch
            {
                SeasonPhase.Preseason => "Preseason",
                SeasonPhase.RegularSeason => "Regular Season",
                SeasonPhase.AllStarBreak => "All-Star Break",
                SeasonPhase.Playoffs => "Playoffs",
                SeasonPhase.Draft => "Draft",
                SeasonPhase.FreeAgency => "Free Agency",
                SeasonPhase.SummerLeague => "Summer League",
                SeasonPhase.TrainingCamp => "Training Camp",
                SeasonPhase.Offseason => "Offseason",
                _ => phase.ToString()
            };
        }

        private string GetOffseasonMessage(SeasonPhase phase)
        {
            return phase switch
            {
                SeasonPhase.Preseason => "Prepare your roster for the regular season",
                SeasonPhase.Draft => "Select the future of your franchise",
                SeasonPhase.FreeAgency => "Build your championship roster",
                SeasonPhase.Offseason => "Rest up. The next season awaits.",
                SeasonPhase.TrainingCamp => "Develop your players and set rotations",
                SeasonPhase.SummerLeague => "Evaluate your young talent",
                SeasonPhase.AllStarBreak => "Enjoy the break before the stretch run",
                _ => "No upcoming games scheduled"
            };
        }

        private void RefreshNextGame()
        {
            var seasonController = GameManager.Instance.SeasonController;
            var nextGame = seasonController?.GetNextGame();
            var team = GameManager.Instance.GetPlayerTeam();

            // Legacy
            if (_nextGamePanel != null) _nextGamePanel.SetActive(false);
            if (_nextGameDateText != null && nextGame != null) _nextGameDateText.text = nextGame.Date.ToString("MMM d");
            if (_nextGameOpponentText != null && nextGame != null)
            {
                string opId = nextGame.IsHomeGame ? nextGame.AwayTeamId : nextGame.HomeTeamId;
                var opp = GameManager.Instance.GetTeam(opId);
                _nextGameOpponentText.text = $"{opp?.City ?? ""} {opp?.Name ?? opId}";
            }
            if (_nextGameLocationText != null && nextGame != null)
                _nextGameLocationText.text = nextGame.IsHomeGame ? "HOME" : "AWAY";

            // ESPN Hero
            if (nextGame == null)
            {
                // No upcoming game — show broadcast-style phase card
                if (_heroMatchupContent != null) _heroMatchupContent.SetActive(false);
                var noGameContainer = _heroNoGameText?.transform.parent?.gameObject;
                if (noGameContainer != null) noGameContainer.SetActive(true);
                if (_heroNoGameText != null)
                {
                    string phase = seasonController != null ? GetPhaseDisplayName(seasonController.CurrentPhase) : "Offseason";
                    _heroNoGameText.text = phase.ToUpper();
                }
                if (_heroSubText != null)
                {
                    string msg = seasonController != null ? GetOffseasonMessage(seasonController.CurrentPhase) : "No upcoming games scheduled";
                    _heroSubText.text = msg;
                }
            }
            else
            {
                if (_heroMatchupContent != null) _heroMatchupContent.SetActive(true);
                var noGameContainer = _heroNoGameText?.transform.parent?.gameObject;
                if (noGameContainer != null) noGameContainer.SetActive(false);

                string opponentId = nextGame.IsHomeGame ? nextGame.AwayTeamId : nextGame.HomeTeamId;
                var opponent = GameManager.Instance.GetTeam(opponentId);

                if (_heroPlayerTeamText != null && team != null)
                    _heroPlayerTeamText.text = $"{team.City} {team.Name}".ToUpper();

                if (_heroOpponentText != null)
                    _heroOpponentText.text = $"{opponent?.City ?? ""} {opponent?.Name ?? opponentId}".ToUpper();

                if (_heroPlayerRecordText != null && team != null)
                    _heroPlayerRecordText.text = $"({team.Wins}-{team.Losses})";

                if (_heroOpponentRecordText != null && opponent != null)
                    _heroOpponentRecordText.text = $"({opponent.Wins}-{opponent.Losses})";

                if (_heroGameDateText != null)
                {
                    string location = nextGame.IsHomeGame ? "HOME" : "AWAY";
                    _heroGameDateText.text = $"{nextGame.Date:ddd MMM d} | {location}";
                }

                // Show play buttons only if today's game
                bool isToday = nextGame.Date.Date == seasonController.CurrentDate.Date;
                if (_playNextGameButton != null) _playNextGameButton.gameObject.SetActive(isToday);
                if (_simNextGameButton != null) _simNextGameButton.gameObject.SetActive(isToday);
            }
        }

        private void RefreshLastGame()
        {
            var seasonController = GameManager.Instance.SeasonController;
            var completedGames = seasonController?.CompletedGames;

            if (completedGames == null || completedGames.Count == 0)
            {
                // Legacy
                if (_lastGamePanel != null) _lastGamePanel.SetActive(false);
                // ESPN
                if (_lastGameCardGo != null) _lastGameCardGo.SetActive(false);
                return;
            }

            var lastGame = completedGames.OrderByDescending(g => g.Date).First();
            string playerTeamId = GameManager.Instance.PlayerTeamId;

            bool isHome = lastGame.HomeTeamId == playerTeamId;
            int playerScore = isHome ? lastGame.HomeScore : lastGame.AwayScore;
            int opponentScore = isHome ? lastGame.AwayScore : lastGame.HomeScore;
            bool won = playerScore > opponentScore;

            string opponentId = isHome ? lastGame.AwayTeamId : lastGame.HomeTeamId;
            var opponent = GameManager.Instance.GetTeam(opponentId);

            // Legacy
            if (_lastGamePanel != null) _lastGamePanel.SetActive(false);
            if (_lastGameResultText != null)
            {
                _lastGameResultText.text = won ? "WIN" : "LOSS";
                _lastGameResultText.color = won ? Color.green : Color.red;
            }
            if (_lastGameScoreText != null)
                _lastGameScoreText.text = $"{playerScore}-{opponentScore} vs {opponent?.Name ?? opponentId}";

            // ESPN
            if (_lastGameCardGo != null) _lastGameCardGo.SetActive(true);

            if (_lastGameCard != null)
            {
                _lastGameCard.SetAccentBorder(won ? UITheme.Success : UITheme.Danger);
                // Subtle win/loss tint on background
                Color tint = won ? new Color32(20, 45, 20, 240) : new Color32(45, 20, 20, 240);
                _lastGameCard.SetBackgroundColor(tint);
            }

            if (_lastGameScoreDisplay != null)
                _lastGameScoreDisplay.text = $"{playerScore} - {opponentScore}";

            if (_lastGameOpponentDisplay != null)
                _lastGameOpponentDisplay.text = $"vs {opponent?.City ?? ""} {opponent?.Name ?? opponentId}  |  {lastGame.Date:MMM d}";

            if (_lastGameBadge != null)
            {
                _lastGameBadge.SetText(won ? "WIN" : "LOSS");
                _lastGameBadge.SetColor(won ? UITheme.Success : UITheme.Danger);
            }
        }

        private void RefreshQuickStats()
        {
            var team = GameManager.Instance.GetPlayerTeam();
            if (team == null) return;

            // Legacy
            if (_winsText != null) _winsText.text = team.Wins.ToString();
            if (_lossesText != null) _lossesText.text = team.Losses.ToString();

            var standings = GameManager.Instance.SeasonController?.GetTeamStandings(team.TeamId);

            // ESPN stat blocks - colored based on record
            if (_statRecord != null)
            {
                _statRecord.UpdateValue($"{team.Wins}-{team.Losses}");
                _statRecord.ValueText.color = team.Wins > team.Losses ? UITheme.Success :
                                               team.Wins < team.Losses ? UITheme.Danger : UITheme.TextPrimary;
            }

            if (standings != null)
            {
                // Legacy
                if (_winStreakText != null)
                {
                    string streakText = standings.Streak > 0 ? $"W{standings.Streak}" :
                                        standings.Streak < 0 ? $"L{Math.Abs(standings.Streak)}" : "-";
                    _winStreakText.text = streakText;
                }
                if (_homeRecordText != null) _homeRecordText.text = $"{standings.HomeWins}-{standings.HomeLosses}";
                if (_awayRecordText != null) _awayRecordText.text = $"{standings.AwayWins}-{standings.AwayLosses}";

                // ESPN
                string streak = standings.Streak > 0 ? $"W{standings.Streak}" :
                                standings.Streak < 0 ? $"L{Math.Abs(standings.Streak)}" : "-";
                if (_statStreak != null)
                {
                    _statStreak.UpdateValue(streak);
                    // Color code: green for win streak, red for losing streak
                    _statStreak.ValueText.color = standings.Streak > 0 ? UITheme.Success :
                                                  standings.Streak < 0 ? UITheme.Danger : UITheme.TextPrimary;

                    // Hot streak flair: gold tint if streak >= 5, reset otherwise
                    if (_streakCard != null)
                    {
                        if (Math.Abs(standings.Streak) >= 5)
                            _streakCard.SetBackgroundColor(new Color32(40, 38, 20, 240));
                        else
                            _streakCard.SetBackgroundColor(UITheme.CardBackground);
                    }
                }

                if (_statHome != null)
                {
                    _statHome.UpdateValue($"{standings.HomeWins}-{standings.HomeLosses}");
                    _statHome.ValueText.color = standings.HomeWins > standings.HomeLosses ? UITheme.Success :
                                                 standings.HomeWins < standings.HomeLosses ? UITheme.Danger : UITheme.TextPrimary;
                }
                if (_statAway != null)
                {
                    _statAway.UpdateValue($"{standings.AwayWins}-{standings.AwayLosses}");
                    _statAway.ValueText.color = standings.AwayWins > standings.AwayLosses ? UITheme.Success :
                                                 standings.AwayWins < standings.AwayLosses ? UITheme.Danger : UITheme.TextPrimary;
                }
            }
        }

        private void RefreshRosterSummary()
        {
            var team = GameManager.Instance.GetPlayerTeam();
            if (team?.Roster == null) return;

            int playerCount = team.Roster.Count;
            int injured = team.Roster.Count(p => p.IsInjured);
            var starPlayer = team.Roster.OrderByDescending(p => p.Overall).FirstOrDefault();

            // Legacy
            if (_rosterCountText != null) _rosterCountText.text = $"{playerCount} Players";
            if (_injuredPlayersText != null)
            {
                _injuredPlayersText.text = injured > 0 ? $"{injured} Injured" : "All Healthy";
                _injuredPlayersText.color = injured > 0 ? Color.red : Color.green;
            }
            if (_starPlayerText != null && starPlayer != null)
                _starPlayerText.text = $"Star: {starPlayer.FirstName} {starPlayer.LastName} ({starPlayer.Overall})";

            // ESPN
            if (_rosterCountDisplay != null) _rosterCountDisplay.text = $"{playerCount} Players";
            if (_injuredDisplay != null)
            {
                _injuredDisplay.text = injured > 0 ? $"{injured} Injured" : "All Healthy";
                _injuredDisplay.color = injured > 0 ? UITheme.Danger : UITheme.Success;
            }
            if (_starPlayerDisplay != null && starPlayer != null)
            {
                _starPlayerDisplay.text = $"Star: {starPlayer.FirstName} {starPlayer.LastName} ({starPlayer.Overall})";
                _starPlayerDisplay.color = UITheme.AccentPrimary;
            }
        }

        private void RefreshFinancialSummary()
        {
            var team = GameManager.Instance.GetPlayerTeam();
            if (team?.Roster == null) return;

            long totalSalary = team.Roster.Sum(p => p.AnnualSalary);
            long capSpace = SalaryCapAmount - totalSalary;
            long luxuryTax = totalSalary > LuxuryTaxThreshold ? (totalSalary - LuxuryTaxThreshold) / 5 : 0;

            // Legacy
            if (_salaryCapText != null) _salaryCapText.text = $"Payroll: ${totalSalary / 1000000f:F1}M";
            if (_capSpaceText != null)
            {
                _capSpaceText.text = $"Cap Space: ${capSpace / 1000000f:F1}M";
                _capSpaceText.color = capSpace >= 0 ? Color.green : Color.red;
            }

            // ESPN
            if (_payrollDisplay != null) _payrollDisplay.text = $"Payroll: ${totalSalary / 1000000f:F1}M";
            if (_capSpaceDisplay != null)
            {
                _capSpaceDisplay.text = $"Cap Space: ${capSpace / 1000000f:F1}M";
                _capSpaceDisplay.color = capSpace >= 0 ? UITheme.Success : UITheme.Danger;
            }
            if (_luxuryTaxDisplay != null)
            {
                _luxuryTaxDisplay.text = luxuryTax > 0 ? $"Luxury Tax: ${luxuryTax / 1000000f:F1}M" : "No Luxury Tax";
                _luxuryTaxDisplay.color = luxuryTax > 0 ? UITheme.Warning : UITheme.TextSecondary;
            }
        }

        #endregion

        #region Widget Management

        public void RegisterWidget(DashboardWidget widget, bool isHero = false)
        {
            _widgets.Add(widget);
            widget.transform.SetParent(isHero ? HeroContainer : GridContainer, false);

            if (isHero)
            {
                RectTransform rt = widget.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        public void RefreshAllWidgets()
        {
            foreach (var widget in _widgets)
            {
                widget.Refresh();
            }
        }

        #endregion

        #region Button Handlers

        private void OnPlayNextGame()
        {
            var nextGame = GameManager.Instance?.SeasonController?.GetTodaysGame();
            if (nextGame != null)
            {
                OnPlayGameClicked?.Invoke(nextGame);
            }
        }

        private void OnSimNextGame()
        {
            var nextGame = GameManager.Instance?.SeasonController?.GetTodaysGame();
            if (nextGame != null)
            {
                OnSimGameClicked?.Invoke(nextGame);
            }
        }

        #endregion

        #region UI Helpers

        private Text CreateText(Transform parent, string content, int fontSize, FontStyle style, Color color, float prefWidth)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = Resources.GetBuiltinResource<Font>(UITheme.FontName);
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.raycastTarget = false;

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = prefWidth;

            return txt;
        }

        private Text CreateLayoutText(Transform parent, string content, int fontSize, FontStyle style, Color color, float prefHeight)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = Resources.GetBuiltinResource<Font>(UITheme.FontName);
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.raycastTarget = false;

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = prefHeight;

            return txt;
        }

        private void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
