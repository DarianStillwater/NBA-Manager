using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Job security status for the player's coaching position
    /// </summary>
    public enum JobSecurityStatus
    {
        Untouchable,     // Elite performance, contract extension likely
        Secure,          // Meeting expectations, no concerns
        Stable,          // Adequate performance, could improve
        Uncertain,       // Owner has concerns, need to improve
        HotSeat,         // In danger of being fired
        FinalWarning,    // One more bad stretch = fired
        Fired            // Lost the job
    }

    /// <summary>
    /// Type of coaching contract
    /// </summary>
    public enum CoachContractType
    {
        Standard,            // Normal coaching contract
        InterimCoach,        // Temporary position
        MultiYearGuaranteed, // Fully guaranteed
        PerformanceBased     // Incentive-laden
    }

    /// <summary>
    /// Represents the player's coaching contract
    /// </summary>
    [Serializable]
    public class CoachContract
    {
        public string ContractId;
        public string TeamId;

        [Header("Terms")]
        public int TotalYears;
        public int CurrentYear;
        public long AnnualSalary;
        public long TotalValue;

        [Header("Guarantees")]
        public int GuaranteedYears;
        public long GuaranteedMoney;
        public bool IsFullyGuaranteed;

        [Header("Options")]
        public bool HasTeamOption;
        public int TeamOptionYear;
        public bool HasCoachOption;
        public int CoachOptionYear;

        [Header("Incentives")]
        public List<CoachContractIncentive> Incentives;

        [Header("Buyout")]
        public long BuyoutAmount;           // What team pays if they fire you
        public float BuyoutPercentage;      // Percentage of remaining money

        public DateTime StartDate;
        public DateTime EndDate;
        public CoachContractType ContractType;

        public int YearsRemaining => TotalYears - CurrentYear;

        public long RemainingValue => AnnualSalary * YearsRemaining;

        public long CalculateBuyout()
        {
            if (BuyoutAmount > 0) return BuyoutAmount;
            return (long)(RemainingValue * BuyoutPercentage);
        }

        public bool IsExpiring => YearsRemaining <= 1;

        /// <summary>
        /// Calculate total earned incentives for the season
        /// </summary>
        public long CalculateEarnedIncentives(int wins, bool madePlayoffs, string playoffResult, bool wonChampionship)
        {
            long earned = 0;

            if (Incentives == null) return earned;

            foreach (var incentive in Incentives)
            {
                if (incentive.WasAchieved(wins, madePlayoffs, playoffResult, wonChampionship))
                {
                    earned += incentive.Amount;
                }
            }

            return earned;
        }

        /// <summary>
        /// Create a standard coaching contract
        /// </summary>
        public static CoachContract CreateStandardContract(string teamId, int years, long annualSalary)
        {
            return new CoachContract
            {
                ContractId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                TotalYears = years,
                CurrentYear = 1,
                AnnualSalary = annualSalary,
                TotalValue = annualSalary * years,
                GuaranteedYears = Math.Max(1, years - 1),
                GuaranteedMoney = annualSalary * Math.Max(1, years - 1),
                IsFullyGuaranteed = false,
                HasTeamOption = years >= 4,
                TeamOptionYear = years,
                HasCoachOption = false,
                BuyoutPercentage = 0.5f,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddYears(years),
                ContractType = CoachContractType.Standard,
                Incentives = new List<CoachContractIncentive>()
            };
        }

        /// <summary>
        /// Create an interim coach contract
        /// </summary>
        public static CoachContract CreateInterimContract(string teamId, long annualSalary)
        {
            return new CoachContract
            {
                ContractId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                TotalYears = 1,
                CurrentYear = 1,
                AnnualSalary = annualSalary,
                TotalValue = annualSalary,
                GuaranteedYears = 0,
                GuaranteedMoney = 0,
                IsFullyGuaranteed = false,
                HasTeamOption = false,
                HasCoachOption = false,
                BuyoutPercentage = 0f, // No buyout for interim
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(6),
                ContractType = CoachContractType.InterimCoach,
                Incentives = new List<CoachContractIncentive>()
            };
        }
    }

    /// <summary>
    /// Incentive clause in coaching contract
    /// </summary>
    [Serializable]
    public class CoachContractIncentive
    {
        public string Description;
        public IncentiveType Type;
        public int TargetValue;           // Wins, round reached, etc.
        public long Amount;
        public bool IsAchieved;

        public bool WasAchieved(int wins, bool madePlayoffs, string playoffResult, bool wonChampionship)
        {
            return Type switch
            {
                IncentiveType.WinTotal => wins >= TargetValue,
                IncentiveType.PlayoffAppearance => madePlayoffs,
                IncentiveType.ConferenceFinals => playoffResult is "Conference Finals" or "Finals" or "Champion",
                IncentiveType.FinalsAppearance => playoffResult is "Finals" or "Champion",
                IncentiveType.Championship => wonChampionship,
                IncentiveType.CoachOfYear => false, // Determined separately
                _ => false
            };
        }
    }

    /// <summary>
    /// Types of coaching contract incentives
    /// </summary>
    public enum IncentiveType
    {
        WinTotal,           // Reach X wins
        PlayoffAppearance,  // Make playoffs
        ConferenceFinals,   // Reach conference finals
        FinalsAppearance,   // Reach NBA Finals
        Championship,       // Win championship
        CoachOfYear         // Win COTY award
    }

    /// <summary>
    /// A single season in the coach's career
    /// </summary>
    [Serializable]
    public class CoachSeasonRecord
    {
        public int Season;
        public string TeamId;
        public string TeamName;

        public int Wins;
        public int Losses;

        public bool MadePlayoffs;
        public string PlayoffResult;      // "First Round", "Conference Semis", "Conference Finals", "Finals", "Champion"
        public int PlayoffWins;
        public int PlayoffLosses;

        public bool WonChampionship;
        public bool WonCoachOfYear;
        public bool WasFired;
        public bool Resigned;

        public long SalaryEarned;
        public long IncentivesEarned;

        public float WinPercentage => Wins + Losses > 0 ? Wins / (float)(Wins + Losses) : 0;
    }

    /// <summary>
    /// Owner expectations for the season
    /// </summary>
    [Serializable]
    public class SeasonExpectations
    {
        public int MinimumWins;
        public int TargetWins;
        public int StretchWins;           // Impressive performance

        public bool ExpectsPlayoffs;
        public string MinimumPlayoffResult;

        public bool ExpectsDevelopment;   // Focus on young players
        public List<string> PlayersToDevelope;

        public string OwnerStatement;     // What owner said about expectations

        /// <summary>
        /// Evaluate how well expectations were met
        /// </summary>
        public ExpectationResult Evaluate(int wins, bool madePlayoffs, string playoffResult)
        {
            if (wins >= StretchWins)
                return ExpectationResult.Exceeded;

            if (wins >= TargetWins)
                return ExpectationResult.Met;

            if (wins >= MinimumWins)
            {
                if (ExpectsPlayoffs && !madePlayoffs)
                    return ExpectationResult.BelowExpectations;
                return ExpectationResult.Adequate;
            }

            return ExpectationResult.Failed;
        }
    }

    /// <summary>
    /// Result of season expectation evaluation
    /// </summary>
    public enum ExpectationResult
    {
        Exceeded,           // Above all expectations
        Met,                // Hit target
        Adequate,           // Met minimum
        BelowExpectations,  // Below minimum but not disastrous
        Failed              // Major disappointment
    }

    /// <summary>
    /// Complete coaching career data for the player
    /// </summary>
    [Serializable]
    public class CoachCareer
    {
        [Header("Identity")]
        public string CoachId;
        public string FirstName;
        public string LastName;
        public int Age;

        [Header("Current Position")]
        public string CurrentTeamId;
        public string CurrentTeamName;
        public CoachContract CurrentContract;
        public JobSecurityStatus JobSecurity;
        public SeasonExpectations CurrentExpectations;

        [Header("Reputation")]
        [Range(0, 100)]
        public int Reputation;              // Overall standing in coaching circles
        [Range(0, 100)]
        public int WinningReputation;       // Known as a winner
        [Range(0, 100)]
        public int DevelopmentReputation;   // Known for developing players
        [Range(0, 100)]
        public int TacticalReputation;      // Known for X's and O's
        [Range(0, 100)]
        public int PlayerRelationsReputation; // Known for player management

        [Header("Career Statistics")]
        public int CareerWins;
        public int CareerLosses;
        public int PlayoffWins;
        public int PlayoffLosses;
        public int Championships;
        public int ConferenceChampionships;
        public int DivisionTitles;
        public int PlayoffAppearances;
        public int CoachOfYearAwards;

        [Header("Career History")]
        public List<CoachSeasonRecord> SeasonHistory;
        public List<string> TeamsCoached;
        public int YearsCoaching;

        [Header("Financial")]
        public long CareerEarnings;
        public long CurrentNetWorth;

        [Header("Status")]
        public bool IsEmployed;
        public bool IsAvailable;            // Looking for work
        public DateTime LastFired;          // When last fired (affects opportunities)

        [Header("Former Player Info")]
        public bool IsFormerPlayer;         // Whether this coach was a former NBA player
        public string FormerPlayerId;       // Original player ID if former player
        public List<string> FormerTeams;    // Teams played for as a player

        public string FullName => $"{FirstName} {LastName}";

        public float CareerWinPercentage => CareerWins + CareerLosses > 0
            ? CareerWins / (float)(CareerWins + CareerLosses)
            : 0;

        public float PlayoffWinPercentage => PlayoffWins + PlayoffLosses > 0
            ? PlayoffWins / (float)(PlayoffWins + PlayoffLosses)
            : 0;

        /// <summary>
        /// Get overall coaching grade based on career
        /// </summary>
        public string GetCoachingGrade()
        {
            int score = 0;

            // Championships worth most
            score += Championships * 20;
            score += ConferenceChampionships * 8;
            score += PlayoffAppearances * 3;

            // Win percentage
            score += (int)(CareerWinPercentage * 30);

            // Years of experience
            score += Math.Min(YearsCoaching * 2, 20);

            // Reputation
            score += Reputation / 5;

            if (score >= 100) return "A+ (Elite)";
            if (score >= 85) return "A (Excellent)";
            if (score >= 70) return "B+ (Very Good)";
            if (score >= 55) return "B (Good)";
            if (score >= 40) return "C+ (Above Average)";
            if (score >= 25) return "C (Average)";
            if (score >= 15) return "D (Below Average)";
            return "F (Unproven)";
        }

        /// <summary>
        /// Get market value based on career accomplishments
        /// </summary>
        public long GetMarketValue()
        {
            long baseValue = 3_000_000; // $3M base

            // Championship bonus
            baseValue += Championships * 2_000_000;

            // Win percentage bonus
            if (CareerWins >= 50)
            {
                baseValue += (long)(CareerWinPercentage * 5_000_000);
            }

            // Experience bonus
            baseValue += Math.Min(YearsCoaching * 200_000, 2_000_000);

            // Reputation bonus
            baseValue += Reputation * 20_000;

            // Recent firing penalty
            if (LastFired != DateTime.MinValue && (DateTime.Now - LastFired).TotalDays < 365)
            {
                baseValue = (long)(baseValue * 0.7f);
            }

            return baseValue;
        }

        /// <summary>
        /// Calculate job attractiveness based on career
        /// </summary>
        public int GetJobAttractiveness()
        {
            int attractiveness = 50; // Base

            attractiveness += Championships * 10;
            attractiveness += (int)(CareerWinPercentage * 20);
            attractiveness += Reputation / 4;

            // Negative factors
            if (JobSecurity == JobSecurityStatus.Fired)
                attractiveness -= 15;

            // Recent seasons matter more
            if (SeasonHistory?.Count > 0)
            {
                var lastSeason = SeasonHistory[^1];
                if (lastSeason.WinPercentage > 0.6f) attractiveness += 10;
                if (lastSeason.WasFired) attractiveness -= 20;
            }

            return Mathf.Clamp(attractiveness, 0, 100);
        }

        /// <summary>
        /// Record a completed season
        /// </summary>
        public void RecordSeason(CoachSeasonRecord season)
        {
            SeasonHistory ??= new List<CoachSeasonRecord>();
            SeasonHistory.Add(season);

            CareerWins += season.Wins;
            CareerLosses += season.Losses;
            PlayoffWins += season.PlayoffWins;
            PlayoffLosses += season.PlayoffLosses;

            if (season.MadePlayoffs) PlayoffAppearances++;
            if (season.WonChampionship) Championships++;
            if (season.WonCoachOfYear) CoachOfYearAwards++;

            CareerEarnings += season.SalaryEarned + season.IncentivesEarned;
            YearsCoaching++;

            // Update reputation based on performance
            UpdateReputation(season);
        }

        /// <summary>
        /// Update reputation based on season performance
        /// </summary>
        private void UpdateReputation(CoachSeasonRecord season)
        {
            // Winning reputation
            if (season.WinPercentage > 0.65f)
                WinningReputation = Mathf.Min(100, WinningReputation + 5);
            else if (season.WinPercentage < 0.35f)
                WinningReputation = Mathf.Max(0, WinningReputation - 5);

            // Championship bump
            if (season.WonChampionship)
            {
                Reputation = Mathf.Min(100, Reputation + 15);
                WinningReputation = Mathf.Min(100, WinningReputation + 10);
            }

            // Firing penalty
            if (season.WasFired)
            {
                Reputation = Mathf.Max(0, Reputation - 10);
            }

            // Overall reputation moves toward component average
            int avgComponent = (WinningReputation + DevelopmentReputation + TacticalReputation + PlayerRelationsReputation) / 4;
            Reputation = (Reputation + avgComponent) / 2;
        }

        /// <summary>
        /// Handle being fired
        /// </summary>
        public void GetFired()
        {
            IsEmployed = false;
            IsAvailable = true;
            JobSecurity = JobSecurityStatus.Fired;
            LastFired = DateTime.Now;

            if (SeasonHistory?.Count > 0)
            {
                SeasonHistory[^1].WasFired = true;
            }

            // Calculate severance from buyout
            if (CurrentContract != null)
            {
                CurrentNetWorth += CurrentContract.CalculateBuyout();
            }

            CurrentTeamId = null;
            CurrentTeamName = null;
            CurrentContract = null;
        }

        /// <summary>
        /// Accept a new job
        /// </summary>
        public void AcceptJob(string teamId, string teamName, CoachContract contract)
        {
            CurrentTeamId = teamId;
            CurrentTeamName = teamName;
            CurrentContract = contract;
            IsEmployed = true;
            IsAvailable = false;
            JobSecurity = JobSecurityStatus.Stable;

            TeamsCoached ??= new List<string>();
            if (!TeamsCoached.Contains(teamId))
            {
                TeamsCoached.Add(teamId);
            }
        }

        /// <summary>
        /// Create a new coaching career
        /// </summary>
        public static CoachCareer CreateNewCareer(string firstName, string lastName, int age = 45)
        {
            return new CoachCareer
            {
                CoachId = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                Age = age,
                Reputation = 50,
                WinningReputation = 50,
                DevelopmentReputation = 50,
                TacticalReputation = 50,
                PlayerRelationsReputation = 50,
                CareerWins = 0,
                CareerLosses = 0,
                PlayoffWins = 0,
                PlayoffLosses = 0,
                Championships = 0,
                YearsCoaching = 0,
                SeasonHistory = new List<CoachSeasonRecord>(),
                TeamsCoached = new List<string>(),
                IsEmployed = false,
                IsAvailable = true,
                CareerEarnings = 0,
                CurrentNetWorth = 0
            };
        }

        /// <summary>
        /// Create career with previous experience
        /// </summary>
        public static CoachCareer CreateExperiencedCareer(
            string firstName, string lastName, int age,
            int yearsExperience, int careerWins, int careerLosses,
            int championships = 0)
        {
            var career = CreateNewCareer(firstName, lastName, age);
            career.YearsCoaching = yearsExperience;
            career.CareerWins = careerWins;
            career.CareerLosses = careerLosses;
            career.Championships = championships;

            // Adjust reputation based on experience
            career.Reputation = Mathf.Clamp(50 + yearsExperience * 2 + championships * 10, 0, 95);
            career.WinningReputation = Mathf.Clamp(50 + (int)(career.CareerWinPercentage * 40), 0, 95);

            return career;
        }
    }

    /// <summary>
    /// Message from the owner about job security
    /// </summary>
    [Serializable]
    public class OwnerJobSecurityMessage
    {
        public string MessageId;
        public DateTime Date;
        public JobSecurityStatus SecurityLevel;
        public string Subject;
        public string Message;
        public bool RequiresResponse;
        public List<string> ResponseOptions;
        public string SelectedResponse;
        public bool WasRead;
    }
}
