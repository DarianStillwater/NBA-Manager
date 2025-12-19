using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages staff hiring process including free agent pool and contract negotiations.
    /// All 30 teams compete for the same free agent pool.
    /// </summary>
    public class StaffHiringManager : MonoBehaviour
    {
        public static StaffHiringManager Instance { get; private set; }

        [Header("Free Agent Pool")]
        [SerializeField] private List<Coach> freeAgentCoaches = new List<Coach>();
        [SerializeField] private List<Scout> freeAgentScouts = new List<Scout>();

        [Header("Pool Settings")]
        [SerializeField] private int baseCoachPoolSize = 30;
        [SerializeField] private int baseScoutPoolSize = 15;

        [Header("Active Negotiations")]
        [SerializeField] private List<StaffNegotiationSession> activeNegotiations = new List<StaffNegotiationSession>();

        [Header("Candidate Browsing")]
        [SerializeField] private int currentCoachIndex = 0;
        [SerializeField] private int currentScoutIndex = 0;
        [SerializeField] private StaffPositionType currentBrowsingPosition;

        // Events
        public event Action<Coach> OnCoachHired;
        public event Action<Scout> OnScoutHired;
        public event Action<string, string> OnStaffHired;  // staffId, teamId
        public event Action OnFreeAgentPoolRefreshed;
        public event Action<StaffNegotiationSession> OnNegotiationStarted;
        public event Action<StaffNegotiationSession> OnNegotiationComplete;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // ==================== FREE AGENT POOL ====================

        /// <summary>
        /// Generate the free agent pool for a new season.
        /// Mix of randomly generated staff and former players.
        /// </summary>
        public void GenerateFreeAgentPool(int coachCount = -1, int scoutCount = -1)
        {
            coachCount = coachCount < 0 ? baseCoachPoolSize : coachCount;
            scoutCount = scoutCount < 0 ? baseScoutPoolSize : scoutCount;

            freeAgentCoaches.Clear();
            freeAgentScouts.Clear();

            var rng = new System.Random();

            // Generate coaches for various positions
            var positions = new[] {
                CoachPosition.HeadCoach,
                CoachPosition.AssistantCoach,
                CoachPosition.OffensiveCoordinator,
                CoachPosition.DefensiveCoordinator
            };

            for (int i = 0; i < coachCount; i++)
            {
                var position = positions[rng.Next(positions.Length)];
                var coach = Coach.CreateRandom(null, position, rng);
                coach.TeamId = null;  // Free agent
                freeAgentCoaches.Add(coach);
            }

            // Generate scouts
            for (int i = 0; i < scoutCount; i++)
            {
                var scout = Scout.CreateRandom(null, rng);
                scout.TeamId = null;  // Free agent
                freeAgentScouts.Add(scout);
            }

            // Sort by overall rating
            freeAgentCoaches = freeAgentCoaches.OrderByDescending(c => c.OverallRating).ToList();
            freeAgentScouts = freeAgentScouts.OrderByDescending(s => s.OverallRating).ToList();

            Debug.Log($"[StaffHiring] Generated free agent pool: {freeAgentCoaches.Count} coaches, {freeAgentScouts.Count} scouts");

            OnFreeAgentPoolRefreshed?.Invoke();
        }

        /// <summary>
        /// Add former players to the free agent pool.
        /// Called by FormerPlayerCareerManager when players retire.
        /// </summary>
        public void AddFormerPlayerCoach(Coach coach)
        {
            if (coach != null && !freeAgentCoaches.Any(c => c.CoachId == coach.CoachId))
            {
                coach.TeamId = null;
                freeAgentCoaches.Add(coach);
                freeAgentCoaches = freeAgentCoaches.OrderByDescending(c => c.OverallRating).ToList();
                Debug.Log($"[StaffHiring] Former player {coach.FullName} added to coaching pool");
            }
        }

        /// <summary>
        /// Add former player scout to the pool.
        /// </summary>
        public void AddFormerPlayerScout(Scout scout)
        {
            if (scout != null && !freeAgentScouts.Any(s => s.ScoutId == scout.ScoutId))
            {
                scout.TeamId = null;
                freeAgentScouts.Add(scout);
                freeAgentScouts = freeAgentScouts.OrderByDescending(s => s.OverallRating).ToList();
                Debug.Log($"[StaffHiring] Former player {scout.FullName} added to scouting pool");
            }
        }

        // ==================== CANDIDATE BROWSING (One-at-a-time) ====================

        /// <summary>
        /// Start browsing candidates for a position.
        /// </summary>
        public void StartBrowsingCandidates(StaffPositionType position)
        {
            currentBrowsingPosition = position;
            currentCoachIndex = 0;
            currentScoutIndex = 0;
        }

        /// <summary>
        /// Get the next coach candidate for the current position.
        /// </summary>
        public Coach GetNextCoachCandidate(StaffPositionType position)
        {
            var eligibleCoaches = GetCoachesForPosition(position);

            if (currentCoachIndex >= eligibleCoaches.Count)
            {
                currentCoachIndex = 0;  // Loop back
                return null;
            }

            return eligibleCoaches[currentCoachIndex];
        }

        /// <summary>
        /// Get the next scout candidate.
        /// </summary>
        public Scout GetNextScoutCandidate()
        {
            if (currentScoutIndex >= freeAgentScouts.Count)
            {
                currentScoutIndex = 0;  // Loop back
                return null;
            }

            return freeAgentScouts[currentScoutIndex];
        }

        /// <summary>
        /// Skip to the next candidate.
        /// </summary>
        public void SkipCandidate(bool isCoach)
        {
            if (isCoach)
                currentCoachIndex++;
            else
                currentScoutIndex++;
        }

        /// <summary>
        /// Get all coaches eligible for a position type.
        /// </summary>
        public List<Coach> GetCoachesForPosition(StaffPositionType positionType)
        {
            var coachPosition = positionType switch
            {
                StaffPositionType.HeadCoach => CoachPosition.HeadCoach,
                StaffPositionType.OffensiveCoordinator => CoachPosition.OffensiveCoordinator,
                StaffPositionType.DefensiveCoordinator => CoachPosition.DefensiveCoordinator,
                StaffPositionType.AssistantCoach => CoachPosition.AssistantCoach,
                _ => CoachPosition.AssistantCoach
            };

            return freeAgentCoaches.Where(c => c.Position == coachPosition).ToList();
        }

        /// <summary>
        /// Get all available scouts.
        /// </summary>
        public List<Scout> GetAvailableScouts()
        {
            return new List<Scout>(freeAgentScouts);
        }

        /// <summary>
        /// Get total count of available staff by type.
        /// </summary>
        public (int coaches, int scouts) GetPoolCounts()
        {
            return (freeAgentCoaches.Count, freeAgentScouts.Count);
        }

        // ==================== NEGOTIATION ====================

        /// <summary>
        /// Start a negotiation session with a candidate.
        /// </summary>
        public StaffNegotiationSession StartNegotiation(string teamId, string candidateId, bool isCoach)
        {
            // Check if already negotiating
            if (activeNegotiations.Any(n => n.CandidateId == candidateId && n.Status == StaffNegotiationStatus.InProgress))
            {
                Debug.LogWarning($"[StaffHiring] Already negotiating with {candidateId}");
                return null;
            }

            StaffNegotiationSession session;

            if (isCoach)
            {
                var coach = freeAgentCoaches.FirstOrDefault(c => c.CoachId == candidateId);
                if (coach == null) return null;

                session = StaffNegotiationSession.CreateForCoach(teamId, coach);
            }
            else
            {
                var scout = freeAgentScouts.FirstOrDefault(s => s.ScoutId == candidateId);
                if (scout == null) return null;

                session = StaffNegotiationSession.CreateForScout(teamId, scout);
            }

            activeNegotiations.Add(session);
            OnNegotiationStarted?.Invoke(session);

            Debug.Log($"[StaffHiring] Started negotiation with {session.CandidateName}");

            return session;
        }

        /// <summary>
        /// Make an offer in an active negotiation.
        /// </summary>
        public NegotiationResponse MakeOffer(string negotiationId, int annualSalary, int years)
        {
            var session = activeNegotiations.FirstOrDefault(n => n.NegotiationId == negotiationId);
            if (session == null || session.Status != StaffNegotiationStatus.InProgress)
            {
                return new NegotiationResponse
                {
                    Success = false,
                    Message = "Invalid negotiation session"
                };
            }

            var offer = new StaffContractOffer
            {
                AnnualSalary = annualSalary,
                Years = years
            };

            return session.ProcessOffer(offer);
        }

        /// <summary>
        /// Accept the current counter-offer.
        /// </summary>
        public (bool success, string message) AcceptCounterOffer(string negotiationId)
        {
            var session = activeNegotiations.FirstOrDefault(n => n.NegotiationId == negotiationId);
            if (session == null || session.Status != StaffNegotiationStatus.CounterReceived)
            {
                return (false, "No counter-offer to accept");
            }

            session.Status = StaffNegotiationStatus.Accepted;
            return FinalizeHire(negotiationId);
        }

        /// <summary>
        /// Finalize a successful negotiation and hire the staff member.
        /// </summary>
        public (bool success, string message) FinalizeHire(string negotiationId)
        {
            var session = activeNegotiations.FirstOrDefault(n => n.NegotiationId == negotiationId);
            if (session == null)
            {
                return (false, "Negotiation not found");
            }

            if (session.Status != StaffNegotiationStatus.Accepted)
            {
                return (false, "Negotiation not accepted");
            }

            if (session.IsCoach)
            {
                var coach = freeAgentCoaches.FirstOrDefault(c => c.CoachId == session.CandidateId);
                if (coach != null)
                {
                    // Update coach with negotiated terms
                    coach.TeamId = session.TeamId;
                    coach.AnnualSalary = session.FinalSalary;
                    coach.ContractYearsRemaining = session.FinalYears;
                    coach.ContractStartDate = DateTime.Now;

                    // Remove from free agent pool
                    freeAgentCoaches.Remove(coach);

                    // Notify
                    OnCoachHired?.Invoke(coach);
                    OnStaffHired?.Invoke(coach.CoachId, session.TeamId);

                    // Notify StaffManagementManager
                    var positionType = coach.Position switch
                    {
                        CoachPosition.HeadCoach => StaffPositionType.HeadCoach,
                        CoachPosition.OffensiveCoordinator => StaffPositionType.OffensiveCoordinator,
                        CoachPosition.DefensiveCoordinator => StaffPositionType.DefensiveCoordinator,
                        _ => StaffPositionType.AssistantCoach
                    };

                    if (StaffManagementManager.Instance != null)
                    {
                        StaffManagementManager.Instance.RecordHire(coach.CoachId, session.TeamId, positionType);
                    }

                    Debug.Log($"[StaffHiring] Hired coach {coach.FullName} to {session.TeamId} for ${session.FinalSalary}/yr x {session.FinalYears} years");
                }
            }
            else
            {
                var scout = freeAgentScouts.FirstOrDefault(s => s.ScoutId == session.CandidateId);
                if (scout != null)
                {
                    // Update scout with negotiated terms
                    scout.TeamId = session.TeamId;
                    scout.AnnualSalary = session.FinalSalary;
                    scout.ContractYearsRemaining = session.FinalYears;
                    scout.ContractStartDate = DateTime.Now;

                    // Remove from free agent pool
                    freeAgentScouts.Remove(scout);

                    // Notify
                    OnScoutHired?.Invoke(scout);
                    OnStaffHired?.Invoke(scout.ScoutId, session.TeamId);

                    if (StaffManagementManager.Instance != null)
                    {
                        StaffManagementManager.Instance.RecordHire(scout.ScoutId, session.TeamId, StaffPositionType.Scout);
                    }

                    Debug.Log($"[StaffHiring] Hired scout {scout.FullName} to {session.TeamId} for ${session.FinalSalary}/yr x {session.FinalYears} years");
                }
            }

            // Complete negotiation
            session.Status = StaffNegotiationStatus.Completed;
            OnNegotiationComplete?.Invoke(session);

            // Remove from active negotiations
            activeNegotiations.Remove(session);

            return (true, $"Successfully hired {session.CandidateName}");
        }

        /// <summary>
        /// Cancel/walk away from a negotiation.
        /// </summary>
        public void CancelNegotiation(string negotiationId)
        {
            var session = activeNegotiations.FirstOrDefault(n => n.NegotiationId == negotiationId);
            if (session != null)
            {
                session.Status = StaffNegotiationStatus.WalkedAway;
                activeNegotiations.Remove(session);
                Debug.Log($"[StaffHiring] Negotiation with {session.CandidateName} cancelled");
            }
        }

        // ==================== FIRING ====================

        /// <summary>
        /// Fire a staff member and return them to the free agent pool.
        /// </summary>
        public (bool success, string message) FireStaff(Coach coach, string teamId)
        {
            if (coach == null || coach.TeamId != teamId)
            {
                return (false, "Invalid coach or team mismatch");
            }

            // Calculate buyout
            int buyout = coach.BuyoutAmount;

            // Reset coach
            coach.TeamId = null;
            coach.ContractYearsRemaining = 0;

            // Add back to free agent pool
            freeAgentCoaches.Add(coach);
            freeAgentCoaches = freeAgentCoaches.OrderByDescending(c => c.OverallRating).ToList();

            // Notify
            var positionType = coach.Position switch
            {
                CoachPosition.HeadCoach => StaffPositionType.HeadCoach,
                CoachPosition.OffensiveCoordinator => StaffPositionType.OffensiveCoordinator,
                CoachPosition.DefensiveCoordinator => StaffPositionType.DefensiveCoordinator,
                _ => StaffPositionType.AssistantCoach
            };

            if (StaffManagementManager.Instance != null)
            {
                StaffManagementManager.Instance.RecordFiring(coach.CoachId, teamId, positionType);
            }

            Debug.Log($"[StaffHiring] Fired coach {coach.FullName} from {teamId} (buyout: ${buyout})");

            return (true, $"Fired {coach.FullName}. Buyout: ${buyout:N0}");
        }

        /// <summary>
        /// Fire a scout.
        /// </summary>
        public (bool success, string message) FireStaff(Scout scout, string teamId)
        {
            if (scout == null || scout.TeamId != teamId)
            {
                return (false, "Invalid scout or team mismatch");
            }

            // Reset scout
            scout.TeamId = null;
            scout.ContractYearsRemaining = 0;
            scout.IsAvailable = true;
            scout.CurrentAssignmentId = null;

            // Add back to free agent pool
            freeAgentScouts.Add(scout);
            freeAgentScouts = freeAgentScouts.OrderByDescending(s => s.OverallRating).ToList();

            if (StaffManagementManager.Instance != null)
            {
                StaffManagementManager.Instance.RecordFiring(scout.ScoutId, teamId, StaffPositionType.Scout);
            }

            Debug.Log($"[StaffHiring] Fired scout {scout.FullName} from {teamId}");

            return (true, $"Fired {scout.FullName}");
        }

        // ==================== AI TEAM HIRING ====================

        /// <summary>
        /// Process AI teams hiring from the pool.
        /// Called during offseason to simulate other teams filling positions.
        /// </summary>
        public void ProcessAITeamHiring(List<string> aiTeamIds, string userTeamId)
        {
            var rng = new System.Random();

            foreach (var teamId in aiTeamIds)
            {
                if (teamId == userTeamId) continue;

                // Random chance to hire coaches
                if (rng.NextDouble() < 0.3 && freeAgentCoaches.Count > 0)
                {
                    var coachIndex = rng.Next(Math.Min(5, freeAgentCoaches.Count));  // Prefer top candidates
                    var coach = freeAgentCoaches[coachIndex];

                    coach.TeamId = teamId;
                    coach.ContractYearsRemaining = rng.Next(2, 5);
                    freeAgentCoaches.Remove(coach);

                    Debug.Log($"[StaffHiring] AI team {teamId} hired coach {coach.FullName}");
                }

                // Random chance to hire scouts
                if (rng.NextDouble() < 0.2 && freeAgentScouts.Count > 0)
                {
                    var scoutIndex = rng.Next(Math.Min(3, freeAgentScouts.Count));
                    var scout = freeAgentScouts[scoutIndex];

                    scout.TeamId = teamId;
                    scout.ContractYearsRemaining = rng.Next(2, 4);
                    freeAgentScouts.Remove(scout);

                    Debug.Log($"[StaffHiring] AI team {teamId} hired scout {scout.FullName}");
                }
            }
        }

        // ==================== SAVE/LOAD ====================

        /// <summary>
        /// Get save data.
        /// </summary>
        public StaffHiringSaveData GetSaveData()
        {
            return new StaffHiringSaveData
            {
                FreeAgentCoaches = new List<Coach>(freeAgentCoaches),
                FreeAgentScouts = new List<Scout>(freeAgentScouts),
                ActiveNegotiations = new List<StaffNegotiationSession>(activeNegotiations)
            };
        }

        /// <summary>
        /// Load save data.
        /// </summary>
        public void LoadSaveData(StaffHiringSaveData data)
        {
            if (data == null) return;

            freeAgentCoaches = data.FreeAgentCoaches ?? new List<Coach>();
            freeAgentScouts = data.FreeAgentScouts ?? new List<Scout>();
            activeNegotiations = data.ActiveNegotiations ?? new List<StaffNegotiationSession>();

            Debug.Log($"[StaffHiring] Loaded: {freeAgentCoaches.Count} coaches, {freeAgentScouts.Count} scouts in pool");
        }
    }

    // ==================== NEGOTIATION DATA STRUCTURES ====================

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
        /// Create a negotiation session for a coach.
        /// </summary>
        public static StaffNegotiationSession CreateForCoach(string teamId, Coach coach)
        {
            var rng = new System.Random();

            // Calculate asking price based on market value with some premium
            int askingPrice = (int)(coach.MarketValue * (1.1f + rng.NextDouble() * 0.2f));
            int minAcceptable = (int)(coach.MarketValue * 0.85f);

            var positionType = coach.Position switch
            {
                CoachPosition.HeadCoach => StaffPositionType.HeadCoach,
                CoachPosition.OffensiveCoordinator => StaffPositionType.OffensiveCoordinator,
                CoachPosition.DefensiveCoordinator => StaffPositionType.DefensiveCoordinator,
                _ => StaffPositionType.AssistantCoach
            };

            return new StaffNegotiationSession
            {
                NegotiationId = $"NEG_{Guid.NewGuid().ToString().Substring(0, 8)}",
                TeamId = teamId,
                CandidateId = coach.CoachId,
                CandidateName = coach.FullName,
                IsCoach = true,
                Position = positionType,
                Status = StaffNegotiationStatus.InProgress,
                RoundNumber = 1,
                MaxRounds = 3 + rng.Next(3),  // 3-5 rounds
                AskingPrice = askingPrice,
                MinimumAcceptable = minAcceptable,
                PreferredYears = 3 + rng.Next(3)  // 3-5 years
            };
        }

        /// <summary>
        /// Create a negotiation session for a scout.
        /// </summary>
        public static StaffNegotiationSession CreateForScout(string teamId, Scout scout)
        {
            var rng = new System.Random();

            int askingPrice = (int)(scout.MarketValue * (1.1f + rng.NextDouble() * 0.2f));
            int minAcceptable = (int)(scout.MarketValue * 0.85f);

            return new StaffNegotiationSession
            {
                NegotiationId = $"NEG_{Guid.NewGuid().ToString().Substring(0, 8)}",
                TeamId = teamId,
                CandidateId = scout.ScoutId,
                CandidateName = scout.FullName,
                IsCoach = false,
                Position = StaffPositionType.Scout,
                Status = StaffNegotiationStatus.InProgress,
                RoundNumber = 1,
                MaxRounds = 3 + rng.Next(2),  // 3-4 rounds
                AskingPrice = askingPrice,
                MinimumAcceptable = minAcceptable,
                PreferredYears = 2 + rng.Next(2)  // 2-3 years
            };
        }

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

    /// <summary>
    /// Save data for staff hiring system.
    /// </summary>
    [Serializable]
    public class StaffHiringSaveData
    {
        public List<Coach> FreeAgentCoaches;
        public List<Scout> FreeAgentScouts;
        public List<StaffNegotiationSession> ActiveNegotiations;
    }
}
