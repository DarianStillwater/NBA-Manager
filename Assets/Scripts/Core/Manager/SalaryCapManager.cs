using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages team salary cap, payroll calculations, and financial constraints.
    /// </summary>
    public class SalaryCapManager
    {
        private Dictionary<string, Contract> _contracts;
        private Dictionary<string, List<TradedPlayerException>> _teamTPEs;
        private Dictionary<string, int> _teamTaxHistory; // Years in tax in last 4

        public SalaryCapManager()
        {
            _contracts = new Dictionary<string, Contract>();
            _teamTPEs = new Dictionary<string, List<TradedPlayerException>>();
            _teamTaxHistory = new Dictionary<string, int>();
        }

        // ==================== CONTRACT MANAGEMENT ====================

        /// <summary>
        /// Registers a contract in the system.
        /// </summary>
        public void RegisterContract(Contract contract)
        {
            _contracts[contract.PlayerId] = contract;
        }

        /// <summary>
        /// Gets a player's contract.
        /// </summary>
        public Contract GetContract(string playerId)
        {
            return _contracts.TryGetValue(playerId, out var contract) ? contract : null;
        }

        /// <summary>
        /// Gets all contracts for a team.
        /// </summary>
        public List<Contract> GetTeamContracts(string teamId)
        {
            return _contracts.Values
                .Where(c => c.TeamId == teamId)
                .OrderByDescending(c => c.CurrentYearSalary)
                .ToList();
        }

        // ==================== PAYROLL CALCULATIONS ====================

        /// <summary>
        /// Calculates total team payroll (all active contracts).
        /// </summary>
        public long GetTeamPayroll(string teamId)
        {
            return _contracts.Values
                .Where(c => c.TeamId == teamId)
                .Sum(c => c.CurrentYearSalary);
        }

        /// <summary>
        /// Gets cap space (can be negative if over cap).
        /// </summary>
        public long GetCapSpace(string teamId)
        {
            return LeagueCBA.SALARY_CAP - GetTeamPayroll(teamId);
        }

        /// <summary>
        /// Gets room under the luxury tax line.
        /// </summary>
        public long GetTaxRoom(string teamId)
        {
            return LeagueCBA.LUXURY_TAX_LINE - GetTeamPayroll(teamId);
        }

        /// <summary>
        /// Gets team's current cap status.
        /// </summary>
        public TeamCapStatus GetCapStatus(string teamId)
        {
            return LeagueCBA.GetCapStatus(GetTeamPayroll(teamId));
        }

        // ==================== LUXURY TAX ====================

        /// <summary>
        /// Calculates luxury tax owed by a team.
        /// </summary>
        public long CalculateLuxuryTax(string teamId)
        {
            long payroll = GetTeamPayroll(teamId);
            bool isRepeater = IsRepeaterTaxTeam(teamId);
            return LeagueCBA.CalculateLuxuryTax(payroll, isRepeater);
        }

        /// <summary>
        /// Checks if team qualifies for repeater tax (in tax 3 of last 4 years).
        /// </summary>
        public bool IsRepeaterTaxTeam(string teamId)
        {
            if (!_teamTaxHistory.TryGetValue(teamId, out int yearsInTax))
                return false;
            return yearsInTax >= 3;
        }

        /// <summary>
        /// Updates tax history at end of season.
        /// </summary>
        public void RecordSeasonTaxStatus(string teamId, bool wasInTax)
        {
            if (!_teamTaxHistory.ContainsKey(teamId))
                _teamTaxHistory[teamId] = 0;
            
            // Simplified: just track count, real implementation would track last 4 seasons
            if (wasInTax)
                _teamTaxHistory[teamId] = Math.Min(4, _teamTaxHistory[teamId] + 1);
            else
                _teamTaxHistory[teamId] = Math.Max(0, _teamTaxHistory[teamId] - 1);
        }

        // ==================== SIGNING AVAILABILITY ====================

        /// <summary>
        /// Gets the available Mid-Level Exception for a team.
        /// </summary>
        public (long amount, int maxYears, string type) GetAvailableMLE(string teamId)
        {
            var status = GetCapStatus(teamId);
            
            return status switch
            {
                TeamCapStatus.UnderCap => (LeagueCBA.ROOM_MLE, LeagueCBA.ROOM_MLE_MAX_YEARS, "Room MLE"),
                TeamCapStatus.OverCap or TeamCapStatus.InLuxuryTax => 
                    (LeagueCBA.NON_TAXPAYER_MLE, LeagueCBA.NON_TAXPAYER_MLE_MAX_YEARS, "Non-Taxpayer MLE"),
                TeamCapStatus.AboveFirstApron => 
                    (LeagueCBA.TAXPAYER_MLE, LeagueCBA.TAXPAYER_MLE_MAX_YEARS, "Taxpayer MLE"),
                TeamCapStatus.AboveSecondApron => (0, 0, "None - Above 2nd Apron"),
                _ => (0, 0, "Unknown")
            };
        }

        /// <summary>
        /// Checks if team can use Bi-Annual Exception.
        /// </summary>
        public bool CanUseBiAnnualException(string teamId, int currentSeason)
        {
            var status = GetCapStatus(teamId);
            
            // Cannot use if above first apron
            if (status >= TeamCapStatus.AboveFirstApron)
                return false;
            
            // Available every other year (simplified)
            return currentSeason % 2 == 0;
        }

        /// <summary>
        /// Gets maximum salary team can offer a free agent with cap space.
        /// </summary>
        public long GetMaxOfferWithCapSpace(string teamId, int playerYearsInLeague)
        {
            long capSpace = GetCapSpace(teamId);
            long maxSalary = LeagueCBA.GetMaxSalary(playerYearsInLeague);
            
            return Math.Min(Math.Max(0, capSpace), maxSalary);
        }

        // ==================== TRADE PLAYER EXCEPTIONS ====================

        /// <summary>
        /// Creates and registers a TPE from a trade.
        /// </summary>
        public void CreateTPE(string teamId, long outgoingSalary, long incomingSalary, string fromPlayerId)
        {
            var tpe = TradedPlayerException.Create(teamId, outgoingSalary, incomingSalary, fromPlayerId);
            if (tpe == null) return;
            
            if (!_teamTPEs.ContainsKey(teamId))
                _teamTPEs[teamId] = new List<TradedPlayerException>();
            
            _teamTPEs[teamId].Add(tpe);
        }

        /// <summary>
        /// Gets all valid TPEs for a team.
        /// </summary>
        public List<TradedPlayerException> GetValidTPEs(string teamId)
        {
            if (!_teamTPEs.TryGetValue(teamId, out var tpes))
                return new List<TradedPlayerException>();
            
            // Remove expired TPEs
            tpes.RemoveAll(t => t.IsExpired);
            
            // Teams above second apron cannot use prior-year TPEs
            if (GetCapStatus(teamId) >= TeamCapStatus.AboveSecondApron)
            {
                // Only return TPEs created this year (simplified check)
                return tpes.Where(t => t.ExpirationDate > DateTime.Now.AddMonths(6)).ToList();
            }
            
            return tpes;
        }

        /// <summary>
        /// Uses a TPE to acquire a player.
        /// </summary>
        public bool UseTPE(string teamId, TradedPlayerException tpe, long incomingSalary)
        {
            if (tpe.IsExpired || incomingSalary > tpe.Amount)
                return false;
            
            if (_teamTPEs.TryGetValue(teamId, out var tpes))
            {
                tpes.Remove(tpe);
            }
            
            return true;
        }

        // ==================== APRON RESTRICTIONS ====================

        /// <summary>
        /// Checks if team can aggregate salaries in a trade.
        /// Teams above second apron cannot aggregate.
        /// </summary>
        public bool CanAggregateSalaries(string teamId)
        {
            return GetCapStatus(teamId) < TeamCapStatus.AboveSecondApron;
        }

        /// <summary>
        /// Checks if team can send cash in a trade.
        /// Teams above second apron cannot.
        /// </summary>
        public bool CanSendCashInTrade(string teamId)
        {
            return GetCapStatus(teamId) < TeamCapStatus.AboveSecondApron;
        }

        /// <summary>
        /// Checks if team can acquire via sign-and-trade.
        /// Cannot if it would keep them above first apron.
        /// </summary>
        public bool CanAcquireViaSignAndTrade(string teamId, long incomingSalary)
        {
            long newPayroll = GetTeamPayroll(teamId) + incomingSalary;
            return newPayroll < LeagueCBA.FIRST_APRON;
        }

        /// <summary>
        /// Checks if team can sign a buyout player.
        /// Teams above second apron cannot.
        /// </summary>
        public bool CanSignBuyoutPlayer(string teamId)
        {
            return GetCapStatus(teamId) < TeamCapStatus.AboveSecondApron;
        }

        // ==================== REPORTING ====================

        /// <summary>
        /// Gets a detailed financial summary for a team.
        /// </summary>
        public TeamFinancialSummary GetFinancialSummary(string teamId)
        {
            long payroll = GetTeamPayroll(teamId);
            
            return new TeamFinancialSummary
            {
                TeamId = teamId,
                TotalPayroll = payroll,
                CapSpace = GetCapSpace(teamId),
                TaxRoomRemaining = GetTaxRoom(teamId),
                Status = GetCapStatus(teamId),
                LuxuryTaxOwed = CalculateLuxuryTax(teamId),
                IsRepeater = IsRepeaterTaxTeam(teamId),
                ActiveContracts = GetTeamContracts(teamId).Count,
                AvailableTPEs = GetValidTPEs(teamId)
            };
        }
    }

    /// <summary>
    /// Summary of a team's financial situation.
    /// </summary>
    public class TeamFinancialSummary
    {
        public string TeamId;
        public long TotalPayroll;
        public long CapSpace;
        public long TaxRoomRemaining;
        public TeamCapStatus Status;
        public long LuxuryTaxOwed;
        public bool IsRepeater;
        public int ActiveContracts;
        public List<TradedPlayerException> AvailableTPEs;

        public override string ToString()
        {
            return $"{TeamId}: ${TotalPayroll:N0} payroll, " +
                   $"${CapSpace:N0} cap space, " +
                   $"Status: {Status}, " +
                   $"Tax: ${LuxuryTaxOwed:N0}";
        }
    }
}
