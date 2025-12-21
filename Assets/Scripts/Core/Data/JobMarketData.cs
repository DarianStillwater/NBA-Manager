using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Represents an open job position in the league.
    /// </summary>
    [Serializable]
    public class JobOpening
    {
        public string OpeningId;
        public string TeamId;
        public string TeamName;
        public string TeamCity;
        public UserRole Position;  // GM or HeadCoach
        public JobOpeningStatus Status;
        public DateTime PostedDate;
        public DateTime? DeadlineDate;

        // Job details
        public int OfferedSalary;
        public int ContractYears;
        public JobRequirements Requirements;
        public string JobDescription;

        // Team context
        public int TeamWins;
        public int TeamLosses;
        public string TeamSituation;  // "Rebuilding", "Contending", etc.
        public string OwnerExpectations;

        // Competition
        public List<JobCandidate> OtherCandidates = new List<JobCandidate>();
        public int EstimatedCompetition;  // 1-10 scale

        // User application
        public JobApplication UserApplication;

        public JobOpening()
        {
            OpeningId = Guid.NewGuid().ToString();
            PostedDate = DateTime.Now;
            Status = JobOpeningStatus.Open;
        }

        public string GetPositionTitle()
        {
            return Position switch
            {
                UserRole.GMOnly => "General Manager",
                UserRole.HeadCoachOnly => "Head Coach",
                _ => "Position"
            };
        }

        public string GetSalaryDisplay()
        {
            return $"${OfferedSalary / 1000000f:F1}M/year";
        }
    }

    /// <summary>
    /// Status of a job opening.
    /// </summary>
    [Serializable]
    public enum JobOpeningStatus
    {
        Open,           // Accepting applications
        Interviewing,   // In interview process
        Offered,        // Offer made (possibly to user)
        Filled,         // Position filled
        Withdrawn       // Opening withdrawn
    }

    /// <summary>
    /// Requirements for a job position.
    /// </summary>
    [Serializable]
    public class JobRequirements
    {
        public int MinExperienceYears;
        public int PreferredReputation;  // 0-100
        public bool RequiresPlayoffExperience;
        public bool RequiresChampionship;
        public string PreferredStyle;  // "Offensive-minded", "Defensive specialist", etc.
        public List<string> DesiredQualities = new List<string>();

        public string GetSummary()
        {
            string summary = "";

            if (MinExperienceYears > 0)
                summary += $"- {MinExperienceYears}+ years experience\n";
            if (RequiresPlayoffExperience)
                summary += "- Playoff experience required\n";
            if (RequiresChampionship)
                summary += "- Championship experience preferred\n";
            if (!string.IsNullOrEmpty(PreferredStyle))
                summary += $"- Looking for: {PreferredStyle}\n";

            return string.IsNullOrEmpty(summary) ? "No specific requirements" : summary;
        }
    }

    /// <summary>
    /// An AI candidate competing for a job.
    /// </summary>
    [Serializable]
    public class JobCandidate
    {
        public string CandidateId;
        public string Name;
        public string CurrentPosition;  // "Unemployed", "Assistant Coach", etc.
        public int Experience;
        public int Reputation;
        public bool IsFormerStar;  // Former player turned coach/GM
        public float FitScore;  // How well they match requirements
    }

    /// <summary>
    /// User's application to a job opening.
    /// </summary>
    [Serializable]
    public class JobApplication
    {
        public string ApplicationId;
        public string OpeningId;
        public DateTime AppliedDate;
        public JobApplicationStatus Status;
        public string CoverLetter;

        // Response from team
        public DateTime? ResponseDate;
        public string TeamResponse;
        public bool InterviewScheduled;
        public DateTime? InterviewDate;

        // Offer details (if offered)
        public bool HasOffer;
        public int OfferedSalary;
        public int OfferedYears;
        public DateTime? OfferExpiration;

        public JobApplication()
        {
            ApplicationId = Guid.NewGuid().ToString();
            AppliedDate = DateTime.Now;
            Status = JobApplicationStatus.Pending;
        }
    }

    /// <summary>
    /// Status of a job application.
    /// </summary>
    [Serializable]
    public enum JobApplicationStatus
    {
        Pending,        // Awaiting response
        Interviewing,   // In interview process
        Offered,        // Received offer
        Accepted,       // User accepted
        Declined,       // User declined
        Rejected,       // Team rejected
        Withdrawn       // User withdrew
    }

    /// <summary>
    /// Reason the user was fired.
    /// </summary>
    [Serializable]
    public enum FiringReason
    {
        PoorRecord,         // Team underperformed
        MissedPlayoffs,     // Failed to make playoffs
        EarlyPlayoffExit,   // Lost in first round
        PlayerConflicts,    // Issues with star players
        OwnerDecision,      // Owner just wanted change
        Scandal,            // Off-court issues
        Mutual,             // Mutual parting
        BudgetCuts          // Organization restructuring
    }

    /// <summary>
    /// Details about being fired from a position.
    /// </summary>
    [Serializable]
    public class FiringDetails
    {
        public string TeamId;
        public string TeamName;
        public UserRole Position;
        public DateTime FiringDate;
        public FiringReason Reason;
        public string PublicStatement;
        public string PrivateReason;
        public int SeveranceMonths;
        public int SeveranceAmount;

        // Impact on reputation
        public int ReputationChange;  // Usually negative

        public string GetReasonDisplay()
        {
            return Reason switch
            {
                FiringReason.PoorRecord => "Poor team performance",
                FiringReason.MissedPlayoffs => "Failed to make playoffs",
                FiringReason.EarlyPlayoffExit => "Disappointing playoff run",
                FiringReason.PlayerConflicts => "Disagreements with players",
                FiringReason.OwnerDecision => "Ownership decided to make a change",
                FiringReason.Scandal => "Off-court issues",
                FiringReason.Mutual => "Mutual agreement to part ways",
                FiringReason.BudgetCuts => "Organization restructuring",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Unsolicited job offer from another team.
    /// </summary>
    [Serializable]
    public class UnsolicitedOffer
    {
        public string OfferId;
        public JobOpening Job;
        public DateTime OfferDate;
        public DateTime ExpirationDate;
        public string PersonalMessage;
        public bool IsUpgrade;  // Better than current position

        public UnsolicitedOffer()
        {
            OfferId = Guid.NewGuid().ToString();
            OfferDate = DateTime.Now;
            ExpirationDate = DateTime.Now.AddDays(7);
        }
    }

    /// <summary>
    /// Complete job market state.
    /// </summary>
    [Serializable]
    public class JobMarketState
    {
        // Open positions
        public List<JobOpening> OpenPositions = new List<JobOpening>();
        public List<JobApplication> UserApplications = new List<JobApplication>();
        public List<UnsolicitedOffer> UnsolicitedOffers = new List<UnsolicitedOffer>();

        // If user is currently unemployed
        public bool IsUnemployed;
        public FiringDetails LastFiring;
        public DateTime? UnemployedSince;

        // Market activity
        public int CurrentSeason;
        public DateTime LastUpdated;

        // Stats
        public int TotalApplications;
        public int TotalInterviews;
        public int TotalOffers;
        public int TotalRejections;

        public void AddOpening(JobOpening opening)
        {
            OpenPositions.Add(opening);
        }

        public void RemoveOpening(string openingId)
        {
            OpenPositions.RemoveAll(o => o.OpeningId == openingId);
        }

        public List<JobOpening> GetOpenings(UserRole? roleFilter = null)
        {
            if (roleFilter == null)
                return OpenPositions.FindAll(o => o.Status == JobOpeningStatus.Open);

            return OpenPositions.FindAll(o =>
                o.Status == JobOpeningStatus.Open &&
                o.Position == roleFilter.Value);
        }

        public JobApplication GetApplication(string openingId)
        {
            return UserApplications.Find(a => a.OpeningId == openingId);
        }
    }

    /// <summary>
    /// Summary for displaying in job market UI.
    /// </summary>
    [Serializable]
    public class JobMarketSummary
    {
        public int TotalOpenings;
        public int GMOpenings;
        public int CoachOpenings;
        public int PendingApplications;
        public int ActiveOffers;
        public List<string> HotTeams;  // Teams actively looking
        public string MarketStatus;  // "Active", "Slow", etc.
    }
}
