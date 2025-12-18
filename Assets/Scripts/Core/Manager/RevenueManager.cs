using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages dynamic revenue generation based on team performance,
    /// market size, attendance, and other factors.
    /// </summary>
    public class RevenueManager : MonoBehaviour
    {
        public static RevenueManager Instance { get; private set; }

        #region Configuration

        [Header("Base Revenue (millions)")]
        [SerializeField] private float _baseTicketRevenue = 80f;
        [SerializeField] private float _baseTVRevenue = 60f;
        [SerializeField] private float _baseMerchandiseRevenue = 30f;
        [SerializeField] private float _baseSponsorshipRevenue = 40f;

        [Header("Market Size Multipliers")]
        [SerializeField] private float _largeMarketMultiplier = 1.5f;
        [SerializeField] private float _mediumMarketMultiplier = 1.0f;
        [SerializeField] private float _smallMarketMultiplier = 0.7f;

        [Header("Performance Bonuses")]
        [SerializeField] private float _playoffBonus = 1.2f;
        [SerializeField] private float _conferenceFinalBonus = 1.4f;
        [SerializeField] private float _finalsBonus = 1.8f;
        [SerializeField] private float _championshipBonus = 2.2f;

        [Header("Attendance Factors")]
        [SerializeField] private float _winningTeamAttendanceBoost = 0.15f;
        [SerializeField] private float _losingTeamAttendanceDrop = 0.10f;
        [SerializeField] private float _starPlayerAttendanceBoost = 0.10f;

        #endregion

        #region State

        private Dictionary<string, SeasonRevenueData> _teamRevenue = new Dictionary<string, SeasonRevenueData>();
        private Dictionary<string, List<RevenueEvent>> _revenueHistory = new Dictionary<string, List<RevenueEvent>>();

        #endregion

        #region Events

        public event Action<string, RevenueEvent> OnRevenueEvent;
        public event Action<string, SeasonRevenueProjection> OnProjectionUpdated;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            GameManager.Instance?.RegisterRevenueManager(this);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize revenue tracking for a new season.
        /// </summary>
        public void InitializeForSeason(int season, List<Team> teams)
        {
            _teamRevenue.Clear();

            foreach (var team in teams)
            {
                var data = new SeasonRevenueData
                {
                    TeamId = team.TeamId,
                    Season = season,
                    MarketSize = team.MarketSize,
                    BaseProjection = CalculateBaseProjection(team)
                };

                _teamRevenue[team.TeamId] = data;
            }
        }

        private SeasonRevenueProjection CalculateBaseProjection(Team team)
        {
            float marketMult = GetMarketMultiplier(team.MarketSize);

            return new SeasonRevenueProjection
            {
                TicketRevenue = _baseTicketRevenue * marketMult,
                TVRevenue = _baseTVRevenue * marketMult,
                MerchandiseRevenue = _baseMerchandiseRevenue * marketMult,
                SponsorshipRevenue = _baseSponsorshipRevenue * marketMult
            };
        }

        private float GetMarketMultiplier(MarketSize size)
        {
            return size switch
            {
                MarketSize.Large => _largeMarketMultiplier,
                MarketSize.Medium => _mediumMarketMultiplier,
                MarketSize.Small => _smallMarketMultiplier,
                _ => _mediumMarketMultiplier
            };
        }

        #endregion

        #region Game Revenue

        /// <summary>
        /// Calculate revenue from a single home game.
        /// </summary>
        public GameRevenueBreakdown CalculateGameRevenue(Team homeTeam, Team awayTeam, int attendance, bool isPlayoff)
        {
            if (!_teamRevenue.TryGetValue(homeTeam.TeamId, out var data))
                return new GameRevenueBreakdown();

            var breakdown = new GameRevenueBreakdown();

            // Base ticket revenue per game (season / 41 home games)
            float baseGameTicket = (data.BaseProjection.TicketRevenue / 41f);

            // Attendance modifier
            float attendanceRate = (float)attendance / homeTeam.ArenaCapacity;
            breakdown.TicketRevenue = baseGameTicket * attendanceRate;

            // Premium seating
            float premiumRate = homeTeam.PremiumSeats > 0
                ? (float)homeTeam.PremiumSeats / homeTeam.ArenaCapacity
                : 0.1f;
            breakdown.PremiumRevenue = breakdown.TicketRevenue * premiumRate * 2.5f;

            // Concessions (average $30 per attendee)
            breakdown.ConcessionRevenue = attendance * 0.000030f; // In millions

            // Merchandise (higher for big games)
            float merchBase = data.BaseProjection.MerchandiseRevenue / 82f;
            float merchMult = 1.0f;
            if (IsRivalryGame(homeTeam, awayTeam)) merchMult += 0.3f;
            if (isPlayoff) merchMult += 0.5f;
            if (HasSuperstar(awayTeam)) merchMult += 0.2f;
            breakdown.MerchandiseRevenue = merchBase * merchMult;

            // Parking
            breakdown.ParkingRevenue = attendance * 0.000025f; // In millions

            // Playoff bonus
            if (isPlayoff)
            {
                breakdown.PlayoffBonus = breakdown.Total * 0.3f;
            }

            // Record the revenue
            data.GamesPlayed++;
            data.ActualTicketRevenue += breakdown.TicketRevenue + breakdown.PremiumRevenue;
            data.ActualMerchandiseRevenue += breakdown.MerchandiseRevenue;
            data.OtherRevenue += breakdown.ConcessionRevenue + breakdown.ParkingRevenue;

            // Fire event
            var evt = new RevenueEvent
            {
                Date = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                EventType = RevenueEventType.HomeGame,
                Amount = breakdown.Total,
                Description = $"Home game vs {awayTeam.Abbreviation}"
            };
            OnRevenueEvent?.Invoke(homeTeam.TeamId, evt);

            return breakdown;
        }

        private bool IsRivalryGame(Team home, Team away)
        {
            // Simple rivalry check based on division
            return home.Division == away.Division;
        }

        private bool HasSuperstar(Team team)
        {
            return team.Roster?.Any(p => p.GetOverallRating() >= 90) ?? false;
        }

        #endregion

        #region Seasonal Revenue

        /// <summary>
        /// Process end of season revenue adjustments.
        /// </summary>
        public SeasonRevenueReport ProcessEndOfSeason(string teamId, PlayoffResult playoffResult)
        {
            if (!_teamRevenue.TryGetValue(teamId, out var data))
                return null;

            var report = new SeasonRevenueReport
            {
                TeamId = teamId,
                Season = data.Season
            };

            // Calculate final revenue
            report.TicketRevenue = data.ActualTicketRevenue;
            report.TVRevenue = CalculateTVRevenue(data, playoffResult);
            report.MerchandiseRevenue = data.ActualMerchandiseRevenue + CalculateMerchBonus(data, playoffResult);
            report.SponsorshipRevenue = CalculateSponsorshipRevenue(data, playoffResult);
            report.PlayoffRevenue = CalculatePlayoffRevenue(data, playoffResult);
            report.OtherRevenue = data.OtherRevenue;

            // Revenue sharing
            report.RevenueSharingPaid = CalculateRevenueSharingContribution(report.TotalRevenue);
            report.RevenueSharingReceived = CalculateRevenueSharingReceived(teamId, report.TotalRevenue);

            report.NetRevenue = report.TotalRevenue - report.RevenueSharingPaid + report.RevenueSharingReceived;

            return report;
        }

        private float CalculateTVRevenue(SeasonRevenueData data, PlayoffResult result)
        {
            float baseTv = data.BaseProjection.TVRevenue;

            // National TV appearances bonus
            float nationalBonus = data.NationalTVGames * 0.5f;

            // Playoff TV money
            float playoffTv = 0;
            switch (result)
            {
                case PlayoffResult.FirstRound:
                    playoffTv = baseTv * 0.1f;
                    break;
                case PlayoffResult.SecondRound:
                    playoffTv = baseTv * 0.2f;
                    break;
                case PlayoffResult.ConferenceFinals:
                    playoffTv = baseTv * 0.35f;
                    break;
                case PlayoffResult.Finals:
                    playoffTv = baseTv * 0.5f;
                    break;
                case PlayoffResult.Champion:
                    playoffTv = baseTv * 0.7f;
                    break;
            }

            return baseTv + nationalBonus + playoffTv;
        }

        private float CalculateMerchBonus(SeasonRevenueData data, PlayoffResult result)
        {
            float bonus = 0;

            switch (result)
            {
                case PlayoffResult.Champion:
                    bonus = data.BaseProjection.MerchandiseRevenue * 0.8f;
                    break;
                case PlayoffResult.Finals:
                    bonus = data.BaseProjection.MerchandiseRevenue * 0.4f;
                    break;
                case PlayoffResult.ConferenceFinals:
                    bonus = data.BaseProjection.MerchandiseRevenue * 0.2f;
                    break;
            }

            return bonus;
        }

        private float CalculateSponsorshipRevenue(SeasonRevenueData data, PlayoffResult result)
        {
            float baseSponsor = data.BaseProjection.SponsorshipRevenue;
            float multiplier = 1.0f;

            // Performance bonus
            if (data.WinPercentage >= 0.6f) multiplier += 0.15f;
            if (data.WinPercentage >= 0.7f) multiplier += 0.15f;

            // Playoff bonus
            if (result >= PlayoffResult.SecondRound) multiplier += 0.1f;
            if (result >= PlayoffResult.ConferenceFinals) multiplier += 0.15f;
            if (result >= PlayoffResult.Champion) multiplier += 0.25f;

            return baseSponsor * multiplier;
        }

        private float CalculatePlayoffRevenue(SeasonRevenueData data, PlayoffResult result)
        {
            // Home playoff games generate significant revenue
            float basePlayoffGame = (data.BaseProjection.TicketRevenue / 41f) * 1.5f;
            int homeGames = result switch
            {
                PlayoffResult.MissedPlayoffs => 0,
                PlayoffResult.PlayIn => 1,
                PlayoffResult.FirstRound => 3,
                PlayoffResult.SecondRound => 5,
                PlayoffResult.ConferenceFinals => 7,
                PlayoffResult.Finals => 10,
                PlayoffResult.Champion => 12,
                _ => 0
            };

            return basePlayoffGame * homeGames;
        }

        private float CalculateRevenueSharingContribution(float totalRevenue)
        {
            // Teams contribute to revenue sharing pool
            float sharingThreshold = 200f; // Teams above this contribute
            if (totalRevenue > sharingThreshold)
            {
                return (totalRevenue - sharingThreshold) * 0.30f; // 30% of excess
            }
            return 0;
        }

        private float CalculateRevenueSharingReceived(string teamId, float totalRevenue)
        {
            // Teams below threshold receive from pool
            float sharingThreshold = 150f;
            if (totalRevenue < sharingThreshold)
            {
                return (sharingThreshold - totalRevenue) * 0.25f; // Partial offset
            }
            return 0;
        }

        #endregion

        #region Projections

        /// <summary>
        /// Update revenue projection based on current performance.
        /// </summary>
        public SeasonRevenueProjection UpdateProjection(string teamId, int wins, int losses, bool inPlayoffs)
        {
            if (!_teamRevenue.TryGetValue(teamId, out var data))
                return null;

            data.Wins = wins;
            data.Losses = losses;
            data.WinPercentage = (wins + losses) > 0 ? (float)wins / (wins + losses) : 0;
            data.InPlayoffs = inPlayoffs;

            var projection = new SeasonRevenueProjection
            {
                TicketRevenue = ProjectTicketRevenue(data),
                TVRevenue = ProjectTVRevenue(data),
                MerchandiseRevenue = ProjectMerchandiseRevenue(data),
                SponsorshipRevenue = ProjectSponsorshipRevenue(data)
            };

            OnProjectionUpdated?.Invoke(teamId, projection);
            return projection;
        }

        private float ProjectTicketRevenue(SeasonRevenueData data)
        {
            float base_ = data.BaseProjection.TicketRevenue;
            float mult = 1.0f;

            // Winning teams sell more tickets
            if (data.WinPercentage >= 0.6f) mult += _winningTeamAttendanceBoost;
            else if (data.WinPercentage < 0.4f) mult -= _losingTeamAttendanceDrop;

            // Playoff potential
            if (data.InPlayoffs) mult *= _playoffBonus;

            // Extrapolate from actual so far
            if (data.GamesPlayed > 0)
            {
                float actualPerGame = data.ActualTicketRevenue / data.GamesPlayed;
                int remainingGames = 41 - data.GamesPlayed;
                return data.ActualTicketRevenue + (actualPerGame * remainingGames * mult);
            }

            return base_ * mult;
        }

        private float ProjectTVRevenue(SeasonRevenueData data)
        {
            return data.BaseProjection.TVRevenue * (data.InPlayoffs ? 1.2f : 1.0f);
        }

        private float ProjectMerchandiseRevenue(SeasonRevenueData data)
        {
            float base_ = data.BaseProjection.MerchandiseRevenue;
            if (data.WinPercentage >= 0.65f) return base_ * 1.3f;
            if (data.WinPercentage < 0.35f) return base_ * 0.8f;
            return base_;
        }

        private float ProjectSponsorshipRevenue(SeasonRevenueData data)
        {
            return data.BaseProjection.SponsorshipRevenue;
        }

        #endregion

        #region Queries

        /// <summary>
        /// Gets the current season revenue data for a team.
        /// </summary>
        public SeasonRevenueData GetSeasonData(string teamId)
        {
            return _teamRevenue.GetValueOrDefault(teamId);
        }

        /// <summary>
        /// Gets total revenue to date.
        /// </summary>
        public float GetRevenueToDate(string teamId)
        {
            if (!_teamRevenue.TryGetValue(teamId, out var data))
                return 0;

            return data.ActualTicketRevenue +
                   data.ActualMerchandiseRevenue +
                   data.OtherRevenue +
                   data.BaseProjection.TVRevenue * ((float)data.GamesPlayed / 82f) +
                   data.BaseProjection.SponsorshipRevenue * ((float)data.GamesPlayed / 82f);
        }

        #endregion
    }

    #region Supporting Types

    [Serializable]
    public class SeasonRevenueData
    {
        public string TeamId;
        public int Season;
        public MarketSize MarketSize;
        public SeasonRevenueProjection BaseProjection;

        // Performance tracking
        public int GamesPlayed;
        public int Wins;
        public int Losses;
        public float WinPercentage;
        public bool InPlayoffs;
        public int NationalTVGames;

        // Actual revenue collected
        public float ActualTicketRevenue;
        public float ActualMerchandiseRevenue;
        public float OtherRevenue;
    }

    [Serializable]
    public class SeasonRevenueProjection
    {
        public float TicketRevenue;
        public float TVRevenue;
        public float MerchandiseRevenue;
        public float SponsorshipRevenue;

        public float Total => TicketRevenue + TVRevenue + MerchandiseRevenue + SponsorshipRevenue;
    }

    [Serializable]
    public class GameRevenueBreakdown
    {
        public float TicketRevenue;
        public float PremiumRevenue;
        public float ConcessionRevenue;
        public float MerchandiseRevenue;
        public float ParkingRevenue;
        public float PlayoffBonus;

        public float Total => TicketRevenue + PremiumRevenue + ConcessionRevenue +
                             MerchandiseRevenue + ParkingRevenue + PlayoffBonus;
    }

    [Serializable]
    public class SeasonRevenueReport
    {
        public string TeamId;
        public int Season;
        public float TicketRevenue;
        public float TVRevenue;
        public float MerchandiseRevenue;
        public float SponsorshipRevenue;
        public float PlayoffRevenue;
        public float OtherRevenue;
        public float RevenueSharingPaid;
        public float RevenueSharingReceived;
        public float NetRevenue;

        public float TotalRevenue => TicketRevenue + TVRevenue + MerchandiseRevenue +
                                    SponsorshipRevenue + PlayoffRevenue + OtherRevenue;
    }

    [Serializable]
    public class RevenueEvent
    {
        public DateTime Date;
        public RevenueEventType EventType;
        public float Amount;
        public string Description;
    }

    public enum RevenueEventType
    {
        HomeGame,
        PlayoffGame,
        Sponsorship,
        MerchandiseSale,
        TVDeal,
        Other
    }

    public enum PlayoffResult
    {
        MissedPlayoffs,
        PlayIn,
        FirstRound,
        SecondRound,
        ConferenceFinals,
        Finals,
        Champion
    }

    public enum MarketSize
    {
        Small,
        Medium,
        Large
    }

    #endregion
}
