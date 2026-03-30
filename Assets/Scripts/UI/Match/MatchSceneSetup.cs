using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Gameplay;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Util;
using NBAHeadCoach.UI.Components;
using NBAHeadCoach.UI.Shell;

namespace NBAHeadCoach.UI.Match
{
    /// <summary>
    /// Bootstraps the Match scene: creates MatchSimulationController,
    /// builds the match UI programmatically, initializes and starts the game.
    /// Attached to a GameObject in the Match scene or auto-created by GameManager.
    /// </summary>
    public class MatchSceneSetup : MonoBehaviour
    {
        private MatchSimulationController _simController;
        private Canvas _canvas;
        private RectTransform _root;

        // UI references
        private Text _awayTeamName, _awayScoreText, _homeTeamName, _homeScoreText;
        private Text _quarterText, _clockText;
        private Image _awayLogo, _homeLogo;
        private GameObject _homePossessionDot, _awayPossessionDot;
        private RectTransform _playByPlayContent;
        private ScrollRect _playByPlayScroll;
        private Text _gameEndTitle, _gameEndScore, _gameEndResult;
        private GameObject _gameEndOverlay;
        private RectTransform _courtArea;
        private AnimatedCourtView _courtView;

        // Speed buttons
        private Button _pauseBtn, _playBtn;
        private Button[] _speedBtns;
        private Text _speedLabel;

        // Coaching overlays
        private GameObject _coachingOverlay;

        // State
        private Team _homeTeam, _awayTeam, _playerTeam;
        private bool _playerIsHome;
        private int _pbpCount;
        private const int MAX_PBP = 80;

        private IEnumerator Start()
        {
            // Wait a frame for scene to settle
            yield return null;

            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("[MatchSceneSetup] No GameManager"); yield break; }

            var mc = gm.MatchController;
            if (mc == null) { Debug.LogError("[MatchSceneSetup] No MatchController"); yield break; }

            _homeTeam = mc.IsHomeGame ? mc.PlayerTeam : mc.OpponentTeam;
            _awayTeam = mc.IsHomeGame ? mc.OpponentTeam : mc.PlayerTeam;
            _playerTeam = mc.PlayerTeam;
            _playerIsHome = mc.IsHomeGame;

            // Find or create canvas
            _canvas = FindAnyObjectByType<Canvas>();
            if (_canvas == null)
            {
                var canvasGo = new GameObject("MatchCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _canvas = canvasGo.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            // Clear existing UI content
            for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
                Destroy(_canvas.transform.GetChild(i).gameObject);

            // Build root
            _root = CreateRT(_canvas.transform, "MatchRoot");
            Stretch(_root); _root.gameObject.AddComponent<Image>().color = UITheme.Background;

            BuildLayout();

            // Create MatchSimulationController
            var simGo = new GameObject("MatchSimController");
            _simController = simGo.AddComponent<MatchSimulationController>();

            // Wait for singleton to set
            yield return null;

            // Create coach for player
            var playerCoach = new GameCoach(
                _playerTeam.Strategy ?? new TeamStrategy(),
                null, null);

            _simController.InitializeMatch(_homeTeam, _awayTeam, _playerTeam, playerCoach);
            SubscribeToEvents();

            // Setup court view with full court sprite and actual team colors
            var courtBgImg = _courtArea.GetComponent<Image>();
            var fullCourtSprite = ArtManager.GetFullCourt();
            // Pick visible, contrasting team colors
            Color homeColor = PickVisibleColor(_homeTeam);
            Color awayColor = PickVisibleColor(_awayTeam);
            // If too similar, force away to white
            float colorDist = Mathf.Abs(homeColor.r - awayColor.r) + Mathf.Abs(homeColor.g - awayColor.g) + Mathf.Abs(homeColor.b - awayColor.b);
            if (colorDist < 0.6f) awayColor = Color.white;
            _courtView.Setup(courtBgImg, fullCourtSprite, homeColor, awayColor);

            // Initialize with starting lineups
            var homeLineup = _homeTeam.StartingLineupIds?.ToList() ?? new System.Collections.Generic.List<string>();
            var awayLineup = _awayTeam.StartingLineupIds?.ToList() ?? new System.Collections.Generic.List<string>();
            _courtView.Initialize(_homeTeam, _awayTeam, homeLineup, awayLineup);

            // Auto-start simulation
            _simController.StartSimulation();
            Debug.Log($"[MatchSceneSetup] Match started: {_awayTeam.Name} @ {_homeTeam.Name}");
        }

        private void BuildLayout()
        {
            // ── HEADER / SCOREBOARD (top 10%) ──
            var header = CreateRT(_root, "Header");
            header.anchorMin = new Vector2(0, 0.9f); header.anchorMax = Vector2.one; header.sizeDelta = Vector2.zero;
            header.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
            BuildScoreboard(header);

            // ── SPEED CONTROLS (below header, 5%) ──
            var controls = CreateRT(_root, "Controls");
            controls.anchorMin = new Vector2(0, 0.85f); controls.anchorMax = new Vector2(1, 0.9f); controls.sizeDelta = Vector2.zero;
            controls.gameObject.AddComponent<Image>().color = UITheme.PanelSurface;
            BuildSpeedControls(controls);

            // ── COURT VIEW (middle, 45%) ──
            _courtArea = CreateRT(_root, "Court");
            _courtArea.anchorMin = new Vector2(0, 0.35f); _courtArea.anchorMax = new Vector2(1, 0.85f); _courtArea.sizeDelta = Vector2.zero;
            // Court background image (full court sprite, stretched to fill)
            var courtBgImg = _courtArea.gameObject.AddComponent<Image>();
            courtBgImg.color = Color.white;
            courtBgImg.type = Image.Type.Simple;
            courtBgImg.preserveAspect = false;

            _courtView = _courtArea.gameObject.AddComponent<AnimatedCourtView>();

            // ── PLAY-BY-PLAY (bottom 35%) ──
            var pbpArea = CreateRT(_root, "PBP");
            pbpArea.anchorMin = Vector2.zero; pbpArea.anchorMax = new Vector2(1, 0.35f); pbpArea.sizeDelta = Vector2.zero;
            pbpArea.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
            BuildPlayByPlay(pbpArea);

            // ── GAME END OVERLAY (hidden) ──
            BuildGameEndOverlay();
        }

        private void BuildScoreboard(RectTransform parent)
        {
            var hlg = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(20, 20, 4, 4); hlg.spacing = 10;

            // Away team
            var awayBlock = CreateRT(parent, "Away"); awayBlock.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var awayHlg = awayBlock.gameObject.AddComponent<HorizontalLayoutGroup>();
            awayHlg.childControlWidth = true; awayHlg.childControlHeight = true; awayHlg.spacing = 8; awayHlg.childAlignment = TextAnchor.MiddleRight;

            var awayLogoGo = CreateRT(awayBlock, "Logo"); awayLogoGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 50;
            _awayLogo = awayLogoGo.gameObject.AddComponent<Image>(); _awayLogo.preserveAspect = true;
            var sp = ArtManager.GetTeamLogo(_awayTeam.TeamId); if (sp != null) _awayLogo.sprite = sp;

            var awayInfo = CreateRT(awayBlock, "Info"); awayInfo.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
            var aivlg = awayInfo.gameObject.AddComponent<VerticalLayoutGroup>();
            aivlg.childControlWidth = true; aivlg.childControlHeight = true; aivlg.childForceExpandWidth = true;
            _awayTeamName = MkText(awayInfo, _awayTeam.Name, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            _awayTeamName.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

            // Away score
            var awayScoreGo = CreateRT(awayBlock, "Score"); awayScoreGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 70;
            awayScoreGo.gameObject.AddComponent<Image>().color = UITheme.PanelSurface;
            _awayScoreText = MkText(awayScoreGo, "0", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            // Possession dot away
            _awayPossessionDot = CreateRT(parent, "APoss").gameObject;
            var aple = _awayPossessionDot.AddComponent<LayoutElement>(); aple.preferredWidth = 10; aple.preferredHeight = 10;
            var apImg = _awayPossessionDot.AddComponent<Image>(); apImg.color = UITheme.AccentPrimary;
            _awayPossessionDot.SetActive(false);

            // Center info (quarter/clock)
            var center = CreateRT(parent, "Center"); center.gameObject.AddComponent<LayoutElement>().preferredWidth = 120;
            var cvlg = center.gameObject.AddComponent<VerticalLayoutGroup>();
            cvlg.childControlWidth = true; cvlg.childControlHeight = true; cvlg.childForceExpandWidth = true;
            _quarterText = MkText(center, "Q1", 14, FontStyle.Bold, UITheme.AccentPrimary, TextAnchor.MiddleCenter);
            _quarterText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            _clockText = MkText(center, "12:00", 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            _clockText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

            // Possession dot home
            _homePossessionDot = CreateRT(parent, "HPoss").gameObject;
            var hple = _homePossessionDot.AddComponent<LayoutElement>(); hple.preferredWidth = 10; hple.preferredHeight = 10;
            var hpImg = _homePossessionDot.AddComponent<Image>(); hpImg.color = UITheme.AccentPrimary;
            _homePossessionDot.SetActive(false);

            // Home score
            var homeScoreGo = CreateRT(parent, "HScore"); homeScoreGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 70;
            homeScoreGo.gameObject.AddComponent<Image>().color = UITheme.PanelSurface;
            _homeScoreText = MkText(homeScoreGo, "0", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            // Home team
            var homeBlock = CreateRT(parent, "Home"); homeBlock.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var homeHlg = homeBlock.gameObject.AddComponent<HorizontalLayoutGroup>();
            homeHlg.childControlWidth = true; homeHlg.childControlHeight = true; homeHlg.spacing = 8; homeHlg.childAlignment = TextAnchor.MiddleLeft;

            var homeInfo = CreateRT(homeBlock, "Info"); homeInfo.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
            var hivlg = homeInfo.gameObject.AddComponent<VerticalLayoutGroup>();
            hivlg.childControlWidth = true; hivlg.childControlHeight = true; hivlg.childForceExpandWidth = true;
            _homeTeamName = MkText(homeInfo, _homeTeam.Name, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            _homeTeamName.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

            var homeLogoGo = CreateRT(homeBlock, "Logo"); homeLogoGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 50;
            _homeLogo = homeLogoGo.gameObject.AddComponent<Image>(); _homeLogo.preserveAspect = true;
            sp = ArtManager.GetTeamLogo(_homeTeam.TeamId); if (sp != null) _homeLogo.sprite = sp;
        }

        private void BuildSpeedControls(RectTransform parent)
        {
            var hlg = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(16, 16, 2, 2); hlg.spacing = 6;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            _pauseBtn = MkCtrlBtn(parent, "||", 36, () => _simController?.PauseSimulation());
            _playBtn = MkCtrlBtn(parent, "\u25B6", 36, () => _simController?.StartSimulation());

            // Spacer
            CreateRT(parent, "Sp").gameObject.AddComponent<LayoutElement>().preferredWidth = 10;

            string[] labels = { "1x", "2x", "4x", "8x", "MAX" };
            SimulationSpeed[] speeds = { SimulationSpeed.Slow, SimulationSpeed.Normal, SimulationSpeed.Fast, SimulationSpeed.VeryFast, SimulationSpeed.Instant };
            _speedBtns = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                _speedBtns[i] = MkCtrlBtn(parent, labels[i], 50, () => {
                    _simController?.SetSpeed(speeds[idx]);
                    UpdateSpeedHighlight(idx);
                });
            }
            UpdateSpeedHighlight(1); // default Normal/2x

            // Spacer
            CreateRT(parent, "Sp2").gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Coaching buttons
            MkCtrlBtn(parent, "TIMEOUT", 80, () => _simController?.CallTimeout());
            MkCtrlBtn(parent, "SUBS", 60, ShowSubstitutionOverlay);
            MkCtrlBtn(parent, "STRATEGY", 80, ShowStrategyOverlay);
            _speedLabel = MkText(CreateRT(parent, "SpLbl"), "2x", 11, FontStyle.Normal, UITheme.TextSecondary, TextAnchor.MiddleCenter);
            _speedLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;
        }

        private void UpdateSpeedHighlight(int activeIdx)
        {
            string[] names = { "1x", "2x", "4x", "8x", "MAX" };
            for (int i = 0; i < _speedBtns.Length; i++)
            {
                var img = _speedBtns[i].GetComponent<Image>();
                img.color = i == activeIdx ? UITheme.AccentPrimary : UITheme.CardBackground;
            }
            if (_speedLabel != null) _speedLabel.text = names[activeIdx];
        }

        private void BuildPlayByPlay(RectTransform parent)
        {
            // Header
            var hdr = CreateRT(parent, "Hdr");
            hdr.anchorMin = new Vector2(0, 1); hdr.anchorMax = Vector2.one;
            hdr.pivot = new Vector2(0.5f, 1); hdr.sizeDelta = new Vector2(0, 24);
            hdr.gameObject.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var ht = MkText(hdr, "PLAY-BY-PLAY", 10, FontStyle.Bold, UITheme.AccentPrimary, TextAnchor.MiddleLeft);
            ht.GetComponent<RectTransform>().offsetMin = new Vector2(12, 0);

            // Scroll area
            var scrollGo = CreateRT(parent, "Scroll");
            scrollGo.anchorMin = Vector2.zero; scrollGo.anchorMax = Vector2.one;
            scrollGo.offsetMin = new Vector2(0, 0); scrollGo.offsetMax = new Vector2(0, -24);
            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;

            var vp = CreateRT(scrollGo, "VP"); Stretch(vp); vp.gameObject.AddComponent<RectMask2D>();
            var content = CreateRT(vp, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1; vlg.childControlWidth = true; vlg.childControlHeight = false; vlg.childForceExpandWidth = true;
            vlg.padding = new RectOffset(8, 8, 4, 4);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = vp;

            _playByPlayContent = content;
            _playByPlayScroll = scroll;
        }

        private void BuildGameEndOverlay()
        {
            _gameEndOverlay = CreateRT(_root, "GameEnd").gameObject; Stretch(_gameEndOverlay.GetComponent<RectTransform>());
            _gameEndOverlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);

            var center = CreateRT(_gameEndOverlay.GetComponent<RectTransform>(), "Center");
            center.anchorMin = new Vector2(0.25f, 0.3f); center.anchorMax = new Vector2(0.75f, 0.7f); center.sizeDelta = Vector2.zero;
            center.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
            UIBuilder.ApplyOutline(center.gameObject, UITheme.AccentPrimary, 2f);

            var vlg = center.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 10; vlg.padding = new RectOffset(30, 30, 20, 20);

            var tGo = CreateRT(center, "Title"); tGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            _gameEndTitle = MkText(tGo, "FINAL", 20, FontStyle.Bold, UITheme.AccentPrimary, TextAnchor.MiddleCenter);

            var sGo = CreateRT(center, "Score"); sGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
            _gameEndScore = MkText(sGo, "", 32, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            var rGo = CreateRT(center, "Result"); rGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            _gameEndResult = MkText(rGo, "", 18, FontStyle.Bold, UITheme.Success, TextAnchor.MiddleCenter);

            // Continue button
            var btnGo = CreateRT(center, "Continue"); btnGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            btnGo.gameObject.AddComponent<Image>().color = UITheme.DarkenColor(UITheme.Success, 0.5f);
            btnGo.gameObject.AddComponent<Button>().onClick.AddListener(OnContinue);
            var bt = MkText(btnGo, "CONTINUE", 14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            _gameEndOverlay.SetActive(false);
        }

        // ── EVENT SUBSCRIPTIONS ──

        private void SubscribeToEvents()
        {
            _simController.OnScoreboardUpdate += OnScoreboardUpdate;
            _simController.OnPlayByPlay += OnPlayByPlay;
            _simController.OnPossessionChange += OnPossessionChange;
            _simController.OnGameComplete += OnGameComplete;
            _simController.OnSpatialStateUpdate += OnSpatialStateUpdate;
            _simController.OnShotAttempt += OnShotAttempt;
        }

        private void OnScoreboardUpdate(ScoreboardUpdate update)
        {
            _homeScoreText.text = update.HomeScore.ToString();
            _awayScoreText.text = update.AwayScore.ToString();
            _quarterText.text = $"Q{update.Quarter}";
            int mins = (int)(update.Clock / 60);
            int secs = (int)(update.Clock % 60);
            _clockText.text = $"{mins}:{secs:D2}";
        }

        private void OnPlayByPlay(PlayByPlayEntry entry)
        {
            if (_playByPlayContent == null) return;

            var row = CreateRT(_playByPlayContent, "PBP");
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 15;
            row.gameObject.AddComponent<Image>().color = _pbpCount % 2 == 0 ? UITheme.PanelSurface : UITheme.CardBackground;

            Color textColor = entry.Type switch
            {
                PlayByPlayType.Score => UITheme.Success,
                PlayByPlayType.DefensivePlay => UITheme.AccentSecondary,
                PlayByPlayType.Turnover => new Color(1f, 0.6f, 0.2f),
                PlayByPlayType.Foul => new Color(0.9f, 0.3f, 0.3f),
                PlayByPlayType.GameEvent => UITheme.AccentPrimary,
                _ => Color.white
            };

            string clock = entry.Clock ?? "";
            string text = $"<color=#{ColorUtility.ToHtmlStringRGB(UITheme.TextSecondary)}>{clock}</color>  {entry.Description}";
            if (entry.IsHighlight) text = $"<b>{text}</b>";

            var t = MkText(row, text, 9, FontStyle.Normal, textColor, TextAnchor.MiddleLeft);
            t.supportRichText = true; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var trt = t.GetComponent<RectTransform>();
            trt.offsetMin = new Vector2(10, 0); trt.offsetMax = new Vector2(-10, 0);

            _pbpCount++;

            // Trim old entries
            while (_playByPlayContent.childCount > MAX_PBP)
                Destroy(_playByPlayContent.GetChild(0).gameObject);

            // Auto-scroll to bottom
            Canvas.ForceUpdateCanvases();
            _playByPlayScroll.verticalNormalizedPosition = 0f;
        }

        private void OnPossessionChange(bool homeHasBall)
        {
            _homePossessionDot.SetActive(homeHasBall);
            _awayPossessionDot.SetActive(!homeHasBall);
            _courtView?.SetOffense(homeHasBall);
        }

        private void OnSpatialStateUpdate(SpatialState state)
        {
            _courtView?.EnqueueState(state);
        }

        private void OnShotAttempt(ShotMarkerData data)
        {
            _courtView?.AddShotMarker(data);
        }

        private void OnGameComplete(BoxScore boxScore)
        {
            bool playerWon = (_playerIsHome && boxScore.HomeScore > boxScore.AwayScore) ||
                             (!_playerIsHome && boxScore.AwayScore > boxScore.HomeScore);

            _gameEndScore.text = $"{_awayTeam.Name} {boxScore.AwayScore}  —  {boxScore.HomeScore} {_homeTeam.Name}";
            _gameEndResult.text = playerWon ? "VICTORY" : "DEFEAT";
            _gameEndResult.color = playerWon ? UITheme.Success : new Color(0.9f, 0.3f, 0.3f);
            _gameEndOverlay.SetActive(true);

            // Record the game result
            var gm = GameManager.Instance;
            var mc = gm?.MatchController;
            if (mc != null)
            {
                mc.OnInteractiveMatchComplete(boxScore);
            }
        }

        private void OnContinue()
        {
            var gm = GameManager.Instance;
            gm?.MatchController?.ContinueFromPostGame();
        }

        // ── COACHING OVERLAYS ──

        private void DismissOverlay()
        {
            if (_coachingOverlay != null) Destroy(_coachingOverlay);
            _coachingOverlay = null;
            _simController?.StartSimulation();
        }

        private RectTransform CreateOverlay(string title)
        {
            _simController?.PauseSimulation();
            if (_coachingOverlay != null) Destroy(_coachingOverlay);

            _coachingOverlay = CreateRT(_root, "CoachOverlay").gameObject;
            Stretch(_coachingOverlay.GetComponent<RectTransform>());
            _coachingOverlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);

            var panel = CreateRT(_coachingOverlay.GetComponent<RectTransform>(), "Panel");
            panel.anchorMin = new Vector2(0.15f, 0.1f); panel.anchorMax = new Vector2(0.85f, 0.9f); panel.sizeDelta = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
            UIBuilder.ApplyOutline(panel.gameObject, UITheme.AccentPrimary, 1f);

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 4; vlg.padding = new RectOffset(16, 16, 8, 8);

            // Title bar
            var hdr = CreateRT(panel, "Hdr"); hdr.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            hdr.gameObject.AddComponent<Image>().color = UITheme.FMCardHeaderBg;
            var ht = MkText(hdr, title, 12, FontStyle.Bold, UITheme.AccentPrimary, TextAnchor.MiddleLeft);
            ht.GetComponent<RectTransform>().offsetMin = new Vector2(12, 0);

            // Close button in header
            var closeGo = CreateRT(hdr, "Close");
            var clrt = closeGo.GetComponent<RectTransform>();
            clrt.anchorMin = new Vector2(1, 0); clrt.anchorMax = Vector2.one;
            clrt.sizeDelta = new Vector2(60, 0); clrt.anchoredPosition = new Vector2(-30, 0);
            closeGo.gameObject.AddComponent<Image>().color = UITheme.PanelSurface;
            closeGo.gameObject.AddComponent<Button>().onClick.AddListener(DismissOverlay);
            var ct = MkText(closeGo, "CLOSE", 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            return panel;
        }

        private void ShowSubstitutionOverlay()
        {
            var panel = CreateOverlay("SUBSTITUTIONS");
            var gm = GameManager.Instance;
            var db = gm?.PlayerDatabase;
            if (_playerTeam == null || db == null) return;

            // Current lineup
            var lblOn = CreateRT(panel, "LblOn"); lblOn.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            lblOn.gameObject.AddComponent<Image>().color = Color.clear;
            MkText(lblOn, "ON COURT", 10, FontStyle.Bold, UITheme.AccentPrimary, TextAnchor.MiddleLeft);

            var currentIds = _playerTeam.StartingLineupIds;
            string[] posLabels = { "PG", "SG", "SF", "PF", "C" };
            string selectedOut = null;
            var onCourtBgs = new List<Image>();

            for (int i = 0; i < 5 && i < currentIds.Length; i++)
            {
                int posIdx = i;
                string pid = currentIds[i];
                var p = db.GetPlayer(pid);
                var row = CreateRT(panel, "On_" + i);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
                var bg = row.gameObject.AddComponent<Image>(); bg.color = UITheme.PanelSurface;
                onCourtBgs.Add(bg);
                var rhlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rhlg.childControlWidth = true; rhlg.childControlHeight = true; rhlg.spacing = 6;
                rhlg.childForceExpandHeight = true; rhlg.padding = new RectOffset(8, 8, 0, 0);

                var posGo = CreateRT(row, "Pos"); posGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 28;
                posGo.gameObject.AddComponent<Image>().color = UITheme.AccentPrimary;
                MkText(posGo, posLabels[i], 9, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

                var nameGo = CreateRT(row, "Name"); nameGo.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                nameGo.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
                string name = p != null ? $"{p.FirstName[0]}. {p.LastName}" : pid;
                MkText(nameGo, name, 11, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft).GetComponent<RectTransform>().offsetMin = new Vector2(6, 0);

                row.gameObject.AddComponent<Button>().onClick.AddListener(() => {
                    selectedOut = pid;
                    for (int j = 0; j < onCourtBgs.Count; j++) onCourtBgs[j].color = j == posIdx ? UITheme.FMNavActive : UITheme.PanelSurface;
                });
            }

            // Bench
            var lblBench = CreateRT(panel, "LblBench"); lblBench.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            lblBench.gameObject.AddComponent<Image>().color = Color.clear;
            MkText(lblBench, "BENCH — tap player above, then bench player to swap", 9, FontStyle.Normal, UITheme.TextSecondary, TextAnchor.MiddleLeft);

            var benchIds = _playerTeam.RosterPlayerIds?.Where(id => !currentIds.Contains(id)).ToList() ?? new List<string>();
            foreach (var bid in benchIds)
            {
                var bp = db.GetPlayer(bid);
                if (bp == null) continue;
                var brow = CreateRT(panel, "B_" + bid);
                brow.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
                brow.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
                string bname = $"{bp.FirstName[0]}. {bp.LastName}  ({bp.Position})";
                var bt = MkText(brow, bname, 10, FontStyle.Normal, UITheme.TextSecondary, TextAnchor.MiddleLeft);
                bt.GetComponent<RectTransform>().offsetMin = new Vector2(10, 0);

                string capturedBid = bid;
                brow.gameObject.AddComponent<Button>().onClick.AddListener(() => {
                    if (selectedOut == null) return;
                    _simController?.MakeSubstitution(selectedOut, capturedBid);
                    DismissOverlay();
                });
            }
        }

        private void ShowStrategyOverlay()
        {
            var panel = CreateOverlay("STRATEGY");
            if (_playerTeam?.Strategy == null) return;

            var strat = _playerTeam.Strategy;

            // Offense
            string[] offNames = Enum.GetNames(typeof(OffensiveSystemType));
            int offIdx = (int)(strat.OffensiveSystem?.PrimarySystem ?? OffensiveSystemType.MotionOffense);
            MkOverlayCycle(panel, "Offense", offNames, offIdx, v => { if (strat.OffensiveSystem != null) strat.OffensiveSystem.PrimarySystem = (OffensiveSystemType)v; });

            // Defense
            string[] defNames = Enum.GetNames(typeof(DefensiveSchemeType));
            int defIdx = (int)(strat.DefensiveSystem?.PrimaryScheme ?? DefensiveSchemeType.ManToManStandard);
            MkOverlayCycle(panel, "Defense", defNames, defIdx, v => { if (strat.DefensiveSystem != null) strat.DefensiveSystem.PrimaryScheme = (DefensiveSchemeType)v; });

            // Pace
            string[] paceNames = Enum.GetNames(typeof(PacePreference));
            int paceIdx = (int)strat.PacePreference;
            MkOverlayCycle(panel, "Pace", paceNames, paceIdx, v => strat.PacePreference = (PacePreference)v);
        }

        private void MkOverlayCycle(RectTransform parent, string label, string[] options, int currentIndex, Action<int> onChange)
        {
            var row = CreateRT(parent, "Pref_" + label);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            row.gameObject.AddComponent<Image>().color = UITheme.PanelSurface;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(12, 8, 0, 0);

            var lblGo = CreateRT(row, "Lbl"); lblGo.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            lblGo.gameObject.AddComponent<Image>().color = Color.clear;
            MkText(lblGo, label, 12, FontStyle.Bold, UITheme.TextSecondary, TextAnchor.MiddleLeft);

            int idx = Mathf.Clamp(currentIndex, 0, options.Length - 1);

            var leftGo = CreateRT(row, "L"); leftGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 30;
            leftGo.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
            MkText(leftGo, "<", 14, FontStyle.Bold, UITheme.AccentPrimary, TextAnchor.MiddleCenter);

            var valGo = CreateRT(row, "V"); valGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
            valGo.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
            var val = MkText(valGo, options[idx], 11, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);

            var rightGo = CreateRT(row, "R"); rightGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 30;
            rightGo.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
            MkText(rightGo, ">", 14, FontStyle.Bold, UITheme.AccentPrimary, TextAnchor.MiddleCenter);

            leftGo.gameObject.AddComponent<Button>().onClick.AddListener(() => { idx = (idx - 1 + options.Length) % options.Length; val.text = options[idx]; onChange(idx); });
            rightGo.gameObject.AddComponent<Button>().onClick.AddListener(() => { idx = (idx + 1) % options.Length; val.text = options[idx]; onChange(idx); });
        }

        private void OnDestroy()
        {
            if (_simController != null)
            {
                _simController.OnScoreboardUpdate -= OnScoreboardUpdate;
                _simController.OnPlayByPlay -= OnPlayByPlay;
                _simController.OnPossessionChange -= OnPossessionChange;
                _simController.OnGameComplete -= OnGameComplete;
                _simController.OnSpatialStateUpdate -= OnSpatialStateUpdate;
                _simController.OnShotAttempt -= OnShotAttempt;
            }
        }

        // ── HELPERS ──

        private static Color PickVisibleColor(Team team)
        {
            // Try primary first, fall back to secondary, then lighten if still too dark
            Color primary = UITheme.ParseTeamColor(team.PrimaryColor, Color.gray);
            Color secondary = UITheme.ParseTeamColor(team.SecondaryColor, Color.gray);

            // Use luminance to check brightness (0.3 threshold — wood court is ~0.4-0.5)
            float primLum = primary.r * 0.299f + primary.g * 0.587f + primary.b * 0.114f;
            float secLum = secondary.r * 0.299f + secondary.g * 0.587f + secondary.b * 0.114f;

            Color best = secLum > primLum ? secondary : primary; // pick brighter one
            float bestLum = Mathf.Max(primLum, secLum);

            // If still too dark, boost brightness
            if (bestLum < 0.35f)
            {
                float boost = 0.35f / Mathf.Max(bestLum, 0.05f);
                best = new Color(
                    Mathf.Min(best.r * boost + 0.15f, 1f),
                    Mathf.Min(best.g * boost + 0.15f, 1f),
                    Mathf.Min(best.b * boost + 0.15f, 1f));
            }
            return best;
        }

        private static RectTransform CreateRT(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
        }

        private static Text MkText(RectTransform parent, string content, int size, FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = content; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size; t.fontStyle = style; t.color = color;
            t.alignment = align; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private Button MkCtrlBtn(RectTransform parent, string label, float width, System.Action onClick)
        {
            var go = CreateRT(parent, $"Btn_{label}");
            go.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            go.gameObject.AddComponent<Image>().color = UITheme.CardBackground;
            var btn = go.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            MkText(go, label, 11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            return btn;
        }
    }
}
