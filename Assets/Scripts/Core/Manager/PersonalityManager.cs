using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages team chemistry, morale events, and personality interactions.
    /// </summary>
    public class PersonalityManager
    {
        private Dictionary<string, Personality> _playerPersonalities = new Dictionary<string, Personality>();
        private Dictionary<string, float> _teamChemistry = new Dictionary<string, float>(); // teamId -> chemistry

        // ==================== PERSONALITY ACCESS ====================

        public Personality GetPersonality(string playerId)
        {
            return _playerPersonalities.TryGetValue(playerId, out var p) ? p : null;
        }

        public void SetPersonality(string playerId, Personality personality)
        {
            _playerPersonalities[playerId] = personality;
        }

        // ==================== CHEMISTRY CALCULATIONS ====================

        /// <summary>
        /// Calculates chemistry between two players (-1 to +1).
        /// </summary>
        public float CalculatePairChemistry(string player1Id, string player2Id)
        {
            var p1 = GetPersonality(player1Id);
            var p2 = GetPersonality(player2Id);
            
            if (p1 == null || p2 == null)
                return 0f;
            
            float chemistry = 0f;
            
            // Check for good combinations
            chemistry += GetTraitSynergy(p1, p2);
            
            // Check for conflicts
            chemistry -= GetTraitConflict(p1, p2);
            
            return Mathf.Clamp(chemistry, -1f, 1f);
        }

        private float GetTraitSynergy(Personality p1, Personality p2)
        {
            float synergy = 0f;
            
            // Leader + Team Player = great chemistry
            if (p1.HasTrait(PersonalityTrait.Leader) && p2.HasTrait(PersonalityTrait.TeamPlayer))
                synergy += 0.3f;
            if (p2.HasTrait(PersonalityTrait.Leader) && p1.HasTrait(PersonalityTrait.TeamPlayer))
                synergy += 0.3f;
            
            // Mentor + any young player benefits
            if (p1.HasTrait(PersonalityTrait.Mentor) && p2.HasTrait(PersonalityTrait.TeamPlayer))
                synergy += 0.2f;
            
            // Two competitors push each other
            if (p1.HasTrait(PersonalityTrait.Competitor) && p2.HasTrait(PersonalityTrait.Competitor))
                synergy += 0.2f;
            
            // Professionals work well with anyone
            if (p1.HasTrait(PersonalityTrait.Professional) || p2.HasTrait(PersonalityTrait.Professional))
                synergy += 0.1f;
            
            // Ring chasers have team-first mentality
            if (p1.HasTrait(PersonalityTrait.RingChaser) && p2.HasTrait(PersonalityTrait.RingChaser))
                synergy += 0.2f;
            
            return synergy;
        }

        private float GetTraitConflict(Personality p1, Personality p2)
        {
            float conflict = 0f;
            
            // Two ball hogs = disaster
            if (p1.HasTrait(PersonalityTrait.BallHog) && p2.HasTrait(PersonalityTrait.BallHog))
                conflict += 0.5f;
            
            // Two leaders can clash
            if (p1.HasTrait(PersonalityTrait.Leader) && p2.HasTrait(PersonalityTrait.Leader))
                conflict += 0.2f;
            
            // Volatile players cause issues
            if (p1.HasTrait(PersonalityTrait.Volatile) || p2.HasTrait(PersonalityTrait.Volatile))
                conflict += 0.1f;
            
            // Two volatile = big problem
            if (p1.HasTrait(PersonalityTrait.Volatile) && p2.HasTrait(PersonalityTrait.Volatile))
                conflict += 0.3f;
            
            // Spotlight seekers compete for attention
            if (p1.HasTrait(PersonalityTrait.SpotlightSeeker) && p2.HasTrait(PersonalityTrait.SpotlightSeeker))
                conflict += 0.2f;
            
            return conflict;
        }

        /// <summary>
        /// Calculates overall team chemistry (0-100).
        /// </summary>
        public float CalculateTeamChemistry(List<string> rosterPlayerIds)
        {
            if (rosterPlayerIds.Count < 2)
                return 50f;
            
            float totalChemistry = 0f;
            int pairs = 0;
            
            // Calculate all pair combinations
            for (int i = 0; i < rosterPlayerIds.Count; i++)
            {
                for (int j = i + 1; j < rosterPlayerIds.Count; j++)
                {
                    totalChemistry += CalculatePairChemistry(rosterPlayerIds[i], rosterPlayerIds[j]);
                    pairs++;
                }
            }
            
            // Average chemistry (-1 to 1) -> convert to 0-100
            float avgChemistry = pairs > 0 ? totalChemistry / pairs : 0f;
            return 50f + (avgChemistry * 50f);
        }

        // ==================== MORALE EVENTS ====================

        /// <summary>
        /// Applies a morale event to all team players.
        /// </summary>
        public void ApplyTeamMoraleEvent(List<string> playerIds, MoraleEvent eventType)
        {
            int baseAmount = GetBaseEventAmount(eventType);
            
            foreach (var playerId in playerIds)
            {
                var personality = GetPersonality(playerId);
                if (personality != null)
                {
                    personality.AdjustMorale(eventType, baseAmount);
                }
            }
        }

        /// <summary>
        /// Applies a morale event to a specific player.
        /// </summary>
        public int ApplyPlayerMoraleEvent(string playerId, MoraleEvent eventType, int? customAmount = null)
        {
            var personality = GetPersonality(playerId);
            if (personality == null)
                return 0;
            
            int baseAmount = customAmount ?? GetBaseEventAmount(eventType);
            return personality.AdjustMorale(eventType, baseAmount);
        }

        private int GetBaseEventAmount(MoraleEvent eventType)
        {
            return eventType switch
            {
                MoraleEvent.Win => 3,
                MoraleEvent.Loss => -3,
                MoraleEvent.BigWin => 8,
                MoraleEvent.ToughLoss => -8,
                MoraleEvent.GotBenched => -10,
                MoraleEvent.BecameStarter => 15,
                MoraleEvent.LowUsage => -5,
                MoraleEvent.HighUsage => 5,
                MoraleEvent.CoachPraise => 8,
                MoraleEvent.CoachCriticism => -6,
                MoraleEvent.TeammateConflict => -12,
                MoraleEvent.ContractSigned => 10,
                MoraleEvent.TradeRumors => -8,
                MoraleEvent.AllStarSelection => 15,
                MoraleEvent.Injury => -10,
                _ => 0
            };
        }

        // ==================== ROLE SATISFACTION ====================

        /// <summary>
        /// Checks if player is satisfied with their role.
        /// Returns morale impact per game.
        /// </summary>
        public int GetRoleSatisfaction(string playerId, PlayerRole actualRole)
        {
            var personality = GetPersonality(playerId);
            if (personality == null)
                return 0;
            
            int roleDiff = (int)actualRole - (int)personality.ExpectedRole;
            
            // Playing below expected role
            if (roleDiff > 0)
            {
                int dissatisfaction = roleDiff * -3;
                
                // Some personalities care more about role
                if (personality.HasTrait(PersonalityTrait.Competitor))
                    dissatisfaction = (int)(dissatisfaction * 1.5f);
                if (personality.HasTrait(PersonalityTrait.BallHog))
                    dissatisfaction = (int)(dissatisfaction * 1.3f);
                if (personality.HasTrait(PersonalityTrait.Professional))
                    dissatisfaction = (int)(dissatisfaction * 0.5f);
                if (personality.HasTrait(PersonalityTrait.TeamPlayer))
                    dissatisfaction = (int)(dissatisfaction * 0.3f);
                
                return dissatisfaction;
            }
            
            // Playing above expected role = happy
            if (roleDiff < 0)
                return Math.Abs(roleDiff) * 2;
            
            return 0; // Met expectations
        }

        // ==================== LOCKER ROOM ====================

        /// <summary>
        /// Gets players who might cause locker room issues.
        /// </summary>
        public List<string> GetPotentialTroublemakers(List<string> rosterPlayerIds)
        {
            var troublemakers = new List<string>();
            
            foreach (var playerId in rosterPlayerIds)
            {
                var personality = GetPersonality(playerId);
                if (personality == null) continue;
                
                // Low morale + volatile = danger
                if (personality.Morale < 30 && personality.HasTrait(PersonalityTrait.Volatile))
                    troublemakers.Add(playerId);
                
                // Very unhappy ball hog
                if (personality.Morale < 25 && personality.HasTrait(PersonalityTrait.BallHog))
                    troublemakers.Add(playerId);
            }
            
            return troublemakers.Distinct().ToList();
        }

        /// <summary>
        /// Gets the team leader (highest morale Leader trait).
        /// </summary>
        public string GetTeamLeader(List<string> rosterPlayerIds)
        {
            string leader = null;
            int highestMorale = 0;
            
            foreach (var playerId in rosterPlayerIds)
            {
                var personality = GetPersonality(playerId);
                if (personality == null) continue;
                
                if (personality.HasTrait(PersonalityTrait.Leader) && personality.Morale > highestMorale)
                {
                    leader = playerId;
                    highestMorale = personality.Morale;
                }
            }
            
            return leader;
        }

        // ==================== MENTORSHIP ====================

        /// <summary>
        /// Gets development bonus for young players based on mentors.
        /// </summary>
        public float GetMentorshipBonus(string youngPlayerId, List<string> rosterPlayerIds)
        {
            float bonus = 0f;

            foreach (var playerId in rosterPlayerIds)
            {
                if (playerId == youngPlayerId) continue;

                var personality = GetPersonality(playerId);
                if (personality == null) continue;

                if (personality.HasTrait(PersonalityTrait.Mentor))
                {
                    // Good chemistry = better mentorship
                    float chemistry = CalculatePairChemistry(youngPlayerId, playerId);
                    bonus += 0.1f + (chemistry * 0.1f);
                }
            }

            return Mathf.Clamp(bonus, 0f, 0.3f); // Max 30% development bonus
        }

        // ==================== SAVE/LOAD ====================

        /// <summary>
        /// Creates save data for the personality system.
        /// </summary>
        public PersonalitySystemSaveData CreateSaveData()
        {
            var data = new PersonalitySystemSaveData
            {
                TeamChemistry = new Dictionary<string, float>(_teamChemistry)
            };

            foreach (var kvp in _playerPersonalities)
            {
                var saveState = PlayerPersonalitySaveState.CreateFrom(kvp.Key, kvp.Value);
                if (saveState != null)
                {
                    data.PlayerPersonalities.Add(saveState);
                }
            }

            Debug.Log($"[PersonalityManager] Saved {data.PlayerPersonalities.Count} personalities");
            return data;
        }

        /// <summary>
        /// Restores personality system from save data.
        /// </summary>
        public void LoadSaveData(PersonalitySystemSaveData data)
        {
            if (data == null) return;

            _playerPersonalities.Clear();
            _teamChemistry.Clear();

            // Restore personalities
            if (data.PlayerPersonalities != null)
            {
                foreach (var state in data.PlayerPersonalities)
                {
                    if (state != null && !string.IsNullOrEmpty(state.PlayerId))
                    {
                        _playerPersonalities[state.PlayerId] = state.ToPersonality();
                    }
                }
            }

            // Restore team chemistry
            if (data.TeamChemistry != null)
            {
                foreach (var kvp in data.TeamChemistry)
                {
                    _teamChemistry[kvp.Key] = kvp.Value;
                }
            }

            Debug.Log($"[PersonalityManager] Loaded {_playerPersonalities.Count} personalities");
        }
    }
}
