using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages player development, aging, and decline.
    /// Handles both offseason development and in-season growth from playing time.
    /// </summary>
    public class PlayerDevelopmentManager
    {
        // Development constants (realistic rates)
        private const float BASE_OFFSEASON_GROWTH = 1.5f;     // Base points per attribute
        private const float BASE_INSEASON_GROWTH = 0.5f;      // Base points per attribute
        private const float MAX_SINGLE_ATTRIBUTE_GROWTH = 5f;  // Max growth per attribute per season
        private const int MINUTES_FOR_FULL_DEVELOPMENT = 2000; // Minutes for 100% development effect

        // Decline constants
        private const float BASE_DECLINE_RATE = 1.0f;          // Base decline per season
        private const float STEEP_DECLINE_MULTIPLIER = 2.0f;   // Multiplier for 35+
        private const float SUDDEN_DROPOFF_CHANCE = 0.05f;     // 5% chance of sudden decline

        private readonly System.Random _rng;
        private PlayerDatabase _playerDatabase;
        private int _currentSeason;

        public PlayerDevelopmentManager()
        {
            _rng = new System.Random();
            _currentSeason = DateTime.Now.Year;
        }

        public PlayerDevelopmentManager(PlayerDatabase playerDatabase, int currentSeason)
        {
            _rng = new System.Random();
            _playerDatabase = playerDatabase;
            _currentSeason = currentSeason;
        }

        public void SetPlayerDatabase(PlayerDatabase db) => _playerDatabase = db;
        public void SetCurrentSeason(int season) => _currentSeason = season;

        // ==================== OFFSEASON DEVELOPMENT ====================

        /// <summary>
        /// Processes offseason development for a player.
        /// Call this once at the end of each offseason.
        /// </summary>
        public DevelopmentResult ProcessOffseasonDevelopment(
            Player player,
            float coachingQuality,           // 0-1, from coaching staff
            float facilityQuality,           // 0-1, from training facility
            string developmentFocus = null,  // Optional focus area
            bool hasMentor = false,          // Veteran mentor bonus
            float mentorChemistry = 0f)      // Chemistry with mentor (0-1)
        {
            var result = new DevelopmentResult
            {
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                Season = _currentSeason,
                DevelopmentType = "Offseason"
            };

            // No development for players past their prime
            if (player.CurrentDevelopmentPhase >= DevelopmentPhase.EarlyDecline)
            {
                result.Summary = $"{player.FullName} is past their developmental years.";
                return result;
            }

            // Calculate development potential
            float developmentMultiplier = CalculateDevelopmentMultiplier(
                player, coachingQuality, facilityQuality, hasMentor, mentorChemistry);

            // Get attributes to develop
            var attributesToDevelop = GetDevelopableAttributes(player, developmentFocus);

            // Process each attribute
            foreach (var attr in attributesToDevelop)
            {
                int currentValue = player.GetAttributeForSimulation(attr);

                // Can't develop past potential
                if (currentValue >= player.HiddenPotential)
                    continue;

                // Calculate growth
                float baseGrowth = BASE_OFFSEASON_GROWTH * developmentMultiplier;

                // Work ethic bonus
                float workEthicBonus = (player.WorkEthic - 50) / 100f;
                baseGrowth *= (1f + workEthicBonus);

                // Coachability bonus
                float coachabilityBonus = (player.Coachability - 50) / 200f;
                baseGrowth *= (1f + coachabilityBonus);

                // Focus area bonus
                if (!string.IsNullOrEmpty(developmentFocus) &&
                    IsAttributeInFocusArea(attr, developmentFocus))
                {
                    baseGrowth *= 1.5f;
                }

                // Randomness
                float variance = (float)(_rng.NextDouble() * 0.4 - 0.2);
                baseGrowth *= (1f + variance);

                // Apply growth (cap to max per season)
                int growth = Mathf.Min(
                    Mathf.RoundToInt(baseGrowth),
                    (int)MAX_SINGLE_ATTRIBUTE_GROWTH,
                    player.HiddenPotential - currentValue  // Can't exceed potential
                );

                if (growth > 0)
                {
                    player.ModifyAttribute(attr, growth);
                    result.AttributeChanges.Add(new AttributeChange
                    {
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = currentValue + growth,
                        Reason = "Offseason Training"
                    });

                    // Log development
                    player.DevelopmentHistory.Add(new DevelopmentLog
                    {
                        Season = _currentSeason,
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = currentValue + growth,
                        Reason = "Offseason Training",
                        Date = DateTime.Now
                    });
                }
            }

            // Generate summary
            int totalGrowth = result.AttributeChanges.Sum(c => c.Change);
            result.OverallChange = totalGrowth;
            result.Summary = GenerateDevelopmentSummary(player, result);

            return result;
        }

        // ==================== IN-SEASON DEVELOPMENT ====================

        /// <summary>
        /// Processes in-season development based on playing time.
        /// Call this at the end of the regular season.
        /// </summary>
        public DevelopmentResult ProcessSeasonDevelopment(
            Player player,
            int minutesPlayed,
            float coachingQuality)
        {
            var result = new DevelopmentResult
            {
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                Season = _currentSeason,
                DevelopmentType = "In-Season"
            };

            // No in-season development for older players
            if (player.CurrentDevelopmentPhase >= DevelopmentPhase.Peak)
            {
                result.Summary = $"{player.FullName} is past in-season development.";
                return result;
            }

            // Calculate playing time factor (0-1)
            float playingTimeFactor = Mathf.Clamp01((float)minutesPlayed / MINUTES_FOR_FULL_DEVELOPMENT);

            // Development multiplier based on age
            float ageMultiplier = player.DevelopmentMultiplier;

            // Get attributes that can develop in-season
            var developableAttrs = GetInSeasonDevelopableAttributes(player);

            foreach (var attr in developableAttrs)
            {
                int currentValue = player.GetAttributeForSimulation(attr);

                if (currentValue >= player.HiddenPotential)
                    continue;

                // Calculate growth
                float baseGrowth = BASE_INSEASON_GROWTH * ageMultiplier * playingTimeFactor * coachingQuality;

                // Randomness
                float variance = (float)(_rng.NextDouble() * 0.3 - 0.1);
                baseGrowth *= (1f + variance);

                int growth = Mathf.Min(
                    Mathf.RoundToInt(baseGrowth),
                    2,  // Max 2 points per attribute in-season
                    player.HiddenPotential - currentValue
                );

                if (growth > 0)
                {
                    player.ModifyAttribute(attr, growth);
                    result.AttributeChanges.Add(new AttributeChange
                    {
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = currentValue + growth,
                        Reason = "In-Season Development"
                    });

                    player.DevelopmentHistory.Add(new DevelopmentLog
                    {
                        Season = _currentSeason,
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = currentValue + growth,
                        Reason = $"In-Season ({minutesPlayed} mins)",
                        Date = DateTime.Now
                    });
                }
            }

            int totalGrowth = result.AttributeChanges.Sum(c => c.Change);
            result.OverallChange = totalGrowth;
            result.Summary = $"{player.FullName} gained {totalGrowth} attribute points from playing time.";

            return result;
        }

        // ==================== AGING & DECLINE ====================

        /// <summary>
        /// Applies aging effects and potential decline.
        /// Call this once per season (typically at season start or end).
        /// </summary>
        public DevelopmentResult ApplyAgingEffects(Player player)
        {
            var result = new DevelopmentResult
            {
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                Season = _currentSeason,
                DevelopmentType = "Aging"
            };

            // Age the player
            player.Age++;

            // No decline for young players
            if (player.CurrentDevelopmentPhase < DevelopmentPhase.EarlyDecline)
            {
                result.Summary = $"{player.FullName} is now {player.Age} years old. No decline expected.";
                return result;
            }

            // Calculate decline intensity
            float declineMultiplier = player.DeclineMultiplier;

            // Check for sudden drop-off
            bool suddenDropoff = false;
            if (player.Age >= 33 && _rng.NextDouble() < SUDDEN_DROPOFF_CHANCE * (player.Age - 32))
            {
                suddenDropoff = true;
                declineMultiplier *= 3f;
                result.Notes = "SUDDEN DECLINE DETECTED - Major drop-off this season";
            }

            // Apply decline to different attribute categories at different rates
            ApplyPhysicalDecline(player, result, declineMultiplier);
            ApplySkillDecline(player, result, declineMultiplier * 0.5f);

            // Mental attributes may not decline
            if (_rng.NextDouble() < 0.3)  // 30% chance mental attributes stay same
            {
                ApplyMentalDecline(player, result, declineMultiplier * 0.2f);
            }

            int totalDecline = result.AttributeChanges.Sum(c => c.Change);
            result.OverallChange = totalDecline;
            result.Summary = GenerateDeclineSummary(player, result, suddenDropoff);

            return result;
        }

        private void ApplyPhysicalDecline(Player player, DevelopmentResult result, float multiplier)
        {
            var physicalAttrs = PlayerAttributeHelper.GetFastDeclineAttributes();

            foreach (var attr in physicalAttrs)
            {
                int currentValue = player.GetAttributeForSimulation(attr);

                // Calculate decline
                float baseDec = BASE_DECLINE_RATE * multiplier;

                // Steeper decline for 35+
                if (player.Age >= 35)
                {
                    baseDec *= STEEP_DECLINE_MULTIPLIER;
                }

                // Randomness
                float variance = (float)(_rng.NextDouble() * 0.5);
                baseDec *= (1f + variance);

                int decline = Mathf.RoundToInt(baseDec);

                if (decline > 0)
                {
                    int newValue = Math.Max(30, currentValue - decline);  // Floor at 30
                    player.SetAttribute(attr, newValue);

                    result.AttributeChanges.Add(new AttributeChange
                    {
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = newValue,
                        Reason = "Age-Related Decline (Physical)"
                    });

                    player.DevelopmentHistory.Add(new DevelopmentLog
                    {
                        Season = _currentSeason,
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = newValue,
                        Reason = "Age Decline",
                        Date = DateTime.Now
                    });
                }
            }
        }

        private void ApplySkillDecline(Player player, DevelopmentResult result, float multiplier)
        {
            var skillAttrs = PlayerAttributeHelper.GetSlowDeclineAttributes();

            foreach (var attr in skillAttrs)
            {
                // Skills decline slower - roll for each
                if (_rng.NextDouble() > 0.5)
                    continue;

                int currentValue = player.GetAttributeForSimulation(attr);

                float baseDec = BASE_DECLINE_RATE * multiplier * 0.5f;
                int decline = Mathf.RoundToInt(baseDec);

                if (decline > 0)
                {
                    int newValue = Math.Max(40, currentValue - decline);  // Floor at 40
                    player.SetAttribute(attr, newValue);

                    result.AttributeChanges.Add(new AttributeChange
                    {
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = newValue,
                        Reason = "Age-Related Decline (Skills)"
                    });

                    player.DevelopmentHistory.Add(new DevelopmentLog
                    {
                        Season = _currentSeason,
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = newValue,
                        Reason = "Age Decline (Skill)",
                        Date = DateTime.Now
                    });
                }
            }
        }

        private void ApplyMentalDecline(Player player, DevelopmentResult result, float multiplier)
        {
            // Mental decline is rare
            var mentalAttrs = new[]
            {
                PlayerAttribute.Consistency,
                PlayerAttribute.Clutch
            };

            foreach (var attr in mentalAttrs)
            {
                if (_rng.NextDouble() > 0.3)
                    continue;

                int currentValue = player.GetAttributeForSimulation(attr);
                int decline = Mathf.RoundToInt(multiplier);

                if (decline > 0)
                {
                    int newValue = Math.Max(50, currentValue - decline);
                    player.SetAttribute(attr, newValue);

                    result.AttributeChanges.Add(new AttributeChange
                    {
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = newValue,
                        Reason = "Age-Related Decline (Mental)"
                    });
                }
            }
        }

        // ==================== DEVELOPMENT FEEDBACK ====================

        /// <summary>
        /// Generates development feedback text for coaches to report to player.
        /// Quality of feedback depends on coach rating.
        /// </summary>
        public string GetDevelopmentFeedback(Player player, float coachQuality)
        {
            var feedback = new List<string>();

            // Higher coach quality = more accurate and detailed feedback
            bool isAccurate = _rng.NextDouble() < coachQuality;
            bool isDetailed = coachQuality >= 0.7f;

            // Recent development history
            var recentChanges = player.DevelopmentHistory
                .Where(d => d.Season == _currentSeason || d.Season == _currentSeason - 1)
                .OrderByDescending(d => d.Date)
                .Take(5)
                .ToList();

            if (recentChanges.Count == 0)
            {
                return $"No significant development to report for {player.FullName}.";
            }

            // Improvements
            var improvements = recentChanges.Where(c => c.IsImprovement).ToList();
            if (improvements.Any())
            {
                var topImprovement = improvements.OrderByDescending(c => c.Change).First();
                string attrName = GetAttributeDisplayName(topImprovement.Attribute);

                if (isAccurate)
                {
                    feedback.Add($"{player.FullName} has shown notable improvement in {attrName.ToLower()}.");
                }
                else
                {
                    // Inaccurate feedback might highlight wrong area
                    feedback.Add($"{player.FullName} appears to be developing well overall.");
                }

                if (isDetailed && improvements.Count > 1)
                {
                    var secondImprovement = improvements.OrderByDescending(c => c.Change).Skip(1).First();
                    feedback.Add($"Also seeing progress in {GetAttributeDisplayName(secondImprovement.Attribute).ToLower()}.");
                }
            }

            // Declines
            var declines = recentChanges.Where(c => !c.IsImprovement).ToList();
            if (declines.Any() && isDetailed)
            {
                var topDecline = declines.OrderBy(c => c.Change).First();
                feedback.Add($"Some concern about declining {GetAttributeDisplayName(topDecline.Attribute).ToLower()}.");
            }

            // Growth room assessment
            if (isDetailed && player.GrowthRoom > 0)
            {
                if (player.GrowthRoom >= 15)
                    feedback.Add("Still has significant room for growth.");
                else if (player.GrowthRoom >= 5)
                    feedback.Add("Has some upside remaining.");
                else
                    feedback.Add("Approaching his ceiling.");
            }

            return string.Join(" ", feedback);
        }

        // ==================== HELPERS ====================

        private float CalculateDevelopmentMultiplier(
            Player player,
            float coachingQuality,
            float facilityQuality,
            bool hasMentor,
            float mentorChemistry)
        {
            // Base multiplier from age
            float multiplier = player.DevelopmentMultiplier;

            // Coaching bonus (0-40%)
            multiplier *= (1f + coachingQuality * 0.4f);

            // Facility bonus (0-20%)
            multiplier *= (1f + facilityQuality * 0.2f);

            // Mentor bonus (0-30%)
            if (hasMentor)
            {
                float mentorBonus = 0.1f + (mentorChemistry * 0.2f);
                multiplier *= (1f + mentorBonus);
            }

            return multiplier;
        }

        private List<PlayerAttribute> GetDevelopableAttributes(Player player, string focus)
        {
            // Get all attributes except personality
            var allAttrs = ((PlayerAttribute[])Enum.GetValues(typeof(PlayerAttribute)))
                .Where(a => a != PlayerAttribute.Ego &&
                           a != PlayerAttribute.Leadership &&
                           a != PlayerAttribute.Composure &&
                           a != PlayerAttribute.Aggression)
                .ToList();

            // If there's a focus, prioritize those attributes
            if (!string.IsNullOrEmpty(focus))
            {
                var focusAttrs = GetAttributesForFocus(focus);
                // Put focus attributes first
                allAttrs = focusAttrs.Concat(allAttrs.Except(focusAttrs)).ToList();
            }

            // Limit to top 10 attributes to develop
            return allAttrs.Take(10).ToList();
        }

        private List<PlayerAttribute> GetInSeasonDevelopableAttributes(Player player)
        {
            // In-season, only certain attributes improve from game experience
            return new List<PlayerAttribute>
            {
                PlayerAttribute.BasketballIQ,
                PlayerAttribute.OffensiveIQ,
                PlayerAttribute.DefensiveIQ,
                PlayerAttribute.Clutch,
                PlayerAttribute.Consistency,
                PlayerAttribute.Composure
            };
        }

        private List<PlayerAttribute> GetAttributesForFocus(string focus)
        {
            return focus.ToLower() switch
            {
                "shooting" => new List<PlayerAttribute>
                {
                    PlayerAttribute.Shot_Three,
                    PlayerAttribute.Shot_MidRange,
                    PlayerAttribute.FreeThrow
                },
                "defense" => new List<PlayerAttribute>
                {
                    PlayerAttribute.Defense_Perimeter,
                    PlayerAttribute.Defense_Interior,
                    PlayerAttribute.DefensiveIQ
                },
                "playmaking" => new List<PlayerAttribute>
                {
                    PlayerAttribute.Passing,
                    PlayerAttribute.BallHandling,
                    PlayerAttribute.OffensiveIQ
                },
                "finishing" => new List<PlayerAttribute>
                {
                    PlayerAttribute.Finishing_Rim,
                    PlayerAttribute.Shot_Close,
                    PlayerAttribute.Finishing_PostMoves
                },
                "physical" => new List<PlayerAttribute>
                {
                    PlayerAttribute.Strength,
                    PlayerAttribute.Stamina,
                    PlayerAttribute.Speed
                },
                "rebounding" => new List<PlayerAttribute>
                {
                    PlayerAttribute.DefensiveRebound,
                    PlayerAttribute.Strength,
                    PlayerAttribute.Vertical
                },
                _ => new List<PlayerAttribute>()
            };
        }

        private bool IsAttributeInFocusArea(PlayerAttribute attr, string focus)
        {
            var focusAttrs = GetAttributesForFocus(focus);
            return focusAttrs.Contains(attr);
        }

        private string GetAttributeDisplayName(PlayerAttribute attr)
        {
            return attr switch
            {
                PlayerAttribute.Shot_Three => "Three-Point Shooting",
                PlayerAttribute.Shot_MidRange => "Mid-Range Game",
                PlayerAttribute.Finishing_Rim => "Finishing at the Rim",
                PlayerAttribute.Defense_Perimeter => "Perimeter Defense",
                PlayerAttribute.Defense_Interior => "Interior Defense",
                PlayerAttribute.BallHandling => "Ball Handling",
                PlayerAttribute.OffensiveIQ => "Offensive Awareness",
                PlayerAttribute.DefensiveIQ => "Defensive Awareness",
                PlayerAttribute.BasketballIQ => "Basketball IQ",
                _ => attr.ToString().Replace("_", " ")
            };
        }

        private string GenerateDevelopmentSummary(Player player, DevelopmentResult result)
        {
            if (result.AttributeChanges.Count == 0)
            {
                return $"{player.FullName} showed minimal development this offseason.";
            }

            int totalGrowth = result.OverallChange;
            string growthDesc = totalGrowth switch
            {
                >= 10 => "significant",
                >= 5 => "solid",
                >= 2 => "modest",
                _ => "minimal"
            };

            var topChange = result.AttributeChanges.OrderByDescending(c => c.Change).First();
            string topAttr = GetAttributeDisplayName(topChange.Attribute).ToLower();

            return $"{player.FullName} showed {growthDesc} development this offseason, " +
                   $"particularly in {topAttr} (+{topChange.Change}).";
        }

        private string GenerateDeclineSummary(Player player, DevelopmentResult result, bool suddenDropoff)
        {
            if (result.AttributeChanges.Count == 0)
            {
                return $"{player.FullName} shows no signs of decline at age {player.Age}.";
            }

            int totalDecline = Math.Abs(result.OverallChange);

            if (suddenDropoff)
            {
                return $"ALERT: {player.FullName} has experienced a significant drop-off. " +
                       $"Lost {totalDecline} total attribute points this season.";
            }

            string declineDesc = totalDecline switch
            {
                >= 8 => "noticeable",
                >= 4 => "moderate",
                _ => "minor"
            };

            var topDecline = result.AttributeChanges.OrderBy(c => c.Change).First();
            string topAttr = GetAttributeDisplayName(topDecline.Attribute).ToLower();

            return $"{player.FullName} is showing {declineDesc} age-related decline at {player.Age}. " +
                   $"Most affected: {topAttr} ({topDecline.Change}).";
        }

        // ==================== BATCH PROCESSING ====================

        /// <summary>
        /// Process offseason development for all players on a team.
        /// </summary>
        public List<DevelopmentResult> ProcessTeamOffseasonDevelopment(
            string teamId,
            List<Coach> coachingStaff,
            int facilityRating,  // 1-5 stars
            Dictionary<string, string> playerDevelopmentFocus = null,
            Dictionary<string, string> mentorAssignments = null)
        {
            var results = new List<DevelopmentResult>();

            if (_playerDatabase == null)
            {
                Debug.LogError("[PlayerDevelopmentManager] PlayerDatabase not set");
                return results;
            }

            // Calculate coaching quality from staff
            float coachingQuality = CalculateCoachingQuality(coachingStaff);
            float facilityQuality = facilityRating / 5f;

            // Get all team players
            var players = _playerDatabase.GetPlayersByTeam(teamId);

            foreach (var player in players)
            {
                string focus = playerDevelopmentFocus?.GetValueOrDefault(player.PlayerId);

                // Check for mentor
                bool hasMentor = mentorAssignments?.ContainsKey(player.PlayerId) ?? false;
                float mentorChemistry = 0.5f;  // Default chemistry

                var result = ProcessOffseasonDevelopment(
                    player, coachingQuality, facilityQuality,
                    focus, hasMentor, mentorChemistry);

                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Process offseason development with detailed instructions for all players.
        /// Uses the full DevelopmentInstruction system with trainers and facility bonuses.
        /// </summary>
        public List<DevelopmentResult> ProcessTeamOffseasonWithInstructions(
            string teamId,
            List<Coach> coachingStaff,
            TrainingFacility facility,
            TeamDevelopmentPlan developmentPlan)
        {
            var results = new List<DevelopmentResult>();

            if (_playerDatabase == null)
            {
                Debug.LogError("[PlayerDevelopmentManager] PlayerDatabase not set");
                return results;
            }

            var players = _playerDatabase.GetPlayersByTeam(teamId);

            foreach (var player in players)
            {
                var instruction = developmentPlan?.GetInstruction(player.PlayerId)
                    ?? DevelopmentInstruction.CreateDefault(player.PlayerId, player.FullName, _currentSeason);

                var result = ProcessOffseasonWithInstruction(player, coachingStaff, facility, instruction);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Process offseason development for a single player with detailed instructions.
        /// </summary>
        public DevelopmentResult ProcessOffseasonWithInstruction(
            Player player,
            List<Coach> coachingStaff,
            TrainingFacility facility,
            DevelopmentInstruction instruction)
        {
            var result = new DevelopmentResult
            {
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                Season = _currentSeason,
                DevelopmentType = "Offseason (Structured)"
            };

            // No development for players past their prime
            if (player.CurrentDevelopmentPhase >= DevelopmentPhase.EarlyDecline)
            {
                result.Summary = $"{player.FullName} is past their developmental years.";
                return result;
            }

            // Calculate base multipliers
            float coachingQuality = CalculateCoachingQuality(coachingStaff);
            float facilityBonus = facility?.DevelopmentBonus ?? 0f;

            // Get attributes from primary focus
            var primaryFocusAttrs = DevelopmentInstruction.GetAttributesForFocus(instruction.PrimaryFocus);
            var secondaryFocusAttrs = DevelopmentInstruction.GetAttributesForFocus(instruction.SecondaryFocus);
            var targetAttrs = instruction.TargetAttributes;

            // Combine all focus attributes (with priority order)
            var allFocusAttrs = targetAttrs
                .Concat(primaryFocusAttrs)
                .Concat(secondaryFocusAttrs)
                .Distinct()
                .Take(12)  // Process up to 12 attributes
                .ToList();

            // If balanced focus, use general developable attributes
            if (allFocusAttrs.Count == 0)
            {
                allFocusAttrs = GetDevelopableAttributes(player, null);
            }

            // Process each attribute
            foreach (var attr in allFocusAttrs)
            {
                int currentValue = player.GetAttributeForSimulation(attr);

                // Can't develop past potential
                if (currentValue >= player.HiddenPotential)
                    continue;

                // Calculate growth
                float baseGrowth = BASE_OFFSEASON_GROWTH * player.DevelopmentMultiplier;

                // Coaching bonus
                baseGrowth *= (1f + coachingQuality * 0.4f);

                // Facility bonus (general + category-specific)
                var category = PlayerAttributeHelper.GetCategory(attr);
                float categoryFacilityBonus = facility?.GetTotalDevelopmentBonus(category) ?? 0f;
                baseGrowth *= (1f + facilityBonus + categoryFacilityBonus);

                // Instruction bonus
                float instructionBonus = instruction.GetAttributeBonus(attr);
                baseGrowth *= (1f + instructionBonus);

                // Work ethic & coachability
                float workEthicBonus = (player.WorkEthic - 50) / 100f;
                float coachabilityBonus = (player.Coachability - 50) / 200f;
                baseGrowth *= (1f + workEthicBonus + coachabilityBonus);

                // Mentor bonus
                if (instruction.MentorBonus > 0)
                {
                    baseGrowth *= (1f + instruction.MentorBonus);
                }

                // Special programs bonus
                foreach (var program in instruction.SpecialPrograms)
                {
                    if (program.AffectedAttributes.Contains(attr))
                    {
                        baseGrowth *= (1f + program.DevelopmentBonus);
                    }
                }

                // Intensity modifier (already in instruction bonus, but affects ceiling)
                if (instruction.Intensity == TrainingIntensity.Extreme)
                {
                    baseGrowth = Mathf.Min(baseGrowth, MAX_SINGLE_ATTRIBUTE_GROWTH + 2);
                }

                // Randomness
                float variance = (float)(_rng.NextDouble() * 0.4 - 0.2);
                baseGrowth *= (1f + variance);

                // Apply growth
                int growth = Mathf.Min(
                    Mathf.RoundToInt(baseGrowth),
                    (int)MAX_SINGLE_ATTRIBUTE_GROWTH,
                    player.HiddenPotential - currentValue
                );

                if (growth > 0)
                {
                    player.ModifyAttribute(attr, growth);
                    result.AttributeChanges.Add(new AttributeChange
                    {
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = currentValue + growth,
                        Reason = $"Offseason Training ({instruction.PrimaryFocus})"
                    });

                    player.DevelopmentHistory.Add(new DevelopmentLog
                    {
                        Season = _currentSeason,
                        Attribute = attr,
                        PreviousValue = currentValue,
                        NewValue = currentValue + growth,
                        Reason = $"Focus: {instruction.PrimaryFocus}",
                        Date = DateTime.Now
                    });
                }
            }

            // Check for training injury (higher intensity = higher risk)
            if (instruction.Intensity >= TrainingIntensity.Intense)
            {
                float injuryChance = instruction.InjuryRiskModifier * (player.InjuryProneness / 100f) * 0.1f;
                if (_rng.NextDouble() < injuryChance)
                {
                    result.Notes = "TRAINING INJURY - Player suffered a minor setback during offseason training.";
                    player.InjuryHistoryCount++;
                    // Could add actual injury here
                }
            }

            // Generate summary
            int totalGrowth = result.AttributeChanges.Sum(c => c.Change);
            result.OverallChange = totalGrowth;
            result.Summary = GenerateDetailedDevelopmentSummary(player, result, instruction);

            return result;
        }

        private string GenerateDetailedDevelopmentSummary(
            Player player,
            DevelopmentResult result,
            DevelopmentInstruction instruction)
        {
            if (result.AttributeChanges.Count == 0)
            {
                return $"{player.FullName} showed minimal development this offseason despite focus on {instruction.PrimaryFocus}.";
            }

            int totalGrowth = result.OverallChange;
            string growthDesc = totalGrowth switch
            {
                >= 15 => "exceptional",
                >= 10 => "significant",
                >= 5 => "solid",
                >= 2 => "modest",
                _ => "minimal"
            };

            var topChange = result.AttributeChanges.OrderByDescending(c => c.Change).First();
            string topAttr = GetAttributeDisplayName(topChange.Attribute).ToLower();

            string summary = $"{player.FullName} showed {growthDesc} development this offseason ";
            summary += $"with focus on {instruction.PrimaryFocus}. ";
            summary += $"Biggest improvement in {topAttr} (+{topChange.Change}).";

            if (!instruction.PlayerAgreed)
            {
                summary += " Development was hindered by player's disagreement with training plan.";
            }

            if (instruction.SpecialPrograms.Count > 0)
            {
                summary += $" Completed {instruction.SpecialPrograms.Count} special program(s).";
            }

            return summary;
        }

        /// <summary>
        /// Process aging for all players in the league.
        /// Call this at the start of each new season.
        /// </summary>
        public List<DevelopmentResult> ProcessLeagueAging()
        {
            var results = new List<DevelopmentResult>();

            if (_playerDatabase == null)
            {
                Debug.LogError("[PlayerDevelopmentManager] PlayerDatabase not set");
                return results;
            }

            var allPlayers = _playerDatabase.GetAllPlayers();

            foreach (var player in allPlayers)
            {
                var result = ApplyAgingEffects(player);
                if (result.AttributeChanges.Count > 0 || player.Age >= 30)
                {
                    results.Add(result);
                }
            }

            return results;
        }

        private float CalculateCoachingQuality(List<Coach> staff)
        {
            if (staff == null || staff.Count == 0)
                return 0.5f;

            // Weight by position relevance
            float totalQuality = 0f;
            float totalWeight = 0f;

            foreach (var coach in staff)
            {
                float weight = coach.Position switch
                {
                    CoachPosition.PlayerDevelopment => 3f,
                    CoachPosition.ShootingCoach => 2f,
                    CoachPosition.BigManCoach => 1.5f,
                    CoachPosition.GuardSkillsCoach => 1.5f,
                    CoachPosition.HeadCoach => 1f,
                    _ => 0.5f
                };

                totalQuality += coach.PlayerDevelopment / 100f * weight;
                totalWeight += weight;
            }

            return Mathf.Clamp01(totalQuality / totalWeight);
        }
    }

    // ==================== RESULT CLASSES ====================

    /// <summary>
    /// Result of a development or aging process.
    /// </summary>
    public class DevelopmentResult
    {
        public string PlayerId;
        public string PlayerName;
        public int Season;
        public string DevelopmentType;  // "Offseason", "In-Season", "Aging"
        public List<AttributeChange> AttributeChanges = new List<AttributeChange>();
        public int OverallChange;       // Total points gained/lost
        public string Summary;
        public string Notes;

        public bool HasChanges => AttributeChanges.Count > 0;
        public bool IsPositive => OverallChange > 0;
    }

    /// <summary>
    /// Single attribute change record.
    /// </summary>
    public class AttributeChange
    {
        public PlayerAttribute Attribute;
        public int PreviousValue;
        public int NewValue;
        public string Reason;

        public int Change => NewValue - PreviousValue;
    }
}
