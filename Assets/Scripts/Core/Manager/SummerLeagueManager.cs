using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Summer League location/type
    /// </summary>
    [Serializable]
    public enum SummerLeagueType
    {
        LasVegas,       // Main event - all 30 teams
        California,     // Sacramento - select teams
        Utah            // Salt Lake City - select teams
    }

    /// <summary>
    /// Player eligibility for Summer League
    /// </summary>
    [Serializable]
    public enum SummerLeagueEligibility
    {
        Rookie,             // First year players
        SecondYear,         // Second year players
        UndraftedInvite,    // Trying to make roster
        TwoWayContract,     // Two-way players
        GLeagueProspect,    // G-League call-up candidates
        InternationalProspect
    }

    /// <summary>
    /// Individual player's Summer League performance
    /// </summary>
    [Serializable]
    public class SummerLeaguePlayerStats
    {
        public string PlayerId;
        public string PlayerName;
        public SummerLeagueEligibility Eligibility;

        [Header("Per Game Stats")]
        public int GamesPlayed;
        public float MinutesPerGame;
        public float PointsPerGame;
        public float ReboundsPerGame;
        public float AssistsPerGame;
        public float StealsPerGame;
        public float BlocksPerGame;
        public float TurnoversPerGame;
        public float FieldGoalPercentage;
        public float ThreePointPercentage;
        public float FreeThrowPercentage;

        [Header("Totals")]
        public int TotalPoints;
        public int TotalRebounds;
        public int TotalAssists;

        [Header("Development Impact")]
        [Range(-10f, 10f)]
        public float ConfidenceChange;
        [Range(-5f, 5f)]
        public float SkillDevelopmentBonus;
        public List<string> SkillsImproved = new List<string>();
        public List<string> WeaknessesExposed = new List<string>();

        [Header("Evaluation")]
        public float OverallGrade;      // A+ to F
        public string ScoutingNotes;
        public bool ExceededExpectations;
        public bool Disappointed;
    }

    /// <summary>
    /// Team's overall Summer League results
    /// </summary>
    [Serializable]
    public class SummerLeagueTeamResult
    {
        public string TeamId;
        public string TeamName;
        public int Wins;
        public int Losses;
        public int PointsFor;
        public int PointsAgainst;
        public int StandingsPosition;
        public bool WonChampionship;

        public List<SummerLeaguePlayerStats> RosterStats = new List<SummerLeaguePlayerStats>();

        [Header("Highlights")]
        public string MvpPlayerId;
        public string MvpPlayerName;
        public List<string> NotablePerformances = new List<string>();
        public List<string> DevelopmentTakeaways = new List<string>();

        public float WinPercentage => GamesPlayed > 0 ? (float)Wins / GamesPlayed : 0f;
        public int GamesPlayed => Wins + Losses;
    }

    /// <summary>
    /// A single Summer League game
    /// </summary>
    [Serializable]
    public class SummerLeagueGame
    {
        public string GameId;
        public DateTime GameDate;
        public SummerLeagueType LeagueType;
        public string HomeTeamId;
        public string AwayTeamId;
        public int HomeScore;
        public int AwayScore;
        public bool IsCompleted;
        public bool IsChampionship;

        public string WinnerId => HomeScore > AwayScore ? HomeTeamId : AwayTeamId;
        public string LoserId => HomeScore > AwayScore ? AwayTeamId : HomeTeamId;
    }

    /// <summary>
    /// Complete Summer League summary for a season
    /// </summary>
    [Serializable]
    public class SummerLeagueSummary
    {
        public int Season;
        public SummerLeagueType LeagueType;
        public DateTime StartDate;
        public DateTime EndDate;
        public bool WasSkipped;

        [Header("Results")]
        public List<SummerLeagueTeamResult> TeamResults = new List<SummerLeagueTeamResult>();
        public List<SummerLeagueGame> Games = new List<SummerLeagueGame>();

        [Header("Awards")]
        public string MvpId;
        public string MvpName;
        public string MvpTeamId;
        public SummerLeaguePlayerStats MvpStats;
        public string ChampionTeamId;

        [Header("Notable Events")]
        public List<string> Headlines = new List<string>();
        public List<string> BreakoutPlayers = new List<string>();
        public List<string> Disappointments = new List<string>();

        public SummerLeagueTeamResult GetTeamResult(string teamId)
        {
            return TeamResults.FirstOrDefault(t => t.TeamId == teamId);
        }
    }

    /// <summary>
    /// Development impact from Summer League participation
    /// </summary>
    [Serializable]
    public class SummerLeagueDevelopmentImpact
    {
        public string PlayerId;
        public float OverallDevelopmentBonus;
        public Dictionary<string, float> AttributeChanges = new Dictionary<string, float>();
        public float ConfidenceChange;
        public float ChemistryWithTeammates;
        public bool EarnedRotationSpot;
        public bool LostRotationSpot;
        public string CoachAssessment;
    }

    /// <summary>
    /// Manages Summer League with skippable option and development impact
    /// </summary>
    public class SummerLeagueManager : MonoBehaviour
    {
        public static SummerLeagueManager Instance { get; private set; }

        [Header("Current Summer League")]
        [SerializeField] private int currentSeason;
        [SerializeField] private SummerLeagueType currentLeague;
        [SerializeField] private bool isActive;
        [SerializeField] private bool playerSkipped;

        [Header("Schedule")]
        [SerializeField] private List<SummerLeagueGame> schedule = new List<SummerLeagueGame>();
        [SerializeField] private int currentGameIndex;

        [Header("Results")]
        [SerializeField] private List<SummerLeagueTeamResult> teamResults = new List<SummerLeagueTeamResult>();

        [Header("Historical Data")]
        [SerializeField] private List<SummerLeagueSummary> historicalSummaries = new List<SummerLeagueSummary>();

        [Header("Configuration")]
        [SerializeField] private int gamesPerTeam = 5;
        [SerializeField] private int teamsInTournament = 30;

        [Header("Development Settings")]
        [SerializeField, Range(0f, 1f)] private float baseDevBonus = 0.5f;
        [SerializeField, Range(0f, 2f)] private float performanceDevMultiplier = 1.0f;
        [SerializeField, Range(0f, 1f)] private float confidenceImpactMultiplier = 0.3f;

        // Events
        public event Action<SummerLeagueSummary> OnSummerLeagueStarted;
        public event Action<SummerLeagueGame> OnGameCompleted;
        public event Action<SummerLeagueSummary> OnSummerLeagueEnded;
        public event Action<SummerLeagueDevelopmentImpact> OnPlayerDevelopmentApplied;

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
        /// Start Summer League for the season
        /// </summary>
        public SummerLeagueSummary StartSummerLeague(int season, SummerLeagueType leagueType = SummerLeagueType.LasVegas)
        {
            currentSeason = season;
            currentLeague = leagueType;
            isActive = true;
            playerSkipped = false;
            currentGameIndex = 0;

            schedule.Clear();
            teamResults.Clear();

            // Initialize team results
            InitializeTeamResults();

            // Generate schedule
            GenerateSchedule();

            var summary = CreateCurrentSummary();
            OnSummerLeagueStarted?.Invoke(summary);

            Debug.Log($"Summer League {season} started in {leagueType}. {schedule.Count} games scheduled.");
            return summary;
        }

        /// <summary>
        /// Skip Summer League and simulate results
        /// Player sees summary of results and development impact
        /// </summary>
        public SummerLeagueSummary SkipSummerLeague()
        {
            if (!isActive)
            {
                Debug.LogWarning("No active Summer League to skip");
                return null;
            }

            playerSkipped = true;

            // Simulate all remaining games
            while (currentGameIndex < schedule.Count)
            {
                SimulateGame(schedule[currentGameIndex]);
                currentGameIndex++;
            }

            // Calculate final standings
            CalculateFinalStandings();

            // Apply development impacts
            ApplyAllDevelopmentImpacts();

            // Generate summary
            var summary = FinalizeSummerLeague();
            summary.WasSkipped = true;

            return summary;
        }

        /// <summary>
        /// Play through Summer League game by game
        /// </summary>
        public SummerLeagueGame PlayNextGame()
        {
            if (!isActive || currentGameIndex >= schedule.Count)
            {
                return null;
            }

            var game = schedule[currentGameIndex];
            SimulateGame(game);
            currentGameIndex++;

            OnGameCompleted?.Invoke(game);

            // Check if Summer League is complete
            if (currentGameIndex >= schedule.Count)
            {
                FinalizeSummerLeague();
            }

            return game;
        }

        /// <summary>
        /// Simulate a single game
        /// </summary>
        private void SimulateGame(SummerLeagueGame game)
        {
            if (game.IsCompleted) return;

            // Summer League games are lower scoring, more variable
            int baseScore = UnityEngine.Random.Range(65, 95);
            int scoreDiff = UnityEngine.Random.Range(-20, 21);

            game.HomeScore = baseScore + UnityEngine.Random.Range(-5, 6);
            game.AwayScore = baseScore + scoreDiff + UnityEngine.Random.Range(-5, 6);

            // Prevent ties
            if (game.HomeScore == game.AwayScore)
            {
                game.HomeScore += UnityEngine.Random.value > 0.5f ? 2 : -2;
            }

            game.IsCompleted = true;

            // Update team results
            UpdateTeamResult(game.HomeTeamId, game.HomeScore, game.AwayScore);
            UpdateTeamResult(game.AwayTeamId, game.AwayScore, game.HomeScore);

            // Simulate player performances
            SimulatePlayerPerformances(game);
        }

        /// <summary>
        /// Simulate individual player performances for a game
        /// </summary>
        private void SimulatePlayerPerformances(SummerLeagueGame game)
        {
            var homeResult = teamResults.FirstOrDefault(t => t.TeamId == game.HomeTeamId);
            var awayResult = teamResults.FirstOrDefault(t => t.TeamId == game.AwayTeamId);

            if (homeResult != null)
            {
                SimulateTeamPlayerPerformances(homeResult, game.HomeScore);
            }
            if (awayResult != null)
            {
                SimulateTeamPlayerPerformances(awayResult, game.AwayScore);
            }
        }

        private void SimulateTeamPlayerPerformances(SummerLeagueTeamResult teamResult, int teamScore)
        {
            // Distribute stats among roster
            foreach (var player in teamResult.RosterStats)
            {
                player.GamesPlayed++;

                // Simulate individual game stats
                float minutes = SimulateMinutes(player);
                float minutesFactor = minutes / 36f;

                int points = Mathf.RoundToInt(UnityEngine.Random.Range(5, 25) * minutesFactor);
                int rebounds = Mathf.RoundToInt(UnityEngine.Random.Range(2, 10) * minutesFactor);
                int assists = Mathf.RoundToInt(UnityEngine.Random.Range(1, 8) * minutesFactor);

                // Update totals
                player.TotalPoints += points;
                player.TotalRebounds += rebounds;
                player.TotalAssists += assists;

                // Update averages
                player.MinutesPerGame = (player.MinutesPerGame * (player.GamesPlayed - 1) + minutes) / player.GamesPlayed;
                player.PointsPerGame = (float)player.TotalPoints / player.GamesPlayed;
                player.ReboundsPerGame = (float)player.TotalRebounds / player.GamesPlayed;
                player.AssistsPerGame = (float)player.TotalAssists / player.GamesPlayed;

                // Shooting percentages
                player.FieldGoalPercentage = UnityEngine.Random.Range(0.35f, 0.55f);
                player.ThreePointPercentage = UnityEngine.Random.Range(0.25f, 0.45f);
                player.FreeThrowPercentage = UnityEngine.Random.Range(0.60f, 0.85f);
            }
        }

        private float SimulateMinutes(SummerLeaguePlayerStats player)
        {
            // Rookies and key prospects get more minutes
            float baseMinutes = player.Eligibility switch
            {
                SummerLeagueEligibility.Rookie => 28f,
                SummerLeagueEligibility.SecondYear => 24f,
                SummerLeagueEligibility.TwoWayContract => 22f,
                SummerLeagueEligibility.UndraftedInvite => 18f,
                SummerLeagueEligibility.GLeagueProspect => 16f,
                SummerLeagueEligibility.InternationalProspect => 20f,
                _ => 20f
            };

            return Mathf.Clamp(baseMinutes + UnityEngine.Random.Range(-8f, 8f), 8f, 36f);
        }

        /// <summary>
        /// Calculate development impact for a player based on Summer League performance
        /// </summary>
        public SummerLeagueDevelopmentImpact CalculateDevelopmentImpact(SummerLeaguePlayerStats stats)
        {
            var impact = new SummerLeagueDevelopmentImpact
            {
                PlayerId = stats.PlayerId
            };

            // Base development bonus for participating
            float devBonus = baseDevBonus;

            // Performance multiplier
            float performanceScore = CalculatePerformanceScore(stats);
            devBonus += performanceScore * performanceDevMultiplier;

            impact.OverallDevelopmentBonus = devBonus;

            // Confidence change based on performance relative to expectations
            if (stats.ExceededExpectations)
            {
                impact.ConfidenceChange = UnityEngine.Random.Range(3f, 8f);
                impact.CoachAssessment = "Strong Summer League showing - ready for bigger role";
            }
            else if (stats.Disappointed)
            {
                impact.ConfidenceChange = UnityEngine.Random.Range(-5f, -1f);
                impact.CoachAssessment = "Struggled in Summer League - needs more development time";
            }
            else
            {
                impact.ConfidenceChange = UnityEngine.Random.Range(-1f, 3f);
                impact.CoachAssessment = "Solid Summer League performance - meeting expectations";
            }

            // Specific attribute improvements based on stats
            if (stats.PointsPerGame > 18f)
            {
                impact.AttributeChanges["Scoring"] = UnityEngine.Random.Range(0.5f, 2f);
            }
            if (stats.AssistsPerGame > 5f)
            {
                impact.AttributeChanges["Playmaking"] = UnityEngine.Random.Range(0.5f, 1.5f);
            }
            if (stats.ReboundsPerGame > 7f)
            {
                impact.AttributeChanges["Rebounding"] = UnityEngine.Random.Range(0.5f, 1.5f);
            }
            if (stats.ThreePointPercentage > 0.38f)
            {
                impact.AttributeChanges["ThreePointShooting"] = UnityEngine.Random.Range(0.3f, 1f);
            }

            // Chemistry with teammates who also played
            impact.ChemistryWithTeammates = UnityEngine.Random.Range(1f, 5f);

            // Rotation spot evaluation
            if (performanceScore > 0.75f && stats.Eligibility == SummerLeagueEligibility.Rookie)
            {
                impact.EarnedRotationSpot = true;
            }
            else if (performanceScore < 0.3f && stats.Eligibility == SummerLeagueEligibility.SecondYear)
            {
                impact.LostRotationSpot = true;
            }

            return impact;
        }

        private float CalculatePerformanceScore(SummerLeaguePlayerStats stats)
        {
            // Weighted score based on key stats
            float ptsScore = Mathf.Clamp01(stats.PointsPerGame / 25f);
            float rebScore = Mathf.Clamp01(stats.ReboundsPerGame / 10f);
            float astScore = Mathf.Clamp01(stats.AssistsPerGame / 8f);
            float effScore = Mathf.Clamp01(stats.FieldGoalPercentage / 0.5f);

            return (ptsScore * 0.4f) + (rebScore * 0.2f) + (astScore * 0.2f) + (effScore * 0.2f);
        }

        /// <summary>
        /// Apply development impacts to all participating players
        /// </summary>
        private void ApplyAllDevelopmentImpacts()
        {
            foreach (var team in teamResults)
            {
                foreach (var player in team.RosterStats)
                {
                    EvaluatePlayerPerformance(player);
                    var impact = CalculateDevelopmentImpact(player);
                    OnPlayerDevelopmentApplied?.Invoke(impact);
                }
            }
        }

        private void EvaluatePlayerPerformance(SummerLeaguePlayerStats stats)
        {
            // Grade player performance
            float score = CalculatePerformanceScore(stats);

            stats.OverallGrade = score switch
            {
                >= 0.9f => 95f,     // A+
                >= 0.8f => 90f,     // A
                >= 0.7f => 85f,     // B+
                >= 0.6f => 80f,     // B
                >= 0.5f => 75f,     // C+
                >= 0.4f => 70f,     // C
                >= 0.3f => 65f,     // D
                _ => 60f            // F
            };

            // Determine if exceeded/disappointed
            float expectedScore = stats.Eligibility switch
            {
                SummerLeagueEligibility.Rookie => 0.5f,
                SummerLeagueEligibility.SecondYear => 0.6f,
                SummerLeagueEligibility.TwoWayContract => 0.45f,
                SummerLeagueEligibility.UndraftedInvite => 0.35f,
                _ => 0.4f
            };

            stats.ExceededExpectations = score > expectedScore + 0.2f;
            stats.Disappointed = score < expectedScore - 0.15f;

            // Generate scouting notes
            stats.ScoutingNotes = GenerateScoutingNotes(stats);

            // Track improvements and weaknesses
            if (stats.PointsPerGame > 15f)
                stats.SkillsImproved.Add("Scoring ability");
            if (stats.ThreePointPercentage > 0.36f)
                stats.SkillsImproved.Add("Perimeter shooting");
            if (stats.AssistsPerGame > 4f)
                stats.SkillsImproved.Add("Court vision");

            if (stats.TurnoversPerGame > 3.5f)
                stats.WeaknessesExposed.Add("Ball security");
            if (stats.FieldGoalPercentage < 0.40f)
                stats.WeaknessesExposed.Add("Shot selection");
        }

        private string GenerateScoutingNotes(SummerLeaguePlayerStats stats)
        {
            var notes = new List<string>();

            if (stats.PointsPerGame > 20f)
                notes.Add("Showed scoring prowess against Summer League competition");
            if (stats.AssistsPerGame > 6f)
                notes.Add("Displayed excellent playmaking ability");
            if (stats.ReboundsPerGame > 8f)
                notes.Add("Dominated the glass");
            if (stats.ThreePointPercentage > 0.40f)
                notes.Add("Knocked down perimeter shots consistently");
            if (stats.TurnoversPerGame > 4f)
                notes.Add("Needs to improve decision making");
            if (stats.FieldGoalPercentage < 0.38f)
                notes.Add("Struggled with efficiency");

            if (notes.Count == 0)
                notes.Add("Solid but unspectacular Summer League showing");

            return string.Join(". ", notes) + ".";
        }

        /// <summary>
        /// Finalize Summer League and create complete summary
        /// </summary>
        private SummerLeagueSummary FinalizeSummerLeague()
        {
            isActive = false;

            // Calculate final standings
            CalculateFinalStandings();

            // Select MVP
            var mvp = SelectMvp();

            var summary = CreateCurrentSummary();
            summary.MvpId = mvp?.PlayerId;
            summary.MvpName = mvp?.PlayerName;
            summary.MvpStats = mvp;

            // Find MVP's team
            foreach (var team in teamResults)
            {
                if (team.RosterStats.Contains(mvp))
                {
                    summary.MvpTeamId = team.TeamId;
                    team.MvpPlayerId = mvp.PlayerId;
                    team.MvpPlayerName = mvp.PlayerName;
                    break;
                }
            }

            // Champion
            var champion = teamResults.FirstOrDefault(t => t.WonChampionship);
            summary.ChampionTeamId = champion?.TeamId;

            // Generate headlines
            summary.Headlines = GenerateHeadlines(summary);
            summary.BreakoutPlayers = IdentifyBreakoutPlayers();
            summary.Disappointments = IdentifyDisappointments();

            // Store in history
            historicalSummaries.Add(summary);

            OnSummerLeagueEnded?.Invoke(summary);
            return summary;
        }

        private void InitializeTeamResults()
        {
            // Would integrate with team roster data
            // Creates 30 team result placeholders
            for (int i = 0; i < teamsInTournament; i++)
            {
                var result = new SummerLeagueTeamResult
                {
                    TeamId = $"TEAM_{i:D2}",
                    TeamName = $"Team {i}",
                    Wins = 0,
                    Losses = 0,
                    PointsFor = 0,
                    PointsAgainst = 0
                };

                // Add placeholder roster
                for (int p = 0; p < 12; p++)
                {
                    result.RosterStats.Add(new SummerLeaguePlayerStats
                    {
                        PlayerId = $"PLAYER_{i}_{p}",
                        PlayerName = $"Player {p}",
                        Eligibility = (SummerLeagueEligibility)(p % 6)
                    });
                }

                teamResults.Add(result);
            }
        }

        private void GenerateSchedule()
        {
            // Generate round-robin style schedule with elimination rounds
            var gameDate = DateTime.Now;
            int gameNumber = 0;

            // Pool play - each team plays 4-5 games
            for (int round = 0; round < gamesPerTeam - 1; round++)
            {
                for (int i = 0; i < teamsInTournament / 2; i++)
                {
                    int homeIndex = (i + round) % teamsInTournament;
                    int awayIndex = (teamsInTournament - 1 - i + round) % teamsInTournament;

                    if (homeIndex != awayIndex)
                    {
                        schedule.Add(new SummerLeagueGame
                        {
                            GameId = $"SL_{currentSeason}_{gameNumber++}",
                            GameDate = gameDate,
                            LeagueType = currentLeague,
                            HomeTeamId = teamResults[homeIndex].TeamId,
                            AwayTeamId = teamResults[awayIndex].TeamId
                        });
                    }
                }
                gameDate = gameDate.AddDays(1);
            }

            // Championship bracket would be added after pool play completes
            // Final game
            schedule.Add(new SummerLeagueGame
            {
                GameId = $"SL_{currentSeason}_FINAL",
                GameDate = gameDate.AddDays(2),
                LeagueType = currentLeague,
                HomeTeamId = "TBD",
                AwayTeamId = "TBD",
                IsChampionship = true
            });
        }

        private void UpdateTeamResult(string teamId, int scored, int allowed)
        {
            var result = teamResults.FirstOrDefault(t => t.TeamId == teamId);
            if (result == null) return;

            result.PointsFor += scored;
            result.PointsAgainst += allowed;

            if (scored > allowed)
                result.Wins++;
            else
                result.Losses++;
        }

        private void CalculateFinalStandings()
        {
            var ranked = teamResults.OrderByDescending(t => t.Wins)
                                   .ThenByDescending(t => t.PointsFor - t.PointsAgainst)
                                   .ToList();

            for (int i = 0; i < ranked.Count; i++)
            {
                ranked[i].StandingsPosition = i + 1;
            }

            // Championship winner
            if (ranked.Count > 0)
            {
                ranked[0].WonChampionship = true;
            }
        }

        private SummerLeaguePlayerStats SelectMvp()
        {
            var allPlayers = teamResults.SelectMany(t => t.RosterStats)
                                        .Where(p => p.GamesPlayed >= 3);

            return allPlayers.OrderByDescending(p => CalculatePerformanceScore(p))
                            .ThenByDescending(p => p.PointsPerGame)
                            .FirstOrDefault();
        }

        private List<string> GenerateHeadlines(SummerLeagueSummary summary)
        {
            var headlines = new List<string>();

            if (summary.MvpStats != null)
            {
                headlines.Add($"{summary.MvpName} dominates Summer League, takes home MVP honors");
            }

            var champion = teamResults.FirstOrDefault(t => t.WonChampionship);
            if (champion != null)
            {
                headlines.Add($"{champion.TeamName} captures Summer League championship");
            }

            // Notable performances
            var topScorer = teamResults.SelectMany(t => t.RosterStats)
                                      .OrderByDescending(p => p.PointsPerGame)
                                      .FirstOrDefault();
            if (topScorer != null && topScorer.PointsPerGame > 22f)
            {
                headlines.Add($"{topScorer.PlayerName} leads Summer League in scoring at {topScorer.PointsPerGame:F1} PPG");
            }

            return headlines;
        }

        private List<string> IdentifyBreakoutPlayers()
        {
            return teamResults.SelectMany(t => t.RosterStats)
                             .Where(p => p.ExceededExpectations)
                             .OrderByDescending(p => CalculatePerformanceScore(p))
                             .Take(5)
                             .Select(p => $"{p.PlayerName} - {p.PointsPerGame:F1} PPG, {p.ReboundsPerGame:F1} RPG")
                             .ToList();
        }

        private List<string> IdentifyDisappointments()
        {
            return teamResults.SelectMany(t => t.RosterStats)
                             .Where(p => p.Disappointed && p.Eligibility == SummerLeagueEligibility.Rookie)
                             .OrderBy(p => CalculatePerformanceScore(p))
                             .Take(3)
                             .Select(p => $"{p.PlayerName} - struggled with {p.FieldGoalPercentage:P0} FG%")
                             .ToList();
        }

        private SummerLeagueSummary CreateCurrentSummary()
        {
            return new SummerLeagueSummary
            {
                Season = currentSeason,
                LeagueType = currentLeague,
                StartDate = schedule.FirstOrDefault()?.GameDate ?? DateTime.Now,
                EndDate = schedule.LastOrDefault()?.GameDate ?? DateTime.Now,
                TeamResults = new List<SummerLeagueTeamResult>(teamResults),
                Games = new List<SummerLeagueGame>(schedule),
                WasSkipped = playerSkipped
            };
        }

        /// <summary>
        /// Get complete results summary for player's team
        /// Called when player skips Summer League
        /// </summary>
        public string GetSkipSummaryForTeam(string teamId)
        {
            var teamResult = teamResults.FirstOrDefault(t => t.TeamId == teamId);
            if (teamResult == null) return "No results available.";

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"=== Summer League Results: {teamResult.TeamName} ===");
            summary.AppendLine($"Record: {teamResult.Wins}-{teamResult.Losses}");
            summary.AppendLine($"Standing: {teamResult.StandingsPosition.ToOrdinal()} place");
            summary.AppendLine();

            if (teamResult.WonChampionship)
            {
                summary.AppendLine("🏆 WON SUMMER LEAGUE CHAMPIONSHIP!");
                summary.AppendLine();
            }

            summary.AppendLine("--- Player Performances ---");
            foreach (var player in teamResult.RosterStats.OrderByDescending(p => p.PointsPerGame))
            {
                string status = "";
                if (player.ExceededExpectations) status = " ⭐";
                if (player.Disappointed) status = " ⚠️";

                summary.AppendLine($"{player.PlayerName}{status}: {player.PointsPerGame:F1} PPG, {player.ReboundsPerGame:F1} RPG, {player.AssistsPerGame:F1} APG");
            }

            summary.AppendLine();
            summary.AppendLine("--- Development Notes ---");
            foreach (var takeaway in teamResult.DevelopmentTakeaways)
            {
                summary.AppendLine($"• {takeaway}");
            }

            return summary.ToString();
        }

        /// <summary>
        /// Check if Summer League is currently active
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Get remaining games count
        /// </summary>
        public int RemainingGames => schedule.Count - currentGameIndex;

        /// <summary>
        /// Get current standings
        /// </summary>
        public List<SummerLeagueTeamResult> GetStandings()
        {
            return teamResults.OrderByDescending(t => t.Wins)
                             .ThenByDescending(t => t.PointsFor - t.PointsAgainst)
                             .ToList();
        }

        /// <summary>
        /// Get player's Summer League history
        /// </summary>
        public List<SummerLeaguePlayerStats> GetPlayerHistory(string playerId)
        {
            return historicalSummaries
                .SelectMany(s => s.TeamResults)
                .SelectMany(t => t.RosterStats)
                .Where(p => p.PlayerId == playerId)
                .ToList();
        }
    }
}
