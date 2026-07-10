using System;
using System.Collections;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Developer shortcut: when armed, drives the game straight into a live playable
    /// match after Boot — no main menu, no new-game wizard, no manual day-advancing.
    /// Used to verify the 3D match view headlessly.
    ///
    /// Arm it either by setting the PlayerPrefs key "DebugMatchBootstrap" to 1 (what the
    /// "Tools/NBA Head Coach/Debug Match" editor menu does) or by setting the static
    /// <see cref="Armed"/> flag before play mode starts. The flag is consumed on the first
    /// run so normal launches are completely unaffected — when it is absent this file adds
    /// nothing to the running game (the hook returns immediately without creating anything).
    ///
    /// Every step reuses the exact production code paths (GameManager.StartNewGame,
    /// AdvanceDay, MatchFlowController.PrepareMatch/StartInteractiveMatch) — no game-init
    /// logic is duplicated here.
    /// </summary>
    public class DebugMatchBootstrap : MonoBehaviour
    {
        /// <summary>PlayerPrefs key that arms the bootstrap (value 1 = armed).</summary>
        public const string ArmedPrefKey = "DebugMatchBootstrap";

        /// <summary>Alternative arming path for code/tests that don't want to touch PlayerPrefs.</summary>
        public static bool Armed;

        // Fixed defaults mirroring the new-game wizard's out-of-the-box selections
        // (Former Player background, Normal difficulty, "Both" role). Any real teams.json
        // id works; Lakers is a stable, always-present choice.
        private const string DefaultTeamId = "LAL";
        private const string DefaultFirstName = "Debug";
        private const string DefaultLastName = "Coach";
        private const int DefaultAge = 45;
        private const int DefaultReputation = 70;   // Former Player bg
        private const int DefaultTactical = 45;     // Former Player bg
        private const int DefaultDevelopment = 60;  // Former Player bg

        // Guard against runaway day-advancing if a schedule is ever missing a player game.
        private const int MaxDaysToAdvance = 400;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            bool armed = Armed || PlayerPrefs.GetInt(ArmedPrefKey, 0) == 1;
            if (!armed) return; // zero impact on normal runs

            var go = new GameObject("DebugMatchBootstrap");
            go.AddComponent<DebugMatchBootstrap>();
        }

        private void Awake()
        {
            // Survive the Boot → MainMenu → Game → Match scene loads while we drive the flow.
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            // Consume the armed flag up front so a normal re-launch is never hijacked,
            // even if this run errors out partway through.
            Armed = false;
            PlayerPrefs.SetInt(ArmedPrefKey, 0);
            PlayerPrefs.Save();

            Debug.Log("[DebugMatchBootstrap] Armed — booting straight into a live match.");

            // 1) Wait for GameManager to finish loading players/teams. It flips to the
            //    MainMenu state once the data load completes.
            yield return new WaitUntil(() =>
                GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.MainMenu &&
                GameManager.Instance.GetTeam(DefaultTeamId) != null);

            var gm = GameManager.Instance;

            // 2) Start a new game exactly the way MenuInjector's wizard does — same entry point,
            //    same argument shape (see NewGameWizard.StartGame). This loads the Game scene.
            var difficulty = DifficultySettings.CreateFromPreset(DifficultyPreset.Normal);
            var birthDate = new DateTime(1980, 6, 15);
            gm.StartNewGame(
                DefaultFirstName, DefaultLastName, DefaultAge, DefaultTeamId,
                difficulty, DefaultTactical, DefaultDevelopment, DefaultReputation,
                UserRole.Both, birthDate);

            // 3) StartNewGame transitions to Playing and kicks off the Game scene load.
            yield return new WaitUntil(() => gm.CurrentState == GameState.Playing);
            yield return null; // let the scene settle a frame

            var season = gm.SeasonController;
            var nextGame = season?.GetNextGame();
            if (nextGame == null)
            {
                Debug.LogError("[DebugMatchBootstrap] No upcoming game found for " + DefaultTeamId + " — aborting.");
                Destroy(gameObject);
                yield break;
            }

            // 4) Advance days until the player team's first game is today. The daily league
            //    auto-sim deliberately skips the player's next game (see LeagueGameSimSystem),
            //    so the target game is still uncompleted when we reach its date.
            int guard = 0;
            while (gm.CurrentDate.Date < nextGame.Date.Date && guard++ < MaxDaysToAdvance)
            {
                gm.AdvanceDay();
            }

            // Prefer today's game; fall back to the originally-resolved next game.
            var game = season.GetTodaysGame() ?? nextGame;

            Debug.Log($"[DebugMatchBootstrap] Game day reached ({gm.CurrentDate:yyyy-MM-dd}) — entering match: " +
                      $"{game.AwayTeamId} @ {game.HomeTeamId}.");

            // 5) Enter the interactive match with FULL playback — the exact path the PreGame
            //    "PLAY GAME" button uses. The Match scene defaults to Full-match playback and
            //    reads the "MatchViewMode3D" pref for the 2D/3D view (set by the editor menu).
            gm.MatchController.PrepareMatch(game);
            gm.MatchController.StartInteractiveMatch();

            Destroy(gameObject);
        }
    }
}
