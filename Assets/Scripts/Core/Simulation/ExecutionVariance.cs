using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>A defensive execution mistake for one possession.</summary>
    public enum LapseType
    {
        None = 0,
        LateCloseout,   // arrives late/short on the shooter — cleaner look
        BlownRotation,  // loses his assignment in the scheme rotation — open look
        MissedHelp      // help never comes — easier finish inside
    }

    /// <summary>An offensive deviation from the called system for one possession.</summary>
    public enum OffensiveDeviation
    {
        None = 0,
        HeroBall,       // waves off the play, forces his own contested look
        IgnoredSystem   // shot selection reverts toward instinct instead of the called mix
    }

    /// <summary>
    /// Player-execution variance: a scheme is only as good as the players running
    /// it. Rolled ONCE per possession in the outcome layer (never by the
    /// choreographer), the results are applied to the math and written into the
    /// possession script as facts for the visuals/narration to draw. High-IQ,
    /// disciplined, fresh players rarely lapse; tired low-IQ rosters in complex
    /// schemes lapse often — which is what keeps strategy from being a win-button.
    /// </summary>
    public static class ExecutionVariance
    {
        /// <summary>How much harder a scheme is to execute than standard man.</summary>
        public static float SchemeComplexity(DefensiveSchemeType scheme) => scheme switch
        {
            DefensiveSchemeType.ManToManStandard => 1.0f,
            DefensiveSchemeType.ManToManConservative => 0.9f,
            DefensiveSchemeType.ManToManAggressive => 1.15f,
            DefensiveSchemeType.SwitchEverything => 1.35f,
            DefensiveSchemeType.Zone2_3 => 1.4f,
            DefensiveSchemeType.Zone3_2 => 1.45f,
            DefensiveSchemeType.Zone1_2_2 => 1.45f,
            DefensiveSchemeType.Zone1_3_1 => 1.55f,
            DefensiveSchemeType.MatchupZone => 1.6f,
            DefensiveSchemeType.BoxAndOne => 1.6f,
            DefensiveSchemeType.TriangleAndTwo => 1.65f,
            DefensiveSchemeType.FullCourtPress => 1.5f,
            DefensiveSchemeType.HalfCourtTrap => 1.5f,
            _ => 1.0f
        };

        /// <summary>Per-defender lapse propensity per possession (0.002–0.045).</summary>
        public static float LapsePropensity(Player p)
        {
            if (p == null) return 0.01f;
            float cons = p.Consistency > 0 ? p.Consistency : 60f;   // unset in some fixtures
            float effIQ = p.DefensiveIQ * 0.5f + cons * 0.25f +
                          (p.Tendencies != null ? p.Tendencies.CloseoutControl : 50f) * 0.25f;
            effIQ -= (100f - p.Energy) * 0.10f;   // tired legs forget assignments
            float rate = 0.005f + 0.025f * Mathf.Clamp01((75f - effIQ) / 50f) * 2f;
            return Mathf.Clamp(rate, 0.002f, 0.045f);
        }

        /// <summary>
        /// Roll the possession's defensive lapse. Returns (None, -1) most of the time.
        /// familiarityMult &gt; 1 right after a mid-game scheme change.
        /// </summary>
        public static (LapseType type, int defenderIndex) RollDefensiveLapse(
            Player[] defense, DefensiveSchemeType scheme, float familiarityMult, System.Random rng)
        {
            if (defense == null || defense.Length == 0) return (LapseType.None, -1);

            float total = 0f;
            var w = new float[defense.Length];
            for (int i = 0; i < defense.Length; i++)
            {
                w[i] = LapsePropensity(defense[i]);
                total += w[i];
            }

            float teamRate = Mathf.Min(total * SchemeComplexity(scheme) * familiarityMult, 0.20f);
            if (rng.NextDouble() >= teamRate) return (LapseType.None, -1);

            // Pick the culprit, weighted by propensity.
            float pick = (float)rng.NextDouble() * total;
            int culprit = defense.Length - 1;
            for (int i = 0; i < defense.Length; i++)
            {
                pick -= w[i];
                if (pick <= 0f) { culprit = i; break; }
            }

            // Lapse flavor: sloppy closers miss closeouts; otherwise rotation/help.
            var p = defense[culprit];
            float cc = p?.Tendencies != null ? p.Tendencies.CloseoutControl : 50f;
            double flavor = rng.NextDouble();
            LapseType type;
            if (cc < 45f && flavor < 0.5) type = LapseType.LateCloseout;
            else if (flavor < 0.4) type = LapseType.LateCloseout;
            else if (flavor < 0.75) type = LapseType.BlownRotation;
            else type = LapseType.MissedHelp;

            return (type, culprit);
        }
    }
}
