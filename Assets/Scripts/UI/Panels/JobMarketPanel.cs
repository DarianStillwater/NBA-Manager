using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// UI panel for the job market when user is unemployed.
    /// Shows available positions, applications, and offers.
    /// </summary>
    public class JobMarketPanel : BasePanel
    {
        [Header("Firing Details (shown when first fired)")]
        [SerializeField] private GameObject _firingDetailsSection;
        [SerializeField] private Text _firingTitleText;
        [SerializeField] private Text _firingReasonText;
        [SerializeField] private Text _severanceText;
        [SerializeField] private Button _acknowledgeFiringButton;

        [Header("Market Summary")]
        [SerializeField] private Text _marketSummaryText;
        [SerializeField] private Text _unemployedDaysText;
        [SerializeField] private Text _applicationsText;

        [Header("Filter")]
        [SerializeField] private Dropdown _roleFilterDropdown;
        [SerializeField] private Toggle _showFilledToggle;

        [Header("Job Listings")]
        [SerializeField] private Transform _jobListContainer;
        [SerializeField] private GameObject _jobListingPrefab;

        [Header("Job Details (Right Panel)")]
        [SerializeField] private GameObject _jobDetailsPanel;
        [SerializeField] private Text _teamNameText;
        [SerializeField] private Text _positionText;
        [SerializeField] private Text _salaryText;
        [SerializeField] private Text _contractText;
        [SerializeField] private Text _teamRecordText;
        [SerializeField] private Text _situationText;
        [SerializeField] private Text _requirementsText;
        [SerializeField] private Text _descriptionText;
        [SerializeField] private Text _competitionText;

        [Header("Application Section")]
        [SerializeField] private InputField _coverLetterInput;
        [SerializeField] private Button _applyButton;
        [SerializeField] private Text _applicationStatusText;
        [SerializeField] private Button _acceptOfferButton;
        [SerializeField] private Button _declineOfferButton;

        [Header("Your Profile")]
        [SerializeField] private Text _profileNameText;
        [SerializeField] private Text _profileReputationText;
        [SerializeField] private Text _profileExperienceText;
        [SerializeField] private Text _knownTraitsText;

        // Current selection
        private JobOpening _selectedJob;
        private FiringDetails _firingDetails;
        private UserRole? _roleFilter;

        public override void Initialize()
        {
            base.Initialize();

            SetupButtons();
            SetupDropdown();

            if (_firingDetailsSection != null)
                _firingDetailsSection.SetActive(false);

            if (_jobDetailsPanel != null)
                _jobDetailsPanel.SetActive(false);
        }

        protected override void OnShown()
        {
            base.OnShown();
            RefreshData();
        }

        private void SetupButtons()
        {
            if (_acknowledgeFiringButton != null)
                _acknowledgeFiringButton.onClick.AddListener(OnAcknowledgeFiring);

            if (_applyButton != null)
                _applyButton.onClick.AddListener(OnApplyClicked);

            if (_acceptOfferButton != null)
                _acceptOfferButton.onClick.AddListener(OnAcceptOffer);

            if (_declineOfferButton != null)
                _declineOfferButton.onClick.AddListener(OnDeclineOffer);
        }

        private void SetupDropdown()
        {
            if (_roleFilterDropdown != null)
            {
                _roleFilterDropdown.ClearOptions();
                _roleFilterDropdown.AddOptions(new List<string>
                {
                    "All Positions",
                    "Head Coach Only",
                    "General Manager Only"
                });
                _roleFilterDropdown.onValueChanged.AddListener(OnFilterChanged);
            }
        }

        /// <summary>
        /// Show the panel after being fired.
        /// </summary>
        public void ShowAfterFiring(FiringDetails firing)
        {
            _firingDetails = firing;

            if (_firingDetailsSection != null)
            {
                _firingDetailsSection.SetActive(true);

                if (_firingTitleText != null)
                    _firingTitleText.text = $"RELEASED BY {firing.TeamName.ToUpper()}";

                if (_firingReasonText != null)
                {
                    _firingReasonText.text = $"Reason: {firing.GetReasonDisplay()}\n\n" +
                                            $"\"{firing.PublicStatement}\"";
                }

                if (_severanceText != null)
                {
                    _severanceText.text = firing.SeveranceAmount > 0
                        ? $"Severance: ${firing.SeveranceAmount / 1000000f:F1}M ({firing.SeveranceMonths} months)"
                        : "No severance package";
                }
            }

            Show();
        }

        private void OnAcknowledgeFiring()
        {
            if (_firingDetailsSection != null)
                _firingDetailsSection.SetActive(false);

            RefreshData();
        }

        private void RefreshData()
        {
            var jobManager = JobMarketManager.Instance;
            if (jobManager == null)
            {
                Debug.LogWarning("[JobMarketPanel] JobMarketManager not found");
                return;
            }

            UpdateMarketSummary(jobManager);
            UpdateProfile();
            RefreshJobListings(jobManager);
        }

        private void UpdateMarketSummary(JobMarketManager manager)
        {
            var summary = manager.GetMarketSummary();
            var state = manager.MarketState;

            if (_marketSummaryText != null)
            {
                _marketSummaryText.text = $"Market Status: {summary.MarketStatus}\n" +
                                         $"Open Positions: {summary.TotalOpenings} " +
                                         $"({summary.CoachOpenings} HC, {summary.GMOpenings} GM)";
            }

            if (_unemployedDaysText != null && state.UnemployedSince.HasValue)
            {
                int days = (DateTime.Now - state.UnemployedSince.Value).Days;
                _unemployedDaysText.text = $"Days unemployed: {days}";
            }

            if (_applicationsText != null)
            {
                _applicationsText.text = $"Applications: {state.TotalApplications} sent, " +
                                        $"{summary.PendingApplications} pending, " +
                                        $"{summary.ActiveOffers} offers";
            }
        }

        private void UpdateProfile()
        {
            var career = GameManager.Instance?.Career;
            if (career == null) return;

            if (_profileNameText != null)
                _profileNameText.text = career.FullName;

            if (_profileReputationText != null)
                _profileReputationText.text = $"Reputation: {career.Reputation}/100";

            if (_profileExperienceText != null)
                _profileExperienceText.text = $"Experience: {career.TotalSeasons} seasons";
        }

        private void RefreshJobListings(JobMarketManager manager)
        {
            // Clear existing
            if (_jobListContainer != null)
            {
                for (int i = _jobListContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(_jobListContainer.GetChild(i).gameObject);
                }
            }

            // Get filtered openings
            var openings = manager.MarketState.GetOpenings(_roleFilter);

            // Create listing rows
            foreach (var opening in openings)
            {
                CreateJobListingRow(opening);
            }
        }

        private void CreateJobListingRow(JobOpening opening)
        {
            if (_jobListContainer == null) return;

            var rowGO = new GameObject($"Job_{opening.TeamId}");
            rowGO.transform.SetParent(_jobListContainer, false);

            // Layout
            var layoutElement = rowGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 60;
            layoutElement.flexibleWidth = 1;

            // Background button
            var button = rowGO.AddComponent<Button>();
            var image = rowGO.AddComponent<Image>();
            image.color = opening.UserApplication != null
                ? new Color(0.2f, 0.3f, 0.4f)  // Applied
                : new Color(0.15f, 0.15f, 0.2f);

            button.onClick.AddListener(() => SelectJob(opening));

            // Text content
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(rowGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleLeft;

            string status = "";
            if (opening.UserApplication != null)
            {
                status = opening.UserApplication.HasOffer ? " [OFFER]" :
                        opening.UserApplication.Status == JobApplicationStatus.Interviewing ? " [INTERVIEW]" :
                        opening.UserApplication.Status == JobApplicationStatus.Pending ? " [APPLIED]" :
                        opening.UserApplication.Status == JobApplicationStatus.Rejected ? " [REJECTED]" : "";
            }

            text.text = $"{opening.TeamCity} {opening.TeamName}\n" +
                       $"{opening.GetPositionTitle()} - {opening.GetSalaryDisplay()}{status}";

            text.color = opening.UserApplication?.HasOffer == true
                ? new Color(0.3f, 0.9f, 0.3f)
                : Color.white;

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);
        }

        private void SelectJob(JobOpening opening)
        {
            _selectedJob = opening;

            if (_jobDetailsPanel != null)
                _jobDetailsPanel.SetActive(true);

            UpdateJobDetails(opening);
        }

        private void UpdateJobDetails(JobOpening opening)
        {
            if (_teamNameText != null)
                _teamNameText.text = $"{opening.TeamCity} {opening.TeamName}";

            if (_positionText != null)
                _positionText.text = opening.GetPositionTitle();

            if (_salaryText != null)
                _salaryText.text = opening.GetSalaryDisplay();

            if (_contractText != null)
                _contractText.text = $"{opening.ContractYears} year contract";

            if (_teamRecordText != null)
                _teamRecordText.text = $"Record: {opening.TeamWins}-{opening.TeamLosses}";

            if (_situationText != null)
                _situationText.text = $"Situation: {opening.TeamSituation}\nExpectations: {opening.OwnerExpectations}";

            if (_requirementsText != null)
                _requirementsText.text = opening.Requirements?.GetSummary() ?? "No specific requirements";

            if (_descriptionText != null)
                _descriptionText.text = opening.JobDescription;

            if (_competitionText != null)
                _competitionText.text = $"Competition: {opening.EstimatedCompetition}/10\n" +
                                       $"Other candidates: {opening.OtherCandidates.Count}";

            UpdateApplicationSection(opening);
        }

        private void UpdateApplicationSection(JobOpening opening)
        {
            var application = opening.UserApplication;

            // Apply button state
            if (_applyButton != null)
            {
                _applyButton.interactable = application == null;
            }

            // Status text
            if (_applicationStatusText != null)
            {
                if (application == null)
                {
                    _applicationStatusText.text = "";
                    _applicationStatusText.color = Color.white;
                }
                else if (application.HasOffer)
                {
                    _applicationStatusText.text = $"OFFER RECEIVED!\n{application.TeamResponse}\n\n" +
                                                 $"Offered: {opening.GetSalaryDisplay()} for {opening.ContractYears} years";
                    _applicationStatusText.color = new Color(0.3f, 0.9f, 0.3f);
                }
                else
                {
                    _applicationStatusText.text = $"Status: {application.Status}\n{application.TeamResponse}";
                    _applicationStatusText.color = application.Status == JobApplicationStatus.Rejected
                        ? new Color(0.8f, 0.3f, 0.3f)
                        : Color.white;
                }
            }

            // Accept/Decline buttons
            bool hasOffer = application?.HasOffer == true;

            if (_acceptOfferButton != null)
            {
                _acceptOfferButton.gameObject.SetActive(hasOffer);
            }

            if (_declineOfferButton != null)
            {
                _declineOfferButton.gameObject.SetActive(hasOffer);
            }

            if (_coverLetterInput != null)
            {
                _coverLetterInput.gameObject.SetActive(application == null);
            }
        }

        private void OnFilterChanged(int index)
        {
            _roleFilter = index switch
            {
                1 => UserRole.HeadCoachOnly,
                2 => UserRole.GMOnly,
                _ => null
            };

            var jobManager = JobMarketManager.Instance;
            if (jobManager != null)
                RefreshJobListings(jobManager);
        }

        private void OnApplyClicked()
        {
            if (_selectedJob == null) return;

            var jobManager = JobMarketManager.Instance;
            if (jobManager == null) return;

            string coverLetter = _coverLetterInput?.text;
            var application = jobManager.ApplyForJob(_selectedJob.OpeningId, coverLetter);

            if (application != null)
            {
                RefreshJobListings(jobManager);
                UpdateJobDetails(_selectedJob);
            }
        }

        private void OnAcceptOffer()
        {
            if (_selectedJob == null) return;

            var jobManager = JobMarketManager.Instance;
            if (jobManager == null) return;

            if (jobManager.AcceptJob(_selectedJob.OpeningId))
            {
                // GameManager will handle transitioning to new role
                Hide();

                // Start new career with accepted team/role
                GameManager.Instance?.StartNewCareerFromJobMarket(
                    _selectedJob.TeamId,
                    _selectedJob.Position,
                    _selectedJob.OfferedSalary,
                    _selectedJob.ContractYears
                );
            }
        }

        private void OnDeclineOffer()
        {
            if (_selectedJob == null) return;

            var jobManager = JobMarketManager.Instance;
            if (jobManager == null) return;

            jobManager.DeclineJob(_selectedJob.OpeningId);
            RefreshJobListings(jobManager);
            UpdateJobDetails(_selectedJob);
        }
    }
}
