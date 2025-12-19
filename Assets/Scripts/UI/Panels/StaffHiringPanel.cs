using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.UI.Components;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Staff hiring panel with one-at-a-time candidate profiles and contract negotiation.
    /// </summary>
    public class StaffHiringPanel : BasePanel
    {
        [Header("Position Selection")]
        [SerializeField] private Dropdown _positionDropdown;
        [SerializeField] private Text _availableBudgetText;
        [SerializeField] private Text _candidateCountText;

        [Header("Candidate Profile")]
        [SerializeField] private GameObject _candidateCard;
        [SerializeField] private Text _candidateNameText;
        [SerializeField] private Text _candidatePositionText;
        [SerializeField] private Text _candidateRatingText;
        [SerializeField] private Text _candidateAgeText;
        [SerializeField] private Text _candidateExperienceText;
        [SerializeField] private Text _candidateAskingPriceText;
        [SerializeField] private Transform _candidateAttributesContainer;

        [Header("Former Player Info")]
        [SerializeField] private GameObject _formerPlayerSection;
        [SerializeField] private Text _playingCareerSummaryText;
        [SerializeField] private Text _specializationsText;

        [Header("Negotiation")]
        [SerializeField] private InputField _offerSalaryInput;
        [SerializeField] private Dropdown _offerYearsDropdown;
        [SerializeField] private Button _makeOfferButton;
        [SerializeField] private Button _skipCandidateButton;
        [SerializeField] private Button _nextCandidateButton;
        [SerializeField] private Button _backButton;

        [Header("Negotiation Status")]
        [SerializeField] private GameObject _negotiationStatusPanel;
        [SerializeField] private Text _negotiationStatusText;
        [SerializeField] private Text _counterOfferText;
        [SerializeField] private Text _candidateResponseText;
        [SerializeField] private Button _acceptCounterButton;
        [SerializeField] private Button _newOfferButton;

        [Header("Success/Failure")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private Text _resultText;
        [SerializeField] private Button _continueButton;

        // State
        private StaffPositionType _currentPosition;
        private object _currentCandidate;  // Coach or Scout
        private StaffNegotiationSession _currentNegotiation;
        private bool _isCoach;

        // Events
        public event Action OnBackClicked;
        public event Action<object> OnStaffHired;

        protected override void Awake()
        {
            base.Awake();
            SetupControls();
        }

        private void SetupControls()
        {
            // Position dropdown
            if (_positionDropdown != null)
            {
                _positionDropdown.ClearOptions();
                _positionDropdown.AddOptions(new List<string>
                {
                    "Head Coach",
                    "Offensive Coordinator",
                    "Defensive Coordinator",
                    "Assistant Coach",
                    "Scout"
                });
                _positionDropdown.onValueChanged.AddListener(OnPositionChanged);
            }

            // Years dropdown
            if (_offerYearsDropdown != null)
            {
                _offerYearsDropdown.ClearOptions();
                _offerYearsDropdown.AddOptions(new List<string>
                {
                    "1 Year",
                    "2 Years",
                    "3 Years",
                    "4 Years",
                    "5 Years"
                });
            }

            // Buttons
            if (_makeOfferButton != null)
                _makeOfferButton.onClick.AddListener(OnMakeOfferClicked);

            if (_skipCandidateButton != null)
                _skipCandidateButton.onClick.AddListener(OnSkipClicked);

            if (_nextCandidateButton != null)
                _nextCandidateButton.onClick.AddListener(OnNextClicked);

            if (_backButton != null)
                _backButton.onClick.AddListener(OnBackClicked_Internal);

            if (_acceptCounterButton != null)
                _acceptCounterButton.onClick.AddListener(OnAcceptCounterClicked);

            if (_newOfferButton != null)
                _newOfferButton.onClick.AddListener(OnNewOfferClicked);

            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClicked);
        }

        protected override void OnShown()
        {
            base.OnShown();
            ResetUI();
            UpdateBudgetDisplay();
            ShowNextCandidate();
        }

        private void ResetUI()
        {
            if (_negotiationStatusPanel != null)
                _negotiationStatusPanel.SetActive(false);

            if (_resultPanel != null)
                _resultPanel.SetActive(false);

            if (_candidateCard != null)
                _candidateCard.SetActive(true);

            _currentNegotiation = null;
        }

        // ==================== POSITION SELECTION ====================

        private void OnPositionChanged(int index)
        {
            _currentPosition = index switch
            {
                0 => StaffPositionType.HeadCoach,
                1 => StaffPositionType.OffensiveCoordinator,
                2 => StaffPositionType.DefensiveCoordinator,
                3 => StaffPositionType.AssistantCoach,
                4 => StaffPositionType.Scout,
                _ => StaffPositionType.AssistantCoach
            };

            _isCoach = _currentPosition != StaffPositionType.Scout;

            // Reset browsing
            if (StaffHiringManager.Instance != null)
            {
                StaffHiringManager.Instance.StartBrowsingCandidates(_currentPosition);
            }

            ShowNextCandidate();
            UpdateCandidateCount();
        }

        private void UpdateBudgetDisplay()
        {
            // Get available budget from TeamFinances
            long availableBudget = 15_000_000;  // Default

            // TODO: Get actual remaining budget
            // var team = GameManager.Instance?.GetPlayerTeam();
            // if (team?.Finances != null)
            //     availableBudget = team.Finances.GetRemainingStaffBudget();

            if (_availableBudgetText != null)
                _availableBudgetText.text = $"Budget: ${availableBudget:N0}";
        }

        private void UpdateCandidateCount()
        {
            if (StaffHiringManager.Instance != null)
            {
                var counts = StaffHiringManager.Instance.GetPoolCounts();
                int count = _isCoach ? counts.coaches : counts.scouts;

                if (_candidateCountText != null)
                    _candidateCountText.text = $"Available: {count}";
            }
        }

        // ==================== CANDIDATE DISPLAY ====================

        private void ShowNextCandidate()
        {
            if (StaffHiringManager.Instance == null) return;

            if (_isCoach)
            {
                var coach = StaffHiringManager.Instance.GetNextCoachCandidate(_currentPosition);
                if (coach != null)
                {
                    _currentCandidate = coach;
                    DisplayCoachCandidate(coach);
                }
                else
                {
                    ShowNoCandidates();
                }
            }
            else
            {
                var scout = StaffHiringManager.Instance.GetNextScoutCandidate();
                if (scout != null)
                {
                    _currentCandidate = scout;
                    DisplayScoutCandidate(scout);
                }
                else
                {
                    ShowNoCandidates();
                }
            }
        }

        private void DisplayCoachCandidate(Coach coach)
        {
            if (_candidateCard != null)
                _candidateCard.SetActive(true);

            if (_candidateNameText != null)
                _candidateNameText.text = coach.FullName;

            if (_candidatePositionText != null)
                _candidatePositionText.text = coach.Position.ToString();

            if (_candidateRatingText != null)
            {
                _candidateRatingText.text = $"Rating: {coach.OverallRating}";
                AttributeDisplayFactory.ApplyRatingColor(_candidateRatingText, coach.OverallRating);
            }

            if (_candidateAgeText != null)
                _candidateAgeText.text = $"Age: {coach.Age}";

            if (_candidateExperienceText != null)
                _candidateExperienceText.text = $"Experience: {coach.ExperienceYears} years";

            if (_candidateAskingPriceText != null)
                _candidateAskingPriceText.text = $"Asking: ${coach.MarketValue:N0}/yr";

            // Former player section
            if (_formerPlayerSection != null)
            {
                _formerPlayerSection.SetActive(coach.IsFormerPlayer);
                if (coach.IsFormerPlayer && _playingCareerSummaryText != null)
                {
                    _playingCareerSummaryText.text = "Former NBA Player";
                }
            }

            // Specializations
            if (_specializationsText != null)
            {
                var specs = string.Join(", ", coach.Specializations);
                _specializationsText.text = string.IsNullOrEmpty(specs) ? "None" : specs;
            }

            // Display attributes
            DisplayCoachAttributes(coach);

            // Pre-fill offer with market value
            if (_offerSalaryInput != null)
                _offerSalaryInput.text = coach.MarketValue.ToString();
        }

        private void DisplayScoutCandidate(Scout scout)
        {
            if (_candidateCard != null)
                _candidateCard.SetActive(true);

            if (_candidateNameText != null)
                _candidateNameText.text = scout.FullName;

            if (_candidatePositionText != null)
                _candidatePositionText.text = "Scout";

            if (_candidateRatingText != null)
            {
                _candidateRatingText.text = $"Rating: {scout.OverallRating}";
                AttributeDisplayFactory.ApplyRatingColor(_candidateRatingText, scout.OverallRating);
            }

            if (_candidateAgeText != null)
                _candidateAgeText.text = $"Age: {scout.Age}";

            if (_candidateExperienceText != null)
                _candidateExperienceText.text = $"Experience: {scout.ExperienceYears} years";

            if (_candidateAskingPriceText != null)
                _candidateAskingPriceText.text = $"Asking: ${scout.MarketValue:N0}/yr";

            // Former player section
            if (_formerPlayerSection != null)
            {
                _formerPlayerSection.SetActive(scout.IsFormerPlayer);
            }

            // Specializations
            if (_specializationsText != null)
            {
                _specializationsText.text = $"{scout.PrimarySpecialization}, {scout.SecondarySpecialization}";
            }

            // Display attributes
            DisplayScoutAttributes(scout);

            // Pre-fill offer
            if (_offerSalaryInput != null)
                _offerSalaryInput.text = scout.MarketValue.ToString();
        }

        private void DisplayCoachAttributes(Coach coach)
        {
            if (_candidateAttributesContainer == null) return;

            var attrs = new Dictionary<string, int>
            {
                { "Offensive Scheme", coach.OffensiveScheme },
                { "Defensive Scheme", coach.DefensiveScheme },
                { "Game Management", coach.GameManagement },
                { "Player Dev", coach.PlayerDevelopment },
                { "Motivation", coach.Motivation }
            };

            AttributeDisplayFactory.PopulateAttributeContainer(_candidateAttributesContainer, attrs, 100f, 40f);
        }

        private void DisplayScoutAttributes(Scout scout)
        {
            if (_candidateAttributesContainer == null) return;

            var attrs = new Dictionary<string, int>
            {
                { "Evaluation", scout.EvaluationAccuracy },
                { "Prospects", scout.ProspectEvaluation },
                { "Pro Players", scout.ProEvaluation },
                { "Potential", scout.PotentialAssessment },
                { "Work Rate", scout.WorkRate }
            };

            AttributeDisplayFactory.PopulateAttributeContainer(_candidateAttributesContainer, attrs, 100f, 40f);
        }

        private void ShowNoCandidates()
        {
            if (_candidateCard != null)
                _candidateCard.SetActive(false);

            if (_candidateNameText != null)
                _candidateNameText.text = "No candidates available";

            _currentCandidate = null;
        }

        // ==================== NEGOTIATION ====================

        private void OnMakeOfferClicked()
        {
            if (_currentCandidate == null || StaffHiringManager.Instance == null) return;

            // Parse offer
            if (!int.TryParse(_offerSalaryInput?.text, out int salary))
            {
                ShowResult("Invalid salary amount", false);
                return;
            }

            int years = (_offerYearsDropdown?.value ?? 0) + 1;  // Dropdown is 0-indexed

            // Get team ID
            var teamId = "PLAYER_TEAM";  // TODO: Get from GameManager

            // Start or continue negotiation
            string candidateId = _isCoach
                ? (_currentCandidate as Coach)?.CoachId
                : (_currentCandidate as Scout)?.ScoutId;

            if (_currentNegotiation == null)
            {
                _currentNegotiation = StaffHiringManager.Instance.StartNegotiation(teamId, candidateId, _isCoach);
            }

            if (_currentNegotiation == null) return;

            // Make offer
            var response = StaffHiringManager.Instance.MakeOffer(_currentNegotiation.NegotiationId, salary, years);

            // Show response
            ShowNegotiationResponse(response);
        }

        private void ShowNegotiationResponse(NegotiationResponse response)
        {
            if (_negotiationStatusPanel != null)
                _negotiationStatusPanel.SetActive(true);

            if (_candidateResponseText != null)
                _candidateResponseText.text = response.Message;

            if (response.NewStatus == StaffNegotiationStatus.Accepted)
            {
                // Finalize hire
                var result = StaffHiringManager.Instance?.FinalizeHire(_currentNegotiation.NegotiationId);
                ShowResult(result?.message ?? "Hired!", true);
                OnStaffHired?.Invoke(_currentCandidate);
            }
            else if (response.NewStatus == StaffNegotiationStatus.Rejected)
            {
                ShowResult("Negotiations failed. The candidate walked away.", false);
                _currentNegotiation = null;
            }
            else if (response.NewStatus == StaffNegotiationStatus.CounterReceived)
            {
                // Show counter offer
                if (_counterOfferText != null)
                    _counterOfferText.text = $"Counter: ${response.CounterSalary:N0}/yr for {response.CounterYears} years";

                if (_acceptCounterButton != null)
                    _acceptCounterButton.gameObject.SetActive(true);

                if (_newOfferButton != null)
                    _newOfferButton.gameObject.SetActive(true);
            }

            if (_negotiationStatusText != null)
                _negotiationStatusText.text = $"Round {_currentNegotiation?.RoundNumber ?? 0} / {_currentNegotiation?.MaxRounds ?? 0}";
        }

        private void OnAcceptCounterClicked()
        {
            if (_currentNegotiation == null || StaffHiringManager.Instance == null) return;

            var result = StaffHiringManager.Instance.AcceptCounterOffer(_currentNegotiation.NegotiationId);

            if (result.success)
            {
                ShowResult(result.message, true);
                OnStaffHired?.Invoke(_currentCandidate);
            }
            else
            {
                ShowResult(result.message, false);
            }
        }

        private void OnNewOfferClicked()
        {
            // Hide counter, allow new offer
            if (_negotiationStatusPanel != null)
                _negotiationStatusPanel.SetActive(false);
        }

        private void ShowResult(string message, bool success)
        {
            if (_resultPanel != null)
                _resultPanel.SetActive(true);

            if (_negotiationStatusPanel != null)
                _negotiationStatusPanel.SetActive(false);

            if (_resultText != null)
            {
                _resultText.text = message;
                _resultText.color = success ? Color.green : Color.red;
            }
        }

        private void OnContinueClicked()
        {
            ResetUI();
            _currentNegotiation = null;
            StaffHiringManager.Instance?.SkipCandidate(_isCoach);
            ShowNextCandidate();
            UpdateCandidateCount();
        }

        // ==================== NAVIGATION ====================

        private void OnSkipClicked()
        {
            if (StaffHiringManager.Instance != null)
            {
                StaffHiringManager.Instance.SkipCandidate(_isCoach);
            }
            ShowNextCandidate();
        }

        private void OnNextClicked()
        {
            if (StaffHiringManager.Instance != null)
            {
                StaffHiringManager.Instance.SkipCandidate(_isCoach);
            }
            ShowNextCandidate();
        }

        private void OnBackClicked_Internal()
        {
            // Cancel any active negotiation
            if (_currentNegotiation != null && StaffHiringManager.Instance != null)
            {
                StaffHiringManager.Instance.CancelNegotiation(_currentNegotiation.NegotiationId);
            }

            OnBackClicked?.Invoke();
        }
    }
}
