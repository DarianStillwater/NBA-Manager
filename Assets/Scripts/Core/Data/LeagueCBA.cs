using System;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// NBA Collective Bargaining Agreement constants and calculations.
    /// Based on 2024-25 season figures.
    /// </summary>
    public static class LeagueCBA
    {
        // ==================== 2024-25 SALARY THRESHOLDS ====================
        
        /// <summary>Salary Cap - soft cap that can be exceeded with exceptions</summary>
        public const long SALARY_CAP = 140_588_000;
        
        /// <summary>Minimum team salary floor (90% of cap)</summary>
        public const long SALARY_FLOOR = 126_529_200;
        
        /// <summary>Luxury tax threshold - penalties begin here</summary>
        public const long LUXURY_TAX_LINE = 170_814_000;
        
        /// <summary>First tax apron - significant restrictions</summary>
        public const long FIRST_APRON = 178_132_000;
        
        /// <summary>Second tax apron - severe restrictions</summary>
        public const long SECOND_APRON = 188_931_000;

        // ==================== SIGNING EXCEPTIONS (2024-25) ====================
        
        /// <summary>Non-Taxpayer Mid-Level Exception - teams under first apron</summary>
        public const long NON_TAXPAYER_MLE = 12_822_000;
        public const int NON_TAXPAYER_MLE_MAX_YEARS = 4;
        
        /// <summary>Taxpayer Mid-Level Exception - teams between aprons</summary>
        public const long TAXPAYER_MLE = 5_168_000;
        public const int TAXPAYER_MLE_MAX_YEARS = 2;
        
        /// <summary>Room Mid-Level Exception - teams under cap</summary>
        public const long ROOM_MLE = 7_983_000;
        public const int ROOM_MLE_MAX_YEARS = 3;
        
        /// <summary>Bi-Annual Exception - available every other year, under first apron</summary>
        public const long BI_ANNUAL_EXCEPTION = 4_668_000;
        public const int BI_ANNUAL_MAX_YEARS = 2;

        // ==================== CONTRACT RULES ====================
        
        /// <summary>Max contract length for Bird rights re-signing</summary>
        public const int MAX_CONTRACT_YEARS_BIRD = 5;
        
        /// <summary>Max contract length for signing with other team</summary>
        public const int MAX_CONTRACT_YEARS_OTHER = 4;
        
        /// <summary>Annual raise percentage for re-signing (Bird rights)</summary>
        public const float ANNUAL_RAISE_BIRD = 0.08f;
        
        /// <summary>Annual raise percentage for signing with new team</summary>
        public const float ANNUAL_RAISE_OTHER = 0.05f;

        // ==================== MAX SALARY BY EXPERIENCE ====================
        
        /// <summary>
        /// Gets the maximum salary a player can earn based on years in league.
        /// </summary>
        public static long GetMaxSalary(int yearsInLeague)
        {
            // 0-6 years: 25% of cap
            // 7-9 years: 30% of cap
            // 10+ years: 35% of cap
            float percentage = yearsInLeague switch
            {
                < 7 => 0.25f,
                < 10 => 0.30f,
                _ => 0.35f
            };
            
            return (long)(SALARY_CAP * percentage);
        }

        /// <summary>
        /// Gets the supermax salary (35% of cap, requires specific criteria).
        /// </summary>
        public static long GetSupermaxSalary() => (long)(SALARY_CAP * 0.35f);

        // ==================== MINIMUM SALARIES BY EXPERIENCE ====================
        
        /// <summary>
        /// Gets the minimum salary for a player based on years of experience.
        /// </summary>
        public static long GetMinimumSalary(int yearsOfExperience)
        {
            // 2024-25 minimum salaries (approximate)
            return yearsOfExperience switch
            {
                0 => 1_119_563,
                1 => 1_902_057,
                2 => 2_165_948,
                3 => 2_239_742,
                4 => 2_313_535,
                5 => 2_469_123,
                6 => 2_624_710,
                7 => 2_780_296,
                8 => 2_935_884,
                9 => 3_091_470,
                _ => 3_246_000  // 10+ years
            };
        }

        // ==================== TEAM STATUS ====================
        
        /// <summary>
        /// Determines a team's salary cap status based on their payroll.
        /// </summary>
        public static TeamCapStatus GetCapStatus(long teamPayroll)
        {
            if (teamPayroll >= SECOND_APRON)
                return TeamCapStatus.AboveSecondApron;
            if (teamPayroll >= FIRST_APRON)
                return TeamCapStatus.AboveFirstApron;
            if (teamPayroll >= LUXURY_TAX_LINE)
                return TeamCapStatus.InLuxuryTax;
            if (teamPayroll >= SALARY_CAP)
                return TeamCapStatus.OverCap;
            return TeamCapStatus.UnderCap;
        }

        // ==================== TRADE SALARY MATCHING ====================
        
        /// <summary>
        /// Calculates the maximum salary a team can receive in a trade
        /// based on the salary they're sending out.
        /// </summary>
        public static long GetMaxTradeIncoming(long outgoingSalary, TeamCapStatus status)
        {
            // Teams above either apron: can only receive 100% of outgoing
            if (status >= TeamCapStatus.AboveFirstApron)
            {
                return outgoingSalary;
            }
            
            // Teams below both aprons have more flexibility
            if (outgoingSalary <= 7_500_000)
            {
                // Up to $7.5M: can receive 200% + $250K
                return (long)(outgoingSalary * 2.0f) + 250_000;
            }
            else if (outgoingSalary <= 29_000_000)
            {
                // $7.5M - $29M: can receive outgoing + $7.5M
                return outgoingSalary + 7_500_000;
            }
            else
            {
                // Over $29M: can receive 125% + $250K
                return (long)(outgoingSalary * 1.25f) + 250_000;
            }
        }

        // ==================== LUXURY TAX CALCULATION ====================
        
        /// <summary>
        /// Calculates the luxury tax owed based on team payroll.
        /// </summary>
        public static long CalculateLuxuryTax(long teamPayroll, bool isRepeater = false)
        {
            if (teamPayroll <= LUXURY_TAX_LINE)
                return 0;
            
            long amountOver = teamPayroll - LUXURY_TAX_LINE;
            long tax = 0;
            
            // Progressive tax brackets
            long[] brackets = { 5_000_000, 5_000_000, 5_000_000, 5_000_000, long.MaxValue };
            float[] rates = { 1.50f, 1.75f, 2.50f, 3.25f, 3.75f };
            
            for (int i = 0; i < brackets.Length && amountOver > 0; i++)
            {
                long taxableInBracket = Math.Min(amountOver, brackets[i]);
                tax += (long)(taxableInBracket * rates[i]);
                amountOver -= taxableInBracket;
            }
            
            // Repeater penalty: 1.5x if in tax 3 of last 4 years
            if (isRepeater)
            {
                tax = (long)(tax * 1.5f);
            }
            
            return tax;
        }

        // ==================== BIRD RIGHTS ====================
        
        /// <summary>
        /// Determines what Bird rights a player has with their current team.
        /// </summary>
        public static BirdRightsType GetBirdRights(int consecutiveSeasons)
        {
            if (consecutiveSeasons >= 3)
                return BirdRightsType.Full;
            if (consecutiveSeasons >= 2)
                return BirdRightsType.Early;
            if (consecutiveSeasons >= 1)
                return BirdRightsType.NonBird;
            return BirdRightsType.None;
        }

        // ==================== SUPERMAX ELIGIBILITY (2023 CBA) ====================
        /// <summary>
        /// Checks if a player meets the criteria for a Supermax extension.
        /// </summary>
        public static bool IsSuperMaxEligible(Player p)
        {
            if (p == null) return false;
            if (p.YearsPro < 7 || p.YearsPro > 9) return false;
            
            // Check for awards (logic pending integration with AwardHistory)
            // For now, return false or check basic counts if available
            return false;
        }

        // ==================== ROOKIE EXTENSIONS (2023 CBA) ====================
        
        /// <summary>Max years for rookie scale extension (increased to 5 in 2023 CBA)</summary>
        public const int ROOKIE_EXTENSION_MAX_YEARS = 5;
        
        /// <summary>
        /// Gets max salary for rookie extension based on criteria.
        /// </summary>
        public static long GetRookieExtensionMaxSalary(bool meetsHigherCriteria)
        {
            // If meets higher criteria (All-NBA, MVP, DPOY, or starter on conf finals team)
            // Can get 30% of cap, otherwise 25%
            return meetsHigherCriteria 
                ? (long)(SALARY_CAP * 0.30f) 
                : (long)(SALARY_CAP * 0.25f);
        }

        // ==================== TRADE DEADLINE & RESTRICTIONS ====================
        
        /// <summary>Trade deadline (2nd Thursday of February, simplified to Feb 8)</summary>
        public static DateTime GetTradeDeadline(int year) => new DateTime(year, 2, 8);
        
        public static bool IsPastTradeDeadline(DateTime currentDate)
        {
            var deadline = GetTradeDeadline(currentDate.Year);
            // If current month is later than deadline month (Feb), and we are in same season... 
            // Usually sim runs Oct -> June. 
            // If date is Oct-Dec, deadline is next year Feb.
            // If date is Jan-Jun, deadline is this year Feb.
            
            int seasonEndYear = currentDate.Month > 7 ? currentDate.Year + 1 : currentDate.Year;
            deadline = GetTradeDeadline(seasonEndYear);
            
            return currentDate > deadline;
        }
        /// <summary>RFA offer sheet matching period (reduced to 24 hours in 2023 CBA)</summary>
        public const int RFA_MATCHING_HOURS = 24;
        
        /// <summary>Qualifying offer multiplier (110% for first-round, 110% increase in 2023)</summary>
        public const float QUALIFYING_OFFER_MULTIPLIER = 1.10f;

        // ==================== TWO-WAY CONTRACTS (2023 CBA) ====================
        
        /// <summary>Max two-way contracts per team (increased to 3 in 2023 CBA)</summary>
        public const int MAX_TWO_WAY_CONTRACTS = 3;
        
        /// <summary>Max NBA games for two-way player</summary>
        public const int TWO_WAY_NBA_GAME_LIMIT = 50;
        
        /// <summary>Max years of experience for two-way eligibility</summary>
        public const int TWO_WAY_MAX_EXPERIENCE = 4;
        
        /// <summary>Two-way salary (half of rookie minimum)</summary>
        public static long GetTwoWaySalary() => GetMinimumSalary(0) / 2;

        // ==================== EXHIBIT 10 CONTRACTS ====================
        
        /// <summary>Exhibit 10 G-League bonus (increased to $75K in 2023 CBA)</summary>
        public const long EXHIBIT_10_BONUS = 75_000;

        // ==================== 10-DAY CONTRACTS ====================
        
        /// <summary>Earliest date to sign 10-day contracts (January 5)</summary>
        public const int TEN_DAY_START_MONTH = 1;
        public const int TEN_DAY_START_DAY = 5;
        
        /// <summary>Max 10-day contracts per player with same team per season</summary>
        public const int MAX_TEN_DAY_PER_PLAYER = 2;
        
        /// <summary>
        /// Calculates prorated 10-day salary based on experience.
        /// </summary>
        public static long GetTenDaySalary(int yearsExperience)
        {
            long fullSeason = GetMinimumSalary(yearsExperience);
            return fullSeason * 10 / 170; // ~170 days in season
        }

        // ==================== OVER-38 RULE ====================
        
        /// <summary>
        /// Checks if contract is subject to Over-38 rule.
        /// Contracts extending past age 38 may have deferred cap hits.
        /// </summary>
        public static bool IsOver38Contract(DateTime playerBirthday, DateTime contractEndDate)
        {
            var age38 = playerBirthday.AddYears(38);
            return contractEndDate > age38;
        }

        // ==================== SALARY CAP GROWTH ====================
        
        /// <summary>Max annual salary cap growth (10% cap in 2023 CBA)</summary>
        public const float MAX_CAP_GROWTH_PERCENT = 0.10f;
    }

    /// <summary>
    /// Team's position relative to salary cap thresholds.
    /// </summary>
    public enum TeamCapStatus
    {
        UnderCap,           // Below salary cap - can sign with cap space
        OverCap,            // Above cap but below tax - use exceptions
        InLuxuryTax,        // Above tax line but below aprons
        AboveFirstApron,    // Above first apron - trade restrictions
        AboveSecondApron    // Above second apron - severe restrictions
    }

    /// <summary>
    /// Types of Bird rights for re-signing players.
    /// </summary>
    public enum BirdRightsType
    {
        None,       // Cannot re-sign over cap
        NonBird,    // 1 year - can offer 120% of minimum
        Early,      // 2 years - can offer 175% or 105% of avg
        Full        // 3+ years - can offer max salary
    }

    /// <summary>
    /// Criteria for supermax (Designated Veteran Extension) eligibility.
    /// </summary>
    [Serializable]
    public class SupermaxCriteria
    {
        public int YearsInLeague;
        public bool IsWithOriginalTeam;
        
        // All-NBA criteria
        public bool AllNBALastSeason;
        public int AllNBACount; // in last 3 seasons
        
        // MVP criteria
        public bool MVPLastThreeSeasons;
        
        // DPOY criteria
        public bool DPOYLastSeason;
        public int DPOYCount; // in last 3 seasons
        
        /// <summary>
        /// Creates criteria from season statistics.
        /// </summary>
        public static SupermaxCriteria Create(
            int yearsInLeague,
            bool isWithOriginalTeam,
            bool allNBA1, bool allNBA2, bool allNBA3,
            bool mvp1, bool mvp2, bool mvp3,
            bool dpoy1, bool dpoy2, bool dpoy3)
        {
            return new SupermaxCriteria
            {
                YearsInLeague = yearsInLeague,
                IsWithOriginalTeam = isWithOriginalTeam,
                AllNBALastSeason = allNBA1,
                AllNBACount = (allNBA1 ? 1 : 0) + (allNBA2 ? 1 : 0) + (allNBA3 ? 1 : 0),
                MVPLastThreeSeasons = mvp1 || mvp2 || mvp3,
                DPOYLastSeason = dpoy1,
                DPOYCount = (dpoy1 ? 1 : 0) + (dpoy2 ? 1 : 0) + (dpoy3 ? 1 : 0)
            };
        }
    }
}
