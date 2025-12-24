using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Generates trade announcements and news when trades execute.
    /// Integrates with news ticker on Dashboard and Inbox for full details.
    /// </summary>
    public class TradeAnnouncementSystem
    {
        private PlayerDatabase _playerDatabase;
        private PlayerValueCalculator _valueCalculator;
        private string _playerTeamId;

        // Events
        public event Action<TradeAnnouncement> OnTradeAnnounced;
        public event Action<NewsTickerItem> OnNewsTickerItem;

        // Announcement history
        private List<TradeAnnouncement> _announcementHistory = new List<TradeAnnouncement>();

        public TradeAnnouncementSystem(PlayerDatabase playerDatabase, PlayerValueCalculator valueCalculator)
        {
            _playerDatabase = playerDatabase;
            _valueCalculator = valueCalculator;
        }

        /// <summary>
        /// Set the player's team for priority highlighting.
        /// </summary>
        public void SetPlayerTeamId(string teamId)
        {
            _playerTeamId = teamId;
        }

        /// <summary>
        /// Generate announcement for an executed trade.
        /// </summary>
        public TradeAnnouncement GenerateAnnouncement(TradeProposal executedTrade)
        {
            var announcement = new TradeAnnouncement
            {
                TradeId = Guid.NewGuid().ToString(),
                ExecutedAt = DateTime.Now,
                InvolvedTeams = executedTrade.GetInvolvedTeams().ToList()
            };

            // Check if player team is involved
            announcement.InvolvesPlayerTeam = announcement.InvolvedTeams.Contains(_playerTeamId);

            // Build asset lists per team
            foreach (var teamId in announcement.InvolvedTeams)
            {
                var received = executedTrade.AllAssets
                    .Where(a => a.ReceivingTeamId == teamId)
                    .ToList();
                var sent = executedTrade.AllAssets
                    .Where(a => a.SendingTeamId == teamId)
                    .ToList();

                announcement.AssetsReceivedByTeam[teamId] = received;
                announcement.AssetsSentByTeam[teamId] = sent;
            }

            // Generate content
            announcement.Headline = GenerateHeadline(executedTrade);
            announcement.Summary = GenerateSummary(executedTrade);
            announcement.Analysis = GenerateAnalysis(executedTrade);
            announcement.TeamGrades = GenerateGrades(executedTrade);

            // Add to history
            _announcementHistory.Add(announcement);

            // Fire events
            OnTradeAnnounced?.Invoke(announcement);

            // Create news ticker item
            var tickerItem = new NewsTickerItem
            {
                Id = announcement.TradeId,
                Headline = announcement.Headline,
                IsPriority = announcement.InvolvesPlayerTeam,
                Category = NewsCategory.Trade,
                Timestamp = announcement.ExecutedAt
            };
            OnNewsTickerItem?.Invoke(tickerItem);

            Debug.Log($"[TradeAnnouncement] {announcement.Headline}");

            return announcement;
        }

        /// <summary>
        /// Generate a breaking news headline.
        /// </summary>
        private string GenerateHeadline(TradeProposal trade)
        {
            // Find the most notable player in the trade
            var players = trade.AllAssets
                .Where(a => a.Type == TradeAssetType.Player)
                .ToList();

            if (players.Count == 0)
            {
                // Picks-only trade
                var teams = trade.GetInvolvedTeams().ToList();
                return $"TRADE: {teams[0]} and {teams[1]} swap draft picks";
            }

            // Find highest-salary player (proxy for most notable)
            var topPlayer = players.OrderByDescending(a => a.Salary).First();
            var player = _playerDatabase?.GetPlayer(topPlayer.PlayerId);
            string playerName = player?.FullName ?? topPlayer.PlayerId;
            string newTeam = topPlayer.ReceivingTeamId;

            // Check if multi-player trade
            if (players.Count >= 4)
            {
                return $"BLOCKBUSTER: {playerName} heads to {newTeam} in {players.Count}-player deal";
            }

            return $"TRADE: {playerName} acquired by {newTeam}";
        }

        /// <summary>
        /// Generate trade summary.
        /// </summary>
        private string GenerateSummary(TradeProposal trade)
        {
            var sb = new StringBuilder();
            var teams = trade.GetInvolvedTeams().ToList();

            foreach (var teamId in teams)
            {
                var received = trade.AllAssets.Where(a => a.ReceivingTeamId == teamId).ToList();

                if (received.Count > 0)
                {
                    sb.Append($"{teamId} receives: ");
                    var items = new List<string>();

                    foreach (var asset in received)
                    {
                        items.Add(GetAssetDescription(asset));
                    }

                    sb.AppendLine(string.Join(", ", items));
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Generate trade analysis (who won the trade).
        /// </summary>
        private string GenerateAnalysis(TradeProposal trade)
        {
            var teams = trade.GetInvolvedTeams().ToList();
            var teamValues = new Dictionary<string, float>();

            // Calculate value received by each team
            foreach (var teamId in teams)
            {
                float valueReceived = 0f;
                float valueSent = 0f;

                foreach (var asset in trade.AllAssets)
                {
                    float value = CalculateAssetValue(asset);

                    if (asset.ReceivingTeamId == teamId)
                        valueReceived += value;
                    if (asset.SendingTeamId == teamId)
                        valueSent += value;
                }

                teamValues[teamId] = valueReceived - valueSent;
            }

            // Determine winner
            var sorted = teamValues.OrderByDescending(kvp => kvp.Value).ToList();
            float diff = sorted[0].Value - sorted[1].Value;

            if (Math.Abs(diff) < 5f)
            {
                return "This appears to be a fair trade where both teams addressed needs. Time will tell who got the better end of this deal.";
            }
            else if (diff > 15f)
            {
                return $"{sorted[0].Key} appears to be the clear winner in this trade, acquiring significant value. {sorted[1].Key} may be looking at this as a salary dump or rebuilding move.";
            }
            else
            {
                return $"{sorted[0].Key} gets the slight edge in this trade, but both teams could benefit depending on how the pieces fit their systems.";
            }
        }

        /// <summary>
        /// Generate letter grades for each team.
        /// </summary>
        private Dictionary<string, string> GenerateGrades(TradeProposal trade)
        {
            var grades = new Dictionary<string, string>();
            var teams = trade.GetInvolvedTeams().ToList();

            foreach (var teamId in teams)
            {
                float valueReceived = 0f;
                float valueSent = 0f;

                foreach (var asset in trade.AllAssets)
                {
                    float value = CalculateAssetValue(asset);

                    if (asset.ReceivingTeamId == teamId)
                        valueReceived += value;
                    if (asset.SendingTeamId == teamId)
                        valueSent += value;
                }

                float netValue = valueReceived - valueSent;

                // Convert to letter grade
                string grade = netValue switch
                {
                    >= 20f => "A+",
                    >= 15f => "A",
                    >= 10f => "A-",
                    >= 5f => "B+",
                    >= 0f => "B",
                    >= -5f => "B-",
                    >= -10f => "C+",
                    >= -15f => "C",
                    >= -20f => "C-",
                    >= -25f => "D",
                    _ => "F"
                };

                grades[teamId] = grade;
            }

            return grades;
        }

        /// <summary>
        /// Calculate value of a trade asset.
        /// </summary>
        private float CalculateAssetValue(TradeAsset asset)
        {
            switch (asset.Type)
            {
                case TradeAssetType.Player:
                    var player = _playerDatabase?.GetPlayer(asset.PlayerId);
                    if (player != null && _valueCalculator != null)
                    {
                        var assessment = _valueCalculator.CalculateValue(player, null, null);
                        return assessment.TotalValue;
                    }
                    return asset.Salary / 2_000_000f;

                case TradeAssetType.DraftPick:
                    float baseValue = asset.IsFirstRound ? 25f : 5f;
                    int yearsAway = asset.Year - DateTime.Now.Year;
                    float timeDiscount = 1f - (yearsAway * 0.1f);
                    return baseValue * Math.Max(0.3f, timeDiscount);

                case TradeAssetType.Cash:
                    return asset.CashAmount / 1_000_000f;

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Get human-readable description of an asset.
        /// </summary>
        private string GetAssetDescription(TradeAsset asset)
        {
            switch (asset.Type)
            {
                case TradeAssetType.Player:
                    var player = _playerDatabase?.GetPlayer(asset.PlayerId);
                    return player?.FullName ?? asset.PlayerId;

                case TradeAssetType.DraftPick:
                    string round = asset.IsFirstRound ? "1st" : "2nd";
                    string orig = asset.OriginalTeamId != asset.SendingTeamId
                        ? $" ({asset.OriginalTeamId})"
                        : "";
                    return $"{asset.Year} {round} round pick{orig}";

                case TradeAssetType.Cash:
                    return $"${asset.CashAmount:N0} cash";

                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Get recent trade announcements.
        /// </summary>
        public List<TradeAnnouncement> GetRecentAnnouncements(int maxCount = 20)
        {
            return _announcementHistory
                .OrderByDescending(a => a.ExecutedAt)
                .Take(maxCount)
                .ToList();
        }

        /// <summary>
        /// Get announcements involving the player's team.
        /// </summary>
        public List<TradeAnnouncement> GetPlayerTeamAnnouncements()
        {
            return _announcementHistory
                .Where(a => a.InvolvesPlayerTeam)
                .OrderByDescending(a => a.ExecutedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Complete trade announcement data.
    /// </summary>
    [Serializable]
    public class TradeAnnouncement
    {
        public string TradeId;
        public DateTime ExecutedAt;

        // Content
        public string Headline;
        public string Summary;
        public string Analysis;
        public Dictionary<string, string> TeamGrades = new Dictionary<string, string>();

        // Details
        public List<string> InvolvedTeams = new List<string>();
        public Dictionary<string, List<TradeAsset>> AssetsReceivedByTeam = new Dictionary<string, List<TradeAsset>>();
        public Dictionary<string, List<TradeAsset>> AssetsSentByTeam = new Dictionary<string, List<TradeAsset>>();

        // Priority
        public bool InvolvesPlayerTeam;

        /// <summary>
        /// Get formatted grade display for a team.
        /// </summary>
        public string GetGradeDisplay(string teamId)
        {
            return TeamGrades.TryGetValue(teamId, out var grade)
                ? $"{teamId}: {grade}"
                : $"{teamId}: N/A";
        }
    }

    /// <summary>
    /// Item for news ticker display on Dashboard.
    /// </summary>
    [Serializable]
    public class NewsTickerItem
    {
        public string Id;
        public string Headline;
        public NewsCategory Category;
        public bool IsPriority;
        public DateTime Timestamp;

        /// <summary>
        /// Get display text for ticker.
        /// </summary>
        public string GetTickerText()
        {
            string prefix = IsPriority ? "[YOUR TEAM] " : "";
            return $"{prefix}{Headline}";
        }
    }

    /// <summary>
    /// News category for filtering.
    /// </summary>
    public enum NewsCategory
    {
        Trade,
        FreeAgency,
        Injury,
        Draft,
        GameResult,
        Retirement,
        Contract,
        General
    }
}
