using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// Evaluates matchup quality between offensive and defensive players.
    /// Provides real-time scoring for identifying advantageous and problematic matchups.
    /// </summary>
    public class MatchupEvaluator
    {
        // ==================== DEPENDENCIES ====================
        private Func<string, Player> _getPlayer;

        // ==================== MATCHUP HISTORY ====================
        private Dictionary<string, MatchupHistory> _matchupHistory = new Dictionary<string, MatchupHistory>();

        // ==================== CONFIGURATION ====================
        private MatchupEvaluatorSettings _settings;

        // ==================== CONSTRUCTOR ====================

        public MatchupEvaluator(Func<string, Player> getPlayer)
        {
            _getPlayer = getPlayer;
            _settings = new MatchupEvaluatorSettings();
        }

        public void SetSettings(MatchupEvaluatorSettings settings)
        {
            _settings = settings;
        }

        // ==================== MATCHUP QUALITY SCORING ====================

        /// <summary>
        /// Evaluates matchup quality from the defender's perspective.
        /// Returns 0-100 where higher = better for defender.
        /// </summary>
        public float EvaluateDefensiveMatchup(string defenderId, string offenderId)
        {
            var defender = _getPlayer(defenderId);
            var offender = _getPlayer(offenderId);

            if (defender == null || offender == null)
                return 50f;

            float score = 50f;  // Neutral baseline

            // Physical matchup
            float physicalScore = EvaluatePhysicalMatchup(defender, offender);
            score += (physicalScore - 50f) * _settings.PhysicalWeight;

            // Skill matchup (defender's defensive skills vs offender's offensive skills)
            float skillScore = EvaluateSkillMatchup(defender, offender);
            score += (skillScore - 50f) * _settings.SkillWeight;

            // Position compatibility
            float positionScore = EvaluatePositionCompatibility(defender, offender);
            score += (positionScore - 50f) * _settings.PositionWeight;

            // Historical performance in this matchup
            float historyScore = GetHistoricalMatchupScore(defenderId, offenderId);
            if (historyScore >= 0)
            {
                score += (historyScore - 50f) * _settings.HistoryWeight;
            }

            return Mathf.Clamp(score, 0f, 100f);
        }

        /// <summary>
        /// Evaluates matchup quality from the offensive perspective.
        /// Returns 0-100 where higher = better for offense.
        /// </summary>
        public float EvaluateOffensiveMatchup(string offenderId, string defenderId)
        {
            return 100f - EvaluateDefensiveMatchup(defenderId, offenderId);
        }

        // ==================== COMPONENT EVALUATIONS ====================

        private float EvaluatePhysicalMatchup(Player defender, Player offender)
        {
            float score = 50f;

            // Height advantage (being taller is good for defense usually, but guards need speed)
            float heightDiff = defender.HeightInches - offender.HeightInches;
            score += Mathf.Clamp(heightDiff * 1.5f, -15f, 15f);

            // Wingspan advantage (critical for defense)
            float wingspanDiff = defender.Wingspan - offender.Wingspan;
            score += Mathf.Clamp(wingspanDiff * 0.3f, -10f, 10f);

            // Speed matchup (defender needs to keep up)
            float speedDiff = defender.Speed - offender.Speed;
            score += Mathf.Clamp(speedDiff * 0.2f, -10f, 10f);

            // Strength matchup (important for post defense)
            float strengthDiff = defender.Strength - offender.Strength;
            score += Mathf.Clamp(strengthDiff * 0.15f, -8f, 8f);

            return Mathf.Clamp(score, 0f, 100f);
        }

        private float EvaluateSkillMatchup(Player defender, Player offender)
        {
            float score = 50f;

            // Perimeter defense vs ball handling/shooting
            if (offender.Position <= Position.SmallForward)
            {
                float perimeterAdvantage = defender.Defense_Perimeter - offender.BallHandling;
                score += Mathf.Clamp(perimeterAdvantage * 0.3f, -15f, 15f);

                float shootingChallenge = defender.Defense_Perimeter - offender.Shot_Three;
                score += Mathf.Clamp(shootingChallenge * 0.2f, -10f, 10f);
            }

            // Interior/post defense vs post moves
            if (offender.Position >= Position.PowerForward)
            {
                float postAdvantage = defender.Defense_PostDefense - offender.Finishing_PostMoves;
                score += Mathf.Clamp(postAdvantage * 0.3f, -15f, 15f);

                float interiorAdvantage = defender.Defense_Interior - offender.Finishing_Rim;
                score += Mathf.Clamp(interiorAdvantage * 0.2f, -10f, 10f);
            }

            // Defensive IQ vs Offensive IQ
            float iqDiff = defender.DefensiveIQ - offender.OffensiveIQ;
            score += Mathf.Clamp(iqDiff * 0.15f, -8f, 8f);

            return Mathf.Clamp(score, 0f, 100f);
        }

        private float EvaluatePositionCompatibility(Player defender, Player offender)
        {
            int positionDiff = Math.Abs((int)defender.Position - (int)offender.Position);

            // Same position = neutral (50)
            // 1 position away = slight disadvantage (45)
            // 2+ positions away = significant disadvantage
            return positionDiff switch
            {
                0 => 55f,  // Same position - slight advantage for knowing tendencies
                1 => 45f,  // Adjacent position - slight mismatch
                2 => 35f,  // Two positions away - notable mismatch
                _ => 25f   // Three+ positions - severe mismatch
            };
        }

        private float GetHistoricalMatchupScore(string defenderId, string offenderId)
        {
            string key = $"{defenderId}_vs_{offenderId}";

            if (!_matchupHistory.ContainsKey(key) || _matchupHistory[key].Possessions < 3)
                return -1f;  // Not enough data

            var history = _matchupHistory[key];
            // Lower PPP allowed = better for defender
            float pppScore = Mathf.Lerp(100f, 0f, history.PointsPerPossession / 2f);
            return pppScore;
        }

        // ==================== MATCHUP TRACKING ====================

        /// <summary>
        /// Records a possession result for matchup tracking.
        /// </summary>
        public void RecordMatchupPossession(string defenderId, string offenderId, int pointsScored, bool madeShot)
        {
            string key = $"{defenderId}_vs_{offenderId}";

            if (!_matchupHistory.ContainsKey(key))
            {
                _matchupHistory[key] = new MatchupHistory
                {
                    DefenderId = defenderId,
                    OffenderId = offenderId
                };
            }

            var history = _matchupHistory[key];
            history.Possessions++;
            history.PointsAllowed += pointsScored;
            if (madeShot) history.ShotsAllowed++;
        }

        /// <summary>
        /// Gets matchup history between two players.
        /// </summary>
        public MatchupHistory GetMatchupHistory(string defenderId, string offenderId)
        {
            string key = $"{defenderId}_vs_{offenderId}";
            return _matchupHistory.TryGetValue(key, out var history) ? history : null;
        }

        // ==================== MATCHUP RECOMMENDATIONS ====================

        /// <summary>
        /// Finds the best defender for a given offensive player from available defenders.
        /// </summary>
        public MatchupRecommendation GetBestDefender(string offenderId, List<string> availableDefenders)
        {
            var recommendations = availableDefenders
                .Select(d => new MatchupRecommendation
                {
                    DefenderId = d,
                    OffenderId = offenderId,
                    MatchupScore = EvaluateDefensiveMatchup(d, offenderId),
                    Reasoning = GetMatchupReasoning(d, offenderId)
                })
                .OrderByDescending(r => r.MatchupScore)
                .ToList();

            return recommendations.FirstOrDefault();
        }

        /// <summary>
        /// Finds the best offensive target against a given defender.
        /// </summary>
        public MatchupRecommendation GetBestOffensiveTarget(string defenderId, List<string> availableOffenders)
        {
            var recommendations = availableOffenders
                .Select(o => new MatchupRecommendation
                {
                    DefenderId = defenderId,
                    OffenderId = o,
                    MatchupScore = EvaluateOffensiveMatchup(o, defenderId),
                    Reasoning = GetOffensiveMatchupReasoning(o, defenderId)
                })
                .OrderByDescending(r => r.MatchupScore)
                .ToList();

            return recommendations.FirstOrDefault();
        }

        /// <summary>
        /// Evaluates all matchups for a lineup and identifies problems/advantages.
        /// </summary>
        public LineupMatchupAnalysis AnalyzeLineupMatchups(
            List<string> teamLineup,
            List<string> opponentLineup,
            Dictionary<string, string> currentMatchups = null)
        {
            var analysis = new LineupMatchupAnalysis();

            // If no matchups provided, assume positional matching
            currentMatchups ??= new Dictionary<string, string>();
            for (int i = 0; i < Math.Min(teamLineup.Count, opponentLineup.Count); i++)
            {
                if (!currentMatchups.ContainsKey(teamLineup[i]))
                    currentMatchups[teamLineup[i]] = opponentLineup[i];
            }

            foreach (var matchup in currentMatchups)
            {
                var score = EvaluateDefensiveMatchup(matchup.Key, matchup.Value);
                var evaluation = new MatchupEvaluation
                {
                    DefenderId = matchup.Key,
                    OffenderId = matchup.Value,
                    Score = score,
                    Classification = ClassifyMatchup(score),
                    Reasoning = GetMatchupReasoning(matchup.Key, matchup.Value)
                };

                if (score < 40)
                    analysis.Problems.Add(evaluation);
                else if (score > 60)
                    analysis.Advantages.Add(evaluation);
                else
                    analysis.Neutral.Add(evaluation);
            }

            // Calculate overall lineup defensive matchup quality
            var allScores = currentMatchups.Select(m => EvaluateDefensiveMatchup(m.Key, m.Value)).ToList();
            analysis.OverallScore = allScores.Any() ? allScores.Average() : 50f;

            return analysis;
        }

        private MatchupClassification ClassifyMatchup(float score)
        {
            return score switch
            {
                < 25 => MatchupClassification.SevereMismatch,
                < 40 => MatchupClassification.Disadvantage,
                < 55 => MatchupClassification.Neutral,
                < 70 => MatchupClassification.Advantage,
                _ => MatchupClassification.StrongAdvantage
            };
        }

        private string GetMatchupReasoning(string defenderId, string offenderId)
        {
            var defender = _getPlayer(defenderId);
            var offender = _getPlayer(offenderId);

            if (defender == null || offender == null)
                return "Unable to evaluate";

            var reasons = new List<string>();

            // Height
            float heightDiff = defender.HeightInches - offender.HeightInches;
            if (heightDiff >= 3)
                reasons.Add("Height advantage");
            else if (heightDiff <= -3)
                reasons.Add("Height disadvantage");

            // Speed
            if (defender.Speed - offender.Speed >= 10)
                reasons.Add("Quicker");
            else if (defender.Speed - offender.Speed <= -10)
                reasons.Add("Slower");

            // Position
            int posDiff = Math.Abs((int)defender.Position - (int)offender.Position);
            if (posDiff >= 2)
                reasons.Add("Position mismatch");

            // Skills
            if (offender.Position <= Position.SmallForward)
            {
                if (defender.Defense_Perimeter - offender.BallHandling >= 15)
                    reasons.Add("Strong perimeter defense");
                else if (defender.Defense_Perimeter - offender.BallHandling <= -15)
                    reasons.Add("Can be beaten off dribble");
            }

            if (offender.Position >= Position.PowerForward)
            {
                if (defender.Defense_PostDefense - offender.Finishing_PostMoves >= 15)
                    reasons.Add("Strong post defense");
                else if (defender.Defense_PostDefense - offender.Finishing_PostMoves <= -15)
                    reasons.Add("Vulnerable in post");
            }

            return reasons.Any() ? string.Join(", ", reasons) : "Even matchup";
        }

        private string GetOffensiveMatchupReasoning(string offenderId, string defenderId)
        {
            var defender = _getPlayer(defenderId);
            var offender = _getPlayer(offenderId);

            if (defender == null || offender == null)
                return "Unable to evaluate";

            var reasons = new List<string>();

            // Size advantage for offense
            if (offender.HeightInches - defender.HeightInches >= 3)
                reasons.Add("Size advantage");

            // Speed advantage
            if (offender.Speed - defender.Speed >= 10)
                reasons.Add("Speed advantage");

            // Skill advantages
            if (offender.Shot_Three - defender.Defense_Perimeter >= 15)
                reasons.Add("Shooting mismatch");
            if (offender.BallHandling - defender.Defense_Perimeter >= 15)
                reasons.Add("Can beat off dribble");
            if (offender.Finishing_PostMoves - defender.Defense_PostDefense >= 15)
                reasons.Add("Post mismatch");

            return reasons.Any() ? string.Join(", ", reasons) : "No clear advantage";
        }

        // ==================== HUNT/HIDE RECOMMENDATIONS ====================

        /// <summary>
        /// Identifies matchups to exploit offensively ("hunt").
        /// </summary>
        public List<HuntHideRecommendation> GetHuntRecommendations(
            List<string> ourOffense,
            List<string> theirDefense)
        {
            var recommendations = new List<HuntHideRecommendation>();

            foreach (var offender in ourOffense)
            {
                foreach (var defender in theirDefense)
                {
                    var offScore = EvaluateOffensiveMatchup(offender, defender);
                    if (offScore >= 65)
                    {
                        recommendations.Add(new HuntHideRecommendation
                        {
                            OurPlayerId = offender,
                            TheirPlayerId = defender,
                            IsHunt = true,
                            Score = offScore,
                            Reasoning = GetOffensiveMatchupReasoning(offender, defender)
                        });
                    }
                }
            }

            return recommendations.OrderByDescending(r => r.Score).Take(3).ToList();
        }

        /// <summary>
        /// Identifies matchups to avoid defensively ("hide").
        /// </summary>
        public List<HuntHideRecommendation> GetHideRecommendations(
            List<string> ourDefense,
            List<string> theirOffense)
        {
            var recommendations = new List<HuntHideRecommendation>();

            foreach (var defender in ourDefense)
            {
                foreach (var offender in theirOffense)
                {
                    var defScore = EvaluateDefensiveMatchup(defender, offender);
                    if (defScore <= 35)
                    {
                        recommendations.Add(new HuntHideRecommendation
                        {
                            OurPlayerId = defender,
                            TheirPlayerId = offender,
                            IsHunt = false,
                            Score = 100 - defScore,  // Invert so higher = worse matchup
                            Reasoning = GetMatchupReasoning(defender, offender)
                        });
                    }
                }
            }

            return recommendations.OrderByDescending(r => r.Score).Take(3).ToList();
        }

        // ==================== RESET ====================

        /// <summary>
        /// Resets matchup history for new game.
        /// </summary>
        public void ResetForNewGame()
        {
            _matchupHistory.Clear();
        }
    }

    // ==================== DATA STRUCTURES ====================

    public enum MatchupClassification
    {
        SevereMismatch,  // < 25
        Disadvantage,    // 25-40
        Neutral,         // 40-55
        Advantage,       // 55-70
        StrongAdvantage  // > 70
    }

    [Serializable]
    public class MatchupHistory
    {
        public string DefenderId;
        public string OffenderId;
        public int Possessions;
        public int PointsAllowed;
        public int ShotsAllowed;

        public float PointsPerPossession => Possessions > 0 ? PointsAllowed / (float)Possessions : 0f;
        public float ShotPercentageAllowed => Possessions > 0 ? ShotsAllowed / (float)Possessions : 0f;
    }

    [Serializable]
    public class MatchupRecommendation
    {
        public string DefenderId;
        public string OffenderId;
        public float MatchupScore;
        public string Reasoning;
    }

    [Serializable]
    public class MatchupEvaluation
    {
        public string DefenderId;
        public string OffenderId;
        public float Score;
        public MatchupClassification Classification;
        public string Reasoning;
    }

    [Serializable]
    public class LineupMatchupAnalysis
    {
        public List<MatchupEvaluation> Problems = new List<MatchupEvaluation>();
        public List<MatchupEvaluation> Advantages = new List<MatchupEvaluation>();
        public List<MatchupEvaluation> Neutral = new List<MatchupEvaluation>();
        public float OverallScore;
    }

    [Serializable]
    public class HuntHideRecommendation
    {
        public string OurPlayerId;
        public string TheirPlayerId;
        public bool IsHunt;  // true = exploit, false = hide
        public float Score;
        public string Reasoning;
    }

    [Serializable]
    public class MatchupEvaluatorSettings
    {
        public float PhysicalWeight = 0.25f;
        public float SkillWeight = 0.35f;
        public float PositionWeight = 0.20f;
        public float HistoryWeight = 0.20f;
    }
}
