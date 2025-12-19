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
        private List<object> _allStaff = new List<object>();  // Mixed Coach and Scout
        private object _selectedStaff;  // Coach or Scout
        private StaffFilterMode _currentFilter = StaffFilterMode.All;
        private StaffSortMode _currentSort = StaffSortMode.Rating;

        // Events
        public event Action<object> OnStaffSelected;
        public event Action<object> OnStaffFired;
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

            // Gather all staff (this would come from CoachingStaffManager and ScoutingManager)
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
            // Get staff from StaffManagementManager (the facade for all staff operations)
            var staffManager = StaffManagementManager.Instance;

            if (staffManager == null)
            {
                Debug.LogWarning("[StaffPanel] StaffManagementManager.Instance is null - cannot gather staff");
                return;
            }

            // Get all coaches
            var coaches = staffManager.GetCoachingStaff(teamId);
            foreach (var coach in coaches)
            {
                _allStaff.Add(coach);
            }

            // Get all scouts
            var scouts = staffManager.GetScouts(teamId);
            foreach (var scout in scouts)
            {
                _allStaff.Add(scout);
            }

            Debug.Log($"[StaffPanel] Gathered {coaches.Count} coaches and {scouts.Count} scouts for team: {teamId}");
        }

        private void RefreshTeamInfo(Team team)
        {
            if (_teamNameText != null)
                _teamNameText.text = team.Name;

            // Get budget info from StaffManagementManager
            long totalBudget = 15_000_000;  // Default staff budget
            long usedBudget = StaffManagementManager.Instance?.GetTotalStaffBudget(team.TeamId) ?? 0;

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

        private List<object> GetFilteredAndSortedStaff()
        {
            var filtered = _allStaff.AsEnumerable();

            // Apply filter
            filtered = _currentFilter switch
            {
                StaffFilterMode.Coaches => filtered.Where(s => s is Coach c && !IsCoordinator(c)),
                StaffFilterMode.Scouts => filtered.Where(s => s is Scout),
                StaffFilterMode.Coordinators => filtered.Where(s => s is Coach c && IsCoordinator(c)),
                _ => filtered
            };

            // Apply sort
            filtered = _currentSort switch
            {
                StaffSortMode.Rating => filtered.OrderByDescending(GetStaffRating),
                StaffSortMode.Salary => filtered.OrderByDescending(GetStaffSalary),
                StaffSortMode.Name => filtered.OrderBy(GetStaffName),
                StaffSortMode.Position => filtered.OrderBy(GetStaffPositionOrder),
                _ => filtered
            };

            return filtered.ToList();
        }

        private bool IsCoordinator(Coach coach)
        {
            return coach.Position == CoachPosition.OffensiveCoordinator ||
                   coach.Position == CoachPosition.DefensiveCoordinator;
        }

        private void CreateStaffRow(object staff)
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

        private GameObject CreateDynamicStaffRow(object staff)
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
            AddColumnText(row, GetStaffName(staff), 150);
            AddColumnText(row, GetStaffPositionShort(staff), 60);
            AddColumnText(row, GetStaffSpecialty(staff), 100);
            AddColumnText(row, GetStaffAssignment(staff), 120);
            AddColumnText(row, $"${GetStaffSalary(staff):N0}", 80);

            // Rating with color
            var ratingObj = AddColumnText(row, GetStaffRating(staff).ToString(), 40);
            var ratingText = ratingObj.GetComponent<Text>();
            if (ratingText != null)
            {
                ratingText.color = AttributeDisplayFactory.GetRatingColor(GetStaffRating(staff));
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

        private void SelectStaff(object staff)
        {
            _selectedStaff = staff;
            OnStaffSelected?.Invoke(staff);
            ShowStaffDetail(staff);
        }

        private void ShowStaffDetail(object staff)
        {
            if (_detailPanel != null)
                _detailPanel.SetActive(true);

            if (staff is Coach coach)
            {
                ShowCoachDetail(coach);
            }
            else if (staff is Scout scout)
            {
                ShowScoutDetail(scout);
            }

            UpdateAssignmentUI(staff);
        }

        private void ShowCoachDetail(Coach coach)
        {
            if (_staffNameText != null)
                _staffNameText.text = coach.FullName;

            if (_staffPositionText != null)
                _staffPositionText.text = coach.Position.ToString();

            if (_staffRatingText != null)
            {
                _staffRatingText.text = $"Rating: {coach.OverallRating}";
                AttributeDisplayFactory.ApplyRatingColor(_staffRatingText, coach.OverallRating);
            }

            if (_staffSalaryText != null)
                _staffSalaryText.text = $"Salary: ${coach.AnnualSalary:N0}/yr";

            if (_staffContractText != null)
                _staffContractText.text = $"Contract: {coach.ContractYearsRemaining} years";

            if (_staffAgeText != null)
                _staffAgeText.text = $"Age: {coach.Age}";

            if (_staffExperienceText != null)
                _staffExperienceText.text = $"Experience: {coach.ExperienceYears} years";

            // Show attributes
            ShowCoachAttributes(coach);
        }

        private void ShowScoutDetail(Scout scout)
        {
            if (_staffNameText != null)
                _staffNameText.text = scout.FullName;

            if (_staffPositionText != null)
                _staffPositionText.text = "Scout";

            if (_staffRatingText != null)
            {
                _staffRatingText.text = $"Rating: {scout.OverallRating}";
                AttributeDisplayFactory.ApplyRatingColor(_staffRatingText, scout.OverallRating);
            }

            if (_staffSalaryText != null)
                _staffSalaryText.text = $"Salary: ${scout.AnnualSalary:N0}/yr";

            if (_staffContractText != null)
                _staffContractText.text = $"Contract: {scout.ContractYearsRemaining} years";

            if (_staffAgeText != null)
                _staffAgeText.text = $"Age: {scout.Age}";

            if (_staffExperienceText != null)
                _staffExperienceText.text = $"Experience: {scout.ExperienceYears} years";

            // Show attributes
            ShowScoutAttributes(scout);
        }

        private void ShowCoachAttributes(Coach coach)
        {
            if (_attributesContainer == null) return;

            var attributes = new Dictionary<string, int>
            {
                { "Offensive Scheme", coach.OffensiveScheme },
                { "Defensive Scheme", coach.DefensiveScheme },
                { "Game Management", coach.GameManagement },
                { "Player Development", coach.PlayerDevelopment },
                { "Motivation", coach.Motivation },
                { "Communication", coach.Communication }
            };

            AttributeDisplayFactory.PopulateAttributeContainer(_attributesContainer, attributes);
        }

        private void ShowScoutAttributes(Scout scout)
        {
            if (_attributesContainer == null) return;

            var attributes = new Dictionary<string, int>
            {
                { "Evaluation Accuracy", scout.EvaluationAccuracy },
                { "Prospect Evaluation", scout.ProspectEvaluation },
                { "Pro Evaluation", scout.ProEvaluation },
                { "Potential Assessment", scout.PotentialAssessment },
                { "Work Rate", scout.WorkRate },
                { "Attention to Detail", scout.AttentionToDetail }
            };

            AttributeDisplayFactory.PopulateAttributeContainer(_attributesContainer, attributes);
        }

        private void ClearStaffDetail()
        {
            _selectedStaff = null;
            if (_detailPanel != null)
                _detailPanel.SetActive(false);
        }

        // ==================== ASSIGNMENT UI ====================

        private void UpdateAssignmentUI(object staff)
        {
            if (_assignmentPanel == null) return;

            _assignmentPanel.SetActive(true);

            // Update task dropdown based on staff type
            if (_taskDropdown != null)
            {
                _taskDropdown.ClearOptions();

                if (staff is Coach coach)
                {
                    if (coach.Position == CoachPosition.AssistantCoach)
                    {
                        _taskDropdown.AddOptions(new List<string>
                        {
                            "Unassigned",
                            "Player Development"
                        });
                    }
                    else if (IsCoordinator(coach))
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
                else if (staff is Scout)
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
                _currentAssignmentText.text = $"Current: {GetStaffAssignment(staff)}";
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

            Debug.Log($"[StaffPanel] Assigning {GetStaffName(_selectedStaff)} to task: {taskName}");

            // TODO: Call StaffManagementManager to assign task
            // StaffManagementManager.Instance.AssignScoutToTask(...) or AssignCoachToPlayers(...)

            UpdateAssignmentUI(_selectedStaff);
        }

        private void OnFireClicked()
        {
            if (_selectedStaff == null) return;

            var team = GameManager.Instance?.GetPlayerTeam();
            if (team == null) return;

            var staffManager = StaffManagementManager.Instance;
            if (staffManager == null)
            {
                Debug.LogWarning("[StaffPanel] StaffManagementManager not available");
                return;
            }

            (bool success, string message) result;

            if (_selectedStaff is Coach coach)
            {
                result = staffManager.FireCoach(team.TeamId, coach.CoachId);
            }
            else if (_selectedStaff is Scout scout)
            {
                result = staffManager.FireScout(team.TeamId, scout.ScoutId);
            }
            else
            {
                Debug.LogWarning("[StaffPanel] Unknown staff type");
                return;
            }

            if (result.success)
            {
                OnStaffFired?.Invoke(_selectedStaff);
                Debug.Log($"[StaffPanel] Fired: {GetStaffName(_selectedStaff)} - {result.message}");
            }
            else
            {
                Debug.LogWarning($"[StaffPanel] Failed to fire: {result.message}");
            }

            RefreshStaffList();
            ClearStaffDetail();
        }

        private void OnViewContractClicked()
        {
            if (_selectedStaff == null) return;
            Debug.Log($"[StaffPanel] View contract for: {GetStaffName(_selectedStaff)}");
            // TODO: Show contract detail popup
        }

        private void OnHireNewClicked_Internal()
        {
            OnHireNewClicked?.Invoke();
            Debug.Log("[StaffPanel] Hire new staff clicked");
            // TODO: Open StaffHiringPanel
        }

        // ==================== HELPER METHODS ====================

        private string GetStaffName(object staff)
        {
            if (staff is Coach coach) return coach.FullName;
            if (staff is Scout scout) return scout.FullName;
            return "Unknown";
        }

        private int GetStaffRating(object staff)
        {
            if (staff is Coach coach) return coach.OverallRating;
            if (staff is Scout scout) return scout.OverallRating;
            return 0;
        }

        private int GetStaffSalary(object staff)
        {
            if (staff is Coach coach) return coach.AnnualSalary;
            if (staff is Scout scout) return scout.AnnualSalary;
            return 0;
        }

        private string GetStaffPositionShort(object staff)
        {
            if (staff is Coach coach)
            {
                return coach.Position switch
                {
                    CoachPosition.HeadCoach => "HC",
                    CoachPosition.AssistantCoach => "AC",
                    CoachPosition.OffensiveCoordinator => "OC",
                    CoachPosition.DefensiveCoordinator => "DC",
                    _ => "COACH"
                };
            }
            if (staff is Scout) return "SCT";
            return "?";
        }

        private int GetStaffPositionOrder(object staff)
        {
            if (staff is Coach coach)
            {
                return coach.Position switch
                {
                    CoachPosition.HeadCoach => 0,
                    CoachPosition.OffensiveCoordinator => 1,
                    CoachPosition.DefensiveCoordinator => 2,
                    CoachPosition.AssistantCoach => 3,
                    _ => 10
                };
            }
            if (staff is Scout) return 20;
            return 99;
        }

        private string GetStaffSpecialty(object staff)
        {
            if (staff is Coach coach)
            {
                if (coach.Specializations.Count > 0)
                    return coach.Specializations[0].ToString();
                return coach.PrimaryStyle.ToString();
            }
            if (staff is Scout scout)
            {
                return scout.PrimarySpecialization.ToString();
            }
            return "-";
        }

        private string GetStaffAssignment(object staff)
        {
            // Get from StaffManagementManager
            string staffId = staff is Coach c ? c.CoachId : (staff is Scout s ? s.ScoutId : null);
            if (string.IsNullOrEmpty(staffId)) return "-";

            var assignment = StaffManagementManager.Instance?.GetAssignment(staffId);
            return assignment?.GetTaskDisplayName() ?? "Unassigned";
        }
    }
}
