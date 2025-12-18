using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Player legacy tier based on career achievements
    /// </summary>
    [Serializable]
    public enum LegacyTier
    {
        HallOfFame,             // All-time great
        AllTimeTier,            // Multiple All-Star, All-NBA
        StarPlayer,             // Multiple All-Star selections
        SolidCareer,            // Quality starter for years
        RolePlayers,            // Journeyman/bench contributor
        BriefCareer             // Short NBA tenure
    }

    /// <summary>
    /// Type of retirement announcement
    /// </summary>
    [Serializable]
    public enum RetirementAnnouncementType
    {
        PreSeason,              // Announces before final season
        MidSeason,              // Announces during season
        PostSeason,             // After playoffs/finals
        Offseason,              // During summer
        Surprise,               // Sudden, unexpected
        Gradual                 // Hinted at, then confirmed
    }

    /// <summary>
    /// Farewell tour event type
    /// </summary>
    [Serializable]
    public enum FarewellEventType
    {
        PreGameTribute,         // Video tribute before tipoff
        HalftimePresentation,   // Mid-game recognition
        PostGameCeremony,       // After game gift presentation
        JerseyPresentaion,      // Special jersey from opponent
        StandingOvation,        // Crowd recognition
        FormerTeammateVisit,    // Old teammates attend
        RetiredJerseyPreview    // If jersey will be retired
    }

    /// <summary>
    /// Retirement decision factors
    /// </summary>
    [Serializable]
    public class RetirementFactors
    {
        public string PlayerId;

        [Range(0f, 1f)] public float AgeDeclineFactor;
        [Range(0f, 1f)] public float InjuryHistoryFactor;
        [Range(0f, 1f)] public float PerformanceDeclineFactor;
        [Range(0f, 1f)] public float MotivationFactor;
        [Range(0f, 1f)] public float FamilyConsiderations;
        [Range(0f, 1f)] public float FinancialSecurity;
        [Range(0f, 1f)] public float ChampionshipAchieved;
        [Range(0f, 1f)] public float TeamSituation;

        public float OverallRetirementProbability =>
            (AgeDeclineFactor * 0.25f) +
            (InjuryHistoryFactor * 0.15f) +
            (PerformanceDeclineFactor * 0.2f) +
            (1f - MotivationFactor) * 0.15f +
            (FamilyConsiderations * 0.1f) +
            (FinancialSecurity * 0.05f) +
            (ChampionshipAchieved * 0.05f) +
            ((1f - TeamSituation) * 0.05f);
    }

    /// <summary>
    /// Player career statistics summary for retirement
    /// </summary>
    [Serializable]
    public class CareerSummary
    {
        public string PlayerId;
        public string PlayerName;

        [Header("Career Totals")]
        public int SeasonsPlayed;
        public int GamesPlayed;
        public int TotalPoints;
        public int TotalRebounds;
        public int TotalAssists;
        public int TotalSteals;
        public int TotalBlocks;

        [Header("Career Averages")]
        public float PointsPerGame;
        public float ReboundsPerGame;
        public float AssistsPerGame;

        [Header("Accolades")]
        public int Championships;
        public int FinalsMvps;
        public int RegularSeasonMvps;
        public int AllStarSelections;
        public int AllNbaTeams;
        public int AllDefensiveTeams;
        public List<string> OtherAwards = new List<string>();

        [Header("Records")]
        public List<string> RecordsHeld = new List<string>();
        public List<string> FranchiseRecords = new List<string>();

        [Header("Teams")]
        public List<string> TeamsPlayedFor = new List<string>();
        public string MostAssociatedTeam;
        public int YearsWithMainTeam;

        [Header("Legacy")]
        public LegacyTier LegacyTier;
        public int ProjectedHOFBallot;      // Years until HOF eligible
        public float HOFProbability;
    }

    /// <summary>
    /// Farewell tour game event
    /// </summary>
    [Serializable]
    public class FarewellTourGame
    {
        public string GameId;
        public DateTime GameDate;
        public string OpponentTeamId;
        public string ArenaName;
        public string CityName;
        public bool IsFormerTeam;
        public bool IsHometown;
        public bool IsRivalry;

        [Header("Events")]
        public List<FarewellEventType> PlannedEvents = new List<FarewellEventType>();
        public List<string> SpecialGuests = new List<string>();

        [Header("Gifts")]
        public string GiftDescription;
        public bool ReceivedJersey;
        public bool ReceivedArtwork;
        public bool ReceivedDonation;       // Donation in player's name

        [Header("Crowd Reaction")]
        public float CrowdIntensity;        // 0-1
        public bool ReceivedStandingOvation;
        public int OvationDurationSeconds;

        [Header("Player Performance")]
        public int PointsScored;
        public int Rebounds;
        public int Assists;
        public bool MemorablePerformance;
        public string PerformanceHighlight;
    }

    /// <summary>
    /// Retirement ceremony details
    /// </summary>
    [Serializable]
    public class RetirementCeremony
    {
        public string CeremonyId;
        public string PlayerId;
        public string PlayerName;
        public DateTime CeremonyDate;
        public string TeamId;
        public string ArenaName;

        [Header("Jersey Retirement")]
        public bool JerseyRetired;
        public string JerseyNumber;
        public bool NumberRetiredLeagueWide;    // Very rare

        [Header("Ceremony Details")]
        public List<string> Speakers = new List<string>();
        public List<string> FormerTeammatesPresent = new List<string>();
        public List<string> FamilyMembersHonored = new List<string>();
        public bool IncludesVideoTribute;
        public int VideoDurationMinutes;
        public bool IncludesStatueUnveiling;

        [Header("Speech")]
        public string SpeechHighlight;
        public List<string> PeopleThanked = new List<string>();
        public bool EmotionalMoment;
        public string EmotionalMomentDescription;

        [Header("Special Moments")]
        public List<string> SpecialMoments = new List<string>();
        public bool SurpriseGuest;
        public string SurpriseGuestName;

        [Header("Media Coverage")]
        public bool NationallyTelevised;
        public int AttendanceCount;
        public float MediaRatingBoost;
    }

    /// <summary>
    /// Complete retirement package for a player
    /// </summary>
    [Serializable]
    public class RetirementPackage
    {
        public string PlayerId;
        public string PlayerName;
        public LegacyTier LegacyTier;
        public RetirementAnnouncementType AnnouncementType;
        public DateTime AnnouncementDate;
        public DateTime FinalGameDate;

        [Header("Farewell Tour")]
        public bool HasFarewellTour;
        public List<FarewellTourGame> FarewellTourGames = new List<FarewellTourGame>();
        public int TotalGiftsReceived;
        public float TourFanEngagement;

        [Header("Final Season Stats")]
        public float FinalSeasonPPG;
        public float FinalSeasonRPG;
        public float FinalSeasonAPG;
        public int FinalSeasonGames;

        [Header("Ceremony")]
        public RetirementCeremony Ceremony;

        [Header("Post-Retirement")]
        public string PostRetirementRole;   // Coach, Analyst, Front Office, etc.
        public bool ImmediateHOFInduction;
        public bool JerseyRetired;
        public List<string> TeamsRetiringJersey = new List<string>();
    }

    /// <summary>
    /// Retirement announcement content
    /// </summary>
    [Serializable]
    public class RetirementAnnouncement
    {
        public string PlayerId;
        public string PlayerName;
        public DateTime Date;
        public RetirementAnnouncementType Type;
        public string Headline;
        public string FullStatement;
        public List<string> QuotesFromPeers = new List<string>();
        public List<string> MediaReactions = new List<string>();
    }

    /// <summary>
    /// Manages player retirements, farewell tours, and ceremonies
    /// </summary>
    public class RetirementManager : MonoBehaviour
    {
        public static RetirementManager Instance { get; private set; }

        [Header("Active Retirements")]
        [SerializeField] private List<RetirementPackage> activeRetirements = new List<RetirementPackage>();
        [SerializeField] private List<RetirementPackage> completedRetirements = new List<RetirementPackage>();

        [Header("Pending Evaluations")]
        [SerializeField] private List<RetirementFactors> pendingEvaluations = new List<RetirementFactors>();

        [Header("Settings")]
        [SerializeField, Range(32, 45)] private int minimumRetirementAge = 34;
        [SerializeField, Range(0f, 1f)] private float legendFarewellTourChance = 0.9f;
        [SerializeField, Range(0f, 1f)] private float starFarewellTourChance = 0.5f;

        // Events
        public event Action<RetirementAnnouncement> OnRetirementAnnounced;
        public event Action<FarewellTourGame> OnFarewellGameCompleted;
        public event Action<RetirementCeremony> OnRetirementCeremonyHeld;
        public event Action<RetirementPackage> OnRetirementComplete;

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
        /// Evaluate all eligible players for potential retirement
        /// Called at end of each season
        /// </summary>
        public List<RetirementFactors> EvaluateRetirements(List<PlayerCareerData> players)
        {
            pendingEvaluations.Clear();

            foreach (var player in players.Where(p => p.Age >= minimumRetirementAge))
            {
                var factors = CalculateRetirementFactors(player);
                pendingEvaluations.Add(factors);

                // Check if retirement is likely
                if (factors.OverallRetirementProbability > 0.5f)
                {
                    if (UnityEngine.Random.value < factors.OverallRetirementProbability)
                    {
                        InitiateRetirement(player);
                    }
                }
            }

            return pendingEvaluations;
        }

        private RetirementFactors CalculateRetirementFactors(PlayerCareerData player)
        {
            var factors = new RetirementFactors
            {
                PlayerId = player.PlayerId
            };

            // Age factor - increases sharply after 36
            factors.AgeDeclineFactor = player.Age switch
            {
                >= 40 => 0.95f,
                >= 38 => 0.75f,
                >= 36 => 0.5f,
                >= 34 => 0.25f,
                _ => 0.1f
            };

            // Injury history
            factors.InjuryHistoryFactor = Mathf.Clamp01(player.CareerInjuries * 0.1f);

            // Performance decline (compare current to peak)
            if (player.PeakRating > 0)
            {
                float declineRatio = 1f - (player.CurrentRating / player.PeakRating);
                factors.PerformanceDeclineFactor = Mathf.Clamp01(declineRatio);
            }

            // Motivation - decreased by consecutive losing seasons
            factors.MotivationFactor = Mathf.Clamp01(1f - (player.ConsecutiveLosing​Seasons * 0.15f));

            // Family considerations - increases with age
            factors.FamilyConsiderations = Mathf.Clamp01((player.Age - 32) * 0.1f);

            // Financial security - based on career earnings
            factors.FinancialSecurity = player.CareerEarnings > 100f ? 1f :
                                        player.CareerEarnings > 50f ? 0.7f : 0.4f;

            // Championship achieved reduces urgency to continue
            factors.ChampionshipAchieved = player.Championships > 0 ? 0.8f : 0.2f;

            // Team situation
            factors.TeamSituation = player.OnContender ? 1f : 0.4f;

            return factors;
        }

        /// <summary>
        /// Initiate retirement process for a player
        /// </summary>
        public RetirementPackage InitiateRetirement(PlayerCareerData player)
        {
            var careerSummary = GenerateCareerSummary(player);
            var legacyTier = DetermineLegacyTier(careerSummary);

            var package = new RetirementPackage
            {
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                LegacyTier = legacyTier,
                AnnouncementType = DetermineAnnouncementType(legacyTier),
                AnnouncementDate = DateTime.Now
            };

            // Generate retirement announcement
            var announcement = GenerateAnnouncement(player, package);
            OnRetirementAnnounced?.Invoke(announcement);

            // Determine if farewell tour
            package.HasFarewellTour = ShouldHaveFarewellTour(legacyTier);

            if (package.HasFarewellTour)
            {
                package.FarewellTourGames = GenerateFarewellTour(player, careerSummary);
            }

            activeRetirements.Add(package);
            return package;
        }

        private CareerSummary GenerateCareerSummary(PlayerCareerData player)
        {
            return new CareerSummary
            {
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                SeasonsPlayed = player.SeasonsPlayed,
                GamesPlayed = player.GamesPlayed,
                TotalPoints = player.CareerPoints,
                TotalRebounds = player.CareerRebounds,
                TotalAssists = player.CareerAssists,
                TotalSteals = player.CareerSteals,
                TotalBlocks = player.CareerBlocks,
                PointsPerGame = player.GamesPlayed > 0 ? (float)player.CareerPoints / player.GamesPlayed : 0,
                ReboundsPerGame = player.GamesPlayed > 0 ? (float)player.CareerRebounds / player.GamesPlayed : 0,
                AssistsPerGame = player.GamesPlayed > 0 ? (float)player.CareerAssists / player.GamesPlayed : 0,
                Championships = player.Championships,
                FinalsMvps = player.FinalsMvps,
                RegularSeasonMvps = player.Mvps,
                AllStarSelections = player.AllStarSelections,
                AllNbaTeams = player.AllNbaSelections,
                AllDefensiveTeams = player.AllDefensiveSelections,
                TeamsPlayedFor = player.TeamsPlayedFor,
                MostAssociatedTeam = player.PrimaryTeam,
                YearsWithMainTeam = player.YearsWithPrimaryTeam
            };
        }

        private LegacyTier DetermineLegacyTier(CareerSummary summary)
        {
            // Hall of Fame caliber
            if (summary.RegularSeasonMvps >= 1 ||
                (summary.AllStarSelections >= 8 && summary.Championships >= 2) ||
                (summary.AllNbaTeams >= 6 && summary.AllStarSelections >= 6))
            {
                return LegacyTier.HallOfFame;
            }

            // All-Time tier
            if (summary.AllStarSelections >= 5 ||
                (summary.AllNbaTeams >= 3 && summary.Championships >= 1))
            {
                return LegacyTier.AllTimeTier;
            }

            // Star player
            if (summary.AllStarSelections >= 2 || summary.AllNbaTeams >= 1)
            {
                return LegacyTier.StarPlayer;
            }

            // Solid career
            if (summary.SeasonsPlayed >= 10 && summary.PointsPerGame >= 10f)
            {
                return LegacyTier.SolidCareer;
            }

            // Role player
            if (summary.SeasonsPlayed >= 5)
            {
                return LegacyTier.RolePlayers;
            }

            return LegacyTier.BriefCareer;
        }

        private RetirementAnnouncementType DetermineAnnouncementType(LegacyTier tier)
        {
            // Legends typically announce pre-season for farewell tour
            if (tier == LegacyTier.HallOfFame || tier == LegacyTier.AllTimeTier)
            {
                return UnityEngine.Random.value < 0.7f
                    ? RetirementAnnouncementType.PreSeason
                    : RetirementAnnouncementType.MidSeason;
            }

            // Stars may announce mid-season or post-season
            if (tier == LegacyTier.StarPlayer)
            {
                float roll = UnityEngine.Random.value;
                if (roll < 0.3f) return RetirementAnnouncementType.PreSeason;
                if (roll < 0.6f) return RetirementAnnouncementType.MidSeason;
                return RetirementAnnouncementType.PostSeason;
            }

            // Others typically announce in offseason or after season
            return UnityEngine.Random.value < 0.5f
                ? RetirementAnnouncementType.PostSeason
                : RetirementAnnouncementType.Offseason;
        }

        private bool ShouldHaveFarewellTour(LegacyTier tier)
        {
            return tier switch
            {
                LegacyTier.HallOfFame => UnityEngine.Random.value < legendFarewellTourChance,
                LegacyTier.AllTimeTier => UnityEngine.Random.value < legendFarewellTourChance * 0.9f,
                LegacyTier.StarPlayer => UnityEngine.Random.value < starFarewellTourChance,
                _ => false
            };
        }

        private RetirementAnnouncement GenerateAnnouncement(PlayerCareerData player, RetirementPackage package)
        {
            var announcement = new RetirementAnnouncement
            {
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                Date = package.AnnouncementDate,
                Type = package.AnnouncementType
            };

            // Generate headline based on legacy
            announcement.Headline = package.LegacyTier switch
            {
                LegacyTier.HallOfFame => $"Legend {player.FullName} announces retirement after historic career",
                LegacyTier.AllTimeTier => $"All-Star {player.FullName} to retire at season's end",
                LegacyTier.StarPlayer => $"{player.FullName} announces retirement after {player.SeasonsPlayed} seasons",
                _ => $"Veteran {player.FullName} calls it a career"
            };

            // Generate statement excerpt
            announcement.FullStatement = GenerateRetirementStatement(player, package);

            // Peer quotes
            announcement.QuotesFromPeers = GeneratePeerQuotes(player, package.LegacyTier);

            // Media reactions
            announcement.MediaReactions = GenerateMediaReactions(player, package.LegacyTier);

            return announcement;
        }

        private string GenerateRetirementStatement(PlayerCareerData player, RetirementPackage package)
        {
            var statements = new List<string>();

            statements.Add($"After {player.SeasonsPlayed} incredible years in this league, " +
                          "I've decided to hang up my sneakers.");

            if (player.Championships > 0)
            {
                statements.Add($"Winning {player.Championships} championship{(player.Championships > 1 ? "s" : "")} " +
                              "was a dream come true.");
            }

            statements.Add("I want to thank my family, teammates, coaches, and especially the fans " +
                          "who supported me throughout this journey.");

            if (package.HasFarewellTour)
            {
                statements.Add("I'm looking forward to saying goodbye to every arena " +
                              "that welcomed me over the years.");
            }

            return string.Join(" ", statements);
        }

        private List<string> GeneratePeerQuotes(PlayerCareerData player, LegacyTier tier)
        {
            var quotes = new List<string>();

            if (tier <= LegacyTier.AllTimeTier)
            {
                quotes.Add($"\"One of the greatest to ever do it. The game loses a true icon.\" - Former teammate");
                quotes.Add($"\"Competed against him for years. Nothing but respect.\" - Rival player");
            }
            else
            {
                quotes.Add($"\"{player.FullName} was a true professional every single day.\" - Former teammate");
            }

            return quotes;
        }

        private List<string> GenerateMediaReactions(PlayerCareerData player, LegacyTier tier)
        {
            var reactions = new List<string>();

            if (tier == LegacyTier.HallOfFame)
            {
                reactions.Add("First-ballot Hall of Famer closes legendary career");
                reactions.Add("Breaking down his impact on the game");
            }
            else if (tier == LegacyTier.AllTimeTier)
            {
                reactions.Add("Hall of Fame case: Where does he rank all-time?");
            }

            reactions.Add($"Looking back at {player.FullName}'s best moments");

            return reactions;
        }

        /// <summary>
        /// Generate farewell tour schedule
        /// </summary>
        private List<FarewellTourGame> GenerateFarewellTour(PlayerCareerData player, CareerSummary summary)
        {
            var tour = new List<FarewellTourGame>();

            // Games at former teams
            foreach (var teamId in summary.TeamsPlayedFor.Where(t => t != player.CurrentTeamId))
            {
                var game = new FarewellTourGame
                {
                    GameId = Guid.NewGuid().ToString(),
                    OpponentTeamId = teamId,
                    IsFormerTeam = true,
                    CrowdIntensity = 0.9f
                };

                // Former teams give special treatment
                game.PlannedEvents.Add(FarewellEventType.PreGameTribute);
                game.PlannedEvents.Add(FarewellEventType.PostGameCeremony);
                game.PlannedEvents.Add(FarewellEventType.JerseyPresentaion);
                game.SpecialGuests.Add("Former teammates from that era");
                game.ReceivedJersey = true;

                tour.Add(game);
            }

            // Add significant rivalry games
            var rivalTeams = GetRivalTeams(player.CurrentTeamId);
            foreach (var rival in rivalTeams.Take(3))
            {
                if (!tour.Any(g => g.OpponentTeamId == rival))
                {
                    tour.Add(new FarewellTourGame
                    {
                        GameId = Guid.NewGuid().ToString(),
                        OpponentTeamId = rival,
                        IsRivalry = true,
                        CrowdIntensity = 0.85f,
                        PlannedEvents = new List<FarewellEventType>
                        {
                            FarewellEventType.PreGameTribute,
                            FarewellEventType.StandingOvation
                        }
                    });
                }
            }

            return tour;
        }

        /// <summary>
        /// Process a farewell tour game
        /// </summary>
        public void ProcessFarewellGame(FarewellTourGame game, int pointsScored, int rebounds, int assists)
        {
            game.PointsScored = pointsScored;
            game.Rebounds = rebounds;
            game.Assists = assists;

            // Determine if memorable performance
            if (pointsScored >= 25 || (pointsScored >= 15 && assists >= 10))
            {
                game.MemorablePerformance = true;
                game.PerformanceHighlight = pointsScored >= 30
                    ? $"Vintage performance! {pointsScored} points in emotional night"
                    : $"Classic showing with {pointsScored}/{rebounds}/{assists} line";
            }

            // Standing ovation
            if (game.CrowdIntensity > 0.7f || game.IsFormerTeam)
            {
                game.ReceivedStandingOvation = true;
                game.OvationDurationSeconds = UnityEngine.Random.Range(45, 180);
            }

            OnFarewellGameCompleted?.Invoke(game);
        }

        /// <summary>
        /// Generate and hold retirement ceremony
        /// </summary>
        public RetirementCeremony HoldRetirementCeremony(RetirementPackage package, PlayerCareerData player)
        {
            var ceremony = new RetirementCeremony
            {
                CeremonyId = Guid.NewGuid().ToString(),
                PlayerId = package.PlayerId,
                PlayerName = package.PlayerName,
                CeremonyDate = DateTime.Now,
                TeamId = player.PrimaryTeam,
                ArenaName = GetArenaName(player.PrimaryTeam)
            };

            // Jersey retirement
            ceremony.JerseyRetired = package.LegacyTier <= LegacyTier.AllTimeTier;
            if (ceremony.JerseyRetired)
            {
                ceremony.JerseyNumber = player.JerseyNumber;
                package.JerseyRetired = true;
                package.TeamsRetiringJersey.Add(player.PrimaryTeam);
            }

            // League-wide jersey retirement (very rare - like 6, 23)
            ceremony.NumberRetiredLeagueWide = package.LegacyTier == LegacyTier.HallOfFame &&
                                               player.AllStarSelections >= 15;

            // Speakers
            ceremony.Speakers.Add("Team Owner");
            ceremony.Speakers.Add("General Manager");
            if (package.LegacyTier <= LegacyTier.StarPlayer)
            {
                ceremony.Speakers.Add("NBA Commissioner (via video)");
                ceremony.Speakers.Add("Former Head Coach");
            }

            // Former teammates
            int teammatesCount = package.LegacyTier switch
            {
                LegacyTier.HallOfFame => UnityEngine.Random.Range(8, 15),
                LegacyTier.AllTimeTier => UnityEngine.Random.Range(5, 10),
                _ => UnityEngine.Random.Range(2, 5)
            };
            for (int i = 0; i < teammatesCount; i++)
            {
                ceremony.FormerTeammatesPresent.Add($"Former Teammate {i + 1}");
            }

            // Family
            ceremony.FamilyMembersHonored.Add("Spouse");
            ceremony.FamilyMembersHonored.Add("Children");
            ceremony.FamilyMembersHonored.Add("Parents");

            // Video tribute
            ceremony.IncludesVideoTribute = true;
            ceremony.VideoDurationMinutes = package.LegacyTier switch
            {
                LegacyTier.HallOfFame => UnityEngine.Random.Range(15, 25),
                LegacyTier.AllTimeTier => UnityEngine.Random.Range(10, 18),
                _ => UnityEngine.Random.Range(5, 12)
            };

            // Statue
            ceremony.IncludesStatueUnveiling = package.LegacyTier == LegacyTier.HallOfFame &&
                                               player.YearsWithPrimaryTeam >= 10;

            // Speech highlights
            ceremony.SpeechHighlight = GenerateSpeechHighlight(player, package);
            ceremony.PeopleThanked = GeneratePeopleThanked(player);

            // Emotional moment
            ceremony.EmotionalMoment = true;
            ceremony.EmotionalMomentDescription = "Tears flow as the arena gives one final standing ovation";

            // Surprise guest chance for legends
            if (package.LegacyTier <= LegacyTier.AllTimeTier && UnityEngine.Random.value < 0.4f)
            {
                ceremony.SurpriseGuest = true;
                ceremony.SurpriseGuestName = "Legend from rival team";
                ceremony.SpecialMoments.Add($"Surprise appearance brings crowd to their feet");
            }

            // Media coverage
            ceremony.NationallyTelevised = package.LegacyTier <= LegacyTier.StarPlayer;
            ceremony.AttendanceCount = UnityEngine.Random.Range(18000, 21000);

            package.Ceremony = ceremony;
            OnRetirementCeremonyHeld?.Invoke(ceremony);

            return ceremony;
        }

        private string GenerateSpeechHighlight(PlayerCareerData player, RetirementPackage package)
        {
            if (player.Championships > 0)
            {
                return $"\"Bringing a championship to this city was the greatest moment of my life.\"";
            }
            if (package.LegacyTier <= LegacyTier.AllTimeTier)
            {
                return $"\"Every time I stepped on that court, I gave everything I had for this jersey.\"";
            }
            return $"\"Thank you for believing in me when no one else did.\"";
        }

        private List<string> GeneratePeopleThanked(PlayerCareerData player)
        {
            return new List<string>
            {
                "Family for unwavering support",
                "Teammates who became brothers",
                "Coaches who pushed me to be better",
                "Training staff who kept me on the court",
                "The fans - you made every moment special",
                "The organization for taking a chance on me"
            };
        }

        /// <summary>
        /// Complete retirement and archive
        /// </summary>
        public void FinalizeRetirement(RetirementPackage package)
        {
            // Determine post-retirement role
            package.PostRetirementRole = DeterminePostRetirementRole(package);

            // HOF status
            if (package.LegacyTier == LegacyTier.HallOfFame)
            {
                package.ImmediateHOFInduction = true;
            }

            activeRetirements.Remove(package);
            completedRetirements.Add(package);

            OnRetirementComplete?.Invoke(package);
        }

        private string DeterminePostRetirementRole(RetirementPackage package)
        {
            float roll = UnityEngine.Random.value;

            if (roll < 0.25f) return "Television Analyst";
            if (roll < 0.4f) return "Assistant Coach";
            if (roll < 0.5f) return "Front Office Advisor";
            if (roll < 0.6f) return "Player Development";
            if (roll < 0.7f) return "Team Ambassador";
            return "Private Life";
        }

        private List<string> GetRivalTeams(string teamId)
        {
            // Would integrate with actual team rivalries
            return new List<string> { "RIVAL_1", "RIVAL_2", "RIVAL_3" };
        }

        private string GetArenaName(string teamId)
        {
            // Would integrate with team data
            return "Home Arena";
        }

        /// <summary>
        /// Get all active farewell tours
        /// </summary>
        public List<RetirementPackage> GetActiveFarewellTours()
        {
            return activeRetirements.Where(r => r.HasFarewellTour).ToList();
        }

        /// <summary>
        /// Get retired player's legacy summary
        /// </summary>
        public RetirementPackage GetRetirementDetails(string playerId)
        {
            return completedRetirements.FirstOrDefault(r => r.PlayerId == playerId);
        }

        /// <summary>
        /// Get all Hall of Fame caliber retirements
        /// </summary>
        public List<RetirementPackage> GetHallOfFamers()
        {
            return completedRetirements.Where(r => r.LegacyTier == LegacyTier.HallOfFame).ToList();
        }
    }

    /// <summary>
    /// Placeholder for player career data
    /// </summary>
    [Serializable]
    public class PlayerCareerData
    {
        public string PlayerId;
        public string FullName;
        public int Age;
        public int SeasonsPlayed;
        public int GamesPlayed;
        public int CareerPoints;
        public int CareerRebounds;
        public int CareerAssists;
        public int CareerSteals;
        public int CareerBlocks;
        public int Championships;
        public int FinalsMvps;
        public int Mvps;
        public int AllStarSelections;
        public int AllNbaSelections;
        public int AllDefensiveSelections;
        public List<string> TeamsPlayedFor = new List<string>();
        public string PrimaryTeam;
        public string CurrentTeamId;
        public int YearsWithPrimaryTeam;
        public string JerseyNumber;
        public float CurrentRating;
        public float PeakRating;
        public int CareerInjuries;
        public int ConsecutiveLosing​Seasons;
        public float CareerEarnings;
        public bool OnContender;
    }
}
