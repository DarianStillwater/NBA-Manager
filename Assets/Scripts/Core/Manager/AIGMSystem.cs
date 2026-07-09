using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Runs the AI general manager in coach-only mode: keeps AIGMController
    /// initialized for the player's team, surfaces the GM's autonomous activity
    /// through the inbox, and persists the GM's hidden personality and the
    /// coach-GM relationship across saves.
    /// </summary>
    public class AIGMSystem : IDailyTickable, ISaveSection
    {
        public string SystemId => "AIGM";
        public int TickOrder => 495;

        public void DailyTick(in DailyTickContext ctx)
        {
            var gm = ctx.Game;
            var config = gm?.UserRoleConfig;
            if (config?.HasAIGM != true) return;

            EnsureInitialized(gm);

            var actions = AIGMController.Instance.ProcessDailyDecisions();
            if (actions == null) return;
            foreach (var action in actions)
            {
                InboxService.Instance?.Publish(InboxMessageType.League, "Front Office",
                    action, "Word from the front office.", deepLinkPanelId: "FrontOffice");
            }
        }

        /// <summary>Idempotent: installs the named AI GM on the player's team.</summary>
        public static void EnsureInitialized(GameManager gm)
        {
            var config = gm?.UserRoleConfig;
            if (config?.HasAIGM != true) return;
            if (AIGMController.Instance.IsInitializedFor(config.TeamId)) return;

            var profile = gm.GetAIGM();
            AIGMController.Instance.Initialize(
                config.AIGMProfileId,
                profile?.FullName ?? "General Manager",
                config.TeamId);
        }

        public void WriteSave(SaveData data)
        {
            if (data == null) return;
            data.AIGMData = AIGMController.Instance.GetSaveData();
        }

        public void ReadSave(SaveData data, in SaveReadContext ctx)
        {
            if (data?.AIGMData != null)
                AIGMController.Instance.LoadSaveData(data.AIGMData);
        }
    }
}
