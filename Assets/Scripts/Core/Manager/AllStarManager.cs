using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Voting source breakdown for All-Star selection
    /// </summary>
    [Serializable]
    public enum VotingSource
    {
        Fan,        // 50% of starter vote
        Player,     // 25% of starter vote
        Media       // 25% of starter vote
    }

    /// <summary>
    /// All-Star roster positions
    /// </summary>
    [Serializable]
    public enum AllStarPosition
    {
        Frontcourt,
        Backcourt,
        WildCard    // Reserves can be any position
    }

    /// <summary>
    /// Types of All-Star events
    /// </summary>
    [Serializable]
    public enum AllStarEventType
    {
        RisingStar,         // Rookie/Sophomore game
        SkillsChallenge,
        ThreePointContest,
        DunkContest,
        AllStarGame,
        CelebrityGame       // Optional
    }

    /// <summary>
    /// Individual voting record for a player
    /// </summary>
    [Serializable]
    public class AllStarVote
    {
        public string PlayerId;
        public string PlayerName;
        public string TeamId;
        public AllStarPosition Position;
        public string Conference;

        [Header("Vote Counts")]
        public int FanVotes;
        public int PlayerVotes;
        public int MediaVotes;

        [Header("Weighted Score")]
        public float WeightedScore;     // Fan 50% + Player 25% + Media 25%
        public int OverallRank;
        public int ConferenceRank;
        public int PositionRank;

        [Header("Selection Status")]
        public bool IsStarter;
        public bool IsReserve;
        public bool IsInjuryReplacement;
        public string ReplacingPlayerId;

        public void CalculateWeightedScore(int maxFanVotes, int maxPlayerVotes, int maxMediaVotes)
        {
            // Normalize votes to 0-100 scale, then weight
            float fanNormalized = maxFanVotes > 0 ? (FanVotes / (float)maxFanVotes) * 100f : 0f;
            float playerNormalized = maxPlayerVotes > 0 ? (PlayerVotes / (float)maxPlayerVotes) * 100f : 0f;
            float mediaNormalized = maxMediaVotes > 0 ? (MediaVotes / (float)maxMediaVotes) * 100f : 0f;

            // Fan 50%, Player 25%, Media 25%
            WeightedScore = (fanNormalized * 0.5f) + (playerNormalized * 0.25f) + (mediaNormalized * 0.25f);
        }
    }

    /// <summary>
    /// Tracks voting returns and updates throughout the voting period
    /// </summary>
    [Serializable]
    public class VotingReturn
    {
        public int ReturnNumber;        // 1st, 2nd, 3rd, etc.
        public DateTime ReleaseDate;
        public List<AllStarVote> TopFanVotes = new List<AllStarVote>();
        public string Headline;
        public List<string> NotableChanges = new List<string>();
    }

    /// <summary>
    /// All-Star Weekend event result
    /// </summary>
    [Serializable]
    public class AllStarEventResult
    {
        public AllStarEventType EventType;
        public string WinnerId;
        public string WinnerName;
        public List<string> ParticipantIds = new List<string>();
        public List<string> FinalRoundIds = new List<string>();
        public Dictionary<string, int> Scores = new Dictionary<string, int>();
        public List<string> Highlights = new List<string>();
    }

    /// <summary>
    /// All-Star Game result
    /// </summary>
    [Serializable]
    public class AllStarGameResult
    {
        public int EastScore;
        public int WestScore;
        public string WinningConference;
        public string MvpId;
        public string MvpName;
        public int MvpPoints;
        public int MvpRebounds;
        public int MvpAssists;
        public List<string> GameHighlights = new List<string>();
        public Dictionary<string, PlayerAllStarStats> PlayerStats = new Dictionary<string, PlayerAllStarStats>();
    }

    /// <summary>
    /// Player stats from All-Star game
    /// </summary>
    [Serializable]
    public class PlayerAllStarStats
    {
        public string PlayerId;
        public int Minutes;
        public int Points;
        public int Rebounds;
        public int Assists;
        public int Steals;
        public int Blocks;
        public int ThreePointersMade;
        public bool WasDunkContestMoment;
    }

    /// <summary>
    /// All-Star Weekend complete summary
    /// </summary>
    [Serializable]
    public class AllStarWeekendSummary
    {
        public int Season;
        public string HostCity;
        public string HostArena;

        [Header("Rosters")]
        public List<AllStarVote> EastStarters = new List<AllStarVote>();
        public List<AllStarVote> EastReserves = new List<AllStarVote>();
        public List<AllStarVote> WestStarters = new List<AllStarVote>();
        public List<AllStarVote> WestReserves = new List<AllStarVote>();

        [Header("Events")]
        public AllStarEventResult RisingStarsResult;
        public AllStarEventResult SkillsChallengeResult;
        public AllStarEventResult ThreePointContestResult;
        public AllStarEventResult DunkContestResult;
        public AllStarGameResult AllStarGameResult;

        [Header("Snubs & Controversies")]
        public List<string> NotableSnubs = new List<string>();
        public List<string> ControversialSelections = new List<string>();

        public List<AllStarVote> GetAllSelected()
        {
            var all = new List<AllStarVote>();
            all.AddRange(EastStarters);
            all.AddRange(EastReserves);
            all.AddRange(WestStarters);
            all.AddRange(WestReserves);
            return all;
        }
    }

    /// <summary>
    /// Manages All-Star voting, selection, and weekend events
    /// Fan votes (50%), Player votes (25%), Media votes (25%)
    /// </summary>
    public class AllStarManager : MonoBehaviour
    {
        public static AllStarManager Instance { get; private set; }

        [Header("Current Season Data")]
        [SerializeField] private int currentSeason;
        [SerializeField] private bool votingOpen;
        [SerializeField] private DateTime votingStartDate;
        [SerializeField] private DateTime votingEndDate;

        [Header("Vote Tracking")]
        [SerializeField] private List<AllStarVote> allVotes = new List<AllStarVote>();
        [SerializeField] private List<VotingReturn> votingReturns = new List<VotingReturn>();
        [SerializeField] private int currentReturnNumber;

        [Header("Historical Data")]
        [SerializeField] private List<AllStarWeekendSummary> historicalWeekends = new List<AllStarWeekendSummary>();

        [Header("Configuration")]
        [SerializeField] private int numberOfVotingReturns = 3;
        [SerializeField] private int startersPerConference = 5;     // 2 backcourt, 3 frontcourt
        [SerializeField] private int reservesPerConference = 7;

        // Events
        public event Action<VotingReturn> OnVotingReturnReleased;
        public event Action<AllStarWeekendSummary> OnRosterFinalized;
        public event Action<AllStarEventResult> OnEventCompleted;
        public event Action<AllStarGameResult> OnAllStarGameCompleted;

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

        /// <summary>
        /// Start the All-Star voting period
        /// </summary>
        public void StartVotingPeriod(int season, DateTime startDate, DateTime endDate)
        {
            currentSeason = season;
            votingStartDate = startDate;
            votingEndDate = endDate;
            votingOpen = true;
            currentReturnNumber = 0;

            allVotes.Clear();
            votingReturns.Clear();

            InitializeVotingForAllPlayers();

            Debug.Log($"All-Star voting opened for {season} season. Ends {endDate:MMM dd}");
        }

        /// <summary>
        /// Initialize voting records for all eligible players
        /// </summary>
        private void InitializeVotingForAllPlayers()
        {
            // This would integrate with PlayerManager to get all active players
            // For now, creates placeholder structure
        }

        /// <summary>
        /// Simulate fan voting based on player popularity, performance, and market size
        /// </summary>
        public void SimulateFanVoting(List<PlayerSeasonStats> playerStats, Dictionary<string, float> playerPopularity)
        {
            foreach (var vote in allVotes)
            {
                var stats = playerStats.FirstOrDefault(s => s.PlayerId == vote.PlayerId);
                float popularity = playerPopularity.ContainsKey(vote.PlayerId) ? playerPopularity[vote.PlayerId] : 0.5f;

                // Fan votes heavily influenced by:
                // 1. Player popularity/name recognition (40%)
                // 2. Team market size (25%)
                // 3. Actual performance (20%)
                // 4. Social media presence (15%)

                float performanceScore = CalculatePerformanceScore(stats);
                float marketMultiplier = GetMarketSizeMultiplier(vote.TeamId);
                float socialMediaBonus = popularity * 0.3f;

                float baseVotes = 100000f;
                float fanVoteScore = (popularity * 0.4f) + (marketMultiplier * 0.25f) +
                                    (performanceScore * 0.2f) + (socialMediaBonus * 0.15f);

                vote.FanVotes = Mathf.RoundToInt(baseVotes * fanVoteScore * UnityEngine.Random.Range(0.9f, 1.1f));
            }
        }

        /// <summary>
        /// Simulate player voting - peers recognize actual skill
        /// </summary>
        public void SimulatePlayerVoting(List<PlayerSeasonStats> playerStats)
        {
            foreach (var vote in allVotes)
            {
                var stats = playerStats.FirstOrDefault(s => s.PlayerId == vote.PlayerId);

                // Player votes influenced by:
                // 1. Actual performance/skill (60%)
                // 2. Reputation among peers (25%)
                // 3. Veteran status/respect (15%)

                float performanceScore = CalculatePerformanceScore(stats);
                float reputationScore = CalculateReputationScore(vote.PlayerId);
                float veteranBonus = GetVeteranRespectBonus(vote.PlayerId);

                float playerVoteScore = (performanceScore * 0.6f) + (reputationScore * 0.25f) + (veteranBonus * 0.15f);

                vote.PlayerVotes = Mathf.RoundToInt(500 * playerVoteScore * UnityEngine.Random.Range(0.85f, 1.15f));
            }
        }

        /// <summary>
        /// Simulate media voting - analytics and narrative focused
        /// </summary>
        public void SimulateMediaVoting(List<PlayerSeasonStats> playerStats, Dictionary<string, float> advancedStats)
        {
            foreach (var vote in allVotes)
            {
                var stats = playerStats.FirstOrDefault(s => s.PlayerId == vote.PlayerId);
                float advanced = advancedStats.ContainsKey(vote.PlayerId) ? advancedStats[vote.PlayerId] : 0.5f;

                // Media votes influenced by:
                // 1. Advanced stats/analytics (45%)
                // 2. Team record (25%)
                // 3. Narrative/storyline (20%)
                // 4. Traditional stats (10%)

                float performanceScore = CalculatePerformanceScore(stats);
                float teamRecordBonus = GetTeamRecordBonus(vote.TeamId);
                float narrativeBonus = CalculateNarrativeBonus(vote.PlayerId);

                float mediaVoteScore = (advanced * 0.45f) + (teamRecordBonus * 0.25f) +
                                      (narrativeBonus * 0.2f) + (performanceScore * 0.1f);

                vote.MediaVotes = Mathf.RoundToInt(100 * mediaVoteScore * UnityEngine.Random.Range(0.9f, 1.1f));
            }
        }

        /// <summary>
        /// Release a voting return (partial results)
        /// </summary>
        public VotingReturn ReleaseVotingReturn()
        {
            if (!votingOpen) return null;

            currentReturnNumber++;

            // Calculate weighted scores
            int maxFan = allVotes.Max(v => v.FanVotes);
            int maxPlayer = allVotes.Max(v => v.PlayerVotes);
            int maxMedia = allVotes.Max(v => v.MediaVotes);

            foreach (var vote in allVotes)
            {
                vote.CalculateWeightedScore(maxFan, maxPlayer, maxMedia);
            }

            // Rank by conference and position
            RankPlayers();

            var votingReturn = new VotingReturn
            {
                ReturnNumber = currentReturnNumber,
                ReleaseDate = DateTime.Now,
                TopFanVotes = allVotes.OrderByDescending(v => v.FanVotes).Take(10).ToList()
            };

            // Generate headline
            var topPlayer = votingReturn.TopFanVotes.FirstOrDefault();
            votingReturn.Headline = topPlayer != null
                ? $"{topPlayer.PlayerName} leads All-Star voting in {currentReturnNumber.ToOrdinal()} return"
                : $"All-Star voting {currentReturnNumber.ToOrdinal()} return released";

            // Track notable changes from last return
            if (votingReturns.Count > 0)
            {
                votingReturn.NotableChanges = DetectVotingChanges(votingReturns.Last(), votingReturn);
            }

            votingReturns.Add(votingReturn);
            OnVotingReturnReleased?.Invoke(votingReturn);

            return votingReturn;
        }

        /// <summary>
        /// Finalize All-Star starters based on combined voting
        /// </summary>
        public AllStarWeekendSummary FinalizeStarters()
        {
            votingOpen = false;

            // Calculate final weighted scores
            int maxFan = allVotes.Max(v => v.FanVotes);
            int maxPlayer = allVotes.Max(v => v.PlayerVotes);
            int maxMedia = allVotes.Max(v => v.MediaVotes);

            foreach (var vote in allVotes)
            {
                vote.CalculateWeightedScore(maxFan, maxPlayer, maxMedia);
            }

            RankPlayers();

            var summary = new AllStarWeekendSummary
            {
                Season = currentSeason,
                HostCity = SelectHostCity(),
                HostArena = GetHostArena()
            };

            // Select starters - Top 2 backcourt and Top 3 frontcourt by weighted vote
            var eastPlayers = allVotes.Where(v => v.Conference == "East").ToList();
            var westPlayers = allVotes.Where(v => v.Conference == "West").ToList();

            // East Starters
            var eastBackcourt = eastPlayers.Where(v => v.Position == AllStarPosition.Backcourt)
                                          .OrderByDescending(v => v.WeightedScore).Take(2).ToList();
            var eastFrontcourt = eastPlayers.Where(v => v.Position == AllStarPosition.Frontcourt)
                                           .OrderByDescending(v => v.WeightedScore).Take(3).ToList();

            foreach (var starter in eastBackcourt.Concat(eastFrontcourt))
            {
                starter.IsStarter = true;
                summary.EastStarters.Add(starter);
            }

            // West Starters
            var westBackcourt = westPlayers.Where(v => v.Position == AllStarPosition.Backcourt)
                                          .OrderByDescending(v => v.WeightedScore).Take(2).ToList();
            var westFrontcourt = westPlayers.Where(v => v.Position == AllStarPosition.Frontcourt)
                                           .OrderByDescending(v => v.WeightedScore).Take(3).ToList();

            foreach (var starter in westBackcourt.Concat(westFrontcourt))
            {
                starter.IsStarter = true;
                summary.WestStarters.Add(starter);
            }

            // Identify snubs
            summary.NotableSnubs = IdentifySnubs(summary);

            OnRosterFinalized?.Invoke(summary);
            return summary;
        }

        /// <summary>
        /// Select reserves (chosen by coaches)
        /// </summary>
        public void SelectReserves(AllStarWeekendSummary summary)
        {
            // Coaches select reserves - more merit-based
            var eastPlayers = allVotes.Where(v => v.Conference == "East" && !v.IsStarter).ToList();
            var westPlayers = allVotes.Where(v => v.Conference == "West" && !v.IsStarter).ToList();

            // Reserves need: 2 guards, 3 frontcourt, 2 wild cards
            SelectConferenceReserves(eastPlayers, summary.EastReserves);
            SelectConferenceReserves(westPlayers, summary.WestReserves);
        }

        private void SelectConferenceReserves(List<AllStarVote> available, List<AllStarVote> reserves)
        {
            // Get top performers regardless of position for coach selection
            var ranked = available.OrderByDescending(v => CalculateCoachSelectionScore(v)).ToList();

            // Must have positional balance
            int guards = 0, frontcourt = 0, wildcards = 0;

            foreach (var player in ranked)
            {
                if (reserves.Count >= reservesPerConference) break;

                bool canAdd = false;
                if (player.Position == AllStarPosition.Backcourt && guards < 2)
                {
                    guards++;
                    canAdd = true;
                }
                else if (player.Position == AllStarPosition.Frontcourt && frontcourt < 3)
                {
                    frontcourt++;
                    canAdd = true;
                }
                else if (wildcards < 2)
                {
                    wildcards++;
                    canAdd = true;
                }

                if (canAdd)
                {
                    player.IsReserve = true;
                    reserves.Add(player);
                }
            }
        }

        /// <summary>
        /// Handle injury replacement
        /// </summary>
        public AllStarVote SelectInjuryReplacement(AllStarVote injuredPlayer, string conference)
        {
            var available = allVotes.Where(v =>
                v.Conference == conference &&
                !v.IsStarter &&
                !v.IsReserve &&
                v.Position == injuredPlayer.Position).ToList();

            if (available.Count == 0)
            {
                // Allow any position if none available at same position
                available = allVotes.Where(v =>
                    v.Conference == conference &&
                    !v.IsStarter &&
                    !v.IsReserve).ToList();
            }

            var replacement = available.OrderByDescending(v => CalculateCoachSelectionScore(v)).FirstOrDefault();

            if (replacement != null)
            {
                replacement.IsInjuryReplacement = true;
                replacement.ReplacingPlayerId = injuredPlayer.PlayerId;
                replacement.IsReserve = injuredPlayer.IsReserve;
                if (injuredPlayer.IsStarter)
                {
                    replacement.IsStarter = true;
                }
            }

            return replacement;
        }

        /// <summary>
        /// Simulate the Three-Point Contest
        /// </summary>
        public AllStarEventResult SimulateThreePointContest(List<string> participantIds)
        {
            var result = new AllStarEventResult
            {
                EventType = AllStarEventType.ThreePointContest,
                ParticipantIds = participantIds
            };

            // First round - all participants
            var firstRoundScores = new Dictionary<string, int>();
            foreach (var playerId in participantIds)
            {
                int score = SimulateThreePointRound(playerId);
                firstRoundScores[playerId] = score;
                result.Scores[playerId + "_R1"] = score;
            }

            // Top 3 advance to finals
            var finalists = firstRoundScores.OrderByDescending(kvp => kvp.Value)
                                           .Take(3)
                                           .Select(kvp => kvp.Key)
                                           .ToList();
            result.FinalRoundIds = finalists;

            // Final round
            var finalScores = new Dictionary<string, int>();
            foreach (var playerId in finalists)
            {
                int score = SimulateThreePointRound(playerId);
                finalScores[playerId] = score;
                result.Scores[playerId + "_Final"] = score;
            }

            var winner = finalScores.OrderByDescending(kvp => kvp.Value).First();
            result.WinnerId = winner.Key;
            result.WinnerName = GetPlayerName(winner.Key);

            // Generate highlights
            if (winner.Value >= 28)
            {
                result.Highlights.Add($"{result.WinnerName} puts on a shooting clinic with {winner.Value} points!");
            }
            if (finalScores.Values.Max() - finalScores.Values.Min() <= 2)
            {
                result.Highlights.Add("Nail-biter finish with just 2 points separating the finalists!");
            }

            OnEventCompleted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Simulate the Slam Dunk Contest
        /// </summary>
        public AllStarEventResult SimulateDunkContest(List<string> participantIds)
        {
            var result = new AllStarEventResult
            {
                EventType = AllStarEventType.DunkContest,
                ParticipantIds = participantIds
            };

            // First round - 2 dunks each
            var firstRoundScores = new Dictionary<string, int>();
            foreach (var playerId in participantIds)
            {
                int dunk1 = SimulateDunk(playerId);
                int dunk2 = SimulateDunk(playerId);
                firstRoundScores[playerId] = dunk1 + dunk2;
                result.Scores[playerId + "_R1D1"] = dunk1;
                result.Scores[playerId + "_R1D2"] = dunk2;
            }

            // Top 2 advance to finals
            var finalists = firstRoundScores.OrderByDescending(kvp => kvp.Value)
                                           .Take(2)
                                           .Select(kvp => kvp.Key)
                                           .ToList();
            result.FinalRoundIds = finalists;

            // Final round - 2 dunks each
            var finalScores = new Dictionary<string, int>();
            foreach (var playerId in finalists)
            {
                int dunk1 = SimulateDunk(playerId, isFinal: true);
                int dunk2 = SimulateDunk(playerId, isFinal: true);
                finalScores[playerId] = dunk1 + dunk2;
                result.Scores[playerId + "_FinalD1"] = dunk1;
                result.Scores[playerId + "_FinalD2"] = dunk2;
            }

            var winner = finalScores.OrderByDescending(kvp => kvp.Value).First();
            result.WinnerId = winner.Key;
            result.WinnerName = GetPlayerName(winner.Key);

            // Generate highlights
            foreach (var kvp in result.Scores)
            {
                if (kvp.Value == 50)
                {
                    string playerId = kvp.Key.Split('_')[0];
                    result.Highlights.Add($"Perfect 50 from {GetPlayerName(playerId)}! The crowd goes wild!");
                }
            }

            OnEventCompleted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Simulate the All-Star Game
        /// </summary>
        public AllStarGameResult SimulateAllStarGame(AllStarWeekendSummary weekend)
        {
            var result = new AllStarGameResult();

            // All-Star games are high-scoring
            int baseScore = UnityEngine.Random.Range(150, 180);
            int scoreDiff = UnityEngine.Random.Range(-15, 16);

            result.EastScore = baseScore + (scoreDiff > 0 ? scoreDiff : 0);
            result.WestScore = baseScore + (scoreDiff < 0 ? -scoreDiff : 0);
            result.WinningConference = result.EastScore > result.WestScore ? "East" : "West";

            // Simulate individual performances
            var allPlayers = weekend.GetAllSelected();
            foreach (var player in allPlayers)
            {
                result.PlayerStats[player.PlayerId] = SimulateAllStarPlayerStats(player);
            }

            // Select MVP - highest performer from winning team
            var winningTeamPlayers = result.WinningConference == "East"
                ? weekend.EastStarters.Concat(weekend.EastReserves)
                : weekend.WestStarters.Concat(weekend.WestReserves);

            var mvpCandidate = winningTeamPlayers
                .OrderByDescending(p => CalculateMvpScore(result.PlayerStats[p.PlayerId]))
                .First();

            result.MvpId = mvpCandidate.PlayerId;
            result.MvpName = mvpCandidate.PlayerName;
            var mvpStats = result.PlayerStats[result.MvpId];
            result.MvpPoints = mvpStats.Points;
            result.MvpRebounds = mvpStats.Rebounds;
            result.MvpAssists = mvpStats.Assists;

            // Generate highlights
            result.GameHighlights.Add($"{result.MvpName} wins MVP with {result.MvpPoints} points!");
            if (result.MvpPoints >= 40)
            {
                result.GameHighlights.Add($"Historic performance! {result.MvpName} erupts for {result.MvpPoints}!");
            }
            if (Math.Abs(result.EastScore - result.WestScore) <= 5)
            {
                result.GameHighlights.Add("Thrilling finish as the game comes down to the wire!");
            }

            // Check for dunk contest moments during game
            foreach (var stats in result.PlayerStats.Values.Where(s => s.WasDunkContestMoment))
            {
                result.GameHighlights.Add($"{GetPlayerName(stats.PlayerId)} throws down a poster dunk!");
            }

            OnAllStarGameCompleted?.Invoke(result);
            return result;
        }

        #region Helper Methods

        private float CalculatePerformanceScore(PlayerSeasonStats stats)
        {
            if (stats == null) return 0.5f;

            // Normalize stats to 0-1 scale
            float ptsScore = Mathf.Clamp01(stats.PointsPerGame / 35f);
            float rebScore = Mathf.Clamp01(stats.ReboundsPerGame / 15f);
            float astScore = Mathf.Clamp01(stats.AssistsPerGame / 12f);

            return (ptsScore * 0.5f) + (rebScore * 0.25f) + (astScore * 0.25f);
        }

        private float GetMarketSizeMultiplier(string teamId)
        {
            // Large markets get voting boost
            var largeMarkets = new[] { "LAL", "NYK", "GSW", "CHI", "BOS", "MIA" };
            var mediumMarkets = new[] { "DAL", "HOU", "PHI", "TOR", "BKN", "PHX" };

            if (largeMarkets.Contains(teamId)) return 1.0f;
            if (mediumMarkets.Contains(teamId)) return 0.8f;
            return 0.6f;
        }

        private float CalculateReputationScore(string playerId)
        {
            // Would integrate with player reputation system
            return UnityEngine.Random.Range(0.4f, 1.0f);
        }

        private float GetVeteranRespectBonus(string playerId)
        {
            // Veterans get respect bonus from peers
            return UnityEngine.Random.Range(0.3f, 0.8f);
        }

        private float GetTeamRecordBonus(string teamId)
        {
            // Would integrate with standings
            return UnityEngine.Random.Range(0.3f, 1.0f);
        }

        private float CalculateNarrativeBonus(string playerId)
        {
            // Storylines: comeback from injury, breakout season, etc.
            return UnityEngine.Random.Range(0.2f, 0.9f);
        }

        private float CalculateCoachSelectionScore(AllStarVote vote)
        {
            // Coaches weight performance more heavily
            return (vote.WeightedScore * 0.3f) +
                   (vote.PlayerVotes / 100f * 0.4f) +
                   (vote.MediaVotes / 10f * 0.3f);
        }

        private void RankPlayers()
        {
            // Overall rank
            var ranked = allVotes.OrderByDescending(v => v.WeightedScore).ToList();
            for (int i = 0; i < ranked.Count; i++)
            {
                ranked[i].OverallRank = i + 1;
            }

            // Conference rank
            foreach (var conference in new[] { "East", "West" })
            {
                var confRanked = allVotes.Where(v => v.Conference == conference)
                                        .OrderByDescending(v => v.WeightedScore).ToList();
                for (int i = 0; i < confRanked.Count; i++)
                {
                    confRanked[i].ConferenceRank = i + 1;
                }
            }

            // Position rank within conference
            foreach (var conference in new[] { "East", "West" })
            {
                foreach (AllStarPosition position in Enum.GetValues(typeof(AllStarPosition)))
                {
                    var posRanked = allVotes.Where(v => v.Conference == conference && v.Position == position)
                                           .OrderByDescending(v => v.WeightedScore).ToList();
                    for (int i = 0; i < posRanked.Count; i++)
                    {
                        posRanked[i].PositionRank = i + 1;
                    }
                }
            }
        }

        private List<string> DetectVotingChanges(VotingReturn previous, VotingReturn current)
        {
            var changes = new List<string>();

            // Check for position changes in top 10
            for (int i = 0; i < Math.Min(current.TopFanVotes.Count, 5); i++)
            {
                var player = current.TopFanVotes[i];
                var prevIndex = previous.TopFanVotes.FindIndex(v => v.PlayerId == player.PlayerId);

                if (prevIndex == -1)
                {
                    changes.Add($"{player.PlayerName} surges into top {i + 1}!");
                }
                else if (prevIndex > i + 2)
                {
                    changes.Add($"{player.PlayerName} jumps from #{prevIndex + 1} to #{i + 1}!");
                }
            }

            return changes;
        }

        private List<string> IdentifySnubs(AllStarWeekendSummary summary)
        {
            var snubs = new List<string>();

            // Players with high performance but not selected
            var selected = summary.GetAllSelected().Select(s => s.PlayerId).ToHashSet();
            var highPerformers = allVotes.Where(v =>
                !selected.Contains(v.PlayerId) &&
                v.WeightedScore > 60).ToList();

            foreach (var snub in highPerformers.Take(3))
            {
                snubs.Add($"{snub.PlayerName} - ranked #{snub.ConferenceRank} in {snub.Conference}");
            }

            return snubs;
        }

        private string SelectHostCity()
        {
            var hostCities = new[] {
                "Indianapolis", "San Francisco", "Los Angeles", "Cleveland",
                "Salt Lake City", "Chicago", "Atlanta", "Charlotte"
            };
            return hostCities[UnityEngine.Random.Range(0, hostCities.Length)];
        }

        private string GetHostArena()
        {
            // Would match with host city
            return "All-Star Arena";
        }

        private int SimulateThreePointRound(string playerId)
        {
            // Each rack has 5 balls (4 regular = 1pt, 1 money ball = 2pt)
            // 5 racks total, max 30 points
            int score = 0;
            float shootingSkill = GetPlayerThreePointSkill(playerId);

            for (int rack = 0; rack < 5; rack++)
            {
                // Regular balls
                for (int ball = 0; ball < 4; ball++)
                {
                    if (UnityEngine.Random.value < shootingSkill * 0.7f)
                        score += 1;
                }
                // Money ball
                if (UnityEngine.Random.value < shootingSkill * 0.6f)
                    score += 2;
            }

            return score;
        }

        private int SimulateDunk(string playerId, bool isFinal = false)
        {
            float dunkSkill = GetPlayerDunkSkill(playerId);
            float baseScore = 35f + (dunkSkill * 15f);

            // Finals have higher stakes, potential for spectacular dunks
            if (isFinal)
            {
                baseScore += UnityEngine.Random.Range(-5f, 10f);
            }

            // Chance of perfect 50
            if (UnityEngine.Random.value < dunkSkill * 0.15f)
            {
                return 50;
            }

            return Mathf.Clamp(Mathf.RoundToInt(baseScore + UnityEngine.Random.Range(-5f, 5f)), 30, 50);
        }

        private float GetPlayerThreePointSkill(string playerId)
        {
            // Would integrate with player stats
            return UnityEngine.Random.Range(0.5f, 0.9f);
        }

        private float GetPlayerDunkSkill(string playerId)
        {
            // Would integrate with player athleticism
            return UnityEngine.Random.Range(0.5f, 0.95f);
        }

        private PlayerAllStarStats SimulateAllStarPlayerStats(AllStarVote player)
        {
            bool isStarter = player.IsStarter;
            int minutes = isStarter ? UnityEngine.Random.Range(20, 30) : UnityEngine.Random.Range(10, 20);

            return new PlayerAllStarStats
            {
                PlayerId = player.PlayerId,
                Minutes = minutes,
                Points = UnityEngine.Random.Range(8, 35),
                Rebounds = UnityEngine.Random.Range(2, 12),
                Assists = UnityEngine.Random.Range(1, 10),
                Steals = UnityEngine.Random.Range(0, 4),
                Blocks = UnityEngine.Random.Range(0, 3),
                ThreePointersMade = UnityEngine.Random.Range(0, 8),
                WasDunkContestMoment = UnityEngine.Random.value < 0.15f
            };
        }

        private float CalculateMvpScore(PlayerAllStarStats stats)
        {
            return stats.Points * 1.0f + stats.Rebounds * 0.8f + stats.Assists * 1.2f +
                   stats.Steals * 1.5f + stats.Blocks * 1.5f + stats.ThreePointersMade * 0.5f;
        }

        private string GetPlayerName(string playerId)
        {
            // Would integrate with PlayerManager
            return $"Player_{playerId}";
        }

        #endregion

        /// <summary>
        /// Get historical All-Star selections for a player
        /// </summary>
        public List<AllStarWeekendSummary> GetPlayerAllStarHistory(string playerId)
        {
            return historicalWeekends.Where(w =>
                w.GetAllSelected().Any(s => s.PlayerId == playerId)).ToList();
        }

        /// <summary>
        /// Get All-Star selection count for a player
        /// </summary>
        public int GetAllStarSelectionCount(string playerId)
        {
            return GetPlayerAllStarHistory(playerId).Count;
        }

        /// <summary>
        /// Check if player has been All-Star MVP
        /// </summary>
        public bool HasBeenAllStarMvp(string playerId)
        {
            return historicalWeekends.Any(w =>
                w.AllStarGameResult != null && w.AllStarGameResult.MvpId == playerId);
        }
    }

    /// <summary>
    /// Placeholder for player season stats
    /// </summary>
    [Serializable]
    public class PlayerSeasonStats
    {
        public string PlayerId;
        public float PointsPerGame;
        public float ReboundsPerGame;
        public float AssistsPerGame;
        public float StealsPerGame;
        public float BlocksPerGame;
        public float ThreePointPercentage;
    }

    /// <summary>
    /// Extension method for ordinal numbers
    /// </summary>
    public static class IntExtensions
    {
        public static string ToOrdinal(this int number)
        {
            if (number <= 0) return number.ToString();

            switch (number % 100)
            {
                case 11:
                case 12:
                case 13:
                    return number + "th";
            }

            switch (number % 10)
            {
                case 1: return number + "st";
                case 2: return number + "nd";
                case 3: return number + "rd";
                default: return number + "th";
            }
        }
    }
}
