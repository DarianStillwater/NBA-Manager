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

        // Stats
        public float PointsPerGame;
        public float ReboundsPerGame;
        public float AssistsPerGame;

        public static PlayerSaveState CreateFrom(Player player)
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
                Potential = player.Potential
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
}
