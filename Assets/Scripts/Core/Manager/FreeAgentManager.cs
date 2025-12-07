using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages free agent signings, exceptions, and Bird rights.
    /// </summary>
    public class FreeAgentManager
    {
        private SalaryCapManager _capManager;
        private PlayerDatabase _playerDatabase;
        
        // Track exception usage per season
        private Dictionary<string, ExceptionUsage> _teamExceptions = new Dictionary<string, ExceptionUsage>();
        
        // Free agent market
        private List<FreeAgent> _freeAgents = new List<FreeAgent>();

        public FreeAgentManager(SalaryCapManager capManager, PlayerDatabase playerDatabase)
        {
            _capManager = capManager;
            _playerDatabase = playerDatabase;
        }

        // ==================== FREE AGENT MARKET ====================

        /// <summary>
        /// Gets all available free agents.
        /// </summary>
        public List<FreeAgent> GetFreeAgents(FreeAgentFilter filter = null)
        {
            if (filter == null)
                return _freeAgents.ToList();
            
            return _freeAgents
                .Where(fa => MatchesFilter(fa, filter))
                .OrderByDescending(fa => fa.EstimatedValue)
                .ToList();
        }

        /// <summary>
        /// Adds a player to the free agent market.
        /// </summary>
        public void AddFreeAgent(string playerId, FreeAgentType type, string previousTeamId = null, int consecutiveSeasons = 0)
        {
            var player = _playerDatabase?.GetPlayer(playerId);
            if (player == null) return;
            
            _freeAgents.Add(new FreeAgent
            {
                PlayerId = playerId,
                Type = type,
                PreviousTeamId = previousTeamId,
                BirdRights = LeagueCBA.GetBirdRights(consecutiveSeasons),
                EstimatedValue = EstimatePlayerValue(player)
            });
        }

        // ==================== SIGNING VALIDATION ====================

        /// <summary>
        /// Checks if a team can sign a free agent with a given offer.
        /// </summary>
        public SigningValidationResult CanSign(string teamId, string playerId, SigningOffer offer)
        {
            var result = new SigningValidationResult { IsValid = true };
            var freeAgent = _freeAgents.FirstOrDefault(fa => fa.PlayerId == playerId);
            
            if (freeAgent == null)
            {
                result.IsValid = false;
                result.Reason = "Player is not a free agent";
                return result;
            }
            
            var status = _capManager.GetCapStatus(teamId);
            
            // Check based on signing method
            switch (offer.Method)
            {
                case SigningMethod.CapSpace:
                    return ValidateCapSpaceSigning(teamId, offer);
                    
                case SigningMethod.BirdRights:
                    return ValidateBirdRightsSigning(teamId, freeAgent, offer);
                    
                case SigningMethod.MidLevelException:
                    return ValidateMLESigning(teamId, offer);
                    
                case SigningMethod.BiAnnualException:
                    return ValidateBiAnnualSigning(teamId, offer);
                    
                case SigningMethod.MinimumSalary:
                    return ValidateMinimumSigning(offer);
                    
                case SigningMethod.TwoWayContract:
                    return ValidateTwoWaySigning(teamId, offer);
                    
                case SigningMethod.TenDayContract:
                    return ValidateTenDaySigning(teamId, playerId, offer, DateTime.Now);
                    
                default:
                    result.IsValid = false;
                    result.Reason = "Unknown signing method";
                    return result;
            }
        }
        
        // ==================== TWO-WAY CONTRACTS ====================
        
        private SigningValidationResult ValidateTwoWaySigning(string teamId, SigningOffer offer)
        {
            var result = new SigningValidationResult { IsValid = true };
            var usage = GetExceptionUsage(teamId);
            
            // Max 3 two-way contracts per team
            if (usage.TwoWayCount >= LeagueCBA.MAX_TWO_WAY_CONTRACTS)
            {
                result.IsValid = false;
                result.Reason = $"Already have {LeagueCBA.MAX_TWO_WAY_CONTRACTS} two-way players";
                return result;
            }
            
            // Player must have < 4 years experience
            if (offer.PlayerYearsExperience >= LeagueCBA.TWO_WAY_MAX_EXPERIENCE)
            {
                result.IsValid = false;
                result.Reason = $"Too experienced for two-way ({LeagueCBA.TWO_WAY_MAX_EXPERIENCE}+ years)";
                return result;
            }
            
            // Max 2 years
            if (offer.Years > 2)
            {
                result.IsValid = false;
                result.Reason = "Two-way contracts max 2 years";
            }
            
            return result;
        }

        // ==================== 10-DAY CONTRACTS ====================
        
        private SigningValidationResult ValidateTenDaySigning(string teamId, string playerId, SigningOffer offer, DateTime currentDate)
        {
            var result = new SigningValidationResult { IsValid = true };
            var usage = GetExceptionUsage(teamId);
            
            // Check date (after January 5)
            if (currentDate.Month == 1 && currentDate.Day < LeagueCBA.TEN_DAY_START_DAY)
            {
                result.IsValid = false;
                result.Reason = $"10-day contracts not available until January {LeagueCBA.TEN_DAY_START_DAY}";
                return result;
            }
            
            // Check trade deadline proximity (10 days before season end)
            var deadline = new DateTime(currentDate.Year, 4, 5);
            if (currentDate > deadline)
            {
                result.IsValid = false;
                result.Reason = "10-day contract deadline has passed";
                return result;
            }
            
            // Max 2 per player per team per season
            int priorTenDays = usage.TenDayHistory.TryGetValue(playerId, out int count) ? count : 0;
            if (priorTenDays >= LeagueCBA.MAX_TEN_DAY_PER_PLAYER)
            {
                result.IsValid = false;
                result.Reason = $"Player already had {LeagueCBA.MAX_TEN_DAY_PER_PLAYER} 10-day contracts with this team";
            }
            
            return result;
        }

        private SigningValidationResult ValidateCapSpaceSigning(string teamId, SigningOffer offer)
        {
            var result = new SigningValidationResult { IsValid = true };
            long capSpace = _capManager.GetCapSpace(teamId);
            
            if (offer.AnnualSalary > capSpace)
            {
                result.IsValid = false;
                result.Reason = $"Salary ${offer.AnnualSalary:N0} exceeds cap space ${capSpace:N0}";
            }
            
            if (offer.Years > LeagueCBA.MAX_CONTRACT_YEARS_OTHER)
            {
                result.IsValid = false;
                result.Reason = $"Max {LeagueCBA.MAX_CONTRACT_YEARS_OTHER} years for non-Bird signing";
            }
            
            return result;
        }

        private SigningValidationResult ValidateBirdRightsSigning(string teamId, FreeAgent freeAgent, SigningOffer offer)
        {
            var result = new SigningValidationResult { IsValid = true };
            
            // Must be the player's previous team
            if (freeAgent.PreviousTeamId != teamId)
            {
                result.IsValid = false;
                result.Reason = "Cannot use Bird rights - not the player's previous team";
                return result;
            }
            
            // Check Bird rights type
            int maxYears;
            long maxSalary;
            
            switch (freeAgent.BirdRights)
            {
                case BirdRightsType.Full:
                    maxYears = LeagueCBA.MAX_CONTRACT_YEARS_BIRD;
                    maxSalary = LeagueCBA.GetMaxSalary(10); // Assume 10+ years for max
                    break;
                    
                case BirdRightsType.Early:
                    maxYears = 4;
                    // 175% of previous or 105% of average
                    maxSalary = LeagueCBA.NON_TAXPAYER_MLE * 2; // Simplified
                    break;
                    
                case BirdRightsType.NonBird:
                    maxYears = 4;
                    maxSalary = (long)(LeagueCBA.GetMinimumSalary(5) * 1.2f);
                    break;
                    
                default:
                    result.IsValid = false;
                    result.Reason = "Player has no Bird rights";
                    return result;
            }
            
            if (offer.Years > maxYears)
            {
                result.IsValid = false;
                result.Reason = $"Max {maxYears} years with {freeAgent.BirdRights} Bird rights";
            }
            
            if (offer.AnnualSalary > maxSalary)
            {
                result.IsValid = false;
                result.Reason = $"Salary exceeds max for {freeAgent.BirdRights} Bird rights";
            }
            
            return result;
        }

        private SigningValidationResult ValidateMLESigning(string teamId, SigningOffer offer)
        {
            var result = new SigningValidationResult { IsValid = true };
            var (mleAmount, mleYears, mleType) = _capManager.GetAvailableMLE(teamId);
            
            // Check if already used this season
            var usage = GetExceptionUsage(teamId);
            if (usage.MLEUsed >= mleAmount)
            {
                result.IsValid = false;
                result.Reason = "Mid-Level Exception already fully used";
                return result;
            }
            
            long remaining = mleAmount - usage.MLEUsed;
            
            if (offer.AnnualSalary > remaining)
            {
                result.IsValid = false;
                result.Reason = $"Salary ${offer.AnnualSalary:N0} exceeds remaining MLE ${remaining:N0}";
            }
            
            if (offer.Years > mleYears)
            {
                result.IsValid = false;
                result.Reason = $"Max {mleYears} years for {mleType}";
            }
            
            // Teams above second apron cannot use MLE
            if (_capManager.GetCapStatus(teamId) >= TeamCapStatus.AboveSecondApron)
            {
                result.IsValid = false;
                result.Reason = "Cannot use MLE - above second apron";
            }
            
            return result;
        }

        private SigningValidationResult ValidateBiAnnualSigning(string teamId, SigningOffer offer)
        {
            var result = new SigningValidationResult { IsValid = true };
            
            // Check availability
            var usage = GetExceptionUsage(teamId);
            if (usage.BiAnnualUsed)
            {
                result.IsValid = false;
                result.Reason = "Bi-Annual Exception already used";
                return result;
            }
            
            if (!_capManager.CanUseBiAnnualException(teamId, DateTime.Now.Year))
            {
                result.IsValid = false;
                result.Reason = "Bi-Annual Exception not available this season";
                return result;
            }
            
            if (offer.AnnualSalary > LeagueCBA.BI_ANNUAL_EXCEPTION)
            {
                result.IsValid = false;
                result.Reason = $"Salary exceeds BAE (${LeagueCBA.BI_ANNUAL_EXCEPTION:N0})";
            }
            
            if (offer.Years > LeagueCBA.BI_ANNUAL_MAX_YEARS)
            {
                result.IsValid = false;
                result.Reason = $"Max {LeagueCBA.BI_ANNUAL_MAX_YEARS} years for BAE";
            }
            
            return result;
        }

        private SigningValidationResult ValidateMinimumSigning(SigningOffer offer)
        {
            var result = new SigningValidationResult { IsValid = true };
            
            long minSalary = LeagueCBA.GetMinimumSalary(offer.PlayerYearsExperience);
            
            if (offer.AnnualSalary > minSalary * 1.05f)
            {
                result.IsValid = false;
                result.Reason = "Salary exceeds minimum for this experience level";
            }
            
            if (offer.Years > 2)
            {
                result.IsValid = false;
                result.Reason = "Minimum contracts max 2 years";
            }
            
            return result;
        }

        // ==================== EXECUTE SIGNING ====================

        /// <summary>
        /// Executes a free agent signing.
        /// </summary>
        public bool ExecuteSigning(string teamId, string playerId, SigningOffer offer)
        {
            var validationResult = CanSign(teamId, playerId, offer);
            if (!validationResult.IsValid)
            {
                Debug.LogWarning($"Signing failed: {validationResult.Reason}");
                return false;
            }
            
            // Create contract
            var contract = Contract.Create(
                playerId, 
                teamId, 
                offer.AnnualSalary, 
                offer.Years,
                offer.Method == SigningMethod.BirdRights
            );
            
            _capManager.RegisterContract(contract);
            
            // Update exception usage
            UpdateExceptionUsage(teamId, offer);
            
            // Remove from free agent list
            _freeAgents.RemoveAll(fa => fa.PlayerId == playerId);
            
            // Update player's team
            var player = _playerDatabase?.GetPlayer(playerId);
            if (player != null)
            {
                player.TeamId = teamId;
            }
            
            Debug.Log($"Signed {playerId} to {teamId}: ${offer.AnnualSalary:N0}/yr for {offer.Years} years via {offer.Method}");
            return true;
        }

        // ==================== RESTRICTED FREE AGENCY ====================

        /// <summary>
        /// Extends a qualifying offer to make a player an RFA.
        /// </summary>
        public bool ExtendQualifyingOffer(string teamId, string playerId)
        {
            var freeAgent = _freeAgents.FirstOrDefault(fa => fa.PlayerId == playerId);
            if (freeAgent == null || freeAgent.PreviousTeamId != teamId)
                return false;
            
            freeAgent.Type = FreeAgentType.Restricted;
            freeAgent.HasQualifyingOffer = true;
            
            return true;
        }

        /// <summary>
        /// Matches an offer sheet for an RFA.
        /// </summary>
        public bool MatchOfferSheet(string teamId, string playerId, SigningOffer matchedOffer)
        {
            var freeAgent = _freeAgents.FirstOrDefault(fa => fa.PlayerId == playerId);
            if (freeAgent == null || freeAgent.Type != FreeAgentType.Restricted)
                return false;
            
            if (freeAgent.PreviousTeamId != teamId)
                return false;
            
            // Create contract matching the offer
            return ExecuteSigning(teamId, playerId, matchedOffer);
        }

        // ==================== UTILITY ====================

        private ExceptionUsage GetExceptionUsage(string teamId)
        {
            if (!_teamExceptions.TryGetValue(teamId, out var usage))
            {
                usage = new ExceptionUsage();
                _teamExceptions[teamId] = usage;
            }
            return usage;
        }

        private void UpdateExceptionUsage(string teamId, SigningOffer offer)
        {
            var usage = GetExceptionUsage(teamId);
            
            switch (offer.Method)
            {
                case SigningMethod.MidLevelException:
                    usage.MLEUsed += offer.AnnualSalary;
                    break;
                case SigningMethod.BiAnnualException:
                    usage.BiAnnualUsed = true;
                    break;
            }
        }

        private bool MatchesFilter(FreeAgent fa, FreeAgentFilter filter)
        {
            if (filter.MinValue.HasValue && fa.EstimatedValue < filter.MinValue.Value)
                return false;
            if (filter.MaxValue.HasValue && fa.EstimatedValue > filter.MaxValue.Value)
                return false;
            if (filter.Type.HasValue && fa.Type != filter.Type.Value)
                return false;
            return true;
        }

        private float EstimatePlayerValue(Player player)
        {
            // Simple value estimation based on overall rating
            return player.OverallRating / 10f;
        }

        /// <summary>
        /// Resets exception usage for new season.
        /// </summary>
        public void StartNewSeason()
        {
            _teamExceptions.Clear();
        }
    }

    // ==================== DATA TYPES ====================

    public class FreeAgent
    {
        public string PlayerId;
        public FreeAgentType Type;
        public string PreviousTeamId;
        public BirdRightsType BirdRights;
        public float EstimatedValue;
        public bool HasQualifyingOffer;
    }

    public enum FreeAgentType
    {
        Unrestricted,
        Restricted
    }

    public class SigningOffer
    {
        public long AnnualSalary;
        public int Years;
        public SigningMethod Method;
        public int PlayerYearsExperience;
        public bool HasPlayerOption;
        public bool HasTeamOption;
    }

    public enum SigningMethod
    {
        CapSpace,
        BirdRights,
        MidLevelException,
        BiAnnualException,
        MinimumSalary,
        TradedPlayerException,
        TwoWayContract,     // G-League/NBA (max 3 per team)
        TenDayContract      // 10-day deal (after Jan 5)
    }

    public class SigningValidationResult
    {
        public bool IsValid;
        public string Reason;
    }

    public class FreeAgentFilter
    {
        public float? MinValue;
        public float? MaxValue;
        public FreeAgentType? Type;
    }

    public class ExceptionUsage
    {
        public long MLEUsed;
        public bool BiAnnualUsed;
        public int TwoWayCount; // Max 3 per team
        public Dictionary<string, int> TenDayHistory = new Dictionary<string, int>(); // playerId -> count
        
        public void RecordTwoWay()
        {
            TwoWayCount++;
        }
        
        public void RecordTenDay(string playerId)
        {
            if (!TenDayHistory.ContainsKey(playerId))
                TenDayHistory[playerId] = 0;
            TenDayHistory[playerId]++;
        }
    }
}
