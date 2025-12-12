using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Owner personality type affecting team management
    /// </summary>
    public enum OwnerType
    {
        Lavish,          // Will spend whatever it takes to win
        Competitive,     // Willing to spend for contention
        Balanced,        // Reasonable budget management
        FrugalButFair,   // Tries to minimize costs but not extreme
        Cheap            // Avoids luxury tax at all costs
    }

    /// <summary>
    /// Owner's preference for team strategy
    /// </summary>
    public enum OwnerPhilosophy
    {
        WinNow,          // Championships over everything
        SustainedSuccess, // Build a perennial contender
        Balanced,        // Mix of winning and development
        Development,     // Focus on building through draft
        Profit           // Maximize revenue, minimize costs
    }

    /// <summary>
    /// Represents the team owner and their personality/preferences
    /// </summary>
    [Serializable]
    public class Owner
    {
        [Header("Identity")]
        public string OwnerId;
        public string FirstName;
        public string LastName;
        public string OwnershipGroupName;  // If not individual owner

        [Header("Personality")]
        public OwnerType SpendingType;
        public OwnerPhilosophy Philosophy;

        [Header("Attributes")]
        [Range(0, 100)]
        public int Patience;              // How long before demanding results
        [Range(0, 100)]
        public int Involvement;           // How much they meddle in operations
        [Range(0, 100)]
        public int MediaPresence;         // How often they make public statements
        [Range(0, 100)]
        public int PlayerRelations;       // How well they connect with players

        [Header("Financial Preferences")]
        [Range(0, 100)]
        public int LuxuryTaxTolerance;    // Willingness to pay luxury tax
        [Range(0, 100)]
        public int RepeaterTaxTolerance;  // Willingness to pay repeater tax
        public int MaxYearsInTax;         // How many consecutive years they'll accept

        [Header("Expectations")]
        public int MinAcceptableWins;     // Below this, coach is on hot seat
        public bool ExpectsPlayoffs;
        public bool ExpectsDeepPlayoffRun;
        public bool ExpectsChampionship;

        [Header("Track Record")]
        public int YearsAsOwner;
        public int ChampionshipsWon;
        public int PlayoffAppearances;
        public long TotalLuxuryTaxPaid;

        public string FullName => string.IsNullOrEmpty(OwnershipGroupName)
            ? $"{FirstName} {LastName}"
            : OwnershipGroupName;

        /// <summary>
        /// Get base budget multiplier based on spending type
        /// </summary>
        public float GetBudgetMultiplier()
        {
            return SpendingType switch
            {
                OwnerType.Lavish => 1.3f,
                OwnerType.Competitive => 1.15f,
                OwnerType.Balanced => 1.0f,
                OwnerType.FrugalButFair => 0.9f,
                OwnerType.Cheap => 0.75f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Check if owner would approve luxury tax spending
        /// </summary>
        public bool WouldApproveLuxuryTax(int currentYearsInTax, bool isContending)
        {
            // Base willingness
            int willingness = LuxuryTaxTolerance;

            // More willing if contending
            if (isContending)
                willingness += 20;

            // Less willing if repeater tax territory
            if (currentYearsInTax >= 3)
                willingness -= 30;

            // Check against max years tolerance
            if (currentYearsInTax >= MaxYearsInTax)
                willingness -= 50;

            return UnityEngine.Random.Range(0, 100) < willingness;
        }

        /// <summary>
        /// Generate owner dialog based on personality
        /// </summary>
        public string GetOwnerQuote(OwnerMood mood)
        {
            return (SpendingType, mood) switch
            {
                (OwnerType.Lavish, OwnerMood.Happy) =>
                    "Money is no object when we're winning. Keep it up!",
                (OwnerType.Lavish, OwnerMood.Neutral) =>
                    "I didn't buy this team to be mediocre. What do you need?",
                (OwnerType.Lavish, OwnerMood.Unhappy) =>
                    "I'm spending more than anyone. Why aren't we winning?",

                (OwnerType.Competitive, OwnerMood.Happy) =>
                    "This is what championship contention looks like. Well done.",
                (OwnerType.Competitive, OwnerMood.Neutral) =>
                    "We need to be competing for titles. What's the plan?",
                (OwnerType.Competitive, OwnerMood.Unhappy) =>
                    "I expect us to be in the mix every year. This isn't acceptable.",

                (OwnerType.Balanced, OwnerMood.Happy) =>
                    "Great season. We're building something sustainable here.",
                (OwnerType.Balanced, OwnerMood.Neutral) =>
                    "Let's be smart with our resources while staying competitive.",
                (OwnerType.Balanced, OwnerMood.Unhappy) =>
                    "We need to find a better balance between spending and results.",

                (OwnerType.FrugalButFair, OwnerMood.Happy) =>
                    "Excellent work maximizing our roster with the budget we have.",
                (OwnerType.FrugalButFair, OwnerMood.Neutral) =>
                    "We need to be creative. There's only so much in the budget.",
                (OwnerType.FrugalButFair, OwnerMood.Unhappy) =>
                    "I understand we have constraints, but we should be doing better.",

                (OwnerType.Cheap, OwnerMood.Happy) =>
                    "See? You don't need to overspend to win games.",
                (OwnerType.Cheap, OwnerMood.Neutral) =>
                    "I'm not paying the luxury tax. Work with what you have.",
                (OwnerType.Cheap, OwnerMood.Unhappy) =>
                    "The fans are unhappy, but I'm not opening the checkbook.",

                _ => "Let's discuss the state of the team."
            };
        }

        // === Factory Methods ===

        public static Owner CreateLavishOwner(string firstName, string lastName)
        {
            return new Owner
            {
                OwnerId = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                SpendingType = OwnerType.Lavish,
                Philosophy = OwnerPhilosophy.WinNow,
                Patience = UnityEngine.Random.Range(30, 50),
                Involvement = UnityEngine.Random.Range(50, 80),
                MediaPresence = UnityEngine.Random.Range(60, 90),
                PlayerRelations = UnityEngine.Random.Range(60, 85),
                LuxuryTaxTolerance = UnityEngine.Random.Range(80, 100),
                RepeaterTaxTolerance = UnityEngine.Random.Range(60, 90),
                MaxYearsInTax = UnityEngine.Random.Range(5, 10),
                MinAcceptableWins = 45,
                ExpectsPlayoffs = true,
                ExpectsDeepPlayoffRun = true,
                ExpectsChampionship = true
            };
        }

        public static Owner CreateCheapOwner(string firstName, string lastName)
        {
            return new Owner
            {
                OwnerId = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                SpendingType = OwnerType.Cheap,
                Philosophy = OwnerPhilosophy.Profit,
                Patience = UnityEngine.Random.Range(50, 70),
                Involvement = UnityEngine.Random.Range(30, 60),
                MediaPresence = UnityEngine.Random.Range(20, 50),
                PlayerRelations = UnityEngine.Random.Range(30, 50),
                LuxuryTaxTolerance = UnityEngine.Random.Range(5, 25),
                RepeaterTaxTolerance = UnityEngine.Random.Range(0, 15),
                MaxYearsInTax = UnityEngine.Random.Range(0, 2),
                MinAcceptableWins = 30,
                ExpectsPlayoffs = false,
                ExpectsDeepPlayoffRun = false,
                ExpectsChampionship = false
            };
        }

        public static Owner CreateBalancedOwner(string firstName, string lastName)
        {
            return new Owner
            {
                OwnerId = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                SpendingType = OwnerType.Balanced,
                Philosophy = OwnerPhilosophy.SustainedSuccess,
                Patience = UnityEngine.Random.Range(50, 70),
                Involvement = UnityEngine.Random.Range(40, 60),
                MediaPresence = UnityEngine.Random.Range(40, 60),
                PlayerRelations = UnityEngine.Random.Range(50, 70),
                LuxuryTaxTolerance = UnityEngine.Random.Range(40, 70),
                RepeaterTaxTolerance = UnityEngine.Random.Range(30, 50),
                MaxYearsInTax = UnityEngine.Random.Range(2, 5),
                MinAcceptableWins = 38,
                ExpectsPlayoffs = true,
                ExpectsDeepPlayoffRun = false,
                ExpectsChampionship = false
            };
        }
    }

    /// <summary>
    /// Owner's current mood/disposition
    /// </summary>
    public enum OwnerMood
    {
        Happy,
        Neutral,
        Unhappy
    }

    /// <summary>
    /// Revenue and expense line item
    /// </summary>
    [Serializable]
    public class FinancialLineItem
    {
        public string Category;
        public string Description;
        public long Amount;
        public bool IsRecurring;
        public DateTime Date;
    }

    /// <summary>
    /// Represents a franchise's complete financial picture
    /// </summary>
    [Serializable]
    public class TeamFinances
    {
        [Header("Team Info")]
        public string TeamId;
        public string TeamName;
        public string TeamCity;

        [Header("Ownership")]
        public Owner TeamOwner;

        [Header("Market & Arena")]
        [Range(1, 30)]
        public int MarketSize;              // 1 = largest market, 30 = smallest
        public string ArenaName;
        public int ArenaCapacity;
        [Range(1, 5)]
        public int ArenaQuality;            // Stars rating

        [Header("Revenue Streams")]
        public long TicketRevenue;
        public long TVMediaRevenue;
        public long MerchandiseRevenue;
        public long SponsorshipRevenue;
        public long OtherRevenue;

        [Header("Expenses")]
        public long PlayerSalaries;
        public long StaffSalaries;          // Coaches, scouts, trainers
        public long LuxuryTaxPayment;
        public long FacilityOperations;
        public long TravelExpenses;
        public long OtherExpenses;

        [Header("Cap Situation")]
        public long SalaryCap;
        public long LuxuryTaxThreshold;
        public long ApronLevel;
        public long CurrentPayroll;
        public int ConsecutiveYearsInTax;

        [Header("Financial History")]
        public List<SeasonFinancials> FinancialHistory;

        public long TotalRevenue => TicketRevenue + TVMediaRevenue + MerchandiseRevenue +
                                    SponsorshipRevenue + OtherRevenue;

        public long TotalExpenses => PlayerSalaries + StaffSalaries + LuxuryTaxPayment +
                                     FacilityOperations + TravelExpenses + OtherExpenses;

        public long NetIncome => TotalRevenue - TotalExpenses;

        public bool IsOverLuxuryTax => CurrentPayroll > LuxuryTaxThreshold;

        public bool IsRepeaterTaxTeam => ConsecutiveYearsInTax >= 3;

        public long CapSpace => Math.Max(0, SalaryCap - CurrentPayroll);

        public long TaxSpace => Math.Max(0, LuxuryTaxThreshold - CurrentPayroll);

        /// <summary>
        /// Calculate luxury tax owed based on current payroll
        /// </summary>
        public long CalculateLuxuryTax()
        {
            if (!IsOverLuxuryTax) return 0;

            long overTax = CurrentPayroll - LuxuryTaxThreshold;

            // Progressive luxury tax brackets
            long tax = 0;
            long[] brackets = { 5_000_000, 10_000_000, 15_000_000, 20_000_000 };
            float[] rates = { 1.50f, 1.75f, 2.50f, 3.25f, 3.75f };

            // Repeater penalty
            float repeaterMultiplier = IsRepeaterTaxTeam ? 1.0f : 0f;
            float[] repeaterRates = { 2.50f, 2.75f, 3.50f, 4.25f, 4.75f };

            long remaining = overTax;
            int bracket = 0;

            while (remaining > 0 && bracket < rates.Length)
            {
                long bracketAmount = bracket < brackets.Length
                    ? Math.Min(remaining, brackets[bracket])
                    : remaining;

                float rate = IsRepeaterTaxTeam ? repeaterRates[bracket] : rates[bracket];
                tax += (long)(bracketAmount * rate);

                remaining -= bracketAmount;
                bracket++;
            }

            return tax;
        }

        /// <summary>
        /// Get budget available for staff (coaches, scouts)
        /// </summary>
        public long GetStaffBudget()
        {
            // Base staff budget scaled by owner spending type
            long baseBudget = 15_000_000; // $15M base
            return (long)(baseBudget * TeamOwner.GetBudgetMultiplier());
        }

        /// <summary>
        /// Get remaining staff budget after current expenses
        /// </summary>
        public long GetRemainingStaffBudget()
        {
            return Math.Max(0, GetStaffBudget() - StaffSalaries);
        }

        /// <summary>
        /// Calculate expected ticket revenue based on factors
        /// </summary>
        public long ProjectTicketRevenue(int projectedWins, bool hasStarPlayer)
        {
            // Base revenue from arena capacity and average ticket price
            long baseRevenue = ArenaCapacity * 41 * 80; // 41 home games, $80 avg ticket

            // Market size modifier
            float marketModifier = 1f + ((30 - MarketSize) / 30f * 0.5f);

            // Winning modifier
            float winModifier = 0.7f + (projectedWins / 82f * 0.6f);

            // Star player modifier
            float starModifier = hasStarPlayer ? 1.15f : 1f;

            // Arena quality modifier
            float arenaModifier = 0.8f + (ArenaQuality / 5f * 0.4f);

            return (long)(baseRevenue * marketModifier * winModifier * starModifier * arenaModifier);
        }

        /// <summary>
        /// Project total revenue for upcoming season
        /// </summary>
        public long ProjectTotalRevenue(int projectedWins, bool hasStarPlayer)
        {
            long ticketProj = ProjectTicketRevenue(projectedWins, hasStarPlayer);

            // TV revenue based on market size (national revenue share + local deals)
            long tvProj = 80_000_000 + (long)((30 - MarketSize) / 30f * 70_000_000);

            // Merchandise scales with winning and stars
            long merchProj = (long)(20_000_000 * (hasStarPlayer ? 1.5f : 1f) * (projectedWins / 41f));

            // Sponsorships based on market
            long sponsorProj = 30_000_000 + (long)((30 - MarketSize) / 30f * 20_000_000);

            return ticketProj + tvProj + merchProj + sponsorProj;
        }

        /// <summary>
        /// Get owner's current mood based on team performance
        /// </summary>
        public OwnerMood GetOwnerMood(int currentWins, int currentLosses, bool isPlayoffBound)
        {
            float winPct = currentWins / (float)(currentWins + currentLosses);

            // Check against expectations
            bool meetsWinExpectation = currentWins >= TeamOwner.MinAcceptableWins * (currentWins + currentLosses) / 82f;
            bool meetsPlayoffExpectation = !TeamOwner.ExpectsPlayoffs || isPlayoffBound;

            if (meetsWinExpectation && meetsPlayoffExpectation && winPct > 0.55f)
                return OwnerMood.Happy;

            if (!meetsWinExpectation || (!meetsPlayoffExpectation && currentWins + currentLosses > 50))
                return OwnerMood.Unhappy;

            return OwnerMood.Neutral;
        }

        /// <summary>
        /// Create initial finances for a team
        /// </summary>
        public static TeamFinances CreateForTeam(string teamId, string teamName, string city,
            int marketSize, Owner owner, long salaryCap, long luxuryTax)
        {
            return new TeamFinances
            {
                TeamId = teamId,
                TeamName = teamName,
                TeamCity = city,
                TeamOwner = owner,
                MarketSize = marketSize,
                ArenaCapacity = UnityEngine.Random.Range(18000, 21000),
                ArenaQuality = UnityEngine.Random.Range(2, 5),
                SalaryCap = salaryCap,
                LuxuryTaxThreshold = luxuryTax,
                ApronLevel = luxuryTax + 7_000_000,
                ConsecutiveYearsInTax = 0,
                FinancialHistory = new List<SeasonFinancials>()
            };
        }
    }

    /// <summary>
    /// Historical financial data for a single season
    /// </summary>
    [Serializable]
    public class SeasonFinancials
    {
        public int Season;
        public long TotalRevenue;
        public long TotalExpenses;
        public long NetIncome;
        public long LuxuryTaxPaid;
        public long PlayerPayroll;
        public long StaffPayroll;
        public int Wins;
        public int Losses;
        public bool MadePlayoffs;
        public string PlayoffResult;
    }

    /// <summary>
    /// Request from coach to owner for budget/spending approval
    /// </summary>
    [Serializable]
    public class OwnerApprovalRequest
    {
        public string RequestId;
        public string RequestType;          // "LuxuryTax", "StaffHire", "FacilityUpgrade"
        public string Description;
        public long Amount;
        public string Justification;
        public DateTime RequestDate;

        public OwnerApprovalStatus Status;
        public string OwnerResponse;
        public DateTime ResponseDate;
    }

    /// <summary>
    /// Status of an owner approval request
    /// </summary>
    public enum OwnerApprovalStatus
    {
        Pending,
        Approved,
        Denied,
        NeedsDiscussion,    // Requires meeting
        Conditional         // Approved with conditions
    }

    /// <summary>
    /// Represents a meeting between coach and owner
    /// </summary>
    [Serializable]
    public class OwnerMeeting
    {
        public string MeetingId;
        public DateTime ScheduledDate;
        public OwnerMeetingType MeetingType;
        public string Agenda;

        public List<OwnerMeetingTopic> Topics;
        public List<OwnerDialogOption> AvailableResponses;

        public bool IsCompleted;
        public string Outcome;
        public int RelationshipChange;
    }

    /// <summary>
    /// Type of owner meeting
    /// </summary>
    public enum OwnerMeetingType
    {
        ScheduledCheckIn,       // Regular meeting
        BudgetRequest,          // Asking for money
        PerformanceReview,      // Discussing results
        HotSeatWarning,         // You're in trouble
        ContractDiscussion,     // Your contract
        TradeApproval,          // Big trade needs approval
        EmergencyMeeting        // Crisis situation
    }

    /// <summary>
    /// Topic to discuss in owner meeting
    /// </summary>
    [Serializable]
    public class OwnerMeetingTopic
    {
        public string TopicId;
        public string Title;
        public string Description;
        public bool IsRequired;
        public bool WasDiscussed;

        public List<OwnerDialogOption> DialogOptions;
        public OwnerDialogOption SelectedOption;
    }

    /// <summary>
    /// Dialog choice when talking to owner
    /// </summary>
    [Serializable]
    public class OwnerDialogOption
    {
        public string OptionId;
        public string DisplayText;          // What player sees
        public string FullResponse;         // Full dialog when selected
        public DialogTone Tone;

        public int SuccessChance;           // % chance of positive outcome
        public int RelationshipImpact;      // + or - to relationship
        public string SuccessOutcome;
        public string FailureOutcome;
    }

    /// <summary>
    /// Tone of dialog option
    /// </summary>
    public enum DialogTone
    {
        Professional,
        Confident,
        Humble,
        Aggressive,
        Pleading,
        Defiant
    }

    /// <summary>
    /// Tracks the coach's relationship with the owner
    /// </summary>
    [Serializable]
    public class CoachOwnerRelationship
    {
        public string CoachId;
        public string OwnerId;

        [Range(0, 100)]
        public int Trust;           // How much owner trusts coach's decisions

        [Range(0, 100)]
        public int Respect;         // Professional respect

        [Range(0, 100)]
        public int Communication;   // Quality of communication

        public int MeetingsHeld;
        public int PromisesMade;
        public int PromisesKept;

        public List<string> PositiveHistory;    // Good things remembered
        public List<string> NegativeHistory;    // Bad things remembered

        public int OverallRelationship => (Trust + Respect + Communication) / 3;

        /// <summary>
        /// Modify relationship based on outcome
        /// </summary>
        public void ModifyRelationship(int trustDelta, int respectDelta, int commDelta)
        {
            Trust = Mathf.Clamp(Trust + trustDelta, 0, 100);
            Respect = Mathf.Clamp(Respect + respectDelta, 0, 100);
            Communication = Mathf.Clamp(Communication + commDelta, 0, 100);
        }

        /// <summary>
        /// Record a promise made to owner
        /// </summary>
        public void MakePromise(string promise)
        {
            PromisesMade++;
            PositiveHistory ??= new List<string>();
            PositiveHistory.Add($"Promised: {promise}");
        }

        /// <summary>
        /// Record a kept promise
        /// </summary>
        public void KeepPromise(string promise)
        {
            PromisesKept++;
            Trust = Mathf.Min(100, Trust + 5);
            PositiveHistory ??= new List<string>();
            PositiveHistory.Add($"Delivered on: {promise}");
        }

        /// <summary>
        /// Record a broken promise
        /// </summary>
        public void BreakPromise(string promise)
        {
            Trust = Mathf.Max(0, Trust - 15);
            NegativeHistory ??= new List<string>();
            NegativeHistory.Add($"Failed to deliver: {promise}");
        }

        /// <summary>
        /// Get relationship status description
        /// </summary>
        public string GetRelationshipStatus()
        {
            int overall = OverallRelationship;

            if (overall >= 85) return "Excellent - Owner has complete faith in you";
            if (overall >= 70) return "Strong - Owner is very supportive";
            if (overall >= 55) return "Good - Healthy professional relationship";
            if (overall >= 40) return "Neutral - Room for improvement";
            if (overall >= 25) return "Strained - Owner has concerns";
            return "Poor - Your position is in jeopardy";
        }
    }
}
