using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Simulation
{
    /// <summary>
    /// Pure per-scheme defensive modifier profile. Every defensive scheme trades
    /// something away: a 2-3 walls off the rim but concedes threes, a 1-3-1 forces
    /// turnovers but bleeds corner looks and offensive boards, a box-and-one smothers
    /// the star while everyone else eats. ManToManStandard is the identity profile,
    /// so a default defense simulates exactly as before these profiles existed.
    ///
    /// Shot/foul/turnover fields are multipliers (1 = neutral); OrebConcededBonus is
    /// additive to the offensive-rebound chance. Magnitudes stay within a few percent
    /// so league-wide scoring balance holds.
    /// </summary>
    public readonly struct DefensiveSchemeProfile
    {
        public readonly float InteriorShotMod;   // RestrictedArea + Paint
        public readonly float MidShotMod;        // Short + Long mid-range
        public readonly float ThreeShotMod;      // Above-the-break threes
        public readonly float CornerThreeMod;    // Corner threes
        public readonly float TurnoverForcedMod; // Multiplies offense's turnover chance
        public readonly float FoulMod;           // Multiplies defensive foul chance
        public readonly float OrebConcededBonus; // Added to offense's OREB chance
        public readonly float StarSuppressionMod;// Multiplies the star's shot prob
        public readonly float OthersBoostMod;    // Multiplies everyone else's (BoxAndOne trade-off)

        public DefensiveSchemeProfile(float interior, float mid, float three, float corner,
            float turnover, float foul, float orebBonus, float starSuppression = 1f, float othersBoost = 1f)
        {
            InteriorShotMod = interior; MidShotMod = mid; ThreeShotMod = three; CornerThreeMod = corner;
            TurnoverForcedMod = turnover; FoulMod = foul; OrebConcededBonus = orebBonus;
            StarSuppressionMod = starSuppression; OthersBoostMod = othersBoost;
        }

        public static readonly DefensiveSchemeProfile Neutral =
            new DefensiveSchemeProfile(1f, 1f, 1f, 1f, 1f, 1f, 0f);

        public float ShotModFor(CourtZone zone) => zone switch
        {
            CourtZone.RestrictedArea => InteriorShotMod,
            CourtZone.Paint => InteriorShotMod,
            CourtZone.ShortMidRange => MidShotMod,
            CourtZone.LongMidRange => MidShotMod,
            CourtZone.ThreePoint => ThreeShotMod,
            CourtZone.Corner => CornerThreeMod,
            _ => 1f
        };

        public static bool IsZoneScheme(DefensiveSchemeType scheme) => scheme switch
        {
            DefensiveSchemeType.Zone2_3 => true,
            DefensiveSchemeType.Zone3_2 => true,
            DefensiveSchemeType.Zone1_3_1 => true,
            DefensiveSchemeType.Zone1_2_2 => true,
            DefensiveSchemeType.BoxAndOne => true,
            DefensiveSchemeType.TriangleAndTwo => true,
            DefensiveSchemeType.MatchupZone => true,
            _ => false
        };

        public static DefensiveSchemeProfile For(DefensiveSchemeType scheme) => scheme switch
        {
            // Man variants: standard is the identity; aggressive reaches (turnovers,
            // fouls, slightly disrupted jumpers); conservative sags (protects the
            // paint, concedes jumpers, avoids fouls); switching kills clean threes
            // off screens but posts up small on mismatches inside.
            DefensiveSchemeType.ManToManStandard =>
                Neutral,
            DefensiveSchemeType.ManToManAggressive =>
                new DefensiveSchemeProfile(1.00f, 0.98f, 0.98f, 0.98f, 1.08f, 1.10f, 0f),
            DefensiveSchemeType.ManToManConservative =>
                new DefensiveSchemeProfile(0.96f, 1.03f, 1.03f, 1.03f, 0.95f, 0.90f, 0f),
            DefensiveSchemeType.SwitchEverything =>
                new DefensiveSchemeProfile(1.03f, 1.00f, 0.97f, 0.97f, 1.00f, 1.00f, 0f),

            // Zones. 2-3 walls the rim, bleeds threes and boards. 3-2 chases
            // shooters off the line, opens the middle. 1-3-1 traps and gambles:
            // turnovers up, corners and glass wide open, reaching fouls.
            DefensiveSchemeType.Zone2_3 =>
                new DefensiveSchemeProfile(0.92f, 0.98f, 1.06f, 1.08f, 0.97f, 0.88f, 0.04f),
            DefensiveSchemeType.Zone3_2 =>
                new DefensiveSchemeProfile(1.05f, 1.02f, 0.94f, 0.95f, 0.98f, 0.92f, 0.03f),
            DefensiveSchemeType.Zone1_3_1 =>
                new DefensiveSchemeProfile(1.02f, 0.97f, 1.02f, 1.07f, 1.12f, 1.05f, 0.05f),
            DefensiveSchemeType.Zone1_2_2 =>       // 3-2 cousin: perimeter-first
                new DefensiveSchemeProfile(1.04f, 1.01f, 0.95f, 0.96f, 1.02f, 0.94f, 0.03f),
            DefensiveSchemeType.MatchupZone =>     // mild 2-3 with man principles
                new DefensiveSchemeProfile(0.96f, 0.99f, 1.03f, 1.04f, 0.99f, 0.94f, 0.02f),

            // Junk defenses: smother the star, concede to the supporting cast.
            DefensiveSchemeType.BoxAndOne =>
                new DefensiveSchemeProfile(0.97f, 1.00f, 1.03f, 1.04f, 1.00f, 1.02f, 0.03f,
                    starSuppression: 0.90f, othersBoost: 1.04f),
            DefensiveSchemeType.TriangleAndTwo =>
                new DefensiveSchemeProfile(0.99f, 1.01f, 1.02f, 1.03f, 1.00f, 1.02f, 0.03f,
                    starSuppression: 0.93f, othersBoost: 1.03f),

            // Pressure schemes: steal/turnover bumps live inline in the possession
            // sim (they predate profiles); the profile carries what pressure
            // concedes behind the press when it's beaten.
            DefensiveSchemeType.FullCourtPress =>
                new DefensiveSchemeProfile(1.05f, 1.02f, 1.02f, 1.02f, 1.00f, 1.06f, 0.02f),
            DefensiveSchemeType.HalfCourtTrap =>
                new DefensiveSchemeProfile(1.04f, 1.01f, 1.02f, 1.03f, 1.00f, 1.05f, 0.02f),

            _ => Neutral
        };

        /// <summary>
        /// The profile a defense actually plays: a zone PrimaryScheme is full-time
        /// zone; a man PrimaryScheme sprinkles in its SecondaryScheme for ZoneUsage%
        /// of possessions, modeled as a blend. Default strategies (man, ZoneUsage 0)
        /// resolve to the identity profile.
        /// </summary>
        public static DefensiveSchemeProfile Effective(DefensiveSystem d)
        {
            if (d == null) return Neutral;
            var primary = For(d.PrimaryScheme);
            if (IsZoneScheme(d.PrimaryScheme)) return primary;
            float frac = Mathf.Clamp01(d.ZoneUsage / 100f);
            if (frac <= 0f) return primary;
            return Lerp(primary, For(d.SecondaryScheme), frac);
        }

        public static DefensiveSchemeProfile Lerp(DefensiveSchemeProfile a, DefensiveSchemeProfile b, float t)
        {
            t = Mathf.Clamp01(t);
            return new DefensiveSchemeProfile(
                Mathf.Lerp(a.InteriorShotMod, b.InteriorShotMod, t),
                Mathf.Lerp(a.MidShotMod, b.MidShotMod, t),
                Mathf.Lerp(a.ThreeShotMod, b.ThreeShotMod, t),
                Mathf.Lerp(a.CornerThreeMod, b.CornerThreeMod, t),
                Mathf.Lerp(a.TurnoverForcedMod, b.TurnoverForcedMod, t),
                Mathf.Lerp(a.FoulMod, b.FoulMod, t),
                Mathf.Lerp(a.OrebConcededBonus, b.OrebConcededBonus, t),
                Mathf.Lerp(a.StarSuppressionMod, b.StarSuppressionMod, t),
                Mathf.Lerp(a.OthersBoostMod, b.OthersBoostMod, t));
        }
    }
}
