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
        private UnifiedCareerProfile _currentCandidate;
        private StaffNegotiationSession _currentNegotiation;
        private bool _isCoach;
        private int _candidateIndex = 0;
        private List<UnifiedCareerProfile> _currentPool = new List<UnifiedCareerProfile>();

        // Events
        public event Action OnBackClicked;
        public event Action<UnifiedCareerProfile> OnStaffHired;

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

            _isCoach = _currentPosition.IsCoachingPosition();
            _candidateIndex = 0;

            // Get candidates from PersonnelManager
            var personnelManager = PersonnelManager.Instance;
            if (personnelManager != null)
            {
                var role = _currentPosition.ToUnifiedRole();
                _currentPool = personnelManager.GetFreeAgentsForPosition(role);
            }

            ShowNextCandidate();
            UpdateCandidateCount();
        }

        private void UpdateBudgetDisplay()
        {
            // Get available budget from TeamFinances
            long availableBudget = 15_000_000;  // Default

            var teamId = GameManager.Instance?.PlayerTeamId;
            var finances = GameManager.Instance?.FinanceManager?.GetTeamFinances(teamId);
            
            if (finances != null)
            {
                availableBudget = finances.GetRemainingStaffBudget();
            }

            if (_availableBudgetText != null)
                _availableBudgetText.text = $"Budget: ${availableBudget:N0}";
        }

        private void UpdateCandidateCount()
        {
            if (_candidateCountText != null)
                _candidateCountText.text = $"Available: {_currentPool.Count}";
        }

        // ==================== CANDIDATE DISPLAY ====================

        private void ShowNextCandidate()
        {
            if (_candidateIndex < _currentPool.Count)
            {
                _currentCandidate = _currentPool[_candidateIndex];
                DisplayCandidate(_currentCandidate);
            }
            else
            {
                ShowNoCandidates();
            }
        }

        private void DisplayCandidate(UnifiedCareerProfile profile)
        {
            if (_candidateCard != null)
                _candidateCard.SetActive(true);

            if (_candidateNameText != null)
                _candidateNameText.text = profile.PersonName;

            if (_candidatePositionText != null)
                _candidatePositionText.text = profile.CurrentRole.ToString();

            if (_candidateRatingText != null)
            {
                _candidateRatingText.text = $"Rating: {profile.OverallRating}";
                AttributeDisplayFactory.ApplyRatingColor(_candidateRatingText, profile.OverallRating);
            }

            if (_candidateAgeText != null)
                _candidateAgeText.text = $"Age: {profile.CurrentAge}";

            if (_candidateExperienceText != null)
            {
                int exp = profile.CurrentTrack == UnifiedCareerTrack.Coaching ? profile.TotalCoachingYears : profile.TotalFrontOfficeYears;
                _candidateExperienceText.text = $"Experience: {exp} years";
            }

            if (_candidateAskingPriceText != null)
                _candidateAskingPriceText.text = $"Asking: ${profile.MarketValue:N0}/yr";

            // Former player section
            if (_formerPlayerSection != null)
            {
                _formerPlayerSection.SetActive(profile.IsFormerPlayer);
                if (profile.IsFormerPlayer && _playingCareerSummaryText != null)
                {
                    _playingCareerSummaryText.text = "Former NBA Player";
                }
            }

            // Specializations
            if (_specializationsText != null)
            {
                var specs = profile.CurrentTrack == UnifiedCareerTrack.Coaching 
                    ? string.Join(", ", profile.Specializations) 
                    : string.Join(", ", profile.ScoutingSpecializations);
                _specializationsText.text = string.IsNullOrEmpty(specs) ? "None" : specs;
            }

            // Display attributes
            DisplayAttributes(profile);

            // Pre-fill offer with market value
            if (_offerSalaryInput != null)
                _offerSalaryInput.text = profile.MarketValue.ToString();
        }

        private void DisplayAttributes(UnifiedCareerProfile profile)
        {
            if (_candidateAttributesContainer == null) return;

            var attrs = new Dictionary<string, int>();
            if (profile.CurrentTrack == UnifiedCareerTrack.Coaching)
            {
                attrs.Add("Offensive Scheme", profile.OffensiveScheme);
                attrs.Add("Defensive Scheme", profile.DefensiveScheme);
                attrs.Add("Game Management", profile.GameManagement);
                attrs.Add("Player Dev", profile.PlayerDevelopment);
                attrs.Add("Motivation", profile.Motivation);
            }
            else
            {
                attrs.Add("Evaluation", profile.EvaluationAccuracy);
                attrs.Add("Prospects", profile.ProspectEvaluation);
                attrs.Add("Pro Players", profile.ProEvaluation);
                attrs.Add("Potential", profile.PotentialAssessment);
                attrs.Add("Work Rate", profile.WorkRate);
            }

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
            if (_currentCandidate == null || PersonnelManager.Instance == null) return;

            // Parse offer
            if (!int.TryParse(_offerSalaryInput?.text, out int salary))
            {
                ShowResult("Invalid salary amount", false);
                return;
            }

            int years = (_offerYearsDropdown?.value ?? 0) + 1;  // Dropdown is 0-indexed

            // Get team ID
            var teamId = GameManager.Instance?.PlayerTeamId ?? "PLAYER_TEAM";

            if (_currentNegotiation == null)
            {
                _currentNegotiation = PersonnelManager.Instance.StartNegotiation("USER", _currentCandidate.ProfileId, teamId);
            }

            if (_currentNegotiation == null) return;

            // Make offer
            var response = PersonnelManager.Instance.MakeOffer(_currentNegotiation.SessionId, salary, years);

            // Show response
            ShowNegotiationResponse(response);
        }

        private void ShowNegotiationResponse(NegotiationResponse response)
        {
            if (_negotiationStatusPanel != null)
                _negotiationStatusPanel.SetActive(true);

            if (_candidateResponseText != null)
                _candidateResponseText.text = response.Message;

            if (response.Result == NegotiationResult.Accepted)
            {
                // Finalize hire
                PersonnelManager.Instance?.FinalizeNegotiation(_currentNegotiation.SessionId, true);
                ShowResult("Hired!", true);
                OnStaffHired?.Invoke(_currentCandidate);
            }
            else if (response.Result == NegotiationResult.Rejected)
            {
                ShowResult("Negotiations failed. The candidate walked away.", false);
                _currentNegotiation = null;
            }
            else if (response.Result == NegotiationResult.Countered)
            {
                // Show counter offer
                if (_counterOfferText != null)
                    _counterOfferText.text = $"Counter: ${response.CounterOffer.Salary:N0}/yr for {response.CounterOffer.Years} years";

                if (_acceptCounterButton != null)
                    _acceptCounterButton.gameObject.SetActive(true);

                if (_newOfferButton != null)
                    _newOfferButton.gameObject.SetActive(true);
            }

            if (_negotiationStatusText != null)
                _negotiationStatusText.text = $"Round {_currentNegotiation?.Steps?.Count ?? 0} / 3";
        }

        private void OnAcceptCounterClicked()
        {
            if (_currentNegotiation == null || PersonnelManager.Instance == null) return;

            PersonnelManager.Instance.FinalizeNegotiation(_currentNegotiation.SessionId, true);
            ShowResult("Hired!", true);
            OnStaffHired?.Invoke(_currentCandidate);
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
            _candidateIndex++;
            ShowNextCandidate();
            UpdateCandidateCount();
        }

        // ==================== NAVIGATION ====================

        private void OnSkipClicked()
        {
            _candidateIndex++;
            ShowNextCandidate();
        }

        private void OnNextClicked()
        {
            _candidateIndex++;
            ShowNextCandidate();
        }

        private void OnBackClicked_Internal()
        {
            // Cancel any active negotiation
            if (_currentNegotiation != null && PersonnelManager.Instance != null)
            {
                PersonnelManager.Instance.FinalizeNegotiation(_currentNegotiation.SessionId, false);
            }

            OnBackClicked?.Invoke();
        }
    }
}
