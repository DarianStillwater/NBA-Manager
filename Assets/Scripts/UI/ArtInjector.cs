using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Shell;
using NBAHeadCoach.UI.GamePanels;

namespace NBAHeadCoach.UI
{
    /// <summary>
    /// Registers all game panels with GameShell on Start.
    /// All panel logic is in individual files under GamePanels/.
    /// </summary>
    public class ArtInjector : MonoBehaviour
    {
        private GameShell _shell;
        private PreGameGamePanel _preGamePanel;
        private PostGameGamePanel _postGamePanel;

        private void Start()
        {
            _shell = GetComponent<GameShell>();
            if (_shell == null) return;

            _postGamePanel = new PostGameGamePanel(_shell);
            _preGamePanel = new PreGameGamePanel(_shell);

            _shell.RegisterPanel("Dashboard", new DashboardGamePanel());
            _shell.RegisterPanel("Roster", new RosterGamePanel(_shell));
            _shell.RegisterPanel("Schedule", new ScheduleGamePanel());
            _shell.RegisterPanel("Standings", new StandingsGamePanel());
            _shell.RegisterPanel("Inbox", new InboxGamePanel());
            _shell.RegisterPanel("Staff", new StaffGamePanel());
            _shell.RegisterPanel("SaveGame", new SaveLoadGamePanel(loadMode: false));
            _shell.RegisterPanel("LoadGame", new SaveLoadGamePanel(loadMode: true));
            _shell.RegisterPanel("Settings", new SettingsMenuPanel(_shell));
            _shell.RegisterPanel("PreGame", _preGamePanel);
            _shell.RegisterPanel("PostGame", _postGamePanel);
        }

        public void ShowPreGame(CalendarEvent gameEvent)
        {
            _preGamePanel?.SetGameEvent(gameEvent);
            _shell?.ShowPanel("PreGame");
        }
    }
}
