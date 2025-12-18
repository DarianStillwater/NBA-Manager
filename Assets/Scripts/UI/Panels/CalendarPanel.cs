using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using CalendarEvent = NBAHeadCoach.Core.Data.CalendarEvent;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Calendar panel showing the season schedule with navigation
    /// </summary>
    public class CalendarPanel : BasePanel
    {
        [Header("Calendar Header")]
        [SerializeField] private Text _monthYearText;
        [SerializeField] private Button _prevMonthButton;
        [SerializeField] private Button _nextMonthButton;
        [SerializeField] private Text _currentDateText;

        [Header("Calendar Grid")]
        [SerializeField] private Transform _calendarGrid;
        [SerializeField] private Transform _dayLabelsContainer;
        [SerializeField] private GameObject _dayPrefab;

        [Header("Selected Day Info")]
        [SerializeField] private GameObject _dayInfoPanel;
        [SerializeField] private Text _selectedDateText;
        [SerializeField] private Text _eventTypeText;
        [SerializeField] private Text _opponentText;
        [SerializeField] private Text _gameTimeText;
        [SerializeField] private Text _gameResultText;
        [SerializeField] private Button _playGameButton;
        [SerializeField] private Button _simGameButton;

        [Header("Upcoming Games")]
        [SerializeField] private Transform _upcomingGamesContainer;
        [SerializeField] private int _upcomingGamesToShow = 5;

        [Header("Colors")]
        [SerializeField] private Color _homeGameColor = new Color(0.2f, 0.5f, 0.2f);
        [SerializeField] private Color _awayGameColor = new Color(0.5f, 0.3f, 0.2f);
        [SerializeField] private Color _completedGameColor = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private Color _todayColor = new Color(0.3f, 0.3f, 0.6f);
        [SerializeField] private Color _selectedColor = new Color(0.4f, 0.4f, 0.7f);
        [SerializeField] private Color _emptyDayColor = new Color(0.15f, 0.15f, 0.2f);

        // State
        private DateTime _displayMonth;
        private DateTime _selectedDate;
        private List<CalendarDayUI> _dayButtons = new List<CalendarDayUI>();
        private List<CalendarEvent> _schedule = new List<CalendarEvent>();

        // Events
        public event Action<CalendarEvent> OnPlayGameRequested;
        public event Action<CalendarEvent> OnSimGameRequested;

        protected override void Awake()
        {
            base.Awake();
            SetupButtons();
            CreateDayLabels();
        }

        private void SetupButtons()
        {
            if (_prevMonthButton != null)
                _prevMonthButton.onClick.AddListener(PreviousMonth);

            if (_nextMonthButton != null)
                _nextMonthButton.onClick.AddListener(NextMonth);

            if (_playGameButton != null)
                _playGameButton.onClick.AddListener(OnPlayClicked);

            if (_simGameButton != null)
                _simGameButton.onClick.AddListener(OnSimClicked);
        }

        private void CreateDayLabels()
        {
            if (_dayLabelsContainer == null) return;

            string[] days = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

            foreach (var day in days)
            {
                var labelGO = new GameObject(day);
                labelGO.transform.SetParent(_dayLabelsContainer, false);

                var text = labelGO.AddComponent<Text>();
                text.text = day;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.color = new Color(0.7f, 0.7f, 0.7f);
                text.alignment = TextAnchor.MiddleCenter;

                var layout = labelGO.AddComponent<LayoutElement>();
                layout.flexibleWidth = 1;
            }
        }

        protected override void OnShown()
        {
            base.OnShown();
            LoadSchedule();

            // Start at current date
            if (GameManager.Instance?.SeasonController != null)
            {
                _displayMonth = GameManager.Instance.SeasonController.CurrentDate;
                _selectedDate = _displayMonth;
            }
            else
            {
                _displayMonth = DateTime.Now;
                _selectedDate = _displayMonth;
            }

            RefreshCalendar();
            RefreshUpcomingGames();
            ClearDayInfo();
        }

        private void LoadSchedule()
        {
            _schedule.Clear();

            if (GameManager.Instance?.SeasonController != null)
            {
                _schedule = GameManager.Instance.SeasonController.Schedule ?? new List<CalendarEvent>();
            }
        }

        #region Calendar Navigation

        private void PreviousMonth()
        {
            _displayMonth = _displayMonth.AddMonths(-1);
            RefreshCalendar();
        }

        private void NextMonth()
        {
            _displayMonth = _displayMonth.AddMonths(1);
            RefreshCalendar();
        }

        private void RefreshCalendar()
        {
            // Update header
            if (_monthYearText != null)
            {
                _monthYearText.text = _displayMonth.ToString("MMMM yyyy");
            }

            if (_currentDateText != null && GameManager.Instance?.SeasonController != null)
            {
                var currentDate = GameManager.Instance.SeasonController.CurrentDate;
                _currentDateText.text = $"Today: {currentDate:MMM d, yyyy}";
            }

            // Clear existing day buttons
            foreach (var day in _dayButtons)
            {
                if (day != null) Destroy(day.gameObject);
            }
            _dayButtons.Clear();

            // Create calendar grid
            CreateCalendarGrid();
        }

        private void CreateCalendarGrid()
        {
            if (_calendarGrid == null) return;

            // Clear existing
            foreach (Transform child in _calendarGrid)
            {
                Destroy(child.gameObject);
            }

            // Get first day of month
            var firstDay = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
            int startDayOfWeek = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);

            // Get current date for highlighting
            DateTime currentDate = DateTime.Now;
            if (GameManager.Instance?.SeasonController != null)
            {
                currentDate = GameManager.Instance.SeasonController.CurrentDate;
            }

            // Add empty cells before first day
            for (int i = 0; i < startDayOfWeek; i++)
            {
                CreateEmptyDayCell();
            }

            // Add day cells
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_displayMonth.Year, _displayMonth.Month, day);
                CreateDayCell(date, currentDate);
            }

            // Fill remaining cells to complete the grid
            int totalCells = startDayOfWeek + daysInMonth;
            int remainingCells = (7 - (totalCells % 7)) % 7;
            for (int i = 0; i < remainingCells; i++)
            {
                CreateEmptyDayCell();
            }
        }

        private void CreateEmptyDayCell()
        {
            var cellGO = new GameObject("EmptyDay");
            cellGO.transform.SetParent(_calendarGrid, false);

            var image = cellGO.AddComponent<Image>();
            image.color = _emptyDayColor;

            var layout = cellGO.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            layout.flexibleHeight = 1;
        }

        private void CreateDayCell(DateTime date, DateTime currentDate)
        {
            GameObject cellGO;
            if (_dayPrefab != null)
            {
                cellGO = Instantiate(_dayPrefab, _calendarGrid);
            }
            else
            {
                cellGO = CreateDefaultDayCell(date);
            }

            var dayUI = cellGO.GetComponent<CalendarDayUI>();
            if (dayUI == null)
            {
                dayUI = cellGO.AddComponent<CalendarDayUI>();
            }

            // Find game on this date
            var gameEvent = _schedule.FirstOrDefault(e =>
                e.Date.Date == date.Date && e.Type == CalendarEventType.Game);

            // Determine colors
            Color bgColor = _emptyDayColor;
            bool isToday = date.Date == currentDate.Date;
            bool isSelected = date.Date == _selectedDate.Date;

            if (gameEvent != null)
            {
                if (gameEvent.IsCompleted)
                    bgColor = _completedGameColor;
                else if (gameEvent.IsHomeGame)
                    bgColor = _homeGameColor;
                else
                    bgColor = _awayGameColor;
            }

            if (isToday) bgColor = _todayColor;
            if (isSelected) bgColor = _selectedColor;

            dayUI.Setup(date, gameEvent, bgColor, isToday, OnDayClicked);
            _dayButtons.Add(dayUI);
        }

        private GameObject CreateDefaultDayCell(DateTime date)
        {
            var cellGO = new GameObject($"Day_{date.Day}");
            cellGO.transform.SetParent(_calendarGrid, false);

            var image = cellGO.AddComponent<Image>();
            var button = cellGO.AddComponent<Button>();

            var layout = cellGO.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            layout.flexibleHeight = 1;

            // Day number text
            var textGO = new GameObject("DayNumber");
            textGO.transform.SetParent(cellGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.text = date.Day.ToString();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(5, 5);
            textRect.offsetMax = new Vector2(-5, -5);

            return cellGO;
        }

        private void OnDayClicked(DateTime date, CalendarEvent gameEvent)
        {
            _selectedDate = date;
            RefreshCalendar(); // Update selection highlight

            if (gameEvent != null)
            {
                ShowDayInfo(date, gameEvent);
            }
            else
            {
                ShowDayInfo(date, null);
            }
        }

        #endregion

        #region Day Info Panel

        private void ClearDayInfo()
        {
            if (_dayInfoPanel != null)
                _dayInfoPanel.SetActive(false);
        }

        private void ShowDayInfo(DateTime date, CalendarEvent gameEvent)
        {
            if (_dayInfoPanel != null)
                _dayInfoPanel.SetActive(true);

            if (_selectedDateText != null)
                _selectedDateText.text = date.ToString("dddd, MMMM d, yyyy");

            if (gameEvent != null)
            {
                ShowGameInfo(gameEvent);
            }
            else
            {
                ShowOffDayInfo(date);
            }
        }

        private void ShowGameInfo(CalendarEvent game)
        {
            if (_eventTypeText != null)
                _eventTypeText.text = game.IsHomeGame ? "HOME GAME" : "AWAY GAME";

            // Get opponent name
            string playerTeamId = GameManager.Instance?.PlayerTeamId ?? "";
            string opponentId = game.IsHomeGame ? game.AwayTeamId : game.HomeTeamId;
            var opponent = GameManager.Instance?.GetTeam(opponentId);

            if (_opponentText != null)
            {
                string prefix = game.IsHomeGame ? "vs" : "@";
                _opponentText.text = $"{prefix} {opponent?.City ?? ""} {opponent?.Name ?? opponentId}";
            }

            if (_gameTimeText != null)
            {
                string broadcast = game.IsNationalTV ? $" ({game.BroadcastNetwork})" : "";
                _gameTimeText.text = $"Game #{game.GameNumber}{broadcast}";
            }

            // Show result or action buttons
            if (game.IsCompleted)
            {
                // Find result
                var result = GameManager.Instance?.SeasonController?.CompletedGames
                    .FirstOrDefault(r => r.GameId == game.EventId);

                if (_gameResultText != null)
                {
                    _gameResultText.gameObject.SetActive(true);
                    if (result != null)
                    {
                        bool won = (game.IsHomeGame && result.HomeScore > result.AwayScore) ||
                                   (!game.IsHomeGame && result.AwayScore > result.HomeScore);
                        string winLoss = won ? "W" : "L";
                        _gameResultText.text = $"Final: {result.AwayScore}-{result.HomeScore} ({winLoss})";
                        _gameResultText.color = won ? Color.green : Color.red;
                    }
                }

                if (_playGameButton != null) _playGameButton.gameObject.SetActive(false);
                if (_simGameButton != null) _simGameButton.gameObject.SetActive(false);
            }
            else
            {
                if (_gameResultText != null) _gameResultText.gameObject.SetActive(false);

                // Show buttons only if this is today's game or next game
                DateTime currentDate = GameManager.Instance?.SeasonController?.CurrentDate ?? DateTime.Now;
                bool canPlay = game.Date.Date == currentDate.Date;

                if (_playGameButton != null)
                {
                    _playGameButton.gameObject.SetActive(canPlay);
                }

                if (_simGameButton != null)
                {
                    _simGameButton.gameObject.SetActive(canPlay);
                }
            }
        }

        private void ShowOffDayInfo(DateTime date)
        {
            if (_eventTypeText != null)
                _eventTypeText.text = "OFF DAY";

            if (_opponentText != null)
                _opponentText.text = "No game scheduled";

            if (_gameTimeText != null)
                _gameTimeText.text = "";

            if (_gameResultText != null)
                _gameResultText.gameObject.SetActive(false);

            if (_playGameButton != null)
                _playGameButton.gameObject.SetActive(false);

            if (_simGameButton != null)
                _simGameButton.gameObject.SetActive(false);
        }

        #endregion

        #region Upcoming Games

        private void RefreshUpcomingGames()
        {
            if (_upcomingGamesContainer == null) return;

            // Clear existing
            foreach (Transform child in _upcomingGamesContainer)
            {
                Destroy(child.gameObject);
            }

            // Get upcoming games
            DateTime currentDate = GameManager.Instance?.SeasonController?.CurrentDate ?? DateTime.Now;
            var upcomingGames = _schedule
                .Where(e => e.Type == CalendarEventType.Game && !e.IsCompleted && e.Date >= currentDate)
                .OrderBy(e => e.Date)
                .Take(_upcomingGamesToShow)
                .ToList();

            foreach (var game in upcomingGames)
            {
                CreateUpcomingGameEntry(game);
            }
        }

        private void CreateUpcomingGameEntry(CalendarEvent game)
        {
            var entryGO = new GameObject($"Game_{game.EventId}");
            entryGO.transform.SetParent(_upcomingGamesContainer, false);

            var layout = entryGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;

            var layoutElement = entryGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 30;
            layoutElement.flexibleWidth = 1;

            // Date
            var dateGO = new GameObject("Date");
            dateGO.transform.SetParent(entryGO.transform, false);
            var dateText = dateGO.AddComponent<Text>();
            dateText.text = game.Date.ToString("MMM d");
            dateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dateText.fontSize = 14;
            dateText.color = new Color(0.7f, 0.7f, 0.7f);
            var dateLayout = dateGO.AddComponent<LayoutElement>();
            dateLayout.preferredWidth = 60;

            // Home/Away indicator
            var locationGO = new GameObject("Location");
            locationGO.transform.SetParent(entryGO.transform, false);
            var locationText = locationGO.AddComponent<Text>();
            locationText.text = game.IsHomeGame ? "vs" : "@";
            locationText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            locationText.fontSize = 14;
            locationText.color = game.IsHomeGame ? _homeGameColor : _awayGameColor;
            var locationLayout = locationGO.AddComponent<LayoutElement>();
            locationLayout.preferredWidth = 30;

            // Opponent
            string opponentId = game.IsHomeGame ? game.AwayTeamId : game.HomeTeamId;
            var opponent = GameManager.Instance?.GetTeam(opponentId);

            var oppGO = new GameObject("Opponent");
            oppGO.transform.SetParent(entryGO.transform, false);
            var oppText = oppGO.AddComponent<Text>();
            oppText.text = $"{opponent?.City ?? ""} {opponent?.Name ?? opponentId}";
            oppText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            oppText.fontSize = 14;
            oppText.color = Color.white;
            var oppLayout = oppGO.AddComponent<LayoutElement>();
            oppLayout.flexibleWidth = 1;
        }

        #endregion

        #region Actions

        private void OnPlayClicked()
        {
            var todaysGame = GetSelectedGame();
            if (todaysGame != null)
            {
                OnPlayGameRequested?.Invoke(todaysGame);
            }
        }

        private void OnSimClicked()
        {
            var todaysGame = GetSelectedGame();
            if (todaysGame != null)
            {
                OnSimGameRequested?.Invoke(todaysGame);
            }
        }

        private CalendarEvent GetSelectedGame()
        {
            return _schedule.FirstOrDefault(e =>
                e.Date.Date == _selectedDate.Date &&
                e.Type == CalendarEventType.Game &&
                !e.IsCompleted);
        }

        #endregion
    }

    /// <summary>
    /// UI component for individual calendar day cells
    /// </summary>
    public class CalendarDayUI : MonoBehaviour
    {
        public DateTime Date { get; private set; }
        public CalendarEvent GameEvent { get; private set; }

        private Button _button;
        private Image _background;
        private Text _dayText;
        private Text _eventText;
        private Action<DateTime, CalendarEvent> _onClick;

        public void Setup(DateTime date, CalendarEvent gameEvent, Color bgColor, bool isToday, Action<DateTime, CalendarEvent> onClick)
        {
            Date = date;
            GameEvent = gameEvent;
            _onClick = onClick;

            _button = GetComponent<Button>();
            _background = GetComponent<Image>();

            if (_background != null)
                _background.color = bgColor;

            if (_button != null)
                _button.onClick.AddListener(() => _onClick?.Invoke(Date, GameEvent));

            // Find or create text elements
            _dayText = GetComponentInChildren<Text>();
            if (_dayText != null)
            {
                _dayText.text = date.Day.ToString();
                if (isToday) _dayText.fontStyle = FontStyle.Bold;
            }

            // Add game indicator if there's a game
            if (gameEvent != null)
            {
                AddGameIndicator(gameEvent);
            }
        }

        private void AddGameIndicator(CalendarEvent game)
        {
            var indicatorGO = new GameObject("GameIndicator");
            indicatorGO.transform.SetParent(transform, false);

            var text = indicatorGO.AddComponent<Text>();
            text.text = game.IsHomeGame ? "H" : "A";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 10;
            text.color = Color.white;
            text.alignment = TextAnchor.LowerRight;

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2, 2);
            rect.offsetMax = new Vector2(-2, -2);
        }
    }
}
