using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages job security, owner expectations, and firing/hiring of coaches
    /// </summary>
    public class JobSecurityManager
    {
        private Dictionary<string, CoachCareer> _coachCareers;
        private Dictionary<string, List<OwnerJobSecurityMessage>> _ownerMessages;
        private Dictionary<string, SeasonExpectations> _expectations;

        public event Action<CoachCareer> OnJobSecurityChanged;
        public event Action<OwnerJobSecurityMessage> OnOwnerMessageReceived;
        public event Action<CoachCareer> OnCoachFired;
        public event Action<CoachCareer, string> OnHotSeatWarning;

        public JobSecurityManager()
        {
            _coachCareers = new Dictionary<string, CoachCareer>();
            _ownerMessages = new Dictionary<string, List<OwnerJobSecurityMessage>>();
            _expectations = new Dictionary<string, SeasonExpectations>();
        }

        /// <summary>
        /// Register a coach's career
        /// </summary>
        public void RegisterCoach(CoachCareer career)
        {
            _coachCareers[career.CoachId] = career;
            _ownerMessages[career.CoachId] = new List<OwnerJobSecurityMessage>();
        }

        /// <summary>
        /// Get coach career by ID
        /// </summary>
        public CoachCareer GetCoachCareer(string coachId)
        {
            _coachCareers.TryGetValue(coachId, out var career);
            return career;
        }

        /// <summary>
        /// Initialize the manager for a new season
        /// </summary>
        public void InitializeForNewSeason(CoachCareer career, string teamId)
        {
            if (career == null) return;

            // Register the coach if not already registered
            if (!_coachCareers.ContainsKey(career.CoachId))
            {
                RegisterCoach(career);
            }

            // Reset season-specific data
            _ownerMessages[career.CoachId]?.Clear();

            Debug.Log($"[JobSecurityManager] Initialized for new season - Coach: {career.CoachId}, Team: {teamId}");
        }

        /// <summary>
        /// Set season expectations from owner
        /// </summary>
        public SeasonExpectations SetSeasonExpectations(
            string coachId,
            TeamFinances teamFinances,
            int projectedTeamStrength)
        {
            var career = GetCoachCareer(coachId);
            if (career == null || teamFinances?.TeamOwner == null) return null;

            var owner = teamFinances.TeamOwner;

            // Calculate expectations based on owner type and team strength
            var expectations = new SeasonExpectations
            {
                PlayersToDevelope = new List<string>()
            };

            // Base win expectations on owner philosophy and team strength
            switch (owner.Philosophy)
            {
                case OwnerPhilosophy.WinNow:
                    expectations.MinimumWins = Math.Max(35, projectedTeamStrength - 10);
                    expectations.TargetWins = projectedTeamStrength + 3;
                    expectations.StretchWins = projectedTeamStrength + 10;
                    expectations.ExpectsPlayoffs = projectedTeamStrength >= 35;
                    expectations.MinimumPlayoffResult = projectedTeamStrength >= 45 ? "Second Round" : "First Round";
                    break;

                case OwnerPhilosophy.SustainedSuccess:
                    expectations.MinimumWins = Math.Max(30, projectedTeamStrength - 8);
                    expectations.TargetWins = projectedTeamStrength;
                    expectations.StretchWins = projectedTeamStrength + 7;
                    expectations.ExpectsPlayoffs = projectedTeamStrength >= 38;
                    expectations.MinimumPlayoffResult = "First Round";
                    break;

                case OwnerPhilosophy.Development:
                    expectations.MinimumWins = Math.Max(20, projectedTeamStrength - 15);
                    expectations.TargetWins = projectedTeamStrength - 5;
                    expectations.StretchWins = projectedTeamStrength + 5;
                    expectations.ExpectsPlayoffs = false;
                    expectations.ExpectsDevelopment = true;
                    break;

                case OwnerPhilosophy.Profit:
                    expectations.MinimumWins = Math.Max(25, projectedTeamStrength - 12);
                    expectations.TargetWins = projectedTeamStrength - 3;
                    expectations.StretchWins = projectedTeamStrength + 5;
                    expectations.ExpectsPlayoffs = false;
                    break;

                default:
                    expectations.MinimumWins = Math.Max(30, projectedTeamStrength - 10);
                    expectations.TargetWins = projectedTeamStrength;
                    expectations.StretchWins = projectedTeamStrength + 8;
                    expectations.ExpectsPlayoffs = projectedTeamStrength >= 40;
                    break;
            }

            // Adjust based on owner's minimum acceptable wins
            expectations.MinimumWins = Math.Max(expectations.MinimumWins, owner.MinAcceptableWins);

            // Generate owner statement
            expectations.OwnerStatement = GenerateExpectationStatement(owner, expectations);

            _expectations[coachId] = expectations;
            career.CurrentExpectations = expectations;

            // Send expectations message
            SendOwnerMessage(coachId, owner, JobSecurityStatus.Stable,
                "Season Expectations", expectations.OwnerStatement, false);

            return expectations;
        }

        /// <summary>
        /// Evaluate job security based on current record
        /// </summary>
        public JobSecurityStatus EvaluateJobSecurity(
            string coachId,
            int currentWins,
            int currentLosses,
            bool isPlayoffBound,
            TeamFinances teamFinances)
        {
            var career = GetCoachCareer(coachId);
            if (career == null) return JobSecurityStatus.Stable;

            var expectations = _expectations.GetValueOrDefault(coachId, career.CurrentExpectations);
            if (expectations == null) return JobSecurityStatus.Stable;

            var owner = teamFinances?.TeamOwner;
            if (owner == null) return JobSecurityStatus.Stable;

            int gamesPlayed = currentWins + currentLosses;
            if (gamesPlayed == 0) return career.JobSecurity;

            float winPct = currentWins / (float)gamesPlayed;
            int projectedWins = (int)(winPct * 82);

            JobSecurityStatus newStatus;

            // Evaluate based on projected performance vs expectations
            if (projectedWins >= expectations.StretchWins)
            {
                newStatus = JobSecurityStatus.Untouchable;
            }
            else if (projectedWins >= expectations.TargetWins)
            {
                newStatus = JobSecurityStatus.Secure;
            }
            else if (projectedWins >= expectations.MinimumWins)
            {
                newStatus = expectations.ExpectsPlayoffs && !isPlayoffBound
                    ? JobSecurityStatus.Uncertain
                    : JobSecurityStatus.Stable;
            }
            else if (projectedWins >= expectations.MinimumWins - 5)
            {
                newStatus = JobSecurityStatus.Uncertain;
            }
            else if (projectedWins >= expectations.MinimumWins - 10)
            {
                newStatus = JobSecurityStatus.HotSeat;
            }
            else
            {
                newStatus = JobSecurityStatus.FinalWarning;
            }

            // Owner patience affects escalation speed
            if (owner.Patience < 30 && newStatus > JobSecurityStatus.Stable)
            {
                // Impatient owner - escalate faster
                newStatus = (JobSecurityStatus)Math.Min((int)newStatus + 1, (int)JobSecurityStatus.FinalWarning);
            }
            else if (owner.Patience > 70 && newStatus > JobSecurityStatus.Uncertain)
            {
                // Patient owner - slower to fire
                newStatus = (JobSecurityStatus)Math.Max((int)newStatus - 1, (int)JobSecurityStatus.Uncertain);
            }

            // Check if status changed
            if (newStatus != career.JobSecurity)
            {
                HandleJobSecurityChange(career, newStatus, owner, currentWins, currentLosses);
            }

            return newStatus;
        }

        /// <summary>
        /// Handle change in job security status
        /// </summary>
        private void HandleJobSecurityChange(
            CoachCareer career,
            JobSecurityStatus newStatus,
            Owner owner,
            int wins,
            int losses)
        {
            var oldStatus = career.JobSecurity;
            career.JobSecurity = newStatus;

            // Send appropriate owner message
            switch (newStatus)
            {
                case JobSecurityStatus.Uncertain:
                    SendOwnerMessage(career.CoachId, owner, newStatus,
                        "Concerns About Our Direction",
                        GenerateUncertainMessage(owner, wins, losses),
                        true);
                    break;

                case JobSecurityStatus.HotSeat:
                    SendOwnerMessage(career.CoachId, owner, newStatus,
                        "We Need to Talk",
                        GenerateHotSeatMessage(owner, wins, losses),
                        true);
                    OnHotSeatWarning?.Invoke(career, GenerateHotSeatMessage(owner, wins, losses));
                    break;

                case JobSecurityStatus.FinalWarning:
                    SendOwnerMessage(career.CoachId, owner, newStatus,
                        "Final Warning",
                        GenerateFinalWarningMessage(owner, wins, losses),
                        true);
                    break;

                case JobSecurityStatus.Secure:
                case JobSecurityStatus.Untouchable:
                    if (oldStatus >= JobSecurityStatus.Uncertain)
                    {
                        // Recovered from hot seat
                        SendOwnerMessage(career.CoachId, owner, newStatus,
                            "Things Are Looking Better",
                            GenerateRecoveryMessage(owner),
                            false);
                    }
                    break;
            }

            OnJobSecurityChanged?.Invoke(career);
        }

        /// <summary>
        /// Send a direct message from the owner
        /// </summary>
        public OwnerJobSecurityMessage SendOwnerMessage(
            string coachId,
            Owner owner,
            JobSecurityStatus securityLevel,
            string subject,
            string message,
            bool requiresResponse)
        {
            var ownerMessage = new OwnerJobSecurityMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Date = DateTime.Now,
                SecurityLevel = securityLevel,
                Subject = subject,
                Message = message,
                RequiresResponse = requiresResponse,
                ResponseOptions = requiresResponse ? GenerateResponseOptions(securityLevel) : null,
                WasRead = false
            };

            if (!_ownerMessages.ContainsKey(coachId))
            {
                _ownerMessages[coachId] = new List<OwnerJobSecurityMessage>();
            }
            _ownerMessages[coachId].Add(ownerMessage);

            OnOwnerMessageReceived?.Invoke(ownerMessage);

            Debug.Log($"[JobSecurity] Owner message sent: {subject}");

            return ownerMessage;
        }

        /// <summary>
        /// Get unread owner messages
        /// </summary>
        public List<OwnerJobSecurityMessage> GetUnreadMessages(string coachId)
        {
            if (!_ownerMessages.TryGetValue(coachId, out var messages))
                return new List<OwnerJobSecurityMessage>();

            return messages.FindAll(m => !m.WasRead);
        }

        /// <summary>
        /// Mark message as read and optionally respond
        /// </summary>
        public void RespondToMessage(string coachId, string messageId, string response = null)
        {
            if (!_ownerMessages.TryGetValue(coachId, out var messages))
                return;

            var message = messages.Find(m => m.MessageId == messageId);
            if (message == null) return;

            message.WasRead = true;
            message.SelectedResponse = response;
        }

        /// <summary>
        /// Check if coach should be fired (called after games or at key points)
        /// </summary>
        public bool ShouldFireCoach(string coachId, TeamFinances teamFinances)
        {
            var career = GetCoachCareer(coachId);
            if (career == null) return false;

            var owner = teamFinances?.TeamOwner;
            if (owner == null) return false;

            // Final warning with continued poor play
            if (career.JobSecurity == JobSecurityStatus.FinalWarning)
            {
                // 30% chance each evaluation
                if (UnityEngine.Random.value < 0.30f)
                    return true;
            }

            // Hot seat with very impatient owner
            if (career.JobSecurity == JobSecurityStatus.HotSeat && owner.Patience < 20)
            {
                if (UnityEngine.Random.value < 0.15f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Fire the coach
        /// </summary>
        public void FireCoach(string coachId, TeamFinances teamFinances, string reason)
        {
            var career = GetCoachCareer(coachId);
            if (career == null) return;

            var owner = teamFinances?.TeamOwner;

            // Send firing message
            string firingMessage = GenerateFiringMessage(owner, reason);
            SendOwnerMessage(coachId, owner, JobSecurityStatus.Fired,
                "Change in Leadership",
                firingMessage,
                false);

            // Update career
            career.GetFired();

            OnCoachFired?.Invoke(career);

            Debug.Log($"[JobSecurity] Coach {career.FullName} has been fired. Reason: {reason}");
        }

        /// <summary>
        /// End of season evaluation
        /// </summary>
        public EndOfSeasonResult EvaluateEndOfSeason(
            string coachId,
            int wins,
            int losses,
            bool madePlayoffs,
            string playoffResult,
            bool wonChampionship,
            TeamFinances teamFinances)
        {
            var career = GetCoachCareer(coachId);
            if (career == null) return null;

            var expectations = _expectations.GetValueOrDefault(coachId, career.CurrentExpectations);
            var owner = teamFinances?.TeamOwner;

            var result = new EndOfSeasonResult
            {
                CoachId = coachId,
                Wins = wins,
                Losses = losses,
                MadePlayoffs = madePlayoffs,
                PlayoffResult = playoffResult,
                WonChampionship = wonChampionship
            };

            // Evaluate against expectations
            result.ExpectationResult = expectations?.Evaluate(wins, madePlayoffs, playoffResult)
                ?? ExpectationResult.Met;

            // Determine outcome
            result.ContractExtensionOffered = false;
            result.WillBeFired = false;

            switch (result.ExpectationResult)
            {
                case ExpectationResult.Exceeded:
                    result.ContractExtensionOffered = true;
                    result.OwnerFeedback = GenerateExceededExpectationsFeedback(owner, wonChampionship);
                    break;

                case ExpectationResult.Met:
                    if (career.CurrentContract?.IsExpiring == true)
                        result.ContractExtensionOffered = true;
                    result.OwnerFeedback = GenerateMetExpectationsFeedback(owner);
                    break;

                case ExpectationResult.Adequate:
                    result.OwnerFeedback = GenerateAdequateFeedback(owner);
                    break;

                case ExpectationResult.BelowExpectations:
                    if (career.JobSecurity >= JobSecurityStatus.HotSeat || owner.Patience < 40)
                        result.WillBeFired = true;
                    result.OwnerFeedback = GenerateBelowExpectationsFeedback(owner, result.WillBeFired);
                    break;

                case ExpectationResult.Failed:
                    result.WillBeFired = owner.Patience < 70; // Very patient owners might keep
                    result.OwnerFeedback = GenerateFailedFeedback(owner, result.WillBeFired);
                    break;
            }

            // Send end of season message
            SendOwnerMessage(coachId, owner, career.JobSecurity,
                "Season Review",
                result.OwnerFeedback,
                false);

            // Process firing if needed
            if (result.WillBeFired)
            {
                FireCoach(coachId, teamFinances, "End of season evaluation");
            }

            return result;
        }

        // === Message Generation ===

        private string GenerateExpectationStatement(Owner owner, SeasonExpectations exp)
        {
            string statement = owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    $"I expect us to compete for a championship. That means at least {exp.TargetWins} wins and a deep playoff run.",
                OwnerType.Competitive =>
                    $"We should be a playoff team. I want to see {exp.TargetWins} wins minimum.",
                OwnerType.Balanced =>
                    $"Let's be competitive and smart. {exp.MinimumWins}-{exp.TargetWins} wins would be acceptable.",
                OwnerType.FrugalButFair =>
                    $"Given our resources, {exp.MinimumWins} wins would show progress.",
                OwnerType.Cheap =>
                    $"Keep costs down and develop our young players. {exp.MinimumWins} wins is fine.",
                _ => $"I expect {exp.TargetWins} wins this season."
            };

            if (exp.ExpectsPlayoffs)
            {
                statement += " Making the playoffs is essential.";
            }

            if (exp.ExpectsDevelopment)
            {
                statement += " Focus on developing our young talent.";
            }

            return statement;
        }

        private string GenerateUncertainMessage(Owner owner, int wins, int losses)
        {
            return owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    $"At {wins}-{losses}, we're not where we need to be. I invested heavily in this roster. I need to see improvement.",
                OwnerType.Competitive =>
                    $"Our {wins}-{losses} record concerns me. We talked about being a playoff team. What's going wrong?",
                OwnerType.Cheap =>
                    $"Even with our budget constraints, {wins}-{losses} is disappointing. The fans are noticing.",
                _ =>
                    $"I'm concerned about our {wins}-{losses} record. Let's turn this around."
            };
        }

        private string GenerateHotSeatMessage(Owner owner, int wins, int losses)
        {
            return owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    $"{wins}-{losses} is unacceptable. I've given you every resource. If this continues, I'll have to make a change.",
                OwnerType.Competitive =>
                    $"We're at {wins}-{losses}. This isn't what I signed up for. Win 3 of your next 5, or we need to have a different conversation.",
                OwnerType.Cheap =>
                    $"Look, I know the budget is tight, but {wins}-{losses}? The fans want answers. I need you to figure this out.",
                _ =>
                    $"At {wins}-{losses}, your job is at risk. I need to see immediate improvement."
            };
        }

        private string GenerateFinalWarningMessage(Owner owner, int wins, int losses)
        {
            return owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    $"This is your final warning. {wins}-{losses} is embarrassing for a franchise with our payroll. One more bad stretch and we're done.",
                OwnerType.Competitive =>
                    $"I've been patient. {wins}-{losses} has tested that patience. This is your last chance to turn things around.",
                _ =>
                    $"I'm telling you directly: at {wins}-{losses}, another losing streak means I'll be forced to make a coaching change."
            };
        }

        private string GenerateRecoveryMessage(Owner owner)
        {
            return owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    "This is the team I expected to see. Keep this momentum going.",
                OwnerType.Competitive =>
                    "Much better. This is the kind of basketball that wins games. Let's stay focused.",
                _ =>
                    "I like what I'm seeing lately. Keep up the good work."
            };
        }

        private string GenerateFiringMessage(Owner owner, string reason)
        {
            return owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    "I've made a difficult decision. We're going in a different direction with our coaching staff. I wish you well in your future endeavors.",
                OwnerType.Competitive =>
                    "After careful consideration, I've decided to make a coaching change. This wasn't easy, but I believe it's necessary for the franchise.",
                OwnerType.Cheap =>
                    "This isn't personal, but the results just aren't there. We need a fresh start.",
                _ =>
                    "I want to thank you for your effort, but we've decided to make a change in leadership."
            };
        }

        private string GenerateExceededExpectationsFeedback(Owner owner, bool wonChampionship)
        {
            if (wonChampionship)
            {
                return "Congratulations! A championship - this is what it's all about. Let's talk about keeping you here long-term.";
            }

            return owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    "Outstanding season. You've exceeded my expectations. Let's discuss an extension.",
                OwnerType.Competitive =>
                    "This is exactly what I wanted to see. Great job. I want to lock you in for the future.",
                _ =>
                    "What a season! You've done a tremendous job. I want to reward that."
            };
        }

        private string GenerateMetExpectationsFeedback(Owner owner)
        {
            return owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    "Good season. You met the goals we set. Let's talk about what we need to take the next step.",
                OwnerType.Competitive =>
                    "Solid year. You did what was asked. Now let's build on this.",
                _ =>
                    "You met expectations this season. Good work. Let's keep building."
            };
        }

        private string GenerateAdequateFeedback(Owner owner)
        {
            return owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    "The results were... adequate. Given our investment, I expected more. But you have another year to prove yourself.",
                OwnerType.Competitive =>
                    "Not our best year, but not a disaster either. I need to see improvement next season.",
                _ =>
                    "An okay season, but there's definitely room for improvement."
            };
        }

        private string GenerateBelowExpectationsFeedback(Owner owner, bool willBeFired)
        {
            if (willBeFired)
            {
                return "I've given this a lot of thought. We fell well short of expectations, and I've decided to make a change.";
            }

            return owner.SpendingType switch
            {
                OwnerType.Lavish =>
                    "This season was a disappointment. I'm giving you one more chance, but I need to see major improvement.",
                OwnerType.Competitive =>
                    "Below expectations. I'm not happy. But I'm willing to give you another shot. Don't waste it.",
                _ =>
                    "The results weren't what we hoped for. Next year needs to be different."
            };
        }

        private string GenerateFailedFeedback(Owner owner, bool willBeFired)
        {
            if (willBeFired)
            {
                return "This season was a complete failure by any measure. I have no choice but to make a change. I'm sorry, but we're letting you go.";
            }

            return "This was a very difficult season. Against my better judgment, I'm giving you one final chance. Don't make me regret it.";
        }

        private List<string> GenerateResponseOptions(JobSecurityStatus status)
        {
            return status switch
            {
                JobSecurityStatus.Uncertain => new List<string>
                {
                    "I understand your concerns. We're working on it.",
                    "The roster needs adjustments. Let's discuss moves.",
                    "Give me time. I have a plan.",
                    "The results will come. Trust the process."
                },
                JobSecurityStatus.HotSeat => new List<string>
                {
                    "I hear you loud and clear. I'll turn this around.",
                    "I take full responsibility. Let me fix this.",
                    "We need to make some changes to the rotation.",
                    "The players are committed. We won't let you down."
                },
                JobSecurityStatus.FinalWarning => new List<string>
                {
                    "I understand. I won't let you down.",
                    "Thank you for the opportunity. I'll make it count.",
                    "I know what's at stake. Watch us respond."
                },
                _ => new List<string> { "Thank you for the feedback." }
            };
        }
    }

    /// <summary>
    /// Result of end-of-season evaluation
    /// </summary>
    public class EndOfSeasonResult
    {
        public string CoachId;
        public int Wins;
        public int Losses;
        public bool MadePlayoffs;
        public string PlayoffResult;
        public bool WonChampionship;
        public ExpectationResult ExpectationResult;
        public bool ContractExtensionOffered;
        public bool WillBeFired;
        public string OwnerFeedback;
    }
}
