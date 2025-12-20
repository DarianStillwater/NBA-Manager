using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.UI.Components;

namespace NBAHeadCoach.UI.Panels
{
    /// <summary>
    /// Staff management panel showing all team staff with assignments and details.
    /// Follows RosterPanel pattern (list + detail view).
    /// </summary>
    public class StaffPanel : BasePanel
    {
        [Header("List Configuration")]
        [SerializeField] private Transform _listContent;
        [SerializeField] private GameObject _staffRowPrefab;

        [Header("Team Info")]
        [SerializeField] private Text _teamNameText;
        [SerializeField] private Text _staffCountText;
        [SerializeField] private Text _budgetUsedText;
        [SerializeField] private Text _budgetRemainingText;

        [Header("Sort & Filter")]
        [SerializeField] private Dropdown _filterDropdown;
        [SerializeField] private Dropdown _sortDropdown;

        [Header("Staff Details Panel")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Text _staffNameText;
        [SerializeField] private Text _staffPositionText;
        [SerializeField] private Text _staffRatingText;
        [SerializeField] private Text _staffSalaryText;
        [SerializeField] private Text _staffContractText;
        [SerializeField] private Text _staffAgeText;
        [SerializeField] private Text _staffExperienceText;
        [SerializeField] private Transform _attributesContainer;

        [Header("Assignment Section")]
        [SerializeField] private GameObject _assignmentPanel;
        [SerializeField] private Dropdown _taskDropdown;
        [SerializeField] private Transform _targetSelectionContainer;
        [SerializeField] private Text _currentAssignmentText;
        [SerializeField] private Button _assignButton;

        [Header("Actions")]
        [SerializeField] private Button _fireButton;
        [SerializeField] private Button _viewContractButton;
        [SerializeField] private Button _hireNewButton;

        // Internal state
        private List<GameObject> _spawnedRows = new List<GameObject>();
        private List<UnifiedCareerProfile> _allStaff = new List<UnifiedCareerProfile>();
        private UnifiedCareerProfile _selectedStaff;
        private StaffFilterMode _currentFilter = StaffFilterMode.All;
        private StaffSortMode _currentSort = StaffSortMode.Rating;

        // Events
        public event Action<UnifiedCareerProfile> OnStaffSelected;
        public event Action<UnifiedCareerProfile> OnStaffFired;
        public event Action OnHireNewClicked;

        private enum StaffFilterMode { All, Coaches, Scouts, Coordinators }
        private enum StaffSortMode { Rating, Salary, Name, Position }

        protected override void Awake()
        {
            base.Awake();
            SetupControls();
        }

        private void SetupControls()
        {
            // Filter dropdown
            if (_filterDropdown != null)
            {
                _filterDropdown.ClearOptions();
                _filterDropdown.AddOptions(new List<string>
                {
                    "All Staff",
                    "Coaches",
                    "Scouts",
                    "Coordinators"
                });
                _filterDropdown.onValueChanged.AddListener(OnFilterChanged);
            }

            // Sort dropdown
            if (_sortDropdown != null)
            {
                _sortDropdown.ClearOptions();
                _sortDropdown.AddOptions(new List<string>
                {
                    "Rating",
                    "Salary",
                    "Name",
                    "Position"
                });
                _sortDropdown.onValueChanged.AddListener(OnSortChanged);
            }

            // Task dropdown for assignments
            if (_taskDropdown != null)
            {
                _taskDropdown.onValueChanged.AddListener(OnTaskDropdownChanged);
            }

            // Buttons
            if (_fireButton != null)
                _fireButton.onClick.AddListener(OnFireClicked);

            if (_viewContractButton != null)
                _viewContractButton.onClick.AddListener(OnViewContractClicked);

            if (_hireNewButton != null)
                _hireNewButton.onClick.AddListener(OnHireNewClicked_Internal);

            if (_assignButton != null)
                _assignButton.onClick.AddListener(OnAssignClicked);
        }

        protected override void OnShown()
        {
            base.OnShown();
            RefreshStaffList();
            ClearStaffDetail();
        }

        // ==================== LIST MANAGEMENT ====================

        public void RefreshStaffList()
        {
            // Clear old rows
            foreach (var go in _spawnedRows)
            {
                if (go != null) Destroy(go);
            }
            _spawnedRows.Clear();
            _allStaff.Clear();

            // Get team
            var team = GameManager.Instance?.GetPlayerTeam();
            if (team == null)
            {
                Debug.Log("[StaffPanel] No team available");
                return;
            }

            // Update team info
            RefreshTeamInfo(team);

            // Gather all staff
            GatherAllStaff(team.TeamId);

            // Apply filters and sorting
            var filteredStaff = GetFilteredAndSortedStaff();

            // Create rows
            foreach (var staff in filteredStaff)
            {
                CreateStaffRow(staff);
            }
        }

        private void GatherAllStaff(string teamId)
        {
            var personnelManager = PersonnelManager.Instance;
            if (personnelManager == null)
            {
                Debug.LogWarning("[StaffPanel] PersonnelManager.Instance is null");
                return;
            }

            _allStaff = personnelManager.GetTeamStaff(teamId);
            Debug.Log($"[StaffPanel] Gathered {_allStaff.Count} staff members for team: {teamId}");
        }

        private void RefreshTeamInfo(Team team)
        {
            if (_teamNameText != null)
                _teamNameText.text = team.Name;

            // Get budget info from FinanceManager
            var finance = GameManager.Instance?.FinanceManager?.GetTeamFinance(team.TeamId);
            long totalBudget = finance?.StaffBudget ?? 15_000_000;
            long usedBudget = _allStaff.Sum(s => (long)s.AnnualSalary);

            if (_staffCountText != null)
                _staffCountText.text = $"Staff: {_allStaff.Count}";

            if (_budgetUsedText != null)
                _budgetUsedText.text = $"Used: ${usedBudget:N0}";

            if (_budgetRemainingText != null)
            {
                long remaining = totalBudget - usedBudget;
                _budgetRemainingText.text = $"Available: ${remaining:N0}";
                _budgetRemainingText.color = remaining > 0 ? Color.green : Color.red;
            }
        }

        private List<UnifiedCareerProfile> GetFilteredAndSortedStaff()
        {
            var filtered = _allStaff.AsEnumerable();

            // Apply filter
            filtered = _currentFilter switch
            {
                StaffFilterMode.Coaches => filtered.Where(s => s.CurrentTrack == UnifiedCareerTrack.Coaching && !s.CurrentRole.IsCoordinator()),
                StaffFilterMode.Scouts => filtered.Where(s => s.CurrentRole == UnifiedRole.Scout),
                StaffFilterMode.Coordinators => filtered.Where(s => s.CurrentRole.IsCoordinator()),
                _ => filtered
            };

            // Apply sort
            filtered = _currentSort switch
            {
                StaffSortMode.Rating => filtered.OrderByDescending(s => s.OverallRating),
                StaffSortMode.Salary => filtered.OrderByDescending(s => s.AnnualSalary),
                StaffSortMode.Name => filtered.OrderBy(s => s.PersonName),
                StaffSortMode.Position => filtered.OrderBy(s => (int)s.CurrentRole),
                _ => filtered
            };

            return filtered.ToList();
        }

        private bool IsCoordinator(UnifiedCareerProfile profile)
        {
            return profile.CurrentRole.IsCoordinator();
        }

        private void CreateStaffRow(UnifiedCareerProfile staff)
        {
            GameObject row;

            if (_staffRowPrefab != null && _listContent != null)
            {
                row = Instantiate(_staffRowPrefab, _listContent);
            }
            else
            {
                // Create dynamic row if no prefab
                row = CreateDynamicStaffRow(staff);
            }

            if (row != null)
            {
                _spawnedRows.Add(row);

                // Add click handler
                var button = row.GetComponent<Button>();
                if (button == null) button = row.AddComponent<Button>();

                var capturedStaff = staff;
                button.onClick.AddListener(() => SelectStaff(capturedStaff));
            }
        }

        private GameObject CreateDynamicStaffRow(UnifiedCareerProfile profile)
        {
            if (_listContent == null) return null;

            var row = new GameObject("StaffRow");
            row.transform.SetParent(_listContent, false);

            // Add layout
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.spacing = 10;
            layout.padding = new RectOffset(5, 5, 5, 5);

            // Add background
            var image = row.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);

            // Size
            var rectTransform = row.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0, 40);

            // Add text columns: Name | Position | Specialty | Assignment | Salary | Rating
            AddColumnText(row, profile.PersonName, 150);
            AddColumnText(row, profile.CurrentRole.ToString().Substring(0, 2).ToUpper(), 60);
            AddColumnText(row, GetStaffSpecialty(profile), 100);
            AddColumnText(row, GetStaffAssignment(profile), 120);
            AddColumnText(row, $"${profile.AnnualSalary:N0}", 80);

            // Rating with color
            var ratingObj = AddColumnText(row, profile.OverallRating.ToString(), 40);
            var ratingText = ratingObj.GetComponent<Text>();
            if (ratingText != null)
            {
                ratingText.color = AttributeDisplayFactory.GetRatingColor(profile.OverallRating);
            }

            return row;
        }

        private GameObject AddColumnText(GameObject parent, string text, float width)
        {
            var textObj = new GameObject("Column");
            textObj.transform.SetParent(parent.transform, false);

            var rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(width, 30);

            var layout = textObj.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.flexibleWidth = 0;

            var textComponent = textObj.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = 12;
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleLeft;

            return textObj;
        }

        // ==================== STAFF DETAIL ====================

        private void SelectStaff(UnifiedCareerProfile profile)
        {
            _selectedStaff = profile;
            OnStaffSelected?.Invoke(profile);
            ShowStaffDetail(profile);
        }

        private void ShowStaffDetail(UnifiedCareerProfile profile)
        {
            if (_detailPanel != null)
                _detailPanel.SetActive(true);

            if (_staffNameText != null)
                _staffNameText.text = profile.PersonName;

            if (_staffPositionText != null)
                _staffPositionText.text = profile.CurrentRole.ToString();

            if (_staffRatingText != null)
            {
                _staffRatingText.text = $"Rating: {profile.OverallRating}";
                AttributeDisplayFactory.ApplyRatingColor(_staffRatingText, profile.OverallRating);
            }

            if (_staffSalaryText != null)
                _staffSalaryText.text = $"Salary: ${profile.AnnualSalary:N0}/yr";

            if (_staffContractText != null)
                _staffContractText.text = $"Contract: {profile.ContractYearsRemaining} years";

            if (_staffAgeText != null)
                _staffAgeText.text = $"Age: {profile.CurrentAge}";

            if (_staffExperienceText != null)
            {
                int exp = profile.CurrentTrack == UnifiedCareerTrack.Coaching ? profile.TotalCoachingYears : profile.TotalFrontOfficeYears;
                _staffExperienceText.text = $"Experience: {exp} years";
            }

            // Show attributes based on role
            ShowAttributes(profile);

            UpdateAssignmentUI(profile);
        }

        private void ShowAttributes(UnifiedCareerProfile profile)
        {
            if (_attributesContainer == null) return;

            var attributes = new Dictionary<string, int>();

            if (profile.CurrentTrack == UnifiedCareerTrack.Coaching)
            {
                attributes.Add("Offensive Scheme", profile.OffensiveScheme);
                attributes.Add("Defensive Scheme", profile.DefensiveScheme);
                attributes.Add("Game Management", profile.GameManagement);
                attributes.Add("Player Development", profile.PlayerDevelopment);
                attributes.Add("Motivation", profile.Motivation);
                attributes.Add("Communication", profile.Communication);
            }
            else
            {
                attributes.Add("Evaluation Accuracy", profile.EvaluationAccuracy);
                attributes.Add("Prospect Evaluation", profile.ProspectEvaluation);
                attributes.Add("Pro Evaluation", profile.ProEvaluation);
                attributes.Add("Potential Assessment", profile.PotentialAssessment);
                attributes.Add("Work Rate", profile.WorkRate);
                attributes.Add("Attention to Detail", profile.AttentionToDetail);
            }

            AttributeDisplayFactory.PopulateAttributeContainer(_attributesContainer, attributes);
        }

        private void ClearStaffDetail()
        {
            _selectedStaff = null;
            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        // ==================== ASSIGNMENT UI ====================

        private void UpdateAssignmentUI(UnifiedCareerProfile profile)
        {
            if (_assignmentPanel == null) return;

            _assignmentPanel.SetActive(true);

            // Update task dropdown based on staff type
            if (_taskDropdown != null)
            {
                _taskDropdown.ClearOptions();

                if (profile.CurrentTrack == UnifiedCareerTrack.Coaching)
                {
                    if (profile.CurrentRole == UnifiedRole.AssistantCoach)
                    {
                        _taskDropdown.AddOptions(new List<string>
                        {
                            "Unassigned",
                            "Player Development"
                        });
                    }
                    else if (profile.CurrentRole.IsCoordinator())
                    {
                        _taskDropdown.AddOptions(new List<string>
                        {
                            "Unassigned",
                            "Game Strategy"
                        });
                    }
                    else
                    {
                        _assignmentPanel.SetActive(false);  // HC doesn't get assigned
                    }
                }
                else if (profile.CurrentRole == UnifiedRole.Scout)
                {
                    _taskDropdown.AddOptions(new List<string>
                    {
                        "Unassigned",
                        "Draft Prospect Scouting",
                        "Opponent Scouting",
                        "Trade Target Evaluation",
                        "Free Agent Evaluation"
                    });
                }
            }

            // Show current assignment
            if (_currentAssignmentText != null)
            {
                var teamId = GameManager.Instance?.GetPlayerTeam()?.TeamId;
                var assignment = PersonnelManager.Instance?.GetActiveAssignments(teamId)
                    ?.FirstOrDefault(a => a.StaffId == profile.ProfileId);
                
                _currentAssignmentText.text = $"Current: {(assignment != null ? assignment.GetTaskDisplayName() : "Unassigned")}";
            }
        }

        // ==================== EVENT HANDLERS ====================

        private void OnFilterChanged(int index)
        {
            _currentFilter = (StaffFilterMode)index;
            RefreshStaffList();
        }

        private void OnSortChanged(int index)
        {
            _currentSort = (StaffSortMode)index;
            RefreshStaffList();
        }

        private void OnTaskDropdownChanged(int index)
        {
            // Task selection changed
            Debug.Log($"[StaffPanel] Task dropdown changed to index: {index}");
        }

        private void OnAssignClicked()
        {
            if (_selectedStaff == null || _taskDropdown == null) return;

            int taskIndex = _taskDropdown.value;
            string taskName = _taskDropdown.options[taskIndex].text;

            Debug.Log($"[StaffPanel] Assigning {_selectedStaff.PersonName} to task: {taskName}");

            if (_selectedStaff.CurrentRole == UnifiedRole.Scout)
            {
                // Mapping task name to StaffTask (this is a bit fragile, should use enum)
                StaffTask task = taskName switch
                {
                    "Draft Prospect Scouting" => StaffTask.DraftProspectScouting,
                    "Opponent Scouting" => StaffTask.OpponentScouting,
                    "Trade Target Evaluation" => StaffTask.TradeTargetEvaluation,
                    "Free Agent Evaluation" => StaffTask.FreeAgentEvaluation,
                    _ => StaffTask.None
                };

                if (task != StaffTask.None)
                {
                    PersonnelManager.Instance.AssignScoutToTask(_selectedStaff.ProfileId, task, "Global"); // Target dummy
                }
                else
                {
                    PersonnelManager.Instance.UnassignStaff(_selectedStaff.ProfileId);
                }
            }
            else if (_selectedStaff.CurrentRole == UnifiedRole.AssistantCoach)
            {
                if (taskName == "Player Development")
                {
                    PersonnelManager.Instance.AssignCoachToPlayers(_selectedStaff.ProfileId, new List<string>()); // Dummy empty list
                }
                else
                {
                    PersonnelManager.Instance.UnassignStaff(_selectedStaff.ProfileId);
                }
            }

            UpdateAssignmentUI(_selectedStaff);
        }

        private void OnFireClicked()
        {
            if (_selectedStaff == null) return;

            var team = GameManager.Instance?.GetPlayerTeam();
            if (team == null) return;

            var personnelManager = PersonnelManager.Instance;
            if (personnelManager == null)
            {
                Debug.LogWarning("[StaffPanel] PersonnelManager not available");
                return;
            }

            // Perform fire operation
            personnelManager.FirePersonnel(_selectedStaff.ProfileId, "Fired by team management");
            
            OnStaffFired?.Invoke(_selectedStaff);
            Debug.Log($"[StaffPanel] Fired: {_selectedStaff.PersonName}");

            RefreshStaffList();
            ClearStaffDetail();
        }

        private void OnViewContractClicked()
        {
            if (_selectedStaff == null) return;
            
            // Display detailed contract info (would be a popup in full implementation)
            var staff = _selectedStaff;
            string contractDetails = $"=== CONTRACT DETAILS ===\n" +
                $"Staff: {staff.PersonName}\n" +
                $"Role: {staff.CurrentRole}\n" +
                $"Team: {staff.CurrentTeamName ?? "Free Agent"}\n" +
                $"Annual Salary: ${staff.AnnualSalary:N0}\n" +
                $"Contract Years Remaining: {staff.ContractYearsRemaining}\n" +
                $"Market Value: ${staff.MarketValue:N0}\n" +
                $"Overall Rating: {staff.OverallRating}";
            
            Debug.Log($"[StaffPanel] {contractDetails}");
            
            // Update the detail panel contract text with more info if available
            if (_staffContractText != null)
            {
                _staffContractText.text = $"Contract: {staff.ContractYearsRemaining}yr @ ${staff.AnnualSalary:N0}/yr";
            }
        }

        private void OnHireNewClicked_Internal()
        {
            OnHireNewClicked?.Invoke();
            Debug.Log("[StaffPanel] Hire new staff clicked");
            
            // Navigate to StaffHiringPanel
            var controller = FindObjectOfType<GameSceneController>();
            if (controller != null)
            {
                controller.ShowPanel("StaffHiring");
            }
        }

        // ==================== HELPER METHODS ====================

        private string GetStaffSpecialty(UnifiedCareerProfile profile)
        {
            if (profile.CurrentTrack == UnifiedCareerTrack.Coaching)
            {
                if (profile.Specializations.Count > 0)
                    return profile.Specializations[0].ToString();
                return profile.PrimaryStyle.ToString();
            }
            else if (profile.ScoutingSpecializations.Count > 0)
            {
                return profile.ScoutingSpecializations[0].ToString();
            }
            return "-";
        }

        private string GetStaffAssignment(UnifiedCareerProfile profile)
        {
            var teamId = GameManager.Instance?.GetPlayerTeam()?.TeamId;
            var assignment = PersonnelManager.Instance?.GetActiveAssignments(teamId)
                ?.FirstOrDefault(a => a.StaffId == profile.ProfileId);
            
            return assignment?.GetTaskDisplayName() ?? "Unassigned";
        }
    }
}
