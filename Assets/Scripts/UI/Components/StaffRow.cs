using System;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Row component for displaying staff members in the staff list.
    /// Shows: Name, Position, Specialty, Current Assignment, Salary, Rating bar.
    /// </summary>
    public class StaffRow : MonoBehaviour
    {
        [Header("Text Fields")]
        public Text NameText;
        public Text PositionText;
        public Text SpecialtyText;
        public Text AssignmentText;
        public Text SalaryText;

        [Header("Rating Display")]
        public Image RatingBar;
        public Image RatingBarBackground;
        public Text RatingText;

        [Header("Interaction")]
        public Button ClickButton;
        public Image SelectionHighlight;

        [Header("Colors")]
        public Color EliteColor = new Color(0.2f, 0.8f, 0.2f);    // Green
        public Color GoodColor = new Color(0.4f, 0.7f, 0.9f);     // Blue
        public Color AverageColor = new Color(0.9f, 0.7f, 0.2f);  // Yellow/Orange
        public Color PoorColor = new Color(0.9f, 0.3f, 0.3f);     // Red

        private string _staffId;
        private StaffPositionType _positionType;
        private bool _isSelected;

        public string StaffId => _staffId;
        public StaffPositionType PositionType => _positionType;

        public event Action<StaffRow> OnRowClicked;

        private void Awake()
        {
            if (ClickButton != null)
            {
                ClickButton.onClick.AddListener(HandleClick);
            }

            SetSelected(false);
        }

        private void OnDestroy()
        {
            if (ClickButton != null)
            {
                ClickButton.onClick.RemoveListener(HandleClick);
            }
        }

        /// <summary>
        /// Setup row with coach data.
        /// </summary>
        public void SetupCoach(Coach coach, string currentAssignment = null)
        {
            if (coach == null) return;

            _staffId = coach.CoachId;
            _positionType = ConvertCoachPosition(coach.Position);

            if (NameText != null)
                NameText.text = coach.FullName;

            if (PositionText != null)
                PositionText.text = StaffLimits.GetAbbreviation(_positionType);

            if (SpecialtyText != null)
                SpecialtyText.text = GetCoachSpecialty(coach);

            if (AssignmentText != null)
                AssignmentText.text = string.IsNullOrEmpty(currentAssignment) ? "--" : currentAssignment;

            if (SalaryText != null)
                SalaryText.text = FormatSalary(coach.AnnualSalary);

            UpdateRatingDisplay(coach.OverallRating);
        }

        /// <summary>
        /// Setup row with scout data.
        /// </summary>
        public void SetupScout(Scout scout, string currentAssignment = null)
        {
            if (scout == null) return;

            _staffId = scout.ScoutId;
            _positionType = StaffPositionType.Scout;

            if (NameText != null)
                NameText.text = scout.FullName;

            if (PositionText != null)
                PositionText.text = "SCT";

            if (SpecialtyText != null)
                SpecialtyText.text = GetScoutSpecialty(scout);

            if (AssignmentText != null)
                AssignmentText.text = string.IsNullOrEmpty(currentAssignment) ? "--" : currentAssignment;

            if (SalaryText != null)
                SalaryText.text = FormatSalary(scout.AnnualSalary);

            UpdateRatingDisplay(scout.OverallRating);
        }

        /// <summary>
        /// Set the selection state of this row.
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            if (SelectionHighlight != null)
            {
                SelectionHighlight.enabled = selected;
            }
        }

        private void HandleClick()
        {
            OnRowClicked?.Invoke(this);
        }

        private void UpdateRatingDisplay(int rating)
        {
            if (RatingText != null)
                RatingText.text = rating.ToString();

            if (RatingBar != null)
            {
                // Fill based on rating (0-100)
                RatingBar.fillAmount = rating / 100f;
                RatingBar.color = GetRatingColor(rating);
            }
        }

        private Color GetRatingColor(int rating)
        {
            if (rating >= 85) return EliteColor;
            if (rating >= 70) return GoodColor;
            if (rating >= 50) return AverageColor;
            return PoorColor;
        }

        private string GetCoachSpecialty(Coach coach)
        {
            // Determine specialty based on highest attribute
            int maxAttr = Mathf.Max(
                coach.OffensiveScheme,
                coach.DefensiveScheme,
                coach.PlayerDevelopment,
                coach.GameManagement
            );

            if (maxAttr == coach.OffensiveScheme && coach.OffensiveScheme > 60)
                return "Offense";
            if (maxAttr == coach.DefensiveScheme && coach.DefensiveScheme > 60)
                return "Defense";
            if (maxAttr == coach.PlayerDevelopment && coach.PlayerDevelopment > 60)
                return "Development";
            if (maxAttr == coach.GameManagement && coach.GameManagement > 60)
                return "Management";

            return "Balanced";
        }

        private string GetScoutSpecialty(Scout scout)
        {
            // Determine specialty based on highest attribute
            int maxAttr = Mathf.Max(
                scout.EvaluatingAbility,
                scout.PotentialAssessment,
                scout.CollegeKnowledge,
                scout.InternationalKnowledge
            );

            if (maxAttr == scout.CollegeKnowledge && scout.CollegeKnowledge > 60)
                return "College";
            if (maxAttr == scout.InternationalKnowledge && scout.InternationalKnowledge > 60)
                return "International";
            if (maxAttr == scout.PotentialAssessment && scout.PotentialAssessment > 60)
                return "Potential";
            if (maxAttr == scout.EvaluatingAbility && scout.EvaluatingAbility > 60)
                return "Evaluation";

            return "Generalist";
        }

        private string FormatSalary(int salary)
        {
            if (salary >= 1_000_000)
                return $"${salary / 1_000_000f:F1}M";
            else if (salary >= 1_000)
                return $"${salary / 1_000f:F0}K";
            return $"${salary:N0}";
        }

        private StaffPositionType ConvertCoachPosition(CoachPosition position)
        {
            return position switch
            {
                CoachPosition.HeadCoach => StaffPositionType.HeadCoach,
                CoachPosition.OffensiveCoordinator => StaffPositionType.OffensiveCoordinator,
                CoachPosition.DefensiveCoordinator => StaffPositionType.DefensiveCoordinator,
                CoachPosition.AssistantCoach => StaffPositionType.AssistantCoach,
                CoachPosition.ShootingCoach => StaffPositionType.AssistantCoach,
                CoachPosition.StrengthCoach => StaffPositionType.AssistantCoach,
                CoachPosition.PlayerDevelopment => StaffPositionType.AssistantCoach,
                CoachPosition.VideoCoordinator => StaffPositionType.AssistantCoach,
                _ => StaffPositionType.AssistantCoach
            };
        }
    }
}
