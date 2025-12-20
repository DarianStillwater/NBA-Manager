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
        /// Setup row with staff data from UnifiedCareerProfile.
        /// </summary>
        public void SetupStaff(UnifiedCareerProfile profile, string currentAssignment = null)
        {
            if (profile == null) return;

            _staffId = profile.ProfileId;
            _positionType = profile.CurrentRole.ToStaffPositionType();

            if (NameText != null)
                NameText.text = profile.PersonName;

            if (PositionText != null)
                PositionText.text = StaffLimits.GetAbbreviation(_positionType);

            if (SpecialtyText != null)
                SpecialtyText.text = GetStaffSpecialty(profile);

            if (AssignmentText != null)
                AssignmentText.text = string.IsNullOrEmpty(currentAssignment) ? "--" : currentAssignment;

            if (SalaryText != null)
                SalaryText.text = FormatSalary(profile.AnnualSalary);

            UpdateRatingDisplay(profile.OverallRating);
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

        private string GetStaffSpecialty(UnifiedCareerProfile profile)
        {
            if (profile.CurrentTrack == UnifiedCareerTrack.Coaching)
            {
                // Determine coaching specialty based on highest attribute
                int maxAttr = Mathf.Max(
                    profile.OffensiveScheme,
                    profile.DefensiveScheme,
                    profile.PlayerDevelopment,
                    profile.GameManagement
                );

                if (maxAttr == profile.OffensiveScheme && profile.OffensiveScheme > 60)
                    return "Offense";
                if (maxAttr == profile.DefensiveScheme && profile.DefensiveScheme > 60)
                    return "Defense";
                if (maxAttr == profile.PlayerDevelopment && profile.PlayerDevelopment > 60)
                    return "Development";
                if (maxAttr == profile.GameManagement && profile.GameManagement > 60)
                    return "Management";

                return "Balanced Coach";
            }
            else if (profile.CurrentTrack == UnifiedCareerTrack.FrontOffice)
            {
                // Determine scouting specialty based on highest attribute
                int maxAttr = Mathf.Max(
                    profile.EvaluationAccuracy,
                    profile.ProspectEvaluation,
                    profile.ProEvaluation,
                    profile.PotentialAssessment
                );

                if (maxAttr == profile.ProspectEvaluation && profile.ProspectEvaluation > 60)
                    return "Prospects";
                if (maxAttr == profile.ProEvaluation && profile.ProEvaluation > 60)
                    return "Pro Scout";
                if (maxAttr == profile.PotentialAssessment && profile.PotentialAssessment > 60)
                    return "Potential";
                if (maxAttr == profile.EvaluationAccuracy && profile.EvaluationAccuracy > 60)
                    return "Evaluation";

                return "General Scout";
            }

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
    }
}
