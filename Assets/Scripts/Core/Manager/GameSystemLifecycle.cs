using System;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Identity marker for every system registered with the GameSystemRegistry.
    /// Registration is the manifest of the game's living systems: a manager that is
    /// constructed but not registered is invisible to the daily loop, save pipeline,
    /// and phase transitions BY DESIGN — register it with the lifecycle interfaces
    /// it actually implements.
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>Stable unique id, e.g. "InjuryManager". Duplicate registration throws.</summary>
        string SystemId { get; }
    }

    /// <summary>
    /// Per-day-advance work. GameManager.AdvanceDay iterates all registered tickables
    /// in (TickOrder, registration index) order — no hand-maintained call list.
    /// </summary>
    public interface IDailyTickable : IGameSystem
    {
        /// <summary>Use TickOrder constants; lower runs first.</summary>
        int TickOrder { get; }
        void DailyTick(in DailyTickContext ctx);
    }

    /// <summary>
    /// Owns one slice of SaveData. WriteSave is called for every registered section on
    /// save; ReadSave on load (registration order). A section must treat a null/empty
    /// slice as "legacy save" and fall back gracefully.
    /// </summary>
    public interface ISaveSection : IGameSystem
    {
        void WriteSave(SaveData data);
        void ReadSave(SaveData data, in SaveReadContext ctx);
    }

    /// <summary>Reacts to season phase transitions (playoffs, offseason stages).</summary>
    public interface ISeasonPhaseListener : IGameSystem
    {
        void OnSeasonPhaseChanged(SeasonPhase oldPhase, SeasonPhase newPhase, DateTime date);
    }

    /// <summary>
    /// New-game-only initialization. NEVER run on load — load restores state from save
    /// sections instead of regenerating it (coaches/lineups/strategies must survive a load).
    /// </summary>
    public interface INewGameInitializable : IGameSystem
    {
        void InitializeForNewGame(in NewGameContext ctx);
    }

    /// <summary>
    /// Canonical daily tick ordering. Gaps of 100 leave room for backlog systems
    /// (practice, fatigue, form, scouting, finance dailies) without renumbering.
    /// Order encodes real dependencies: energy/injury recovery (SeasonCalendar) must
    /// run before games are simulated (LeagueSim); everything reading today's results
    /// runs after.
    /// </summary>
    public static class TickOrder
    {
        public const int SeasonCalendar = 100; // phase check, injury recovery, energy recovery, morale decay
        public const int LeagueSim      = 200; // simulate today's non-player games
        public const int TradeOffers    = 300;
        public const int Personnel      = 400;
        public const int JobMarket      = 500;
        public const int Mentorship     = 600; // internally gated to Mondays
        public const int Media          = 700;
        public const int InboxDigest    = 800; // reserved for future digest/notification producers
    }

    public readonly struct DailyTickContext
    {
        public readonly DateTime Date;
        public readonly string PlayerTeamId;
        public readonly GameManager Game; // may be null in headless tests

        public DailyTickContext(DateTime date, string playerTeamId, GameManager game)
        {
            Date = date;
            PlayerTeamId = playerTeamId;
            Game = game;
        }
    }

    public readonly struct SaveReadContext
    {
        public readonly string SaveVersion;
        /// <summary>True when the save predates save-section ownership (v1.0.x).</summary>
        public readonly bool IsLegacySave;
        public readonly int CurrentSeason;

        public SaveReadContext(string saveVersion, bool isLegacySave, int currentSeason)
        {
            SaveVersion = saveVersion;
            IsLegacySave = isLegacySave;
            CurrentSeason = currentSeason;
        }
    }

    public readonly struct NewGameContext
    {
        public readonly string PlayerTeamId;
        public readonly int Season;
        public readonly DifficultySettings Difficulty;

        public NewGameContext(string playerTeamId, int season, DifficultySettings difficulty)
        {
            PlayerTeamId = playerTeamId;
            Season = season;
            Difficulty = difficulty;
        }
    }
}
