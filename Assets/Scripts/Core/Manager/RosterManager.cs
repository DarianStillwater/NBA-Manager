using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages team rosters including standard, two-way, and hardship spots.
    /// Enforces all CBA roster limits and rules.
    /// </summary>
    public class RosterManager
    {
        // ==================== ROSTER LIMITS (2023 CBA) ====================
        
        public const int STANDARD_ROSTER_MAX = 15;
        public const int STANDARD_ROSTER_MIN = 14; // During season (12 at start)
        public const int TWO_WAY_SLOTS = 3;
        public const int TRAINING_CAMP_MAX = 20;
        public const int TWO_WAY_MAX_EXPERIENCE = 4; // Years in league
        public const int TWO_WAY_NBA_GAME_LIMIT = 50;
        public const int TEN_DAY_START_DAY = 5; // January 5th
        public const int MAX_TEN_DAY_PER_PLAYER = 2;

        private Dictionary<string, TeamRoster> _teamRosters = new Dictionary<string, TeamRoster>();
        private SalaryCapManager _capManager;

        public RosterManager(SalaryCapManager capManager)
        {
            _capManager = capManager;
        }

        // ==================== ROSTER ACCESS ====================

        public TeamRoster GetRoster(string teamId)
        {
            if (!_teamRosters.TryGetValue(teamId, out var roster))
            {
                roster = new TeamRoster { TeamId = teamId };
                _teamRosters[teamId] = roster;
            }
            return roster;
        }

        public int GetStandardRosterCount(string teamId)
        {
            return GetRoster(teamId).StandardPlayers.Count;
        }

        public int GetTwoWayCount(string teamId)
        {
            return GetRoster(teamId).TwoWayPlayers.Count;
        }

        // ==================== ROSTER VALIDATION ====================

        /// <summary>
        /// Checks if team can add a player to standard roster.
        /// </summary>
        public RosterValidationResult CanAddToStandardRoster(string teamId)
        {
            var result = new RosterValidationResult { IsValid = true };
            var roster = GetRoster(teamId);

            if (roster.StandardPlayers.Count >= STANDARD_ROSTER_MAX)
            {
                result.IsValid = false;
                result.Reason = $"Standard roster full ({STANDARD_ROSTER_MAX} players)";
            }

            return result;
        }

        /// <summary>
        /// Checks if team can add a two-way player.
        /// </summary>
        public RosterValidationResult CanAddTwoWay(string teamId, int playerYearsExperience)
        {
            var result = new RosterValidationResult { IsValid = true };
            var roster = GetRoster(teamId);

            if (roster.TwoWayPlayers.Count >= TWO_WAY_SLOTS)
            {
                result.IsValid = false;
                result.Reason = $"All {TWO_WAY_SLOTS} two-way slots filled";
                return result;
            }

            if (playerYearsExperience >= TWO_WAY_MAX_EXPERIENCE)
            {
                result.IsValid = false;
                result.Reason = $"Player has {playerYearsExperience}+ years experience (max {TWO_WAY_MAX_EXPERIENCE - 1})";
            }

            return result;
        }

        /// <summary>
        /// Checks if team can sign a 10-day contract.
        /// </summary>
        public RosterValidationResult CanSign10DayContract(string teamId, string playerId, DateTime currentDate)
        {
            var result = new RosterValidationResult { IsValid = true };
            var roster = GetRoster(teamId);

            // Check date (after January 5)
            if (currentDate.Month == 1 && currentDate.Day < TEN_DAY_START_DAY)
            {
                result.IsValid = false;
                result.Reason = "10-day contracts cannot be signed before January 5";
                return result;
            }

            // Before deadline (10 days before season end)
            // Simplified: assume season ends April 15
            var deadline = new DateTime(currentDate.Year, 4, 5);
            if (currentDate > deadline)
            {
                result.IsValid = false;
                result.Reason = "10-day contract deadline has passed";
                return result;
            }

            // Check if player already had 2 10-day contracts with this team
            int priorTenDays = roster.GetTenDayContractCount(playerId);
            if (priorTenDays >= MAX_TEN_DAY_PER_PLAYER)
            {
                result.IsValid = false;
                result.Reason = $"Player already had {MAX_TEN_DAY_PER_PLAYER} 10-day contracts with this team";
            }

            return result;
        }

        // ==================== HARDSHIP EXCEPTION ====================

        /// <summary>
        /// Checks if team qualifies for hardship exception.
        /// Requires 4+ players injured for 3+ games, expected out 2+ weeks.
        /// </summary>
        public bool QualifiesForHardship(string teamId)
        {
            var roster = GetRoster(teamId);
            return roster.InjuredPlayers.Count(p => p.IsLongTermInjury) >= 4;
        }

        /// <summary>
        /// Gets number of hardship slots available.
        /// 4 injured = 1 slot, 5 injured = 2 slots, etc.
        /// </summary>
        public int GetHardshipSlots(string teamId)
        {
            var roster = GetRoster(teamId);
            int longTermInjured = roster.InjuredPlayers.Count(p => p.IsLongTermInjury);
            return Math.Max(0, longTermInjured - 3); // 4=1, 5=2, 6=3, etc.
        }

        /// <summary>
        /// Adds player via hardship exception.
        /// </summary>
        public RosterValidationResult AddViaHardship(string teamId, string playerId)
        {
            var result = new RosterValidationResult { IsValid = true };

            if (!QualifiesForHardship(teamId))
            {
                result.IsValid = false;
                result.Reason = "Team does not qualify for hardship (needs 4+ long-term injured)";
                return result;
            }

            var roster = GetRoster(teamId);
            int usedSlots = roster.HardshipPlayers.Count;
            int availableSlots = GetHardshipSlots(teamId);

            if (usedSlots >= availableSlots)
            {
                result.IsValid = false;
                result.Reason = $"All hardship slots used ({usedSlots}/{availableSlots})";
                return result;
            }

            roster.HardshipPlayers.Add(new RosterSpot { PlayerId = playerId, AddedDate = DateTime.Now });
            return result;
        }

        // ==================== ROSTER OPERATIONS ====================

        /// <summary>
        /// Adds player to standard roster.
        /// </summary>
        public bool AddToStandardRoster(string teamId, string playerId)
        {
            if (!CanAddToStandardRoster(teamId).IsValid)
                return false;

            var roster = GetRoster(teamId);
            roster.StandardPlayers.Add(new RosterSpot { PlayerId = playerId, AddedDate = DateTime.Now });
            return true;
        }

        /// <summary>
        /// Adds player on two-way contract.
        /// </summary>
        public bool AddTwoWayPlayer(string teamId, string playerId, int yearsExperience)
        {
            if (!CanAddTwoWay(teamId, yearsExperience).IsValid)
                return false;

            var roster = GetRoster(teamId);
            roster.TwoWayPlayers.Add(new TwoWayRosterSpot 
            { 
                PlayerId = playerId, 
                AddedDate = DateTime.Now,
                NBAGamesPlayed = 0
            });
            return true;
        }

        /// <summary>
        /// Converts two-way player to standard contract.
        /// Required for playoff eligibility.
        /// </summary>
        public bool ConvertTwoWayToStandard(string teamId, string playerId)
        {
            var roster = GetRoster(teamId);
            var twoWay = roster.TwoWayPlayers.FirstOrDefault(p => p.PlayerId == playerId);
            
            if (twoWay == null)
                return false;

            if (!CanAddToStandardRoster(teamId).IsValid)
                return false;

            roster.TwoWayPlayers.Remove(twoWay);
            roster.StandardPlayers.Add(new RosterSpot { PlayerId = playerId, AddedDate = DateTime.Now });
            return true;
        }

        /// <summary>
        /// Removes player from roster (waive, trade, etc).
        /// </summary>
        public bool RemoveFromRoster(string teamId, string playerId)
        {
            var roster = GetRoster(teamId);
            
            var standard = roster.StandardPlayers.FirstOrDefault(p => p.PlayerId == playerId);
            if (standard != null)
            {
                roster.StandardPlayers.Remove(standard);
                return true;
            }

            var twoWay = roster.TwoWayPlayers.FirstOrDefault(p => p.PlayerId == playerId);
            if (twoWay != null)
            {
                roster.TwoWayPlayers.Remove(twoWay);
                return true;
            }

            var hardship = roster.HardshipPlayers.FirstOrDefault(p => p.PlayerId == playerId);
            if (hardship != null)
            {
                roster.HardshipPlayers.Remove(hardship);
                return true;
            }

            return false;
        }

        // ==================== INJURY TRACKING ====================

        /// <summary>
        /// Marks player as injured. Used for hardship exception calculation.
        /// </summary>
        public void MarkPlayerInjured(string teamId, string playerId, int expectedWeeksOut)
        {
            var roster = GetRoster(teamId);
            roster.InjuredPlayers.Add(new InjuredPlayer
            {
                PlayerId = playerId,
                InjuryDate = DateTime.Now,
                ExpectedWeeksOut = expectedWeeksOut,
                GamesMissed = 0
            });
        }

        /// <summary>
        /// Returns player from injury. May require roster move if hardship player present.
        /// </summary>
        public void ReturnFromInjury(string teamId, string playerId)
        {
            var roster = GetRoster(teamId);
            roster.InjuredPlayers.RemoveAll(p => p.PlayerId == playerId);

            // Check if hardship players need to be released
            if (roster.HardshipPlayers.Any() && !QualifiesForHardship(teamId))
            {
                Debug.LogWarning($"{teamId}: Player returned from injury. Must release hardship player.");
            }
        }

        // ==================== TWO-WAY GAME TRACKING ====================

        /// <summary>
        /// Records that a two-way player played in an NBA game.
        /// </summary>
        public void RecordTwoWayGame(string teamId, string playerId)
        {
            var roster = GetRoster(teamId);
            var twoWay = roster.TwoWayPlayers.FirstOrDefault(p => p.PlayerId == playerId);
            
            if (twoWay != null)
            {
                twoWay.NBAGamesPlayed++;
                
                if (twoWay.NBAGamesPlayed >= TWO_WAY_NBA_GAME_LIMIT)
                {
                    Debug.LogWarning($"{playerId} hit {TWO_WAY_NBA_GAME_LIMIT} NBA games - must convert or release");
                }
            }
        }

        /// <summary>
        /// Gets remaining NBA games for a two-way player.
        /// </summary>
        public int GetTwoWayGamesRemaining(string teamId, string playerId)
        {
            var roster = GetRoster(teamId);
            var twoWay = roster.TwoWayPlayers.FirstOrDefault(p => p.PlayerId == playerId);
            return twoWay != null ? TWO_WAY_NBA_GAME_LIMIT - twoWay.NBAGamesPlayed : 0;
        }

        // ==================== PLAYOFF ELIGIBILITY ====================

        /// <summary>
        /// Gets playoff-eligible players. Two-way players are NOT eligible.
        /// </summary>
        public List<string> GetPlayoffEligiblePlayers(string teamId)
        {
            var roster = GetRoster(teamId);
            return roster.StandardPlayers.Select(p => p.PlayerId).ToList();
        }

        /// <summary>
        /// Checks if player is playoff eligible.
        /// </summary>
        public bool IsPlayoffEligible(string teamId, string playerId)
        {
            var roster = GetRoster(teamId);
            return roster.StandardPlayers.Any(p => p.PlayerId == playerId);
        }
    }

    // ==================== DATA STRUCTURES ====================

    [Serializable]
    public class TeamRoster
    {
        public string TeamId;
        public List<RosterSpot> StandardPlayers = new List<RosterSpot>();
        public List<TwoWayRosterSpot> TwoWayPlayers = new List<TwoWayRosterSpot>();
        public List<RosterSpot> HardshipPlayers = new List<RosterSpot>();
        public List<InjuredPlayer> InjuredPlayers = new List<InjuredPlayer>();
        public Dictionary<string, int> TenDayHistory = new Dictionary<string, int>(); // playerId -> count

        public int GetTenDayContractCount(string playerId)
        {
            return TenDayHistory.TryGetValue(playerId, out int count) ? count : 0;
        }

        public void RecordTenDayContract(string playerId)
        {
            if (!TenDayHistory.ContainsKey(playerId))
                TenDayHistory[playerId] = 0;
            TenDayHistory[playerId]++;
        }

        public int TotalRosterSize => StandardPlayers.Count + TwoWayPlayers.Count;
    }

    [Serializable]
    public class RosterSpot
    {
        public string PlayerId;
        public DateTime AddedDate;
    }

    [Serializable]
    public class TwoWayRosterSpot : RosterSpot
    {
        public int NBAGamesPlayed;
    }

    [Serializable]
    public class InjuredPlayer
    {
        public string PlayerId;
        public DateTime InjuryDate;
        public int ExpectedWeeksOut;
        public int GamesMissed;

        /// <summary>
        /// Long-term injury = missed 3+ games AND expected out 2+ weeks
        /// </summary>
        public bool IsLongTermInjury => GamesMissed >= 3 && ExpectedWeeksOut >= 2;
    }

    public class RosterValidationResult
    {
        public bool IsValid;
        public string Reason;
    }
}
