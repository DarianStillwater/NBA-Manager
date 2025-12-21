using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages job market, firings, and career transitions.
    /// Handles the job search phase when user is fired.
    /// </summary>
    public class JobMarketManager : MonoBehaviour
    {
        private static JobMarketManager _instance;
        public static JobMarketManager Instance => _instance;

        [Header("Market Settings")]
        [SerializeField] private int _minOpeningsPerSeason = 5;
        [SerializeField] private int _maxOpeningsPerSeason = 12;

        [Header("State")]
        [SerializeField] private JobMarketState _marketState;

        private System.Random _rng;

        // Events
        public event Action<FiringDetails> OnPlayerFired;
        public event Action<JobOpening> OnJobOffered;
        public event Action<string, string> OnJobAccepted;  // teamId, position

        public JobMarketState MarketState => _marketState;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _rng = new System.Random();
            _marketState = new JobMarketState();
        }

        private void Start()
        {
            // Register with GameManager
            GameManager.Instance?.RegisterJobMarketManager(this);
        }

        #region Player Firing

        /// <summary>
        /// Handle the player being fired from their current position.
        /// Linked fates - losing one role means losing both.
        /// </summary>
        public void HandlePlayerFired(FiringReason reason, string publicStatement = null)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            var config = gm.UserRoleConfig;
            if (config == null) return;

            var firing = new FiringDetails
            {
                TeamId = config.TeamId,
                TeamName = gm.GetTeam(config.TeamId)?.Name ?? "Team",
                Position = config.CurrentRole,
                FiringDate = gm.CurrentDate,
                Reason = reason,
                PublicStatement = publicStatement ?? GenerateFiringStatement(reason),
                PrivateReason = GetPrivateReason(reason),
                SeveranceMonths = CalculateSeverance(gm.Career),
                ReputationChange = CalculateReputationImpact(reason)
            };

            firing.SeveranceAmount = firing.SeveranceMonths * (gm.Career?.CurrentSalary ?? 0) / 12;

            // Apply reputation change
            if (gm.Career != null)
            {
                gm.Career.Reputation = Mathf.Clamp(gm.Career.Reputation + firing.ReputationChange, 0, 100);
            }

            // Update market state
            _marketState.IsUnemployed = true;
            _marketState.LastFiring = firing;
            _marketState.UnemployedSince = gm.CurrentDate;

            // Generate initial job openings
            GenerateSeasonOpenings(gm.CurrentSeason);

            // Fire event
            OnPlayerFired?.Invoke(firing);

            Debug.Log($"[JobMarketManager] Player fired from {firing.TeamName}. Reason: {firing.Reason}. " +
                     $"Reputation change: {firing.ReputationChange}");

            // Transition to job market state
            gm.ChangeState(GameState.JobMarket, firing);
        }

        private string GenerateFiringStatement(FiringReason reason)
        {
            return reason switch
            {
                FiringReason.PoorRecord => "We've decided to go in a different direction. We thank them for their efforts.",
                FiringReason.MissedPlayoffs => "The results weren't what we expected. We're making a change to get back on track.",
                FiringReason.EarlyPlayoffExit => "After a disappointing postseason, we've decided a fresh start is needed.",
                FiringReason.PlayerConflicts => "Sometimes things don't work out. We wish them well in their future endeavors.",
                FiringReason.OwnerDecision => "Ownership has decided to make changes to the leadership structure.",
                FiringReason.Mutual => "We've mutually agreed to part ways. This is the best decision for both parties.",
                _ => "We're making a change in leadership."
            };
        }

        private string GetPrivateReason(FiringReason reason)
        {
            return reason switch
            {
                FiringReason.PoorRecord => "The owner lost patience with the losing record",
                FiringReason.MissedPlayoffs => "Missing the playoffs was unacceptable to ownership",
                FiringReason.PlayerConflicts => "Star players demanded a change",
                FiringReason.OwnerDecision => "New ownership wanted their own people",
                _ => "It was time for a change"
            };
        }

        private int CalculateSeverance(UnifiedCareerProfile career)
        {
            if (career == null) return 0;

            // Remaining contract months (simplified)
            return Math.Min(career.ContractYears * 12, 24);
        }

        private int CalculateReputationImpact(FiringReason reason)
        {
            return reason switch
            {
                FiringReason.PoorRecord => -15,
                FiringReason.MissedPlayoffs => -10,
                FiringReason.EarlyPlayoffExit => -5,
                FiringReason.PlayerConflicts => -12,
                FiringReason.OwnerDecision => -3,
                FiringReason.Scandal => -25,
                FiringReason.Mutual => -2,
                FiringReason.BudgetCuts => 0,
                _ => -5
            };
        }

        #endregion

        #region Job Openings

        /// <summary>
        /// Generate job openings for a season.
        /// </summary>
        public void GenerateSeasonOpenings(int season)
        {
            _marketState.CurrentSeason = season;
            _marketState.OpenPositions.Clear();

            // Determine number of openings
            int numOpenings = _rng.Next(_minOpeningsPerSeason, _maxOpeningsPerSeason + 1);

            // Get all teams
            var allTeams = GameManager.Instance?.GetAllTeams() ?? new List<Team>();
            var shuffledTeams = allTeams.OrderBy(t => _rng.Next()).ToList();

            // Generate mix of GM and Coach openings
            for (int i = 0; i < numOpenings && i < shuffledTeams.Count; i++)
            {
                var team = shuffledTeams[i];
                var position = _rng.Next(100) < 60 ? UserRole.HeadCoachOnly : UserRole.GMOnly;

                var opening = CreateOpening(team, position);
                _marketState.AddOpening(opening);
            }

            _marketState.LastUpdated = DateTime.Now;

            Debug.Log($"[JobMarketManager] Generated {numOpenings} job openings for season {season}");
        }

        private JobOpening CreateOpening(Team team, UserRole position)
        {
            var opening = new JobOpening
            {
                TeamId = team.TeamId,
                TeamName = team.Name,
                TeamCity = team.City,
                Position = position,
                TeamWins = team.Wins,
                TeamLosses = team.Losses,
                Status = JobOpeningStatus.Open
            };

            // Set salary based on position and team
            opening.OfferedSalary = position == UserRole.GMOnly
                ? 2_500_000 + _rng.Next(2_000_000)
                : 3_000_000 + _rng.Next(4_000_000);

            opening.ContractYears = 3 + _rng.Next(3);  // 3-5 years

            // Team situation
            float winPct = team.TotalGames > 0 ? (float)team.Wins / team.TotalGames : 0.5f;
            opening.TeamSituation = winPct switch
            {
                >= 0.6f => "Contending",
                >= 0.5f => "Competitive",
                >= 0.4f => "Rebuilding",
                _ => "Full Rebuild"
            };

            // Owner expectations
            opening.OwnerExpectations = opening.TeamSituation switch
            {
                "Contending" => "Championship or bust",
                "Competitive" => "Make the playoffs",
                "Rebuilding" => "Show improvement",
                _ => "Patient development"
            };

            // Requirements
            opening.Requirements = new JobRequirements
            {
                MinExperienceYears = winPct >= 0.5f ? 3 : 0,
                PreferredReputation = winPct >= 0.6f ? 60 : 40,
                RequiresPlayoffExperience = winPct >= 0.55f,
                RequiresChampionship = winPct >= 0.65f,
                PreferredStyle = _rng.Next(100) < 50 ? "Offensive-minded" : "Defensive-focused"
            };

            // Job description
            opening.JobDescription = GenerateJobDescription(opening);

            // Generate AI candidates
            opening.OtherCandidates = GenerateCandidates(opening);
            opening.EstimatedCompetition = Math.Min(10, opening.OtherCandidates.Count + _rng.Next(3));

            return opening;
        }

        private string GenerateJobDescription(JobOpening opening)
        {
            string roleDesc = opening.Position == UserRole.GMOnly
                ? "lead all basketball operations, including trades, draft, and player personnel decisions"
                : "lead the team on the court, develop players, and compete for championships";

            return $"The {opening.TeamCity} {opening.TeamName} are seeking a {opening.GetPositionTitle()} to {roleDesc}. " +
                   $"Current situation: {opening.TeamSituation}. " +
                   $"Owner expectations: {opening.OwnerExpectations}.";
        }

        private List<JobCandidate> GenerateCandidates(JobOpening opening)
        {
            var candidates = new List<JobCandidate>();
            int numCandidates = 3 + _rng.Next(5);

            var names = new[] {
                "Mike Brown", "John Davis", "Chris Johnson", "David Williams",
                "James Wilson", "Robert Miller", "Michael Anderson", "William Taylor"
            };

            for (int i = 0; i < numCandidates; i++)
            {
                candidates.Add(new JobCandidate
                {
                    CandidateId = Guid.NewGuid().ToString(),
                    Name = names[_rng.Next(names.Length)],
                    CurrentPosition = _rng.Next(100) < 40 ? "Assistant Coach" : "Unemployed",
                    Experience = _rng.Next(2, 15),
                    Reputation = 30 + _rng.Next(50),
                    IsFormerStar = _rng.Next(100) < 20,
                    FitScore = 0.3f + (float)_rng.NextDouble() * 0.6f
                });
            }

            return candidates;
        }

        #endregion

        #region Applications

        /// <summary>
        /// Apply for a job opening.
        /// </summary>
        public JobApplication ApplyForJob(string openingId, string coverLetter = null)
        {
            var opening = _marketState.OpenPositions.Find(o => o.OpeningId == openingId);
            if (opening == null || opening.Status != JobOpeningStatus.Open)
            {
                Debug.LogWarning($"[JobMarketManager] Cannot apply - opening not found or closed");
                return null;
            }

            // Check if already applied
            if (_marketState.UserApplications.Any(a => a.OpeningId == openingId))
            {
                Debug.LogWarning($"[JobMarketManager] Already applied to this position");
                return null;
            }

            var application = new JobApplication
            {
                OpeningId = openingId,
                CoverLetter = coverLetter
            };

            _marketState.UserApplications.Add(application);
            _marketState.TotalApplications++;

            opening.UserApplication = application;

            Debug.Log($"[JobMarketManager] Applied to {opening.TeamName} {opening.GetPositionTitle()}");

            // Process application (immediate response for now)
            ProcessApplication(application, opening);

            return application;
        }

        private void ProcessApplication(JobApplication application, JobOpening opening)
        {
            var career = GameManager.Instance?.Career;
            float hireChance = CalculateHireChance(career, opening);

            // 3 possible outcomes: Reject, Interview, or Direct Offer
            float roll = (float)_rng.NextDouble();

            if (roll < hireChance * 0.3f)
            {
                // Direct offer (rare for good fits)
                application.Status = JobApplicationStatus.Offered;
                application.HasOffer = true;
                application.OfferedSalary = opening.OfferedSalary;
                application.OfferedYears = opening.ContractYears;
                application.OfferExpiration = DateTime.Now.AddDays(7);
                application.TeamResponse = "We're impressed with your credentials and want to offer you the position.";
                _marketState.TotalOffers++;

                OnJobOffered?.Invoke(opening);
            }
            else if (roll < hireChance)
            {
                // Interview
                application.Status = JobApplicationStatus.Interviewing;
                application.InterviewScheduled = true;
                application.InterviewDate = DateTime.Now.AddDays(3);
                application.TeamResponse = "We'd like to schedule an interview to discuss the position.";
                _marketState.TotalInterviews++;
            }
            else
            {
                // Rejected
                application.Status = JobApplicationStatus.Rejected;
                application.TeamResponse = "We appreciate your interest but have decided to pursue other candidates.";
                _marketState.TotalRejections++;
            }

            application.ResponseDate = DateTime.Now;
        }

        private float CalculateHireChance(UnifiedCareerProfile career, JobOpening opening)
        {
            if (career == null) return 0.1f;

            float chance = 0.3f;  // Base chance

            // Reputation match
            float repDiff = career.Reputation - opening.Requirements.PreferredReputation;
            chance += repDiff * 0.005f;

            // Experience
            if (career.TotalSeasons >= opening.Requirements.MinExperienceYears)
                chance += 0.15f;

            // Playoff experience
            if (opening.Requirements.RequiresPlayoffExperience && career.PlayoffAppearances > 0)
                chance += 0.1f;

            // Championship
            if (opening.Requirements.RequiresChampionship && career.Championships > 0)
                chance += 0.15f;

            // Team situation affects standards
            chance += opening.TeamSituation switch
            {
                "Contending" => -0.1f,  // Higher standards
                "Full Rebuild" => 0.15f, // Lower standards
                _ => 0f
            };

            return Mathf.Clamp01(chance);
        }

        #endregion

        #region Accept/Decline Jobs

        /// <summary>
        /// Accept a job offer.
        /// </summary>
        public bool AcceptJob(string openingId)
        {
            var application = _marketState.UserApplications.Find(a => a.OpeningId == openingId);
            if (application == null || !application.HasOffer)
            {
                Debug.LogWarning("[JobMarketManager] No offer to accept");
                return false;
            }

            var opening = _marketState.OpenPositions.Find(o => o.OpeningId == openingId);
            if (opening == null) return false;

            application.Status = JobApplicationStatus.Accepted;
            opening.Status = JobOpeningStatus.Filled;

            // Clear unemployed status
            _marketState.IsUnemployed = false;
            _marketState.UnemployedSince = null;

            // Fire event
            OnJobAccepted?.Invoke(opening.TeamId, opening.GetPositionTitle());

            Debug.Log($"[JobMarketManager] Accepted job: {opening.TeamName} {opening.GetPositionTitle()}");

            // GameManager will handle the actual role change
            return true;
        }

        /// <summary>
        /// Decline a job offer.
        /// </summary>
        public void DeclineJob(string openingId)
        {
            var application = _marketState.UserApplications.Find(a => a.OpeningId == openingId);
            if (application != null)
            {
                application.Status = JobApplicationStatus.Declined;
                application.HasOffer = false;
            }
        }

        /// <summary>
        /// Withdraw an application.
        /// </summary>
        public void WithdrawApplication(string openingId)
        {
            var application = _marketState.UserApplications.Find(a => a.OpeningId == openingId);
            if (application != null)
            {
                application.Status = JobApplicationStatus.Withdrawn;
            }
        }

        #endregion

        #region Unsolicited Offers

        /// <summary>
        /// Generate an unsolicited job offer (called periodically).
        /// </summary>
        public void CheckForUnsolicitedOffers()
        {
            var career = GameManager.Instance?.Career;
            if (career == null) return;

            // Higher reputation = more likely to get offers
            float offerChance = career.Reputation / 500f;  // Max 20% with 100 rep

            if (_rng.NextDouble() < offerChance)
            {
                GenerateUnsolicitedOffer();
            }
        }

        private void GenerateUnsolicitedOffer()
        {
            var allTeams = GameManager.Instance?.GetAllTeams() ?? new List<Team>();
            var currentTeamId = GameManager.Instance?.PlayerTeamId;

            // Pick a random team that's not the current one
            var availableTeams = allTeams.Where(t => t.TeamId != currentTeamId).ToList();
            if (availableTeams.Count == 0) return;

            var team = availableTeams[_rng.Next(availableTeams.Count)];
            var position = _rng.Next(100) < 60 ? UserRole.HeadCoachOnly : UserRole.GMOnly;

            var opening = CreateOpening(team, position);
            opening.Status = JobOpeningStatus.Offered;

            var offer = new UnsolicitedOffer
            {
                Job = opening,
                PersonalMessage = $"We've been impressed by your work and would like to discuss bringing you to {team.City}.",
                IsUpgrade = opening.OfferedSalary > (GameManager.Instance?.Career?.CurrentSalary ?? 0)
            };

            _marketState.UnsolicitedOffers.Add(offer);

            Debug.Log($"[JobMarketManager] Received unsolicited offer from {team.Name}");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Get summary of current job market.
        /// </summary>
        public JobMarketSummary GetMarketSummary()
        {
            var openings = _marketState.GetOpenings();

            return new JobMarketSummary
            {
                TotalOpenings = openings.Count,
                GMOpenings = openings.Count(o => o.Position == UserRole.GMOnly),
                CoachOpenings = openings.Count(o => o.Position == UserRole.HeadCoachOnly),
                PendingApplications = _marketState.UserApplications.Count(a =>
                    a.Status == JobApplicationStatus.Pending || a.Status == JobApplicationStatus.Interviewing),
                ActiveOffers = _marketState.UserApplications.Count(a => a.HasOffer),
                MarketStatus = openings.Count >= 8 ? "Active" : openings.Count >= 4 ? "Moderate" : "Slow"
            };
        }

        /// <summary>
        /// Advance day in job market (process interviews, expire offers, etc.)
        /// </summary>
        public void AdvanceDay(DateTime currentDate)
        {
            // Expire old offers
            foreach (var app in _marketState.UserApplications)
            {
                if (app.HasOffer && app.OfferExpiration.HasValue && currentDate > app.OfferExpiration.Value)
                {
                    app.HasOffer = false;
                    app.Status = JobApplicationStatus.Rejected;
                    app.TeamResponse = "The offer has expired.";
                }
            }

            // Remove expired unsolicited offers
            _marketState.UnsolicitedOffers.RemoveAll(o => currentDate > o.ExpirationDate);

            // AI fills some positions over time
            foreach (var opening in _marketState.OpenPositions.Where(o => o.Status == JobOpeningStatus.Open))
            {
                if (_rng.Next(100) < 3)  // 3% daily chance position gets filled
                {
                    opening.Status = JobOpeningStatus.Filled;
                }
            }
        }

        #endregion
    }
}
