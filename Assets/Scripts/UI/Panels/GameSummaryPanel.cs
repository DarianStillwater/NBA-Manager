using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Displays game summary for GM-Only mode.
    /// Shows box score, key moments, and coach performance after autonomous game simulation.
    /// </summary>
    public class GameSummaryPanel : BasePanel
    {
        [Header("Game Info")]
        [SerializeField] private Text _gameInfoText;
        [SerializeField] private Text _finalScoreText;
        [SerializeField] private Text _quarterScoresText;
        [SerializeField] private Text _outcomeText;

        [Header("Coach Performance")]
        [SerializeField] private Text _coachNameText;
        [SerializeField] private Text _coachRatingText;
        [SerializeField] private Text _coachAssessmentText;
        [SerializeField] private Transform _coachDecisionsContainer;
        [SerializeField] private Text _coachPostGameText;

        [Header("Box Score")]
        [SerializeField] private Transform _userBoxScoreContainer;
        [SerializeField] private Transform _opponentBoxScoreContainer;
        [SerializeField] private GameObject _boxScoreRowPrefab;
        [SerializeField] private Text _userTeamNameText;
        [SerializeField] private Text _opponentTeamNameText;

        [Header("Team Stats")]
        [SerializeField] private Text _userTeamStatsText;
        [SerializeField] private Text _opponentTeamStatsText;

        [Header("Key Moments")]
        [SerializeField] private Transform _keyMomentsContainer;
        [SerializeField] private GameObject _momentRowPrefab;

        [Header("Tabs")]
        [SerializeField] private Button _boxScoreTabButton;
        [SerializeField] private Button _coachingTabButton;
        [SerializeField] private Button _momentsTabButton;
        [SerializeField] private GameObject _boxScorePanel;
        [SerializeField] private GameObject _coachingPanel;
        [SerializeField] private GameObject _momentsPanel;

        [Header("Navigation")]
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _viewCoachProfileButton;

        // Current data
        private AutonomousGameResult _currentResult;
        private string _userTeamId;
        private string _opponentTeamId;

        public override void Initialize()
        {
            base.Initialize();

            // Setup tabs
            if (_boxScoreTabButton != null)
                _boxScoreTabButton.onClick.AddListener(() => ShowTab("boxscore"));
            if (_coachingTabButton != null)
                _coachingTabButton.onClick.AddListener(() => ShowTab("coaching"));
            if (_momentsTabButton != null)
                _momentsTabButton.onClick.AddListener(() => ShowTab("moments"));

            // Setup navigation
            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClicked);
            if (_viewCoachProfileButton != null)
                _viewCoachProfileButton.onClick.AddListener(OnViewCoachProfileClicked);

            // Default to box score tab
            ShowTab("boxscore");
        }

        /// <summary>
        /// Display the game result summary.
        /// </summary>
        public void DisplayResult(AutonomousGameResult result, string userTeamId, string userTeamName, string opponentTeamName)
        {
            _currentResult = result;
            _userTeamId = userTeamId;
            _opponentTeamId = result.WasHomeGame ? result.AwayTeamId : result.HomeTeamId;

            UpdateGameInfo(result, userTeamName, opponentTeamName);
            UpdateCoachPerformance(result);
            UpdateBoxScores(result, userTeamName, opponentTeamName);
            UpdateTeamStats(result);
            UpdateKeyMoments(result);

            ShowTab("boxscore");
        }

        private void UpdateGameInfo(AutonomousGameResult result, string userTeamName, string opponentTeamName)
        {
            // Game info
            if (_gameInfoText != null)
            {
                string location = result.WasHomeGame ? "vs" : "@";
                _gameInfoText.text = $"{userTeamName} {location} {opponentTeamName}\n{result.GameDate:MMM d, yyyy}";
            }

            // Final score
            if (_finalScoreText != null)
            {
                _finalScoreText.text = $"{result.UserTeamScore} - {result.OpponentScore}";
                _finalScoreText.color = result.UserTeamWon ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.8f, 0.3f, 0.3f);
            }

            // Quarter scores
            if (_quarterScoresText != null)
            {
                var userQ = result.WasHomeGame ? result.HomeQuarterScores : result.AwayQuarterScores;
                var oppQ = result.WasHomeGame ? result.AwayQuarterScores : result.HomeQuarterScores;

                string quarters = "Q1: " + userQ[0] + "-" + oppQ[0] +
                                 " | Q2: " + userQ[1] + "-" + oppQ[1] +
                                 " | Q3: " + userQ[2] + "-" + oppQ[2] +
                                 " | Q4: " + userQ[3] + "-" + oppQ[3];

                if (result.WasOvertime)
                    quarters += $" | OT x{result.OvertimePeriods}";

                _quarterScoresText.text = quarters;
            }

            // Outcome
            if (_outcomeText != null)
            {
                string outcome = result.UserTeamWon ? "VICTORY" : "DEFEAT";
                int margin = Math.Abs(result.UserTeamScore - result.OpponentScore);
                _outcomeText.text = $"{outcome} ({(result.UserTeamWon ? "+" : "-")}{margin})";
                _outcomeText.color = result.UserTeamWon ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.8f, 0.3f, 0.3f);
            }
        }

        private void UpdateCoachPerformance(AutonomousGameResult result)
        {
            var perf = result.CoachPerformance;

            if (_coachNameText != null)
                _coachNameText.text = result.CoachName;

            if (_coachRatingText != null)
            {
                _coachRatingText.text = $"Performance: {perf.OverallRating}/10";
                _coachRatingText.color = perf.OverallRating >= 7 ? new Color(0.2f, 0.7f, 0.2f) :
                                         perf.OverallRating >= 4 ? Color.white :
                                         new Color(0.8f, 0.3f, 0.3f);
            }

            if (_coachAssessmentText != null)
            {
                string assessment = perf.OverallAssessment + "\n\n";
                assessment += $"Timeouts: {perf.TimeoutsCalled} used, {perf.TimeoutsRemaining} remaining\n";
                assessment += $"Substitutions: {perf.SubstitutionsMade}\n";
                assessment += $"Adjustments: {perf.AdjustmentsMade}\n\n";
                assessment += perf.RotationSummary + "\n";
                assessment += perf.StarMinutesComment;

                // Add positives
                if (perf.PositiveAspects.Count > 0)
                {
                    assessment += "\n\nPositives:";
                    foreach (var pos in perf.PositiveAspects)
                        assessment += $"\n  + {pos}";
                }

                // Add concerns
                if (perf.AreasOfConcern.Count > 0)
                {
                    assessment += "\n\nConcerns:";
                    foreach (var concern in perf.AreasOfConcern)
                        assessment += $"\n  - {concern}";
                }

                _coachAssessmentText.text = assessment;
            }

            // Decisions
            if (_coachDecisionsContainer != null)
            {
                ClearContainer(_coachDecisionsContainer);
                foreach (var decision in perf.NotableDecisions)
                {
                    CreateDecisionRow(decision);
                }
            }

            if (_coachPostGameText != null)
            {
                _coachPostGameText.text = $"\"{result.CoachPostGameComment}\"";
            }
        }

        private void UpdateBoxScores(AutonomousGameResult result, string userTeamName, string opponentTeamName)
        {
            if (_userTeamNameText != null)
                _userTeamNameText.text = userTeamName;
            if (_opponentTeamNameText != null)
                _opponentTeamNameText.text = opponentTeamName;

            // User team box score
            if (_userBoxScoreContainer != null)
            {
                ClearContainer(_userBoxScoreContainer);
                foreach (var player in result.GetUserTeamBoxScore())
                {
                    if (player.MinutesPlayed > 0)
                        CreateBoxScoreRow(_userBoxScoreContainer, player);
                }
            }

            // Opponent box score
            if (_opponentBoxScoreContainer != null)
            {
                ClearContainer(_opponentBoxScoreContainer);
                foreach (var player in result.GetOpponentBoxScore())
                {
                    if (player.MinutesPlayed > 0)
                        CreateBoxScoreRow(_opponentBoxScoreContainer, player);
                }
            }
        }

        private void UpdateTeamStats(AutonomousGameResult result)
        {
            var userStats = result.GetUserTeamStats();
            var oppStats = result.GetOpponentTeamStats();

            if (_userTeamStatsText != null)
            {
                _userTeamStatsText.text = FormatTeamStats(userStats);
            }

            if (_opponentTeamStatsText != null)
            {
                _opponentTeamStatsText.text = FormatTeamStats(oppStats);
            }
        }

        private string FormatTeamStats(TeamBoxScore stats)
        {
            return $"FG: {stats.FGM}/{stats.FGA} ({stats.FGPct:F1}%)\n" +
                   $"3PT: {stats.ThreePM}/{stats.ThreePA} ({stats.ThreePct:F1}%)\n" +
                   $"FT: {stats.FTM}/{stats.FTA} ({stats.FTPct:F1}%)\n" +
                   $"REB: {stats.TotalRebounds} (O:{stats.ORB} D:{stats.DRB})\n" +
                   $"AST: {stats.Assists}\n" +
                   $"TO: {stats.Turnovers}\n" +
                   $"STL: {stats.Steals}\n" +
                   $"BLK: {stats.Blocks}\n\n" +
                   $"Paint: {stats.PointsInPaint}\n" +
                   $"Fast Break: {stats.FastBreakPoints}\n" +
                   $"Bench: {stats.BenchPoints}";
        }

        private void UpdateKeyMoments(AutonomousGameResult result)
        {
            if (_keyMomentsContainer == null) return;

            ClearContainer(_keyMomentsContainer);

            foreach (var moment in result.KeyMoments)
            {
                CreateMomentRow(moment);
            }

            // Add game narrative at the bottom
            if (!string.IsNullOrEmpty(result.GameNarrative))
            {
                var narrativeGO = new GameObject("Narrative");
                narrativeGO.transform.SetParent(_keyMomentsContainer, false);
                var text = narrativeGO.AddComponent<Text>();
                text.text = $"\n{result.GameNarrative}";
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.color = new Color(0.7f, 0.7f, 0.7f);
            }
        }

        private void CreateBoxScoreRow(Transform container, PlayerBoxScore player)
        {
            GameObject rowGO;

            if (_boxScoreRowPrefab != null)
            {
                rowGO = Instantiate(_boxScoreRowPrefab, container);
            }
            else
            {
                rowGO = CreateSimpleBoxScoreRow(player);
                rowGO.transform.SetParent(container, false);
            }

            var text = rowGO.GetComponentInChildren<Text>();
            if (text != null)
            {
                string starter = player.Started ? "*" : " ";
                string star = player.WasGameStar ? " [STAR]" : "";
                text.text = $"{starter}{player.PlayerName,-18} {player.MinutesPlayed,3} {player.Points,3} " +
                           $"{player.FGM}-{player.FGA} {player.ThreePM}-{player.ThreePA} " +
                           $"{player.FTM}-{player.FTA} {player.TotalRebounds,2} {player.Assists,2} " +
                           $"{player.Steals,2} {player.Blocks,2} {player.Turnovers,2} {player.PlusMinus,+3}{star}";
            }
        }

        private GameObject CreateSimpleBoxScoreRow(PlayerBoxScore player)
        {
            var rowGO = new GameObject($"Row_{player.PlayerName}");

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 24;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(rowGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.color = player.WasGameStar ? new Color(1f, 0.9f, 0.3f) : Color.white;

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return rowGO;
        }

        private void CreateDecisionRow(CoachDecision decision)
        {
            var rowGO = new GameObject($"Decision_{decision.Quarter}");
            rowGO.transform.SetParent(_coachDecisionsContainer, false);

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 40;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(rowGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.text = $"Q{decision.Quarter} {decision.GameClock}: {decision.Description}\n  Outcome: {decision.Outcome}";
            text.color = decision.Quality switch
            {
                CoachDecision.DecisionQuality.Excellent => new Color(0.2f, 0.8f, 0.2f),
                CoachDecision.DecisionQuality.Good => new Color(0.5f, 0.8f, 0.5f),
                CoachDecision.DecisionQuality.Questionable => new Color(0.8f, 0.6f, 0.2f),
                CoachDecision.DecisionQuality.Poor => new Color(0.8f, 0.3f, 0.3f),
                _ => Color.white
            };

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void CreateMomentRow(GameMoment moment)
        {
            var rowGO = new GameObject($"Moment_{moment.Quarter}_{moment.GameClock}");
            rowGO.transform.SetParent(_keyMomentsContainer, false);

            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 30;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(rowGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.text = $"Q{moment.Quarter} {moment.GameClock} - {moment.Description}";
            text.color = moment.Type switch
            {
                GameMoment.MomentType.ClutchPlay => new Color(1f, 0.9f, 0.3f),
                GameMoment.MomentType.BigPlay => new Color(0.5f, 0.8f, 1f),
                GameMoment.MomentType.CoachingAdjustment => new Color(0.7f, 0.5f, 1f),
                _ => Color.white
            };

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ShowTab(string tabName)
        {
            // Hide all panels
            SetActive(_boxScorePanel, false);
            SetActive(_coachingPanel, false);
            SetActive(_momentsPanel, false);

            // Show selected panel
            switch (tabName.ToLower())
            {
                case "boxscore":
                    SetActive(_boxScorePanel, true);
                    break;
                case "coaching":
                    SetActive(_coachingPanel, true);
                    break;
                case "moments":
                    SetActive(_momentsPanel, true);
                    break;
            }
        }

        private void OnContinueClicked()
        {
            Hide();
            // Return to calendar/dashboard
            GameManager.Instance?.ChangeState(GameState.Playing);
        }

        private void OnViewCoachProfileClicked()
        {
            // TODO: Open coach profile panel
            Debug.Log("View coach profile clicked");
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }

        private void SetActive(GameObject go, bool active)
        {
            if (go != null)
                go.SetActive(active);
        }
    }
}
