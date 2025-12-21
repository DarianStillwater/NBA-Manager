using System;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Main game states for state machine
    /// </summary>
    [Serializable]
    public enum GameState
    {
        /// <summary>Initial loading state</summary>
        Booting,

        /// <summary>Main menu (New Game, Load, Settings, Quit)</summary>
        MainMenu,

        /// <summary>Creating a new career</summary>
        NewGame,

        /// <summary>Main gameplay - dashboard/hub view</summary>
        Playing,

        /// <summary>Pre-game preparation</summary>
        PreGame,

        /// <summary>Active match simulation</summary>
        Match,

        /// <summary>Post-game results</summary>
        PostGame,

        /// <summary>Offseason activities (Draft, FA, etc.)</summary>
        Offseason,

        /// <summary>Game is paused</summary>
        Paused,

        /// <summary>Loading a saved game</summary>
        Loading,

        /// <summary>Saving the game</summary>
        Saving,

        /// <summary>Job market - user is unemployed and searching for work</summary>
        JobMarket
    }

    /// <summary>
    /// Sub-states for offseason phases
    /// </summary>
    [Serializable]
    public enum OffseasonState
    {
        SeasonEnd,
        DraftLottery,
        DraftCombine,
        Draft,
        FreeAgencyMoratorium,
        FreeAgencyOpen,
        SummerLeague,
        TrainingCamp,
        Complete
    }

    /// <summary>
    /// Sub-states for new game wizard
    /// </summary>
    [Serializable]
    public enum NewGameState
    {
        CoachCreation,
        TeamSelection,
        RoleSelection,
        DifficultySettings,
        ContractNegotiation,
        Confirmation
    }

    /// <summary>
    /// Event args for state transitions
    /// </summary>
    public class GameStateChangedEventArgs : EventArgs
    {
        public GameState PreviousState { get; }
        public GameState NewState { get; }
        public object Data { get; }

        public GameStateChangedEventArgs(GameState previousState, GameState newState, object data = null)
        {
            PreviousState = previousState;
            NewState = newState;
            Data = data;
        }
    }
}
