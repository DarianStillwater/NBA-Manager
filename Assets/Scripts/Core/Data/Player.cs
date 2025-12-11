using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Complete player data model with all attributes for simulation.
    /// Attributes range from 0-100 where 50 is league average.
    /// </summary>
    [Serializable]
    public class Player
    {
        // ==================== BIOGRAPHICAL DATA ====================
        [Header("Biographical")]
        public string PlayerId;
        public int Overall => (Finishing_Rim + Shot_Three + Defense_Perimeter + Passing) / 4; // Placeholder

        public string FirstName;
        public string LastName;
        public int JerseyNumber;
        public Position Position;
        public int Age;
        public int YearsPro; // 0 = Rookie
        public float HeightInches; // e.g., 78 = 6'6"
        public float WeightLbs;
        public string SkinToneCode; // For 3D model customization (e.g., "TONE_03")
        public string HairStyle;
        public string TeamId;

        // ==================== OFFENSIVE ATTRIBUTES ====================
        [Header("Offense - Scoring")]
        [Range(0, 100)] public int Finishing_Rim;        // Layups, dunks at rim
        [Range(0, 100)] public int Finishing_PostMoves;  // Back-to-basket scoring
        [Range(0, 100)] public int Shot_Close;           // Floaters, close shots
        [Range(0, 100)] public int Shot_MidRange;        // Pull-up jumpers
        [Range(0, 100)] public int Shot_Three;           // 3-point shooting
        [Range(0, 100)] public int FreeThrow;            // Free throw accuracy

        [Header("Offense - Playmaking")]
        [Range(0, 100)] public int Passing;              // Pass accuracy & vision
        [Range(0, 100)] public int BallHandling;         // Dribble moves, control
        [Range(0, 100)] public int OffensiveIQ;          // Reads, positioning
        [Range(0, 100)] public int SpeedWithBall;        // Movement while dribbling

        // ==================== DEFENSIVE ATTRIBUTES ====================
        [Header("Defense")]
        [Range(0, 100)] public int Defense_Perimeter;    // Guarding wings/guards
        [Range(0, 100)] public int Defense_Interior;     // Protecting the paint
        [Range(0, 100)] public int Defense_PostDefense;  // Guarding back-to-basket
        [Range(0, 100)] public int Steal;                // Ball-hawking ability
        [Range(0, 100)] public int Block;                // Shot-blocking
        [Range(0, 100)] public int DefensiveIQ;          // Help defense, rotations
        [Range(0, 100)] public int DefensiveRebound;     // Boxing out, grabbing boards

        // ==================== PHYSICAL ATTRIBUTES ====================
        [Header("Physical")]
        [Range(0, 100)] public int Speed;                // Foot speed without ball
        [Range(0, 100)] public int Acceleration;         // First step quickness
        [Range(0, 100)] public int Strength;             // Body control, physicality
        [Range(0, 100)] public int Vertical;             // Leaping ability
        [Range(0, 100)] public int Stamina;              // How long before fatigue
        [Range(0, 100)] public int Durability;           // Injury resistance
        [Range(0, 100)] public int Wingspan;             // Relative to height (affects contests)

        // ==================== MENTAL ATTRIBUTES ====================
        [Header("Mental")]
        [Range(0, 100)] public int BasketballIQ;         // Overall court awareness
        [Range(0, 100)] public int Clutch;               // Performance in big moments
        [Range(0, 100)] public int Consistency;          // Game-to-game variance
        [Range(0, 100)] public int WorkEthic;            // Training improvement rate
        [Range(0, 100)] public int Coachability;         // Responds to adjustments

        // ==================== PERSONALITY TRAITS ====================
        [Header("Personality")]
        [Range(0, 100)] public int Ego;                  // Demands shots, attention
        [Range(0, 100)] public int Leadership;           // Boosts teammates
        [Range(0, 100)] public int Composure;            // Handles pressure
        [Range(0, 100)] public int Aggression;           // Playing style intensity

        // ==================== DYNAMIC STATE (Changes during game) ====================
        [Header("Current State")]
        [Range(0, 100)] public float Energy;             // Depletes during play
        [Range(0, 100)] public float Morale;             // Affected by game events
        [Range(0, 100)] public float Form;               // Hot/cold streak
        public bool IsInjured;
        public string InjuryType;
        public int InjuryDaysRemaining;

        // ==================== COMPUTED PROPERTIES ====================
        public string FullName => $"{FirstName} {LastName}";
        
        public int OverallRating
        {
            get
            {
                // Weight attributes based on position
                float score = Position switch
                {
                    Position.PointGuard => (BallHandling * 0.2f + Passing * 0.2f + Shot_Three * 0.15f + 
                                           Speed * 0.15f + Defense_Perimeter * 0.15f + BasketballIQ * 0.15f),
                    Position.ShootingGuard => (Shot_Three * 0.2f + Shot_MidRange * 0.15f + Finishing_Rim * 0.15f +
                                              Defense_Perimeter * 0.2f + BallHandling * 0.15f + Speed * 0.15f),
                    Position.SmallForward => (Shot_Three * 0.15f + Finishing_Rim * 0.2f + Defense_Perimeter * 0.15f +
                                             Defense_Interior * 0.15f + Speed * 0.15f + Vertical * 0.1f + Strength * 0.1f),
                    Position.PowerForward => (Finishing_Rim * 0.2f + Defense_Interior * 0.2f + DefensiveRebound * 0.15f +
                                             Shot_MidRange * 0.15f + Strength * 0.15f + Block * 0.15f),
                    Position.Center => (Defense_Interior * 0.2f + Block * 0.2f + DefensiveRebound * 0.2f +
                                       Finishing_Rim * 0.2f + Strength * 0.1f + Finishing_PostMoves * 0.1f),
                    _ => 50
                };
                return Mathf.RoundToInt(score);
            }
        }

        /// <summary>
        /// Returns shooting percentage modifier based on energy and morale.
        /// </summary>
        public float GetConditionModifier()
        {
            float energyMod = Mathf.Lerp(0.7f, 1.0f, Energy / 100f);
            float moraleMod = Mathf.Lerp(0.9f, 1.1f, Morale / 100f);
            float formMod = Mathf.Lerp(0.85f, 1.15f, Form / 100f);
            return energyMod * moraleMod * formMod;
        }

        /// <summary>
        /// Depletes energy based on action intensity.
        /// </summary>
        public void ConsumeEnergy(float amount)
        {
            float staminaMultiplier = Mathf.Lerp(1.5f, 0.5f, Stamina / 100f);
            Energy = Mathf.Max(0, Energy - (amount * staminaMultiplier));
        }

        // ==================== HISTORY & AWARDS ====================
        public List<SeasonStats> CareerStats = new List<SeasonStats>();
        public List<AwardHistory> Awards = new List<AwardHistory>();
        
        public SeasonStats CurrentSeasonStats 
        {
            get 
            {
                if (CareerStats == null || CareerStats.Count == 0) return null;
                return CareerStats[CareerStats.Count - 1]; // Return current/latest season
            }
        }

        /// <summary>
        /// Restores energy (called when player is on bench).
        /// </summary>
        public void RestoreEnergy(float amount)
        {
            Energy = Mathf.Min(100, Energy + amount);
        }
    }

    public enum Position
    {
        PointGuard = 1,
        ShootingGuard = 2,
        SmallForward = 3,
        PowerForward = 4,
        Center = 5
    }
}
