using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Orchestrates the practice system including scheduling, execution, and results.
    /// Handles player development, play familiarity, and opponent preparation.
    /// </summary>
    public class PracticeManager
    {
        private static PracticeManager _instance;
        public static PracticeManager Instance => _instance ??= new PracticeManager();

        // ==================== EVENTS ====================
        public event Action<PracticeSession, PracticeResults> OnPracticeCompleted;
        public event Action<PracticeEvent> OnPracticeEvent;
        public event Action<PracticeInjury> OnPracticeInjury;
        public event Action<string, string, float> OnSkillProgress; // playerId, skill, progress

        // ==================== CONFIGURATION ====================
        private const float BASE_DEVELOPMENT_RATE = 1.0f;
        private const float FATIGUE_RECOVERY_PER_REST_DAY = 15f;
        private const float CHEMISTRY_CHANGE_PER_PRACTICE = 0.5f;
        private const float OPPONENT_PREP_BASE_BONUS = 5f;

        // ==================== DEPENDENCIES ====================
        private Func<string, Player> _getPlayer;
        private Func<string, Coach> _getCoach;
        private Func<string, Team> _getTeam;
        private Func<string, PlayBook> _getPlayBook;
        private System.Random _random;

        // ==================== STATE ====================
        private Dictionary<string, List<PracticeSession>> _teamPracticeHistory = new Dictionary<string, List<PracticeSession>>();
        private Dictionary<string, float> _opponentPrepBonuses = new Dictionary<string, float>(); // opponent team ID -> bonus

        /// <summary>
        /// Initializes the manager with dependency providers.
        /// </summary>
        public void Initialize(
            Func<string, Player> getPlayer,
            Func<string, Coach> getCoach,
            Func<string, Team> getTeam,
            Func<string, PlayBook> getPlayBook,
            int? seed = null)
        {
            _getPlayer = getPlayer;
            _getCoach = getCoach;
            _getTeam = getTeam;
            _getPlayBook = getPlayBook;
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        /// <summary>
        /// Generates a recommended practice plan for the day based on context.
        /// </summary>
        public PracticeSession GenerateRecommendedPractice(string teamId, DateTime date, ScheduleContext context)
        {
            PracticeSession session;

            // Determine practice type based on schedule context
            if (context.IsGameDay)
            {
                // Game day = light shootaround
                session = PracticeSession.CreateShootaround(teamId, date, context.NextOpponentId);
                AddShootaroundDrills(session);
            }
            else if (context.IsBackToBackRest)
            {
                // Rest day after back-to-back
                session = PracticeSession.CreateRecoverySession(teamId, date);
                AddRecoveryDrills(session);
            }
            else if (context.DaysUntilNextGame <= 1 && !string.IsNullOrEmpty(context.NextOpponentId))
            {
                // Day before game = opponent prep
                session = PracticeSession.CreateGamePrepPractice(teamId, date, context.NextOpponentId);
                AddGamePrepDrills(session, context.NextOpponentId);
            }
            else if (context.DaysUntilNextGame >= 3)
            {
                // Several days off = heavy development
                session = PracticeSession.CreateDevelopmentPractice(teamId, date);
                session.Intensity = PracticeIntensity.High;
                AddDevelopmentDrills(session, context);
            }
            else
            {
                // Normal practice day
                session = PracticeSession.CreateDevelopmentPractice(teamId, date);
                AddBalancedDrills(session, context);
            }

            return session;
        }

        /// <summary>
        /// Executes a practice session and returns the results.
        /// </summary>
        public PracticeResults ExecutePractice(PracticeSession session)
        {
            var results = new PracticeResults();
            var team = _getTeam?.Invoke(session.TeamId);
            var headCoach = team != null ? _getCoach?.Invoke(team.HeadCoachId) : null;

            // Determine participating players
            var participantIds = session.ParticipatingPlayerIds.Count > 0
                ? session.ParticipatingPlayerIds
                : team?.RosterPlayerIds?.Where(id => !session.RestingPlayerIds.Contains(id)).ToList()
                  ?? new List<string>();

            // Calculate overall practice quality
            results.OverallQuality = CalculatePracticeQuality(session, headCoach, participantIds);
            results.PlayerEngagement = CalculatePlayerEngagement(session, participantIds);

            // Process each player
            foreach (var playerId in participantIds)
            {
                var player = _getPlayer?.Invoke(playerId);
                if (player == null) continue;

                var playerGains = ProcessPlayerPractice(player, session, headCoach, results.OverallQuality);
                results.PlayerGains[playerId] = playerGains;

                // Check for injury
                if (CheckForPracticeInjury(player, session))
                {
                    var injury = GeneratePracticeInjury(player, session);
                    results.Injuries.Add(injury);
                    OnPracticeInjury?.Invoke(injury);
                }
            }

            // Process play familiarity gains
            if (session.PlaysToDrill.Count > 0 || session.Drills.Any(d => d.Drill.PlayFamiliarityBonus > 0))
            {
                var playBook = _getPlayBook?.Invoke(session.TeamId);
                if (playBook != null)
                {
                    ProcessPlayFamiliarityGains(session, playBook, results);
                }
            }

            // Calculate opponent prep bonus
            if (session.IsGamePrepSession)
            {
                results.OpponentPrepBonus = CalculateOpponentPrepBonus(session, headCoach, results.OverallQuality);
                AddOpponentPrepBonus(session.OpponentTeamId, results.OpponentPrepBonus);
            }

            // Calculate chemistry change
            results.ChemistryChange = CalculateChemistryChange(session, results);

            // Generate practice events
            GeneratePracticeEvents(session, participantIds, results);

            // Mark session as completed
            session.IsCompleted = true;
            session.Results = results;

            // Store in history
            if (!_teamPracticeHistory.ContainsKey(session.TeamId))
                _teamPracticeHistory[session.TeamId] = new List<PracticeSession>();
            _teamPracticeHistory[session.TeamId].Add(session);

            // Fire event
            OnPracticeCompleted?.Invoke(session, results);

            return results;
        }

        /// <summary>
        /// Gets the current opponent preparation bonus for a team.
        /// </summary>
        public float GetOpponentPrepBonus(string opponentTeamId)
        {
            return _opponentPrepBonuses.TryGetValue(opponentTeamId, out var bonus) ? bonus : 0f;
        }

        /// <summary>
        /// Clears opponent prep bonus after game is played.
        /// </summary>
        public void ClearOpponentPrepBonus(string opponentTeamId)
        {
            _opponentPrepBonuses.Remove(opponentTeamId);
        }

        /// <summary>
        /// Gets practice history for a team.
        /// </summary>
        public List<PracticeSession> GetPracticeHistory(string teamId, int lastNSessions = 10)
        {
            if (!_teamPracticeHistory.TryGetValue(teamId, out var history))
                return new List<PracticeSession>();

            return history.TakeLast(lastNSessions).ToList();
        }

        // ==================== PRIVATE METHODS ====================

        private PlayerPracticeGains ProcessPlayerPractice(Player player, PracticeSession session, Coach coach, int practiceQuality)
        {
            var gains = new PlayerPracticeGains { PlayerId = player.PlayerId };

            float qualityMultiplier = practiceQuality / 100f;
            float coachMultiplier = coach != null ? (0.7f + coach.DevelopingPlayers / 200f) : 0.8f;
            float playerMultiplier = player.DevelopmentMultiplier;

            // Process each drill
            foreach (var scheduledDrill in session.Drills)
            {
                var drill = scheduledDrill.Drill;

                // Check if this player is a focus player for this drill
                bool isFocusPlayer = scheduledDrill.FocusPlayerIds.Count == 0 ||
                                     scheduledDrill.FocusPlayerIds.Contains(player.PlayerId);

                float focusMultiplier = isFocusPlayer ? 1.2f : 0.8f;

                // Check position relevance
                float positionMultiplier = 1.0f;
                if (drill.TargetPositions.Count > 0)
                {
                    positionMultiplier = drill.TargetPositions.Contains(player.Position) ? 1.2f : 0.6f;
                }

                // Calculate drill duration factor
                float durationFactor = scheduledDrill.DurationMinutes / (float)drill.RecommendedDuration;
                durationFactor = Mathf.Clamp(durationFactor, 0.5f, 1.5f);

                // Process skill gains
                foreach (var skillEffect in drill.SkillEffects)
                {
                    float progressGain = skillEffect.Value * BASE_DEVELOPMENT_RATE *
                                         qualityMultiplier * coachMultiplier * playerMultiplier *
                                         focusMultiplier * positionMultiplier * durationFactor;

                    // Add some randomness (0.8 to 1.2)
                    progressGain *= 0.8f + (float)_random.NextDouble() * 0.4f;

                    string attrName = skillEffect.Key.ToString();
                    if (!gains.SkillProgress.ContainsKey(attrName))
                        gains.SkillProgress[attrName] = 0f;
                    gains.SkillProgress[attrName] += progressGain;

                    OnSkillProgress?.Invoke(player.PlayerId, attrName, progressGain);
                }

                // Process tendency gains
                foreach (var tendencyEffect in drill.TendencyEffects)
                {
                    float progressGain = tendencyEffect.Value * 10f * // Base tendency progress
                                         qualityMultiplier * focusMultiplier * durationFactor;

                    if (!gains.TendencyProgress.ContainsKey(tendencyEffect.Key))
                        gains.TendencyProgress[tendencyEffect.Key] = 0f;
                    gains.TendencyProgress[tendencyEffect.Key] += progressGain;
                }

                // Track quality minutes
                if (isFocusPlayer)
                {
                    gains.QualityMinutes += scheduledDrill.DurationMinutes;
                }

                // Accumulate fatigue
                float fatigueCost = drill.FatigueCost * (scheduledDrill.DurationMinutes / 15f);
                fatigueCost *= GetIntensityFatigueMultiplier(session.Intensity);
                gains.FatigueIncurred += fatigueCost;
            }

            // Calculate morale change based on engagement and player preferences
            gains.MoraleChange = CalculatePlayerMoraleChange(player, session, gains);

            // Apply gains to player
            ApplyPracticeGainsToPlayer(player, gains);

            return gains;
        }

        private void ApplyPracticeGainsToPlayer(Player player, PlayerPracticeGains gains)
        {
            // Apply fatigue
            player.Energy = Mathf.Max(0, player.Energy - gains.FatigueIncurred);

            // Apply morale change
            player.AdjustMorale(gains.MoraleChange);

            // Note: Skill progress is tracked but applied through PlayerDevelopmentManager
            // Tendency progress is applied through TendencyCoachingManager
        }

        private int CalculatePracticeQuality(PracticeSession session, Coach coach, List<string> participantIds)
        {
            float quality = 50f; // Base quality

            // Coach influence
            if (coach != null)
            {
                quality += (coach.GameKnowledge - 50) / 5f; // +/- 10 from coach knowledge
                quality += (coach.MotivatingPlayers - 50) / 5f; // +/- 10 from motivation
            }

            // Intensity influence
            quality += session.Intensity switch
            {
                PracticeIntensity.VeryLight => -10f,
                PracticeIntensity.Light => -5f,
                PracticeIntensity.Normal => 0f,
                PracticeIntensity.High => 5f,
                PracticeIntensity.Intense => 10f,
                _ => 0f
            };

            // Duration influence (sweet spot is 90-120 min)
            if (session.DurationMinutes < 60)
                quality -= 10f;
            else if (session.DurationMinutes > 150)
                quality -= 5f;

            // Drill variety bonus
            var uniqueCategories = session.Drills.Select(d => d.Drill.Category).Distinct().Count();
            quality += Math.Min(10f, uniqueCategories * 2f);

            // Player fatigue penalty (average team energy)
            float avgEnergy = 0f;
            int count = 0;
            foreach (var playerId in participantIds)
            {
                var player = _getPlayer?.Invoke(playerId);
                if (player != null)
                {
                    avgEnergy += player.Energy;
                    count++;
                }
            }
            if (count > 0)
            {
                avgEnergy /= count;
                if (avgEnergy < 70f)
                    quality -= (70f - avgEnergy) / 3f; // Penalty for tired team
            }

            // Random variance (+/- 5)
            quality += (_random.Next(11) - 5);

            return Mathf.Clamp(Mathf.RoundToInt(quality), 20, 100);
        }

        private int CalculatePlayerEngagement(PracticeSession session, List<string> participantIds)
        {
            float engagement = 0f;
            int drillCount = 0;

            foreach (var drill in session.Drills)
            {
                engagement += drill.Drill.Engagement;
                drillCount++;
            }

            if (drillCount > 0)
                engagement /= drillCount;
            else
                engagement = 50f;

            // Adjust for intensity
            engagement += session.Intensity switch
            {
                PracticeIntensity.VeryLight => -15f,
                PracticeIntensity.Light => -5f,
                PracticeIntensity.Normal => 0f,
                PracticeIntensity.High => 10f,
                PracticeIntensity.Intense => 5f, // Too intense can reduce engagement
                _ => 0f
            };

            // Team morale influence
            float avgMorale = 0f;
            int count = 0;
            foreach (var playerId in participantIds)
            {
                var player = _getPlayer?.Invoke(playerId);
                if (player != null)
                {
                    avgMorale += player.Morale;
                    count++;
                }
            }
            if (count > 0)
            {
                avgMorale /= count;
                engagement += (avgMorale - 50f) / 5f;
            }

            return Mathf.Clamp(Mathf.RoundToInt(engagement), 10, 100);
        }

        private float CalculatePlayerMoraleChange(Player player, PracticeSession session, PlayerPracticeGains gains)
        {
            float moraleChange = 0f;

            // High engagement drills boost morale
            float avgEngagement = session.Drills.Count > 0
                ? session.Drills.Average(d => d.Drill.Engagement)
                : 50f;
            moraleChange += (avgEngagement - 50f) / 25f; // +/- 2 from engagement

            // Quality minutes boost morale
            if (gains.QualityMinutes > 30)
                moraleChange += 1f;
            else if (gains.QualityMinutes < 10)
                moraleChange -= 1f;

            // Personality affects practice response
            if (player.Personality != null)
            {
                if (player.Personality.HasTrait(PersonalityTrait.Competitor))
                    moraleChange += 1f; // Competitors love practice
                if (player.Personality.HasTrait(PersonalityTrait.Professional))
                    moraleChange += 0.5f;
            }

            // High intensity can fatigue and frustrate
            if (session.Intensity == PracticeIntensity.Intense && gains.FatigueIncurred > 15f)
                moraleChange -= 1f;

            return Mathf.Clamp(moraleChange, -3f, 3f);
        }

        private void ProcessPlayFamiliarityGains(PracticeSession session, PlayBook playBook, PracticeResults results)
        {
            float baseFamiliarityGain = 2f;

            // Gain from drilling specific plays
            foreach (var playId in session.PlaysToDrill)
            {
                float gain = baseFamiliarityGain * (session.Drills.Any(d => d.Drill.HelpsOpponentPrep) ? 1.5f : 1.0f);
                results.PlayFamiliarityGains[playId] = gain;

                // Actually increase familiarity in playbook
                playBook.IncreaseFamiliarity(playId, gain);
            }

            // Gain from drills that increase play familiarity generally
            foreach (var drill in session.Drills.Where(d => d.Drill.PlayFamiliarityBonus > 0))
            {
                // Apply to all plays in playbook
                float gain = drill.Drill.PlayFamiliarityBonus * (drill.DurationMinutes / 15f);
                playBook.IncreaseAllFamiliarity(gain * 0.5f); // Half effect for general drilling
            }
        }

        private float CalculateOpponentPrepBonus(PracticeSession session, Coach coach, int practiceQuality)
        {
            float bonus = OPPONENT_PREP_BASE_BONUS;

            // Quality influences prep
            bonus *= practiceQuality / 50f;

            // Film session bonus
            if (session.IncludeFilmSession)
                bonus += 3f;

            // Specific play drilling bonus
            bonus += session.PlaysToDrill.Count * 0.5f;

            // Coach preparation skill
            if (coach != null)
            {
                bonus *= (0.8f + coach.GamePlanning / 250f);
            }

            // Drills that help opponent prep
            int prepDrillCount = session.Drills.Count(d => d.Drill.HelpsOpponentPrep);
            bonus += prepDrillCount * 1.5f;

            return Mathf.Clamp(bonus, 0f, 25f);
        }

        private void AddOpponentPrepBonus(string opponentTeamId, float bonus)
        {
            if (!_opponentPrepBonuses.ContainsKey(opponentTeamId))
                _opponentPrepBonuses[opponentTeamId] = 0f;

            _opponentPrepBonuses[opponentTeamId] = Mathf.Min(50f, _opponentPrepBonuses[opponentTeamId] + bonus);
        }

        private float CalculateChemistryChange(PracticeSession session, PracticeResults results)
        {
            float change = 0f;

            // Base chemistry from practice
            if (session.PrimaryFocus == PracticeFocus.TeamBuilding)
                change += CHEMISTRY_CHANGE_PER_PRACTICE * 2f;
            else if (session.PrimaryFocus == PracticeFocus.TeamConcepts)
                change += CHEMISTRY_CHANGE_PER_PRACTICE * 1.5f;
            else
                change += CHEMISTRY_CHANGE_PER_PRACTICE;

            // Team drills boost chemistry
            int teamDrillCount = session.Drills.Count(d => d.Drill.IsTeamDrill);
            change += teamDrillCount * 0.2f;

            // Scrimmages can boost or hurt chemistry
            bool hasScrimmage = session.Drills.Any(d => d.Drill.Category == DrillCategory.Scrimmage);
            if (hasScrimmage)
            {
                change += results.OverallQuality > 60 ? 0.5f : -0.3f;
            }

            // Events affect chemistry
            foreach (var evt in results.Events)
            {
                change += evt.Type switch
                {
                    PracticeEventType.PositiveInteraction => 0.3f,
                    PracticeEventType.ConflictIncident => -0.5f,
                    PracticeEventType.TeamChemistryBoost => 1.0f,
                    PracticeEventType.MentorMoment => 0.4f,
                    _ => 0f
                };
            }

            return Mathf.Clamp(change, -2f, 3f);
        }

        private bool CheckForPracticeInjury(Player player, PracticeSession session)
        {
            // Base injury chance per practice: 0.5%
            float injuryChance = 0.005f;

            // Intensity multiplier
            injuryChance *= session.Intensity switch
            {
                PracticeIntensity.VeryLight => 0.2f,
                PracticeIntensity.Light => 0.5f,
                PracticeIntensity.Normal => 1.0f,
                PracticeIntensity.High => 1.5f,
                PracticeIntensity.Intense => 2.5f,
                _ => 1.0f
            };

            // Drill risk accumulation
            foreach (var drill in session.Drills)
            {
                injuryChance += (drill.Drill.InjuryRisk - 1.0f) * 0.002f * (drill.DurationMinutes / 15f);
            }

            // Player injury proneness
            injuryChance *= (0.5f + player.InjuryProneness / 100f);

            // Fatigue increases injury risk
            if (player.Energy < 50f)
                injuryChance *= 1.5f;
            if (player.Energy < 30f)
                injuryChance *= 2.0f;

            // Durability helps prevent injuries
            injuryChance *= (1.5f - player.Durability / 200f);

            return _random.NextDouble() < injuryChance;
        }

        private PracticeInjury GeneratePracticeInjury(Player player, PracticeSession session)
        {
            // Practice injuries are usually minor
            var severityRoll = _random.NextDouble();
            var severity = severityRoll < 0.7 ? InjurySeverity.Minor :
                           severityRoll < 0.95 ? InjurySeverity.Moderate :
                           InjurySeverity.Major;

            // Common practice injuries
            var injuryTypes = new[] { InjuryType.AnkleSprain, InjuryType.KneeSprain, InjuryType.HamstringStrain,
                                      InjuryType.CalfStrain, InjuryType.BackSpasms };
            var injuryType = injuryTypes[_random.Next(injuryTypes.Length)];

            int daysOut = severity switch
            {
                InjurySeverity.Minor => _random.Next(1, 5),
                InjurySeverity.Moderate => _random.Next(5, 15),
                InjurySeverity.Major => _random.Next(15, 45),
                _ => _random.Next(1, 7)
            };

            return new PracticeInjury
            {
                PlayerId = player.PlayerId,
                InjuryType = injuryType,
                Severity = severity,
                DaysOut = daysOut,
                Description = $"{player.FullName} suffered a {severity.ToString().ToLower()} {injuryType} during practice"
            };
        }

        private void GeneratePracticeEvents(PracticeSession session, List<string> participantIds, PracticeResults results)
        {
            // Chance for various events
            if (_random.NextDouble() < 0.15) // 15% chance for standout performance
            {
                var playerId = participantIds[_random.Next(participantIds.Count)];
                var player = _getPlayer?.Invoke(playerId);
                if (player != null)
                {
                    results.Events.Add(new PracticeEvent
                    {
                        Type = PracticeEventType.StandoutPerformance,
                        PlayerId = playerId,
                        Description = $"{player.FullName} had an excellent practice",
                        Impact = 2f
                    });
                }
            }

            if (_random.NextDouble() < 0.05) // 5% chance for conflict
            {
                if (participantIds.Count >= 2)
                {
                    var player1Id = participantIds[_random.Next(participantIds.Count)];
                    var player2Id = participantIds.Where(id => id != player1Id).ElementAt(_random.Next(participantIds.Count - 1));
                    var player1 = _getPlayer?.Invoke(player1Id);
                    var player2 = _getPlayer?.Invoke(player2Id);

                    if (player1 != null && player2 != null)
                    {
                        results.Events.Add(new PracticeEvent
                        {
                            Type = PracticeEventType.ConflictIncident,
                            PlayerId = player1Id,
                            SecondPlayerId = player2Id,
                            Description = $"Tension between {player1.FullName} and {player2.FullName} during scrimmage",
                            Impact = -1f
                        });
                    }
                }
            }

            // Fire events
            foreach (var evt in results.Events)
            {
                OnPracticeEvent?.Invoke(evt);
            }
        }

        private float GetIntensityFatigueMultiplier(PracticeIntensity intensity)
        {
            return intensity switch
            {
                PracticeIntensity.VeryLight => 0.3f,
                PracticeIntensity.Light => 0.6f,
                PracticeIntensity.Normal => 1.0f,
                PracticeIntensity.High => 1.4f,
                PracticeIntensity.Intense => 1.8f,
                _ => 1.0f
            };
        }

        // ==================== DRILL SCHEDULING HELPERS ====================

        private void AddShootaroundDrills(PracticeSession session)
        {
            session.AddDrill(PracticeDrill.GetDrillById("shoot_spot"), 10);
            session.AddDrill(PracticeDrill.GetDrillById("shoot_free_throw"), 10);
            session.AddDrill(PracticeDrill.GetDrillById("team_halfcourt_sets"), 15);
        }

        private void AddRecoveryDrills(PracticeSession session)
        {
            session.AddDrill(PracticeDrill.GetDrillById("shoot_free_throw"), 15);
            session.AddDrill(PracticeDrill.GetDrillById("film_self"), 20);
        }

        private void AddGamePrepDrills(PracticeSession session, string opponentTeamId)
        {
            session.AddDrill(PracticeDrill.GetDrillById("film_opponent"), 25);
            session.AddDrill(PracticeDrill.GetDrillById("def_pnr_coverage"), 15);
            session.AddDrill(PracticeDrill.GetDrillById("team_halfcourt_sets"), 20);
            session.AddDrill(PracticeDrill.GetDrillById("scrim_situational"), 15);
        }

        private void AddDevelopmentDrills(PracticeSession session, ScheduleContext context)
        {
            session.AddDrill(PracticeDrill.GetDrillById("shoot_spot"), 15);
            session.AddDrill(PracticeDrill.GetDrillById("shoot_off_dribble"), 12);
            session.AddDrill(PracticeDrill.GetDrillById("handle_basic"), 10);
            session.AddDrill(PracticeDrill.GetDrillById("def_closeout"), 12);
            session.AddDrill(PracticeDrill.GetDrillById("def_help_rotation"), 12);
            session.AddDrill(PracticeDrill.GetDrillById("scrim_full"), 20);
        }

        private void AddBalancedDrills(PracticeSession session, ScheduleContext context)
        {
            session.AddDrill(PracticeDrill.GetDrillById("shoot_spot"), 12);
            session.AddDrill(PracticeDrill.GetDrillById("def_pnr_coverage"), 12);
            session.AddDrill(PracticeDrill.GetDrillById("team_halfcourt_sets"), 15);
            session.AddDrill(PracticeDrill.GetDrillById("def_box_out"), 10);
            session.AddDrill(PracticeDrill.GetDrillById("scrim_controlled"), 15);
        }
    }

    /// <summary>
    /// Context for schedule-aware practice planning.
    /// </summary>
    public class ScheduleContext
    {
        public bool IsGameDay;
        public bool IsBackToBackRest;
        public int DaysUntilNextGame;
        public string NextOpponentId;
        public int GamesPlayedThisWeek;
        public bool IsPlayoffs;
    }
}
