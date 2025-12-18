using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Status of a job opening
    /// </summary>
    public enum JobOpeningStatus
    {
        Open,               // Accepting applications
        InterviewPhase,     // Conducting interviews
        FinalCandidates,    // Down to final choices
        Filled,             // Position filled
        Withdrawn           // Team decided to keep current coach
    }

    /// <summary>
    /// Represents a coaching job opening
    /// </summary>
    [Serializable]
    public class CoachingJobOpening
    {
        public string JobId;
        public string TeamId;
        public string TeamName;
        public string TeamCity;

        [Header("Position Details")]
        public bool IsHeadCoachPosition;
        public bool IsInterimPosition;
        public DateTime PostedDate;
        public DateTime Deadline;
        public JobOpeningStatus Status;

        [Header("Team Context")]
        public int MarketSize;              // 1-30, 1 is largest
        public int LastSeasonWins;
        public int LastSeasonLosses;
        public bool MadePlayoffsLastYear;
        public int ProjectedWins;           // Expected wins for next season
        public string TeamSituation;        // "Rebuilding", "Contending", etc.

        [Header("Compensation")]
        public long MinSalary;
        public long MaxSalary;
        public int ContractYearsOffered;

        [Header("Requirements")]
        public int MinExperience;           // Years of coaching required
        public int MinReputation;           // Minimum reputation score
        public bool PrefersDevelopmentCoach;
        public bool PrefersVeteranCoach;
        public bool WantsChampionshipPedigree;

        [Header("Selection Process")]
        public List<string> ApplicantCoachIds;
        public List<string> InterviewedCoachIds;
        public List<string> FinalistCoachIds;
        public string SelectedCoachId;

        /// <summary>
        /// Calculate job attractiveness to coaches
        /// </summary>
        public int GetJobAttractiveness()
        {
            int score = 50;

            // Market size (bigger markets more attractive)
            score += (31 - MarketSize);

            // Winning situation more attractive
            score += ProjectedWins / 3;

            // Salary
            if (MaxSalary >= 10_000_000) score += 15;
            else if (MaxSalary >= 7_000_000) score += 10;
            else if (MaxSalary >= 5_000_000) score += 5;

            // Contract length
            score += ContractYearsOffered * 3;

            return Mathf.Clamp(score, 20, 100);
        }

        /// <summary>
        /// Check if a coach meets minimum requirements
        /// </summary>
        public bool MeetsRequirements(CoachCareer coach)
        {
            if (coach.YearsCoaching < MinExperience) return false;
            if (coach.Reputation < MinReputation) return false;
            if (WantsChampionshipPedigree && coach.Championships == 0) return false;

            return true;
        }

        /// <summary>
        /// Calculate how interested the team is in a specific coach
        /// </summary>
        public int GetTeamInterest(CoachCareer coach)
        {
            if (!MeetsRequirements(coach)) return 0;

            int interest = 50;

            // Experience bonus
            interest += Math.Min(coach.YearsCoaching * 2, 20);

            // Win percentage
            interest += (int)(coach.CareerWinPercentage * 30);

            // Championships
            interest += coach.Championships * 15;

            // Reputation
            interest += (coach.Reputation - 50) / 3;

            // Match with team preferences
            if (PrefersDevelopmentCoach && coach.DevelopmentReputation > 70)
                interest += 10;
            if (PrefersVeteranCoach && coach.YearsCoaching >= 10)
                interest += 10;

            // Penalty for recent firing
            if (coach.LastFired != DateTime.MinValue && (DateTime.Now - coach.LastFired).TotalDays < 180)
                interest -= 20;

            // Former player bonus - teams like hiring former players
            if (coach.IsFormerPlayer)
            {
                interest += 10;

                // Extra bonus if they played for this team
                if (coach.FormerTeams != null && coach.FormerTeams.Contains(TeamId))
                {
                    interest += 15;
                }
            }

            return Mathf.Clamp(interest, 0, 100);
        }
    }

    /// <summary>
    /// Application submitted by the player
    /// </summary>
    [Serializable]
    public class JobApplication
    {
        public string ApplicationId;
        public string JobId;
        public string CoachId;
        public DateTime SubmittedDate;

        public JobApplicationStatus Status;
        public string CoverLetter;          // Player's pitch

        public bool WasInterviewed;
        public int InterviewScore;          // How well the interview went
        public string InterviewFeedback;

        public bool ReceivedOffer;
        public CoachContract OfferedContract;
    }

    /// <summary>
    /// Status of a job application
    /// </summary>
    public enum JobApplicationStatus
    {
        Submitted,
        UnderReview,
        InterviewScheduled,
        InterviewCompleted,
        Finalist,
        OfferExtended,
        OfferAccepted,
        Rejected,
        Withdrawn
    }

    /// <summary>
    /// Unsolicited job offer from a team
    /// </summary>
    [Serializable]
    public class UnsolicitedJobOffer
    {
        public string OfferId;
        public string TeamId;
        public string TeamName;
        public string FromPersonName;       // GM or Owner name

        public DateTime OfferDate;
        public DateTime ExpirationDate;

        public CoachContract ProposedContract;
        public string Message;              // Why they want you

        public UnsolicitedOfferStatus Status;
        public bool IsPoaching;             // Trying to take you from current job
    }

    /// <summary>
    /// Status of unsolicited offer
    /// </summary>
    public enum UnsolicitedOfferStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired,
        CounterOffered
    }

    /// <summary>
    /// Interview event for job application
    /// </summary>
    [Serializable]
    public class CoachInterview
    {
        public string InterviewId;
        public string JobId;
        public string CoachId;
        public string TeamId;

        public DateTime ScheduledDate;
        public bool IsCompleted;

        public List<InterviewQuestion> Questions;
        public int OverallScore;
        public string TeamImpression;
    }

    /// <summary>
    /// Question asked during interview
    /// </summary>
    [Serializable]
    public class InterviewQuestion
    {
        public string QuestionId;
        public string Question;
        public List<InterviewAnswer> AnswerOptions;
        public InterviewAnswer SelectedAnswer;
    }

    /// <summary>
    /// Possible answer to interview question
    /// </summary>
    [Serializable]
    public class InterviewAnswer
    {
        public string AnswerId;
        public string AnswerText;
        public int ScoreImpact;             // How much this affects interview score
        public string TeamReaction;         // What the team thought
    }

    /// <summary>
    /// Manages the coaching job market
    /// </summary>
    public class CoachJobMarketManager
    {
        private Dictionary<string, CoachingJobOpening> _openPositions;
        private Dictionary<string, List<JobApplication>> _applications;
        private Dictionary<string, List<UnsolicitedJobOffer>> _unsolicitedOffers;
        private Dictionary<string, CoachInterview> _scheduledInterviews;

        public event Action<CoachingJobOpening> OnJobOpened;
        public event Action<CoachingJobOpening> OnJobFilled;
        public event Action<JobApplication> OnApplicationStatusChanged;
        public event Action<UnsolicitedJobOffer> OnOfferReceived;
        public event Action<CoachInterview> OnInterviewScheduled;

        public CoachJobMarketManager()
        {
            _openPositions = new Dictionary<string, CoachingJobOpening>();
            _applications = new Dictionary<string, List<JobApplication>>();
            _unsolicitedOffers = new Dictionary<string, List<UnsolicitedJobOffer>>();
            _scheduledInterviews = new Dictionary<string, CoachInterview>();
        }

        // === Job Openings ===

        /// <summary>
        /// Post a new coaching job opening
        /// </summary>
        public CoachingJobOpening PostJobOpening(
            string teamId,
            string teamName,
            string teamCity,
            int marketSize,
            int lastWins,
            int lastLosses,
            bool madePlayoffs,
            int projectedWins,
            string situation,
            long minSalary,
            long maxSalary,
            int yearsOffered,
            bool isInterim = false)
        {
            var opening = new CoachingJobOpening
            {
                JobId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                TeamName = teamName,
                TeamCity = teamCity,
                IsHeadCoachPosition = true,
                IsInterimPosition = isInterim,
                PostedDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(isInterim ? 7 : 21),
                Status = JobOpeningStatus.Open,
                MarketSize = marketSize,
                LastSeasonWins = lastWins,
                LastSeasonLosses = lastLosses,
                MadePlayoffsLastYear = madePlayoffs,
                ProjectedWins = projectedWins,
                TeamSituation = situation,
                MinSalary = minSalary,
                MaxSalary = maxSalary,
                ContractYearsOffered = yearsOffered,
                MinExperience = isInterim ? 0 : 2,
                MinReputation = isInterim ? 30 : 40,
                ApplicantCoachIds = new List<string>(),
                InterviewedCoachIds = new List<string>(),
                FinalistCoachIds = new List<string>()
            };

            // Set preferences based on situation
            switch (situation)
            {
                case "Rebuilding":
                    opening.PrefersDevelopmentCoach = true;
                    opening.WantsChampionshipPedigree = false;
                    break;
                case "Contending":
                    opening.PrefersVeteranCoach = true;
                    opening.WantsChampionshipPedigree = true;
                    break;
            }

            _openPositions[opening.JobId] = opening;
            OnJobOpened?.Invoke(opening);

            Debug.Log($"[JobMarket] Job posted: {teamName} Head Coach");

            return opening;
        }

        /// <summary>
        /// Get all open positions
        /// </summary>
        public List<CoachingJobOpening> GetOpenPositions()
        {
            return _openPositions.Values
                .Where(j => j.Status == JobOpeningStatus.Open || j.Status == JobOpeningStatus.InterviewPhase)
                .OrderByDescending(j => j.GetJobAttractiveness())
                .ToList();
        }

        /// <summary>
        /// Get positions the coach qualifies for
        /// </summary>
        public List<CoachingJobOpening> GetQualifiedOpenings(CoachCareer coach)
        {
            return GetOpenPositions()
                .Where(j => j.MeetsRequirements(coach))
                .ToList();
        }

        // === Applications ===

        /// <summary>
        /// Apply for a coaching position
        /// </summary>
        public JobApplication ApplyForJob(string jobId, CoachCareer coach, string coverLetter)
        {
            if (!_openPositions.TryGetValue(jobId, out var opening))
                return null;

            if (opening.Status != JobOpeningStatus.Open)
                return null;

            if (!opening.MeetsRequirements(coach))
                return null;

            var application = new JobApplication
            {
                ApplicationId = Guid.NewGuid().ToString(),
                JobId = jobId,
                CoachId = coach.CoachId,
                SubmittedDate = DateTime.Now,
                Status = JobApplicationStatus.Submitted,
                CoverLetter = coverLetter,
                WasInterviewed = false
            };

            opening.ApplicantCoachIds.Add(coach.CoachId);

            if (!_applications.ContainsKey(coach.CoachId))
            {
                _applications[coach.CoachId] = new List<JobApplication>();
            }
            _applications[coach.CoachId].Add(application);

            Debug.Log($"[JobMarket] {coach.FullName} applied for {opening.TeamName} position");

            // Process application
            ProcessApplication(application, opening, coach);

            return application;
        }

        /// <summary>
        /// Process a job application
        /// </summary>
        private void ProcessApplication(JobApplication app, CoachingJobOpening opening, CoachCareer coach)
        {
            int teamInterest = opening.GetTeamInterest(coach);

            // High interest = schedule interview
            if (teamInterest >= 60)
            {
                app.Status = JobApplicationStatus.InterviewScheduled;
                ScheduleInterview(app, opening, coach);
            }
            else if (teamInterest >= 40)
            {
                app.Status = JobApplicationStatus.UnderReview;
                // May still get interview later
            }
            else
            {
                app.Status = JobApplicationStatus.Rejected;
                app.InterviewFeedback = "Thank you for your interest, but we've decided to pursue other candidates.";
            }

            OnApplicationStatusChanged?.Invoke(app);
        }

        /// <summary>
        /// Schedule an interview
        /// </summary>
        private CoachInterview ScheduleInterview(JobApplication app, CoachingJobOpening opening, CoachCareer coach)
        {
            var interview = new CoachInterview
            {
                InterviewId = Guid.NewGuid().ToString(),
                JobId = opening.JobId,
                CoachId = coach.CoachId,
                TeamId = opening.TeamId,
                ScheduledDate = DateTime.Now.AddDays(UnityEngine.Random.Range(2, 5)),
                IsCompleted = false,
                Questions = GenerateInterviewQuestions(opening)
            };

            _scheduledInterviews[interview.InterviewId] = interview;
            opening.InterviewedCoachIds.Add(coach.CoachId);

            OnInterviewScheduled?.Invoke(interview);

            return interview;
        }

        /// <summary>
        /// Complete an interview
        /// </summary>
        public void CompleteInterview(string interviewId, Dictionary<string, string> answers)
        {
            if (!_scheduledInterviews.TryGetValue(interviewId, out var interview))
                return;

            interview.IsCompleted = true;
            int totalScore = 0;

            foreach (var question in interview.Questions)
            {
                if (answers.TryGetValue(question.QuestionId, out var answerId))
                {
                    var answer = question.AnswerOptions.Find(a => a.AnswerId == answerId);
                    if (answer != null)
                    {
                        question.SelectedAnswer = answer;
                        totalScore += answer.ScoreImpact;
                    }
                }
            }

            interview.OverallScore = totalScore;

            // Update application status
            var apps = GetApplicationsForCoach(interview.CoachId);
            var app = apps?.Find(a => a.JobId == interview.JobId);
            if (app != null)
            {
                app.WasInterviewed = true;
                app.InterviewScore = totalScore;
                app.InterviewFeedback = GenerateInterviewFeedback(totalScore);

                if (totalScore >= 70)
                {
                    app.Status = JobApplicationStatus.Finalist;
                    _openPositions[interview.JobId].FinalistCoachIds.Add(interview.CoachId);
                }
                else
                {
                    app.Status = JobApplicationStatus.InterviewCompleted;
                }

                OnApplicationStatusChanged?.Invoke(app);
            }
        }

        /// <summary>
        /// Generate interview questions based on job
        /// </summary>
        private List<InterviewQuestion> GenerateInterviewQuestions(CoachingJobOpening opening)
        {
            var questions = new List<InterviewQuestion>();

            // Philosophy question
            questions.Add(new InterviewQuestion
            {
                QuestionId = Guid.NewGuid().ToString(),
                Question = "What's your coaching philosophy?",
                AnswerOptions = new List<InterviewAnswer>
                {
                    new InterviewAnswer
                    {
                        AnswerId = "1",
                        AnswerText = "Defense wins championships. I prioritize defensive excellence.",
                        ScoreImpact = opening.TeamSituation == "Contending" ? 25 : 15,
                        TeamReaction = "Interesting approach..."
                    },
                    new InterviewAnswer
                    {
                        AnswerId = "2",
                        AnswerText = "I believe in player development and building for sustained success.",
                        ScoreImpact = opening.PrefersDevelopmentCoach ? 25 : 15,
                        TeamReaction = "We appreciate that perspective."
                    },
                    new InterviewAnswer
                    {
                        AnswerId = "3",
                        AnswerText = "I adapt my system to the players we have.",
                        ScoreImpact = 20,
                        TeamReaction = "Flexibility is important."
                    },
                    new InterviewAnswer
                    {
                        AnswerId = "4",
                        AnswerText = "High-paced, modern offense with analytics integration.",
                        ScoreImpact = 18,
                        TeamReaction = "The game has evolved."
                    }
                }
            });

            // Situation-specific question
            if (opening.TeamSituation == "Rebuilding")
            {
                questions.Add(new InterviewQuestion
                {
                    QuestionId = Guid.NewGuid().ToString(),
                    Question = "How do you handle a rebuilding situation?",
                    AnswerOptions = new List<InterviewAnswer>
                    {
                        new InterviewAnswer
                        {
                            AnswerId = "1",
                            AnswerText = "Focus on developing young players while remaining competitive.",
                            ScoreImpact = 25,
                            TeamReaction = "That's exactly what we need."
                        },
                        new InterviewAnswer
                        {
                            AnswerId = "2",
                            AnswerText = "Be patient, trust the process, and build a winning culture.",
                            ScoreImpact = 20,
                            TeamReaction = "Culture is important to us."
                        },
                        new InterviewAnswer
                        {
                            AnswerId = "3",
                            AnswerText = "I expect to win now. Rebuilding isn't in my vocabulary.",
                            ScoreImpact = 5,
                            TeamReaction = "That might not align with our timeline..."
                        }
                    }
                });
            }
            else if (opening.TeamSituation == "Contending")
            {
                questions.Add(new InterviewQuestion
                {
                    QuestionId = Guid.NewGuid().ToString(),
                    Question = "What does it take to win a championship?",
                    AnswerOptions = new List<InterviewAnswer>
                    {
                        new InterviewAnswer
                        {
                            AnswerId = "1",
                            AnswerText = "Elite defense, experienced players, and the ability to close games.",
                            ScoreImpact = 25,
                            TeamReaction = "You clearly understand what it takes."
                        },
                        new InterviewAnswer
                        {
                            AnswerId = "2",
                            AnswerText = "Managing egos, keeping players healthy, and peaking at the right time.",
                            ScoreImpact = 22,
                            TeamReaction = "Managing a contender is an art."
                        },
                        new InterviewAnswer
                        {
                            AnswerId = "3",
                            AnswerText = "I've won before and I know how to get there again.",
                            ScoreImpact = WantsChampionshipPedigreeCheck(opening) ? 20 : 10,
                            TeamReaction = "Experience matters."
                        }
                    }
                });
            }

            // Player management question
            questions.Add(new InterviewQuestion
            {
                QuestionId = Guid.NewGuid().ToString(),
                Question = "How do you handle star players?",
                AnswerOptions = new List<InterviewAnswer>
                {
                    new InterviewAnswer
                    {
                        AnswerId = "1",
                        AnswerText = "Open communication, mutual respect, and involving them in decisions.",
                        ScoreImpact = 22,
                        TeamReaction = "Communication is key."
                    },
                    new InterviewAnswer
                    {
                        AnswerId = "2",
                        AnswerText = "I treat everyone the same. Stars don't get special treatment.",
                        ScoreImpact = 12,
                        TeamReaction = "That can work, but requires buy-in."
                    },
                    new InterviewAnswer
                    {
                        AnswerId = "3",
                        AnswerText = "Build the system around their strengths and let them lead.",
                        ScoreImpact = 20,
                        TeamReaction = "Maximizing talent is crucial."
                    }
                }
            });

            return questions;
        }

        private bool WantsChampionshipPedigreeCheck(CoachingJobOpening opening)
        {
            return opening.WantsChampionshipPedigree;
        }

        private string GenerateInterviewFeedback(int score)
        {
            if (score >= 70) return "Excellent interview. You're one of our top candidates.";
            if (score >= 55) return "Good interview. We'll be in touch as we continue our search.";
            if (score >= 40) return "Thank you for your time. We have some concerns but will consider your candidacy.";
            return "We appreciate you coming in, but we'll be pursuing other directions.";
        }

        // === Unsolicited Offers ===

        /// <summary>
        /// Generate unsolicited offers for a coach based on their reputation
        /// </summary>
        public List<UnsolicitedJobOffer> GenerateUnsolicitedOffers(CoachCareer coach, List<TeamFinances> teamsLookingForCoaches)
        {
            var offers = new List<UnsolicitedJobOffer>();

            if (!coach.IsEmployed && coach.Reputation < 40)
                return offers; // Low reputation coaches don't get courted

            foreach (var team in teamsLookingForCoaches)
            {
                // Calculate if team would pursue this coach
                int interest = CalculateTeamInterest(coach, team);

                if (interest >= 60)
                {
                    var offer = CreateUnsolicitedOffer(coach, team, interest);
                    offers.Add(offer);

                    if (!_unsolicitedOffers.ContainsKey(coach.CoachId))
                    {
                        _unsolicitedOffers[coach.CoachId] = new List<UnsolicitedJobOffer>();
                    }
                    _unsolicitedOffers[coach.CoachId].Add(offer);

                    OnOfferReceived?.Invoke(offer);
                }
            }

            return offers;
        }

        /// <summary>
        /// Create unsolicited offer for poaching an employed coach
        /// </summary>
        public UnsolicitedJobOffer CreatePoachingOffer(
            CoachCareer coach,
            TeamFinances offeringTeam,
            TeamFinances currentTeam)
        {
            // Only poach if offering team is significantly better opportunity
            if (offeringTeam.MarketSize >= currentTeam.MarketSize + 5 &&
                coach.Reputation >= 65)
            {
                return null; // Not a big enough upgrade
            }

            var offer = new UnsolicitedJobOffer
            {
                OfferId = Guid.NewGuid().ToString(),
                TeamId = offeringTeam.TeamId,
                TeamName = offeringTeam.TeamName,
                FromPersonName = offeringTeam.TeamOwner.FullName,
                OfferDate = DateTime.Now,
                ExpirationDate = DateTime.Now.AddDays(7),
                Status = UnsolicitedOfferStatus.Pending,
                IsPoaching = true,
                Message = GeneratePoachingMessage(coach, offeringTeam),
                ProposedContract = CreateAttractivePoacingContract(coach, offeringTeam)
            };

            if (!_unsolicitedOffers.ContainsKey(coach.CoachId))
            {
                _unsolicitedOffers[coach.CoachId] = new List<UnsolicitedJobOffer>();
            }
            _unsolicitedOffers[coach.CoachId].Add(offer);

            OnOfferReceived?.Invoke(offer);

            return offer;
        }

        private int CalculateTeamInterest(CoachCareer coach, TeamFinances team)
        {
            int interest = 40;

            // Reputation boost
            interest += (coach.Reputation - 50) / 2;

            // Championship pedigree
            interest += coach.Championships * 10;

            // Recent success
            if (coach.SeasonHistory?.Count > 0)
            {
                var lastSeason = coach.SeasonHistory[^1];
                if (lastSeason.WinPercentage > 0.6f) interest += 15;
            }

            // Big markets want proven winners
            if (team.MarketSize <= 10)
            {
                interest += coach.WinningReputation / 5;
            }

            return Mathf.Clamp(interest, 0, 100);
        }

        private UnsolicitedJobOffer CreateUnsolicitedOffer(CoachCareer coach, TeamFinances team, int interest)
        {
            long baseSalary = coach.GetMarketValue();

            // More interested teams offer more
            long offeredSalary = (long)(baseSalary * (1 + (interest - 50) / 100f));

            int years = interest >= 75 ? 5 : (interest >= 60 ? 4 : 3);

            var contract = CoachContract.CreateStandardContract(team.TeamId, years, offeredSalary);

            return new UnsolicitedJobOffer
            {
                OfferId = Guid.NewGuid().ToString(),
                TeamId = team.TeamId,
                TeamName = team.TeamName,
                FromPersonName = team.TeamOwner.FullName,
                OfferDate = DateTime.Now,
                ExpirationDate = DateTime.Now.AddDays(14),
                Status = UnsolicitedOfferStatus.Pending,
                IsPoaching = coach.IsEmployed,
                Message = GenerateOfferMessage(coach, team, interest),
                ProposedContract = contract
            };
        }

        private CoachContract CreateAttractivePoacingContract(CoachCareer coach, TeamFinances team)
        {
            // Poaching requires premium offer
            long salary = (long)(coach.GetMarketValue() * 1.3f);
            var contract = CoachContract.CreateStandardContract(team.TeamId, 5, salary);
            contract.IsFullyGuaranteed = true;
            return contract;
        }

        private string GenerateOfferMessage(CoachCareer coach, TeamFinances team, int interest)
        {
            if (interest >= 75)
            {
                return $"Coach {coach.LastName}, we've been very impressed with your work. " +
                       $"The {team.TeamName} organization would be honored to have you lead our team. " +
                       "We believe you're the perfect fit for what we're building.";
            }
            else if (interest >= 60)
            {
                return $"Coach {coach.LastName}, we're conducting a coaching search and your name " +
                       "has come up repeatedly. We'd like to discuss a potential opportunity with the " +
                       $"{team.TeamName}.";
            }

            return $"Coach {coach.LastName}, the {team.TeamName} are exploring their options for " +
                   "the head coaching position. Would you be interested in discussing the opportunity?";
        }

        private string GeneratePoachingMessage(CoachCareer coach, TeamFinances team)
        {
            return $"Coach {coach.LastName}, I'll be direct. We want you to lead the {team.TeamName}. " +
                   "I know you're currently employed, but I believe we can offer you a championship-caliber " +
                   "roster and the resources to compete at the highest level. Let's talk.";
        }

        // === Offer Management ===

        /// <summary>
        /// Accept an unsolicited offer
        /// </summary>
        public bool AcceptOffer(string offerId, CoachCareer coach)
        {
            var offers = GetOffersForCoach(coach.CoachId);
            var offer = offers?.Find(o => o.OfferId == offerId);

            if (offer == null || offer.Status != UnsolicitedOfferStatus.Pending)
                return false;

            offer.Status = UnsolicitedOfferStatus.Accepted;
            coach.AcceptJob(offer.TeamId, offer.TeamName, offer.ProposedContract);

            // Reject all other pending offers
            foreach (var other in offers.Where(o => o.OfferId != offerId && o.Status == UnsolicitedOfferStatus.Pending))
            {
                other.Status = UnsolicitedOfferStatus.Declined;
            }

            Debug.Log($"[JobMarket] {coach.FullName} accepted offer from {offer.TeamName}");

            return true;
        }

        /// <summary>
        /// Decline an unsolicited offer
        /// </summary>
        public void DeclineOffer(string offerId, string coachId)
        {
            var offers = GetOffersForCoach(coachId);
            var offer = offers?.Find(o => o.OfferId == offerId);

            if (offer != null)
            {
                offer.Status = UnsolicitedOfferStatus.Declined;
            }
        }

        /// <summary>
        /// Get all offers for a coach
        /// </summary>
        public List<UnsolicitedJobOffer> GetOffersForCoach(string coachId)
        {
            _unsolicitedOffers.TryGetValue(coachId, out var offers);
            return offers ?? new List<UnsolicitedJobOffer>();
        }

        /// <summary>
        /// Get pending offers
        /// </summary>
        public List<UnsolicitedJobOffer> GetPendingOffers(string coachId)
        {
            return GetOffersForCoach(coachId)
                .Where(o => o.Status == UnsolicitedOfferStatus.Pending)
                .ToList();
        }

        /// <summary>
        /// Get applications for a coach
        /// </summary>
        public List<JobApplication> GetApplicationsForCoach(string coachId)
        {
            _applications.TryGetValue(coachId, out var apps);
            return apps ?? new List<JobApplication>();
        }

        /// <summary>
        /// Get pending interviews
        /// </summary>
        public List<CoachInterview> GetPendingInterviews(string coachId)
        {
            return _scheduledInterviews.Values
                .Where(i => i.CoachId == coachId && !i.IsCompleted)
                .ToList();
        }

        /// <summary>
        /// Process job market at end of period (simulate AI decisions)
        /// </summary>
        public void ProcessJobMarket()
        {
            // Expire old offers
            foreach (var offerList in _unsolicitedOffers.Values)
            {
                foreach (var offer in offerList.Where(o => o.Status == UnsolicitedOfferStatus.Pending))
                {
                    if (DateTime.Now > offer.ExpirationDate)
                    {
                        offer.Status = UnsolicitedOfferStatus.Expired;
                    }
                }
            }

            // Progress open positions
            foreach (var opening in _openPositions.Values.Where(o => o.Status == JobOpeningStatus.Open))
            {
                if (DateTime.Now > opening.Deadline)
                {
                    opening.Status = JobOpeningStatus.InterviewPhase;
                }
            }
        }

        /// <summary>
        /// Get hiring bonus for a former player coach based on user relationship.
        /// Returns a multiplier (e.g., 0.25 = 25% bonus) to add to interest.
        /// </summary>
        public float GetFormerPlayerHiringBonus(string formerPlayerId, string teamId)
        {
            if (FormerPlayerCareerManager.Instance == null)
                return 0f;

            return FormerPlayerCareerManager.Instance.GetHiringBonus(formerPlayerId, teamId);
        }

        /// <summary>
        /// Calculate team interest with former player bonus included.
        /// </summary>
        public int GetTeamInterestWithFormerPlayerBonus(CoachingJobOpening opening, CoachCareer coach)
        {
            int baseInterest = opening.GetTeamInterest(coach);

            if (coach.IsFormerPlayer && !string.IsNullOrEmpty(coach.FormerPlayerId))
            {
                float bonus = GetFormerPlayerHiringBonus(coach.FormerPlayerId, opening.TeamId);
                baseInterest += (int)(baseInterest * bonus);
            }

            return Mathf.Clamp(baseInterest, 0, 100);
        }
    }
}
