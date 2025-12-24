using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Validates trades according to NBA CBA salary matching rules.
    /// </summary>
    public class TradeValidator
    {
        private SalaryCapManager _capManager;
        private DraftPickRegistry _draftPickRegistry;

        public TradeValidator(SalaryCapManager capManager, DraftPickRegistry draftPickRegistry = null)
        {
            _capManager = capManager;
            _draftPickRegistry = draftPickRegistry;
        }

        /// <summary>
        /// Set or update the draft pick registry reference.
        /// </summary>
        public void SetDraftPickRegistry(DraftPickRegistry registry)
        {
            _draftPickRegistry = registry;
        }

        // ==================== MAIN VALIDATION ====================

        /// <summary>
        /// Validates if a trade is legal according to CBA rules.
        /// Returns a validation result with any issues found.
        /// </summary>
        public TradeValidationResult ValidateTrade(TradeProposal proposal)
        {
            var result = new TradeValidationResult { IsValid = true };
            
            // 1. Check salary matching for each team
            foreach (var teamId in proposal.GetInvolvedTeams())
            {
                var salaryResult = ValidateSalaryMatching(proposal, teamId);
                if (!salaryResult.IsValid)
                {
                    result.IsValid = false;
                    result.Issues.AddRange(salaryResult.Issues);
                }
            }
            
            // 2. Check apron-specific restrictions
            foreach (var teamId in proposal.GetInvolvedTeams())
            {
                var apronResult = ValidateApronRestrictions(proposal, teamId);
                if (!apronResult.IsValid)
                {
                    result.IsValid = false;
                    result.Issues.AddRange(apronResult.Issues);
                }
            }
            
            // 3. Check roster limits
            foreach (var teamId in proposal.GetInvolvedTeams())
            {
                var rosterResult = ValidateRosterLimits(proposal, teamId);
                if (!rosterResult.IsValid)
                {
                    result.IsValid = false;
                    result.Issues.AddRange(rosterResult.Issues);
                }
            }
            
            // 4. Check no-trade clauses
            var ntcResult = ValidateNoTradeClauses(proposal);
            if (!ntcResult.IsValid)
            {
                result.Issues.AddRange(ntcResult.Issues);
                result.RequiresPlayerConsent = true;
            }
            
            // 5. Check Ted Stepien Rule (cannot trade consecutive first-round picks)
            foreach (var teamId in proposal.GetInvolvedTeams())
            {
                var stepienResult = ValidateStepienRule(proposal, teamId);
                if (!stepienResult.IsValid)
                {
                    result.IsValid = false;
                    result.Issues.AddRange(stepienResult.Issues);
                }
            }
            
            // 6. Check trade deadline
            var deadlineResult = ValidateTradeDeadline(proposal);
            if (!deadlineResult.IsValid)
            {
                result.IsValid = false;
                result.Issues.AddRange(deadlineResult.Issues);
            }
            
            // 7. Check recently-signed player restrictions
            var recentlySignedResult = ValidateRecentlySignedPlayers(proposal);
            if (!recentlySignedResult.IsValid)
            {
                result.IsValid = false;
                result.Issues.AddRange(recentlySignedResult.Issues);
            }
            
            return result;
        }
        
        // ==================== TRADE DEADLINE ====================
        
        /// <summary>
        /// Validates that trade is before the trade deadline.
        /// </summary>
        public TradeValidationResult ValidateTradeDeadline(TradeProposal proposal)
        {
            var result = new TradeValidationResult { IsValid = true };
            
            if (LeagueCBA.IsPastTradeDeadline(proposal.ProposedDate))
            {
                result.IsValid = false;
                result.Issues.Add($"Trade cannot be completed - past trade deadline ({LeagueCBA.GetTradeDeadline(proposal.ProposedDate.Year):MMM d})");
            }
            
            return result;
        }

        // ==================== RECENTLY SIGNED PLAYERS ====================
        
        /// <summary>
        /// Validates that recently-signed players can be traded.
        /// Players signed as free agents cannot be traded for 3 months or until Dec 15.
        /// </summary>
        public TradeValidationResult ValidateRecentlySignedPlayers(TradeProposal proposal)
        {
            var result = new TradeValidationResult { IsValid = true };
            
            foreach (var asset in proposal.AllAssets)
            {
                if (asset.Type != TradeAssetType.Player)
                    continue;
                
                var contract = _capManager.GetContract(asset.PlayerId);
                if (contract == null)
                    continue;
                
                if (!contract.CanBeTradedAfterSigning(proposal.ProposedDate))
                {
                    result.IsValid = false;
                    var earliestDate = contract.SignedDate.AddMonths(3);
                    result.Issues.Add($"{asset.PlayerId}: Cannot be traded until {earliestDate:MMM d} (3-month restriction)");
                }
            }
            
            return result;
        }

        // ==================== SALARY MATCHING ====================

        /// <summary>
        /// Validates salary matching for a specific team in the trade.
        /// </summary>
        public TradeValidationResult ValidateSalaryMatching(TradeProposal proposal, string teamId)
        {
            var result = new TradeValidationResult { IsValid = true };
            
            long outgoingSalary = proposal.GetOutgoingSalary(teamId);
            long incomingSalary = proposal.GetIncomingSalary(teamId);
            
            // Teams under the cap can receive up to their cap space
            long capSpace = _capManager.GetCapSpace(teamId);
            if (capSpace > 0)
            {
                // Can receive: cap space + salary matching on any outgoing
                long maxIncoming = capSpace + LeagueCBA.GetMaxTradeIncoming(outgoingSalary, TeamCapStatus.UnderCap);
                
                if (incomingSalary > maxIncoming)
                {
                    result.IsValid = false;
                    result.Issues.Add($"{teamId}: Incoming salary ${incomingSalary:N0} exceeds max ${maxIncoming:N0} (cap space + matching)");
                }
                return result;
            }
            
            // Teams over the cap - check based on status
            var status = _capManager.GetCapStatus(teamId);
            long maxAllowed = LeagueCBA.GetMaxTradeIncoming(outgoingSalary, status);
            
            if (incomingSalary > maxAllowed)
            {
                result.IsValid = false;
                
                string rule = status switch
                {
                    TeamCapStatus.AboveFirstApron or TeamCapStatus.AboveSecondApron => 
                        "100% of outgoing (above apron)",
                    _ when outgoingSalary <= 7_500_000 => "200% + $250K",
                    _ when outgoingSalary <= 29_000_000 => "outgoing + $7.5M",
                    _ => "125% + $250K"
                };
                
                result.Issues.Add($"{teamId}: Incoming ${incomingSalary:N0} exceeds max ${maxAllowed:N0} ({rule})");
            }
            
            return result;
        }

        // ==================== APRON RESTRICTIONS ====================

        /// <summary>
        /// Checks for apron-specific trade restrictions.
        /// </summary>
        public TradeValidationResult ValidateApronRestrictions(TradeProposal proposal, string teamId)
        {
            var result = new TradeValidationResult { IsValid = true };
            var status = _capManager.GetCapStatus(teamId);
            
            if (status < TeamCapStatus.AboveFirstApron)
                return result; // No restrictions
            
            // FIRST APRON RESTRICTIONS
            if (status >= TeamCapStatus.AboveFirstApron)
            {
                // Check if trade would keep team above first apron via sign-and-trade
                if (proposal.IsSignAndTrade)
                {
                    long newPayroll = _capManager.GetTeamPayroll(teamId) + proposal.GetNetSalaryChange(teamId);
                    if (newPayroll >= LeagueCBA.FIRST_APRON)
                    {
                        result.IsValid = false;
                        result.Issues.Add($"{teamId}: Cannot acquire via sign-and-trade if staying above first apron");
                    }
                }
            }
            
            // SECOND APRON RESTRICTIONS
            if (status >= TeamCapStatus.AboveSecondApron)
            {
                // Cannot aggregate salaries
                if (proposal.GetOutgoingPlayerCount(teamId) > 1)
                {
                    // Check if aggregating is required
                    var outgoingPlayers = proposal.GetOutgoingPlayers(teamId);
                    bool isAggregating = outgoingPlayers.Count > 1;
                    
                    if (isAggregating)
                    {
                        result.IsValid = false;
                        result.Issues.Add($"{teamId}: Cannot aggregate multiple player salaries (above 2nd apron)");
                    }
                }
                
                // Cannot send cash
                if (proposal.GetCashOutgoing(teamId) > 0)
                {
                    result.IsValid = false;
                    result.Issues.Add($"{teamId}: Cannot send cash in trade (above 2nd apron)");
                }
                
                // Cannot trade picks 7+ years out
                var farPicks = proposal.GetOutgoingPicks(teamId)
                    .Where(p => p.Year > DateTime.Now.Year + 6);
                if (farPicks.Any())
                {
                    result.IsValid = false;
                    result.Issues.Add($"{teamId}: Cannot trade picks more than 7 years out (above 2nd apron)");
                }
            }
            
            return result;
        }

        // ==================== ROSTER LIMITS ====================

        /// <summary>
        /// Validates roster size after trade.
        /// </summary>
        public TradeValidationResult ValidateRosterLimits(TradeProposal proposal, string teamId)
        {
            var result = new TradeValidationResult { IsValid = true };
            
            int currentRoster = _capManager.GetTeamContracts(teamId).Count;
            int outgoing = proposal.GetOutgoingPlayerCount(teamId);
            int incoming = proposal.GetIncomingPlayerCount(teamId);
            int newRoster = currentRoster - outgoing + incoming;
            
            // Maximum 15 standard + 2 two-way = 17
            if (newRoster > 15)
            {
                result.IsValid = false;
                result.Issues.Add($"{teamId}: Trade would result in {newRoster} players (max 15)");
            }
            
            // Minimum 12 during season (simplified)
            if (newRoster < 12)
            {
                result.IsValid = false;
                result.Issues.Add($"{teamId}: Trade would result in only {newRoster} players (min 12)");
            }
            
            return result;
        }

        // ==================== NO-TRADE CLAUSES ====================

        /// <summary>
        /// Checks if any players have no-trade clauses that would block the trade.
        /// </summary>
        public TradeValidationResult ValidateNoTradeClauses(TradeProposal proposal)
        {
            var result = new TradeValidationResult { IsValid = true };
            
            foreach (var asset in proposal.AllAssets)
            {
                if (asset.Type != TradeAssetType.Player)
                    continue;
                
                var contract = _capManager.GetContract(asset.PlayerId);
                if (contract != null && contract.HasNoTradeClause)
                {
                    result.Issues.Add($"{asset.PlayerId} has a no-trade clause - requires consent");
                    result.PlayersRequiringConsent.Add(asset.PlayerId);
                }
            }
            
            result.RequiresPlayerConsent = result.PlayersRequiringConsent.Count > 0;
            return result;
        }

        // ==================== TED STEPIEN RULE ====================
        
        // Track team's draft pick ownership (would be populated from a draft pick manager)
        private Dictionary<string, HashSet<int>> _teamFirstRoundPicks = new Dictionary<string, HashSet<int>>();
        
        /// <summary>
        /// Sets the team's current first-round pick ownership for Stepien Rule validation.
        /// Call this to initialize the validator with current team pick holdings.
        /// </summary>
        public void SetTeamPickOwnership(string teamId, IEnumerable<int> yearsOwnedFirstRound)
        {
            _teamFirstRoundPicks[teamId] = new HashSet<int>(yearsOwnedFirstRound);
        }

        /// <summary>
        /// Validates the Ted Stepien Rule: teams cannot trade first-round picks
        /// in consecutive years. Teams must always have at least one first-round
        /// pick every other year (7-year window).
        /// </summary>
        public TradeValidationResult ValidateStepienRule(TradeProposal proposal, string teamId)
        {
            var result = new TradeValidationResult { IsValid = true };

            // Get picks team is trading away in this proposal
            var outgoingFirstRounders = proposal.GetOutgoingPicks(teamId)
                .Where(p => p.IsFirstRound)
                .Select(p => p.DraftPickDetails ?? DraftPick.CreateFirstRound(p.Year, p.OriginalTeamId, teamId))
                .ToList();

            if (outgoingFirstRounders.Count == 0)
                return result; // Not trading any first-rounders

            // Use registry if available for accurate validation
            if (_draftPickRegistry != null)
            {
                bool valid = _draftPickRegistry.ValidateStepienRule(teamId, outgoingFirstRounders);
                if (!valid)
                {
                    result.IsValid = false;
                    result.Issues.Add($"{teamId}: STEPIEN RULE VIOLATION - Cannot trade these picks. " +
                        "Would leave team without first-round picks in consecutive years.");
                }
                return result;
            }

            // Fallback to manual calculation if registry not available
            var ownedPicks = GetTeamFirstRoundPicks(teamId);

            // Simulate pick ownership after this trade
            var picksAfterTrade = new HashSet<int>(ownedPicks);
            foreach (var pick in outgoingFirstRounders)
            {
                picksAfterTrade.Remove(pick.Year);
            }

            // Add any first-rounders being acquired
            var incomingFirstRounders = proposal.AllAssets
                .Where(a => a.ReceivingTeamId == teamId && a.Type == TradeAssetType.DraftPick && a.IsFirstRound)
                .Select(a => a.Year);

            foreach (var year in incomingFirstRounders)
            {
                picksAfterTrade.Add(year);
            }

            // Check for consecutive years without a first-round pick
            int currentYear = DateTime.Now.Year;
            int checkWindow = 7;

            for (int year = currentYear; year < currentYear + checkWindow - 1; year++)
            {
                bool hasThisYear = picksAfterTrade.Contains(year);
                bool hasNextYear = picksAfterTrade.Contains(year + 1);

                if (!hasThisYear && !hasNextYear)
                {
                    result.IsValid = false;
                    result.Issues.Add($"{teamId}: STEPIEN RULE VIOLATION - Cannot trade pick. " +
                        $"Would leave team without first-round picks in both {year} and {year + 1}.");
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets a team's owned first-round picks.
        /// Uses registry if available, otherwise falls back to cached/default data.
        /// </summary>
        private HashSet<int> GetTeamFirstRoundPicks(string teamId)
        {
            // Use registry if available
            if (_draftPickRegistry != null)
            {
                var picks = _draftPickRegistry.GetPicksOwnedBy(teamId)
                    .Where(p => p.Round == 1)
                    .Select(p => p.Year)
                    .ToHashSet();
                return picks;
            }

            // Check if we have tracked picks for this team (legacy support)
            if (_teamFirstRoundPicks.TryGetValue(teamId, out var cachedPicks))
            {
                return cachedPicks;
            }

            // Default: assume team owns their own pick for next 7 years
            int currentYear = DateTime.Now.Year;
            var defaultPicks = new HashSet<int>();
            for (int i = 0; i < 7; i++)
            {
                defaultPicks.Add(currentYear + i);
            }

            _teamFirstRoundPicks[teamId] = defaultPicks;
            return defaultPicks;
        }

        // ==================== TRADE VALUE ESTIMATE ====================

        /// <summary>
        /// Estimates the trade value balance (positive = good for receiving team).
        /// Used for AI trade evaluation.
        /// </summary>
        public float EstimateTradeBalance(TradeProposal proposal, string evaluatingTeamId)
        {
            float valueReceiving = 0f;
            float valueGiving = 0f;
            
            foreach (var asset in proposal.AllAssets)
            {
                float assetValue = EstimateAssetValue(asset);
                
                if (asset.ReceivingTeamId == evaluatingTeamId)
                    valueReceiving += assetValue;
                else if (asset.SendingTeamId == evaluatingTeamId)
                    valueGiving += assetValue;
            }
            
            return valueReceiving - valueGiving;
        }

        /// <summary>
        /// Estimates the value of a single trade asset.
        /// </summary>
        private float EstimateAssetValue(TradeAsset asset)
        {
            return asset.Type switch
            {
                TradeAssetType.Player => EstimatePlayerValue(asset),
                TradeAssetType.DraftPick => EstimateDraftPickValue(asset),
                TradeAssetType.Cash => asset.CashAmount / 1_000_000f, // $1M = 1 point
                _ => 0f
            };
        }

        private float EstimatePlayerValue(TradeAsset asset)
        {
            var contract = _capManager.GetContract(asset.PlayerId);
            if (contract == null) return 0f;
            
            // Base value on salary (proxy for skill)
            float salaryValue = contract.CurrentYearSalary / 5_000_000f;
            
            // Adjust for contract length (expiring = less value for acquiring team)
            float contractMultiplier = contract.YearsRemaining switch
            {
                1 => 0.7f,   // Expiring
                2 => 1.0f,
                3 => 0.95f,
                4 => 0.85f,
                _ => 0.75f   // Very long
            };
            
            return salaryValue * contractMultiplier;
        }

        private float EstimateDraftPickValue(TradeAsset asset)
        {
            // First round picks are valuable
            float baseValue = asset.IsFirstRound ? 15f : 3f;
            
            // Future picks are worth less (discount rate)
            int yearsAway = asset.Year - DateTime.Now.Year;
            float timeDiscount = 1f - (yearsAway * 0.1f);
            
            return baseValue * Math.Max(0.3f, timeDiscount);
        }
    }

    // ==================== VALIDATION RESULT ====================

    /// <summary>
    /// Result of trade validation.
    /// </summary>
    public class TradeValidationResult
    {
        public bool IsValid;
        public List<string> Issues = new List<string>();
        public bool RequiresPlayerConsent;
        public List<string> PlayersRequiringConsent = new List<string>();

        public override string ToString()
        {
            if (IsValid && !RequiresPlayerConsent)
                return "Trade is valid";
            
            return string.Join("\n", Issues);
        }
    }

    // ==================== TRADE PROPOSAL ====================

    /// <summary>
    /// Represents a proposed trade between teams.
    /// </summary>
    public class TradeProposal
    {
        public List<TradeAsset> AllAssets = new List<TradeAsset>();
        public bool IsSignAndTrade;
        public DateTime ProposedDate;

        public HashSet<string> GetInvolvedTeams()
        {
            var teams = new HashSet<string>();
            foreach (var asset in AllAssets)
            {
                teams.Add(asset.SendingTeamId);
                teams.Add(asset.ReceivingTeamId);
            }
            return teams;
        }

        public long GetOutgoingSalary(string teamId)
        {
            return AllAssets
                .Where(a => a.SendingTeamId == teamId && a.Type == TradeAssetType.Player)
                .Sum(a => a.Salary);
        }

        public long GetIncomingSalary(string teamId)
        {
            return AllAssets
                .Where(a => a.ReceivingTeamId == teamId && a.Type == TradeAssetType.Player)
                .Sum(a => a.Salary);
        }

        public long GetNetSalaryChange(string teamId)
        {
            return GetIncomingSalary(teamId) - GetOutgoingSalary(teamId);
        }

        public int GetOutgoingPlayerCount(string teamId)
        {
            return AllAssets.Count(a => a.SendingTeamId == teamId && a.Type == TradeAssetType.Player);
        }

        public int GetIncomingPlayerCount(string teamId)
        {
            return AllAssets.Count(a => a.ReceivingTeamId == teamId && a.Type == TradeAssetType.Player);
        }

        public List<TradeAsset> GetOutgoingPlayers(string teamId)
        {
            return AllAssets
                .Where(a => a.SendingTeamId == teamId && a.Type == TradeAssetType.Player)
                .ToList();
        }

        public long GetCashOutgoing(string teamId)
        {
            return AllAssets
                .Where(a => a.SendingTeamId == teamId && a.Type == TradeAssetType.Cash)
                .Sum(a => a.CashAmount);
        }

        public IEnumerable<TradeAsset> GetOutgoingPicks(string teamId)
        {
            return AllAssets
                .Where(a => a.SendingTeamId == teamId && a.Type == TradeAssetType.DraftPick);
        }
    }

    /// <summary>
    /// A single asset in a trade (player, pick, or cash).
    /// </summary>
    public class TradeAsset
    {
        public TradeAssetType Type;
        public string SendingTeamId;
        public string ReceivingTeamId;
        
        // Player-specific
        public string PlayerId;
        public long Salary;
        
        // Draft pick-specific (NEW: includes full DraftPick with protections)
        public int Year;
        public bool IsFirstRound;
        public string OriginalTeamId; // Whose pick it originally was
        public DraftPick DraftPickDetails; // Full pick with protections
        
        // Cash-specific
        public long CashAmount;
        
        /// <summary>
        /// Gets a description of this trade asset.
        /// </summary>
        public string GetDescription()
        {
            return Type switch
            {
                TradeAssetType.Player => PlayerId,
                TradeAssetType.DraftPick => DraftPickDetails?.GetDescription() 
                    ?? $"{Year} {(IsFirstRound ? "1st" : "2nd")} Round ({OriginalTeamId})",
                TradeAssetType.Cash => $"${CashAmount:N0} cash",
                _ => "Unknown"
            };
        }
    }

    public enum TradeAssetType
    {
        Player,
        DraftPick,
        Cash
    }
}
