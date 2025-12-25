using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Central registry for tracking draft pick ownership across all teams.
    /// Handles pick transfers, Stepien Rule validation, and pick projections.
    /// </summary>
    public class DraftPickRegistry
    {
        // Key format: "{OriginalTeamId}_{Year}_{Round}" e.g., "LAL_2025_1"
        private Dictionary<string, DraftPick> _allPicks = new Dictionary<string, DraftPick>();

        // Track pick transfer history
        private List<PickTransferRecord> _transferHistory = new List<PickTransferRecord>();

        // Team standings cache for pick projection (updated periodically)
        private Dictionary<string, int> _teamStandingsCache = new Dictionary<string, int>();

        // Events
        public event Action<DraftPick, string, string> OnPickTransferred; // pick, fromTeam, toTeam
        public event Action<DraftPick> OnPickConveyed;

        // How many years of future picks to track
        public const int FUTURE_YEARS_TRACKED = 7;

        /// <summary>
        /// Initialize registry with all teams' picks for the specified number of seasons.
        /// </summary>
        public void InitializeForSeason(int currentYear, List<Team> teams)
        {
            _allPicks.Clear();

            foreach (var team in teams)
            {
                // Create picks for current year through FUTURE_YEARS_TRACKED
                for (int year = currentYear; year <= currentYear + FUTURE_YEARS_TRACKED; year++)
                {
                    // First round pick
                    var firstRound = DraftPick.CreateFirstRound(year, team.TeamId, team.TeamId);
                    string key1 = GetPickKey(team.TeamId, year, 1);
                    _allPicks[key1] = firstRound;

                    // Second round pick
                    var secondRound = DraftPick.CreateSecondRound(year, team.TeamId, team.TeamId);
                    string key2 = GetPickKey(team.TeamId, year, 2);
                    _allPicks[key2] = secondRound;
                }
            }

            // Load real-world draft pick trades (Dec 2025 data)
            LoadInitialDraftData();

            Debug.Log($"[DraftPickRegistry] Initialized with {_allPicks.Count} picks for {teams.Count} teams");
        }

        /// <summary>
        /// Load initial draft pick ownership data from JSON resource.
        /// Applies real-world traded picks, protections, and swap rights.
        /// </summary>
        private void LoadInitialDraftData()
        {
            var jsonAsset = Resources.Load<TextAsset>("Data/initial_draft_picks");
            if (jsonAsset == null)
            {
                Debug.Log("[DraftPickRegistry] No initial draft pick data found - using default ownership");
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<InitialDraftPickData>(jsonAsset.text);
                if (data == null)
                {
                    Debug.LogWarning("[DraftPickRegistry] Failed to parse initial draft pick data");
                    return;
                }

                int tradedCount = 0;
                int swapCount = 0;

                // Apply traded picks
                if (data.tradedPicks != null)
                {
                    foreach (var traded in data.tradedPicks)
                    {
                        var pick = GetPick(traded.originalTeamId, traded.year, traded.round);
                        if (pick != null)
                        {
                            // Update ownership
                            pick.CurrentOwnerId = traded.currentOwnerId;

                            // Apply protections
                            if (traded.protections != null)
                            {
                                pick.Protections.Clear();
                                foreach (var protection in traded.protections)
                                {
                                    pick.Protections.Add(protection.ToDraftProtection());
                                }
                            }

                            // Apply final conveyance type
                            pick.FinalConveyance = InitialDraftPickDataExtensions.ParseConveyanceType(traded.finalConveyance);

                            tradedCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"[DraftPickRegistry] Initial data: Pick not found - {traded.originalTeamId} {traded.year} Rd{traded.round}");
                        }
                    }
                }

                // Apply swap rights
                if (data.swapRights != null)
                {
                    foreach (var swap in data.swapRights)
                    {
                        var pick = GetPick(swap.originalTeamId, swap.year, 1);
                        if (pick != null)
                        {
                            pick.IsSwapRight = true;
                            pick.SwapBeneficiaryTeamId = swap.beneficiaryTeamId;
                            swapCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"[DraftPickRegistry] Initial data: Pick not found for swap - {swap.originalTeamId} {swap.year}");
                        }
                    }
                }

                Debug.Log($"[DraftPickRegistry] Loaded initial data: {tradedCount} traded picks, {swapCount} swap rights (as of {data.dataAsOf})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DraftPickRegistry] Error loading initial draft data: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all picks currently owned by a team.
        /// </summary>
        public List<DraftPick> GetPicksOwnedBy(string teamId)
        {
            return _allPicks.Values
                .Where(p => p.CurrentOwnerId == teamId)
                .OrderBy(p => p.Year)
                .ThenBy(p => p.Round)
                .ToList();
        }

        /// <summary>
        /// Get all picks for a specific draft year.
        /// </summary>
        public List<DraftPick> GetPicksForYear(int year)
        {
            return _allPicks.Values
                .Where(p => p.Year == year)
                .OrderBy(p => p.Round)
                .ThenBy(p => p.OriginalTeamId)
                .ToList();
        }

        /// <summary>
        /// Get a specific pick.
        /// </summary>
        public DraftPick GetPick(string originalTeamId, int year, int round)
        {
            string key = GetPickKey(originalTeamId, year, round);
            _allPicks.TryGetValue(key, out var pick);
            return pick;
        }

        /// <summary>
        /// Transfer ownership of a pick from one team to another.
        /// </summary>
        public bool TransferPick(string originalTeamId, int year, int round, string fromTeamId, string toTeamId)
        {
            var pick = GetPick(originalTeamId, year, round);

            if (pick == null)
            {
                Debug.LogWarning($"[DraftPickRegistry] Pick not found: {originalTeamId} {year} Rd{round}");
                return false;
            }

            if (pick.CurrentOwnerId != fromTeamId)
            {
                Debug.LogWarning($"[DraftPickRegistry] Pick owner mismatch: {pick.CurrentOwnerId} != {fromTeamId}");
                return false;
            }

            // Record the transfer
            var record = new PickTransferRecord
            {
                OriginalTeamId = originalTeamId,
                Year = year,
                Round = round,
                FromTeamId = fromTeamId,
                ToTeamId = toTeamId,
                TransferDate = GameManager.Instance?.CurrentDate ?? DateTime.Now
            };
            _transferHistory.Add(record);

            // Update ownership
            pick.CurrentOwnerId = toTeamId;

            Debug.Log($"[DraftPickRegistry] Transferred {originalTeamId} {year} Rd{round} pick: {fromTeamId} -> {toTeamId}");
            OnPickTransferred?.Invoke(pick, fromTeamId, toTeamId);

            return true;
        }

        /// <summary>
        /// Add protections to a pick during trade.
        /// </summary>
        public void AddProtections(string originalTeamId, int year, int round, List<DraftProtection> protections)
        {
            var pick = GetPick(originalTeamId, year, round);
            if (pick != null)
            {
                pick.Protections.AddRange(protections);
            }
        }

        /// <summary>
        /// Create a swap right for a pick.
        /// </summary>
        public void CreateSwapRight(string originalTeamId, int year, string beneficiaryTeamId)
        {
            var pick = GetPick(originalTeamId, year, 1);
            if (pick != null)
            {
                pick.IsSwapRight = true;
                pick.SwapBeneficiaryTeamId = beneficiaryTeamId;
            }
        }

        // === STEPIEN RULE VALIDATION ===

        /// <summary>
        /// Validate Stepien Rule: Teams must have first round pick in every other year.
        /// Returns true if proposed outgoing picks would NOT violate Stepien Rule.
        /// </summary>
        public bool ValidateStepienRule(string teamId, List<DraftPick> proposedOutgoing)
        {
            // Get current picks owned by team
            var currentPicks = GetPicksOwnedBy(teamId);
            var currentFirstRounds = currentPicks
                .Where(p => p.Round == 1)
                .Select(p => p.Year)
                .ToHashSet();

            // Simulate removal of proposed outgoing picks
            foreach (var outgoing in proposedOutgoing)
            {
                if (outgoing.Round == 1)
                {
                    currentFirstRounds.Remove(outgoing.Year);
                }
            }

            // Check for consecutive years without first round picks
            var years = currentFirstRounds.OrderBy(y => y).ToList();

            for (int i = 0; i < years.Count - 1; i++)
            {
                // If there's a gap of 2+ years, that's a violation
                // (e.g., having 2025 and 2028 means missing both 2026 AND 2027)
                if (years[i + 1] - years[i] > 2)
                {
                    Debug.LogWarning($"[DraftPickRegistry] Stepien violation: Gap from {years[i]} to {years[i + 1]}");
                    return false;
                }
            }

            // Also check the ends
            int currentYear = GameManager.Instance?.CurrentDate.Year ?? DateTime.Now.Year;

            // Must have a first round pick within next 7 years at alternating intervals
            for (int y = currentYear; y <= currentYear + FUTURE_YEARS_TRACKED; y += 2)
            {
                bool hasPickInRange = currentFirstRounds.Any(pick => pick >= y && pick <= y + 1);
                if (!hasPickInRange && y <= currentYear + 4) // Only strict for near-term
                {
                    Debug.LogWarning($"[DraftPickRegistry] Stepien violation: No first rounder available around {y}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get detailed Stepien compliance status for a team.
        /// </summary>
        public StepienStatus GetStepienStatus(string teamId)
        {
            var status = new StepienStatus { TeamId = teamId };
            var picks = GetPicksOwnedBy(teamId);
            var firstRounds = picks.Where(p => p.Round == 1).ToList();

            int currentYear = GameManager.Instance?.CurrentDate.Year ?? DateTime.Now.Year;

            for (int year = currentYear; year <= currentYear + FUTURE_YEARS_TRACKED; year++)
            {
                var pick = firstRounds.FirstOrDefault(p => p.Year == year);
                if (pick != null)
                {
                    status.FirstRoundPicksByYear[year] = pick.OriginalTeamId;
                }
                else
                {
                    status.FirstRoundPicksByYear[year] = null; // No pick this year
                }
            }

            // Determine which picks are tradeable
            status.TradeableFirstRounds = new List<int>();
            var yearsList = status.FirstRoundPicksByYear
                .Where(kvp => kvp.Value != null)
                .Select(kvp => kvp.Key)
                .OrderBy(y => y)
                .ToList();

            for (int i = 0; i < yearsList.Count; i++)
            {
                int year = yearsList[i];

                // Can trade if either previous or next year has a pick
                bool hasPrevious = i > 0 && yearsList[i - 1] == year - 1;
                bool hasNext = i < yearsList.Count - 1 && yearsList[i + 1] == year + 1;

                // Can only trade if it won't create consecutive missing years
                if (hasPrevious || hasNext || yearsList.Count > (FUTURE_YEARS_TRACKED / 2))
                {
                    status.TradeableFirstRounds.Add(year);
                }
            }

            return status;
        }

        // === PICK PROJECTION ===

        /// <summary>
        /// Update team standings for pick projection.
        /// </summary>
        public void UpdateStandings(Dictionary<string, int> standings)
        {
            _teamStandingsCache = new Dictionary<string, int>(standings);
        }

        /// <summary>
        /// Get projected draft position for a team's pick.
        /// </summary>
        public int GetProjectedPosition(string originalTeamId, int year)
        {
            // For current year, use standings if available
            int currentYear = GameManager.Instance?.CurrentDate.Year ?? DateTime.Now.Year;

            if (year == currentYear && _teamStandingsCache.TryGetValue(originalTeamId, out int standing))
            {
                // Inverse standings (worst team = best pick)
                return 31 - standing; // Assumes 30 teams
            }

            // Future years: estimate based on current standings with regression to mean
            if (_teamStandingsCache.TryGetValue(originalTeamId, out int currentStanding))
            {
                int yearsAway = year - currentYear;
                // Regress toward mean (15) by 20% per year
                float regression = Mathf.Pow(0.8f, yearsAway);
                float projected = 15f + (31 - currentStanding - 15f) * regression;
                return Mathf.RoundToInt(Mathf.Clamp(projected, 1, 30));
            }

            return 15; // Default to middle
        }

        /// <summary>
        /// Get pick value tier based on projected position.
        /// </summary>
        public PickValueTier GetPickValueTier(DraftPick pick)
        {
            int projected = GetProjectedPosition(pick.OriginalTeamId, pick.Year);

            if (pick.Round == 2)
                return PickValueTier.SecondRound;

            if (pick.IsProtected)
            {
                // Check if likely to convey
                foreach (var protection in pick.Protections)
                {
                    if (protection.Year == pick.Year && protection.IsProtected(projected))
                    {
                        return PickValueTier.LikelyProtected;
                    }
                }
            }

            return projected switch
            {
                <= 5 => PickValueTier.Top5,
                <= 10 => PickValueTier.Top10,
                <= 14 => PickValueTier.Lottery,
                <= 20 => PickValueTier.MidFirstRound,
                _ => PickValueTier.LateFirstRound
            };
        }

        // === SAVE/LOAD ===

        /// <summary>
        /// Create save data for serialization.
        /// </summary>
        public DraftPickRegistrySaveData CreateSaveData()
        {
            return new DraftPickRegistrySaveData
            {
                AllPicks = _allPicks.Values.ToList(),
                TransferHistory = new List<PickTransferRecord>(_transferHistory)
            };
        }

        /// <summary>
        /// Restore from save data.
        /// </summary>
        public void RestoreFromSave(DraftPickRegistrySaveData data)
        {
            if (data == null) return;

            _allPicks.Clear();
            if (data.AllPicks != null)
            {
                foreach (var pick in data.AllPicks)
                {
                    string key = GetPickKey(pick.OriginalTeamId, pick.Year, pick.Round);
                    _allPicks[key] = pick;
                }
            }

            _transferHistory = data.TransferHistory ?? new List<PickTransferRecord>();

            Debug.Log($"[DraftPickRegistry] Restored {_allPicks.Count} picks from save");
        }

        // === UTILITY ===

        private string GetPickKey(string originalTeamId, int year, int round)
        {
            return $"{originalTeamId}_{year}_{round}";
        }

        /// <summary>
        /// Get count of picks owned by each team.
        /// </summary>
        public Dictionary<string, int> GetPickCountByTeam()
        {
            return _allPicks.Values
                .GroupBy(p => p.CurrentOwnerId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// Get all picks a team has traded away.
        /// </summary>
        public List<DraftPick> GetTradedAwayPicks(string teamId)
        {
            return _allPicks.Values
                .Where(p => p.OriginalTeamId == teamId && p.CurrentOwnerId != teamId)
                .ToList();
        }

        /// <summary>
        /// Get all picks a team has acquired from other teams.
        /// </summary>
        public List<DraftPick> GetAcquiredPicks(string teamId)
        {
            return _allPicks.Values
                .Where(p => p.CurrentOwnerId == teamId && p.OriginalTeamId != teamId)
                .ToList();
        }

        /// <summary>
        /// Process draft completion - remove used picks, advance remaining.
        /// </summary>
        public void ProcessDraftCompletion(int draftYear)
        {
            // Remove picks from the completed draft
            var keysToRemove = _allPicks
                .Where(kvp => kvp.Value.Year == draftYear)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _allPicks.Remove(key);
            }

            Debug.Log($"[DraftPickRegistry] Removed {keysToRemove.Count} picks from {draftYear} draft");
        }
    }

    // === SUPPORTING TYPES ===

    /// <summary>
    /// Record of a pick transfer for history tracking.
    /// </summary>
    [Serializable]
    public class PickTransferRecord
    {
        public string OriginalTeamId;
        public int Year;
        public int Round;
        public string FromTeamId;
        public string ToTeamId;
        public DateTime TransferDate;
        public string TradeId; // Reference to the trade this was part of
    }

    /// <summary>
    /// Pick value tier for trade evaluation.
    /// </summary>
    public enum PickValueTier
    {
        Top5,
        Top10,
        Lottery,
        MidFirstRound,
        LateFirstRound,
        SecondRound,
        LikelyProtected
    }

    /// <summary>
    /// Stepien Rule compliance status for a team.
    /// </summary>
    public class StepienStatus
    {
        public string TeamId;
        public Dictionary<int, string> FirstRoundPicksByYear = new Dictionary<int, string>(); // Year -> OriginalTeamId or null
        public List<int> TradeableFirstRounds = new List<int>(); // Years where first round can be traded
        public bool IsInCompliance => TradeableFirstRounds.Count > 0;
    }

    /// <summary>
    /// Save data for draft pick registry.
    /// </summary>
    [Serializable]
    public class DraftPickRegistrySaveData
    {
        public List<DraftPick> AllPicks;
        public List<PickTransferRecord> TransferHistory;
    }
}
