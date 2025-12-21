using System;
using System.Collections.Generic;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Represents a player's behavioral tendencies - some innate (hard to change), some coachable.
    /// Tendencies affect how players execute within the tactical system and can create
    /// synergies or conflicts with coaching instructions.
    /// </summary>
    [Serializable]
    public class PlayerTendencies
    {
        // ============================================
        // INNATE TENDENCIES (Hard to change, -100 to +100 scale)
        // These represent core personality/instinct traits
        // ============================================

        /// <summary>
        /// Shot selection tendency.
        /// -100 = Quick Trigger (shoots immediately when open, takes contested shots)
        /// +100 = Patient Shooter (waits for perfect looks, passes up decent shots)
        /// </summary>
        public int ShotSelection { get; set; }

        /// <summary>
        /// Defensive gambling tendency.
        /// -100 = Conservative (stays in position, rarely gambles for steals)
        /// +100 = Aggressive (goes for steals/blocks, sometimes out of position)
        /// </summary>
        public int DefensiveGambling { get; set; }

        /// <summary>
        /// Clutch behavior tendency.
        /// -100 = Shrinks Under Pressure (worse performance in clutch moments)
        /// +100 = Rises to Occasion (elevated play in big moments)
        /// </summary>
        public int ClutchBehavior { get; set; }

        /// <summary>
        /// Ball movement tendency.
        /// -100 = Ball Stopper (holds ball, iso-heavy, low assist rates)
        /// +100 = Willing Passer (moves ball quickly, high assist potential)
        /// </summary>
        public int BallMovement { get; set; }

        /// <summary>
        /// Effort consistency tendency.
        /// -100 = Coasts Sometimes (inconsistent effort, conserves energy)
        /// +100 = Always Hustles (maximum effort every play, may tire faster)
        /// </summary>
        public int EffortConsistency { get; set; }

        /// <summary>
        /// Risk tolerance in decision making.
        /// -100 = Risk Averse (safe plays, avoids turnovers)
        /// +100 = Risk Taker (flashy passes, aggressive drives)
        /// </summary>
        public int RiskTolerance { get; set; }

        /// <summary>
        /// Leadership style on court.
        /// -100 = Silent (leads by example only)
        /// +100 = Vocal (constantly communicating, directing teammates)
        /// </summary>
        public int LeadershipStyle { get; set; }

        /// <summary>
        /// Reaction to adversity.
        /// -100 = Gets Frustrated (negative body language, less engaged)
        /// +100 = Stays Composed (maintains focus regardless of circumstances)
        /// </summary>
        public int AdversityResponse { get; set; }

        // ============================================
        // COACHABLE TENDENCIES (Can be trained, 0-100 scale)
        // These represent learned behaviors that can be developed
        // ============================================

        /// <summary>
        /// Screen setting quality (0-100).
        /// Low = Slips early, soft contact
        /// High = Sets hard screens, holds position
        /// </summary>
        public int ScreenSetting { get; set; }

        /// <summary>
        /// Help defense tendency (0-100).
        /// Low = Stays home on assignment
        /// High = Over-helps, leaves shooter open
        /// Middle values = Balanced help rotations
        /// </summary>
        public int HelpDefenseAggression { get; set; }

        /// <summary>
        /// Transition defense effort (0-100).
        /// Low = Gambles for steals, slow getting back
        /// High = Sprints back every time
        /// </summary>
        public int TransitionDefense { get; set; }

        /// <summary>
        /// Post defense positioning (0-100).
        /// Low = Plays behind, gives up position
        /// High = Fronts post, aggressive denial
        /// </summary>
        public int PostDefenseAggression { get; set; }

        /// <summary>
        /// Closeout technique (0-100).
        /// Low = Fly-by closeouts, gets blown by
        /// High = Controlled closeouts, contests without fouling
        /// </summary>
        public int CloseoutControl { get; set; }

        /// <summary>
        /// Off-ball movement quality (0-100).
        /// Low = Stands around, easy to guard
        /// High = Constant motion, finding openings
        /// </summary>
        public int OffBallMovement { get; set; }

        /// <summary>
        /// Box out discipline (0-100).
        /// Low = Ball watches, poor positioning
        /// High = Consistent contact, secures position
        /// </summary>
        public int BoxOutDiscipline { get; set; }

        /// <summary>
        /// Pick and roll coverage execution (0-100).
        /// Low = Gets confused, poor execution
        /// High = Clean execution of assigned coverage
        /// </summary>
        public int PnRCoverageExecution { get; set; }

        /// <summary>
        /// Defensive communication (0-100).
        /// Low = Silent, doesn't call out screens
        /// High = Vocal, alerts teammates to actions
        /// </summary>
        public int DefensiveCommunication { get; set; }

        /// <summary>
        /// Shot discipline - following shot selection rules (0-100).
        /// Low = Takes shots outside role
        /// High = Stays within defined shot profile
        /// </summary>
        public int ShotDiscipline { get; set; }

        // ============================================
        // TRAINING PROGRESS (for coachable tendencies)
        // ============================================

        /// <summary>
        /// Progress towards improving each coachable tendency.
        /// Key = tendency name, Value = progress (0-100)
        /// </summary>
        public Dictionary<string, float> TrainingProgress { get; set; } = new Dictionary<string, float>();

        /// <summary>
        /// Currently assigned focus areas for training.
        /// Maximum of 2-3 focus areas at once for best results.
        /// </summary>
        public List<string> CurrentTrainingFocus { get; set; } = new List<string>();

        // ============================================
        // METHODS
        // ============================================

        /// <summary>
        /// Creates a new PlayerTendencies with randomized values based on player archetype.
        /// </summary>
        public static PlayerTendencies GenerateForArchetype(PlayerArchetype archetype, int overallRating)
        {
            var tendencies = new PlayerTendencies();
            var random = new Random();

            // Base randomization for innate tendencies (-50 to +50 range)
            tendencies.ShotSelection = random.Next(-50, 51);
            tendencies.DefensiveGambling = random.Next(-50, 51);
            tendencies.ClutchBehavior = random.Next(-50, 51);
            tendencies.BallMovement = random.Next(-50, 51);
            tendencies.EffortConsistency = random.Next(-50, 51);
            tendencies.RiskTolerance = random.Next(-50, 51);
            tendencies.LeadershipStyle = random.Next(-50, 51);
            tendencies.AdversityResponse = random.Next(-50, 51);

            // Apply archetype modifiers
            ApplyArchetypeModifiers(tendencies, archetype);

            // Higher rated players tend to have better mental tendencies
            int mentalBonus = (overallRating - 70) / 3; // -10 to +10 roughly
            tendencies.ClutchBehavior = Clamp(tendencies.ClutchBehavior + mentalBonus, -100, 100);
            tendencies.EffortConsistency = Clamp(tendencies.EffortConsistency + mentalBonus, -100, 100);
            tendencies.AdversityResponse = Clamp(tendencies.AdversityResponse + mentalBonus, -100, 100);

            // Base coachable tendencies (30-70 range, improvable)
            tendencies.ScreenSetting = random.Next(30, 71);
            tendencies.HelpDefenseAggression = random.Next(30, 71);
            tendencies.TransitionDefense = random.Next(30, 71);
            tendencies.PostDefenseAggression = random.Next(30, 71);
            tendencies.CloseoutControl = random.Next(30, 71);
            tendencies.OffBallMovement = random.Next(30, 71);
            tendencies.BoxOutDiscipline = random.Next(30, 71);
            tendencies.PnRCoverageExecution = random.Next(30, 71);
            tendencies.DefensiveCommunication = random.Next(30, 71);
            tendencies.ShotDiscipline = random.Next(30, 71);

            // Apply archetype modifiers to coachable tendencies
            ApplyArchetypeCoachableModifiers(tendencies, archetype);

            return tendencies;
        }

        private static void ApplyArchetypeModifiers(PlayerTendencies t, PlayerArchetype archetype)
        {
            switch (archetype)
            {
                case PlayerArchetype.Playmaker:
                    t.BallMovement += 30;  // More willing to pass
                    t.RiskTolerance += 20; // Takes some risks with passes
                    break;

                case PlayerArchetype.Scorer:
                    t.ShotSelection -= 20; // More aggressive shot selection
                    t.BallMovement -= 20;  // More iso-oriented
                    break;

                case PlayerArchetype.Defender:
                    t.DefensiveGambling -= 10; // Slightly more conservative
                    t.EffortConsistency += 20; // High effort
                    break;

                case PlayerArchetype.Stretch:
                    t.ShotSelection += 20; // Patient for good shots
                    break;

                case PlayerArchetype.Athletic:
                    t.RiskTolerance += 15; // Uses athleticism aggressively
                    t.DefensiveGambling += 15; // Uses athleticism on D
                    break;

                case PlayerArchetype.PostPlayer:
                    t.BallMovement -= 10; // Works in post
                    t.ShotSelection += 10; // Patient for post position
                    break;

                case PlayerArchetype.ThreeAndD:
                    t.ShotSelection += 25; // Only takes good threes
                    t.DefensiveGambling -= 15; // Disciplined defense
                    break;

                case PlayerArchetype.Facilitator:
                    t.BallMovement += 40; // Elite passing
                    t.ShotSelection += 20; // Pass-first
                    break;
            }

            // Clamp all values
            t.ShotSelection = Clamp(t.ShotSelection, -100, 100);
            t.DefensiveGambling = Clamp(t.DefensiveGambling, -100, 100);
            t.ClutchBehavior = Clamp(t.ClutchBehavior, -100, 100);
            t.BallMovement = Clamp(t.BallMovement, -100, 100);
            t.EffortConsistency = Clamp(t.EffortConsistency, -100, 100);
            t.RiskTolerance = Clamp(t.RiskTolerance, -100, 100);
            t.LeadershipStyle = Clamp(t.LeadershipStyle, -100, 100);
            t.AdversityResponse = Clamp(t.AdversityResponse, -100, 100);
        }

        private static void ApplyArchetypeCoachableModifiers(PlayerTendencies t, PlayerArchetype archetype)
        {
            switch (archetype)
            {
                case PlayerArchetype.Defender:
                    t.CloseoutControl += 15;
                    t.HelpDefenseAggression += 10;
                    t.DefensiveCommunication += 15;
                    t.BoxOutDiscipline += 10;
                    break;

                case PlayerArchetype.PostPlayer:
                    t.ScreenSetting += 15;
                    t.BoxOutDiscipline += 20;
                    t.PostDefenseAggression += 15;
                    break;

                case PlayerArchetype.Playmaker:
                case PlayerArchetype.Facilitator:
                    t.OffBallMovement += 10;
                    t.PnRCoverageExecution += 10;
                    break;

                case PlayerArchetype.ThreeAndD:
                    t.ShotDiscipline += 20;
                    t.CloseoutControl += 10;
                    t.TransitionDefense += 10;
                    break;

                case PlayerArchetype.Scorer:
                    t.OffBallMovement += 15;
                    t.ShotDiscipline -= 10; // Takes more shots
                    break;
            }

            // Clamp coachable tendencies to 0-100
            t.ScreenSetting = Clamp(t.ScreenSetting, 0, 100);
            t.HelpDefenseAggression = Clamp(t.HelpDefenseAggression, 0, 100);
            t.TransitionDefense = Clamp(t.TransitionDefense, 0, 100);
            t.PostDefenseAggression = Clamp(t.PostDefenseAggression, 0, 100);
            t.CloseoutControl = Clamp(t.CloseoutControl, 0, 100);
            t.OffBallMovement = Clamp(t.OffBallMovement, 0, 100);
            t.BoxOutDiscipline = Clamp(t.BoxOutDiscipline, 0, 100);
            t.PnRCoverageExecution = Clamp(t.PnRCoverageExecution, 0, 100);
            t.DefensiveCommunication = Clamp(t.DefensiveCommunication, 0, 100);
            t.ShotDiscipline = Clamp(t.ShotDiscipline, 0, 100);
        }

        /// <summary>
        /// Calculates how well this player's tendencies fit with a given instruction.
        /// Returns a modifier from 0.7 (bad conflict) to 1.3 (great synergy).
        /// </summary>
        public float GetInstructionCompatibility(string instructionType, int instructionValue)
        {
            float compatibility = 1.0f;

            // Match instruction types with relevant tendencies
            switch (instructionType.ToLower())
            {
                case "defensiveaggressiveness":
                    // If instruction wants aggressive D but player is conservative (or vice versa)
                    int tendencyDirection = DefensiveGambling > 0 ? 1 : -1;
                    int instructionDirection = instructionValue > 50 ? 1 : -1;
                    if (tendencyDirection == instructionDirection)
                        compatibility += 0.1f + (Math.Abs(DefensiveGambling) / 500f);
                    else
                        compatibility -= 0.1f + (Math.Abs(DefensiveGambling) / 500f);
                    break;

                case "shotfrequency":
                    // Quick trigger players struggle with low shot frequency instructions
                    if (ShotSelection < -30 && instructionValue < 30)
                        compatibility -= 0.15f;
                    else if (ShotSelection > 30 && instructionValue > 70)
                        compatibility += 0.1f;
                    break;

                case "ballhandlingduty":
                    // Ball stoppers struggle when asked to move ball quickly
                    if (BallMovement < -30 && instructionValue < 30)
                        compatibility -= 0.15f;
                    else if (BallMovement > 30 && instructionValue > 70)
                        compatibility += 0.1f;
                    break;

                case "effort":
                    // Players who coast struggle with high effort demands
                    if (EffortConsistency < -30 && instructionValue > 70)
                        compatibility -= 0.2f;
                    else if (EffortConsistency > 30)
                        compatibility += 0.1f;
                    break;
            }

            return Clamp(compatibility, 0.7f, 1.3f);
        }

        /// <summary>
        /// Gets the clutch performance modifier based on game situation.
        /// </summary>
        public float GetClutchModifier(bool isClutchSituation)
        {
            if (!isClutchSituation) return 1.0f;

            // -100 clutch = 0.85 modifier, +100 clutch = 1.15 modifier
            return 1.0f + (ClutchBehavior / 667f); // Maps -100 to +100 => 0.85 to 1.15
        }

        /// <summary>
        /// Gets effort modifier based on game situation and fatigue.
        /// </summary>
        public float GetEffortModifier(float currentStamina, int quarter)
        {
            // Base effort from tendency
            float baseEffort = 1.0f + (EffortConsistency / 500f); // 0.8 to 1.2

            // Players who coast may reduce effort when fatigued or in non-critical situations
            if (EffortConsistency < 0 && currentStamina < 50)
            {
                baseEffort -= 0.1f * (50 - currentStamina) / 50f;
            }

            // Players who always hustle maintain effort but tire faster
            if (EffortConsistency > 50)
            {
                // Small bonus but stamina drains faster (handled elsewhere)
                baseEffort += 0.05f;
            }

            return Clamp(baseEffort, 0.7f, 1.25f);
        }

        /// <summary>
        /// Determines if player will take a contested shot based on tendencies.
        /// </summary>
        public bool WillTakeContestedShot(float contestLevel, float shotQuality)
        {
            // Quick trigger players take more contested shots
            // Patient shooters pass up contested looks

            float threshold = 50 - (ShotSelection * 0.3f); // Range: 20 to 80
            float adjustedQuality = shotQuality - (contestLevel * 0.5f);

            return adjustedQuality > threshold;
        }

        /// <summary>
        /// Calculates passing tendency in current situation.
        /// Returns probability (0-1) of choosing to pass vs shoot.
        /// </summary>
        public float GetPassingProbability(float shotQuality, float bestTeammateQuality)
        {
            // Base probability from ball movement tendency
            float baseProbability = 0.5f + (BallMovement / 250f); // 0.1 to 0.9

            // Adjust based on shot quality differential
            float qualityDiff = bestTeammateQuality - shotQuality;
            baseProbability += qualityDiff / 100f;

            // Risk tolerance affects willingness to make difficult passes
            if (RiskTolerance > 30 && bestTeammateQuality > 60)
                baseProbability += 0.1f;

            return Clamp(baseProbability, 0.1f, 0.9f);
        }

        /// <summary>
        /// Gets a text description of the player's most notable tendencies.
        /// </summary>
        public List<string> GetNotableTendencies()
        {
            var notable = new List<string>();

            // Innate tendencies (only mention if significant)
            if (ShotSelection < -40)
                notable.Add("Quick trigger - takes contested shots");
            else if (ShotSelection > 40)
                notable.Add("Patient shooter - waits for quality looks");

            if (DefensiveGambling > 40)
                notable.Add("Aggressive defender - gambles for steals");
            else if (DefensiveGambling < -40)
                notable.Add("Disciplined defender - stays in position");

            if (ClutchBehavior > 40)
                notable.Add("Clutch performer - elevates in big moments");
            else if (ClutchBehavior < -40)
                notable.Add("Struggles under pressure");

            if (BallMovement > 40)
                notable.Add("Willing passer - moves the ball");
            else if (BallMovement < -40)
                notable.Add("Ball dominant - tends to hold the ball");

            if (EffortConsistency > 40)
                notable.Add("High motor - maximum effort every play");
            else if (EffortConsistency < -40)
                notable.Add("Inconsistent effort - coasts at times");

            if (RiskTolerance > 40)
                notable.Add("Risk taker - attempts difficult plays");
            else if (RiskTolerance < -40)
                notable.Add("Risk averse - plays it safe");

            if (LeadershipStyle > 40)
                notable.Add("Vocal leader - directs teammates");
            else if (LeadershipStyle < -40)
                notable.Add("Leads by example - quiet presence");

            if (AdversityResponse > 40)
                notable.Add("Mentally tough - stays composed");
            else if (AdversityResponse < -40)
                notable.Add("Can get frustrated - affected by adversity");

            return notable;
        }

        /// <summary>
        /// Gets areas that need coaching attention (low coachable tendencies).
        /// </summary>
        public List<CoachingArea> GetCoachingNeeds()
        {
            var needs = new List<CoachingArea>();

            if (ScreenSetting < 50)
                needs.Add(new CoachingArea("Screen Setting", ScreenSetting, "Needs work on setting solid screens"));
            if (HelpDefenseAggression < 40 || HelpDefenseAggression > 70)
                needs.Add(new CoachingArea("Help Defense Balance", HelpDefenseAggression,
                    HelpDefenseAggression < 40 ? "Too passive in help situations" : "Over-helps and leaves shooters"));
            if (TransitionDefense < 50)
                needs.Add(new CoachingArea("Transition Defense", TransitionDefense, "Needs to get back faster"));
            if (CloseoutControl < 50)
                needs.Add(new CoachingArea("Closeout Technique", CloseoutControl, "Gets blown by on closeouts"));
            if (BoxOutDiscipline < 50)
                needs.Add(new CoachingArea("Box Out", BoxOutDiscipline, "Needs better rebounding positioning"));
            if (PnRCoverageExecution < 50)
                needs.Add(new CoachingArea("PnR Coverage", PnRCoverageExecution, "Struggles with pick and roll defense"));
            if (DefensiveCommunication < 50)
                needs.Add(new CoachingArea("Defensive Communication", DefensiveCommunication, "Needs to call out screens"));
            if (ShotDiscipline < 50)
                needs.Add(new CoachingArea("Shot Selection", ShotDiscipline, "Takes shots outside his role"));

            return needs;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }

    /// <summary>
    /// Represents an area where a player needs coaching improvement.
    /// </summary>
    [Serializable]
    public class CoachingArea
    {
        public string Name { get; set; }
        public int CurrentLevel { get; set; }
        public string Description { get; set; }

        public CoachingArea(string name, int level, string description)
        {
            Name = name;
            CurrentLevel = level;
            Description = description;
        }
    }

    /// <summary>
    /// Player archetypes that influence tendency generation.
    /// </summary>
    public enum PlayerArchetype
    {
        Balanced,
        Playmaker,
        Scorer,
        Defender,
        Stretch,
        Athletic,
        PostPlayer,
        ThreeAndD,
        Facilitator
    }
}
