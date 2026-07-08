using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
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
    public class AllStarManager : ISeasonPhaseListener, IDailyTickable, ISaveSection
    {
        public int TickOrder => 155;

        /// <summary>
        /// Test seam / player lookup. Defaults to the live database.
        /// </summary>
        public Func<string, Data.Player> PlayerSource;

        private Data.Player ResolvePlayer(string playerId) =>
            PlayerSource != null ? PlayerSource(playerId)
            : GameManager.Instance?.PlayerDatabase?.GetPlayer(playerId);

        /// <summary>
        /// Daily rail: the game (and contests) run on All-Star Sunday — two days
        /// into the break — once selections exist and the game hasn't been played.
        /// </summary>
        public void DailyTick(in DailyTickContext ctx)
        {
            var gm = ctx.Game;
            if (gm?.SeasonController == null) return;
            if (gm.SeasonController.CurrentPhase != Data.SeasonPhase.AllStarBreak) return;
            if (CurrentWeekend == null || CurrentWeekend.Season != gm.CurrentSeason) return;
            if (CurrentWeekend.AllStarGameResult != null) return;
            if (ctx.Date.Day < 16) return; // break starts Feb 14; Sunday is the 16th

            // Best-record coach rule: if the player's team leads its conference,
            // the player coaches the game — hold it for them (one day of grace)
            // instead of auto-simming, and tell them once.
            if (PlayerCoachesASG(gm) && ctx.Date.Day < 18)
            {
                if (!_coachInviteSent)
                {
                    _coachInviteSent = true;
                    string conf = gm.GetPlayerTeam()?.Conference ?? "conference";
                    InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                        $"You're coaching the {conf} All-Stars",
                        "Best record in the conference at the break — the sideline is yours. " +
                        "Run the game from the All-Star desk.",
                        highPriority: true, deepLinkPanelId: "AllStar");
                }
                return;
            }

            RunAllStarWeekend(gm.PlayerDatabase);
        }

        private bool _coachInviteSent;

        /// <summary>
        /// The real-NBA rule: the coach of the conference's best team at the break
        /// coaches its All-Star squad. Only meaningful if the user actually coaches.
        /// </summary>
        public static bool PlayerCoachesASG(GameManager gm)
        {
            if (gm == null || !Data.RolePermissions.CanPlayInteractiveMatch) return false;
            var playerTeam = gm.GetPlayerTeam();
            if (playerTeam == null) return false;
            return TeamLeadsConference(playerTeam, gm.AllTeams);
        }

        /// <summary>Pure best-record check, testable headless. Ties break toward the player's team.</summary>
        public static bool TeamLeadsConference(Team team, IEnumerable<Team> allTeams)
        {
            if (team == null || allTeams == null) return false;
            float Pct(Team t) => t.Wins + t.Losses == 0 ? 0f : (float)t.Wins / (t.Wins + t.Losses);
            float mine = Pct(team);
            foreach (var t in allTeams)
            {
                if (t == null || t.TeamId == team.TeamId || t.Conference != team.Conference) continue;
                if (Pct(t) > mine) return false;
            }
            return true;
        }

        public static AllStarManager Instance { get; private set; }

        public string SystemId => "AllStar";

        /// <summary>The finalized rosters for the current season's weekend.</summary>
        public AllStarWeekendSummary CurrentWeekend { get; private set; }

        /// <summary>
        /// Phase rail: when the All-Star break begins, run the full selection
        /// (fan/player/media voting -> starters -> reserves) and announce it.
        /// </summary>
        public void OnSeasonPhaseChanged(Data.SeasonPhase oldPhase, Data.SeasonPhase newPhase, System.DateTime date)
        {
            if (newPhase != Data.SeasonPhase.AllStarBreak) return;

            var gm = GameManager.Instance;
            if (gm?.PlayerDatabase == null) return;

            RunSelections(gm.CurrentSeason, date, gm.PlayerDatabase.GetAllPlayers(),
                id => gm.GetTeam(id)?.Conference);
        }

        /// <summary>
        /// Run the complete All-Star selection for a season: build the voting pool
        /// from eligible players, simulate the three voting bodies, finalize the
        /// starters (2 backcourt + 3 frontcourt per conference), pick reserves, and
        /// announce the rosters. Idempotent per season.
        /// </summary>
        public AllStarWeekendSummary RunSelections(int season, DateTime date,
            List<Data.Player> players, Func<string, string> conferenceOf)
        {
            if (CurrentWeekend != null && CurrentWeekend.Season == season) return CurrentWeekend;
            if (players == null || conferenceOf == null) return null;

            var eligible = players
                .Where(p => p?.CurrentSeasonStats != null && p.CurrentSeasonStats.GamesPlayed >= 15)
                .ToList();
            if (eligible.Count < 24)
            {
                Debug.LogWarning($"[AllStarManager] Only {eligible.Count} eligible players — skipping selections");
                return null;
            }

            currentSeason = season;
            votingOpen = true;
            allVotes.Clear();
            votingReturns.Clear();

            foreach (var p in eligible)
            {
                string conf = conferenceOf(p.TeamId) ?? "";
                allVotes.Add(new AllStarVote
                {
                    PlayerId = p.PlayerId,
                    PlayerName = p.FullName,
                    TeamId = p.TeamId,
                    Position = p.Position == Data.Position.PointGuard || p.Position == Data.Position.ShootingGuard
                        ? AllStarPosition.Backcourt
                        : AllStarPosition.Frontcourt,
                    Conference = conf.StartsWith("East") ? "East" : "West"
                });
            }

            var snapshots = eligible
                .Select(p => PlayerSeasonStats.FromSeasonStats(p.PlayerId, p.CurrentSeasonStats))
                .ToList();
            var popularity = eligible.ToDictionary(p => p.PlayerId,
                p => Mathf.Clamp01(p.OverallRating / 100f));
            var advanced = eligible.ToDictionary(p => p.PlayerId,
                p => p.CurrentSeasonStats.PPG);

            SimulateFanVoting(snapshots, popularity);
            SimulatePlayerVoting(snapshots);
            SimulateMediaVoting(snapshots, advanced);

            var summary = FinalizeStarters();
            SelectReserves(summary);
            CurrentWeekend = summary;
            if (historicalWeekends.All(w => w.Season != season))
                historicalWeekends.Add(summary);

            // Persist the selections — they feed the season's award record
            AwardsStore.Instance?.RecordAllStars(season,
                summary.EastStarters.Concat(summary.WestStarters)
                    .Concat(summary.EastReserves).Concat(summary.WestReserves)
                    .Select(v => v.PlayerId));

            AnnounceSelections(summary);
            return summary;
        }

        private void AnnounceSelections(AllStarWeekendSummary summary)
        {
            var inbox = InboxService.Instance;
            if (inbox == null || summary == null) return;

            string Names(IEnumerable<AllStarVote> votes) =>
                string.Join(", ", votes.Select(v => v.PlayerName));

            inbox.Publish(InboxMessageType.League, "League Office",
                $"{summary.Season} All-Star rosters announced",
                $"EAST starters: {Names(summary.EastStarters)}\n" +
                $"WEST starters: {Names(summary.WestStarters)}\n" +
                $"East reserves: {Names(summary.EastReserves)}\n" +
                $"West reserves: {Names(summary.WestReserves)}");

            // Player-team selections get their own high-priority message
            string pid = GameManager.Instance?.PlayerTeamId;
            if (string.IsNullOrEmpty(pid)) return;

            var ours = summary.EastStarters.Concat(summary.WestStarters)
                .Concat(summary.EastReserves).Concat(summary.WestReserves)
                .Where(v => v.TeamId == pid)
                .ToList();
            if (ours.Count > 0)
            {
                inbox.Publish(InboxMessageType.League, "League Office",
                    ours.Count == 1
                        ? $"{ours[0].PlayerName} named an All-Star!"
                        : $"{ours.Count} of your players named All-Stars!",
                    Names(ours),
                    highPriority: true,
                    deepLinkPanelId: "Roster",
                    deepLinkPayload: ours[0].PlayerId);
            }
        }

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

        public AllStarManager()
        {
            Instance = this;
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
        /// Run the full weekend: 3PT contest, dunk contest, and the game itself —
        /// the game is a REAL simulation (GameSimulator over synthetic East/West
        /// teams built from the selections), with a proper box score and an MVP.
        /// Exhibition: nothing touches season stats, standings, or energy.
        /// Idempotent per season.
        /// </summary>
        public AllStarWeekendSummary RunAllStarWeekend(Data.PlayerDatabase playerDb)
        {
            var weekend = CurrentWeekend;
            if (weekend == null || weekend.AllStarGameResult != null || playerDb == null)
                return weekend;

            var selected = weekend.GetAllSelected();

            // Saturday night: contests with real names, seeded by real (hidden) skills
            var shooters = selected
                .OrderByDescending(v => ResolvePlayer(v.PlayerId)?.Shot_Three ?? 0)
                .Take(8).Select(v => v.PlayerId).ToList();
            weekend.ThreePointContestResult = SimulateThreePointContest(shooters);

            var leapers = selected
                .OrderByDescending(v => (ResolvePlayer(v.PlayerId)?.Vertical ?? 0) +
                                        (ResolvePlayer(v.PlayerId)?.Finishing_Rim ?? 0))
                .Take(6).Select(v => v.PlayerId).ToList();
            weekend.DunkContestResult = SimulateDunkContest(leapers);

            // Sunday: the game, for real
            var east = BuildConferenceTeam(weekend, "East");
            var west = BuildConferenceTeam(weekend, "West");
            var sim = new Simulation.GameSimulator(playerDb);
            var gameResult = sim.SimulateExhibition(east, west);

            weekend.AllStarGameResult = ConvertToAllStarResult(weekend, gameResult);
            OnAllStarGameCompleted?.Invoke(weekend.AllStarGameResult);

            var r = weekend.AllStarGameResult;
            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                $"All-Star Game: {r.WinningConference} wins {Math.Max(r.EastScore, r.WestScore)}-{Math.Min(r.EastScore, r.WestScore)}",
                $"{r.MvpName} takes MVP with {r.MvpPoints}/{r.MvpRebounds}/{r.MvpAssists}. " +
                $"{weekend.ThreePointContestResult?.WinnerName} won the three-point contest; " +
                $"{weekend.DunkContestResult?.WinnerName} took the dunk title.",
                deepLinkPanelId: "AllStar");

            return weekend;
        }

        /// <summary>Synthetic conference squad over real players in the shared database.</summary>
        public Team BuildConferenceTeam(AllStarWeekendSummary weekend, string conference)
        {
            bool east = conference == "East";
            var starters = east ? weekend.EastStarters : weekend.WestStarters;
            var reserves = east ? weekend.EastReserves : weekend.WestReserves;

            var team = new Team
            {
                TeamId = east ? "ALLSTAR_EAST" : "ALLSTAR_WEST",
                City = east ? "East" : "West",
                Nickname = "All-Stars",
                Conference = conference
            };
            team.RosterPlayerIds = starters.Concat(reserves).Select(v => v.PlayerId).ToList();
            for (int i = 0; i < 5 && i < starters.Count; i++)
                team.StartingLineupIds[i] = starters[i].PlayerId;

            // The player's squad carries the player's coaching identity; the other
            // bench gets an exhibition coach.
            var gm = GameManager.Instance;
            var playerTeam = gm?.GetPlayerTeam();
            bool playerBench = gm != null && PlayerCoachesASG(gm) &&
                               playerTeam?.Conference == conference;
            if (playerBench && playerTeam?.Strategy != null)
            {
                team.CoachPersonality = playerTeam.CoachPersonality;
                team.Strategy = playerTeam.Strategy;
                team.OffensiveStrategy = playerTeam.Strategy;
                team.DefensiveStrategy = playerTeam.Strategy;
            }
            else
            {
                var coach = AI.AICoachPersonality.CreateRandom(team.TeamId, $"{conference} bench",
                    new System.Random(weekend.Season * 31 + (east ? 1 : 2)));
                team.CoachPersonality = coach;
                team.AutoSetStrategy(coach);
            }
            return team;
        }

        /// <summary>Box score of the real sim → the All-Star result shape the UI renders.</summary>
        private AllStarGameResult ConvertToAllStarResult(AllStarWeekendSummary weekend, Simulation.GameResult game)
        {
            var result = new AllStarGameResult
            {
                EastScore = game.HomeScore,
                WestScore = game.AwayScore,
                WinningConference = game.HomeScore > game.AwayScore ? "East" : "West"
            };

            foreach (var line in game.BoxScore.PlayerStats.Values)
            {
                result.PlayerStats[line.PlayerId] = new PlayerAllStarStats
                {
                    PlayerId = line.PlayerId,
                    Minutes = line.Minutes,
                    Points = line.Points,
                    Rebounds = line.Rebounds,
                    Assists = line.Assists,
                    Steals = line.Steals,
                    Blocks = line.Blocks,
                    ThreePointersMade = line.ThreePointMade
                };
            }

            var winners = result.WinningConference == "East"
                ? weekend.EastStarters.Concat(weekend.EastReserves)
                : weekend.WestStarters.Concat(weekend.WestReserves);
            var mvp = winners
                .Where(v => result.PlayerStats.ContainsKey(v.PlayerId))
                .OrderByDescending(v => CalculateMvpScore(result.PlayerStats[v.PlayerId]))
                .FirstOrDefault();
            if (mvp != null)
            {
                result.MvpId = mvp.PlayerId;
                result.MvpName = mvp.PlayerName;
                var line = result.PlayerStats[mvp.PlayerId];
                result.MvpPoints = line.Points;
                result.MvpRebounds = line.Rebounds;
                result.MvpAssists = line.Assists;
                result.GameHighlights.Add($"{result.MvpName} wins MVP with {line.Points} points!");
                if (line.Points >= 40)
                    result.GameHighlights.Add($"Historic performance! {result.MvpName} erupts for {line.Points}!");
            }
            if (Math.Abs(result.EastScore - result.WestScore) <= 5)
                result.GameHighlights.Add("Thrilling finish as the game comes down to the wire!");

            return result;
        }

        /// <summary>
        /// Simulate the All-Star Game (legacy random model — superseded by
        /// RunAllStarWeekend's real simulation; kept for compatibility).
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
            var p = ResolvePlayer(playerId);
            if (p == null) return 0.7f;
            return Mathf.Clamp(0.4f + p.Shot_Three / 100f * 0.55f, 0.4f, 0.95f);
        }

        private float GetPlayerDunkSkill(string playerId)
        {
            var p = ResolvePlayer(playerId);
            if (p == null) return 0.7f;
            return Mathf.Clamp(0.35f + (p.Vertical + p.Finishing_Rim) / 200f * 0.6f, 0.35f, 0.95f);
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
            var p = ResolvePlayer(playerId);
            return p?.FullName ?? playerId;
        }

        #endregion

        // ==================== PERSISTENCE ====================
        // AllStarWeekendSummary holds Dictionaries (JsonUtility-hostile), so the
        // save shape is a flat DTO: rosters, contest winners, and the game line.

        public void WriteSave(SaveData data)
        {
            if (data == null || CurrentWeekend == null) return;
            var w = CurrentWeekend;
            var dto = new AllStarSaveData
            {
                Season = w.Season,
                HostCity = w.HostCity,
                CoachInviteSent = _coachInviteSent,
                NotableSnubs = new List<string>(w.NotableSnubs ?? new List<string>())
            };

            void PackVotes(List<AllStarVote> votes, bool east, bool starter)
            {
                if (votes == null) return;
                foreach (var v in votes)
                    dto.Selections.Add(new AllStarVoteRecord
                    {
                        PlayerId = v.PlayerId, PlayerName = v.PlayerName, TeamId = v.TeamId,
                        Conference = v.Conference, IsEast = east, IsStarter = starter
                    });
            }
            PackVotes(w.EastStarters, true, true);
            PackVotes(w.EastReserves, true, false);
            PackVotes(w.WestStarters, false, true);
            PackVotes(w.WestReserves, false, false);

            if (w.ThreePointContestResult != null)
            {
                dto.ThreePointWinnerId = w.ThreePointContestResult.WinnerId;
                dto.ThreePointWinnerName = w.ThreePointContestResult.WinnerName;
            }
            if (w.DunkContestResult != null)
            {
                dto.DunkWinnerId = w.DunkContestResult.WinnerId;
                dto.DunkWinnerName = w.DunkContestResult.WinnerName;
            }
            if (w.AllStarGameResult != null)
            {
                var g = w.AllStarGameResult;
                dto.GamePlayed = true;
                dto.EastScore = g.EastScore;
                dto.WestScore = g.WestScore;
                dto.WinningConference = g.WinningConference;
                dto.MvpId = g.MvpId; dto.MvpName = g.MvpName;
                dto.MvpPoints = g.MvpPoints; dto.MvpRebounds = g.MvpRebounds; dto.MvpAssists = g.MvpAssists;
                dto.GameHighlights = new List<string>(g.GameHighlights ?? new List<string>());
                foreach (var line in g.PlayerStats.Values)
                    dto.GameLines.Add(line);
            }

            data.AllStarData = dto;
        }

        public void ReadSave(SaveData data, in SaveReadContext ctx)
        {
            var dto = data?.AllStarData;
            if (dto == null || dto.Season == 0 || dto.Selections == null || dto.Selections.Count == 0)
                return;

            var w = new AllStarWeekendSummary
            {
                Season = dto.Season,
                HostCity = dto.HostCity,
                NotableSnubs = dto.NotableSnubs ?? new List<string>()
            };
            foreach (var rec in dto.Selections)
            {
                var vote = new AllStarVote
                {
                    PlayerId = rec.PlayerId, PlayerName = rec.PlayerName,
                    TeamId = rec.TeamId, Conference = rec.Conference, IsStarter = rec.IsStarter
                };
                var list = rec.IsEast
                    ? (rec.IsStarter ? w.EastStarters : w.EastReserves)
                    : (rec.IsStarter ? w.WestStarters : w.WestReserves);
                list.Add(vote);
            }

            if (!string.IsNullOrEmpty(dto.ThreePointWinnerId))
                w.ThreePointContestResult = new AllStarEventResult
                {
                    EventType = AllStarEventType.ThreePointContest,
                    WinnerId = dto.ThreePointWinnerId, WinnerName = dto.ThreePointWinnerName
                };
            if (!string.IsNullOrEmpty(dto.DunkWinnerId))
                w.DunkContestResult = new AllStarEventResult
                {
                    EventType = AllStarEventType.DunkContest,
                    WinnerId = dto.DunkWinnerId, WinnerName = dto.DunkWinnerName
                };
            if (dto.GamePlayed)
            {
                var g = new AllStarGameResult
                {
                    EastScore = dto.EastScore, WestScore = dto.WestScore,
                    WinningConference = dto.WinningConference,
                    MvpId = dto.MvpId, MvpName = dto.MvpName,
                    MvpPoints = dto.MvpPoints, MvpRebounds = dto.MvpRebounds, MvpAssists = dto.MvpAssists,
                    GameHighlights = dto.GameHighlights ?? new List<string>()
                };
                foreach (var line in dto.GameLines ?? new List<PlayerAllStarStats>())
                    g.PlayerStats[line.PlayerId] = line;
                w.AllStarGameResult = g;
            }

            _coachInviteSent = dto.CoachInviteSent;
            CurrentWeekend = w;
        }

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

    /// <summary>Flat, JsonUtility-safe snapshot of the current season's All-Star weekend.</summary>
    [Serializable]
    public class AllStarSaveData
    {
        public int Season;
        public string HostCity;
        public bool CoachInviteSent;
        public List<AllStarVoteRecord> Selections = new List<AllStarVoteRecord>();
        public List<string> NotableSnubs = new List<string>();

        public string ThreePointWinnerId;
        public string ThreePointWinnerName;
        public string DunkWinnerId;
        public string DunkWinnerName;

        public bool GamePlayed;
        public int EastScore;
        public int WestScore;
        public string WinningConference;
        public string MvpId;
        public string MvpName;
        public int MvpPoints;
        public int MvpRebounds;
        public int MvpAssists;
        public List<string> GameHighlights = new List<string>();
        public List<PlayerAllStarStats> GameLines = new List<PlayerAllStarStats>();
    }

    [Serializable]
    public class AllStarVoteRecord
    {
        public string PlayerId;
        public string PlayerName;
        public string TeamId;
        public string Conference;
        public bool IsEast;
        public bool IsStarter;
    }
}
