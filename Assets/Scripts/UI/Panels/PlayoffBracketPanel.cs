using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Playoff bracket panel showing the tournament bracket, play-in, and series status.
    /// Displays both conferences side by side with NBA Finals in the center.
    /// </summary>
    public class PlayoffBracketPanel : BasePanel
    {
        #region UI References

        [Header("View Controls")]
        [SerializeField] private Button _playInTabButton;
        [SerializeField] private Button _bracketTabButton;
        [SerializeField] private Button _finalsTabButton;

        [Header("Panels")]
        [SerializeField] private GameObject _playInPanel;
        [SerializeField] private GameObject _bracketPanel;
        [SerializeField] private GameObject _finalsPanel;

        [Header("Play-In Tournament")]
        [SerializeField] private Transform _eastPlayInContainer;
        [SerializeField] private Transform _westPlayInContainer;
        [SerializeField] private GameObject _playInGamePrefab;

        [Header("Bracket Display")]
        [SerializeField] private Transform _eastBracketContainer;
        [SerializeField] private Transform _westBracketContainer;
        [SerializeField] private Transform _finalsContainer;
        [SerializeField] private GameObject _seriesCardPrefab;

        [Header("Series Detail")]
        [SerializeField] private GameObject _seriesDetailPanel;
        [SerializeField] private Text _seriesDetailTitle;
        [SerializeField] private Text _seriesDetailStatus;
        [SerializeField] private Transform _gamesListContainer;
        [SerializeField] private GameObject _gameResultPrefab;

        [Header("Finals Display")]
        [SerializeField] private Text _eastChampionText;
        [SerializeField] private Text _westChampionText;
        [SerializeField] private Text _finalsStatusText;
        [SerializeField] private Text _championText;
        [SerializeField] private Image _championLogo;

        [Header("Colors")]
        [SerializeField] private Color _playerTeamColor = new Color(0.2f, 0.5f, 0.8f);
        [SerializeField] private Color _activeSeriesColor = new Color(0.3f, 0.4f, 0.3f);
        [SerializeField] private Color _completedSeriesColor = new Color(0.2f, 0.2f, 0.25f);
        [SerializeField] private Color _eliminatedTeamColor = new Color(0.5f, 0.2f, 0.2f);

        #endregion

        #region State

        private PlayoffBracketView _currentView = PlayoffBracketView.Bracket;
        private PlayoffManager _playoffManager;
        private string _playerTeamId;
        private PlayoffSeries _selectedSeries;

        private List<PlayInGameUI> _playInGames = new List<PlayInGameUI>();
        private List<SeriesCardUI> _seriesCards = new List<SeriesCardUI>();

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            SetupButtons();
        }

        private void SetupButtons()
        {
            _playInTabButton?.onClick.AddListener(() => ShowView(PlayoffBracketView.PlayIn));
            _bracketTabButton?.onClick.AddListener(() => ShowView(PlayoffBracketView.Bracket));
            _finalsTabButton?.onClick.AddListener(() => ShowView(PlayoffBracketView.Finals));
        }

        protected override void OnShown()
        {
            base.OnShown();

            _playoffManager = PlayoffManager.Instance;
            _playerTeamId = GameManager.Instance?.PlayerTeamId ?? "";

            // Subscribe to playoff events
            if (_playoffManager != null)
            {
                _playoffManager.OnPlayInGameCompleted += OnPlayInGameCompleted;
                _playoffManager.OnPlayoffGameCompleted += OnPlayoffGameCompleted;
                _playoffManager.OnSeriesCompleted += OnSeriesCompleted;
                _playoffManager.OnPhaseChanged += OnPhaseChanged;
                _playoffManager.OnNBAChampion += OnChampionCrowned;
            }

            RefreshDisplay();
            ShowAppropriateView();
        }

        protected override void OnHidden()
        {
            base.OnHidden();

            // Unsubscribe from events
            if (_playoffManager != null)
            {
                _playoffManager.OnPlayInGameCompleted -= OnPlayInGameCompleted;
                _playoffManager.OnPlayoffGameCompleted -= OnPlayoffGameCompleted;
                _playoffManager.OnSeriesCompleted -= OnSeriesCompleted;
                _playoffManager.OnPhaseChanged -= OnPhaseChanged;
                _playoffManager.OnNBAChampion -= OnChampionCrowned;
            }
        }

        #endregion

        #region View Management

        private void ShowView(PlayoffBracketView view)
        {
            _currentView = view;

            _playInPanel?.SetActive(view == PlayoffBracketView.PlayIn);
            _bracketPanel?.SetActive(view == PlayoffBracketView.Bracket);
            _finalsPanel?.SetActive(view == PlayoffBracketView.Finals);

            UpdateTabButtonStates();
            RefreshCurrentView();
        }

        private void ShowAppropriateView()
        {
            if (_playoffManager == null)
            {
                ShowView(PlayoffBracketView.Bracket);
                return;
            }

            var phase = _playoffManager.CurrentPhase;
            var view = phase switch
            {
                PlayoffPhase.PlayIn => PlayoffBracketView.PlayIn,
                PlayoffPhase.Finals => PlayoffBracketView.Finals,
                PlayoffPhase.Complete => PlayoffBracketView.Finals,
                _ => PlayoffBracketView.Bracket
            };

            ShowView(view);
        }

        private void UpdateTabButtonStates()
        {
            // Highlight active tab (would update button visuals)
            // Implementation depends on UI toolkit being used
        }

        private void RefreshCurrentView()
        {
            switch (_currentView)
            {
                case PlayoffBracketView.PlayIn:
                    RefreshPlayIn();
                    break;
                case PlayoffBracketView.Bracket:
                    RefreshBracket();
                    break;
                case PlayoffBracketView.Finals:
                    RefreshFinals();
                    break;
            }
        }

        private void RefreshDisplay()
        {
            RefreshPlayIn();
            RefreshBracket();
            RefreshFinals();
        }

        #endregion

        #region Play-In Display

        private void RefreshPlayIn()
        {
            if (_playoffManager?.CurrentBracket?.PlayIn == null) return;

            var playIn = _playoffManager.CurrentBracket.PlayIn;

            // Clear existing UI
            ClearContainer(_eastPlayInContainer);
            ClearContainer(_westPlayInContainer);
            _playInGames.Clear();

            // Create East play-in games
            CreatePlayInGameUI(playIn.East.SevenVsEight, _eastPlayInContainer, "7 vs 8");
            CreatePlayInGameUI(playIn.East.NineVsTen, _eastPlayInContainer, "9 vs 10");
            CreatePlayInGameUI(playIn.East.EightSeedGame, _eastPlayInContainer, "8-Seed Game");

            // Create West play-in games
            CreatePlayInGameUI(playIn.West.SevenVsEight, _westPlayInContainer, "7 vs 8");
            CreatePlayInGameUI(playIn.West.NineVsTen, _westPlayInContainer, "9 vs 10");
            CreatePlayInGameUI(playIn.West.EightSeedGame, _westPlayInContainer, "8-Seed Game");
        }

        private void CreatePlayInGameUI(PlayInGame game, Transform container, string label)
        {
            if (game == null || container == null) return;

            // Create UI element (using prefab or dynamic creation)
            var uiObj = _playInGamePrefab != null
                ? Instantiate(_playInGamePrefab, container)
                : CreateDefaultPlayInGameUI(container);

            var ui = uiObj.GetComponent<PlayInGameUI>() ?? uiObj.AddComponent<PlayInGameUI>();
            ui.Initialize(game, label, GetTeamName, IsPlayerTeam);
            _playInGames.Add(ui);
        }

        private GameObject CreateDefaultPlayInGameUI(Transform parent)
        {
            var obj = new GameObject("PlayInGame");
            obj.transform.SetParent(parent, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 80);

            var layout = obj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 5;

            var bg = obj.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f);

            return obj;
        }

        #endregion

        #region Bracket Display

        private void RefreshBracket()
        {
            if (_playoffManager?.CurrentBracket == null) return;

            var bracket = _playoffManager.CurrentBracket;

            // Clear existing UI
            ClearContainer(_eastBracketContainer);
            ClearContainer(_westBracketContainer);
            ClearContainer(_finalsContainer);
            _seriesCards.Clear();

            // Create East bracket
            CreateRoundDisplay(bracket.Eastern.FirstRound, _eastBracketContainer, "First Round");
            CreateRoundDisplay(bracket.Eastern.ConferenceSemis, _eastBracketContainer, "Conf. Semis");
            if (bracket.Eastern.ConferenceFinals != null)
            {
                CreateSeriesCard(bracket.Eastern.ConferenceFinals, _eastBracketContainer);
            }

            // Create West bracket
            CreateRoundDisplay(bracket.Western.FirstRound, _westBracketContainer, "First Round");
            CreateRoundDisplay(bracket.Western.ConferenceSemis, _westBracketContainer, "Conf. Semis");
            if (bracket.Western.ConferenceFinals != null)
            {
                CreateSeriesCard(bracket.Western.ConferenceFinals, _westBracketContainer);
            }

            // Finals
            if (bracket.Finals != null)
            {
                CreateSeriesCard(bracket.Finals, _finalsContainer);
            }
        }

        private void CreateRoundDisplay(PlayoffSeries[] series, Transform container, string roundLabel)
        {
            if (series == null || container == null) return;

            foreach (var s in series)
            {
                if (s != null)
                {
                    CreateSeriesCard(s, container);
                }
            }
        }

        private void CreateSeriesCard(PlayoffSeries series, Transform container)
        {
            if (series == null || container == null) return;

            var uiObj = _seriesCardPrefab != null
                ? Instantiate(_seriesCardPrefab, container)
                : CreateDefaultSeriesCard(container);

            var ui = uiObj.GetComponent<SeriesCardUI>() ?? uiObj.AddComponent<SeriesCardUI>();
            ui.Initialize(series, GetTeamName, GetTeamAbbreviation, IsPlayerTeam);
            ui.OnClick += () => ShowSeriesDetail(series);
            _seriesCards.Add(ui);

            // Apply colors
            UpdateSeriesCardColor(ui, series);
        }

        private GameObject CreateDefaultSeriesCard(Transform parent)
        {
            var obj = new GameObject("SeriesCard");
            obj.transform.SetParent(parent, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 100);

            var bg = obj.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.18f, 0.23f);

            var button = obj.AddComponent<Button>();

            return obj;
        }

        private void UpdateSeriesCardColor(SeriesCardUI ui, PlayoffSeries series)
        {
            Color bgColor;

            if (series.HigherSeedTeamId == _playerTeamId || series.LowerSeedTeamId == _playerTeamId)
            {
                bgColor = _playerTeamColor;
            }
            else if (series.IsComplete)
            {
                bgColor = _completedSeriesColor;
            }
            else
            {
                bgColor = _activeSeriesColor;
            }

            ui.SetBackgroundColor(bgColor);
        }

        #endregion

        #region Finals Display

        private void RefreshFinals()
        {
            if (_playoffManager?.CurrentBracket == null) return;

            var bracket = _playoffManager.CurrentBracket;

            // East Champion
            var eastChamp = bracket.Eastern.ConferenceChampion;
            if (_eastChampionText != null)
            {
                _eastChampionText.text = string.IsNullOrEmpty(eastChamp)
                    ? "TBD"
                    : GetTeamName(eastChamp);
            }

            // West Champion
            var westChamp = bracket.Western.ConferenceChampion;
            if (_westChampionText != null)
            {
                _westChampionText.text = string.IsNullOrEmpty(westChamp)
                    ? "TBD"
                    : GetTeamName(westChamp);
            }

            // Finals Status
            if (_finalsStatusText != null)
            {
                if (bracket.Finals != null)
                {
                    _finalsStatusText.text = bracket.Finals.GetStatusString(GetTeamAbbreviation);
                }
                else
                {
                    _finalsStatusText.text = "Waiting for Conference Finals";
                }
            }

            // Champion
            if (_championText != null)
            {
                if (!string.IsNullOrEmpty(bracket.ChampionTeamId))
                {
                    _championText.text = $"{GetTeamName(bracket.ChampionTeamId)}\nNBA CHAMPIONS!";
                    _championText.gameObject.SetActive(true);
                }
                else
                {
                    _championText.gameObject.SetActive(false);
                }
            }
        }

        #endregion

        #region Series Detail

        private void ShowSeriesDetail(PlayoffSeries series)
        {
            if (series == null || _seriesDetailPanel == null) return;

            _selectedSeries = series;
            _seriesDetailPanel.SetActive(true);

            // Title
            if (_seriesDetailTitle != null)
            {
                string roundName = series.Round switch
                {
                    PlayoffRound.FirstRound => "First Round",
                    PlayoffRound.ConferenceSemis => "Conference Semifinals",
                    PlayoffRound.ConferenceFinals => $"{series.Conference} Conference Finals",
                    PlayoffRound.Finals => "NBA Finals",
                    _ => "Playoffs"
                };
                _seriesDetailTitle.text = $"{roundName}\n#{series.HigherSeed} {GetTeamName(series.HigherSeedTeamId)} vs #{series.LowerSeed} {GetTeamName(series.LowerSeedTeamId)}";
            }

            // Status
            if (_seriesDetailStatus != null)
            {
                _seriesDetailStatus.text = series.GetStatusString(GetTeamName);
            }

            // Games list
            RefreshGamesList(series);
        }

        private void RefreshGamesList(PlayoffSeries series)
        {
            if (_gamesListContainer == null) return;

            ClearContainer(_gamesListContainer);

            foreach (var game in series.Games.OrderBy(g => g.GameNumber))
            {
                CreateGameResultUI(game, series);
            }

            // Add upcoming game if series not complete
            if (!series.IsComplete)
            {
                CreateUpcomingGameUI(series);
            }
        }

        private void CreateGameResultUI(PlayoffGame game, PlayoffSeries series)
        {
            var obj = _gameResultPrefab != null
                ? Instantiate(_gameResultPrefab, _gamesListContainer)
                : CreateDefaultGameResultUI(_gamesListContainer);

            var text = obj.GetComponentInChildren<Text>();
            if (text != null)
            {
                string homeAbbr = GetTeamAbbreviation(game.HomeTeamId);
                string awayAbbr = GetTeamAbbreviation(game.AwayTeamId);

                if (game.IsComplete)
                {
                    string otText = game.WasOvertime ? $" (OT{(game.OvertimePeriods > 1 ? game.OvertimePeriods.ToString() : "")})" : "";
                    text.text = $"Game {game.GameNumber}: {awayAbbr} {game.AwayScore} @ {homeAbbr} {game.HomeScore}{otText}";
                }
                else
                {
                    text.text = $"Game {game.GameNumber}: {awayAbbr} @ {homeAbbr} - {game.Date:MMM dd}";
                }
            }
        }

        private void CreateUpcomingGameUI(PlayoffSeries series)
        {
            var obj = CreateDefaultGameResultUI(_gamesListContainer);
            var text = obj.GetComponentInChildren<Text>();
            if (text != null)
            {
                string homeTeam = GetTeamAbbreviation(series.GetHomeTeamForGame(series.NextGameNumber));
                string awayTeam = homeTeam == GetTeamAbbreviation(series.HigherSeedTeamId)
                    ? GetTeamAbbreviation(series.LowerSeedTeamId)
                    : GetTeamAbbreviation(series.HigherSeedTeamId);

                string gameLabel = series.IsEliminationGame() ? " (ELIMINATION)" : "";
                if (series.IsGame7()) gameLabel = " (GAME 7!)";

                text.text = $"Game {series.NextGameNumber}: {awayTeam} @ {homeTeam} - UPCOMING{gameLabel}";
                text.color = Color.yellow;
            }
        }

        private GameObject CreateDefaultGameResultUI(Transform parent)
        {
            var obj = new GameObject("GameResult");
            obj.transform.SetParent(parent, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 30);

            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;

            return obj;
        }

        public void HideSeriesDetail()
        {
            _seriesDetailPanel?.SetActive(false);
            _selectedSeries = null;
        }

        #endregion

        #region Event Handlers

        private void OnPlayInGameCompleted(PlayInGame game)
        {
            RefreshPlayIn();
        }

        private void OnPlayoffGameCompleted(PlayoffSeries series, PlayoffGame game)
        {
            RefreshBracket();

            // Update detail if viewing this series
            if (_selectedSeries == series)
            {
                RefreshGamesList(series);
            }
        }

        private void OnSeriesCompleted(PlayoffSeries series)
        {
            RefreshBracket();
            RefreshFinals();
        }

        private void OnPhaseChanged(PlayoffPhase phase)
        {
            ShowAppropriateView();
        }

        private void OnChampionCrowned(string championId, string runnerUpId)
        {
            RefreshFinals();
            ShowView(PlayoffBracketView.Finals);

            // Could show celebration animation here
            Debug.Log($"[PlayoffBracketPanel] NBA Champion: {GetTeamName(championId)}!");
        }

        #endregion

        #region Helpers

        private void ClearContainer(Transform container)
        {
            if (container == null) return;
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }

        private string GetTeamName(string teamId)
        {
            return GameManager.Instance?.GetTeam(teamId)?.Name ?? teamId;
        }

        private string GetTeamAbbreviation(string teamId)
        {
            return GameManager.Instance?.GetTeam(teamId)?.Abbreviation ?? teamId;
        }

        private bool IsPlayerTeam(string teamId)
        {
            return teamId == _playerTeamId;
        }

        #endregion
    }

    #region Enums & Helper Classes

    public enum PlayoffBracketView
    {
        PlayIn,
        Bracket,
        Finals
    }

    /// <summary>
    /// UI component for a play-in game display
    /// </summary>
    public class PlayInGameUI : MonoBehaviour
    {
        [SerializeField] private Text _labelText;
        [SerializeField] private Text _team1Text;
        [SerializeField] private Text _team2Text;
        [SerializeField] private Text _scoreText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Image _background;

        private PlayInGame _game;
        private Func<string, string> _getTeamName;
        private Func<string, bool> _isPlayerTeam;

        public void Initialize(PlayInGame game, string label, Func<string, string> getTeamName, Func<string, bool> isPlayerTeam)
        {
            _game = game;
            _getTeamName = getTeamName;
            _isPlayerTeam = isPlayerTeam;

            Refresh();
        }

        public void Refresh()
        {
            if (_game == null) return;

            if (_labelText != null)
                _labelText.text = _game.GameType.ToString().Replace("Vs", " vs ");

            if (_team1Text != null && !string.IsNullOrEmpty(_game.HigherSeedTeamId))
            {
                _team1Text.text = $"#{_game.HigherSeed} {_getTeamName?.Invoke(_game.HigherSeedTeamId) ?? _game.HigherSeedTeamId}";
                _team1Text.color = _isPlayerTeam?.Invoke(_game.HigherSeedTeamId) == true ? Color.cyan : Color.white;
            }

            if (_team2Text != null && !string.IsNullOrEmpty(_game.LowerSeedTeamId))
            {
                _team2Text.text = $"#{_game.LowerSeed} {_getTeamName?.Invoke(_game.LowerSeedTeamId) ?? _game.LowerSeedTeamId}";
                _team2Text.color = _isPlayerTeam?.Invoke(_game.LowerSeedTeamId) == true ? Color.cyan : Color.white;
            }

            if (_game.IsComplete)
            {
                if (_scoreText != null)
                    _scoreText.text = $"{_game.HigherSeedScore} - {_game.LowerSeedScore}";
                if (_statusText != null)
                    _statusText.text = $"Winner: {_getTeamName?.Invoke(_game.WinnerTeamId)}";
            }
            else
            {
                if (_scoreText != null)
                    _scoreText.text = "vs";
                if (_statusText != null)
                    _statusText.text = "Upcoming";
            }
        }
    }

    /// <summary>
    /// UI component for a playoff series card
    /// </summary>
    public class SeriesCardUI : MonoBehaviour
    {
        [SerializeField] private Text _higherSeedText;
        [SerializeField] private Text _lowerSeedText;
        [SerializeField] private Text _seriesScoreText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Image _background;
        [SerializeField] private Button _button;

        private PlayoffSeries _series;
        private Func<string, string> _getTeamName;
        private Func<string, string> _getTeamAbbr;
        private Func<string, bool> _isPlayerTeam;

        public event Action OnClick;

        public void Initialize(PlayoffSeries series, Func<string, string> getTeamName,
            Func<string, string> getTeamAbbr, Func<string, bool> isPlayerTeam)
        {
            _series = series;
            _getTeamName = getTeamName;
            _getTeamAbbr = getTeamAbbr;
            _isPlayerTeam = isPlayerTeam;

            if (_button == null)
                _button = GetComponent<Button>();

            _button?.onClick.AddListener(() => OnClick?.Invoke());

            Refresh();
        }

        public void Refresh()
        {
            if (_series == null) return;

            if (_higherSeedText != null)
            {
                string abbr = _getTeamAbbr?.Invoke(_series.HigherSeedTeamId) ?? _series.HigherSeedTeamId;
                _higherSeedText.text = $"({_series.HigherSeed}) {abbr}";
                _higherSeedText.fontStyle = _series.WinnerTeamId == _series.HigherSeedTeamId ? FontStyle.Bold : FontStyle.Normal;
            }

            if (_lowerSeedText != null)
            {
                string abbr = _getTeamAbbr?.Invoke(_series.LowerSeedTeamId) ?? _series.LowerSeedTeamId;
                _lowerSeedText.text = $"({_series.LowerSeed}) {abbr}";
                _lowerSeedText.fontStyle = _series.WinnerTeamId == _series.LowerSeedTeamId ? FontStyle.Bold : FontStyle.Normal;
            }

            if (_seriesScoreText != null)
            {
                _seriesScoreText.text = $"{_series.HigherSeedWins} - {_series.LowerSeedWins}";
            }

            if (_statusText != null)
            {
                _statusText.text = _series.IsComplete ? "FINAL" : $"Game {_series.NextGameNumber}";
            }
        }

        public void SetBackgroundColor(Color color)
        {
            if (_background != null)
                _background.color = color;
        }
    }

    #endregion
}
