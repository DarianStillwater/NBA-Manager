using System;
using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Handles all foul-related logic including probability calculations,
    /// team foul tracking, bonus situations, and technical/flagrant fouls.
    /// </summary>
    public class FoulSystem
    {
        // ==================== CONSTANTS ====================

        private const float BASE_FOUL_RATE = 0.18f;        // ~22 fouls per team per game
        private const float BASE_TECH_RATE = 0.002f;       // ~0.2% per possession
        private const float VOLATILE_TECH_BONUS = 0.015f;  // +1.5% for Volatile trait
        private const float FLAGRANT_REVIEW_RATE = 0.02f;  // 2% of clutch fouls reviewed
        private const int BONUS_THRESHOLD = 5;             // Team fouls for bonus
        private const int DOUBLE_BONUS_THRESHOLD = 10;     // Team fouls for double bonus
        private const int FOUL_OUT_LIMIT = 6;              // Personal fouls to foul out
        private const int TECH_EJECTION_LIMIT = 2;         // Technicals to eject

        // ==================== STATE ====================

        private Dictionary<string, int> _teamFoulsPerQuarter;  // teamId -> fouls this quarter
        private Dictionary<string, int> _playerPersonalFouls;  // playerId -> total fouls
        private Dictionary<string, int> _playerTechnicalFouls; // playerId -> technicals
        private int _currentQuarter;

        // ==================== INITIALIZATION ====================

        public FoulSystem()
        {
            _teamFoulsPerQuarter = new Dictionary<string, int>();
            _playerPersonalFouls = new Dictionary<string, int>();
            _playerTechnicalFouls = new Dictionary<string, int>();
            _currentQuarter = 1;
        }

        /// <summary>
        /// Reset team fouls at the start of a new quarter.
        /// Personal and technical fouls persist through the game.
        /// </summary>
        public void ResetQuarterFouls()
        {
            _teamFoulsPerQuarter.Clear();
            _currentQuarter++;
        }

        /// <summary>
        /// Initialize for a new game.
        /// </summary>
        public void ResetGame()
        {
            _teamFoulsPerQuarter.Clear();
            _playerPersonalFouls.Clear();
            _playerTechnicalFouls.Clear();
            _currentQuarter = 1;
        }

        // ==================== FOUL PROBABILITY ====================

        /// <summary>
        /// Calculate the probability of a defensive foul occurring.
        /// </summary>
        /// <param name="defender">The defending player</param>
        /// <param name="ballHandler">The offensive player with the ball</param>
        /// <param name="shotType">The type of shot being attempted (null if not shooting)</param>
        /// <param name="context">Current game context</param>
        /// <returns>Probability from 0 to 1</returns>
        public float CalculateFoulProbability(Player defender, Player ballHandler, ShotType? shotType, GameContext context)
        {
            float probability = BASE_FOUL_RATE;

            // Defender modifiers (higher stats = fewer fouls)
            probability -= (defender.DefensiveIQ - 50) / 500f;      // ±10%
            probability -= (defender.Composure - 50) / 500f;        // ±10%

            // Tendencies
            if (defender.Tendencies != null)
            {
                // CloseoutControl: 0-100, higher = better closeouts = fewer fouls
                probability -= (defender.Tendencies.CloseoutControl - 50) / 400f;  // ±12.5%

                // DefensiveGambling: -100 to +100, positive = more gambling = more fouls
                probability += defender.Tendencies.DefensiveGambling / 500f;       // ±20%
            }

            // Aggression increases foul tendency
            probability += (defender.Aggression - 50) / 400f;  // ±12.5%

            // Ball handler attributes (high BallHandling = draws fouls)
            probability += (ballHandler.BallHandling - 50) / 400f;  // ±12.5%

            // Shot type modifiers - rim attacks draw more fouls
            if (shotType == ShotType.Dunk || shotType == ShotType.Layup)
            {
                probability += 0.10f;
            }
            else if (shotType == ShotType.Floater || shotType == ShotType.Hookshot)
            {
                probability += 0.05f;
            }

            // Defender in foul trouble - plays more carefully
            int defenderFouls = GetPlayerFouls(defender.PlayerId);
            if (defenderFouls >= 4)
            {
                probability -= 0.05f;  // More careful with 4+ fouls
            }
            else if (defenderFouls >= 5)
            {
                probability -= 0.08f;  // Very careful with 5 fouls
            }

            // Late game fouling intentionally (close game, last 2 minutes, trailing)
            if (context.IsLastTwoMinutes && context.ScoreDifferential < 0 && context.ScoreDifferential >= -10)
            {
                probability += 0.15f;  // Intentional fouls
            }

            return Mathf.Clamp(probability, 0.05f, 0.35f);
        }

        /// <summary>
        /// Calculate probability of a technical foul.
        /// </summary>
        public float CalculateTechnicalProbability(Player player, GameContext context)
        {
            float probability = BASE_TECH_RATE;

            // Volatile personality trait massively increases tech chance
            if (player.Personality?.HasTrait(PersonalityTrait.Volatile) == true)
            {
                probability += VOLATILE_TECH_BONUS;
            }

            // Low composure increases tech chance
            probability += (50 - player.Composure) / 500f;  // +0.1 at 0 composure

            // Frustration from losing big
            if (context.ScoreDifferential < -15)
            {
                probability += 0.005f;
            }

            // Already has 1 technical - player is more careful
            if (GetPlayerTechnicals(player.PlayerId) == 1)
            {
                probability *= 0.3f;
            }

            return Mathf.Clamp(probability, 0.0005f, 0.05f);
        }

        // ==================== FOUL TYPE DETERMINATION ====================

        /// <summary>
        /// Determine if a foul is a shooting foul.
        /// </summary>
        public bool IsShootingFoul(ShotType? shotType, float shotClockRemaining)
        {
            // If shot was being attempted, it's a shooting foul
            if (shotType != null) return true;

            // If shot clock < 4, often fouling shooter
            if (shotClockRemaining < 4f) return UnityEngine.Random.value < 0.6f;

            return false;
        }

        /// <summary>
        /// Determine if a foul should be upgraded to flagrant (only in clutch time).
        /// </summary>
        public FoulType DetermineFlagrantType(Player defender, ShotType? shotType, bool isClutchTime)
        {
            // Only review in clutch time (per requirements)
            if (!isClutchTime) return FoulType.Personal;

            float flagrantChance = FLAGRANT_REVIEW_RATE;

            // High aggression increases flagrant risk
            flagrantChance += (defender.Aggression - 50) / 1000f;

            // Hard fouls on driving plays
            if (shotType == ShotType.Dunk || shotType == ShotType.Layup)
            {
                flagrantChance += 0.02f;
            }

            if (UnityEngine.Random.value < flagrantChance)
            {
                // 70% Flagrant 1, 30% Flagrant 2
                return UnityEngine.Random.value < 0.70f ? FoulType.Flagrant1 : FoulType.Flagrant2;
            }

            return FoulType.Personal;
        }

        /// <summary>
        /// Determine free throw scenario based on foul type and game situation.
        /// </summary>
        public FreeThrowScenario DetermineFreeThrowScenario(
            FoulType foulType,
            bool isShootingFoul,
            bool shotWasMade,
            bool isBehindArc,
            string teamId)
        {
            switch (foulType)
            {
                case FoulType.Technical:
                    return FreeThrowScenario.Technical;

                case FoulType.Flagrant1:
                case FoulType.Flagrant2:
                    return FreeThrowScenario.Flagrant;

                case FoulType.Offensive:
                    return FreeThrowScenario.None;  // Offensive foul = turnover, no FTs

                default:
                    if (isShootingFoul)
                    {
                        if (shotWasMade)
                            return FreeThrowScenario.AndOne;
                        return isBehindArc ? FreeThrowScenario.ThreeShots : FreeThrowScenario.TwoShots;
                    }
                    else
                    {
                        // Non-shooting foul - check bonus
                        if (IsInBonus(teamId))
                            return FreeThrowScenario.Bonus;
                        return FreeThrowScenario.None;
                    }
            }
        }

        /// <summary>
        /// Get number of free throws for a scenario.
        /// </summary>
        public int GetFreeThrowCount(FreeThrowScenario scenario)
        {
            return scenario switch
            {
                FreeThrowScenario.AndOne => 1,
                FreeThrowScenario.TwoShots => 2,
                FreeThrowScenario.ThreeShots => 3,
                FreeThrowScenario.Bonus => 2,
                FreeThrowScenario.Technical => 1,
                FreeThrowScenario.Flagrant => 2,
                _ => 0
            };
        }

        // ==================== TEAM FOUL TRACKING ====================

        /// <summary>
        /// Record a team foul for the quarter.
        /// </summary>
        public void AddTeamFoul(string teamId)
        {
            if (!_teamFoulsPerQuarter.ContainsKey(teamId))
                _teamFoulsPerQuarter[teamId] = 0;
            _teamFoulsPerQuarter[teamId]++;
        }

        /// <summary>
        /// Get team fouls for the current quarter.
        /// </summary>
        public int GetTeamFouls(string teamId)
        {
            return _teamFoulsPerQuarter.TryGetValue(teamId, out int fouls) ? fouls : 0;
        }

        /// <summary>
        /// Check if team is in bonus (5+ team fouls this quarter).
        /// </summary>
        public bool IsInBonus(string teamId)
        {
            return GetTeamFouls(teamId) >= BONUS_THRESHOLD;
        }

        /// <summary>
        /// Check if team is in double bonus (10+ team fouls this quarter).
        /// </summary>
        public bool IsInDoubleBonus(string teamId)
        {
            return GetTeamFouls(teamId) >= DOUBLE_BONUS_THRESHOLD;
        }

        // ==================== PERSONAL FOUL TRACKING ====================

        /// <summary>
        /// Record a personal foul for a player.
        /// </summary>
        public void AddPlayerFoul(string playerId)
        {
            if (!_playerPersonalFouls.ContainsKey(playerId))
                _playerPersonalFouls[playerId] = 0;
            _playerPersonalFouls[playerId]++;
        }

        /// <summary>
        /// Get player's total personal fouls.
        /// </summary>
        public int GetPlayerFouls(string playerId)
        {
            return _playerPersonalFouls.TryGetValue(playerId, out int fouls) ? fouls : 0;
        }

        /// <summary>
        /// Check if player has fouled out (6+ fouls).
        /// </summary>
        public bool HasFouledOut(string playerId)
        {
            return GetPlayerFouls(playerId) >= FOUL_OUT_LIMIT;
        }

        // ==================== TECHNICAL FOUL TRACKING ====================

        /// <summary>
        /// Record a technical foul for a player.
        /// </summary>
        public void AddTechnicalFoul(string playerId)
        {
            if (!_playerTechnicalFouls.ContainsKey(playerId))
                _playerTechnicalFouls[playerId] = 0;
            _playerTechnicalFouls[playerId]++;
        }

        /// <summary>
        /// Get player's technical fouls.
        /// </summary>
        public int GetPlayerTechnicals(string playerId)
        {
            return _playerTechnicalFouls.TryGetValue(playerId, out int techs) ? techs : 0;
        }

        /// <summary>
        /// Check if player should be ejected (2+ technicals).
        /// </summary>
        public bool ShouldEject(string playerId)
        {
            return GetPlayerTechnicals(playerId) >= TECH_EJECTION_LIMIT;
        }

        // ==================== FOUL EVENT CREATION ====================

        /// <summary>
        /// Create a complete foul event with all details.
        /// </summary>
        public PossessionEvent CreateFoulEvent(
            Player fouledPlayer,
            Player fouler,
            string foulerTeamId,
            FoulType foulType,
            bool isShootingFoul,
            bool shotWasMade,
            bool isBehindArc,
            ShotType? shotType,
            float gameClock,
            int quarter,
            GameContext context)
        {
            // Upgrade to flagrant if applicable
            if (foulType == FoulType.Personal || foulType == FoulType.Shooting)
            {
                foulType = DetermineFlagrantType(fouler, shotType, context.IsClutchTime);
            }

            // Record the foul
            if (foulType != FoulType.Technical)
            {
                AddPlayerFoul(fouler.PlayerId);
                AddTeamFoul(foulerTeamId);
            }
            else
            {
                AddTechnicalFoul(fouler.PlayerId);
            }

            // Determine free throw scenario
            var scenario = DetermineFreeThrowScenario(
                foulType,
                isShootingFoul,
                shotWasMade,
                isBehindArc,
                foulerTeamId);

            int freeThrows = GetFreeThrowCount(scenario);

            // Check for foul out or ejection
            bool fouledOut = HasFouledOut(fouler.PlayerId);
            bool ejected = foulType == FoulType.Flagrant2 ||
                          (foulType == FoulType.Technical && ShouldEject(fouler.PlayerId));

            var foulDetail = new FoulEventDetail
            {
                FoulType = foulType,
                FoulerId = fouler.PlayerId,
                FouledPlayerId = fouledPlayer.PlayerId,
                FreeThrowsAwarded = freeThrows,
                Scenario = scenario,
                IsBonusSituation = IsInBonus(foulerTeamId),
                IsDoubleBonusSituation = IsInDoubleBonus(foulerTeamId),
                FoulerTotalFouls = GetPlayerFouls(fouler.PlayerId),
                TeamFoulsThisQuarter = GetTeamFouls(foulerTeamId),
                FoulerFouledOut = fouledOut,
                FoulerEjected = ejected
            };

            return new PossessionEvent
            {
                Type = EventType.Foul,
                GameClock = gameClock,
                Quarter = quarter,
                ActorPlayerId = fouledPlayer.PlayerId,
                DefenderPlayerId = fouler.PlayerId,
                Outcome = EventOutcome.Success,
                ShotType = shotType,
                IsAndOne = scenario == FreeThrowScenario.AndOne,
                FoulDetail = foulDetail
            };
        }

        // ==================== FOUL TROUBLE INDICATORS ====================

        /// <summary>
        /// Get qualitative foul trouble indicator for UI (no numbers shown).
        /// </summary>
        public string GetFoulTroubleIndicator(string playerId, int quarter)
        {
            int fouls = GetPlayerFouls(playerId);

            if (fouls >= 5)
                return "FOUL TROUBLE: One more and he's out!";
            if (quarter <= 2 && fouls >= 3)
                return "FOUL TROUBLE: Heavy foul load early";
            if (quarter == 3 && fouls >= 4)
                return "FOUL TROUBLE: Walking a tightrope";
            if (quarter >= 4 && fouls >= 4)
                return "FOUL TROUBLE: In danger of fouling out";

            return null;  // No indicator if not in trouble
        }

        /// <summary>
        /// Get team foul status for UI display.
        /// </summary>
        public string GetTeamFoulStatus(string teamId)
        {
            int fouls = GetTeamFouls(teamId);

            if (fouls >= DOUBLE_BONUS_THRESHOLD)
                return "DOUBLE BONUS";
            if (fouls >= BONUS_THRESHOLD)
                return "BONUS";

            return $"Team Fouls: {fouls}";
        }
    }
}
