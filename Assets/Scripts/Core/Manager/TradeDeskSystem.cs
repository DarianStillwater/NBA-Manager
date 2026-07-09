using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// The league's trade market. Runs AI-to-AI trades through the season with a
    /// frenzy in the final week before the deadline, and resolves incoming AI
    /// offers the player accepts or declines from the Front Office desk.
    /// Roster syncing happens in GameManager.OnTradeExecutedHandler, which every
    /// execution path reaches via TradeSystem.OnTradeExecuted.
    /// </summary>
    public class TradeDeskSystem : IDailyTickable
    {
        public string SystemId => "TradeDesk";
        public int TickOrder => Manager.TickOrder.TradeOffers + 10; // after fresh offers land

        private readonly PlayerDatabase _playerDb;
        private readonly SalaryCapManager _capManager;
        private readonly TradeSystem _trades;
        private readonly AITradeEvaluator _evaluator;
        private readonly AITradeOfferGenerator _offerGen;
        private readonly TradeValidator _validator;
        private readonly Func<string, Team> _teamLookup;
        private readonly Func<List<Team>> _allTeams;

        // League activity tuning
        private const float BASE_DAILY_TRADE_CHANCE = 0.04f;   // ~1 league trade / 25 days
        private const float FORTNIGHT_TRADE_CHANCE = 0.12f;    // 14 days out
        private const float FRENZY_TRADE_CHANCE = 0.30f;       // final week
        private const int DEADLINE_DAY_ATTEMPTS = 4;           // deadline-day flurry
        private const float DEADLINE_DAY_CHANCE = 0.65f;       // per attempt

        public event Action<TradeProposal, string, string> OnLeagueTradeExecuted; // proposal, sellerId, buyerId

        public TradeDeskSystem(
            PlayerDatabase playerDb,
            SalaryCapManager capManager,
            TradeSystem trades,
            AITradeEvaluator evaluator,
            AITradeOfferGenerator offerGen,
            DraftPickRegistry draftPickRegistry,
            Func<string, Team> teamLookup,
            Func<List<Team>> allTeams)
        {
            _playerDb = playerDb;
            _capManager = capManager;
            _trades = trades;
            _evaluator = evaluator;
            _offerGen = offerGen;
            _teamLookup = teamLookup;
            _allTeams = allTeams;
            _validator = new TradeValidator(capManager, draftPickRegistry);
        }

        public static TradeDeskSystem CreateDefault(GameManager gm)
        {
            return new TradeDeskSystem(
                gm.PlayerDatabase,
                gm.SalaryCapManager,
                gm.Trades,
                gm.TradeEvaluator,
                gm.TradeOfferGenerator,
                gm.DraftPickRegistry,
                gm.GetTeam,
                () => gm.AllTeams);
        }

        // ==================== DAILY LEAGUE ACTIVITY ====================

        public void DailyTick(in DailyTickContext ctx)
        {
            RunLeagueTradeDay(ctx.Date, ctx.PlayerTeamId);
        }

        /// <summary>
        /// One day of AI-to-AI trade activity. Public so tests can drive specific
        /// dates without a registry.
        /// </summary>
        public int RunLeagueTradeDay(DateTime date, string playerTeamId)
        {
            if (OffseasonManager.Instance != null && OffseasonManager.Instance.EngineActive) return 0;
            if (PlayoffManager.Instance != null && PlayoffManager.Instance.IsPlayoffsActive) return 0;

            // Window: from just after opening night (Oct 25) to the Feb deadline.
            // Mar–Jul fall past the deadline check; Aug–Oct 24 are blocked here.
            int seasonEndYear = date.Month > 7 ? date.Year + 1 : date.Year;
            var deadline = LeagueCBA.GetTradeDeadline(seasonEndYear);
            if (date.Date > deadline.Date) return 0;
            if (date.Month == 8 || date.Month == 9) return 0;
            if (date.Month == 10 && date.Day < 25) return 0;

            int daysOut = (deadline.Date - date.Date).Days;

            int attempts; float chance;
            if (daysOut == 0) { attempts = DEADLINE_DAY_ATTEMPTS; chance = DEADLINE_DAY_CHANCE; }
            else if (daysOut <= 7) { attempts = 1; chance = FRENZY_TRADE_CHANCE; }
            else if (daysOut <= 14) { attempts = 1; chance = FORTNIGHT_TRADE_CHANCE; }
            else { attempts = 1; chance = BASE_DAILY_TRADE_CHANCE; }

            int executed = 0;
            for (int i = 0; i < attempts; i++)
            {
                if (UnityEngine.Random.value >= chance) continue;
                if (TryExecuteLeagueTrade(date, playerTeamId)) executed++;
            }
            return executed;
        }

        /// <summary>
        /// Build and, if both front offices agree, execute one AI-to-AI trade:
        /// a seller moving a veteran to a buyer for salary-matched players and
        /// possibly a pick. Returns true when a trade executed.
        /// </summary>
        public bool TryExecuteLeagueTrade(DateTime date, string playerTeamId)
        {
            var profiles = _offerGen?.GetAllFrontOffices();
            if (profiles == null || profiles.Count < 2) return false;

            var aiProfiles = profiles.Values
                .Where(fo => fo.TeamId != playerTeamId && _teamLookup(fo.TeamId) != null)
                .ToList();
            if (aiProfiles.Count < 2) return false;

            var sellers = aiProfiles.Where(fo =>
                fo.CurrentSituation == TeamSituation.Rebuilding ||
                fo.CurrentSituation == TeamSituation.StuckInMiddle ||
                _capManager.GetCapStatus(fo.TeamId) >= TeamCapStatus.InLuxuryTax).ToList();
            var buyers = aiProfiles.Where(fo =>
                fo.CurrentSituation == TeamSituation.Championship ||
                fo.CurrentSituation == TeamSituation.Contending ||
                fo.CurrentSituation == TeamSituation.PlayoffBubble).ToList();
            if (sellers.Count == 0 || buyers.Count == 0) return false;

            var seller = sellers[UnityEngine.Random.Range(0, sellers.Count)];
            var buyerPool = buyers.Where(b => b.TeamId != seller.TeamId).ToList();
            if (buyerPool.Count == 0) return false;
            var buyer = buyerPool[UnityEngine.Random.Range(0, buyerPool.Count)];

            var proposal = BuildLeagueProposal(seller, buyer, date);
            if (proposal == null) return false;

            if (!_validator.ValidateTrade(proposal).IsValid) return false;

            // Both AI front offices must sign off through the real evaluator.
            if (!(_evaluator.EvaluateTrade(proposal, seller.TeamId)?.IsAcceptable ?? false)) return false;
            if (!(_evaluator.EvaluateTrade(proposal, buyer.TeamId)?.IsAcceptable ?? false)) return false;

            _trades.ExecuteTrade(proposal);
            OnLeagueTradeExecuted?.Invoke(proposal, seller.TeamId, buyer.TeamId);
            return true;
        }

        private TradeProposal BuildLeagueProposal(FrontOfficeProfile seller, FrontOfficeProfile buyer, DateTime date)
        {
            var target = PickSellerTarget(seller);
            if (target.player == null || target.contract == null) return null;

            var proposal = new TradeProposal { ProposedDate = date };
            proposal.AllAssets.Add(new TradeAsset
            {
                Type = TradeAssetType.Player,
                PlayerId = target.player.PlayerId,
                Salary = target.contract.CurrentYearSalary,
                SendingTeamId = seller.TeamId,
                ReceivingTeamId = buyer.TeamId
            });

            // Salary-match from the buyer's mid-tier — never their best players.
            var buyerRoster = RosterWithContracts(buyer.TeamId)
                .Where(pc => pc.player.OverallRating < target.player.OverallRating)
                .OrderByDescending(pc => pc.contract.CurrentYearSalary)
                .ToList();

            // The seller must have roster room for the return package (max 15
            // after the swap) or validation kills the deal every time.
            int sellerCount = _teamLookup(seller.TeamId)?.RosterPlayerIds?.Count ?? 15;
            int maxIncoming = Math.Max(1, Math.Min(3, 15 - sellerCount + 1));

            long needed = target.contract.CurrentYearSalary;
            long offered = 0;
            foreach (var (player, contract) in buyerRoster)
            {
                if (offered >= (long)(needed * 0.8f)) break;
                if (proposal.GetOutgoingPlayerCount(buyer.TeamId) >= maxIncoming) break;

                proposal.AllAssets.Add(new TradeAsset
                {
                    Type = TradeAssetType.Player,
                    PlayerId = player.PlayerId,
                    Salary = contract.CurrentYearSalary,
                    SendingTeamId = buyer.TeamId,
                    ReceivingTeamId = seller.TeamId
                });
                offered += contract.CurrentYearSalary;
            }

            if (proposal.GetOutgoingPlayerCount(buyer.TeamId) == 0) return null;

            return proposal;
        }

        private (Player player, Contract contract) PickSellerTarget(FrontOfficeProfile seller)
        {
            // Movable veterans: real salary, not the franchise's untouchable star.
            var candidates = RosterWithContracts(seller.TeamId)
                .Where(pc => pc.player.Age >= 26
                          && pc.contract.CurrentYearSalary >= 3_000_000L
                          && pc.contract.CurrentYearSalary <= 40_000_000L
                          && pc.player.OverallRating <= 86)
                .ToList();
            if (candidates.Count == 0) return (null, null);

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private List<(Player player, Contract contract)> RosterWithContracts(string teamId)
        {
            var result = new List<(Player, Contract)>();
            var team = _teamLookup(teamId);
            if (team?.RosterPlayerIds == null) return result;

            foreach (var id in team.RosterPlayerIds)
            {
                var player = _playerDb?.GetPlayer(id);
                var contract = _capManager.GetContract(id);
                if (player != null && contract != null && player.RetirementYear <= 0)
                    result.Add((player, contract));
            }
            return result;
        }

        // ==================== PLAYER DESK: INCOMING OFFERS ====================

        /// <summary>
        /// Accept a pending incoming offer: restamp its date, validate against the
        /// CBA, and execute. The offering AI already agreed — no re-evaluation.
        /// </summary>
        public TradeResult AcceptIncomingOffer(string offerId, DateTime date)
        {
            var offer = _offerGen?.GetAllOffers().FirstOrDefault(o => o.OfferId == offerId);

            if (offer == null || offer.Status != IncomingOfferStatus.Pending)
                return Invalid("This offer is no longer on the table.");

            if (offer.Proposal == null || offer.Proposal.AllAssets == null || offer.Proposal.AllAssets.Count == 0)
            {
                offer.Status = IncomingOfferStatus.Expired;
                return Invalid("The offer details were lost — it has been withdrawn.");
            }

            offer.Proposal.ProposedDate = date;
            var result = _trades.FinalizeAgreedTrade(offer.Proposal);

            if (result.Status == TradeStatus.Completed)
                _offerGen.RespondToOffer(offerId, IncomingOfferResponse.Accept);

            return result;
        }

        public void DeclineIncomingOffer(string offerId)
        {
            _offerGen?.RespondToOffer(offerId, IncomingOfferResponse.Reject);
        }

        private static TradeResult Invalid(string reason)
        {
            var validation = new TradeValidationResult { IsValid = false };
            validation.Issues.Add(reason);
            return new TradeResult { Status = TradeStatus.Invalid, ValidationResult = validation };
        }

        // ==================== ROSTER SYNC ====================

        /// <summary>
        /// Applies a trade's player movement to Team.RosterPlayerIds and repairs
        /// any starting lineup that lost a player to the deal. Called from
        /// GameManager on every executed trade; static so tests can drive it
        /// against local teams.
        /// </summary>
        public static void ApplyRosterSync(TradeProposal proposal, Func<string, Team> teamLookup)
        {
            if (proposal == null || teamLookup == null) return;

            var touched = new HashSet<Team>();
            foreach (var asset in proposal.AllAssets)
            {
                if (asset.Type != TradeAssetType.Player || string.IsNullOrEmpty(asset.PlayerId)) continue;

                var from = teamLookup(asset.SendingTeamId);
                if (from != null)
                {
                    from.RosterPlayerIds.Remove(asset.PlayerId);
                    touched.Add(from);
                }

                var to = teamLookup(asset.ReceivingTeamId);
                if (to != null && !to.RosterPlayerIds.Contains(asset.PlayerId))
                {
                    to.RosterPlayerIds.Add(asset.PlayerId);
                    touched.Add(to);
                }
            }

            foreach (var team in touched)
            {
                team.InvalidateRosterCache();
                RepairLineup(team);
            }
        }

        private static void RepairLineup(Team team)
        {
            if (team.StartingLineupIds == null) return;

            bool lineupBroken = team.StartingLineupIds.Any(id =>
                !string.IsNullOrEmpty(id) && !team.RosterPlayerIds.Contains(id));
            if (!lineupBroken) return;

            team.AutoSetStartingLineup(team.CoachPersonality);

            // AutoSetStartingLineup needs resolvable Player objects; if any hole
            // survives (headless context, unresolved ids), patch it with a bench id.
            for (int i = 0; i < team.StartingLineupIds.Length; i++)
            {
                var id = team.StartingLineupIds[i];
                if (!string.IsNullOrEmpty(id) && team.RosterPlayerIds.Contains(id)) continue;
                team.StartingLineupIds[i] = team.RosterPlayerIds
                    .FirstOrDefault(pid => !team.StartingLineupIds.Contains(pid));
            }
        }
    }
}
