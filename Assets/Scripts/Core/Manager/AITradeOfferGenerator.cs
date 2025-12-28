using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Generates incoming trade offers from AI teams to the player's team.
    /// Offers are rare (1-2 per week) and based on FO personality.
    /// </summary>
    public class AITradeOfferGenerator
    {
        private PlayerDatabase _playerDatabase;
        private SalaryCapManager _capManager;
        private PlayerValueCalculator _valueCalculator;
        private DraftPickRegistry _draftPickRegistry;
        private Dictionary<string, FrontOfficeProfile> _frontOffices;
        private string _playerTeamId;

        // Pending offers
        private List<IncomingTradeOffer> _pendingOffers = new List<IncomingTradeOffer>();

        // Events
        public event Action<IncomingTradeOffer> OnNewOfferGenerated;
        public event Action<IncomingTradeOffer> OnOfferExpired;

        // Configuration
        private const float DAILY_OFFER_CHANCE = 0.15f; // ~15% daily = 1-2 per week
        private const int MIN_OFFER_EXPIRY_DAYS = 3;
        private const int MAX_OFFER_EXPIRY_DAYS = 7;

        public AITradeOfferGenerator(
            PlayerDatabase playerDatabase,
            SalaryCapManager capManager,
            PlayerValueCalculator valueCalculator,
            DraftPickRegistry draftPickRegistry = null)
        {
            _playerDatabase = playerDatabase;
            _capManager = capManager;
            _valueCalculator = valueCalculator;
            _draftPickRegistry = draftPickRegistry;
            _frontOffices = new Dictionary<string, FrontOfficeProfile>();
        }

        /// <summary>
        /// Load initial front office profiles from JSON resource.
        /// Should be called during game initialization.
        /// </summary>
        public void LoadInitialFrontOfficeData()
        {
            var jsonAsset = Resources.Load<TextAsset>("Data/initial_front_offices");
            if (jsonAsset == null)
            {
                Debug.LogWarning("[AITradeOfferGenerator] Could not load initial_front_offices.json - using default profiles");
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<InitialFrontOfficeData>(jsonAsset.text);
                if (data?.frontOffices == null)
                {
                    Debug.LogWarning("[AITradeOfferGenerator] initial_front_offices.json is empty or invalid");
                    return;
                }

                foreach (var entry in data.frontOffices)
                {
                    var profile = entry.ToFrontOfficeProfile();
                    RegisterFrontOffice(profile);
                }

                Debug.Log($"[AITradeOfferGenerator] Loaded {data.frontOffices.Count} front office profiles (as of {data.dataAsOf})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AITradeOfferGenerator] Error parsing initial_front_offices.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Get front office profile for a team.
        /// </summary>
        public FrontOfficeProfile GetFrontOffice(string teamId)
        {
            return _frontOffices.TryGetValue(teamId, out var profile) ? profile : null;
        }

        /// <summary>
        /// Get all registered front office profiles.
        /// </summary>
        public IReadOnlyDictionary<string, FrontOfficeProfile> GetAllFrontOffices()
        {
            return _frontOffices;
        }

        /// <summary>
        /// Set the player's team ID.
        /// </summary>
        public void SetPlayerTeamId(string teamId)
        {
            _playerTeamId = teamId;
        }

        /// <summary>
        /// Register front office profiles for AI decision making.
        /// </summary>
        public void RegisterFrontOffice(FrontOfficeProfile profile)
        {
            _frontOffices[profile.TeamId] = profile;
        }

        /// <summary>
        /// Process daily - called on each day advance.
        /// </summary>
        public void ProcessDailyOffers()
        {
            if (string.IsNullOrEmpty(_playerTeamId)) return;

            // Expire old offers
            ExpireOldOffers();

            // Check if we should generate a new offer today
            if (ShouldGenerateOffer())
            {
                var offer = TryGenerateOffer();
                if (offer != null)
                {
                    _pendingOffers.Add(offer);
                    OnNewOfferGenerated?.Invoke(offer);
                    Debug.Log($"[AITradeOffer] New offer from {offer.OfferingTeamId}: {offer.OfferMessage}");
                }
            }
        }

        /// <summary>
        /// Determine if an offer should be generated today.
        /// </summary>
        private bool ShouldGenerateOffer()
        {
            // Base chance
            float chance = DAILY_OFFER_CHANCE;

            // Reduce chance if already have pending offers
            int pendingCount = _pendingOffers.Count(o => o.Status == IncomingOfferStatus.Pending);
            if (pendingCount >= 3) return false; // Max 3 pending at once
            if (pendingCount >= 1) chance *= 0.5f; // Halve chance if already have one

            return UnityEngine.Random.value < chance;
        }

        /// <summary>
        /// Try to generate a valid offer.
        /// </summary>
        private IncomingTradeOffer TryGenerateOffer()
        {
            // Select an interested team
            var offeringTeam = SelectInterestedTeam();
            if (offeringTeam == null) return null;

            // Identify desirable players on user's team
            var desirablePlayers = IdentifyDesirablePlayers(offeringTeam);
            if (desirablePlayers.Count == 0) return null;

            // Pick a target player
            var targetPlayer = desirablePlayers[UnityEngine.Random.Range(0, desirablePlayers.Count)];

            // Build a trade proposal
            var proposal = BuildTradeProposal(offeringTeam, targetPlayer);
            if (proposal == null) return null;

            // Create the offer
            var currentDate = GameManager.Instance?.CurrentDate ?? DateTime.Now;
            int expiryDays = UnityEngine.Random.Range(MIN_OFFER_EXPIRY_DAYS, MAX_OFFER_EXPIRY_DAYS + 1);

            return new IncomingTradeOffer
            {
                OfferId = Guid.NewGuid().ToString(),
                OfferingTeamId = offeringTeam.TeamId,
                Proposal = proposal,
                OfferMessage = GenerateOfferMessage(offeringTeam, targetPlayer),
                ReceivedAt = currentDate,
                ExpiresAt = currentDate.AddDays(expiryDays),
                Status = IncomingOfferStatus.Pending
            };
        }

        /// <summary>
        /// Select an AI team that might be interested in trading.
        /// Based on FO personality - aggressive teams more likely.
        /// </summary>
        private FrontOfficeProfile SelectInterestedTeam()
        {
            var candidates = new List<(FrontOfficeProfile fo, float weight)>();

            foreach (var kvp in _frontOffices)
            {
                if (kvp.Key == _playerTeamId) continue; // Skip player's team

                var fo = kvp.Value;
                float weight = 1f;

                // Aggression affects likelihood
                weight *= fo.Aggression switch
                {
                    TradeAggression.Desperate => 3f,
                    TradeAggression.Aggressive => 2f,
                    TradeAggression.Moderate => 1f,
                    TradeAggression.Conservative => 0.5f,
                    TradeAggression.VeryPassive => 0.2f,
                    _ => 1f
                };

                // Team situation affects likelihood
                weight *= fo.CurrentSituation switch
                {
                    TeamSituation.Championship => 1.5f,    // Actively improving
                    TeamSituation.Contending => 1.3f,
                    TeamSituation.PlayoffBubble => 1.2f,   // Need a push
                    TeamSituation.Rebuilding => 1.4f,      // Looking to acquire young talent or picks
                    TeamSituation.StuckInMiddle => 1.1f,
                    _ => 1f
                };

                if (weight > 0)
                {
                    candidates.Add((fo, weight));
                }
            }

            if (candidates.Count == 0) return null;

            // Weighted random selection
            float totalWeight = candidates.Sum(c => c.weight);
            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            foreach (var (fo, weight) in candidates)
            {
                cumulative += weight;
                if (roll <= cumulative)
                    return fo;
            }

            return candidates.Last().fo;
        }

        /// <summary>
        /// Identify players on user's team that the AI might want.
        /// </summary>
        private List<Player> IdentifyDesirablePlayers(FrontOfficeProfile offeringFO)
        {
            var desirable = new List<Player>();
            var userTeam = GameManager.Instance?.GetTeam(_playerTeamId);

            if (userTeam?.Roster == null) return desirable;

            foreach (var player in userTeam.Roster)
            {
                var contract = _capManager?.GetContract(player.PlayerId);
                var assessment = _valueCalculator?.CalculateValue(player, contract, offeringFO);

                if (assessment == null) continue;

                bool isDesirable = false;

                // Contenders want proven players
                if (offeringFO.CurrentSituation <= TeamSituation.Contending)
                {
                    if (player.OverallRating >= 75 && player.Age <= 32)
                        isDesirable = true;
                }

                // Rebuilding teams want young players with potential
                if (offeringFO.CurrentSituation == TeamSituation.Rebuilding)
                {
                    if (player.Age <= 24 && player.HiddenPotential >= 70)
                        isDesirable = true;
                }

                // Value-based: anyone with good contract value
                if (assessment.ContractValue >= 10f)
                    isDesirable = true;

                // Don't target minimum salary players (not worth a formal offer)
                if (contract != null && contract.CurrentYearSalary < 3_000_000)
                    isDesirable = false;

                if (isDesirable)
                    desirable.Add(player);
            }

            return desirable;
        }

        /// <summary>
        /// Build a trade proposal for the target player.
        /// </summary>
        private TradeProposal BuildTradeProposal(FrontOfficeProfile offeringFO, Player targetPlayer)
        {
            var proposal = new TradeProposal { ProposedDate = DateTime.Now };
            var targetContract = _capManager?.GetContract(targetPlayer.PlayerId);

            if (targetContract == null) return null;

            // Add target player to proposal
            proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.Player,
                PlayerId = targetPlayer.PlayerId,
                Salary = targetContract.CurrentYearSalary,
                SendingTeamId = _playerTeamId,
                ReceivingTeamId = offeringFO.TeamId
            });

            // Calculate target value
            var targetAssessment = _valueCalculator?.CalculateValue(targetPlayer, targetContract, offeringFO);
            float targetValue = targetAssessment?.TotalValue ?? targetContract.CurrentYearSalary / 2_000_000f;

            // Build return package to match value
            float offeredValue = 0f;
            long offeredSalary = 0L;
            long neededSalary = targetContract.CurrentYearSalary;

            // Get available players from offering team
            var offeringTeam = GameManager.Instance?.GetTeam(offeringFO.TeamId);
            var availablePlayers = offeringTeam?.Roster?
                .OrderByDescending(p => _capManager?.GetContract(p.PlayerId)?.CurrentYearSalary ?? 0)
                .ToList() ?? new List<Player>();

            // Try to add players to match salary and value
            foreach (var offerPlayer in availablePlayers)
            {
                if (offeredValue >= targetValue * 0.9f && offeredSalary >= neededSalary * 0.8f)
                    break;

                var offerContract = _capManager?.GetContract(offerPlayer.PlayerId);
                if (offerContract == null) continue;

                // Skip if player is too valuable (offering team wouldn't give up)
                var offerAssessment = _valueCalculator?.CalculateValue(offerPlayer, offerContract, null);
                if (offerAssessment?.TotalValue > targetValue * 0.8f)
                    continue;

                // Add player to offer
                proposal.AllAssets.Add(new TradeAsset
                {
                    Type = TradeAssetType.Player,
                    PlayerId = offerPlayer.PlayerId,
                    Salary = offerContract.CurrentYearSalary,
                    SendingTeamId = offeringFO.TeamId,
                    ReceivingTeamId = _playerTeamId
                });

                offeredValue += offerAssessment?.TotalValue ?? 0f;
                offeredSalary += offerContract.CurrentYearSalary;
            }

            // Add draft picks if needed to reach value
            if (offeredValue < targetValue * 0.85f && offeringFO.WillingToIncludePicks)
            {
                var availablePicks = _draftPickRegistry?.GetPicksOwnedBy(offeringFO.TeamId)
                    ?? new List<DraftPick>();

                foreach (var pick in availablePicks.OrderBy(p => p.Year).ThenByDescending(p => p.Round))
                {
                    if (offeredValue >= targetValue * 0.95f) break;
                    if (proposal.AllAssets.Count(a => a.Type == TradeAssetType.DraftPick) >= 2) break;

                    float pickValue = pick.IsFirstRound ? 25f : 5f;
                    int yearsAway = pick.Year - DateTime.Now.Year;
                    pickValue *= Math.Max(0.3f, 1f - yearsAway * 0.1f);

                    proposal.AllAssets.Add(new TradeAsset
                    {
                        Type = TradeAssetType.DraftPick,
                        Year = pick.Year,
                        IsFirstRound = pick.IsFirstRound,
                        OriginalTeamId = pick.OriginalTeamId,
                        SendingTeamId = offeringFO.TeamId,
                        ReceivingTeamId = _playerTeamId,
                        DraftPickDetails = pick
                    });

                    offeredValue += pickValue;
                }
            }

            // Validate we have something reasonable
            if (proposal.AllAssets.Count(a => a.SendingTeamId == offeringFO.TeamId) == 0)
                return null;

            return proposal;
        }

        /// <summary>
        /// Generate a message from the AI GM.
        /// </summary>
        private string GenerateOfferMessage(FrontOfficeProfile fo, Player targetPlayer)
        {
            var messages = new List<string>();

            // Based on team situation
            string situation = fo.CurrentSituation switch
            {
                TeamSituation.Championship =>
                    $"We believe {targetPlayer.FullName} could be the missing piece for a championship run.",
                TeamSituation.Contending =>
                    $"We're looking to add talent for a playoff push, and {targetPlayer.FullName} fits our needs.",
                TeamSituation.Rebuilding =>
                    $"We're building for the future and see {targetPlayer.FullName} as part of our core.",
                TeamSituation.PlayoffBubble =>
                    $"We need to shake things up, and {targetPlayer.FullName} could help us get over the hump.",
                _ =>
                    $"We have interest in {targetPlayer.FullName} and believe this deal works for both teams."
            };

            messages.Add(situation);

            // Add personality flavor
            if (fo.Aggression >= TradeAggression.Aggressive)
            {
                messages.Add("We're prepared to move quickly on this.");
            }
            else if (fo.Aggression <= TradeAggression.Conservative)
            {
                messages.Add("Take your time reviewing this offer.");
            }

            return string.Join(" ", messages);
        }

        /// <summary>
        /// Expire offers that have passed their expiration date.
        /// </summary>
        public void ExpireOldOffers()
        {
            var currentDate = GameManager.Instance?.CurrentDate ?? DateTime.Now;

            foreach (var offer in _pendingOffers.Where(o => o.Status == IncomingOfferStatus.Pending))
            {
                if (currentDate >= offer.ExpiresAt)
                {
                    offer.Status = IncomingOfferStatus.Expired;
                    OnOfferExpired?.Invoke(offer);
                    Debug.Log($"[AITradeOffer] Offer from {offer.OfferingTeamId} expired");
                }
            }
        }

        /// <summary>
        /// Get all pending offers.
        /// </summary>
        public List<IncomingTradeOffer> GetPendingOffers()
        {
            return _pendingOffers
                .Where(o => o.Status == IncomingOfferStatus.Pending)
                .OrderByDescending(o => o.ReceivedAt)
                .ToList();
        }

        /// <summary>
        /// Get all offers (including historical).
        /// </summary>
        public List<IncomingTradeOffer> GetAllOffers()
        {
            return _pendingOffers.OrderByDescending(o => o.ReceivedAt).ToList();
        }

        /// <summary>
        /// Respond to an offer.
        /// </summary>
        public void RespondToOffer(string offerId, IncomingOfferResponse response)
        {
            var offer = _pendingOffers.FirstOrDefault(o => o.OfferId == offerId);
            if (offer == null) return;

            offer.Status = response switch
            {
                IncomingOfferResponse.Accept => IncomingOfferStatus.Accepted,
                IncomingOfferResponse.Reject => IncomingOfferStatus.Rejected,
                IncomingOfferResponse.Counter => IncomingOfferStatus.Countered,
                _ => offer.Status
            };

            Debug.Log($"[AITradeOffer] Responded to offer from {offer.OfferingTeamId}: {response}");
        }

        /// <summary>
        /// Get number of pending offers (for UI notification badge).
        /// </summary>
        public int GetPendingOfferCount()
        {
            return _pendingOffers.Count(o => o.Status == IncomingOfferStatus.Pending);
        }

        // === SAVE/LOAD ===

        public IncomingOffersSaveData CreateSaveData()
        {
            return new IncomingOffersSaveData
            {
                Offers = new List<IncomingTradeOffer>(_pendingOffers)
            };
        }

        public void RestoreFromSave(IncomingOffersSaveData data)
        {
            _pendingOffers = data?.Offers ?? new List<IncomingTradeOffer>();
        }
    }

    /// <summary>
    /// An incoming trade offer from an AI team.
    /// </summary>
    [Serializable]
    public class IncomingTradeOffer
    {
        public string OfferId;
        public string OfferingTeamId;
        public TradeProposal Proposal;
        public string OfferMessage;
        public DateTime ReceivedAt;
        public DateTime ExpiresAt;
        public IncomingOfferStatus Status;

        /// <summary>
        /// Days until offer expires.
        /// </summary>
        public int DaysUntilExpiry
        {
            get
            {
                var currentDate = GameManager.Instance?.CurrentDate ?? DateTime.Now;
                return Math.Max(0, (ExpiresAt - currentDate).Days);
            }
        }
    }

    /// <summary>
    /// Status of an incoming offer.
    /// </summary>
    public enum IncomingOfferStatus
    {
        Pending,
        Accepted,
        Rejected,
        Countered,
        Expired
    }

    /// <summary>
    /// Player's response to an incoming offer.
    /// </summary>
    public enum IncomingOfferResponse
    {
        Accept,
        Reject,
        Counter
    }

    /// <summary>
    /// Save data for incoming offers.
    /// </summary>
    [Serializable]
    public class IncomingOffersSaveData
    {
        public List<IncomingTradeOffer> Offers;
    }
}
