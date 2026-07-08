using System;
using System.Collections.Generic;
using System.Linq;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// The in-season free agent wire. AI teams that fall below 13 healthy players
    /// sign the best available free agent to a minimum deal; the player can sign
    /// anyone from the leftover pool while a roster spot is open. Dormant while
    /// the offseason engine owns free agency.
    /// </summary>
    public class InSeasonFreeAgencySystem : IDailyTickable
    {
        public string SystemId => "InSeasonFA";
        public int TickOrder => Manager.TickOrder.TradeOffers + 20;

        private const int AI_TARGET_ACTIVE = 13;

        private readonly FreeAgentManager _freeAgents;
        private readonly PlayerDatabase _playerDb;
        private readonly Func<string, Team> _teamLookup;
        private readonly Func<List<Team>> _allTeams;

        public InSeasonFreeAgencySystem(
            FreeAgentManager freeAgents,
            PlayerDatabase playerDb,
            Func<string, Team> teamLookup,
            Func<List<Team>> allTeams)
        {
            _freeAgents = freeAgents;
            _playerDb = playerDb;
            _teamLookup = teamLookup;
            _allTeams = allTeams;
        }

        public static InSeasonFreeAgencySystem CreateDefault(GameManager gm)
        {
            return new InSeasonFreeAgencySystem(
                gm.FreeAgents, gm.PlayerDatabase, gm.GetTeam, () => gm.AllTeams);
        }

        public void DailyTick(in DailyTickContext ctx)
        {
            RunAiSigningDay(ctx.Date, ctx.PlayerTeamId, ctx.Game?.Transactions);
        }

        /// <summary>
        /// One day of AI roster patching. Public so tests can drive it directly.
        /// Returns how many signings executed.
        /// </summary>
        public int RunAiSigningDay(DateTime date, string playerTeamId, TransactionLog log = null)
        {
            if (OffseasonManager.Instance != null && OffseasonManager.Instance.EngineActive) return 0;
            if (_freeAgents == null || _allTeams == null) return 0;

            int signed = 0;
            foreach (var team in _allTeams())
            {
                if (team == null) continue;
                if (team.TeamId == playerTeamId && Data.RolePermissions.CanMakeRosterMoves) continue;
                if (team.RosterPlayerIds.Count >= 15) continue;
                if (CountActive(team) >= AI_TARGET_ACTIVE) continue;

                if (SignBestAvailableMinimum(team, date, log)) signed++;
            }
            return signed;
        }

        private static SigningOffer MinimumOffer(Player player)
        {
            return new SigningOffer
            {
                AnnualSalary = LeagueCBA.GetMinimumSalary(player.YearsPro),
                Years = 1,
                Method = SigningMethod.MinimumSalary,
                PlayerYearsExperience = player.YearsPro
            };
        }

        private int CountActive(Team team)
        {
            int active = 0;
            foreach (var id in team.RosterPlayerIds)
            {
                var p = _playerDb?.GetPlayer(id);
                if (p != null && !p.IsInjured) active++;
            }
            return active;
        }

        private bool SignBestAvailableMinimum(Team team, DateTime date, TransactionLog log)
        {
            var pool = _freeAgents.GetFreeAgents()
                .OrderByDescending(fa => fa.EstimatedValue)
                .ToList();

            foreach (var fa in pool)
            {
                var player = _playerDb?.GetPlayer(fa.PlayerId);
                if (player == null || player.RetirementYear > 0) continue;

                var offer = MinimumOffer(player);

                if (!_freeAgents.CanSign(team.TeamId, fa.PlayerId, offer).IsValid) continue;
                if (!_freeAgents.ExecuteSigning(team.TeamId, fa.PlayerId, offer)) continue;

                if (!team.RosterPlayerIds.Contains(fa.PlayerId))
                    team.RosterPlayerIds.Add(fa.PlayerId);
                team.InvalidateRosterCache();

                log?.Add(TransactionType.Signing, date,
                    new List<string> { team.TeamId }, new List<string> { fa.PlayerId },
                    $"{player.FullName} signs with {team.TeamId} (rest of season, minimum)");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Player-team signing from the in-season pool — a buyout-market style
        /// minimum deal for the rest of the season.
        /// </summary>
        public bool SignFreeAgentToPlayerTeam(GameManager gm, string playerId, out string failReason)
        {
            failReason = "";
            var player = _playerDb?.GetPlayer(playerId);
            var team = gm?.GetPlayerTeam();
            if (player == null || team == null || _freeAgents == null)
            { failReason = "Unavailable."; return false; }

            if (OffseasonManager.Instance != null && OffseasonManager.Instance.EngineActive)
            { failReason = "Free agency runs from the offseason desk right now."; return false; }

            if (team.RosterPlayerIds.Count >= 15)
            { failReason = "Roster is full (15)."; return false; }

            var fa = _freeAgents.GetFreeAgents().FirstOrDefault(f => f.PlayerId == playerId);
            if (fa == null) { failReason = "No longer a free agent."; return false; }

            var offer = MinimumOffer(player);

            var check = _freeAgents.CanSign(team.TeamId, playerId, offer);
            if (!check.IsValid) { failReason = check.Reason; return false; }

            if (!_freeAgents.ExecuteSigning(team.TeamId, playerId, offer))
            { failReason = "Signing failed validation."; return false; }

            if (!team.RosterPlayerIds.Contains(playerId))
                team.RosterPlayerIds.Add(playerId);
            team.InvalidateRosterCache();

            gm.Transactions?.Add(TransactionType.Signing, gm.CurrentDate,
                new List<string> { team.TeamId }, new List<string> { playerId },
                $"{player.FullName} signs with {team.Name} (rest of season, minimum)");

            InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                $"{player.FullName} signs with {team.Name}",
                "Rest-of-season minimum contract.",
                highPriority: true, deepLinkPanelId: "Roster", deepLinkPayload: playerId);

            return true;
        }
    }
}
