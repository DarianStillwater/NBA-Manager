using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// The July market. Rival teams bid on the top free agents, bids escalate day
    /// over day, each marketed free agent picks a winner on his decision day, and a
    /// winning bid on a restricted free agent becomes an offer sheet the original
    /// team must match by the next daily tick.
    ///
    /// Owns all bid/offer-sheet state; OffseasonManager just calls RunDay and
    /// persists what ToSave returns. Constructed with explicit dependencies (no
    /// GameManager) so it is directly testable.
    /// </summary>
    public class FreeAgencyMarket
    {
        /// <summary>How many free agents are "on the market" (get AI bids) per day.</summary>
        public const int MARKETED_COUNT = 15;
        public const int MAX_BIDDERS_PER_FA = 3;
        /// <summary>Market value at or above which a signing is league news.</summary>
        public const long STAR_VALUE = 22_000_000L;
        private const float ESCALATION = 1.08f;

        private readonly FreeAgentManager _fam;
        private readonly SalaryCapManager _cap;
        private readonly PlayerDatabase _db;
        private readonly Func<List<Team>> _teamSource;
        private readonly Func<string> _playerTeamSource;
        private System.Random _rng;

        private readonly List<FreeAgentOffer> _bids = new List<FreeAgentOffer>();
        private readonly List<RestrictedFreeAgentStatus> _sheets = new List<RestrictedFreeAgentStatus>();
        private readonly Dictionary<string, DateTime> _decisionDays = new Dictionary<string, DateTime>();
        private readonly List<string> _marketed = new List<string>();
        private readonly List<string> _digest = new List<string>();

        private DateTime _today;

        /// <summary>Last day's league-wide signings summary (also posted to the inbox).</summary>
        public string LastDigest { get; private set; } = "";

        public FreeAgencyMarket(FreeAgentManager fam, SalaryCapManager cap, PlayerDatabase db,
            Func<List<Team>> teamSource, Func<string> playerTeamSource, int seed,
            DateTime today = default)
        {
            _fam = fam;
            _cap = cap;
            _db = db;
            _teamSource = teamSource ?? (() => new List<Team>());
            _playerTeamSource = playerTeamSource ?? (() => "");
            _rng = new System.Random(seed);
            _today = today;
        }

        // ==================== PUBLIC QUERY SURFACE (market browser UI) ====================

        /// <summary>Free agents currently drawing AI interest, best first.</summary>
        public IReadOnlyList<string> MarketedPlayerIds => _marketed;

        /// <summary>Live bids on a free agent (all teams), highest first.</summary>
        public List<FreeAgentOffer> GetBids(string playerId) => _bids
            .Where(b => b.PlayerId == playerId && b.Status == FreeAgentOfferStatus.Pending)
            .OrderByDescending(b => b.AnnualAverage).ToList();

        /// <summary>The day this free agent decides, or null if he isn't on the clock.</summary>
        public DateTime? GetDecisionDay(string playerId) =>
            _decisionDays.TryGetValue(playerId, out var d) ? d : (DateTime?)null;

        /// <summary>True when a rival team has a live bid on this free agent.</summary>
        public bool IsContested(string playerId, string excludingTeamId = null) => _bids.Any(b =>
            b.PlayerId == playerId && b.Status == FreeAgentOfferStatus.Pending &&
            b.TeamId != excludingTeamId);

        /// <summary>Your team's live bid on this free agent, if any.</summary>
        public FreeAgentOffer GetPlayerTeamBid(string playerId)
        {
            string me = _playerTeamSource();
            return _bids.FirstOrDefault(b => b.PlayerId == playerId && b.TeamId == me &&
                                             b.Status == FreeAgentOfferStatus.Pending);
        }

        /// <summary>RFA offer sheets awaiting YOUR match-or-walk call.</summary>
        public IReadOnlyList<RestrictedFreeAgentStatus> PendingMatchDecisions
        {
            get
            {
                string me = _playerTeamSource();
                return _sheets.Where(s => s.OriginalTeamId == me).ToList();
            }
        }

        /// <summary>
        /// Fuzzy agent chatter about rival interest — bucketed and never exact, but
        /// stable for a given (player, day) so re-opening the screen doesn't reroll.
        /// </summary>
        public string GetAgentIntel(string playerId)
        {
            var bids = GetBids(playerId);
            if (bids.Count == 0)
                return "Quiet so far — his agent says nobody has put a number in front of him.";

            string me = _playerTeamSource();
            long top = bids.Max(b => b.AnnualAverage);

            // Deterministic per (player, day) skew, then round to the nearest $2M so
            // the exact figure never leaks.
            int h = StableHash(playerId + "|" + _today.ToString("yyyyMMdd"));
            float skew = 0.9f + (h % 21) / 100f;               // 0.90x - 1.10x
            long fuzzed = (long)(top * skew);
            long bucket = Math.Max(2_000_000L, (fuzzed + 1_000_000L) / 2_000_000L * 2_000_000L);

            string crowd = bids.Count switch
            {
                1 => "one team is in on him",
                2 => "a couple of teams are circling",
                3 or 4 => "several teams are circling",
                _ => "it's a crowded market"
            };
            string leader = bids[0].TeamId == me ? " You're the name he keeps mentioning." : "";
            var day = GetDecisionDay(playerId);
            string clock = day.HasValue ? $" He wants to decide by {day.Value:MMM d}." : "";

            return $"His agent says {crowd} — top offer somewhere around ${bucket / 1_000_000f:0}M a year.{leader}{clock}";
        }

        // ==================== PLAYER-TEAM ACTIONS ====================

        /// <summary>
        /// Place or raise your team's bid on a marketed free agent. The salary is
        /// validated through FreeAgentManager.CanSign, trying cap space, then the
        /// MLE, then the BAE, then a minimum deal.
        /// </summary>
        public bool PlaceBid(string playerId, int years, long annualSalary, out string failReason)
        {
            failReason = "";
            string me = _playerTeamSource();
            if (string.IsNullOrEmpty(me)) { failReason = "No team."; return false; }

            var fa = _fam?.GetFreeAgents()?.FirstOrDefault(f => f.PlayerId == playerId);
            var player = _db?.GetPlayer(playerId);
            if (fa == null || player == null) { failReason = "No longer a free agent."; return false; }
            if (_sheets.Any(s => s.PlayerId == playerId))
            { failReason = "An offer sheet is already out on him."; return false; }

            years = Mathf.Clamp(years, 1, 5);
            var terms = Afford(me, playerId, player, annualSalary, years);
            if (terms == null) { failReason = "You can't fit that offer under the cap."; return false; }

            RecordBid(playerId, me, terms);
            EnsureDecisionDay(playerId, player);
            return true;
        }

        /// <summary>True while an offer sheet on this free agent is awaiting a match call.</summary>
        public bool HasOfferSheet(string playerId) => _sheets.Any(s => s.PlayerId == playerId);

        /// <summary>League-minimum salary for this free agent's years of service.</summary>
        public long MinSalaryFor(string playerId) =>
            LeagueCBA.GetMinimumSalary(ServiceYears(_db?.GetPlayer(playerId)));

        /// <summary>
        /// Forget everything about a player — bids, offer sheet, decision day. Called
        /// when he signs outside the market (an own-FA re-signing), so the UI doesn't
        /// keep showing ghost bids until the next daily prune.
        /// </summary>
        public void DropPlayer(string playerId)
        {
            _bids.RemoveAll(b => b.PlayerId == playerId);
            _sheets.RemoveAll(s => s.PlayerId == playerId);
            _decisionDays.Remove(playerId);
        }

        /// <summary>Pull your team's bid.</summary>
        public bool WithdrawBid(string playerId)
        {
            string me = _playerTeamSource();
            return _bids.RemoveAll(b => b.PlayerId == playerId && b.TeamId == me) > 0;
        }

        /// <summary>
        /// Match or decline an RFA offer sheet on your own player. Declining lets the
        /// sheet go through as a signing with the bidding team.
        /// </summary>
        public bool ResolveMatchDecision(string playerId, bool match, out string failReason)
        {
            failReason = "";
            string me = _playerTeamSource();
            var sheet = _sheets.FirstOrDefault(s => s.PlayerId == playerId && s.OriginalTeamId == me);
            if (sheet == null) { failReason = "No offer sheet pending."; return false; }

            var offer = sheet.OfferSheets.FirstOrDefault();
            if (offer == null) { _sheets.Remove(sheet); failReason = "Offer sheet is empty."; return false; }

            if (match && !TryMatch(sheet, offer))
            { failReason = "You can't legally match that sheet."; return false; }

            if (!match) Sign(offer, note: "declined match");
            _sheets.Remove(sheet);
            return true;
        }

        // ==================== DAILY MARKET ====================

        /// <summary>
        /// One market day: yesterday's offer sheets resolve, AI teams bid (and raise),
        /// free agents whose decision day arrived pick a winner, and the day's
        /// signings go out as one digest.
        /// </summary>
        public void RunDay(DateTime date)
        {
            if (_fam == null) return;
            _today = date;
            _digest.Clear();

            var pool = _fam.GetFreeAgents() ?? new List<FreeAgent>();
            var live = new HashSet<string>(pool.Select(f => f.PlayerId));
            _bids.RemoveAll(b => !live.Contains(b.PlayerId));
            _sheets.RemoveAll(s => !live.Contains(s.PlayerId));
            foreach (var gone in _decisionDays.Keys.Where(k => !live.Contains(k)).ToList())
                _decisionDays.Remove(gone);

            ResolveOfferSheets(date);

            // Sheet resolutions just signed players — re-read the pool before bidding
            pool = _fam.GetFreeAgents() ?? new List<FreeAgent>();

            var marketed = Marketed(pool);
            _marketed.Clear();
            foreach (var entry in marketed)
            {
                _marketed.Add(entry.fa.PlayerId);
                EnsureDecisionDay(entry.fa.PlayerId, entry.player);
                GenerateBids(entry.fa, entry.player);
            }

            ResolveDecisions(pool, date);
            PublishDigest();
        }

        private List<(FreeAgent fa, Player player)> Marketed(List<FreeAgent> pool)
        {
            return pool
                .Select(fa => (fa, player: _db?.GetPlayer(fa.PlayerId)))
                .Where(x => x.player != null && x.player.RetirementYear == 0 &&
                            !_sheets.Any(s => s.PlayerId == x.fa.PlayerId))
                .OrderByDescending(x => OffseasonManager.MarketValue(x.player))
                .ThenBy(x => x.fa.PlayerId)
                .Take(MARKETED_COUNT)
                .ToList();
        }

        private void EnsureDecisionDay(string playerId, Player player)
        {
            if (_decisionDays.ContainsKey(playerId)) return;
            int extra = OffseasonManager.MarketValue(player) >= STAR_VALUE ? 2 : 0;
            _decisionDays[playerId] = _today.Date.AddDays(2 + extra + _rng.Next(2));
        }

        /// <summary>
        /// One new suitor per free agent per day (so a market builds up), plus
        /// escalation: an existing bidder who's been outbid raises if it can.
        /// </summary>
        private void GenerateBids(FreeAgent fa, Player player)
        {
            string playerId = fa.PlayerId;
            long ask = OffseasonManager.MarketValue(player);
            var existing = GetBids(playerId);
            long top = existing.Count > 0 ? existing[0].AnnualAverage : 0L;

            // Bidding war: everyone below the leading number takes one more swing
            foreach (var bid in existing.Where(b => b.AnnualAverage < top))
            {
                var raise = Afford(bid.TeamId, playerId, player,
                    (long)(top * ESCALATION), bid.Years);
                if (raise != null && raise.Salary > bid.AnnualAverage)
                {
                    bid.AnnualAverage = raise.Salary;
                    bid.TotalValue = raise.Salary * bid.Years;
                    bid.Method = raise.Method;
                    bid.OfferDate = _today;
                }
            }

            int target = 1 + _rng.Next(MAX_BIDDERS_PER_FA);
            if (existing.Count >= target) return;

            string me = _playerTeamSource();
            bool playerControls = !string.IsNullOrEmpty(me) && RolePermissions.CanMakeRosterMoves;
            var bidderIds = new HashSet<string>(existing.Select(b => b.TeamId));

            var candidates = (_teamSource() ?? new List<Team>())
                .Where(t => t != null && !bidderIds.Contains(t.TeamId) &&
                            !(playerControls && t.TeamId == me) &&
                            _cap.GetStandardContractCount(t.TeamId) < RosterManager.STANDARD_ROSTER_MAX)
                .OrderByDescending(t => _cap.GetCapSpace(t.TeamId))
                .ThenBy(t => t.TeamId)
                .Take(6)
                .ToList();
            if (candidates.Count == 0) return;

            var team = candidates[_rng.Next(candidates.Count)];
            long bidSalary = top > 0 ? (long)(top * ESCALATION) : ask;
            int years = ask >= STAR_VALUE ? 3 + _rng.Next(2) : 1 + _rng.Next(3);

            var terms = Afford(team.TeamId, playerId, player, bidSalary, years);
            if (terms == null) return;
            RecordBid(playerId, team.TeamId, terms);
        }

        private void RecordBid(string playerId, string teamId, Terms terms)
        {
            var bid = _bids.FirstOrDefault(b => b.PlayerId == playerId && b.TeamId == teamId);
            if (bid == null)
            {
                bid = new FreeAgentOffer
                {
                    OfferId = $"{playerId}|{teamId}",
                    PlayerId = playerId,
                    TeamId = teamId,
                    Status = FreeAgentOfferStatus.Pending
                };
                _bids.Add(bid);
            }
            bid.Years = terms.Years;
            bid.AnnualAverage = terms.Salary;
            bid.TotalValue = terms.Salary * terms.Years;
            bid.Method = terms.Method;
            bid.OfferDate = _today;
            bid.Status = FreeAgentOfferStatus.Pending;
        }

        private void ResolveDecisions(List<FreeAgent> pool, DateTime date)
        {
            foreach (var playerId in _decisionDays
                         .Where(kv => kv.Value.Date <= date.Date)
                         .Select(kv => kv.Key).ToList())
            {
                var bids = GetBids(playerId);
                if (bids.Count == 0) continue;

                var fa = pool.FirstOrDefault(f => f.PlayerId == playerId);
                var player = _db?.GetPlayer(playerId);
                if (fa == null || player == null) { _decisionDays.Remove(playerId); continue; }

                var best = bids.OrderByDescending(b => Score(b, player)).First();

                bool restricted = fa.Type == FreeAgentType.Restricted && fa.HasQualifyingOffer &&
                                  !string.IsNullOrEmpty(fa.PreviousTeamId) &&
                                  fa.PreviousTeamId != best.TeamId;
                if (restricted)
                {
                    OpenOfferSheet(fa, player, best, date);
                    continue;
                }

                Sign(best);
            }
        }

        /// <summary>
        /// Deterministic free-agent taste: money leads, winning matters, length is a
        /// tiebreak, plus a stable per-offer quirk so identical offers still split.
        /// ponytail: no personality wiring (front-office profiles aren't loaded in
        /// the offseason path) — add a fit term here if it starts feeling flat.
        /// </summary>
        private float Score(FreeAgentOffer offer, Player player)
        {
            var team = FindTeam(offer.TeamId);
            float score = offer.AnnualAverage / 1_000_000f;
            // ponytail: wins nudge, money decides — 82 wins can't outweigh $5M/yr.
            score += (team?.Wins ?? 0) * 0.05f;
            score += offer.Years * 0.5f;
            score += (StableHash(offer.OfferId) % 5) * 0.2f;
            return score;
        }

        // ==================== RESTRICTED FREE AGENCY ====================

        private void OpenOfferSheet(FreeAgent fa, Player player, FreeAgentOffer best, DateTime date)
        {
            var sheet = new RestrictedFreeAgentStatus
            {
                PlayerId = fa.PlayerId,
                OriginalTeamId = fa.PreviousTeamId,
                MatchDeadline = date.Date.AddDays(1),
                QualifyingOffer = new QualifyingOffer
                {
                    PlayerId = fa.PlayerId,
                    TeamId = fa.PreviousTeamId,
                    Amount = fa.QualifyingOfferAmount,
                    Extended = true
                }
            };
            sheet.OfferSheets.Add(best);
            _sheets.Add(sheet);

            _bids.RemoveAll(b => b.PlayerId == fa.PlayerId);
            _decisionDays.Remove(fa.PlayerId);

            string bidder = FindTeam(best.TeamId)?.Name ?? best.TeamId;
            string original = FindTeam(fa.PreviousTeamId)?.Name ?? fa.PreviousTeamId;
            _digest.Add($"{player.FullName} signs an offer sheet with {bidder} " +
                        $"({best.Years}yr, {Money(best.AnnualAverage)}/yr) — {original} must match.");

            bool mine = fa.PreviousTeamId == _playerTeamSource();
            if (mine || OffseasonManager.MarketValue(player) >= STAR_VALUE)
                InboxService.Instance?.Publish(InboxMessageType.League,
                    mine ? "Front Office" : "League Office",
                    mine
                        ? $"OFFER SHEET: {player.FullName} — match or lose him"
                        : $"{player.FullName} signs an offer sheet with {bidder}",
                    $"{bidder} put down {best.Years} years at {Money(best.AnnualAverage)} a year. " +
                    (mine
                        ? $"You have until {sheet.MatchDeadline:MMM d} to match. Ignoring it lets the front office decide."
                        : $"{original} holds first refusal."),
                    highPriority: mine,
                    deepLinkPanelId: "FrontOffice",
                    deepLinkPayload: fa.PlayerId);
        }

        private void ResolveOfferSheets(DateTime date)
        {
            // Strictly after the deadline: the stated match-by day is the player's to use
            foreach (var sheet in _sheets.Where(s => s.MatchDeadline.Date < date.Date).ToList())
            {
                var offer = sheet.OfferSheets.FirstOrDefault();
                if (offer == null) { _sheets.Remove(sheet); continue; }

                if (ShouldMatch(sheet, offer) && TryMatch(sheet, offer))
                    _sheets.Remove(sheet);
                else
                {
                    Sign(offer, note: "no match");
                    _sheets.Remove(sheet);
                }
            }
        }

        /// <summary>
        /// AI first refusal: match a sheet worth up to ~110% of the player's market
        /// value, but only while the match keeps the team under the tax line.
        /// </summary>
        private bool ShouldMatch(RestrictedFreeAgentStatus sheet, FreeAgentOffer offer)
        {
            var player = _db?.GetPlayer(sheet.PlayerId);
            if (player == null) return false;
            long value = OffseasonManager.MarketValue(player);
            if (offer.AnnualAverage > (long)(value * 1.10f)) return false;
            return _cap.GetTeamPayroll(sheet.OriginalTeamId) + offer.AnnualAverage
                   <= LeagueCBA.LUXURY_TAX_LINE;
        }

        private bool TryMatch(RestrictedFreeAgentStatus sheet, FreeAgentOffer offer)
        {
            var player = _db?.GetPlayer(sheet.PlayerId);
            if (player == null) return false;

            var terms = Afford(sheet.OriginalTeamId, sheet.PlayerId, player,
                offer.AnnualAverage, offer.Years, exactTerms: true);
            if (terms == null) return false;

            var matched = new FreeAgentOffer
            {
                OfferId = $"{sheet.PlayerId}|{sheet.OriginalTeamId}",
                PlayerId = sheet.PlayerId,
                TeamId = sheet.OriginalTeamId,
                Years = terms.Years,
                AnnualAverage = terms.Salary,
                TotalValue = terms.Salary * terms.Years,
                Method = terms.Method,
                OfferDate = _today
            };
            if (!Sign(matched, note: "matched")) return false;
            sheet.MatchedOffer = matched;
            offer.Status = FreeAgentOfferStatus.Matched;
            return true;
        }

        // ==================== SIGNING ====================

        private bool Sign(FreeAgentOffer offer, string note = null)
        {
            var player = _db?.GetPlayer(offer.PlayerId);
            var team = FindTeam(offer.TeamId);
            if (player == null || team == null) return false;

            var fa = _fam.GetFreeAgents()?.FirstOrDefault(f => f.PlayerId == offer.PlayerId);
            string previousTeamId = fa?.PreviousTeamId;

            var signing = new SigningOffer
            {
                AnnualSalary = offer.AnnualAverage,
                Years = offer.Years,
                Method = offer.Method,
                PlayerYearsExperience = ServiceYears(player)
            };
            if (!_fam.ExecuteSigning(offer.TeamId, offer.PlayerId, signing))
            {
                _bids.RemoveAll(b => b.OfferId == offer.OfferId);
                return false;
            }

            if (!team.RosterPlayerIds.Contains(offer.PlayerId))
                team.RosterPlayerIds.Add(offer.PlayerId);

            offer.Status = FreeAgentOfferStatus.Accepted;
            _bids.RemoveAll(b => b.PlayerId == offer.PlayerId);
            _decisionDays.Remove(offer.PlayerId);

            string suffix = string.IsNullOrEmpty(note) ? "" : $" [{note}]";
            _digest.Add($"{player.FullName} → {team.Name} " +
                        $"({offer.Years}yr, {Money(offer.AnnualAverage)}/yr){suffix}");

            string me = _playerTeamSource();
            bool mine = offer.TeamId == me;
            bool lost = !mine && previousTeamId == me;
            long value = OffseasonManager.MarketValue(player);
            if (mine || lost || value >= STAR_VALUE)
                InboxService.Instance?.Publish(InboxMessageType.League, "League Office",
                    lost
                        ? $"{player.FullName} leaves for {team.Name}"
                        : $"{player.FullName} signs with {team.Name}",
                    $"{offer.Years} years at {Money(offer.AnnualAverage)} a year" +
                    (string.IsNullOrEmpty(note) ? "." : $" ({note})."),
                    highPriority: mine || lost,
                    deepLinkPanelId: "FrontOffice");
            return true;
        }

        private void PublishDigest()
        {
            if (_digest.Count == 0) return;    // quiet day: yesterday's wire stands
            LastDigest = string.Join("\n", _digest);
            InboxService.Instance?.Publish(InboxMessageType.League, "League Wire",
                $"Free agency: {_digest.Count} move(s) around the league",
                LastDigest, deepLinkPanelId: "FrontOffice");
        }

        // ==================== CAP PLUMBING ====================

        private class Terms
        {
            public long Salary;
            public int Years;
            public SigningMethod Method;
        }

        /// <summary>
        /// Best legal way for a team to put roughly this number on the table: cap
        /// space (trimmed to what's there unless an exact match is required), then
        /// the MLE, then the BAE, then a minimum deal. Years step down until the
        /// CBA validator accepts — unless exactTerms demands the salary AND length
        /// on the table (an RFA match, which can't shorten the deal).
        /// </summary>
        private Terms Afford(string teamId, string playerId, Player player, long target, int years,
            bool exactTerms = false)
        {
            if (string.IsNullOrEmpty(teamId)) return null;
            years = Mathf.Clamp(years, 1, 5);
            int service = ServiceYears(player);
            long minSalary = LeagueCBA.GetMinimumSalary(service);
            target = Math.Max(minSalary, target);

            long capSpace = _cap.GetCapSpace(teamId);
            var usage = _fam.GetUsage(teamId);
            var (mleAmount, _, _) = _cap.GetAvailableMLE(teamId);
            long mleRoom = Math.Max(0L, mleAmount - usage.MLEUsed);

            var attempts = new List<(SigningMethod method, long salary)>();
            // Own free agent: Bird rights are how an over-the-cap team keeps (or
            // matches for) its own player
            var fa = _fam.GetFreeAgents()?.FirstOrDefault(f => f.PlayerId == playerId);
            if (fa != null && fa.PreviousTeamId == teamId && fa.BirdRights != BirdRightsType.None)
                attempts.Add((SigningMethod.BirdRights, target));
            if (capSpace >= target)
                attempts.Add((SigningMethod.CapSpace, target));
            else if (!exactTerms && capSpace >= Math.Max(minSalary, target / 2))
                attempts.Add((SigningMethod.CapSpace, capSpace));    // lowball, still real money
            if (mleRoom >= target)
                attempts.Add((SigningMethod.MidLevelException, target));
            else if (!exactTerms && mleRoom >= Math.Max(minSalary, target / 2))
                attempts.Add((SigningMethod.MidLevelException, mleRoom));
            if (!usage.BiAnnualUsed && target <= LeagueCBA.BI_ANNUAL_EXCEPTION)
                attempts.Add((SigningMethod.BiAnnualException, target));
            if (target <= minSalary)
                attempts.Add((SigningMethod.MinimumSalary, minSalary));

            int shortest = exactTerms ? years : 1;
            foreach (var (method, salary) in attempts)
            {
                for (int y = years; y >= shortest; y--)
                {
                    var probe = new SigningOffer
                    {
                        AnnualSalary = salary,
                        Years = y,
                        Method = method,
                        PlayerYearsExperience = service
                    };
                    if (_fam.CanSign(teamId, playerId, probe).IsValid)
                        return new Terms { Salary = salary, Years = y, Method = method };
                }
            }
            return null;
        }

        /// <summary>
        /// Years of service. YearsPro is 0 for every shipped player, so entry year
        /// is the real source; undrafted players fall back to YearsPro.
        /// </summary>
        private int ServiceYears(Player player)
        {
            if (player == null) return 0;
            if (player.DraftYear > 0 && _today.Year > 1)
                return Math.Max(0, _today.Year - player.DraftYear);
            return player.YearsPro;
        }

        private Team FindTeam(string teamId) =>
            string.IsNullOrEmpty(teamId) ? null
                : (_teamSource() ?? new List<Team>()).FirstOrDefault(t => t?.TeamId == teamId);

        private static string Money(long v) => v >= 1_000_000L
            ? $"${v / 1_000_000f:0.0}M" : $"${v / 1_000f:0}K";

        private static int StableHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int h = 17;
            foreach (char c in s) h = unchecked(h * 31 + c);
            return h & 0x7FFFFFF;
        }

        // ==================== PERSISTENCE ====================

        /// <summary>Wipe all market state (call before restoring a save).</summary>
        public void Clear()
        {
            _bids.Clear();
            _sheets.Clear();
            _decisionDays.Clear();
            _marketed.Clear();
            _digest.Clear();
            LastDigest = "";
        }

        /// <summary>Flatten bids and offer sheets into save records (one list, JsonUtility-safe).</summary>
        public List<Data.MarketBidRecord> ToSave()
        {
            var list = new List<Data.MarketBidRecord>();

            foreach (var b in _bids)
                list.Add(new Data.MarketBidRecord
                {
                    PlayerId = b.PlayerId,
                    TeamId = b.TeamId,
                    Years = b.Years,
                    AnnualSalary = b.AnnualAverage,
                    MethodInt = (int)b.Method,
                    DecisionDayStr = _decisionDays.TryGetValue(b.PlayerId, out var d) ? d.ToString("o") : ""
                });

            // Free agents on the clock with no bids yet still need their decision day
            foreach (var kv in _decisionDays.Where(kv => !_bids.Any(b => b.PlayerId == kv.Key)))
                list.Add(new Data.MarketBidRecord
                {
                    PlayerId = kv.Key, TeamId = "", DecisionDayStr = kv.Value.ToString("o")
                });

            foreach (var s in _sheets)
            {
                var o = s.OfferSheets.FirstOrDefault();
                if (o == null) continue;
                list.Add(new Data.MarketBidRecord
                {
                    PlayerId = s.PlayerId,
                    TeamId = o.TeamId,
                    Years = o.Years,
                    AnnualSalary = o.AnnualAverage,
                    MethodInt = (int)o.Method,
                    IsOfferSheet = true,
                    OriginalTeamId = s.OriginalTeamId,
                    MatchDeadlineStr = s.MatchDeadline.ToString("o"),
                    QualifyingOfferAmount = (long)(s.QualifyingOffer?.Amount ?? 0f)
                });
            }

            return list;
        }

        /// <summary>
        /// Restore bids/sheets/decision days, plus the browser's marketed list and
        /// the last wire digest. Absent or null = empty market.
        /// </summary>
        public void Restore(List<Data.MarketBidRecord> records, DateTime today,
            List<string> marketedIds = null, string lastDigest = null)
        {
            Clear();
            _today = today;
            if (marketedIds != null) _marketed.AddRange(marketedIds.Where(id => !string.IsNullOrEmpty(id)));
            if (!string.IsNullOrEmpty(lastDigest)) LastDigest = lastDigest;
            if (records == null) return;

            foreach (var r in records)
            {
                if (r == null || string.IsNullOrEmpty(r.PlayerId)) continue;

                if (ParseDate(r.DecisionDayStr, out var dd))
                    _decisionDays[r.PlayerId] = dd;

                if (string.IsNullOrEmpty(r.TeamId)) continue;

                var offer = new FreeAgentOffer
                {
                    OfferId = $"{r.PlayerId}|{r.TeamId}",
                    PlayerId = r.PlayerId,
                    TeamId = r.TeamId,
                    Years = Math.Max(1, r.Years),
                    AnnualAverage = r.AnnualSalary,
                    TotalValue = r.AnnualSalary * Math.Max(1, r.Years),
                    Method = (SigningMethod)r.MethodInt,
                    Status = FreeAgentOfferStatus.Pending
                };

                if (!r.IsOfferSheet) { _bids.Add(offer); continue; }

                var sheet = new RestrictedFreeAgentStatus
                {
                    PlayerId = r.PlayerId,
                    OriginalTeamId = r.OriginalTeamId,
                    QualifyingOffer = new QualifyingOffer
                    {
                        PlayerId = r.PlayerId,
                        TeamId = r.OriginalTeamId,
                        Amount = r.QualifyingOfferAmount,
                        Extended = true
                    }
                };
                if (ParseDate(r.MatchDeadlineStr, out var md)) sheet.MatchDeadline = md;
                sheet.OfferSheets.Add(offer);
                _sheets.Add(sheet);
            }
        }

        private static bool ParseDate(string s, out DateTime value)
        {
            value = default;
            return !string.IsNullOrEmpty(s) && DateTime.TryParse(s, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out value);
        }
    }
}
