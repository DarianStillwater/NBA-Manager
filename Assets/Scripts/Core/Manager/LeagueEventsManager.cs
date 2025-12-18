using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Type of major league event
    /// </summary>
    [Serializable]
    public enum LeagueEventType
    {
        Expansion,              // New team added
        Relocation,             // Team moves cities
        Rebranding,             // Team changes name/colors
        ArenaChange,            // New arena
        OwnershipChange,        // Team sold
        ConferenceRealignment,  // Teams switch conferences
        RuleChange,             // Significant rule modification
        CBAAgreement,           // New CBA signed
        InternationalGame,      // Regular season game abroad
        AllStarLocation,        // All-Star weekend location
        DraftLotteryReform      // Lottery odds change
    }

    /// <summary>
    /// Expansion team setup status
    /// </summary>
    [Serializable]
    public enum ExpansionPhase
    {
        Announced,              // Expansion announced
        OwnerSelected,          // Ownership group chosen
        CitySelected,           // City confirmed
        NameRevealed,           // Team name/branding
        ArenaPlanned,           // Arena plans finalized
        ExpansionDraft,         // Player selection
        FirstSeason,            // Inaugural season
        Established             // Full member
    }

    /// <summary>
    /// Relocation status
    /// </summary>
    [Serializable]
    public enum RelocationPhase
    {
        RumorsStarted,
        OwnerSeekingMove,
        CityNegotiations,
        LeagueApproval,
        Approved,
        MovingOperations,
        Completed,
        Blocked                 // League rejected
    }

    /// <summary>
    /// Potential expansion city
    /// </summary>
    [Serializable]
    public class ExpansionCity
    {
        public string CityName;
        public string StateName;
        public string MetroArea;
        public int MetroPopulation;
        public string ProposedArena;
        public int ArenaCapacity;
        public float MarketViability;       // 0-1
        public float FanbaseProjection;     // 0-1
        public float CorporateSupportScore; // 0-1
        public bool HasNBAHistory;          // Former team
        public string FormerTeamName;
        public List<string> CompetingTeams = new List<string>();  // Other pro sports
        public float OverallScore => (MarketViability + FanbaseProjection + CorporateSupportScore) / 3f;
    }

    /// <summary>
    /// Expansion team details
    /// </summary>
    [Serializable]
    public class ExpansionTeam
    {
        public string TeamId;
        public string TeamName;
        public string Nickname;
        public string City;
        public string Conference;
        public string Division;

        [Header("Ownership")]
        public string PrimaryOwner;
        public float OwnerNetWorth;         // Billions
        public string OwnershipGroup;

        [Header("Branding")]
        public string PrimaryColor;
        public string SecondaryColor;
        public string Mascot;
        public string ArenaName;
        public int ArenaCapacity;

        [Header("Setup")]
        public ExpansionPhase CurrentPhase;
        public int ExpansionYear;
        public float ExpansionFee;          // Billions
        public DateTime FirstGameDate;

        [Header("Roster")]
        public List<string> ExpansionDraftPicks = new List<string>();
        public List<int> DraftPicksReceived = new List<int>();
        public int CapSpace;

        [Header("Projections")]
        public int ProjectedWinsYear1;
        public int YearsToCompetitiveness;
    }

    /// <summary>
    /// Expansion draft selection
    /// </summary>
    [Serializable]
    public class ExpansionDraftPick
    {
        public int PickNumber;
        public string PlayerId;
        public string PlayerName;
        public string FormerTeamId;
        public int Salary;
        public int ContractYearsRemaining;
        public string Position;
    }

    /// <summary>
    /// Expansion draft rules for each team
    /// </summary>
    [Serializable]
    public class ExpansionDraftProtection
    {
        public string TeamId;
        public List<string> ProtectedPlayerIds = new List<string>();
        public int ProtectionSlots;         // Usually 8 or 15
        public bool UsedFullProtection;
        public string PlayerLost;           // Player taken in expansion
    }

    /// <summary>
    /// Team relocation details
    /// </summary>
    [Serializable]
    public class TeamRelocation
    {
        public string TeamId;
        public string OriginalCity;
        public string OriginalName;
        public string NewCity;
        public string NewName;
        public string NewNickname;

        [Header("Status")]
        public RelocationPhase Phase;
        public DateTime AnnouncementDate;
        public DateTime EffectiveDate;
        public int MovingSeason;

        [Header("Reasons")]
        public List<string> RelocationReasons = new List<string>();
        public bool ArenaIssues;
        public bool AttendanceIssues;
        public bool OwnerRequest;
        public bool CityFundingRefused;

        [Header("New Location")]
        public string NewArena;
        public int NewArenaCapacity;
        public bool NewArenaUnderConstruction;

        [Header("Approval")]
        public int OwnersVotedFor;
        public int OwnersVotedAgainst;
        public bool LeagueApproved;
        public string CommissionerStatement;

        [Header("Fan Reaction")]
        public float OriginalCityAnger;     // 0-1
        public float NewCityExcitement;     // 0-1
        public List<string> MediaReactions = new List<string>();
    }

    /// <summary>
    /// Team rebranding (name change, colors, etc.)
    /// </summary>
    [Serializable]
    public class TeamRebranding
    {
        public string TeamId;
        public DateTime EffectiveDate;

        [Header("Name Change")]
        public bool NameChanged;
        public string OldName;
        public string NewName;

        [Header("Visual Identity")]
        public bool ColorsChanged;
        public string OldPrimaryColor;
        public string NewPrimaryColor;
        public bool LogoChanged;
        public bool UniformsChanged;

        [Header("Reasoning")]
        public string RebrandReason;
        public bool FanDriven;
        public bool OwnerDriven;

        [Header("Reception")]
        public float FanApproval;           // 0-1
        public float MerchandiseSalesChange; // Percentage
    }

    /// <summary>
    /// Conference/Division realignment
    /// </summary>
    [Serializable]
    public class ConferenceRealignment
    {
        public int EffectiveSeason;
        public List<DivisionChange> Changes = new List<DivisionChange>();
        public string RealignmentReason;
        public bool DueToExpansion;
    }

    [Serializable]
    public class DivisionChange
    {
        public string TeamId;
        public string OldConference;
        public string NewConference;
        public string OldDivision;
        public string NewDivision;
    }

    /// <summary>
    /// Major league rule change
    /// </summary>
    [Serializable]
    public class RuleChange
    {
        public string RuleId;
        public int ImplementationSeason;
        public string RuleName;
        public string Description;
        public string OldRule;
        public string NewRule;
        public string ImpactAnalysis;
        public List<string> TeamsAffectedMost = new List<string>();
    }

    /// <summary>
    /// Historical league event record
    /// </summary>
    [Serializable]
    public class LeagueHistoricalEvent
    {
        public string EventId;
        public LeagueEventType Type;
        public int Season;
        public DateTime Date;
        public string Headline;
        public string Description;
        public List<string> InvolvedTeamIds = new List<string>();
        public float LeagueImpact;          // 0-1
    }

    /// <summary>
    /// Manages major league events: Expansion, Relocation, Rebranding
    /// </summary>
    public class LeagueEventsManager : MonoBehaviour
    {
        public static LeagueEventsManager Instance { get; private set; }

        [Header("Active Events")]
        [SerializeField] private List<ExpansionTeam> pendingExpansions = new List<ExpansionTeam>();
        [SerializeField] private List<TeamRelocation> pendingRelocations = new List<TeamRelocation>();
        [SerializeField] private List<TeamRebranding> pendingRebrands = new List<TeamRebranding>();

        [Header("Completed Events")]
        [SerializeField] private List<ExpansionTeam> completedExpansions = new List<ExpansionTeam>();
        [SerializeField] private List<TeamRelocation> completedRelocations = new List<TeamRelocation>();
        [SerializeField] private List<LeagueHistoricalEvent> leagueHistory = new List<LeagueHistoricalEvent>();

        [Header("Expansion Cities Pool")]
        [SerializeField] private List<ExpansionCity> potentialExpansionCities = new List<ExpansionCity>();

        [Header("Configuration")]
        [SerializeField] private int currentTeamCount = 30;
        [SerializeField] private int maxTeams = 32;
        [SerializeField, Range(0f, 0.05f)] private float yearlyExpansionChance = 0.02f;
        [SerializeField, Range(0f, 0.1f)] private float yearlyRelocationChance = 0.03f;

        [Header("Expansion Draft Settings")]
        [SerializeField] private int protectedPlayersPerTeam = 8;
        [SerializeField] private int maxPlayersPerTeamTaken = 1;
        [SerializeField] private int totalExpansionDraftPicks = 30;

        // Events
        public event Action<ExpansionTeam> OnExpansionAnnounced;
        public event Action<ExpansionTeam> OnExpansionTeamEstablished;
        public event Action<TeamRelocation> OnRelocationAnnounced;
        public event Action<TeamRelocation> OnRelocationCompleted;
        public event Action<TeamRebranding> OnRebrandingAnnounced;
        public event Action<List<ExpansionDraftPick>> OnExpansionDraftCompleted;
        public event Action<LeagueHistoricalEvent> OnMajorLeagueEvent;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeExpansionCities();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeExpansionCities()
        {
            potentialExpansionCities = new List<ExpansionCity>
            {
                new ExpansionCity {
                    CityName = "Seattle",
                    StateName = "Washington",
                    MetroPopulation = 4000000,
                    HasNBAHistory = true,
                    FormerTeamName = "SuperSonics",
                    MarketViability = 0.95f,
                    FanbaseProjection = 0.9f,
                    CorporateSupportScore = 0.85f,
                    ProposedArena = "Climate Pledge Arena"
                },
                new ExpansionCity {
                    CityName = "Las Vegas",
                    StateName = "Nevada",
                    MetroPopulation = 2800000,
                    HasNBAHistory = false,
                    MarketViability = 0.85f,
                    FanbaseProjection = 0.75f,
                    CorporateSupportScore = 0.9f,
                    ProposedArena = "T-Mobile Arena"
                },
                new ExpansionCity {
                    CityName = "Vancouver",
                    StateName = "British Columbia",
                    MetroPopulation = 2600000,
                    HasNBAHistory = true,
                    FormerTeamName = "Grizzlies",
                    MarketViability = 0.7f,
                    FanbaseProjection = 0.8f,
                    CorporateSupportScore = 0.65f
                },
                new ExpansionCity {
                    CityName = "Louisville",
                    StateName = "Kentucky",
                    MetroPopulation = 1400000,
                    HasNBAHistory = false,
                    MarketViability = 0.6f,
                    FanbaseProjection = 0.85f,
                    CorporateSupportScore = 0.5f
                },
                new ExpansionCity {
                    CityName = "Kansas City",
                    StateName = "Missouri",
                    MetroPopulation = 2200000,
                    HasNBAHistory = true,
                    FormerTeamName = "Kings",
                    MarketViability = 0.65f,
                    FanbaseProjection = 0.7f,
                    CorporateSupportScore = 0.6f
                },
                new ExpansionCity {
                    CityName = "Pittsburgh",
                    StateName = "Pennsylvania",
                    MetroPopulation = 2400000,
                    HasNBAHistory = false,
                    MarketViability = 0.6f,
                    FanbaseProjection = 0.65f,
                    CorporateSupportScore = 0.7f
                },
                new ExpansionCity {
                    CityName = "Mexico City",
                    StateName = "Mexico",
                    MetroPopulation = 21000000,
                    HasNBAHistory = false,
                    MarketViability = 0.75f,
                    FanbaseProjection = 0.85f,
                    CorporateSupportScore = 0.6f
                }
            };
        }

        /// <summary>
        /// Check for potential league events at start of season
        /// </summary>
        public void CheckForLeagueEvents(int season)
        {
            // Check expansion possibility
            if (currentTeamCount < maxTeams && UnityEngine.Random.value < yearlyExpansionChance)
            {
                ConsiderExpansion(season);
            }

            // Check relocation rumors
            if (UnityEngine.Random.value < yearlyRelocationChance)
            {
                GenerateRelocationRumors();
            }

            // Process any pending events
            ProcessPendingExpansions(season);
            ProcessPendingRelocations(season);
        }

        #region Expansion

        /// <summary>
        /// Consider expansion to a new city
        /// </summary>
        private void ConsiderExpansion(int announcementSeason)
        {
            // Select best expansion city
            var topCities = potentialExpansionCities
                .OrderByDescending(c => c.OverallScore)
                .Take(3)
                .ToList();

            if (topCities.Count == 0) return;

            // Seattle gets priority due to history
            var selectedCity = topCities.FirstOrDefault(c => c.CityName == "Seattle")
                              ?? topCities[UnityEngine.Random.Range(0, topCities.Count)];

            AnnounceExpansion(selectedCity, announcementSeason);
        }

        /// <summary>
        /// Announce expansion to a specific city
        /// </summary>
        public ExpansionTeam AnnounceExpansion(ExpansionCity city, int announcementSeason)
        {
            var expansion = new ExpansionTeam
            {
                TeamId = $"EXP_{city.CityName.ToUpper().Replace(" ", "")}",
                City = city.CityName,
                CurrentPhase = ExpansionPhase.Announced,
                ExpansionYear = announcementSeason + 2,  // 2 years to setup
                ExpansionFee = 2.5f + UnityEngine.Random.Range(0f, 1f),  // $2.5-3.5 billion
                ArenaName = city.ProposedArena ?? $"{city.CityName} Arena",
                ArenaCapacity = city.ArenaCapacity > 0 ? city.ArenaCapacity : 18500
            };

            // Generate team identity
            expansion.TeamName = GenerateTeamName(city);
            expansion.Nickname = expansion.TeamName.Split(' ').Last();
            expansion.PrimaryOwner = GenerateOwnerName();
            expansion.OwnerNetWorth = UnityEngine.Random.Range(3f, 15f);

            // Conference assignment (balance)
            expansion.Conference = currentTeamCount % 2 == 0 ? "Eastern" : "Western";

            // Colors and branding
            var colors = GenerateTeamColors(city);
            expansion.PrimaryColor = colors.Item1;
            expansion.SecondaryColor = colors.Item2;

            // Projections
            expansion.ProjectedWinsYear1 = UnityEngine.Random.Range(18, 30);
            expansion.YearsToCompetitiveness = UnityEngine.Random.Range(4, 8);

            pendingExpansions.Add(expansion);
            potentialExpansionCities.Remove(city);

            // Record event
            RecordLeagueEvent(LeagueEventType.Expansion,
                $"NBA announces expansion to {city.CityName}",
                $"The {expansion.TeamName} will join the league in {expansion.ExpansionYear}",
                new List<string> { expansion.TeamId },
                0.9f);

            OnExpansionAnnounced?.Invoke(expansion);
            return expansion;
        }

        /// <summary>
        /// Process expansion phases
        /// </summary>
        private void ProcessPendingExpansions(int currentSeason)
        {
            foreach (var expansion in pendingExpansions.ToList())
            {
                if (currentSeason >= expansion.ExpansionYear)
                {
                    // Time for expansion draft and first season
                    if (expansion.CurrentPhase < ExpansionPhase.ExpansionDraft)
                    {
                        expansion.CurrentPhase = ExpansionPhase.ExpansionDraft;
                        // Expansion draft would be triggered separately
                    }
                    else if (expansion.CurrentPhase == ExpansionPhase.ExpansionDraft)
                    {
                        expansion.CurrentPhase = ExpansionPhase.FirstSeason;
                        currentTeamCount++;
                    }
                    else if (expansion.CurrentPhase == ExpansionPhase.FirstSeason)
                    {
                        expansion.CurrentPhase = ExpansionPhase.Established;
                        pendingExpansions.Remove(expansion);
                        completedExpansions.Add(expansion);
                        OnExpansionTeamEstablished?.Invoke(expansion);
                    }
                }
            }
        }

        /// <summary>
        /// Conduct expansion draft
        /// </summary>
        public List<ExpansionDraftPick> ConductExpansionDraft(ExpansionTeam expansion,
            List<ExpansionDraftProtection> teamProtections)
        {
            var picks = new List<ExpansionDraftPick>();
            var takenFromTeams = new Dictionary<string, int>();

            // Initialize counts
            foreach (var protection in teamProtections)
            {
                takenFromTeams[protection.TeamId] = 0;
            }

            // Select players from unprotected pool
            for (int pickNum = 1; pickNum <= totalExpansionDraftPicks; pickNum++)
            {
                // Find available players (not protected, team hasn't lost max)
                var availableTeams = teamProtections
                    .Where(p => takenFromTeams[p.TeamId] < maxPlayersPerTeamTaken)
                    .ToList();

                if (availableTeams.Count == 0) break;

                // Select random team and take player
                var selectedTeam = availableTeams[UnityEngine.Random.Range(0, availableTeams.Count)];

                var pick = new ExpansionDraftPick
                {
                    PickNumber = pickNum,
                    FormerTeamId = selectedTeam.TeamId,
                    PlayerId = $"PLAYER_EXP_{pickNum}",  // Would be actual unprotected player
                    PlayerName = $"Expansion Pick {pickNum}",
                    Position = GetRandomPosition(),
                    Salary = UnityEngine.Random.Range(2, 15),
                    ContractYearsRemaining = UnityEngine.Random.Range(1, 4)
                };

                picks.Add(pick);
                expansion.ExpansionDraftPicks.Add(pick.PlayerId);
                takenFromTeams[selectedTeam.TeamId]++;
                selectedTeam.PlayerLost = pick.PlayerId;
            }

            OnExpansionDraftCompleted?.Invoke(picks);
            return picks;
        }

        private string GenerateTeamName(ExpansionCity city)
        {
            // If returning city, may use old name
            if (city.HasNBAHistory && UnityEngine.Random.value < 0.7f)
            {
                return $"{city.CityName} {city.FormerTeamName}";
            }

            var nicknames = new[] {
                "Aces", "Aviators", "Barons", "Blaze", "Condors", "Coyotes",
                "Dragons", "Eclipse", "Emeralds", "Express", "Flames", "Flash",
                "Fury", "Guardians", "Hawks", "Hurricanes", "Ice", "Inferno",
                "Jets", "Knights", "Legends", "Lightning", "Monarchs", "Outlaws",
                "Phoenix", "Pride", "Raptors", "Reign", "Royals", "Rush",
                "Sharks", "Spirit", "Storm", "Surge", "Thunder", "Titans",
                "Vipers", "Wolves", "Zephyrs"
            };

            return $"{city.CityName} {nicknames[UnityEngine.Random.Range(0, nicknames.Length)]}";
        }

        private (string, string) GenerateTeamColors(ExpansionCity city)
        {
            var colorPairs = new[] {
                ("Navy Blue", "Silver"),
                ("Forest Green", "Gold"),
                ("Royal Purple", "White"),
                ("Crimson Red", "Black"),
                ("Teal", "Orange"),
                ("Maroon", "Cream"),
                ("Electric Blue", "Neon Green")
            };

            var pair = colorPairs[UnityEngine.Random.Range(0, colorPairs.Length)];
            return pair;
        }

        private string GenerateOwnerName()
        {
            var firstNames = new[] { "David", "Mark", "Robert", "Michael", "James", "William" };
            var lastNames = new[] { "Sterling", "Fertitta", "Ballmer", "Cuban", "Lacob", "Harris" };
            return $"{firstNames[UnityEngine.Random.Range(0, firstNames.Length)]} " +
                   $"{lastNames[UnityEngine.Random.Range(0, lastNames.Length)]}";
        }

        #endregion

        #region Relocation

        /// <summary>
        /// Generate relocation rumors for struggling teams
        /// </summary>
        private void GenerateRelocationRumors()
        {
            // Would integrate with team attendance/financial data
            // For now, creates placeholder relocation scenario
        }

        /// <summary>
        /// Initiate team relocation process
        /// </summary>
        public TeamRelocation InitiateRelocation(string teamId, string currentCity, string currentName,
            ExpansionCity newCity, List<string> reasons)
        {
            var relocation = new TeamRelocation
            {
                TeamId = teamId,
                OriginalCity = currentCity,
                OriginalName = currentName,
                NewCity = newCity.CityName,
                Phase = RelocationPhase.RumorsStarted,
                AnnouncementDate = DateTime.Now,
                RelocationReasons = reasons
            };

            // Determine new name
            if (newCity.HasNBAHistory)
            {
                relocation.NewName = $"{newCity.CityName} {newCity.FormerTeamName}";
                relocation.NewNickname = newCity.FormerTeamName;
            }
            else
            {
                relocation.NewName = $"{newCity.CityName} {currentName.Split(' ').Last()}";
                relocation.NewNickname = currentName.Split(' ').Last();
            }

            // Parse reasons
            relocation.ArenaIssues = reasons.Any(r => r.Contains("arena"));
            relocation.AttendanceIssues = reasons.Any(r => r.Contains("attendance"));
            relocation.OwnerRequest = reasons.Any(r => r.Contains("owner"));
            relocation.CityFundingRefused = reasons.Any(r => r.Contains("funding"));

            pendingRelocations.Add(relocation);

            RecordLeagueEvent(LeagueEventType.Relocation,
                $"Relocation rumors swirl around {currentName}",
                $"Reports suggest the team may move to {newCity.CityName}",
                new List<string> { teamId },
                0.7f);

            return relocation;
        }

        /// <summary>
        /// Process relocation through league approval
        /// </summary>
        public void ProcessRelocationVote(TeamRelocation relocation)
        {
            // Owners vote (need 3/4 majority)
            relocation.OwnersVotedFor = UnityEngine.Random.Range(15, 28);
            relocation.OwnersVotedAgainst = 30 - relocation.OwnersVotedFor;

            if (relocation.OwnersVotedFor >= 23)  // 3/4 of 30
            {
                relocation.LeagueApproved = true;
                relocation.Phase = RelocationPhase.Approved;
                relocation.CommissionerStatement =
                    $"After careful consideration, the league has approved the relocation to {relocation.NewCity}.";

                OnRelocationAnnounced?.Invoke(relocation);
            }
            else
            {
                relocation.LeagueApproved = false;
                relocation.Phase = RelocationPhase.Blocked;
                relocation.CommissionerStatement =
                    "The ownership group has voted against the proposed relocation.";

                pendingRelocations.Remove(relocation);
            }
        }

        /// <summary>
        /// Complete relocation process
        /// </summary>
        public void CompleteRelocation(TeamRelocation relocation, int newSeason)
        {
            relocation.Phase = RelocationPhase.Completed;
            relocation.EffectiveDate = DateTime.Now;
            relocation.MovingSeason = newSeason;

            // Fan reactions
            relocation.OriginalCityAnger = UnityEngine.Random.Range(0.7f, 1f);
            relocation.NewCityExcitement = UnityEngine.Random.Range(0.75f, 0.95f);

            relocation.MediaReactions.Add($"Heartbreak in {relocation.OriginalCity} as team departs");
            relocation.MediaReactions.Add($"{relocation.NewCity} welcomes NBA basketball");

            pendingRelocations.Remove(relocation);
            completedRelocations.Add(relocation);

            // Remove from potential expansion cities
            potentialExpansionCities.RemoveAll(c => c.CityName == relocation.NewCity);

            // Add original city back to expansion pool
            potentialExpansionCities.Add(new ExpansionCity
            {
                CityName = relocation.OriginalCity,
                HasNBAHistory = true,
                FormerTeamName = relocation.OriginalName.Split(' ').Last(),
                MarketViability = 0.6f,
                FanbaseProjection = 0.8f,
                CorporateSupportScore = 0.5f
            });

            RecordLeagueEvent(LeagueEventType.Relocation,
                $"{relocation.OriginalName} officially relocates to {relocation.NewCity}",
                $"The franchise will be known as the {relocation.NewName}",
                new List<string> { relocation.TeamId },
                0.85f);

            OnRelocationCompleted?.Invoke(relocation);
        }

        private void ProcessPendingRelocations(int currentSeason)
        {
            foreach (var relocation in pendingRelocations.ToList())
            {
                if (relocation.Phase == RelocationPhase.Approved &&
                    relocation.MovingSeason <= currentSeason)
                {
                    CompleteRelocation(relocation, currentSeason);
                }
            }
        }

        #endregion

        #region Rebranding

        /// <summary>
        /// Initiate team rebranding
        /// </summary>
        public TeamRebranding InitiateRebranding(string teamId, string oldName, string newName = null,
            string newPrimaryColor = null, bool newLogo = false)
        {
            var rebrand = new TeamRebranding
            {
                TeamId = teamId,
                EffectiveDate = DateTime.Now,
                OldName = oldName
            };

            if (!string.IsNullOrEmpty(newName))
            {
                rebrand.NameChanged = true;
                rebrand.NewName = newName;
            }

            if (!string.IsNullOrEmpty(newPrimaryColor))
            {
                rebrand.ColorsChanged = true;
                rebrand.NewPrimaryColor = newPrimaryColor;
            }

            rebrand.LogoChanged = newLogo;
            rebrand.UniformsChanged = rebrand.ColorsChanged || newLogo;

            // Fan approval varies
            rebrand.FanApproval = UnityEngine.Random.Range(0.3f, 0.9f);
            rebrand.MerchandiseSalesChange = rebrand.FanApproval > 0.6f
                ? UnityEngine.Random.Range(10f, 50f)
                : UnityEngine.Random.Range(-20f, 10f);

            pendingRebrands.Add(rebrand);
            OnRebrandingAnnounced?.Invoke(rebrand);

            string changeDescription = rebrand.NameChanged
                ? $"renamed to {rebrand.NewName}"
                : "unveils new visual identity";

            RecordLeagueEvent(LeagueEventType.Rebranding,
                $"{oldName} {changeDescription}",
                rebrand.RebrandReason ?? "Organization seeks fresh start with new identity",
                new List<string> { teamId },
                0.5f);

            return rebrand;
        }

        #endregion

        #region Helper Methods

        private void RecordLeagueEvent(LeagueEventType type, string headline, string description,
            List<string> teamIds, float impact)
        {
            var ev = new LeagueHistoricalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Type = type,
                Date = DateTime.Now,
                Headline = headline,
                Description = description,
                InvolvedTeamIds = teamIds,
                LeagueImpact = impact
            };

            leagueHistory.Add(ev);
            OnMajorLeagueEvent?.Invoke(ev);
        }

        private string GetRandomPosition()
        {
            var positions = new[] { "PG", "SG", "SF", "PF", "C" };
            return positions[UnityEngine.Random.Range(0, positions.Length)];
        }

        #endregion

        /// <summary>
        /// Get all potential expansion cities
        /// </summary>
        public List<ExpansionCity> GetExpansionCities()
        {
            return potentialExpansionCities.OrderByDescending(c => c.OverallScore).ToList();
        }

        /// <summary>
        /// Get pending expansions
        /// </summary>
        public List<ExpansionTeam> GetPendingExpansions() => pendingExpansions;

        /// <summary>
        /// Get pending relocations
        /// </summary>
        public List<TeamRelocation> GetPendingRelocations() => pendingRelocations;

        /// <summary>
        /// Get league history
        /// </summary>
        public List<LeagueHistoricalEvent> GetLeagueHistory(int? lastNYears = null)
        {
            if (lastNYears.HasValue)
            {
                var cutoff = DateTime.Now.AddYears(-lastNYears.Value);
                return leagueHistory.Where(e => e.Date >= cutoff).ToList();
            }
            return leagueHistory;
        }

        /// <summary>
        /// Get current team count
        /// </summary>
        public int CurrentTeamCount => currentTeamCount;

        /// <summary>
        /// Check if expansion is possible
        /// </summary>
        public bool CanExpand => currentTeamCount < maxTeams;
    }
}
