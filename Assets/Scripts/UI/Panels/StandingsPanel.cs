using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// League standings panel showing conference/division standings
    /// </summary>
    public class StandingsPanel : BasePanel
    {
        [Header("View Controls")]
        [SerializeField] private Dropdown _viewModeDropdown;
        [SerializeField] private Toggle _eastToggle;
        [SerializeField] private Toggle _westToggle;

        [Header("Standings Tables")]
        [SerializeField] private Transform _eastStandingsContainer;
        [SerializeField] private Transform _westStandingsContainer;
        [SerializeField] private GameObject _standingsRowPrefab;

        [Header("Table Headers")]
        [SerializeField] private Transform _eastHeaderContainer;
        [SerializeField] private Transform _westHeaderContainer;

        [Header("Player Team Highlight")]
        [SerializeField] private Color _playerTeamColor = new Color(0.2f, 0.4f, 0.6f);
        [SerializeField] private Color _normalRowColor = new Color(0.15f, 0.15f, 0.2f);
        [SerializeField] private Color _altRowColor = new Color(0.18f, 0.18f, 0.23f);
        [SerializeField] private Color _playoffLineColor = new Color(0.4f, 0.4f, 0.2f);

        [Header("Playoff Line")]
        [SerializeField] private int _playoffSpots = 10; // Play-in tournament includes 7-10

        // State
        private StandingsViewMode _currentViewMode = StandingsViewMode.Conference;
        private List<StandingsRowUI> _eastRows = new List<StandingsRowUI>();
        private List<StandingsRowUI> _westRows = new List<StandingsRowUI>();
        private string _playerTeamId;

        protected override void Awake()
        {
            base.Awake();
            SetupControls();
        }

        private void SetupControls()
        {
            if (_viewModeDropdown != null)
            {
                _viewModeDropdown.ClearOptions();
                _viewModeDropdown.AddOptions(new List<string>
                {
                    "Conference",
                    "Division",
                    "League"
                });
                _viewModeDropdown.onValueChanged.AddListener(OnViewModeChanged);
            }

            if (_eastToggle != null)
            {
                _eastToggle.isOn = true;
                _eastToggle.onValueChanged.AddListener(OnEastToggleChanged);
            }

            if (_westToggle != null)
            {
                _westToggle.isOn = true;
                _westToggle.onValueChanged.AddListener(OnWestToggleChanged);
            }
        }

        protected override void OnShown()
        {
            base.OnShown();

            _playerTeamId = GameManager.Instance?.PlayerTeamId ?? "";
            RefreshStandings();
        }

        private void OnViewModeChanged(int index)
        {
            _currentViewMode = (StandingsViewMode)index;
            RefreshStandings();
        }

        private void OnEastToggleChanged(bool isOn)
        {
            if (_eastStandingsContainer != null)
                _eastStandingsContainer.gameObject.SetActive(isOn);
        }

        private void OnWestToggleChanged(bool isOn)
        {
            if (_westStandingsContainer != null)
                _westStandingsContainer.gameObject.SetActive(isOn);
        }

        #region Standings Display

        private void RefreshStandings()
        {
            ClearStandings();
            CreateHeaders();

            switch (_currentViewMode)
            {
                case StandingsViewMode.Conference:
                    ShowConferenceStandings();
                    break;
                case StandingsViewMode.Division:
                    ShowDivisionStandings();
                    break;
                case StandingsViewMode.League:
                    ShowLeagueStandings();
                    break;
            }
        }

        private void ClearStandings()
        {
            foreach (var row in _eastRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _eastRows.Clear();

            foreach (var row in _westRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _westRows.Clear();
        }

        private void CreateHeaders()
        {
            CreateHeaderRow(_eastHeaderContainer);
            CreateHeaderRow(_westHeaderContainer);
        }

        private void CreateHeaderRow(Transform container)
        {
            if (container == null) return;

            // Clear existing
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }

            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(container, false);

            var layout = headerGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;

            var layoutElement = headerGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 25;
            layoutElement.flexibleWidth = 1;

            // Add header columns
            AddHeaderColumn(headerGO.transform, "#", 30);
            AddHeaderColumn(headerGO.transform, "Team", 180);
            AddHeaderColumn(headerGO.transform, "W", 35);
            AddHeaderColumn(headerGO.transform, "L", 35);
            AddHeaderColumn(headerGO.transform, "PCT", 50);
            AddHeaderColumn(headerGO.transform, "GB", 40);
            AddHeaderColumn(headerGO.transform, "L10", 50);
            AddHeaderColumn(headerGO.transform, "STRK", 45);
        }

        private void AddHeaderColumn(Transform parent, string text, float width)
        {
            var colGO = new GameObject(text);
            colGO.transform.SetParent(parent, false);

            var colText = colGO.AddComponent<Text>();
            colText.text = text;
            colText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            colText.fontSize = 12;
            colText.fontStyle = FontStyle.Bold;
            colText.color = new Color(0.7f, 0.7f, 0.7f);
            colText.alignment = text == "Team" ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;

            var layout = colGO.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
        }

        private void ShowConferenceStandings()
        {
            var seasonController = GameManager.Instance?.SeasonController;
            if (seasonController == null) return;

            // Eastern Conference
            var eastStandings = seasonController.GetConferenceStandings("Eastern");
            ShowStandingsTable(eastStandings, _eastStandingsContainer, _eastRows, "Eastern Conference");

            // Western Conference
            var westStandings = seasonController.GetConferenceStandings("Western");
            ShowStandingsTable(westStandings, _westStandingsContainer, _westRows, "Western Conference");
        }

        private void ShowDivisionStandings()
        {
            // Would show 6 divisions grouped
            // For now, fall back to conference
            ShowConferenceStandings();
        }

        private void ShowLeagueStandings()
        {
            var seasonController = GameManager.Instance?.SeasonController;
            if (seasonController == null) return;

            // Combine both conferences
            var eastStandings = seasonController.GetConferenceStandings("Eastern");
            var westStandings = seasonController.GetConferenceStandings("Western");

            var allStandings = eastStandings.Concat(westStandings)
                .OrderByDescending(s => s.WinPercentage)
                .ThenByDescending(s => s.Wins)
                .ToList();

            // Recalculate positions
            for (int i = 0; i < allStandings.Count; i++)
            {
                allStandings[i].GamesBehind = i == 0 ? 0 :
                    ((allStandings[0].Wins - allStandings[i].Wins) +
                     (allStandings[i].Losses - allStandings[0].Losses)) / 2f;
            }

            // Show in east container only
            ShowStandingsTable(allStandings, _eastStandingsContainer, _eastRows, "League Standings");

            // Hide west container
            if (_westStandingsContainer != null)
                _westStandingsContainer.gameObject.SetActive(false);
        }

        private void ShowStandingsTable(List<TeamStandings> standings, Transform container,
            List<StandingsRowUI> rowList, string title)
        {
            if (container == null || standings == null) return;

            // Clear existing rows in container
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }

            // Calculate games behind leader
            if (standings.Count > 0)
            {
                var leader = standings[0];
                foreach (var team in standings)
                {
                    team.GamesBehind = ((leader.Wins - team.Wins) + (team.Losses - leader.Losses)) / 2f;
                }
            }

            // Create rows
            for (int i = 0; i < standings.Count; i++)
            {
                var row = CreateStandingsRow(standings[i], i + 1, container);
                rowList.Add(row);
            }
        }

        private StandingsRowUI CreateStandingsRow(TeamStandings standings, int rank, Transform container)
        {
            GameObject rowGO;
            if (_standingsRowPrefab != null)
            {
                rowGO = Instantiate(_standingsRowPrefab, container);
            }
            else
            {
                rowGO = CreateDefaultStandingsRow(container);
            }

            var rowUI = rowGO.GetComponent<StandingsRowUI>();
            if (rowUI == null)
            {
                rowUI = rowGO.AddComponent<StandingsRowUI>();
            }

            // Determine row color
            Color rowColor;
            bool isPlayerTeam = standings.TeamId == _playerTeamId;

            if (isPlayerTeam)
            {
                rowColor = _playerTeamColor;
            }
            else if (rank == _playoffSpots) // Play-in line
            {
                rowColor = _playoffLineColor;
            }
            else
            {
                rowColor = rank % 2 == 0 ? _altRowColor : _normalRowColor;
            }

            // Get additional team data
            var team = GameManager.Instance?.GetTeam(standings.TeamId);

            rowUI.Setup(standings, rank, rowColor, isPlayerTeam, team);

            return rowUI;
        }

        private GameObject CreateDefaultStandingsRow(Transform container)
        {
            var rowGO = new GameObject("StandingsRow");
            rowGO.transform.SetParent(container, false);

            var bg = rowGO.AddComponent<Image>();
            bg.color = _normalRowColor;

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(5, 5, 2, 2);

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 28;
            layoutElement.flexibleWidth = 1;

            return rowGO;
        }

        #endregion

        #region Enums

        private enum StandingsViewMode
        {
            Conference,
            Division,
            League
        }

        #endregion
    }

    /// <summary>
    /// UI component for individual standings rows
    /// </summary>
    public class StandingsRowUI : MonoBehaviour
    {
        public TeamStandings Standings { get; private set; }
        public int Rank { get; private set; }

        private Image _background;
        private List<Text> _columnTexts = new List<Text>();

        public void Setup(TeamStandings standings, int rank, Color bgColor, bool isPlayerTeam, Team team)
        {
            Standings = standings;
            Rank = rank;

            _background = GetComponent<Image>();
            if (_background != null)
            {
                _background.color = bgColor;
            }

            // Clear existing columns
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            _columnTexts.Clear();

            // Add columns
            AddColumn(rank.ToString(), 30, TextAnchor.MiddleCenter);
            AddColumn($"{team?.City ?? ""} {standings.TeamName}", 180, TextAnchor.MiddleLeft, isPlayerTeam);
            AddColumn(standings.Wins.ToString(), 35, TextAnchor.MiddleCenter);
            AddColumn(standings.Losses.ToString(), 35, TextAnchor.MiddleCenter);
            AddColumn(standings.WinPercentage.ToString(".000"), 50, TextAnchor.MiddleCenter);
            AddColumn(standings.GamesBehind == 0 ? "-" : standings.GamesBehind.ToString("0.0"), 40, TextAnchor.MiddleCenter);
            AddColumn(GetLast10String(standings), 50, TextAnchor.MiddleCenter);
            AddColumn(GetStreakString(standings), 45, TextAnchor.MiddleCenter);
        }

        private void AddColumn(string text, float width, TextAnchor alignment, bool bold = false)
        {
            var colGO = new GameObject("Col");
            colGO.transform.SetParent(transform, false);

            var colText = colGO.AddComponent<Text>();
            colText.text = text;
            colText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            colText.fontSize = 13;
            colText.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            colText.color = Color.white;
            colText.alignment = alignment;

            var layout = colGO.AddComponent<LayoutElement>();
            layout.preferredWidth = width;

            _columnTexts.Add(colText);
        }

        private string GetLast10String(TeamStandings standings)
        {
            // Would track last 10 games
            return $"{standings.Last10Wins}-{standings.Last10Losses}";
        }

        private string GetStreakString(TeamStandings standings)
        {
            if (standings.Streak == 0) return "-";
            return standings.Streak > 0 ? $"W{standings.Streak}" : $"L{Math.Abs(standings.Streak)}";
        }
    }
}
