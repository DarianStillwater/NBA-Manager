using System;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Modals
{
    /// <summary>
    /// Slide-in panel displaying contract details for staff or players.
    /// </summary>
    public class ContractDetailPanel : SlidePanel
    {
        [Header("Header")]
        [SerializeField] private Text _personNameText;
        [SerializeField] private Text _roleText;
        [SerializeField] private Text _teamText;

        [Header("Contract Overview")]
        [SerializeField] private Text _annualSalaryText;
        [SerializeField] private Text _totalValueText;
        [SerializeField] private Text _yearsRemainingText;
        [SerializeField] private Text _contractDurationText;

        [Header("Contract Dates")]
        [SerializeField] private Text _contractStartText;
        [SerializeField] private Text _contractEndText;

        [Header("Market Value")]
        [SerializeField] private Text _marketValueText;
        [SerializeField] private Text _marketComparisonText;

        [Header("Options Section")]
        [SerializeField] private GameObject _optionsSection;
        [SerializeField] private Text _optionTypeText;
        [SerializeField] private Text _optionDetailsText;

        [Header("Incentives Section")]
        [SerializeField] private GameObject _incentivesSection;
        [SerializeField] private Transform _incentivesContainer;
        [SerializeField] private GameObject _incentiveRowPrefab;

        [Header("Buyout Section")]
        [SerializeField] private GameObject _buyoutSection;
        [SerializeField] private Text _buyoutAmountText;

        [Header("Actions")]
        [SerializeField] private Button _closeButton;

        public event Action OnClosed;

        protected override void Awake()
        {
            base.Awake();

            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);
        }

        /// <summary>
        /// Display contract details for a staff member.
        /// </summary>
        public void ShowStaffContract(UnifiedCareerProfile profile)
        {
            if (profile == null) return;

            // Header
            SetText(_personNameText, profile.PersonName);
            SetText(_roleText, FormatRole(profile.CurrentRole));
            SetText(_teamText, profile.CurrentTeamName ?? "Free Agent");

            // Contract Overview
            SetText(_annualSalaryText, FormatSalary(profile.AnnualSalary));

            int totalValue = profile.AnnualSalary * profile.ContractYearsRemaining;
            SetText(_totalValueText, FormatSalary(totalValue));
            SetText(_yearsRemainingText, $"{profile.ContractYearsRemaining} year{(profile.ContractYearsRemaining != 1 ? "s" : "")}");

            // Contract Dates
            if (profile.ContractStartDate != default)
            {
                SetText(_contractStartText, profile.ContractStartDate.ToString("MMM yyyy"));

                var endDate = profile.ContractStartDate.AddYears(profile.ContractYearsRemaining);
                SetText(_contractEndText, endDate.ToString("MMM yyyy"));
            }
            else
            {
                SetText(_contractStartText, "--");
                SetText(_contractEndText, "--");
            }

            // Market Value Comparison
            SetText(_marketValueText, FormatSalary(profile.MarketValue));

            int salaryDiff = profile.AnnualSalary - profile.MarketValue;
            if (salaryDiff > 0)
            {
                SetText(_marketComparisonText, $"Overpaid by {FormatSalary(salaryDiff)}");
                if (_marketComparisonText != null)
                    _marketComparisonText.color = new Color(0.9f, 0.5f, 0.3f); // Orange
            }
            else if (salaryDiff < 0)
            {
                SetText(_marketComparisonText, $"Underpaid by {FormatSalary(-salaryDiff)}");
                if (_marketComparisonText != null)
                    _marketComparisonText.color = new Color(0.3f, 0.8f, 0.4f); // Green
            }
            else
            {
                SetText(_marketComparisonText, "At market value");
                if (_marketComparisonText != null)
                    _marketComparisonText.color = Color.white;
            }

            // Options (hide for staff for now)
            if (_optionsSection != null)
                _optionsSection.SetActive(false);

            // Incentives from CoachContract
            DisplayIncentives(profile.CurrentContract);

            // Buyout
            DisplayBuyout(profile.CurrentContract);

            ShowSlide();
        }

        /// <summary>
        /// Display contract details for a player.
        /// </summary>
        public void ShowPlayerContract(Player player)
        {
            if (player == null) return;

            // Header
            SetText(_personNameText, player.FullName);
            SetText(_roleText, player.Position.ToString());
            SetText(_teamText, player.TeamId ?? "Free Agent");

            // Contract Overview
            var contract = player.CurrentContract;
            if (contract != null)
            {
                SetText(_annualSalaryText, FormatSalary((int)contract.AverageSalary));
                SetText(_totalValueText, FormatSalary((int)contract.TotalValue));
                SetText(_yearsRemainingText, $"{contract.YearsRemaining} year{(contract.YearsRemaining != 1 ? "s" : "")}");

                // Contract Dates
                SetText(_contractStartText, contract.SignedDate.ToString("MMM yyyy"));
                SetText(_contractEndText, contract.EndDate.ToString("MMM yyyy"));

                // Options
                DisplayPlayerOptions(contract);
            }
            else
            {
                SetText(_annualSalaryText, "--");
                SetText(_totalValueText, "--");
                SetText(_yearsRemainingText, "No contract");
                SetText(_contractStartText, "--");
                SetText(_contractEndText, "--");

                if (_optionsSection != null)
                    _optionsSection.SetActive(false);
            }

            // Market Value
            int marketValue = player.GetMarketValue();
            SetText(_marketValueText, FormatSalary(marketValue));

            // Hide staff-specific sections
            if (_incentivesSection != null)
                _incentivesSection.SetActive(false);
            if (_buyoutSection != null)
                _buyoutSection.SetActive(false);

            ShowSlide();
        }

        private void DisplayIncentives(CoachContract contract)
        {
            if (_incentivesSection == null) return;

            if (contract?.Incentives == null || contract.Incentives.Count == 0)
            {
                _incentivesSection.SetActive(false);
                return;
            }

            _incentivesSection.SetActive(true);

            // Clear existing
            if (_incentivesContainer != null)
            {
                foreach (Transform child in _incentivesContainer)
                    Destroy(child.gameObject);
            }

            // Add incentive rows
            foreach (var incentive in contract.Incentives)
            {
                if (_incentiveRowPrefab != null && _incentivesContainer != null)
                {
                    var row = Instantiate(_incentiveRowPrefab, _incentivesContainer);
                    var texts = row.GetComponentsInChildren<Text>();
                    if (texts.Length >= 2)
                    {
                        texts[0].text = incentive.Description;
                        texts[1].text = FormatSalary((int)incentive.BonusAmount);
                    }
                }
            }
        }

        private void DisplayBuyout(CoachContract contract)
        {
            if (_buyoutSection == null) return;

            if (contract == null || contract.BuyoutPercentage <= 0)
            {
                _buyoutSection.SetActive(false);
                return;
            }

            _buyoutSection.SetActive(true);
            long buyoutAmount = contract.CalculateBuyout();
            SetText(_buyoutAmountText, FormatSalary((int)buyoutAmount));
        }

        private void DisplayPlayerOptions(Contract contract)
        {
            if (_optionsSection == null) return;

            if (contract.PlayerOption)
            {
                _optionsSection.SetActive(true);
                SetText(_optionTypeText, "Player Option");
                SetText(_optionDetailsText, $"Final year ({contract.EndDate.Year})");
            }
            else if (contract.TeamOption)
            {
                _optionsSection.SetActive(true);
                SetText(_optionTypeText, "Team Option");
                SetText(_optionDetailsText, $"Final year ({contract.EndDate.Year})");
            }
            else if (contract.EarlyTerminationOption)
            {
                _optionsSection.SetActive(true);
                SetText(_optionTypeText, "Early Termination");
                SetText(_optionDetailsText, "Can opt out early");
            }
            else
            {
                _optionsSection.SetActive(false);
            }
        }

        private void OnCloseClicked()
        {
            HideSlide(() => OnClosed?.Invoke());
        }

        private void SetText(Text textComponent, string value)
        {
            if (textComponent != null)
                textComponent.text = value;
        }

        private string FormatSalary(int salary)
        {
            if (salary >= 1_000_000)
                return $"${salary / 1_000_000f:F2}M";
            else if (salary >= 1_000)
                return $"${salary / 1_000f:F0}K";
            return $"${salary:N0}";
        }

        private string FormatRole(UnifiedRole role)
        {
            return role switch
            {
                UnifiedRole.HeadCoach => "Head Coach",
                UnifiedRole.AssistantCoach => "Assistant Coach",
                UnifiedRole.OffensiveCoordinator => "Offensive Coordinator",
                UnifiedRole.DefensiveCoordinator => "Defensive Coordinator",
                UnifiedRole.Scout => "Scout",
                UnifiedRole.GeneralManager => "General Manager",
                UnifiedRole.AssistantGM => "Assistant GM",
                _ => role.ToString()
            };
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnCloseClicked);
        }
    }
}
