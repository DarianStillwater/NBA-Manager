using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages team finances, owner interactions, and budget approvals
    /// </summary>
    public class FinanceManager
    {
        private Dictionary<string, TeamFinances> _teamFinances;
        private Dictionary<string, CoachOwnerRelationship> _ownerRelationships;
        private Dictionary<string, List<OwnerApprovalRequest>> _pendingRequests;
        private Dictionary<string, OwnerMeeting> _activeMeetings;

        public event Action<OwnerMeeting> OnMeetingScheduled;
        public event Action<OwnerMeeting> OnMeetingCompleted;
        public event Action<OwnerApprovalRequest> OnRequestApproved;
        public event Action<OwnerApprovalRequest> OnRequestDenied;
        public event Action<string, string> OnOwnerMessage; // (title, message)

        public FinanceManager()
        {
            _teamFinances = new Dictionary<string, TeamFinances>();
            _ownerRelationships = new Dictionary<string, CoachOwnerRelationship>();
            _pendingRequests = new Dictionary<string, List<OwnerApprovalRequest>>();
            _activeMeetings = new Dictionary<string, OwnerMeeting>();
        }

        /// <summary>
        /// Register team finances
        /// </summary>
        public void RegisterTeamFinances(TeamFinances finances)
        {
            _teamFinances[finances.TeamId] = finances;
        }

        /// <summary>
        /// Get finances for a team
        /// </summary>
        public TeamFinances GetTeamFinances(string teamId)
        {
            _teamFinances.TryGetValue(teamId, out var finances);
            return finances;
        }

        /// <summary>
        /// Initialize relationship between coach and owner
        /// </summary>
        public void InitializeOwnerRelationship(string coachId, string teamId)
        {
            var finances = GetTeamFinances(teamId);
            if (finances?.TeamOwner == null) return;

            var relationship = new CoachOwnerRelationship
            {
                CoachId = coachId,
                OwnerId = finances.TeamOwner.OwnerId,
                Trust = 50,       // Start neutral
                Respect = 50,
                Communication = 50,
                MeetingsHeld = 0,
                PromisesMade = 0,
                PromisesKept = 0,
                PositiveHistory = new List<string>(),
                NegativeHistory = new List<string>()
            };

            string key = GetRelationshipKey(coachId, teamId);
            _ownerRelationships[key] = relationship;
        }

        /// <summary>
        /// Get relationship with owner
        /// </summary>
        public CoachOwnerRelationship GetOwnerRelationship(string coachId, string teamId)
        {
            string key = GetRelationshipKey(coachId, teamId);
            _ownerRelationships.TryGetValue(key, out var relationship);
            return relationship;
        }

        private string GetRelationshipKey(string coachId, string teamId) => $"{coachId}_{teamId}";

        // === Budget & Approval System ===

        /// <summary>
        /// Request luxury tax approval from owner
        /// </summary>
        public OwnerApprovalRequest RequestLuxuryTaxApproval(
            string teamId,
            string coachId,
            long projectedOverage,
            string justification)
        {
            var finances = GetTeamFinances(teamId);
            if (finances == null) return null;

            var request = new OwnerApprovalRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                RequestType = "LuxuryTax",
                Description = $"Approval to exceed luxury tax threshold by ${projectedOverage / 1_000_000f:F1}M",
                Amount = projectedOverage,
                Justification = justification,
                RequestDate = DateTime.Now,
                Status = OwnerApprovalStatus.NeedsDiscussion
            };

            // This requires a meeting
            ScheduleOwnerMeeting(teamId, coachId, OwnerMeetingType.BudgetRequest,
                "Luxury Tax Discussion", new List<OwnerMeetingTopic>
                {
                    CreateLuxuryTaxTopic(finances, projectedOverage, justification)
                });

            AddPendingRequest(teamId, request);
            return request;
        }

        /// <summary>
        /// Request staff budget increase
        /// </summary>
        public OwnerApprovalRequest RequestStaffBudgetIncrease(
            string teamId,
            string coachId,
            long additionalAmount,
            string purpose)
        {
            var finances = GetTeamFinances(teamId);
            if (finances == null) return null;

            var request = new OwnerApprovalRequest
            {
                RequestId = Guid.NewGuid().ToString(),
                RequestType = "StaffBudget",
                Description = $"Request for additional ${additionalAmount / 1_000_000f:F1}M staff budget",
                Amount = additionalAmount,
                Justification = purpose,
                RequestDate = DateTime.Now,
                Status = OwnerApprovalStatus.Pending
            };

            // Small requests can be auto-approved based on relationship
            var relationship = GetOwnerRelationship(coachId, teamId);
            if (relationship != null && additionalAmount < 2_000_000 && relationship.Trust >= 60)
            {
                request.Status = OwnerApprovalStatus.Approved;
                request.OwnerResponse = "I trust your judgment on staffing decisions.";
                request.ResponseDate = DateTime.Now;
                OnRequestApproved?.Invoke(request);
            }
            else
            {
                // Needs discussion
                request.Status = OwnerApprovalStatus.NeedsDiscussion;
                ScheduleOwnerMeeting(teamId, coachId, OwnerMeetingType.BudgetRequest,
                    "Staff Budget Discussion", new List<OwnerMeetingTopic>
                    {
                        CreateStaffBudgetTopic(additionalAmount, purpose)
                    });
            }

            AddPendingRequest(teamId, request);
            return request;
        }

        private void AddPendingRequest(string teamId, OwnerApprovalRequest request)
        {
            if (!_pendingRequests.ContainsKey(teamId))
            {
                _pendingRequests[teamId] = new List<OwnerApprovalRequest>();
            }
            _pendingRequests[teamId].Add(request);
        }

        // === Owner Meeting System ===

        /// <summary>
        /// Schedule a meeting with the owner
        /// </summary>
        public OwnerMeeting ScheduleOwnerMeeting(
            string teamId,
            string coachId,
            OwnerMeetingType type,
            string agenda,
            List<OwnerMeetingTopic> topics)
        {
            var finances = GetTeamFinances(teamId);
            if (finances == null) return null;

            var meeting = new OwnerMeeting
            {
                MeetingId = Guid.NewGuid().ToString(),
                ScheduledDate = DateTime.Now.AddDays(1),
                MeetingType = type,
                Agenda = agenda,
                Topics = topics ?? new List<OwnerMeetingTopic>(),
                AvailableResponses = new List<OwnerDialogOption>(),
                IsCompleted = false
            };

            _activeMeetings[meeting.MeetingId] = meeting;
            OnMeetingScheduled?.Invoke(meeting);

            // Send owner message about the meeting
            string title = GetMeetingNotificationTitle(type);
            string message = GetMeetingNotificationMessage(type, finances.TeamOwner);
            OnOwnerMessage?.Invoke(title, message);

            return meeting;
        }

        /// <summary>
        /// Start an owner meeting and get available dialog options
        /// </summary>
        public OwnerMeeting StartMeeting(string meetingId, string teamId)
        {
            if (!_activeMeetings.TryGetValue(meetingId, out var meeting))
                return null;

            var finances = GetTeamFinances(teamId);
            if (finances == null) return null;

            // Generate opening statement from owner
            var owner = finances.TeamOwner;
            meeting.Outcome = GetOwnerOpeningStatement(owner, meeting.MeetingType);

            return meeting;
        }

        /// <summary>
        /// Select a dialog option in meeting
        /// </summary>
        public MeetingDialogResult SelectDialogOption(
            string meetingId,
            string topicId,
            string optionId,
            string teamId,
            string coachId)
        {
            if (!_activeMeetings.TryGetValue(meetingId, out var meeting))
                return null;

            var topic = meeting.Topics.Find(t => t.TopicId == topicId);
            if (topic == null) return null;

            var option = topic.DialogOptions.Find(o => o.OptionId == optionId);
            if (option == null) return null;

            topic.SelectedOption = option;
            topic.WasDiscussed = true;

            var finances = GetTeamFinances(teamId);
            var relationship = GetOwnerRelationship(coachId, teamId);

            // Calculate success based on option success chance and relationship
            int effectiveChance = option.SuccessChance;
            if (relationship != null)
            {
                effectiveChance += (relationship.Trust - 50) / 5; // ±10% based on trust
            }

            // Owner personality affects certain tones
            effectiveChance = AdjustChanceByTone(effectiveChance, option.Tone, finances.TeamOwner);

            bool success = UnityEngine.Random.Range(0, 100) < effectiveChance;

            var result = new MeetingDialogResult
            {
                WasSuccessful = success,
                OwnerResponse = success ? option.SuccessOutcome : option.FailureOutcome,
                RelationshipChange = success ? option.RelationshipImpact : -Math.Abs(option.RelationshipImpact) / 2
            };

            // Apply relationship changes
            if (relationship != null)
            {
                int change = result.RelationshipChange;
                relationship.ModifyRelationship(change, change / 2, change / 3);
            }

            return result;
        }

        /// <summary>
        /// Complete a meeting and resolve pending requests
        /// </summary>
        public void CompleteMeeting(string meetingId, string teamId, string coachId, bool wasSuccessful)
        {
            if (!_activeMeetings.TryGetValue(meetingId, out var meeting))
                return;

            meeting.IsCompleted = true;

            var relationship = GetOwnerRelationship(coachId, teamId);
            if (relationship != null)
            {
                relationship.MeetingsHeld++;

                // General relationship improvement from meeting
                if (wasSuccessful)
                {
                    relationship.ModifyRelationship(3, 2, 2);
                }
            }

            // Process any pending requests based on meeting outcome
            if (_pendingRequests.TryGetValue(teamId, out var requests))
            {
                foreach (var request in requests.FindAll(r => r.Status == OwnerApprovalStatus.NeedsDiscussion))
                {
                    if (wasSuccessful)
                    {
                        request.Status = OwnerApprovalStatus.Approved;
                        request.OwnerResponse = "After our discussion, I'm willing to proceed with this.";
                        OnRequestApproved?.Invoke(request);
                    }
                    else
                    {
                        request.Status = OwnerApprovalStatus.Denied;
                        request.OwnerResponse = "I don't think this is the right move for us right now.";
                        OnRequestDenied?.Invoke(request);
                    }
                    request.ResponseDate = DateTime.Now;
                }
            }

            _activeMeetings.Remove(meetingId);
            OnMeetingCompleted?.Invoke(meeting);
        }

        private int AdjustChanceByTone(int baseChance, DialogTone tone, Owner owner)
        {
            // Owner personality affects how they respond to different tones
            return (owner.SpendingType, tone) switch
            {
                (OwnerType.Lavish, DialogTone.Confident) => baseChance + 10,
                (OwnerType.Lavish, DialogTone.Aggressive) => baseChance + 5,
                (OwnerType.Lavish, DialogTone.Humble) => baseChance - 5,

                (OwnerType.Cheap, DialogTone.Professional) => baseChance + 10,
                (OwnerType.Cheap, DialogTone.Aggressive) => baseChance - 20,
                (OwnerType.Cheap, DialogTone.Pleading) => baseChance - 10,

                (OwnerType.Balanced, DialogTone.Professional) => baseChance + 5,
                (OwnerType.Balanced, DialogTone.Confident) => baseChance + 5,

                (_, DialogTone.Defiant) => baseChance - 15, // Never goes well

                _ => baseChance
            };
        }

        // === Meeting Topic Generators ===

        private OwnerMeetingTopic CreateLuxuryTaxTopic(TeamFinances finances, long overage, string justification)
        {
            var topic = new OwnerMeetingTopic
            {
                TopicId = Guid.NewGuid().ToString(),
                Title = "Luxury Tax Spending",
                Description = $"Request to exceed the luxury tax by ${overage / 1_000_000f:F1}M",
                IsRequired = true,
                WasDiscussed = false,
                DialogOptions = new List<OwnerDialogOption>()
            };

            // Generate options based on owner type
            bool isContending = true; // Would be determined by team record

            topic.DialogOptions.Add(new OwnerDialogOption
            {
                OptionId = Guid.NewGuid().ToString(),
                DisplayText = "Present the championship case",
                FullResponse = "This roster gives us a real shot at a championship. The luxury tax is worth it for a title.",
                Tone = DialogTone.Confident,
                SuccessChance = finances.TeamOwner.LuxuryTaxTolerance + (isContending ? 20 : -10),
                RelationshipImpact = 5,
                SuccessOutcome = "You make a compelling case. Let's go for it.",
                FailureOutcome = "I appreciate the optimism, but I'm not convinced we're there yet."
            });

            topic.DialogOptions.Add(new OwnerDialogOption
            {
                OptionId = Guid.NewGuid().ToString(),
                DisplayText = "Emphasize player development investment",
                FullResponse = "This isn't just about this year. These players will develop and give us a long championship window.",
                Tone = DialogTone.Professional,
                SuccessChance = finances.TeamOwner.LuxuryTaxTolerance - 10,
                RelationshipImpact = 3,
                SuccessOutcome = "Building for sustained success... I can get behind that.",
                FailureOutcome = "That's a long-term argument for a short-term expense."
            });

            topic.DialogOptions.Add(new OwnerDialogOption
            {
                OptionId = Guid.NewGuid().ToString(),
                DisplayText = "Appeal to fan expectations",
                FullResponse = "The fans expect us to compete. Cutting payroll now sends the wrong message.",
                Tone = DialogTone.Pleading,
                SuccessChance = 30 + finances.TeamOwner.MediaPresence / 3,
                RelationshipImpact = -2,
                SuccessOutcome = "You're right, we can't be seen as not trying...",
                FailureOutcome = "The fans will understand when they see us build a winner the right way."
            });

            topic.DialogOptions.Add(new OwnerDialogOption
            {
                OptionId = Guid.NewGuid().ToString(),
                DisplayText = "Offer cost-saving alternatives",
                FullResponse = "If you're not comfortable with the full amount, let's look at where we can trim to get closer.",
                Tone = DialogTone.Professional,
                SuccessChance = 60,
                RelationshipImpact = 2,
                SuccessOutcome = "That's the kind of flexibility I like to see. Let's work together on this.",
                FailureOutcome = "I appreciate the offer, but even a reduced amount is more than I want to spend."
            });

            return topic;
        }

        private OwnerMeetingTopic CreateStaffBudgetTopic(long amount, string purpose)
        {
            var topic = new OwnerMeetingTopic
            {
                TopicId = Guid.NewGuid().ToString(),
                Title = "Staff Budget Request",
                Description = $"Request for ${amount / 1_000_000f:F2}M for {purpose}",
                IsRequired = true,
                WasDiscussed = false,
                DialogOptions = new List<OwnerDialogOption>()
            };

            topic.DialogOptions.Add(new OwnerDialogOption
            {
                OptionId = Guid.NewGuid().ToString(),
                DisplayText = "Explain the competitive advantage",
                FullResponse = $"Adding this {purpose} will give us an edge over other teams. It's an investment in winning.",
                Tone = DialogTone.Confident,
                SuccessChance = 55,
                RelationshipImpact = 3,
                SuccessOutcome = "If it helps us win, let's do it.",
                FailureOutcome = "I'm not sure the ROI is there."
            });

            topic.DialogOptions.Add(new OwnerDialogOption
            {
                OptionId = Guid.NewGuid().ToString(),
                DisplayText = "Show how it fits the budget",
                FullResponse = "I've found room in our existing budget. This won't significantly impact our bottom line.",
                Tone = DialogTone.Professional,
                SuccessChance = 70,
                RelationshipImpact = 5,
                SuccessOutcome = "Good financial planning. Approved.",
                FailureOutcome = "Let me see those numbers again..."
            });

            topic.DialogOptions.Add(new OwnerDialogOption
            {
                OptionId = Guid.NewGuid().ToString(),
                DisplayText = "Make it personal",
                FullResponse = "I've worked with this person before. I know they'll make a difference.",
                Tone = DialogTone.Humble,
                SuccessChance = 45,
                RelationshipImpact = -1,
                SuccessOutcome = "If you're vouching for them personally, I'll trust your judgment.",
                FailureOutcome = "I need more than personal assurances."
            });

            return topic;
        }

        // === Message Generators ===

        private string GetMeetingNotificationTitle(OwnerMeetingType type)
        {
            return type switch
            {
                OwnerMeetingType.ScheduledCheckIn => "Scheduled Meeting with Owner",
                OwnerMeetingType.BudgetRequest => "Budget Discussion Requested",
                OwnerMeetingType.PerformanceReview => "Performance Review Meeting",
                OwnerMeetingType.HotSeatWarning => "Urgent Meeting with Owner",
                OwnerMeetingType.ContractDiscussion => "Contract Discussion",
                OwnerMeetingType.TradeApproval => "Trade Approval Needed",
                OwnerMeetingType.EmergencyMeeting => "Emergency Meeting Called",
                _ => "Meeting with Owner"
            };
        }

        private string GetMeetingNotificationMessage(OwnerMeetingType type, Owner owner)
        {
            return type switch
            {
                OwnerMeetingType.BudgetRequest =>
                    $"{owner.FullName} wants to discuss your budget request. Be prepared to make your case.",
                OwnerMeetingType.PerformanceReview =>
                    $"{owner.FullName} wants to review the team's performance. This is your chance to address any concerns.",
                OwnerMeetingType.HotSeatWarning =>
                    $"{owner.FullName} has called an urgent meeting. This could be about your job security.",
                OwnerMeetingType.TradeApproval =>
                    $"{owner.FullName} needs to sign off on the proposed trade. Make sure you can justify it.",
                _ => $"{owner.FullName} wants to meet with you."
            };
        }

        private string GetOwnerOpeningStatement(Owner owner, OwnerMeetingType type)
        {
            return (type, owner.SpendingType) switch
            {
                (OwnerMeetingType.BudgetRequest, OwnerType.Lavish) =>
                    "So you need more money? Tell me why this will help us win a championship.",

                (OwnerMeetingType.BudgetRequest, OwnerType.Cheap) =>
                    "I've looked at your request. I have to say, I'm skeptical. Convince me this is absolutely necessary.",

                (OwnerMeetingType.BudgetRequest, _) =>
                    "Let's talk about this budget request. I'm open-minded, but I need to understand the value.",

                (OwnerMeetingType.HotSeatWarning, _) =>
                    "I'll be direct with you. I'm concerned about where this team is headed. What's your plan to turn this around?",

                (OwnerMeetingType.PerformanceReview, _) =>
                    "Let's review how things are going. I want to hear your assessment of the team.",

                (OwnerMeetingType.TradeApproval, OwnerType.Lavish) =>
                    "Walk me through this trade. If it helps us compete, I'm on board.",

                (OwnerMeetingType.TradeApproval, OwnerType.Cheap) =>
                    "This trade has salary implications. Make sure you've considered the financial impact.",

                _ => "Thanks for meeting with me. Let's get started."
            };
        }

        // === Financial Operations ===

        /// <summary>
        /// Process end of season financials
        /// </summary>
        public SeasonFinancials ProcessEndOfSeason(string teamId, int wins, int losses, bool madePlayoffs, string playoffResult)
        {
            var finances = GetTeamFinances(teamId);
            if (finances == null) return null;

            // Calculate final luxury tax
            finances.LuxuryTaxPayment = finances.CalculateLuxuryTax();

            // Record season
            var seasonRecord = new SeasonFinancials
            {
                Season = DateTime.Now.Year,
                TotalRevenue = finances.TotalRevenue,
                TotalExpenses = finances.TotalExpenses,
                NetIncome = finances.NetIncome,
                LuxuryTaxPaid = finances.LuxuryTaxPayment,
                PlayerPayroll = finances.PlayerSalaries,
                StaffPayroll = finances.StaffSalaries,
                Wins = wins,
                Losses = losses,
                MadePlayoffs = madePlayoffs,
                PlayoffResult = playoffResult
            };

            finances.FinancialHistory ??= new List<SeasonFinancials>();
            finances.FinancialHistory.Add(seasonRecord);

            // Update consecutive years in tax
            if (finances.IsOverLuxuryTax)
            {
                finances.ConsecutiveYearsInTax++;
                finances.TeamOwner.TotalLuxuryTaxPaid += finances.LuxuryTaxPayment;
            }
            else
            {
                finances.ConsecutiveYearsInTax = 0;
            }

            return seasonRecord;
        }

        /// <summary>
        /// Update cap numbers for new season
        /// </summary>
        public void UpdateCapNumbers(string teamId, long newSalaryCap, long newLuxuryTax)
        {
            var finances = GetTeamFinances(teamId);
            if (finances == null) return;

            finances.SalaryCap = newSalaryCap;
            finances.LuxuryTaxThreshold = newLuxuryTax;
            finances.ApronLevel = newLuxuryTax + 7_000_000;
        }

        /// <summary>
        /// Update team payroll
        /// </summary>
        public void UpdatePayroll(string teamId, long playerSalaries, long staffSalaries)
        {
            var finances = GetTeamFinances(teamId);
            if (finances == null) return;

            finances.PlayerSalaries = playerSalaries;
            finances.StaffSalaries = staffSalaries;
            finances.CurrentPayroll = playerSalaries + staffSalaries;
        }

        /// <summary>
        /// Get financial summary for display
        /// </summary>
        public FinancialSummary GetFinancialSummary(string teamId)
        {
            var finances = GetTeamFinances(teamId);
            if (finances == null) return null;

            return new FinancialSummary
            {
                TeamName = finances.TeamName,
                SalaryCap = finances.SalaryCap,
                LuxuryTaxLine = finances.LuxuryTaxThreshold,
                CurrentPayroll = finances.CurrentPayroll,
                CapSpace = finances.CapSpace,
                TaxSpace = finances.TaxSpace,
                IsOverTax = finances.IsOverLuxuryTax,
                ProjectedTax = finances.CalculateLuxuryTax(),
                YearsInTax = finances.ConsecutiveYearsInTax,
                IsRepeater = finances.IsRepeaterTaxTeam,
                OwnerWillingness = finances.TeamOwner.LuxuryTaxTolerance,
                OwnerMood = finances.GetOwnerMood(0, 0, false).ToString()
            };
        }
    }

    /// <summary>
    /// Result of selecting a dialog option in meeting
    /// </summary>
    public class MeetingDialogResult
    {
        public bool WasSuccessful;
        public string OwnerResponse;
        public int RelationshipChange;
    }

    /// <summary>
    /// Summary of team financial situation for UI
    /// </summary>
    public class FinancialSummary
    {
        public string TeamName;
        public long SalaryCap;
        public long LuxuryTaxLine;
        public long CurrentPayroll;
        public long CapSpace;
        public long TaxSpace;
        public bool IsOverTax;
        public long ProjectedTax;
        public int YearsInTax;
        public bool IsRepeater;
        public int OwnerWillingness;
        public string OwnerMood;

        public string GetCapSummary()
        {
            if (CapSpace > 0)
                return $"${CapSpace / 1_000_000f:F1}M under cap";
            return $"${Math.Abs(CapSpace) / 1_000_000f:F1}M over cap";
        }

        public string GetTaxSummary()
        {
            if (!IsOverTax)
                return $"${TaxSpace / 1_000_000f:F1}M under luxury tax";

            string taxStr = $"${ProjectedTax / 1_000_000f:F1}M luxury tax bill";
            if (IsRepeater)
                taxStr += " (REPEATER)";
            return taxStr;
        }
    }
}
