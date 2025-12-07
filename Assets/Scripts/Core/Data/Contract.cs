using System;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Types of NBA contracts under the CBA.
    /// </summary>
    public enum ContractType
    {
        Standard,           // Regular NBA contract
        TwoWay,            // G-League/NBA split (max 50 NBA games)
        TenDay,            // 10-day contract
        Exhibit10,         // Training camp with G-League bonus
        RookieScale,       // First-round pick rookie contract
        Minimum,           // Veteran minimum
        MidLevel,          // Signed via MLE
        BiAnnual,          // Signed via BAE
        Supermax           // Designated veteran extension (35% max)
    }

    [Serializable]
    public class ContractIncentive
    {
        public AwardType Condition;
        public long Amount;
        public bool IsLikely; // Affects cap hold
    }

    /// <summary>
    /// Represents a player's contract with their team.
    /// </summary>
    [Serializable]
    public class Contract
    {
        // ==================== INCENTIVES ====================
        public System.Collections.Generic.List<ContractIncentive> Incentives = new System.Collections.Generic.List<ContractIncentive>();

        // ==================== BASIC CONTRACT INFO ====================
        
        public string PlayerId;
        public string TeamId;
        public ContractType Type = ContractType.Standard;
        
        /// <summary>Current season salary in dollars</summary>
        public long CurrentYearSalary;
        
        /// <summary>Years remaining on contract (including current)</summary>
        public int YearsRemaining;
        
        /// <summary>Total guaranteed money remaining</summary>
        public long GuaranteedMoney;
        
        /// <summary>Annual raise percentage baked into contract</summary>
        public float AnnualRaisePercent;
        
        /// <summary>Date contract was signed</summary>
        public DateTime SignedDate;

        // ==================== GUARANTEE INFO ====================
        
        /// <summary>Date by which salary becomes fully guaranteed</summary>
        public DateTime GuaranteeDate;
        
        /// <summary>Amount guaranteed if waived before GuaranteeDate</summary>
        public long PartialGuaranteeAmount;
        
        /// <summary>Is the contract fully guaranteed?</summary>
        public bool IsFullyGuaranteed => GuaranteedMoney >= GetTotalRemainingValue();

        // ==================== CONTRACT OPTIONS ====================
        
        /// <summary>Does player have option for final year?</summary>
        public bool HasPlayerOption;
        
        /// <summary>Does team have option for final year?</summary>
        public bool HasTeamOption;
        
        /// <summary>Year the option applies (0 = no option)</summary>
        public int OptionYear;
        
        /// <summary>No-trade clause prevents trades without consent</summary>
        public bool HasNoTradeClause;
        
        /// <summary>Trade kicker - bonus % if player is traded (max 15%)</summary>
        public float TradeKickerPercent;

        // ==================== BIRD RIGHTS ====================
        
        /// <summary>Consecutive seasons with current team</summary>
        public int ConsecutiveSeasonsWithTeam;
        
        /// <summary>Current Bird rights status (calculated)</summary>
        public BirdRightsType BirdRights => LeagueCBA.GetBirdRights(ConsecutiveSeasonsWithTeam);

        // ==================== TWO-WAY SPECIFIC ====================
        
        /// <summary>For two-way: NBA games played this season</summary>
        public int TwoWayNBAGamesPlayed;
        
        /// <summary>For two-way: max 50 NBA games allowed</summary>
        public const int TWO_WAY_MAX_GAMES = 50;

        // ==================== LEGACY BOOLS (for backwards compat) ====================
        
        public bool IsTwoWay => Type == ContractType.TwoWay;
        public bool IsTenDay => Type == ContractType.TenDay;
        public bool IsRookieScale => Type == ContractType.RookieScale;
        public bool IsSupermax => Type == ContractType.Supermax;

        // ==================== COMPUTED PROPERTIES ====================
        
        /// <summary>Is this the final year of the contract?</summary>
        public bool IsExpiring => YearsRemaining == 1;
        
        /// <summary>Is this a max contract?</summary>
        public bool IsMaxContract(int yearsInLeague) => 
            CurrentYearSalary >= LeagueCBA.GetMaxSalary(yearsInLeague) * 0.95f;
        
        /// <summary>Is this a minimum salary contract?</summary>
        public bool IsMinimumContract(int yearsOfExperience) => 
            CurrentYearSalary <= LeagueCBA.GetMinimumSalary(yearsOfExperience) * 1.05f;
        
        /// <summary>Can this player be traded? (3-month restriction after signing)</summary>
        public bool CanBeTradedAfterSigning(DateTime currentDate)
        {
            // Players signed as free agents cannot be traded for 3 months
            // or until December 15, whichever is later
            var threeMonths = SignedDate.AddMonths(3);
            var dec15 = new DateTime(SignedDate.Year, 12, 15);
            var earliestTradeDate = threeMonths > dec15 ? threeMonths : dec15;
            return currentDate >= earliestTradeDate;
        }

        // ==================== SALARY PROJECTION ====================
        
        /// <summary>
        /// Gets the salary for a future year of this contract.
        /// </summary>
        public long GetSalaryForYear(int yearsFromNow)
        {
            if (yearsFromNow >= YearsRemaining)
                return 0;
            
            return (long)(CurrentYearSalary * Math.Pow(1 + AnnualRaisePercent, yearsFromNow));
        }
        
        /// <summary>
        /// Gets the total remaining value of this contract.
        /// </summary>
        public long GetTotalRemainingValue()
        {
            long total = 0;
            for (int i = 0; i < YearsRemaining; i++)
            {
                total += GetSalaryForYear(i);
            }
            return total;
        }
        
        /// <summary>
        /// Gets dead cap if player is waived (stretch provision available).
        /// </summary>
        public long GetDeadCapIfWaived(DateTime waiveDate)
        {
            if (waiveDate < GuaranteeDate)
                return PartialGuaranteeAmount;
            return GuaranteedMoney;
        }

        // ==================== FACTORY METHODS ====================
        
        /// <summary>
        /// Creates a new standard contract.
        /// </summary>
        public static Contract Create(
            string playerId, 
            string teamId, 
            long salary, 
            int years, 
            bool isBirdRights = false)
        {
            return new Contract
            {
                PlayerId = playerId,
                TeamId = teamId,
                Type = ContractType.Standard,
                CurrentYearSalary = salary,
                YearsRemaining = years,
                GuaranteedMoney = salary * years,
                AnnualRaisePercent = isBirdRights ? LeagueCBA.ANNUAL_RAISE_BIRD : LeagueCBA.ANNUAL_RAISE_OTHER,
                ConsecutiveSeasonsWithTeam = 0,
                SignedDate = DateTime.Now,
                GuaranteeDate = DateTime.Now // Fully guaranteed
            };
        }
        
        /// <summary>
        /// Creates a two-way contract (G-League/NBA).
        /// </summary>
        public static Contract CreateTwoWay(string playerId, string teamId, int years = 1)
        {
            // Two-way salary is half of rookie minimum
            long salary = LeagueCBA.GetMinimumSalary(0) / 2;
            
            return new Contract
            {
                PlayerId = playerId,
                TeamId = teamId,
                Type = ContractType.TwoWay,
                CurrentYearSalary = salary,
                YearsRemaining = Math.Min(years, 2), // Max 2 years
                GuaranteedMoney = salary / 2, // Half can be guaranteed
                AnnualRaisePercent = 0.05f,
                SignedDate = DateTime.Now
            };
        }
        
        /// <summary>
        /// Creates a 10-day contract.
        /// </summary>
        public static Contract CreateTenDay(string playerId, string teamId, int yearsExperience)
        {
            // 10-day salary is prorated minimum
            long fullSeason = LeagueCBA.GetMinimumSalary(yearsExperience);
            long tenDaySalary = fullSeason * 10 / 170; // ~170 days in season
            
            return new Contract
            {
                PlayerId = playerId,
                TeamId = teamId,
                Type = ContractType.TenDay,
                CurrentYearSalary = tenDaySalary,
                YearsRemaining = 1, // Expires after 10 days
                GuaranteedMoney = tenDaySalary, // Fully guaranteed
                AnnualRaisePercent = 0f,
                SignedDate = DateTime.Now,
                GuaranteeDate = DateTime.Now
            };
        }
        
        /// <summary>
        /// Creates an Exhibit 10 contract (training camp).
        /// </summary>
        public static Contract CreateExhibit10(string playerId, string teamId)
        {
            long salary = LeagueCBA.GetMinimumSalary(0);
            
            return new Contract
            {
                PlayerId = playerId,
                TeamId = teamId,
                Type = ContractType.Exhibit10,
                CurrentYearSalary = salary,
                YearsRemaining = 1,
                GuaranteedMoney = 0, // Non-guaranteed
                PartialGuaranteeAmount = 75_000, // Exhibit 10 bonus if waived to G-League
                AnnualRaisePercent = 0f,
                SignedDate = DateTime.Now
            };
        }
        
        /// <summary>
        /// Creates a rookie scale contract based on draft position.
        /// </summary>
        public static Contract CreateRookieScale(string playerId, string teamId, int draftPosition)
        {
            long baseSalary = draftPosition switch
            {
                1 => 12_000_000,
                2 => 10_500_000,
                3 => 9_500_000,
                <= 5 => 8_000_000,
                <= 10 => 5_000_000,
                <= 20 => 3_000_000,
                <= 30 => 2_000_000,
                _ => 1_500_000  // Second round
            };
            
            return new Contract
            {
                PlayerId = playerId,
                TeamId = teamId,
                Type = ContractType.RookieScale,
                CurrentYearSalary = baseSalary,
                YearsRemaining = 4,
                GuaranteedMoney = baseSalary * 2, // First 2 years
                AnnualRaisePercent = 0.05f,
                HasTeamOption = true,
                OptionYear = 4,
                SignedDate = DateTime.Now
            };
        }
        
        /// <summary>
        /// Creates a minimum salary contract.
        /// </summary>
        public static Contract CreateMinimum(string playerId, string teamId, int yearsOfExperience, int years = 1)
        {
            long salary = LeagueCBA.GetMinimumSalary(yearsOfExperience);
            
            return new Contract
            {
                PlayerId = playerId,
                TeamId = teamId,
                Type = ContractType.Minimum,
                CurrentYearSalary = salary,
                YearsRemaining = Math.Min(years, 2),
                GuaranteedMoney = salary,
                AnnualRaisePercent = 0.05f,
                SignedDate = DateTime.Now,
                GuaranteeDate = DateTime.Now
            };
        }
        
        /// <summary>
        /// Creates a supermax (designated veteran) contract.
        /// Player must be 8-9 years in league + meet accolade requirements.
        /// </summary>
        public static Contract CreateSupermax(string playerId, string teamId, int yearsInLeague)
        {
            long salary = LeagueCBA.GetSupermaxSalary(); // 35% of cap
            
            return new Contract
            {
                PlayerId = playerId,
                TeamId = teamId,
                Type = ContractType.Supermax,
                CurrentYearSalary = salary,
                YearsRemaining = 5, // Supermax is always 5 years
                GuaranteedMoney = salary * 5,
                AnnualRaisePercent = LeagueCBA.ANNUAL_RAISE_BIRD, // 8%
                HasNoTradeClause = yearsInLeague >= 8, // Often included
                SignedDate = DateTime.Now,
                GuaranteeDate = DateTime.Now
            };
        }

        // ==================== UTILITIES ====================
        
        public override string ToString()
        {
            string optionStr = HasPlayerOption ? " (PO)" : HasTeamOption ? " (TO)" : "";
            string typeStr = Type != ContractType.Standard ? $" [{Type}]" : "";
            return $"${CurrentYearSalary:N0}/yr, {YearsRemaining}yr{optionStr}{typeStr}";
        }
    }

    /// <summary>
    /// Represents a Traded Player Exception created from a trade imbalance.
    /// </summary>
    [Serializable]
    public class TradedPlayerException
    {
        public string TeamId;
        public long Amount;
        public DateTime ExpirationDate;
        public string CreatedFromPlayerId;
        
        public bool IsExpired => DateTime.Now > ExpirationDate;
        
        /// <summary>
        /// Creates a TPE from the difference in trade salaries.
        /// Valid for 1 year from creation.
        /// </summary>
        public static TradedPlayerException Create(string teamId, long outgoingSalary, long incomingSalary, string fromPlayerId)
        {
            if (outgoingSalary <= incomingSalary)
                return null;
            
            return new TradedPlayerException
            {
                TeamId = teamId,
                Amount = outgoingSalary - incomingSalary,
                ExpirationDate = DateTime.Now.AddYears(1),
                CreatedFromPlayerId = fromPlayerId
            };
        }
    }
}
