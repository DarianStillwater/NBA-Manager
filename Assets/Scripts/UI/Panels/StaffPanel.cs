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
        [SerializeField] private Text _staffTierText;
        [SerializeField] private Image _staffTierIndicator;
        [SerializeField] private Text _staffSalaryText;
        [SerializeField] private Text _staffContractText;
        [SerializeField] private Text _staffAgeText;
        [SerializeField] private Text _staffExperienceText;

        [Header("Text-Based Evaluation")]
        [SerializeField] private Text _overallAssessmentText;
        [SerializeField] private Text _strengthsSummaryText;
        [SerializeField] private Text _weaknessesSummaryText;
        [SerializeField] private Text _careerTrajectoryText;
        [SerializeField] private Transform _skillAssessmentsContainer;
        [SerializeField] private GameObject _skillAssessmentRowPrefab;

        [Header("Legacy Attributes (Hidden)")]
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
            var finance = GameManager.Instance?.FinanceManager?.GetTeamFinances(team.TeamId);
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
                _staffPositionText.text = FormatRoleName(profile.CurrentRole);

            // Generate text-based evaluation
            var evaluation = StaffEvaluationGenerator.Instance.GenerateEvaluation(profile);

            // Show tier instead of numeric rating
            if (_staffTierText != null)
                _staffTierText.text = evaluation.Tier.GetLabel();

            if (_staffTierIndicator != null)
                _staffTierIndicator.color = evaluation.Tier.GetColor();

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

            // Show text-based evaluation
            ShowEvaluation(evaluation);

            UpdateAssignmentUI(profile);
        }

        private void ShowEvaluation(StaffEvaluation evaluation)
        {
            if (evaluation == null) return;

            // Overall assessment
            if (_overallAssessmentText != null)
                _overallAssessmentText.text = evaluation.OverallAssessment;

            // Strengths
            if (_strengthsSummaryText != null)
                _strengthsSummaryText.text = evaluation.StrengthsSummary;

            // Weaknesses
            if (_weaknessesSummaryText != null)
                _weaknessesSummaryText.text = evaluation.WeaknessesSummary;

            // Career trajectory
            if (_careerTrajectoryText != null)
                _careerTrajectoryText.text = evaluation.CareerTrajectory;

            // Skill assessments
            PopulateSkillAssessments(evaluation.SkillAssessments);

            // Hide legacy attributes container
            if (_attributesContainer != null)
                _attributesContainer.gameObject.SetActive(false);
        }

        private void PopulateSkillAssessments(List<StaffSkillAssessment> assessments)
        {
            if (_skillAssessmentsContainer == null) return;

            // Clear existing
            foreach (Transform child in _skillAssessmentsContainer)
                Destroy(child.gameObject);

            if (assessments == null) return;

            foreach (var assessment in assessments)
            {
                CreateSkillAssessmentRow(assessment);
            }
        }

        private void CreateSkillAssessmentRow(StaffSkillAssessment assessment)
        {
            if (_skillAssessmentsContainer == null) return;

            GameObject row;

            if (_skillAssessmentRowPrefab != null)
            {
                row = Instantiate(_skillAssessmentRowPrefab, _skillAssessmentsContainer);
                var texts = row.GetComponentsInChildren<Text>();
                if (texts.Length >= 3)
                {
                    texts[0].text = assessment.SkillName;
                    texts[1].text = assessment.Grade;
                    texts[2].text = assessment.Description;
                }
            }
            else
            {
                // Create dynamically if no prefab
                row = new GameObject("SkillRow");
                row.transform.SetParent(_skillAssessmentsContainer, false);

                var layout = row.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 2;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                // Header: Skill Name + Grade
                var headerObj = new GameObject("Header");
                headerObj.transform.SetParent(row.transform, false);
                var headerLayout = headerObj.AddComponent<HorizontalLayoutGroup>();
                headerLayout.spacing = 10;

                var nameText = CreateText(headerObj, assessment.SkillName, 14, FontStyle.Bold);
                var gradeText = CreateText(headerObj, assessment.Grade, 14, FontStyle.Normal);

                // Set grade color based on text
                gradeText.color = GetGradeColor(assessment.Grade);

                // Description
                var descText = CreateText(row, assessment.Description, 12, FontStyle.Normal);
                descText.color = new Color(0.8f, 0.8f, 0.8f);
            }
        }

        private Text CreateText(GameObject parent, string content, int fontSize, FontStyle style)
        {
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(parent.transform, false);
            var text = textObj.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        private Color GetGradeColor(string grade)
        {
            return grade switch
            {
                "Elite" => new Color(0.2f, 0.8f, 0.2f),
                "Excellent" => new Color(0.3f, 0.7f, 0.3f),
                "Very Good" => new Color(0.4f, 0.7f, 0.9f),
                "Solid" => new Color(0.6f, 0.8f, 0.4f),
                "Average" => new Color(0.9f, 0.7f, 0.2f),
                "Below Average" => new Color(0.9f, 0.5f, 0.3f),
                "Poor" => new Color(0.9f, 0.3f, 0.3f),
                _ => Color.white
            };
        }

        private string FormatRoleName(UnifiedRole role)
        {
            return role switch
            {
                UnifiedRole.HeadCoach => "Head Coach",
                UnifiedRole.AssistantCoach => "Assistant Coach",
                UnifiedRole.PositionCoach => "Position Coach",
                UnifiedRole.Coordinator => "Coordinator",
                UnifiedRole.OffensiveCoordinator => "Offensive Coordinator",
                UnifiedRole.DefensiveCoordinator => "Defensive Coordinator",
                UnifiedRole.Scout => "Scout",
                UnifiedRole.GeneralManager => "General Manager",
                UnifiedRole.AssistantGM => "Assistant GM",
                UnifiedRole.Unemployed => "Unemployed",
                UnifiedRole.Retired => "Retired",
                _ => role.ToString()
            };
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

            var teamId = GameManager.Instance?.PlayerTeamId;
            if (string.IsNullOrEmpty(teamId)) return;

            var controller = FindObjectOfType<GameSceneController>();

            if (_selectedStaff.CurrentRole == UnifiedRole.Scout)
            {
                var task = MapToScoutTaskType(taskName);

                if (task == ScoutTaskType.Unassigned)
                {
                    PersonnelManager.Instance?.UnassignStaff(_selectedStaff.ProfileId);
                    UpdateAssignmentUI(_selectedStaff);
                    return;
                }

                // Show appropriate selection panel based on task type
                switch (task)
                {
                    case ScoutTaskType.DraftProspectScouting:
                        if (controller != null)
                        {
                            controller.ShowProspectSelectionForScouting(_selectedStaff, 5, (prospectIds) =>
                            {
                                PersonnelManager.Instance?.AssignScoutToTask(
                                    _selectedStaff.ProfileId,
                                    teamId,
                                    task,
                                    prospectIds,
                                    null);
                                UpdateAssignmentUI(_selectedStaff);
                            });
                        }
                        break;

                    case ScoutTaskType.OpponentAdvanceScouting:
                        if (controller != null)
                        {
                            controller.ShowTeamSelectionForScouting(_selectedStaff, (targetTeamId) =>
                            {
                                PersonnelManager.Instance?.AssignScoutToTask(
                                    _selectedStaff.ProfileId,
                                    teamId,
                                    task,
                                    null,
                                    targetTeamId);
                                UpdateAssignmentUI(_selectedStaff);
                            });
                        }
                        break;

                    case ScoutTaskType.TradeTargetEvaluation:
                    case ScoutTaskType.FreeAgentEvaluation:
                        if (controller != null)
                        {
                            controller.ShowPlayerSelectionForScout(_selectedStaff, 5, (playerIds) =>
                            {
                                PersonnelManager.Instance?.AssignScoutToTask(
                                    _selectedStaff.ProfileId,
                                    teamId,
                                    task,
                                    playerIds,
                                    null);
                                UpdateAssignmentUI(_selectedStaff);
                            });
                        }
                        break;
                }
            }
            else if (_selectedStaff.CurrentRole == UnifiedRole.AssistantCoach)
            {
                if (taskName == "Player Development")
                {
                    int capacity = DevelopmentAssignment.CalculateCoachCapacity(_selectedStaff.PlayerDevelopment);

                    if (controller != null)
                    {
                        controller.ShowPlayerSelectionForCoach(_selectedStaff, capacity, (playerIds) =>
                        {
                            var (success, message) = PersonnelManager.Instance?.AssignCoachToPlayers(
                                _selectedStaff.ProfileId,
                                teamId,
                                playerIds) ?? (false, "PersonnelManager not available");

                            if (!success)
                            {
                                controller.ShowAlert("Assignment Failed", message);
                            }

                            UpdateAssignmentUI(_selectedStaff);
                        });
                    }
                }
                else
                {
                    PersonnelManager.Instance?.UnassignStaff(_selectedStaff.ProfileId);
                    UpdateAssignmentUI(_selectedStaff);
                }
            }
        }

        private ScoutTaskType MapToScoutTaskType(string taskName)
        {
            return taskName switch
            {
                "Draft Prospect Scouting" => ScoutTaskType.DraftProspectScouting,
                "Opponent Scouting" => ScoutTaskType.OpponentAdvanceScouting,
                "Trade Target Evaluation" => ScoutTaskType.TradeTargetEvaluation,
                "Free Agent Evaluation" => ScoutTaskType.FreeAgentEvaluation,
                _ => ScoutTaskType.Unassigned
            };
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

            // Show confirmation dialog
            var controller = FindObjectOfType<GameSceneController>();
            if (controller != null)
            {
                var staffName = _selectedStaff.PersonName;
                var staffRole = _selectedStaff.CurrentRole.ToString();
                var staffToFire = _selectedStaff;

                controller.ShowDestructiveConfirmation(
                    "Fire Staff Member?",
                    $"Are you sure you want to fire {staffName} ({staffRole})?\n\nThis action cannot be undone.",
                    () =>
                    {
                        // Perform fire operation
                        personnelManager.FirePersonnel(staffToFire.ProfileId, "Fired by team management");
                        OnStaffFired?.Invoke(staffToFire);
                        RefreshStaffList();
                        ClearStaffDetail();
                    },
                    "Fire",
                    "Cancel"
                );
            }
            else
            {
                // Fallback: fire without confirmation
                personnelManager.FirePersonnel(_selectedStaff.ProfileId, "Fired by team management");
                OnStaffFired?.Invoke(_selectedStaff);
                RefreshStaffList();
                ClearStaffDetail();
            }
        }

        private void OnViewContractClicked()
        {
            if (_selectedStaff == null) return;

            // Show contract detail modal
            var controller = FindObjectOfType<GameSceneController>();
            if (controller != null)
            {
                controller.ShowContractDetail(_selectedStaff);
            }
            else
            {
                // Fallback: update inline text
                if (_staffContractText != null)
                {
                    _staffContractText.text = $"Contract: {_selectedStaff.ContractYearsRemaining}yr @ ${_selectedStaff.AnnualSalary:N0}/yr";
                }
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
