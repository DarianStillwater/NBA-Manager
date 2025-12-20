using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Status of a staff negotiation.
    /// </summary>
    public enum StaffNegotiationStatus
    {
        NotStarted,
        InProgress,
        CounterReceived,
        Accepted,
        Rejected,
        WalkedAway,
        Completed
    }

    /// <summary>
    /// A contract offer to a staff candidate.
    /// </summary>
    [Serializable]
    public class StaffContractOffer
    {
        public int AnnualSalary;
        public int Years;  // 1-5 years

        public int TotalValue => AnnualSalary * Years;
    }

    /// <summary>
    /// Response from a negotiation round.
    /// </summary>
    [Serializable]
    public class NegotiationResponse
    {
        public bool Success;
        public string Message;
        public StaffNegotiationStatus NewStatus;
        public int? CounterSalary;
        public int? CounterYears;
    }

    /// <summary>
    /// Tracks a negotiation session with a staff candidate.
    /// </summary>
    [Serializable]
    public class StaffNegotiationSession
    {
        public string NegotiationId;
        public string TeamId;
        public string CandidateId;
        public string CandidateName;
        public bool IsCoach;  // True = coach, False = scout
        public StaffPositionType Position;

        [Header("Status")]
        public StaffNegotiationStatus Status;
        public int RoundNumber;
        public int MaxRounds;

        [Header("Candidate Expectations")]
        public int AskingPrice;        // Initial asking salary
        public int MinimumAcceptable;  // Walk-away threshold
        public int PreferredYears;

        [Header("Current State")]
        public int LastOfferSalary;
        public int LastOfferYears;
        public int CounterSalary;
        public int CounterYears;

        [Header("Final Terms")]
        public int FinalSalary;
        public int FinalYears;

        public List<NegotiationRound> History = new List<NegotiationRound>();

        /// <summary>
        /// Process an offer from the team.
        /// </summary>
        public NegotiationResponse ProcessOffer(StaffContractOffer offer)
        {
            LastOfferSalary = offer.AnnualSalary;
            LastOfferYears = offer.Years;

            var response = new NegotiationResponse();

            // Calculate offer percentage of asking price
            float offerPercent = (float)offer.AnnualSalary / AskingPrice;

            // Record this round
            var round = new NegotiationRound
            {
                RoundNumber = RoundNumber,
                OfferSalary = offer.AnnualSalary,
                OfferYears = offer.Years
            };

            // Accept threshold: 95% of asking price
            if (offerPercent >= 0.95f)
            {
                Status = StaffNegotiationStatus.Accepted;
                FinalSalary = offer.AnnualSalary;
                FinalYears = offer.Years;

                round.Response = "Accepted";
                History.Add(round);

                response.Success = true;
                response.Message = $"{CandidateName} accepts your offer!";
                response.NewStatus = Status;
                return response;
            }

            // Reject if below minimum and final round
            if (offer.AnnualSalary < MinimumAcceptable && RoundNumber >= MaxRounds)
            {
                Status = StaffNegotiationStatus.Rejected;
                round.Response = "Rejected - walked away";
                History.Add(round);

                response.Success = false;
                response.Message = $"{CandidateName} is not interested at that price and ends negotiations.";
                response.NewStatus = Status;
                return response;
            }

            // Counter-offer
            RoundNumber++;

            // Calculate counter - candidate lowers ask based on round number
            float reductionPerRound = 0.05f;
            float newAskMultiplier = 1f - (reductionPerRound * (RoundNumber - 1));
            CounterSalary = (int)(AskingPrice * Math.Max(0.85f, newAskMultiplier));
            CounterYears = PreferredYears;

            // If offer is close, split the difference
            if (offerPercent >= 0.85f)
            {
                CounterSalary = (offer.AnnualSalary + CounterSalary) / 2;
            }

            Status = StaffNegotiationStatus.CounterReceived;

            round.Response = $"Counter: ${CounterSalary:N0}";
            round.CounterSalary = CounterSalary;
            round.CounterYears = CounterYears;
            History.Add(round);

            response.Success = true;
            response.Message = $"{CandidateName} counters with ${CounterSalary:N0}/year for {CounterYears} years.";
            response.NewStatus = Status;
            response.CounterSalary = CounterSalary;
            response.CounterYears = CounterYears;

            return response;
        }
    }

    /// <summary>
    /// Record of a single negotiation round.
    /// </summary>
    [Serializable]
    public class NegotiationRound
    {
        public int RoundNumber;
        public int OfferSalary;
        public int OfferYears;
        public string Response;
        public int? CounterSalary;
        public int? CounterYears;
    }
}
