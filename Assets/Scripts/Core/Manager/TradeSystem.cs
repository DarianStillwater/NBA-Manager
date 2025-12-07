using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages trade execution and AI trade evaluation.
    /// </summary>
    public class TradeSystem
    {
        private SalaryCapManager _capManager;
        private TradeValidator _validator;
        private PlayerDatabase _playerDatabase;
        
        // Trade history for AI learning
        private List<TradeRecord> _tradeHistory = new List<TradeRecord>();

        public TradeSystem(SalaryCapManager capManager, PlayerDatabase playerDatabase)
        {
            _capManager = capManager;
            _playerDatabase = playerDatabase;
            _validator = new TradeValidator(capManager);
        }

        // ==================== TRADE PROPOSAL ====================

        /// <summary>
        /// Creates a new trade proposal builder.
        /// </summary>
        public TradeProposalBuilder CreateProposal()
        {
            return new TradeProposalBuilder(_capManager);
        }

        /// <summary>
        /// Validates and potentially executes a trade proposal.
        /// </summary>
        public TradeResult ProposeTrade(TradeProposal proposal, bool executeIfValid = false)
        {
            var result = new TradeResult();
            
            // 1. Validate the trade
            var validation = _validator.ValidateTrade(proposal);
            result.ValidationResult = validation;
            
            if (!validation.IsValid)
            {
                result.Status = TradeStatus.Invalid;
                return result;
            }
            
            // 2. Check if player consent is needed
            if (validation.RequiresPlayerConsent)
            {
                result.Status = TradeStatus.AwaitingPlayerConsent;
                result.PlayersNeedingConsent = validation.PlayersRequiringConsent;
                return result;
            }
            
            // 3. AI evaluation for non-user teams
            var aiTeams = GetAIControlledTeams(proposal);
            foreach (var teamId in aiTeams)
            {
                if (!EvaluateTradeForAI(proposal, teamId))
                {
                    result.Status = TradeStatus.Rejected;
                    result.RejectedByTeam = teamId;
                    result.RejectionReason = GenerateRejectionReason(proposal, teamId);
                    return result;
                }
            }
            
            // 4. Execute if approved
            if (executeIfValid)
            {
                ExecuteTrade(proposal);
                result.Status = TradeStatus.Completed;
            }
            else
            {
                result.Status = TradeStatus.Approved;
            }
            
            return result;
        }

        // ==================== AI TRADE EVALUATION ====================

        /// <summary>
        /// AI evaluates whether to accept a trade.
        /// </summary>
        private bool EvaluateTradeForAI(TradeProposal proposal, string teamId)
        {
            float tradeBalance = _validator.EstimateTradeBalance(proposal, teamId);
            
            // AI accepts if trade is at least neutral or slightly negative (desperation)
            float acceptanceThreshold = GetAIAcceptanceThreshold(teamId);
            
            return tradeBalance >= acceptanceThreshold;
        }

        /// <summary>
        /// Gets the minimum trade value an AI team will accept.
        /// Varies based on team situation.
        /// </summary>
        private float GetAIAcceptanceThreshold(string teamId)
        {
            var status = _capManager.GetCapStatus(teamId);
            
            // Teams in tax trouble are more desperate to dump salary
            if (status >= TeamCapStatus.AboveSecondApron)
                return -5f; // Will accept losing value to clear cap
            
            if (status >= TeamCapStatus.InLuxuryTax)
                return -2f;
            
            // Normal teams want at least even value
            return 0f;
        }

        /// <summary>
        /// Generates a reason for trade rejection.
        /// </summary>
        private string GenerateRejectionReason(TradeProposal proposal, string teamId)
        {
            float balance = _validator.EstimateTradeBalance(proposal, teamId);
            
            if (balance < -5f)
                return "We'd be giving up too much in this deal.";
            if (balance < -2f)
                return "The value isn't quite there for us.";
            if (balance < 0f)
                return "We're not convinced this makes us better.";
            
            return "We've decided to pass on this trade.";
        }

        // ==================== TRADE EXECUTION ====================

        /// <summary>
        /// Executes an approved trade.
        /// </summary>
        public void ExecuteTrade(TradeProposal proposal)
        {
            // Process each asset
            foreach (var asset in proposal.AllAssets)
            {
                switch (asset.Type)
                {
                    case TradeAssetType.Player:
                        TransferPlayer(asset);
                        break;
                    case TradeAssetType.DraftPick:
                        TransferDraftPick(asset);
                        break;
                    case TradeAssetType.Cash:
                        // Cash considerations are just recorded
                        break;
                }
            }
            
            // Create TPEs for salary imbalances
            foreach (var teamId in proposal.GetInvolvedTeams())
            {
                long outgoing = proposal.GetOutgoingSalary(teamId);
                long incoming = proposal.GetIncomingSalary(teamId);
                
                if (outgoing > incoming)
                {
                    // Team sent out more salary - create TPE
                    var outgoingPlayers = proposal.GetOutgoingPlayers(teamId);
                    if (outgoingPlayers.Count > 0)
                    {
                        _capManager.CreateTPE(teamId, outgoing, incoming, outgoingPlayers[0].PlayerId);
                    }
                }
            }
            
            // Record trade in history
            _tradeHistory.Add(new TradeRecord
            {
                Proposal = proposal,
                ExecutedDate = DateTime.Now
            });
            
            Debug.Log($"Trade executed successfully involving {proposal.GetInvolvedTeams().Count} teams");
        }

        private void TransferPlayer(TradeAsset asset)
        {
            var contract = _capManager.GetContract(asset.PlayerId);
            if (contract != null)
            {
                string oldTeam = contract.TeamId;
                contract.TeamId = asset.ReceivingTeamId;
                contract.ConsecutiveSeasonsWithTeam = 0; // Reset Bird rights
                
                Debug.Log($"Transferred {asset.PlayerId} from {oldTeam} to {asset.ReceivingTeamId}");
            }
            
            // Update player's team in database
            var player = _playerDatabase?.GetPlayer(asset.PlayerId);
            if (player != null)
            {
                player.TeamId = asset.ReceivingTeamId;
            }
        }

        private void TransferDraftPick(TradeAsset asset)
        {
            // In a full implementation, this would update draft pick ownership records
            Debug.Log($"Transferred {asset.Year} {(asset.IsFirstRound ? "1st" : "2nd")} round pick " +
                     $"from {asset.SendingTeamId} to {asset.ReceivingTeamId}");
        }

        // ==================== UTILITY ====================

        private HashSet<string> GetAIControlledTeams(TradeProposal proposal)
        {
            // In a full implementation, check which teams are AI vs user
            // For now, return all teams except the first one (assume user controls first)
            var teams = proposal.GetInvolvedTeams().ToList();
            var aiTeams = new HashSet<string>();
            
            for (int i = 1; i < teams.Count; i++)
            {
                aiTeams.Add(teams[i]);
            }
            
            return aiTeams;
        }

        /// <summary>
        /// Gets recent trade history.
        /// </summary>
        public List<TradeRecord> GetTradeHistory(int maxRecords = 50)
        {
            return _tradeHistory
                .OrderByDescending(t => t.ExecutedDate)
                .Take(maxRecords)
                .ToList();
        }
    }

    // ==================== TRADE PROPOSAL BUILDER ====================

    /// <summary>
    /// Fluent builder for creating trade proposals.
    /// </summary>
    public class TradeProposalBuilder
    {
        private TradeProposal _proposal;
        private SalaryCapManager _capManager;

        public TradeProposalBuilder(SalaryCapManager capManager)
        {
            _proposal = new TradeProposal { ProposedDate = DateTime.Now };
            _capManager = capManager;
        }

        public TradeProposalBuilder AddPlayer(string playerId, string fromTeam, string toTeam)
        {
            var contract = _capManager.GetContract(playerId);
            
            _proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.Player,
                PlayerId = playerId,
                Salary = contract?.CurrentYearSalary ?? 0,
                SendingTeamId = fromTeam,
                ReceivingTeamId = toTeam
            });
            
            return this;
        }

        public TradeProposalBuilder AddDraftPick(int year, bool isFirstRound, string fromTeam, string toTeam, string originalTeam = null)
        {
            var pick = isFirstRound 
                ? DraftPick.CreateFirstRound(year, originalTeam ?? fromTeam, fromTeam)
                : DraftPick.CreateSecondRound(year, originalTeam ?? fromTeam, fromTeam);
            
            _proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.DraftPick,
                Year = year,
                IsFirstRound = isFirstRound,
                OriginalTeamId = originalTeam ?? fromTeam,
                SendingTeamId = fromTeam,
                ReceivingTeamId = toTeam,
                DraftPickDetails = pick
            });
            
            return this;
        }

        /// <summary>
        /// Adds a protected draft pick to the trade.
        /// Example: Top 10 protected 2025, Top 5 protected 2026, then unprotected 2027.
        /// </summary>
        public TradeProposalBuilder AddProtectedDraftPick(
            int startYear, 
            string fromTeam, 
            string toTeam, 
            string originalTeam = null,
            params DraftProtection[] protections)
        {
            var pick = DraftPick.CreateProtectedFirstRound(
                startYear, 
                originalTeam ?? fromTeam, 
                fromTeam, 
                protections);
            
            _proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.DraftPick,
                Year = startYear,
                IsFirstRound = true,
                OriginalTeamId = originalTeam ?? fromTeam,
                SendingTeamId = fromTeam,
                ReceivingTeamId = toTeam,
                DraftPickDetails = pick
            });
            
            return this;
        }

        /// <summary>
        /// Adds a pick swap right to the trade.
        /// </summary>
        public TradeProposalBuilder AddPickSwap(int year, string originalTeam, string beneficiaryTeam)
        {
            var swap = DraftPick.CreateSwapRight(year, originalTeam, beneficiaryTeam);
            
            _proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.DraftPick,
                Year = year,
                IsFirstRound = true,
                OriginalTeamId = originalTeam,
                SendingTeamId = originalTeam,
                ReceivingTeamId = beneficiaryTeam,
                DraftPickDetails = swap
            });
            
            return this;
        }

        public TradeProposalBuilder AddCash(long amount, string fromTeam, string toTeam)
        {
            _proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.Cash,
                CashAmount = amount,
                SendingTeamId = fromTeam,
                ReceivingTeamId = toTeam
            });
            
            return this;
        }

        public TradeProposalBuilder AsSignAndTrade()
        {
            _proposal.IsSignAndTrade = true;
            return this;
        }

        public TradeProposal Build() => _proposal;
    }

    // ==================== RESULT TYPES ====================

    public class TradeResult
    {
        public TradeStatus Status;
        public TradeValidationResult ValidationResult;
        public string RejectedByTeam;
        public string RejectionReason;
        public List<string> PlayersNeedingConsent = new List<string>();
    }

    public enum TradeStatus
    {
        Invalid,
        AwaitingPlayerConsent,
        Rejected,
        Approved,
        Completed
    }

    public class TradeRecord
    {
        public TradeProposal Proposal;
        public DateTime ExecutedDate;
    }
}
