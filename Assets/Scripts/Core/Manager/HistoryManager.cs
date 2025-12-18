using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages historical records, season archives, franchise records, and Hall of Fame.
    /// Tracks all-time leaders, single-game/season records, and career milestones.
    /// </summary>
    public class HistoryManager : MonoBehaviour
    {
        public static HistoryManager Instance { get; private set; }

        #region State

        [Header("Season Archives")]
        [SerializeField] private List<SeasonArchive> _seasonArchives = new List<SeasonArchive>();

        [Header("Franchise Records")]
        [SerializeField] private Dictionary<string, FranchiseRecords> _franchiseRecords = new Dictionary<string, FranchiseRecords>();

        [Header("League Records")]
        [SerializeField] private LeagueRecords _leagueRecords = new LeagueRecords();

        [Header("Hall of Fame")]
        [SerializeField] private List<HallOfFameInductee> _hallOfFame = new List<HallOfFameInductee>();
        [SerializeField] private List<string> _hallOfFameEligible = new List<string>(); // Player IDs waiting for eligibility

        #endregion

        #region Events

        public event Action<SeasonArchive> OnSeasonArchived;
        public event Action<RecordBroken> OnRecordBroken;
        public event Action<HallOfFameInductee> OnHallOfFameInduction;
        public event Action<CareerMilestone> OnMilestoneReached;

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
            GameManager.Instance?.RegisterHistoryManager(this);
        }

        #endregion

        #region Season Archiving

        /// <summary>
        /// Archive a completed season with all relevant data
        /// </summary>
        public SeasonArchive ArchiveSeason(int year, List<Team> teams, List<Player> players, AwardVotingResults awards,
            string championTeamId, string finalsMvpId, PlayoffBracket playoffBracket)
        {
            var archive = new SeasonArchive
            {
                Year = year,
                ChampionTeamId = championTeamId,
                FinalsMVPId = finalsMvpId,
                MVPId = awards?.MVP?.PlayerId,
                DPOYId = awards?.DPOY?.PlayerId,
                ROTYId = awards?.ROTY?.PlayerId,
                SixthManId = awards?.SixthMan?.PlayerId,
                MIPId = awards?.MIP?.PlayerId
            };

            // Archive team standings
            foreach (var team in teams.OrderByDescending(t => t.WinPercentage))
            {
                archive.TeamStandings.Add(new ArchivedTeamRecord
                {
                    TeamId = team.TeamId,
                    Wins = team.Wins,
                    Losses = team.Losses,
                    ConferenceRank = GetConferenceRank(team, teams),
                    MadePlayoffs = GetConferenceRank(team, teams) <= 10
                });
            }

            // Archive statistical leaders
            var statLeaders = CalculateStatLeaders(players);
            archive.ScoringLeaderId = statLeaders.GetValueOrDefault("PPG");
            archive.ReboundingLeaderId = statLeaders.GetValueOrDefault("RPG");
            archive.AssistsLeaderId = statLeaders.GetValueOrDefault("APG");
            archive.StealsLeaderId = statLeaders.GetValueOrDefault("SPG");
            archive.BlocksLeaderId = statLeaders.GetValueOrDefault("BPG");

            // Archive All-NBA teams
            archive.AllNBAFirstTeam = awards?.AllNBAFirst?.Select(p => p.PlayerId).ToList() ?? new List<string>();
            archive.AllNBASecondTeam = awards?.AllNBASecond?.Select(p => p.PlayerId).ToList() ?? new List<string>();
            archive.AllNBAThirdTeam = awards?.AllNBAThird?.Select(p => p.PlayerId).ToList() ?? new List<string>();
            archive.AllDefenseFirstTeam = awards?.AllDefenseFirst?.Select(p => p.PlayerId).ToList() ?? new List<string>();
            archive.AllDefenseSecondTeam = awards?.AllDefenseSecond?.Select(p => p.PlayerId).ToList() ?? new List<string>();

            _seasonArchives.Add(archive);
            OnSeasonArchived?.Invoke(archive);

            Debug.Log($"[HistoryManager] Archived season {year}. Champion: {championTeamId}");

            // Check for records broken during season
            CheckSeasonRecords(year, teams, players);

            return archive;
        }

        private int GetConferenceRank(Team team, List<Team> allTeams)
        {
            var conferenceTeams = allTeams.Where(t => t.Conference == team.Conference)
                                          .OrderByDescending(t => t.WinPercentage)
                                          .ToList();
            return conferenceTeams.FindIndex(t => t.TeamId == team.TeamId) + 1;
        }

        private Dictionary<string, string> CalculateStatLeaders(List<Player> players)
        {
            var leaders = new Dictionary<string, string>();
            var eligible = players.Where(p => p.CurrentSeasonStats?.GamesPlayed >= 58).ToList();

            if (eligible.Count == 0) return leaders;

            leaders["PPG"] = eligible.OrderByDescending(p => p.CurrentSeasonStats.PPG).First().PlayerId;
            leaders["RPG"] = eligible.OrderByDescending(p => p.CurrentSeasonStats.RPG).First().PlayerId;
            leaders["APG"] = eligible.OrderByDescending(p => p.CurrentSeasonStats.APG).First().PlayerId;
            leaders["SPG"] = eligible.OrderByDescending(p => p.CurrentSeasonStats.SPG).First().PlayerId;
            leaders["BPG"] = eligible.OrderByDescending(p => p.CurrentSeasonStats.BPG).First().PlayerId;

            return leaders;
        }

        /// <summary>
        /// Get archive for a specific season
        /// </summary>
        public SeasonArchive GetSeasonArchive(int year)
        {
            return _seasonArchives.FirstOrDefault(a => a.Year == year);
        }

        /// <summary>
        /// Get all season archives
        /// </summary>
        public List<SeasonArchive> GetAllArchives()
        {
            return _seasonArchives.OrderByDescending(a => a.Year).ToList();
        }

        #endregion

        #region Records Tracking

        /// <summary>
        /// Check and update records after a game
        /// </summary>
        public void CheckGameRecords(int year, string teamId, BoxScore boxScore)
        {
            // Initialize franchise records if needed
            if (!_franchiseRecords.ContainsKey(teamId))
            {
                _franchiseRecords[teamId] = new FranchiseRecords { TeamId = teamId };
            }

            var franchise = _franchiseRecords[teamId];

            foreach (var playerStats in boxScore.PlayerStats)
            {
                var playerId = playerStats.PlayerId;

                // Check single-game records
                CheckSingleGameRecord(ref franchise.SingleGamePoints, "Points", year, playerId, teamId, playerStats.Points);
                CheckSingleGameRecord(ref franchise.SingleGameRebounds, "Rebounds", year, playerId, teamId, playerStats.Rebounds);
                CheckSingleGameRecord(ref franchise.SingleGameAssists, "Assists", year, playerId, teamId, playerStats.Assists);
                CheckSingleGameRecord(ref franchise.SingleGameSteals, "Steals", year, playerId, teamId, playerStats.Steals);
                CheckSingleGameRecord(ref franchise.SingleGameBlocks, "Blocks", year, playerId, teamId, playerStats.Blocks);
                CheckSingleGameRecord(ref franchise.SingleGameThrees, "3PM", year, playerId, teamId, playerStats.ThreePM);

                // Check league records too
                CheckSingleGameRecord(ref _leagueRecords.SingleGamePoints, "Points (League)", year, playerId, teamId, playerStats.Points);
                CheckSingleGameRecord(ref _leagueRecords.SingleGameRebounds, "Rebounds (League)", year, playerId, teamId, playerStats.Rebounds);
                CheckSingleGameRecord(ref _leagueRecords.SingleGameAssists, "Assists (League)", year, playerId, teamId, playerStats.Assists);

                // Notable game achievements
                if (playerStats.Points >= 50)
                {
                    Debug.Log($"[HistoryManager] 50-point game: {playerId} with {playerStats.Points} points!");
                }

                // Triple-double
                if (playerStats.Points >= 10 && playerStats.Rebounds >= 10 && playerStats.Assists >= 10)
                {
                    Debug.Log($"[HistoryManager] Triple-double: {playerId}!");
                }
            }

            // Team single-game records
            int teamPoints = boxScore.IsHome ? boxScore.HomeScore : boxScore.AwayScore;
            CheckSingleGameRecord(ref franchise.TeamHighestScore, "Team Points", year, null, teamId, teamPoints);
        }

        private void CheckSingleGameRecord(ref SingleGameRecord current, string recordName, int year,
            string playerId, string teamId, int value)
        {
            if (current == null || value > current.Value)
            {
                var oldRecord = current;
                current = new SingleGameRecord
                {
                    PlayerId = playerId,
                    TeamId = teamId,
                    Year = year,
                    Value = value
                };

                if (oldRecord != null)
                {
                    OnRecordBroken?.Invoke(new RecordBroken
                    {
                        RecordType = RecordType.SingleGame,
                        RecordName = recordName,
                        NewValue = value,
                        OldValue = oldRecord.Value,
                        PlayerId = playerId,
                        Year = year
                    });
                }
            }
        }

        /// <summary>
        /// Check season records at end of year
        /// </summary>
        private void CheckSeasonRecords(int year, List<Team> teams, List<Player> players)
        {
            // Best team record
            var bestTeam = teams.OrderByDescending(t => t.Wins).First();
            if (_leagueRecords.BestTeamRecord == null || bestTeam.Wins > _leagueRecords.BestTeamRecord.Wins)
            {
                _leagueRecords.BestTeamRecord = new SeasonTeamRecord
                {
                    TeamId = bestTeam.TeamId,
                    Year = year,
                    Wins = bestTeam.Wins,
                    Losses = bestTeam.Losses
                };
                Debug.Log($"[HistoryManager] New best team record: {bestTeam.Wins}-{bestTeam.Losses}");
            }

            // Player season records
            foreach (var p in players.Where(x => x.CurrentSeasonStats?.GamesPlayed >= 58))
            {
                var stats = p.CurrentSeasonStats;

                CheckSeasonRecord(ref _leagueRecords.SeasonPPG, "PPG", year, p.PlayerId, stats.PPG);
                CheckSeasonRecord(ref _leagueRecords.SeasonRPG, "RPG", year, p.PlayerId, stats.RPG);
                CheckSeasonRecord(ref _leagueRecords.SeasonAPG, "APG", year, p.PlayerId, stats.APG);
            }
        }

        private void CheckSeasonRecord(ref SeasonStatRecord current, string recordName, int year,
            string playerId, float value)
        {
            if (current == null || value > current.Value)
            {
                current = new SeasonStatRecord
                {
                    PlayerId = playerId,
                    Year = year,
                    Value = value
                };
            }
        }

        /// <summary>
        /// Get franchise records for a team
        /// </summary>
        public FranchiseRecords GetFranchiseRecords(string teamId)
        {
            return _franchiseRecords.GetValueOrDefault(teamId);
        }

        /// <summary>
        /// Get league-wide records
        /// </summary>
        public LeagueRecords GetLeagueRecords()
        {
            return _leagueRecords;
        }

        #endregion

        #region Career Milestones

        /// <summary>
        /// Check if player reached career milestones after a game
        /// </summary>
        public void CheckCareerMilestones(Player player, PlayerGameStats gameStats)
        {
            // Points milestones: 10k, 15k, 20k, 25k, 30k, 35k, 40k
            int[] pointMilestones = { 10000, 15000, 20000, 25000, 30000, 35000, 40000 };
            CheckMilestone(player, "Career Points", player.CareerPoints, gameStats.Points, pointMilestones);

            // Assists milestones: 5k, 7.5k, 10k, 12k, 15k
            int[] assistMilestones = { 5000, 7500, 10000, 12000, 15000 };
            CheckMilestone(player, "Career Assists", player.CareerAssists, gameStats.Assists, assistMilestones);

            // Rebounds milestones: 5k, 7.5k, 10k, 12.5k, 15k
            int[] reboundMilestones = { 5000, 7500, 10000, 12500, 15000 };
            CheckMilestone(player, "Career Rebounds", player.CareerRebounds, gameStats.Rebounds, reboundMilestones);

            // Steals milestones: 1k, 1.5k, 2k, 2.5k, 3k
            int[] stealMilestones = { 1000, 1500, 2000, 2500, 3000 };
            CheckMilestone(player, "Career Steals", player.CareerSteals, gameStats.Steals, stealMilestones);

            // Blocks milestones: 1k, 1.5k, 2k, 2.5k, 3k
            int[] blockMilestones = { 1000, 1500, 2000, 2500, 3000 };
            CheckMilestone(player, "Career Blocks", player.CareerBlocks, gameStats.Blocks, blockMilestones);
        }

        private void CheckMilestone(Player player, string statName, int careerTotal, int gameValue, int[] milestones)
        {
            int previousTotal = careerTotal - gameValue;

            foreach (int milestone in milestones)
            {
                if (previousTotal < milestone && careerTotal >= milestone)
                {
                    var achieved = new CareerMilestone
                    {
                        PlayerId = player.PlayerId,
                        PlayerName = player.FullName,
                        StatName = statName,
                        MilestoneValue = milestone,
                        ActualValue = careerTotal
                    };

                    OnMilestoneReached?.Invoke(achieved);
                    Debug.Log($"[HistoryManager] MILESTONE: {player.FullName} reaches {milestone:N0} {statName}!");
                    break; // Only report one milestone per game per stat
                }
            }
        }

        #endregion

        #region Hall of Fame

        /// <summary>
        /// Mark a retired player as eligible for Hall of Fame (5 years after retirement)
        /// </summary>
        public void AddHallOfFameEligible(string playerId, int retirementYear)
        {
            if (!_hallOfFameEligible.Contains(playerId))
            {
                _hallOfFameEligible.Add(playerId);
                Debug.Log($"[HistoryManager] {playerId} becomes Hall of Fame eligible in {retirementYear + 5}");
            }
        }

        /// <summary>
        /// Simulate Hall of Fame voting for eligible players
        /// </summary>
        public List<HallOfFameInductee> VoteHallOfFame(int currentYear, List<Player> allPlayers, List<Player> retiredPlayers)
        {
            var newInductees = new List<HallOfFameInductee>();

            foreach (var playerId in _hallOfFameEligible.ToList())
            {
                var player = retiredPlayers.FirstOrDefault(p => p.PlayerId == playerId);
                if (player == null) continue;

                // Check if 5 years since retirement
                int retirementYear = player.RetirementYear;
                if (currentYear < retirementYear + 5) continue;

                // Calculate Hall of Fame score
                float hofScore = CalculateHOFScore(player);

                // 75+ score = first ballot, 60-74 = probable, 50-59 = borderline
                bool inducted = hofScore >= 60 || (hofScore >= 50 && UnityEngine.Random.value > 0.5f);

                if (inducted)
                {
                    var inductee = new HallOfFameInductee
                    {
                        PlayerId = playerId,
                        PlayerName = player.FullName,
                        InductionYear = currentYear,
                        HofScore = hofScore,
                        IsFirstBallot = (currentYear == retirementYear + 5 && hofScore >= 75),
                        CareerPoints = player.CareerPoints,
                        Championships = player.Awards.Count(a => a.Type == AwardType.NBAChampion),
                        MVPs = player.Awards.Count(a => a.Type == AwardType.MVP),
                        AllStarSelections = player.Awards.Count(a => a.Type == AwardType.AllStar),
                        AllNBASelections = player.Awards.Count(a =>
                            a.Type == AwardType.AllNBAFirstTeam ||
                            a.Type == AwardType.AllNBASecondTeam ||
                            a.Type == AwardType.AllNBAThirdTeam)
                    };

                    _hallOfFame.Add(inductee);
                    _hallOfFameEligible.Remove(playerId);
                    newInductees.Add(inductee);

                    OnHallOfFameInduction?.Invoke(inductee);
                    Debug.Log($"[HistoryManager] HOF INDUCTION: {player.FullName} ({(inductee.IsFirstBallot ? "First Ballot" : "")})");
                }
            }

            return newInductees;
        }

        private float CalculateHOFScore(Player player)
        {
            float score = 0;

            // Career stats
            score += player.CareerPoints / 1000f; // ~20-30 points for great scorers
            score += player.CareerAssists / 500f;
            score += player.CareerRebounds / 500f;

            // Awards
            score += player.Awards.Count(a => a.Type == AwardType.MVP) * 15;
            score += player.Awards.Count(a => a.Type == AwardType.FinalsMVP) * 10;
            score += player.Awards.Count(a => a.Type == AwardType.NBAChampion) * 8;
            score += player.Awards.Count(a => a.Type == AwardType.AllStar) * 2;
            score += player.Awards.Count(a => a.Type == AwardType.AllNBAFirstTeam) * 4;
            score += player.Awards.Count(a => a.Type == AwardType.AllNBASecondTeam) * 2;
            score += player.Awards.Count(a => a.Type == AwardType.AllNBAThirdTeam) * 1;
            score += player.Awards.Count(a => a.Type == AwardType.DefensivePlayerOfYear) * 10;
            score += player.Awards.Count(a => a.Type == AwardType.AllDefensiveFirstTeam) * 3;

            // Longevity bonus
            score += player.YearsPro * 0.5f;

            return score;
        }

        /// <summary>
        /// Get all Hall of Fame inductees
        /// </summary>
        public List<HallOfFameInductee> GetHallOfFame()
        {
            return _hallOfFame.OrderByDescending(h => h.InductionYear).ToList();
        }

        /// <summary>
        /// Check if player is in Hall of Fame
        /// </summary>
        public bool IsInHallOfFame(string playerId)
        {
            return _hallOfFame.Any(h => h.PlayerId == playerId);
        }

        #endregion

        #region All-Time Leaders

        /// <summary>
        /// Get all-time leaders for a stat category
        /// </summary>
        public List<(Player player, int value)> GetAllTimeLeaders(List<Player> allPlayers, StatCategory category, int count = 10)
        {
            return category switch
            {
                StatCategory.Points => allPlayers.OrderByDescending(p => p.CareerPoints)
                                                 .Take(count)
                                                 .Select(p => (p, p.CareerPoints))
                                                 .ToList(),

                StatCategory.Rebounds => allPlayers.OrderByDescending(p => p.CareerRebounds)
                                                   .Take(count)
                                                   .Select(p => (p, p.CareerRebounds))
                                                   .ToList(),

                StatCategory.Assists => allPlayers.OrderByDescending(p => p.CareerAssists)
                                                  .Take(count)
                                                  .Select(p => (p, p.CareerAssists))
                                                  .ToList(),

                StatCategory.Steals => allPlayers.OrderByDescending(p => p.CareerSteals)
                                                 .Take(count)
                                                 .Select(p => (p, p.CareerSteals))
                                                 .ToList(),

                StatCategory.Blocks => allPlayers.OrderByDescending(p => p.CareerBlocks)
                                                 .Take(count)
                                                 .Select(p => (p, p.CareerBlocks))
                                                 .ToList(),

                StatCategory.GamesPlayed => allPlayers.OrderByDescending(p => p.CareerGamesPlayed)
                                                      .Take(count)
                                                      .Select(p => (p, p.CareerGamesPlayed))
                                                      .ToList(),

                _ => new List<(Player, int)>()
            };
        }

        #endregion

        #region Save/Load

        public HistorySaveData CreateSaveData()
        {
            return new HistorySaveData
            {
                SeasonArchives = _seasonArchives,
                FranchiseRecords = _franchiseRecords,
                LeagueRecords = _leagueRecords,
                HallOfFame = _hallOfFame,
                HallOfFameEligible = _hallOfFameEligible
            };
        }

        public void RestoreFromSave(HistorySaveData data)
        {
            if (data == null) return;

            _seasonArchives = data.SeasonArchives ?? new List<SeasonArchive>();
            _franchiseRecords = data.FranchiseRecords ?? new Dictionary<string, FranchiseRecords>();
            _leagueRecords = data.LeagueRecords ?? new LeagueRecords();
            _hallOfFame = data.HallOfFame ?? new List<HallOfFameInductee>();
            _hallOfFameEligible = data.HallOfFameEligible ?? new List<string>();

            Debug.Log($"[HistoryManager] Restored {_seasonArchives.Count} archived seasons, {_hallOfFame.Count} HOF inductees");
        }

        #endregion
    }

    #region Data Classes

    [Serializable]
    public class SeasonArchive
    {
        public int Year;
        public string ChampionTeamId;
        public string FinalsMVPId;
        public string MVPId;
        public string DPOYId;
        public string ROTYId;
        public string SixthManId;
        public string MIPId;
        public string COYTeamId;

        public string ScoringLeaderId;
        public string ReboundingLeaderId;
        public string AssistsLeaderId;
        public string StealsLeaderId;
        public string BlocksLeaderId;

        public List<ArchivedTeamRecord> TeamStandings = new List<ArchivedTeamRecord>();
        public List<string> AllNBAFirstTeam = new List<string>();
        public List<string> AllNBASecondTeam = new List<string>();
        public List<string> AllNBAThirdTeam = new List<string>();
        public List<string> AllDefenseFirstTeam = new List<string>();
        public List<string> AllDefenseSecondTeam = new List<string>();
    }

    [Serializable]
    public class ArchivedTeamRecord
    {
        public string TeamId;
        public int Wins;
        public int Losses;
        public int ConferenceRank;
        public bool MadePlayoffs;
    }

    [Serializable]
    public class FranchiseRecords
    {
        public string TeamId;

        // Single game records
        public SingleGameRecord SingleGamePoints;
        public SingleGameRecord SingleGameRebounds;
        public SingleGameRecord SingleGameAssists;
        public SingleGameRecord SingleGameSteals;
        public SingleGameRecord SingleGameBlocks;
        public SingleGameRecord SingleGameThrees;
        public SingleGameRecord TeamHighestScore;
        public SingleGameRecord TeamLowestScore;

        // Season records
        public SeasonTeamRecord BestSeasonRecord;
        public SeasonTeamRecord WorstSeasonRecord;

        // Franchise leaders
        public string AllTimePointsLeaderId;
        public string AllTimeReboundsLeaderId;
        public string AllTimeAssistsLeaderId;
        public string AllTimeGamesPlayedLeaderId;
    }

    [Serializable]
    public class LeagueRecords
    {
        public SingleGameRecord SingleGamePoints;
        public SingleGameRecord SingleGameRebounds;
        public SingleGameRecord SingleGameAssists;

        public SeasonStatRecord SeasonPPG;
        public SeasonStatRecord SeasonRPG;
        public SeasonStatRecord SeasonAPG;

        public SeasonTeamRecord BestTeamRecord;
        public SeasonTeamRecord WorstTeamRecord;
    }

    [Serializable]
    public class SingleGameRecord
    {
        public string PlayerId;
        public string TeamId;
        public int Year;
        public int Value;
    }

    [Serializable]
    public class SeasonStatRecord
    {
        public string PlayerId;
        public int Year;
        public float Value;
    }

    [Serializable]
    public class SeasonTeamRecord
    {
        public string TeamId;
        public int Year;
        public int Wins;
        public int Losses;
    }

    [Serializable]
    public class HallOfFameInductee
    {
        public string PlayerId;
        public string PlayerName;
        public int InductionYear;
        public float HofScore;
        public bool IsFirstBallot;
        public int CareerPoints;
        public int Championships;
        public int MVPs;
        public int AllStarSelections;
        public int AllNBASelections;
    }

    [Serializable]
    public class RecordBroken
    {
        public RecordType RecordType;
        public string RecordName;
        public int NewValue;
        public int OldValue;
        public string PlayerId;
        public int Year;
    }

    [Serializable]
    public class CareerMilestone
    {
        public string PlayerId;
        public string PlayerName;
        public string StatName;
        public int MilestoneValue;
        public int ActualValue;
    }

    [Serializable]
    public class HistorySaveData
    {
        public List<SeasonArchive> SeasonArchives;
        public Dictionary<string, FranchiseRecords> FranchiseRecords;
        public LeagueRecords LeagueRecords;
        public List<HallOfFameInductee> HallOfFame;
        public List<string> HallOfFameEligible;
    }

    public enum RecordType
    {
        SingleGame,
        Season,
        Career,
        Franchise,
        League
    }

    public enum StatCategory
    {
        Points,
        Rebounds,
        Assists,
        Steals,
        Blocks,
        GamesPlayed,
        Minutes
    }

    #endregion
}
