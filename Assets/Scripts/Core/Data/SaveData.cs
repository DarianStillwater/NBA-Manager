using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Complete save file structure for game persistence
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const string CURRENT_VERSION = "1.0.0";

        [Header("Metadata")]
        public string SaveVersion = CURRENT_VERSION;
        public DateTime SaveTimestamp;
        public string SaveName;
        public string SaveSlot;
        public bool IsIronman;  // Ironman mode - single save, no reload

        [Header("Career")]
        public CoachCareer Career;
        public string PlayerTeamId;
        public DifficultySettings Difficulty;

        [Header("Season")]
        public int CurrentSeason;
        public DateTime CurrentDate;
        public SeasonPhase CurrentPhase;

        [Header("League State")]
        public List<TeamSaveState> TeamStates = new List<TeamSaveState>();
        public List<PlayerSaveState> PlayerStates = new List<PlayerSaveState>();
        public List<ContractSaveState> Contracts = new List<ContractSaveState>();

        [Header("Calendar")]
        public CalendarSaveData CalendarData;

        [Header("Playoffs")]
        public PlayoffSaveData PlayoffData;

        [Header("Draft")]
        public List<DraftProspectSaveState> DraftClass = new List<DraftProspectSaveState>();
        public Dictionary<string, List<int>> TeamDraftPicks = new Dictionary<string, List<int>>();

        [Header("Transaction History")]
        public List<TransactionRecord> Transactions = new List<TransactionRecord>();

        [Header("Awards")]
        public Dictionary<int, SeasonAwards> AwardsHistory = new Dictionary<int, SeasonAwards>();

        [Header("Former Player Careers")]
        public FormerPlayerCareerSaveData FormerPlayerCareers;
        public GMJobSecuritySaveData GMJobSecurity;

        [Header("Unified Career System")]
        public UnifiedCareerSaveData UnifiedCareers;

        /// <summary>
        /// Create a display-friendly summary of the save
        /// </summary>
        public SaveSlotInfo CreateSlotInfo()
        {
            return new SaveSlotInfo
            {
                SlotName = SaveSlot,
                SaveName = SaveName,
                CoachName = Career?.FullName ?? "Unknown",
                TeamId = PlayerTeamId,
                Season = CurrentSeason,
                Date = CurrentDate,
                Record = GetRecordString(),
                SaveTimestamp = SaveTimestamp,
                IsIronman = IsIronman
            };
        }

        private string GetRecordString()
        {
            if (Career?.SeasonHistory == null || Career.SeasonHistory.Count == 0)
                return "0-0";

            int wins = 0, losses = 0;
            foreach (var season in Career.SeasonHistory)
            {
                wins += season.Wins;
                losses += season.Losses;
            }
            return $"{wins}-{losses}";
        }
    }

    /// <summary>
    /// Info displayed in save slot UI
    /// </summary>
    [Serializable]
    public class SaveSlotInfo
    {
        public string SlotName;
        public string SaveName;
        public string CoachName;
        public string TeamId;
        public int Season;
        public DateTime Date;
        public string Record;
        public DateTime SaveTimestamp;
        public bool IsIronman;
        public bool IsEmpty => string.IsNullOrEmpty(CoachName);
    }

    /// <summary>
    /// Serializable team state for saves
    /// </summary>
    [Serializable]
    public class TeamSaveState
    {
        public string TeamId;
        public int Wins;
        public int Losses;
        public int PlayoffWins;
        public int PlayoffLosses;
        public List<string> RosterPlayerIds = new List<string>();
        public List<int> StartingLineupIndices = new List<int>();
        public float SalaryCap;
        public float TotalSalary;
        public string HeadCoachId;

        public static TeamSaveState CreateFrom(Team team)
        {
            if (team == null) return null;

            var state = new TeamSaveState
            {
                TeamId = team.TeamId,
                Wins = team.Wins,
                Losses = team.Losses,
                RosterPlayerIds = new List<string>(),
                StartingLineupIndices = new List<int>(team.StartingLineup ?? new int[5])
            };

            if (team.Roster != null)
            {
                foreach (var player in team.Roster)
                {
                    state.RosterPlayerIds.Add(player.PlayerId);
                }
            }

            return state;
        }

        public void ApplyTo(Team team)
        {
            if (team == null) return;

            team.Wins = Wins;
            team.Losses = Losses;
            // Roster restoration would need PlayerDatabase lookup
        }
    }

    /// <summary>
    /// Serializable player state for saves
    /// </summary>
    [Serializable]
    public class PlayerSaveState
    {
        public string PlayerId;
        public string TeamId;
        public int Age;
        public float Energy;
        public float Morale;
        public bool IsInjured;
        public int InjuryDaysRemaining;

        // Enhanced injury data
        public InjuryType CurrentInjuryType;
        public InjurySeverity CurrentInjurySeverity;
        public int OriginalInjuryDays;
        public DateTime? InjuryDate;
        public List<InjuryHistorySaveState> InjuryHistory = new List<InjuryHistorySaveState>();
        public List<MinutesRecordSaveState> RecentMinutes = new List<MinutesRecordSaveState>();

        // Key attributes that can change
        public int Overall;
        public int Potential;

        // Stats (legacy - kept for backwards compatibility)
        public float PointsPerGame;
        public float ReboundsPerGame;
        public float AssistsPerGame;

        // Full career stats history
        public List<SeasonStatsSaveState> CareerStats = new List<SeasonStatsSaveState>();

        // Flag for generated players (not in base database)
        public bool IsGeneratedPlayer;

        // Full player data for generated players (needed to recreate them on load)
        public GeneratedPlayerData GeneratedData;

        public static PlayerSaveState CreateFrom(Player player, int currentSeason = 0)
        {
            if (player == null) return null;

            var state = new PlayerSaveState
            {
                PlayerId = player.PlayerId,
                TeamId = player.TeamId,
                Age = player.Age,
                Energy = player.Energy,
                Morale = player.Morale,
                IsInjured = player.IsInjured,
                InjuryDaysRemaining = player.InjuryDaysRemaining,
                CurrentInjuryType = player.CurrentInjuryType,
                CurrentInjurySeverity = player.CurrentInjurySeverity,
                OriginalInjuryDays = player.OriginalInjuryDays,
                InjuryDate = player.InjuryDate,
                Overall = player.Overall,
                Potential = player.Potential,
                IsGeneratedPlayer = player.IsGenerated
            };

            // Save injury history
            if (player.InjuryHistoryList != null)
            {
                state.InjuryHistory = player.InjuryHistoryList
                    .Select(h => InjuryHistorySaveState.CreateFrom(h))
                    .Where(h => h != null)
                    .ToList();
            }

            // Save recent minutes
            if (player.RecentMinutes != null)
            {
                state.RecentMinutes = player.RecentMinutes
                    .Select(m => MinutesRecordSaveState.CreateFrom(m))
                    .Where(m => m != null)
                    .ToList();
            }

            // Save career stats
            if (player.CareerStats != null && player.CareerStats.Count > 0)
            {
                for (int i = 0; i < player.CareerStats.Count; i++)
                {
                    var seasonStats = player.CareerStats[i];
                    // Only include game logs for current season (last entry)
                    bool isCurrentSeason = (i == player.CareerStats.Count - 1) &&
                                           (currentSeason == 0 || seasonStats.Year == currentSeason);
                    var saveState = SeasonStatsSaveState.CreateFrom(seasonStats, isCurrentSeason);
                    if (saveState != null)
                    {
                        state.CareerStats.Add(saveState);
                    }
                }

                // Set legacy stats from current season for backwards compatibility
                var current = player.CurrentSeasonStats;
                if (current != null)
                {
                    state.PointsPerGame = current.PPG;
                    state.ReboundsPerGame = current.RPG;
                    state.AssistsPerGame = current.APG;
                }
            }

            // For generated players, save full player data for recreation
            if (player.IsGenerated)
            {
                state.GeneratedData = GeneratedPlayerData.CreateFrom(player);
            }

            return state;
        }

        public void ApplyTo(Player player)
        {
            if (player == null) return;

            player.TeamId = TeamId;
            player.Age = Age;
            player.Energy = Energy;
            player.Morale = Morale;
            player.IsInjured = IsInjured;
            player.InjuryDaysRemaining = InjuryDaysRemaining;
            player.CurrentInjuryType = CurrentInjuryType;
            player.CurrentInjurySeverity = CurrentInjurySeverity;
            player.OriginalInjuryDays = OriginalInjuryDays;
            player.InjuryDate = InjuryDate;

            // Restore injury history
            if (InjuryHistory != null && InjuryHistory.Count > 0)
            {
                player.InjuryHistoryList = InjuryHistory
                    .Select(h => h.ToInjuryHistory())
                    .Where(h => h != null)
                    .ToList();
            }

            // Restore recent minutes
            if (RecentMinutes != null && RecentMinutes.Count > 0)
            {
                player.RecentMinutes = RecentMinutes
                    .Select(m => m.ToMinutesRecord())
                    .Where(m => m != null)
                    .ToList();
            }

            // Restore career stats
            if (CareerStats != null && CareerStats.Count > 0)
            {
                player.CareerStats = CareerStats
                    .Select(s => s.ToSeasonStats())
                    .Where(s => s != null)
                    .ToList();
            }
        }

        /// <summary>
        /// Create a Player object from a generated player save state.
        /// Used when loading saves with players not in the base database.
        /// </summary>
        public Player CreateGeneratedPlayer()
        {
            if (!IsGeneratedPlayer || GeneratedData == null) return null;
            return GeneratedData.ToPlayer();
        }
    }

    /// <summary>
    /// Complete player data for generated players (drafted rookies, etc.)
    /// Allows recreation of players not in the base database.
    /// </summary>
    [Serializable]
    public class GeneratedPlayerData
    {
        // Identity
        public string PlayerId;
        public string FirstName;
        public string LastName;
        public int JerseyNumber;
        public Data.Position Position;
        public DateTime BirthDate;
        public int HeightInches;
        public int WeightLbs;
        public string Nationality;
        public string College;

        // Draft info
        public int DraftYear;
        public int DraftRound;
        public int DraftPick;
        public string DraftedByTeamId;

        // All attributes (stored individually for full recreation)
        public int Finishing_Rim, Finishing_PostMoves;
        public int Shot_Close, Shot_MidRange, Shot_Three, FreeThrow;
        public int Passing, BallHandling, OffensiveIQ, SpeedWithBall;
        public int Defense_Perimeter, Defense_Interior, Defense_PostDefense;
        public int Steal, Block, DefensiveIQ, DefensiveRebound;
        public int Speed, Acceleration, Strength, Vertical, Stamina, Durability, Wingspan;
        public int BasketballIQ, Clutch, Consistency, WorkEthic, Coachability;
        public int Ego, Leadership, Composure, Aggression;

        // Hidden development attributes
        public int HiddenPotential;
        public int PeakAge;
        public int DeclineRate;
        public int InjuryProneness;

        public static GeneratedPlayerData CreateFrom(Player player)
        {
            if (player == null) return null;

            return new GeneratedPlayerData
            {
                PlayerId = player.PlayerId,
                FirstName = player.FirstName,
                LastName = player.LastName,
                JerseyNumber = player.JerseyNumber,
                Position = player.Position,
                BirthDate = player.BirthDate,
                HeightInches = player.HeightInches,
                WeightLbs = player.WeightLbs,
                Nationality = player.Nationality,
                College = player.College,
                DraftYear = player.DraftYear,
                DraftRound = player.DraftRound,
                DraftPick = player.DraftPick,
                DraftedByTeamId = player.DraftedByTeamId,
                // Attributes
                Finishing_Rim = player.Finishing_Rim,
                Finishing_PostMoves = player.Finishing_PostMoves,
                Shot_Close = player.Shot_Close,
                Shot_MidRange = player.Shot_MidRange,
                Shot_Three = player.Shot_Three,
                FreeThrow = player.FreeThrow,
                Passing = player.Passing,
                BallHandling = player.BallHandling,
                OffensiveIQ = player.OffensiveIQ,
                SpeedWithBall = player.SpeedWithBall,
                Defense_Perimeter = player.Defense_Perimeter,
                Defense_Interior = player.Defense_Interior,
                Defense_PostDefense = player.Defense_PostDefense,
                Steal = player.Steal,
                Block = player.Block,
                DefensiveIQ = player.DefensiveIQ,
                DefensiveRebound = player.DefensiveRebound,
                Speed = player.Speed,
                Acceleration = player.Acceleration,
                Strength = player.Strength,
                Vertical = player.Vertical,
                Stamina = player.Stamina,
                Durability = player.Durability,
                Wingspan = player.Wingspan,
                BasketballIQ = player.BasketballIQ,
                Clutch = player.Clutch,
                Consistency = player.Consistency,
                WorkEthic = player.WorkEthic,
                Coachability = player.Coachability,
                Ego = player.Ego,
                Leadership = player.Leadership,
                Composure = player.Composure,
                Aggression = player.Aggression,
                HiddenPotential = player.HiddenPotential,
                PeakAge = player.PeakAge,
                DeclineRate = player.DeclineRate,
                InjuryProneness = player.InjuryProneness
            };
        }

        public Player ToPlayer()
        {
            var player = new Player
            {
                PlayerId = PlayerId,
                FirstName = FirstName,
                LastName = LastName,
                JerseyNumber = JerseyNumber,
                Position = Position,
                BirthDate = BirthDate,
                HeightInches = HeightInches,
                WeightLbs = WeightLbs,
                Nationality = Nationality,
                College = College,
                DraftYear = DraftYear,
                DraftRound = DraftRound,
                DraftPick = DraftPick,
                DraftedByTeamId = DraftedByTeamId,
                IsGenerated = true,
                // Attributes
                Finishing_Rim = Finishing_Rim,
                Finishing_PostMoves = Finishing_PostMoves,
                Shot_Close = Shot_Close,
                Shot_MidRange = Shot_MidRange,
                Shot_Three = Shot_Three,
                FreeThrow = FreeThrow,
                Passing = Passing,
                BallHandling = BallHandling,
                OffensiveIQ = OffensiveIQ,
                SpeedWithBall = SpeedWithBall,
                Defense_Perimeter = Defense_Perimeter,
                Defense_Interior = Defense_Interior,
                Defense_PostDefense = Defense_PostDefense,
                Steal = Steal,
                Block = Block,
                DefensiveIQ = DefensiveIQ,
                DefensiveRebound = DefensiveRebound,
                Speed = Speed,
                Acceleration = Acceleration,
                Strength = Strength,
                Vertical = Vertical,
                Stamina = Stamina,
                Durability = Durability,
                Wingspan = Wingspan,
                BasketballIQ = BasketballIQ,
                Clutch = Clutch,
                Consistency = Consistency,
                WorkEthic = WorkEthic,
                Coachability = Coachability,
                Ego = Ego,
                Leadership = Leadership,
                Composure = Composure,
                Aggression = Aggression,
                HiddenPotential = HiddenPotential,
                PeakAge = PeakAge,
                DeclineRate = DeclineRate,
                InjuryProneness = InjuryProneness
            };

            return player;
        }
    }

    /// <summary>
    /// Contract save state
    /// </summary>
    [Serializable]
    public class ContractSaveState
    {
        public string ContractId;
        public string PlayerId;
        public string TeamId;
        public int Years;
        public int CurrentYear;
        public float TotalValue;
        public float AnnualValue;
        public bool HasPlayerOption;
        public bool HasTeamOption;
        public int OptionYear;
    }

    /// <summary>
    /// Calendar save data
    /// </summary>
    [Serializable]
    public class CalendarSaveData
    {
        public int Season;
        public DateTime CurrentDate;
        public SeasonPhase Phase;
        public int CurrentGameIndex;
        public List<SavedGameResult> CompletedGames = new List<SavedGameResult>();
    }

    /// <summary>
    /// Draft prospect save state
    /// </summary>
    [Serializable]
    public class DraftProspectSaveState
    {
        public string ProspectId;
        public string Name;
        public int Age;
        public string Position;
        public int ConsensusRank;
        public string College;
        public bool WasDrafted;
        public string DraftedByTeamId;
        public int DraftPick;
    }

    /// <summary>
    /// Transaction record for history
    /// </summary>
    [Serializable]
    public class TransactionRecord
    {
        public string TransactionId;
        public DateTime Date;
        public TransactionType Type;
        public List<string> TeamIds = new List<string>();
        public List<string> PlayerIds = new List<string>();
        public string Description;
    }

    [Serializable]
    public enum TransactionType
    {
        Trade,
        Signing,
        Waiver,
        DraftPick,
        Extension,
        Retirement
    }

    /// <summary>
    /// Season awards record
    /// </summary>
    [Serializable]
    public class SeasonAwards
    {
        public int Season;
        public string MvpId;
        public string DpoyId;
        public string RoyId;
        public string SixthManId;
        public string MipId;
        public string CotyId;
        public List<string> AllNbaFirst = new List<string>();
        public List<string> AllNbaSecond = new List<string>();
        public List<string> AllNbaThird = new List<string>();
        public List<string> AllDefenseFirst = new List<string>();
        public List<string> AllDefenseSecond = new List<string>();
        public List<string> AllStars = new List<string>();
    }

    /// <summary>
    /// Game result record for saving
    /// </summary>
    [Serializable]
    public class SavedGameResult
    {
        public string GameId;
        public DateTime Date;
        public string HomeTeamId;
        public string AwayTeamId;
        public int HomeScore;
        public int AwayScore;
        public bool IsPlayoff;
        public int PlayoffRound;
        public int PlayoffGame;

        public string WinnerId => HomeScore > AwayScore ? HomeTeamId : AwayTeamId;
        public string LoserId => HomeScore > AwayScore ? AwayTeamId : HomeTeamId;
        public bool WasOvertime;
        public int OvertimePeriods;
    }

    /// <summary>
    /// Difficulty settings for game
    /// </summary>
    [Serializable]
    public class DifficultySettings
    {
        [Header("AI Behavior")]
        [Range(0f, 1f)] public float AIAggressiveness = 0.5f;
        [Range(0f, 1f)] public float AITradeFrequency = 0.5f;
        [Range(0f, 1f)] public float AIFAAggressiveness = 0.5f;

        [Header("Simulation")]
        [Range(0f, 1f)] public float InjuryFrequency = 0.5f;
        [Range(0f, 1f)] public float RandomnessLevel = 0.5f;

        [Header("Career")]
        [Range(0f, 1f)] public float OwnerPatience = 0.5f;
        [Range(0f, 1f)] public float FinancialPressure = 0.5f;

        [Header("Presets")]
        public DifficultyPreset Preset = DifficultyPreset.Normal;

        public static DifficultySettings CreateFromPreset(DifficultyPreset preset)
        {
            return preset switch
            {
                DifficultyPreset.Easy => new DifficultySettings
                {
                    AIAggressiveness = 0.3f,
                    AITradeFrequency = 0.3f,
                    AIFAAggressiveness = 0.3f,
                    InjuryFrequency = 0.3f,
                    OwnerPatience = 0.8f,
                    FinancialPressure = 0.3f,
                    Preset = preset
                },
                DifficultyPreset.Normal => new DifficultySettings
                {
                    AIAggressiveness = 0.5f,
                    AITradeFrequency = 0.5f,
                    AIFAAggressiveness = 0.5f,
                    InjuryFrequency = 0.5f,
                    OwnerPatience = 0.5f,
                    FinancialPressure = 0.5f,
                    Preset = preset
                },
                DifficultyPreset.Hard => new DifficultySettings
                {
                    AIAggressiveness = 0.7f,
                    AITradeFrequency = 0.7f,
                    AIFAAggressiveness = 0.7f,
                    InjuryFrequency = 0.6f,
                    OwnerPatience = 0.3f,
                    FinancialPressure = 0.7f,
                    Preset = preset
                },
                DifficultyPreset.Legendary => new DifficultySettings
                {
                    AIAggressiveness = 0.9f,
                    AITradeFrequency = 0.8f,
                    AIFAAggressiveness = 0.9f,
                    InjuryFrequency = 0.7f,
                    OwnerPatience = 0.2f,
                    FinancialPressure = 0.9f,
                    Preset = preset
                },
                _ => new DifficultySettings()
            };
        }
    }

    [Serializable]
    public enum DifficultyPreset
    {
        Easy,
        Normal,
        Hard,
        Legendary,
        Custom
    }

    /// <summary>
    /// Serializable injury history for saves
    /// </summary>
    [Serializable]
    public class InjuryHistorySaveState
    {
        public InjuryType BodyPart;
        public int TimesInjured;
        public float ReInjuryRiskModifier;
        public DateTime? LastInjuryDate;
        public int TotalDaysMissed;

        public static InjuryHistorySaveState CreateFrom(InjuryHistory history)
        {
            if (history == null) return null;

            return new InjuryHistorySaveState
            {
                BodyPart = history.BodyPart,
                TimesInjured = history.TimesInjured,
                ReInjuryRiskModifier = history.ReInjuryRiskModifier,
                LastInjuryDate = history.LastInjuryDate,
                TotalDaysMissed = history.TotalDaysMissed
            };
        }

        public InjuryHistory ToInjuryHistory()
        {
            return new InjuryHistory
            {
                BodyPart = BodyPart,
                TimesInjured = TimesInjured,
                ReInjuryRiskModifier = ReInjuryRiskModifier,
                LastInjuryDate = LastInjuryDate,
                TotalDaysMissed = TotalDaysMissed
            };
        }
    }

    /// <summary>
    /// Serializable minutes record for load management tracking
    /// </summary>
    [Serializable]
    public class MinutesRecordSaveState
    {
        public DateTime Date;
        public float Minutes;
        public bool WasBackToBack;

        public static MinutesRecordSaveState CreateFrom(MinutesRecord record)
        {
            if (record == null) return null;

            return new MinutesRecordSaveState
            {
                Date = record.Date,
                Minutes = record.Minutes,
                WasBackToBack = record.WasBackToBack
            };
        }

        public MinutesRecord ToMinutesRecord()
        {
            return new MinutesRecord
            {
                Date = Date,
                Minutes = Minutes,
                WasBackToBack = WasBackToBack
            };
        }
    }

    /// <summary>
    /// Serializable game log for saving individual game performances.
    /// Only used for current season - past seasons don't save game logs.
    /// </summary>
    [Serializable]
    public class GameLogSaveState
    {
        public string GameId;
        public DateTime Date;
        public string OpponentTeamId;
        public bool IsHome;
        public bool IsPlayoff;
        public int PlayoffRound;

        public int Minutes;
        public bool Started;
        public int Points;
        public int FGM, FGA;
        public int ThreePM, ThreePA;
        public int FTM, FTA;
        public int ORB, DRB;
        public int Assists, Steals, Blocks;
        public int Turnovers, PersonalFouls;
        public int PlusMinus;

        public int TeamScore, OpponentScore;
        public bool WasOvertime;
        public int OvertimePeriods;

        public static GameLogSaveState CreateFrom(Data.GameLog log)
        {
            if (log == null) return null;

            return new GameLogSaveState
            {
                GameId = log.GameId,
                Date = log.Date,
                OpponentTeamId = log.OpponentTeamId,
                IsHome = log.IsHome,
                IsPlayoff = log.IsPlayoff,
                PlayoffRound = log.PlayoffRound,
                Minutes = log.Minutes,
                Started = log.Started,
                Points = log.Points,
                FGM = log.FGM,
                FGA = log.FGA,
                ThreePM = log.ThreePM,
                ThreePA = log.ThreePA,
                FTM = log.FTM,
                FTA = log.FTA,
                ORB = log.ORB,
                DRB = log.DRB,
                Assists = log.Assists,
                Steals = log.Steals,
                Blocks = log.Blocks,
                Turnovers = log.Turnovers,
                PersonalFouls = log.PersonalFouls,
                PlusMinus = log.PlusMinus,
                TeamScore = log.TeamScore,
                OpponentScore = log.OpponentScore,
                WasOvertime = log.WasOvertime,
                OvertimePeriods = log.OvertimePeriods
            };
        }

        public Data.GameLog ToGameLog()
        {
            return Data.GameLog.Create(
                GameId, Date, OpponentTeamId, IsHome, IsPlayoff, PlayoffRound,
                Minutes, Started, Points, FGM, FGA, ThreePM, ThreePA,
                FTM, FTA, ORB, DRB, Assists, Steals, Blocks, Turnovers,
                PersonalFouls, PlusMinus, TeamScore, OpponentScore,
                WasOvertime, OvertimePeriods
            );
        }
    }

    /// <summary>
    /// Serializable season statistics for career history persistence.
    /// Game logs only included for current season.
    /// </summary>
    [Serializable]
    public class SeasonStatsSaveState
    {
        public int Year;
        public string TeamId;

        // Totals
        public int GamesPlayed;
        public int GamesStarted;
        public int MinutesPlayed;
        public int Points;
        public int FG_Made, FG_Attempts;
        public int ThreeP_Made, ThreeP_Attempts;
        public int FT_Made, FT_Attempts;
        public int OffensiveRebounds, DefensiveRebounds;
        public int Assists, Steals, Blocks;
        public int Turnovers, PersonalFouls;
        public int TotalPlusMinus;

        // Advanced stats
        public float PER, TrueShootingPct, EffectiveFGPct;
        public float ThreePAr, FTr;
        public float OrbPct, DrbPct, TrbPct;
        public float AstPct, StlPct, BlkPct, TovPct, UsgPct;
        public float OffensiveWinShares, DefensiveWinShares, WinShares, WinSharesPer48;
        public float OffensiveBPM, DefensiveBPM, BoxPlusMinus, VORP;

        // Game logs - only populated for current season
        public List<GameLogSaveState> GameLogs = new List<GameLogSaveState>();

        /// <summary>
        /// Create save state from SeasonStats.
        /// </summary>
        /// <param name="stats">The season stats to save</param>
        /// <param name="includeGameLogs">True for current season, false for past seasons</param>
        public static SeasonStatsSaveState CreateFrom(Data.SeasonStats stats, bool includeGameLogs)
        {
            if (stats == null) return null;

            var state = new SeasonStatsSaveState
            {
                Year = stats.Year,
                TeamId = stats.TeamId,
                GamesPlayed = stats.GamesPlayed,
                GamesStarted = stats.GamesStarted,
                MinutesPlayed = stats.MinutesPlayed,
                Points = stats.Points,
                FG_Made = stats.FG_Made,
                FG_Attempts = stats.FG_Attempts,
                ThreeP_Made = stats.ThreeP_Made,
                ThreeP_Attempts = stats.ThreeP_Attempts,
                FT_Made = stats.FT_Made,
                FT_Attempts = stats.FT_Attempts,
                OffensiveRebounds = stats.OffensiveRebounds,
                DefensiveRebounds = stats.DefensiveRebounds,
                Assists = stats.Assists,
                Steals = stats.Steals,
                Blocks = stats.Blocks,
                Turnovers = stats.Turnovers,
                PersonalFouls = stats.PersonalFouls,
                TotalPlusMinus = stats.TotalPlusMinus,
                // Advanced
                PER = stats.PER,
                TrueShootingPct = stats.TrueShootingPct,
                EffectiveFGPct = stats.EffectiveFGPct,
                ThreePAr = stats.ThreePAr,
                FTr = stats.FTr,
                OrbPct = stats.OrbPct,
                DrbPct = stats.DrbPct,
                TrbPct = stats.TrbPct,
                AstPct = stats.AstPct,
                StlPct = stats.StlPct,
                BlkPct = stats.BlkPct,
                TovPct = stats.TovPct,
                UsgPct = stats.UsgPct,
                OffensiveWinShares = stats.OffensiveWinShares,
                DefensiveWinShares = stats.DefensiveWinShares,
                WinShares = stats.WinShares,
                WinSharesPer48 = stats.WinSharesPer48,
                OffensiveBPM = stats.OffensiveBPM,
                DefensiveBPM = stats.DefensiveBPM,
                BoxPlusMinus = stats.BoxPlusMinus,
                VORP = stats.VORP
            };

            // Only include game logs for current season
            if (includeGameLogs && stats.GameLogs != null)
            {
                state.GameLogs = stats.GameLogs
                    .Select(g => GameLogSaveState.CreateFrom(g))
                    .Where(g => g != null)
                    .ToList();
            }

            return state;
        }

        public Data.SeasonStats ToSeasonStats()
        {
            var stats = new Data.SeasonStats(Year, TeamId)
            {
                GamesPlayed = GamesPlayed,
                GamesStarted = GamesStarted,
                MinutesPlayed = MinutesPlayed,
                Points = Points,
                FG_Made = FG_Made,
                FG_Attempts = FG_Attempts,
                ThreeP_Made = ThreeP_Made,
                ThreeP_Attempts = ThreeP_Attempts,
                FT_Made = FT_Made,
                FT_Attempts = FT_Attempts,
                OffensiveRebounds = OffensiveRebounds,
                DefensiveRebounds = DefensiveRebounds,
                Assists = Assists,
                Steals = Steals,
                Blocks = Blocks,
                Turnovers = Turnovers,
                PersonalFouls = PersonalFouls,
                TotalPlusMinus = TotalPlusMinus,
                // Advanced
                PER = PER,
                TrueShootingPct = TrueShootingPct,
                EffectiveFGPct = EffectiveFGPct,
                ThreePAr = ThreePAr,
                FTr = FTr,
                OrbPct = OrbPct,
                DrbPct = DrbPct,
                TrbPct = TrbPct,
                AstPct = AstPct,
                StlPct = StlPct,
                BlkPct = BlkPct,
                TovPct = TovPct,
                UsgPct = UsgPct,
                OffensiveWinShares = OffensiveWinShares,
                DefensiveWinShares = DefensiveWinShares,
                WinShares = WinShares,
                WinSharesPer48 = WinSharesPer48,
                OffensiveBPM = OffensiveBPM,
                DefensiveBPM = DefensiveBPM,
                BoxPlusMinus = BoxPlusMinus,
                VORP = VORP
            };

            // Restore game logs if present
            if (GameLogs != null && GameLogs.Count > 0)
            {
                stats.GameLogs = GameLogs
                    .Select(g => g.ToGameLog())
                    .Where(g => g != null)
                    .ToList();
            }

            return stats;
        }
    }
}
