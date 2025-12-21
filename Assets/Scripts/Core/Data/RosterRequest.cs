using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Types of roster requests a coach can make to the GM.
    /// </summary>
    [Serializable]
    public enum RosterRequestType
    {
        TradePlayer,        // Trade a specific player
        SignFreeAgent,      // Sign a free agent
        WaivePlayer,        // Cut a player
        ExtendContract,     // Extend a player's contract
        PromoteGLeague,     // Bring up a G-League player
        AcquireBigMan,      // Need a center/PF
        AcquireGuard,       // Need a guard
        AcquireShooter,     // Need shooting
        AcquireDefender,    // Need defensive help
        AcquireVeteran,     // Need veteran presence
        IncreaseBudget,     // Request more cap space flexibility
        TradeForPick        // Trade for a draft pick
    }

    /// <summary>
    /// Priority level for roster requests.
    /// </summary>
    [Serializable]
    public enum RequestPriority
    {
        Low,        // Nice to have
        Medium,     // Would help the team
        High,       // Significant need
        Critical    // Urgent - team success depends on it
    }

    /// <summary>
    /// Status of a roster request.
    /// </summary>
    [Serializable]
    public enum RequestStatus
    {
        Pending,    // Awaiting GM decision
        Approved,   // GM agreed to action
        Denied,     // GM refused
        InProgress, // GM is working on it
        Completed,  // Request fulfilled
        Expired     // No longer relevant (player signed elsewhere, etc.)
    }

    /// <summary>
    /// A roster request from the coach to the GM.
    /// </summary>
    [Serializable]
    public class RosterRequest
    {
        public string RequestId;
        public DateTime RequestDate;
        public RosterRequestType Type;
        public RequestPriority Priority;
        public RequestStatus Status;

        // Target player (if applicable)
        public string TargetPlayerId;
        public string TargetPlayerName;

        // For trades - what we'd give up
        public string TradeAwayPlayerId;
        public string TradeAwayPlayerName;

        // Coach's reasoning
        public string CoachReasoning;
        public string PositionNeed;

        // GM's response
        public RosterRequestResult Result;

        public RosterRequest()
        {
            RequestId = Guid.NewGuid().ToString();
            RequestDate = DateTime.Now;
            Status = RequestStatus.Pending;
        }

        public static RosterRequest CreateTradeRequest(string targetPlayerId, string targetName, string tradeAwayId, string tradeAwayName, string reasoning)
        {
            return new RosterRequest
            {
                Type = RosterRequestType.TradePlayer,
                Priority = RequestPriority.Medium,
                TargetPlayerId = targetPlayerId,
                TargetPlayerName = targetName,
                TradeAwayPlayerId = tradeAwayId,
                TradeAwayPlayerName = tradeAwayName,
                CoachReasoning = reasoning
            };
        }

        public static RosterRequest CreateSigningRequest(string playerId, string playerName, string reasoning)
        {
            return new RosterRequest
            {
                Type = RosterRequestType.SignFreeAgent,
                Priority = RequestPriority.Medium,
                TargetPlayerId = playerId,
                TargetPlayerName = playerName,
                CoachReasoning = reasoning
            };
        }

        public static RosterRequest CreateWaiveRequest(string playerId, string playerName, string reasoning)
        {
            return new RosterRequest
            {
                Type = RosterRequestType.WaivePlayer,
                Priority = RequestPriority.Low,
                TargetPlayerId = playerId,
                TargetPlayerName = playerName,
                CoachReasoning = reasoning
            };
        }

        public static RosterRequest CreateNeedRequest(RosterRequestType needType, RequestPriority priority, string reasoning)
        {
            return new RosterRequest
            {
                Type = needType,
                Priority = priority,
                CoachReasoning = reasoning,
                PositionNeed = GetPositionNeedString(needType)
            };
        }

        private static string GetPositionNeedString(RosterRequestType type)
        {
            return type switch
            {
                RosterRequestType.AcquireBigMan => "Center or Power Forward",
                RosterRequestType.AcquireGuard => "Point Guard or Shooting Guard",
                RosterRequestType.AcquireShooter => "Perimeter shooter",
                RosterRequestType.AcquireDefender => "Defensive specialist",
                RosterRequestType.AcquireVeteran => "Veteran leader",
                _ => ""
            };
        }
    }

    /// <summary>
    /// Result of a roster request from the GM.
    /// </summary>
    [Serializable]
    public class RosterRequestResult
    {
        public bool IsApproved;
        public DateTime ResponseDate;
        public string GMResponse;
        public string RevealedTrait;  // Personality insight about the GM
        public string ActionTaken;    // What the GM actually did

        // If trade approved - the actual trade details
        public TradeDetails ActualTrade;

        // If signing approved - contract details
        public ContractOffer ActualContract;

        public RosterRequestResult()
        {
            ResponseDate = DateTime.Now;
        }

        public static RosterRequestResult Approve(string response, string actionTaken = null)
        {
            return new RosterRequestResult
            {
                IsApproved = true,
                GMResponse = response,
                ActionTaken = actionTaken
            };
        }

        public static RosterRequestResult Deny(string response, string revealedTrait = null)
        {
            return new RosterRequestResult
            {
                IsApproved = false,
                GMResponse = response,
                RevealedTrait = revealedTrait
            };
        }
    }

    /// <summary>
    /// Trade details when a trade request is approved.
    /// </summary>
    [Serializable]
    public class TradeDetails
    {
        public string TradeId;
        public DateTime TradeDate;
        public List<string> PlayersAcquired = new List<string>();
        public List<string> PlayersSent = new List<string>();
        public List<int> PicksAcquired = new List<int>();
        public List<int> PicksSent = new List<int>();
    }

    /// <summary>
    /// Contract offer details when a signing is approved.
    /// </summary>
    [Serializable]
    public class ContractOffer
    {
        public string PlayerId;
        public int Years;
        public int TotalValue;
        public int AnnualValue;
        public bool HasPlayerOption;
        public bool HasTeamOption;
    }

    /// <summary>
    /// History of all roster requests for the season.
    /// </summary>
    [Serializable]
    public class RosterRequestHistory
    {
        public List<RosterRequest> AllRequests = new List<RosterRequest>();
        public int TotalRequests;
        public int ApprovedRequests;
        public int DeniedRequests;
        public int PendingRequests;

        public void AddRequest(RosterRequest request)
        {
            AllRequests.Add(request);
            TotalRequests++;
            PendingRequests++;
        }

        public void UpdateRequestResult(string requestId, RosterRequestResult result)
        {
            var request = AllRequests.Find(r => r.RequestId == requestId);
            if (request != null)
            {
                request.Result = result;
                request.Status = result.IsApproved ? RequestStatus.Approved : RequestStatus.Denied;
                PendingRequests--;

                if (result.IsApproved)
                    ApprovedRequests++;
                else
                    DeniedRequests++;
            }
        }

        public List<RosterRequest> GetPendingRequests()
        {
            return AllRequests.FindAll(r => r.Status == RequestStatus.Pending);
        }

        public float GetApprovalRate()
        {
            int decided = ApprovedRequests + DeniedRequests;
            if (decided == 0) return 0;
            return (float)ApprovedRequests / decided;
        }
    }

    /// <summary>
    /// Quick summary of coach-GM relationship based on request history.
    /// </summary>
    [Serializable]
    public class CoachGMRelationship
    {
        public float ApprovalRate;
        public int TotalInteractions;
        public List<string> KnownGMTraits = new List<string>();
        public string RelationshipStatus;  // "Good", "Strained", "New", etc.
        public DateTime LastInteraction;

        public void UpdateFromHistory(RosterRequestHistory history)
        {
            ApprovalRate = history.GetApprovalRate();
            TotalInteractions = history.TotalRequests;

            RelationshipStatus = TotalInteractions switch
            {
                0 => "New",
                < 5 => "Building",
                _ => ApprovalRate switch
                {
                    >= 0.7f => "Strong",
                    >= 0.5f => "Professional",
                    >= 0.3f => "Strained",
                    _ => "Difficult"
                }
            };
        }
    }
}
