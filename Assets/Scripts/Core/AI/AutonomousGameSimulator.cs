using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// Simulates games autonomously when the user is in GM-Only mode.
    /// Uses AICoachPersonality to make coaching decisions.
    /// </summary>
    public class AutonomousGameSimulator
    {
        private static AutonomousGameSimulator _instance;
        public static AutonomousGameSimulator Instance => _instance ??= new AutonomousGameSimulator();

        private System.Random _rng;

        public AutonomousGameSimulator()
        {
            _rng = new System.Random();
        }

        /// <summary>
        /// Simulates a complete game autonomously.
        /// </summary>
        public AutonomousGameResult SimulateGame(
            Team userTeam,
            Team opponentTeam,
            AICoachPersonality aiCoach,
            bool isHome,
            bool isPlayoffs = false,
            int playoffRound = 0)
        {
            var result = new AutonomousGameResult
            {
                GameId = Guid.NewGuid().ToString(),
                GameDate = DateTime.Now,
                HomeTeamId = isHome ? userTeam.TeamId : opponentTeam.TeamId,
                AwayTeamId = isHome ? opponentTeam.TeamId : userTeam.TeamId,
                WasHomeGame = isHome,
                CoachName = aiCoach.CoachName
            };

            // Get rosters
            var userRoster = userTeam.Roster ?? new List<Player>();
            var opponentRoster = opponentTeam.Roster ?? new List<Player>();

            if (userRoster.Count < 5 || opponentRoster.Count < 5)
            {
                Debug.LogWarning("[AutonomousGameSimulator] Insufficient roster size for simulation");
                return CreateForfeitResult(result, userRoster.Count < 5);
            }

            // Calculate team strengths
            float userStrength = CalculateTeamStrength(userRoster);
            float opponentStrength = CalculateTeamStrength(opponentRoster);

            // Home court advantage
            float homeAdvantage = isHome ? 3f : -3f;

            // Coach bonus based on personality
            float coachBonus = CalculateCoachBonus(aiCoach, userRoster, opponentRoster);

            // Simulate quarter by quarter
            int userTotal = 0;
            int oppTotal = 0;
            var keyMoments = new List<GameMoment>();

            for (int quarter = 1; quarter <= 4; quarter++)
            {
                var (userQ, oppQ, moments) = SimulateQuarter(
                    quarter,
                    userStrength + homeAdvantage + coachBonus,
                    opponentStrength,
                    aiCoach,
                    userTotal - oppTotal,
                    userRoster,
                    opponentRoster
                );

                userTotal += userQ;
                oppTotal += oppQ;

                if (isHome)
                {
                    result.HomeQuarterScores[quarter - 1] = userQ;
                    result.AwayQuarterScores[quarter - 1] = oppQ;
                }
                else
                {
                    result.HomeQuarterScores[quarter - 1] = oppQ;
                    result.AwayQuarterScores[quarter - 1] = userQ;
                }

                keyMoments.AddRange(moments);
            }

            // Handle overtime if tied
            while (userTotal == oppTotal)
            {
                result.WasOvertime = true;
                result.OvertimePeriods++;

                var (userOT, oppOT, otMoments) = SimulateOvertime(
                    userStrength + homeAdvantage + coachBonus,
                    opponentStrength,
                    aiCoach,
                    userRoster,
                    opponentRoster
                );

                userTotal += userOT;
                oppTotal += oppOT;
                keyMoments.AddRange(otMoments);

                if (result.OvertimePeriods > 4) // Safety limit
                {
                    if (userTotal == oppTotal) userTotal++; // Force win
                    break;
                }
            }

            // Set final scores
            result.HomeScore = isHome ? userTotal : oppTotal;
            result.AwayScore = isHome ? oppTotal : userTotal;

            // Generate box scores
            result.HomeBoxScore = GenerateBoxScore(isHome ? userRoster : opponentRoster, isHome ? userTotal : oppTotal, true);
            result.AwayBoxScore = GenerateBoxScore(isHome ? opponentRoster : userRoster, isHome ? oppTotal : userTotal, false);

            // Generate team stats
            result.HomeTeamStats = AggregateTeamStats(result.HomeBoxScore, result.HomeScore);
            result.AwayTeamStats = AggregateTeamStats(result.AwayBoxScore, result.AwayScore);

            // Generate key moments
            result.KeyMoments = keyMoments.OrderBy(m => m.Quarter).ThenByDescending(m => m.GameClock).ToList();

            // Evaluate coach performance
            result.CoachPerformance = EvaluateCoachPerformance(aiCoach, result, userRoster);

            // Generate narrative
            result.GameNarrative = GenerateGameNarrative(result, userTeam, opponentTeam, aiCoach);
            result.CoachPostGameComment = GeneratePostGameComment(aiCoach, result);

            return result;
        }

        private float CalculateTeamStrength(List<Player> roster)
        {
            if (roster == null || roster.Count == 0) return 50f;

            // Top 8 players weighted
            var sortedRoster = roster.OrderByDescending(p => p.Overall).Take(8).ToList();
            float strength = 0;

            for (int i = 0; i < sortedRoster.Count; i++)
            {
                float weight = i < 5 ? 1.0f : 0.5f; // Starters weighted more
                strength += sortedRoster[i].Overall * weight;
            }

            return strength / 6.5f; // Normalize
        }

        private float CalculateCoachBonus(AICoachPersonality coach, List<Player> userRoster, List<Player> oppRoster)
        {
            float bonus = 0;

            // Good adjustment speed helps
            bonus += (coach.InGameAdjustmentSpeed - 50) * 0.05f;

            // Clutch coaching
            bonus += (coach.ClutchPressure - 50) * 0.03f;

            // ATO plays
            bonus += (coach.ATOPlayQuality - 50) * 0.02f;

            // Motivation
            bonus += (coach.MotivationAbility - 50) * 0.03f;

            // Stubbornness can hurt
            bonus -= (coach.Stubbornness - 50) * 0.02f;

            return bonus;
        }

        private (int userScore, int oppScore, List<GameMoment> moments) SimulateQuarter(
            int quarter,
            float userStrength,
            float oppStrength,
            AICoachPersonality coach,
            int currentScoreDiff,
            List<Player> userRoster,
            List<Player> oppRoster)
        {
            var moments = new List<GameMoment>();

            // Base points per quarter
            int basePts = 27;

            // Pace adjustment
            float paceModifier = coach.PreferredPace / 100f;

            // Calculate scores with randomness
            float userExpected = basePts * paceModifier * (userStrength / 75f);
            float oppExpected = basePts * paceModifier * (oppStrength / 75f);

            // Add randomness (variance)
            float variance = 5f;
            int userScore = Mathf.RoundToInt(userExpected + (float)(_rng.NextDouble() * variance * 2 - variance));
            int oppScore = Mathf.RoundToInt(oppExpected + (float)(_rng.NextDouble() * variance * 2 - variance));

            // Coach adjustments affect outcomes
            if (quarter >= 3 && currentScoreDiff < -10)
            {
                // Coach makes adjustments when behind
                if (coach.InGameAdjustmentSpeed > 60)
                {
                    userScore += _rng.Next(1, 4);
                    moments.Add(new GameMoment
                    {
                        Description = $"{coach.CoachName} makes halftime adjustments that energize the team",
                        Quarter = quarter,
                        GameClock = "12:00",
                        Type = GameMoment.MomentType.CoachingAdjustment,
                        ScoreDiff = currentScoreDiff
                    });
                }
            }

            // Fourth quarter pressure
            if (quarter == 4)
            {
                int clutchModifier = (coach.ClutchPressure - 50) / 20;
                userScore += clutchModifier;
            }

            // Generate random key moment
            if (_rng.Next(100) < 30) // 30% chance per quarter
            {
                moments.Add(GenerateRandomMoment(quarter, userRoster, currentScoreDiff));
            }

            return (Math.Max(userScore, 15), Math.Max(oppScore, 15), moments);
        }

        private (int userScore, int oppScore, List<GameMoment> moments) SimulateOvertime(
            float userStrength,
            float oppStrength,
            AICoachPersonality coach,
            List<Player> userRoster,
            List<Player> oppRoster)
        {
            var moments = new List<GameMoment>();

            // OT is 5 minutes (about 1/2.4 of a quarter)
            int basePts = 11;

            float userExpected = basePts * (userStrength / 75f);
            float oppExpected = basePts * (oppStrength / 75f);

            // Clutch matters more in OT
            float clutchBonus = (coach.ClutchPressure - 50) * 0.1f;
            userExpected += clutchBonus;

            int userScore = Mathf.RoundToInt(userExpected + (float)(_rng.NextDouble() * 4 - 2));
            int oppScore = Mathf.RoundToInt(oppExpected + (float)(_rng.NextDouble() * 4 - 2));

            // Ensure scores differ
            if (userScore == oppScore)
            {
                if (_rng.Next(100) < 50 + (coach.ClutchPressure - 50))
                    userScore++;
                else
                    oppScore++;
            }

            moments.Add(new GameMoment
            {
                Description = "Overtime period begins",
                Quarter = 5,
                GameClock = "5:00",
                Type = GameMoment.MomentType.MomentumShift,
                ScoreDiff = 0
            });

            return (Math.Max(userScore, 5), Math.Max(oppScore, 5), moments);
        }

        private List<PlayerBoxScore> GenerateBoxScore(List<Player> roster, int teamScore, bool isStarters)
        {
            var boxScore = new List<PlayerBoxScore>();
            var sortedRoster = roster.OrderByDescending(p => p.Overall).ToList();

            // Distribute team score among players
            int remainingPoints = teamScore;
            int playersToScore = Math.Min(sortedRoster.Count, 10);

            // Calculate minutes based on rotation
            int totalMinutes = 240; // 5 players x 48 minutes
            var minutesDistribution = GetMinutesDistribution(sortedRoster.Count);

            for (int i = 0; i < sortedRoster.Count; i++)
            {
                var player = sortedRoster[i];
                bool started = i < 5;
                int minutes = i < minutesDistribution.Length ? minutesDistribution[i] : 0;

                if (minutes == 0) continue;

                // Calculate player stats based on their rating and minutes
                var stats = GeneratePlayerStats(player, minutes, teamScore, i == 0);

                boxScore.Add(new PlayerBoxScore
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.FullName ?? $"{player.FirstName} {player.LastName}",
                    Position = player.Position,
                    Started = started,
                    MinutesPlayed = minutes,
                    Points = stats.points,
                    FGM = stats.fgm,
                    FGA = stats.fga,
                    ThreePM = stats.threePm,
                    ThreePA = stats.threePa,
                    FTM = stats.ftm,
                    FTA = stats.fta,
                    ORB = stats.orb,
                    DRB = stats.drb,
                    Assists = stats.assists,
                    Turnovers = stats.turnovers,
                    Steals = stats.steals,
                    Blocks = stats.blocks,
                    PersonalFouls = stats.fouls,
                    PlusMinus = (started ? 5 : -2) + _rng.Next(-8, 8),
                    WasGameStar = i == 0 && stats.points >= 25
                });
            }

            return boxScore;
        }

        private int[] GetMinutesDistribution(int rosterSize)
        {
            // Standard rotation distribution
            return new[] { 36, 34, 32, 30, 28, 22, 18, 14, 10, 6, 4, 2, 0, 0, 0 };
        }

        private (int points, int fgm, int fga, int threePm, int threePa, int ftm, int fta,
                 int orb, int drb, int assists, int turnovers, int steals, int blocks, int fouls)
            GeneratePlayerStats(Player player, int minutes, int teamScore, bool isStar)
        {
            float minutesFactor = minutes / 36f;
            float skillFactor = player.Overall / 85f;

            // Points
            int basePoints = isStar ? 22 : Mathf.RoundToInt(8 * skillFactor);
            int points = Mathf.RoundToInt(basePoints * minutesFactor + _rng.Next(-4, 5));

            // Shooting
            int fga = Mathf.RoundToInt((points / 1.1f) + _rng.Next(0, 3));
            int fgm = Mathf.RoundToInt(fga * (0.35f + skillFactor * 0.15f + (float)_rng.NextDouble() * 0.1f));
            int threePa = Mathf.RoundToInt(fga * 0.35f);
            int threePm = Mathf.RoundToInt(threePa * (0.3f + (float)_rng.NextDouble() * 0.15f));
            int fta = Mathf.RoundToInt(points * 0.25f + _rng.Next(0, 3));
            int ftm = Mathf.RoundToInt(fta * (0.7f + (float)_rng.NextDouble() * 0.2f));

            // Adjust points to match shooting
            points = fgm * 2 - threePm + threePm * 3 + ftm;

            // Rebounds
            bool isBig = player.Position == Position.Center || player.Position == Position.PowerForward;
            int orb = Mathf.RoundToInt((isBig ? 2 : 0.5f) * minutesFactor + _rng.Next(0, 2));
            int drb = Mathf.RoundToInt((isBig ? 5 : 2) * minutesFactor * skillFactor + _rng.Next(-1, 2));

            // Playmaking
            bool isGuard = player.Position == Position.PointGuard || player.Position == Position.ShootingGuard;
            int assists = Mathf.RoundToInt((isGuard ? 4 : 1.5f) * minutesFactor * skillFactor + _rng.Next(-1, 2));
            int turnovers = Mathf.RoundToInt(1.5f * minutesFactor + _rng.Next(0, 2));

            // Defense
            int steals = Mathf.RoundToInt(0.8f * minutesFactor * skillFactor + (float)_rng.NextDouble());
            int blocks = Mathf.RoundToInt((isBig ? 1.2f : 0.2f) * minutesFactor * skillFactor + (float)_rng.NextDouble());

            // Fouls
            int fouls = _rng.Next(1, 4);

            return (
                Math.Max(points, 0),
                Math.Max(fgm, 0),
                Math.Max(fga, fgm),
                Math.Max(threePm, 0),
                Math.Max(threePa, threePm),
                Math.Max(ftm, 0),
                Math.Max(fta, ftm),
                Math.Max(orb, 0),
                Math.Max(drb, 0),
                Math.Max(assists, 0),
                Math.Max(turnovers, 0),
                Math.Max(steals, 0),
                Math.Max(blocks, 0),
                Math.Max(fouls, 0)
            );
        }

        private TeamBoxScore AggregateTeamStats(List<PlayerBoxScore> boxScore, int teamScore)
        {
            var stats = new TeamBoxScore
            {
                Points = teamScore
            };

            foreach (var player in boxScore)
            {
                stats.FGM += player.FGM;
                stats.FGA += player.FGA;
                stats.ThreePM += player.ThreePM;
                stats.ThreePA += player.ThreePA;
                stats.FTM += player.FTM;
                stats.FTA += player.FTA;
                stats.ORB += player.ORB;
                stats.DRB += player.DRB;
                stats.Assists += player.Assists;
                stats.Turnovers += player.Turnovers;
                stats.Steals += player.Steals;
                stats.Blocks += player.Blocks;
                stats.PersonalFouls += player.PersonalFouls;

                if (!player.Started)
                    stats.BenchPoints += player.Points;
            }

            stats.Pace = 98 + _rng.Next(-5, 6);
            stats.PointsInPaint = Mathf.RoundToInt(teamScore * 0.4f + _rng.Next(-5, 6));
            stats.FastBreakPoints = Mathf.RoundToInt(teamScore * 0.12f + _rng.Next(-3, 4));
            stats.PointsOffTurnovers = Mathf.RoundToInt(teamScore * 0.15f + _rng.Next(-3, 4));
            stats.SecondChancePoints = Mathf.RoundToInt(teamScore * 0.1f + _rng.Next(-2, 3));

            return stats;
        }

        private CoachPerformanceSummary EvaluateCoachPerformance(
            AICoachPersonality coach,
            AutonomousGameResult result,
            List<Player> roster)
        {
            var perf = new CoachPerformanceSummary();

            // Base rating on win/loss and margin
            int scoreDiff = result.UserTeamScore - result.OpponentScore;
            perf.OverallRating = 5;

            if (result.UserTeamWon)
            {
                perf.OverallRating += scoreDiff > 15 ? 3 : scoreDiff > 8 ? 2 : 1;
            }
            else
            {
                perf.OverallRating -= Math.Abs(scoreDiff) > 15 ? 3 : Math.Abs(scoreDiff) > 8 ? 2 : 1;
            }

            perf.OverallRating = Mathf.Clamp(perf.OverallRating, 1, 10);

            // Assessment text
            perf.OverallAssessment = perf.OverallRating switch
            {
                >= 9 => "Masterful coaching performance",
                >= 7 => "Solid game management",
                >= 5 => "Average performance",
                >= 3 => "Some questionable decisions",
                _ => "Poor game management"
            };

            // Timeout usage
            perf.TimeoutsCalled = 4 + _rng.Next(0, 3);
            perf.TimeoutsRemaining = 7 - perf.TimeoutsCalled;
            perf.SubstitutionsMade = 15 + _rng.Next(0, 10);
            perf.AdjustmentsMade = coach.InGameAdjustmentSpeed > 60 ? 3 + _rng.Next(0, 3) : 1 + _rng.Next(0, 2);

            // Notable decisions
            GenerateNotableDecisions(perf, coach, result);

            // Rotation summary
            perf.RotationSummary = $"Used {coach.RotationDepth}-man rotation";

            // Star minutes
            var userBox = result.GetUserTeamBoxScore();
            var topPlayer = userBox.OrderByDescending(p => p.MinutesPlayed).FirstOrDefault();
            if (topPlayer != null)
            {
                perf.StarMinutesComment = topPlayer.MinutesPlayed > 38
                    ? $"{topPlayer.PlayerName} played heavy minutes ({topPlayer.MinutesPlayed})"
                    : $"Minutes well distributed, {topPlayer.PlayerName} led with {topPlayer.MinutesPlayed}";
            }

            // Positives and concerns
            if (result.UserTeamWon)
                perf.PositiveAspects.Add("Got the win");
            if (coach.InGameAdjustmentSpeed > 60)
                perf.PositiveAspects.Add("Made effective in-game adjustments");
            if (perf.OverallRating >= 7)
                perf.PositiveAspects.Add("Overall game plan was effective");

            if (!result.UserTeamWon)
                perf.AreasOfConcern.Add("Failed to secure victory");
            if (coach.Stubbornness > 70)
                perf.AreasOfConcern.Add("May be too rigid with strategy");

            return perf;
        }

        private void GenerateNotableDecisions(CoachPerformanceSummary perf, AICoachPersonality coach, AutonomousGameResult result)
        {
            // Add some random notable decisions
            string[] goodDecisions = {
                "Called timely timeout to stop opponent run",
                "Made effective defensive switch",
                "Good late-game substitution pattern",
                "Drew up excellent ATO play"
            };

            string[] badDecisions = {
                "Slow to make adjustments",
                "Questionable rotation choices",
                "Burned timeout unnecessarily",
                "Stuck with struggling lineup too long"
            };

            // Add 1-3 decisions based on rating
            int numDecisions = _rng.Next(1, 4);
            for (int i = 0; i < numDecisions; i++)
            {
                bool good = perf.OverallRating >= 5 ? _rng.Next(100) < 70 : _rng.Next(100) < 30;
                var decisions = good ? goodDecisions : badDecisions;

                perf.NotableDecisions.Add(new CoachDecision
                {
                    Description = decisions[_rng.Next(decisions.Length)],
                    Quarter = _rng.Next(1, 5),
                    GameClock = $"{_rng.Next(1, 12)}:{_rng.Next(0, 60):D2}",
                    Quality = good ? CoachDecision.DecisionQuality.Good : CoachDecision.DecisionQuality.Questionable,
                    Outcome = good ? "Positive impact" : "Minimal impact"
                });
            }
        }

        private GameMoment GenerateRandomMoment(int quarter, List<Player> roster, int scoreDiff)
        {
            var types = Enum.GetValues(typeof(GameMoment.MomentType));
            var type = (GameMoment.MomentType)types.GetValue(_rng.Next(types.Length));

            var player = roster.OrderByDescending(p => p.Overall).Take(5).ToList()[_rng.Next(5)];

            string description = type switch
            {
                GameMoment.MomentType.BigPlay => $"{player.FirstName} {player.LastName} with a spectacular play",
                GameMoment.MomentType.LeadChange => "Lead changes hands",
                GameMoment.MomentType.Run => _rng.Next(100) < 50 ? "Team goes on a run" : "Opponent responds with a run",
                GameMoment.MomentType.ClutchPlay => $"{player.FirstName} {player.LastName} delivers in the clutch",
                GameMoment.MomentType.MomentumShift => "Momentum swings",
                _ => "Notable play"
            };

            return new GameMoment
            {
                Description = description,
                Quarter = quarter,
                GameClock = $"{_rng.Next(1, 12)}:{_rng.Next(0, 60):D2}",
                Type = type,
                ScoreDiff = scoreDiff,
                InvolvedPlayerId = player.PlayerId,
                InvolvedPlayerName = $"{player.FirstName} {player.LastName}"
            };
        }

        private string GenerateGameNarrative(AutonomousGameResult result, Team userTeam, Team opponent, AICoachPersonality coach)
        {
            string outcome = result.UserTeamWon ? "victory" : "defeat";
            string margin = Math.Abs(result.UserTeamScore - result.OpponentScore) > 15 ? "dominant" :
                           Math.Abs(result.UserTeamScore - result.OpponentScore) > 8 ? "comfortable" :
                           Math.Abs(result.UserTeamScore - result.OpponentScore) <= 3 ? "nail-biting" : "competitive";

            var topScorer = result.GetUserTeamBoxScore().OrderByDescending(p => p.Points).FirstOrDefault();
            string scorerNote = topScorer != null
                ? $"{topScorer.PlayerName} led the way with {topScorer.Points} points."
                : "";

            return $"A {margin} {outcome} against the {opponent.City} {opponent.Name}. " +
                   $"Final score: {result.UserTeamScore}-{result.OpponentScore}. " +
                   scorerNote;
        }

        private string GeneratePostGameComment(AICoachPersonality coach, AutonomousGameResult result)
        {
            bool won = result.UserTeamWon;
            int margin = Math.Abs(result.UserTeamScore - result.OpponentScore);

            if (won && margin > 15)
            {
                return coach.Temperament switch
                {
                    CoachTemperament.Calm => "The guys executed well tonight. We'll enjoy this one and get ready for the next.",
                    CoachTemperament.Fiery => "That's what I'm talking about! We came out with energy and didn't let up!",
                    CoachTemperament.Calculated => "We got the matchups we wanted and exploited them efficiently.",
                    _ => "Great team effort tonight. Everyone contributed."
                };
            }
            else if (won)
            {
                return "Got the W. Not our best performance, but we found a way. We'll clean things up in practice.";
            }
            else if (margin > 15)
            {
                return coach.Temperament switch
                {
                    CoachTemperament.Fiery => "Unacceptable. We didn't come ready to compete. That's on me.",
                    CoachTemperament.Calm => "Tough night. We'll review the film and get better.",
                    _ => "Not good enough tonight. We need to respond."
                };
            }
            else
            {
                return "Close game. Few plays here and there could have gone differently. We'll learn from this.";
            }
        }

        private AutonomousGameResult CreateForfeitResult(AutonomousGameResult result, bool userForfeits)
        {
            if (userForfeits)
            {
                result.HomeScore = result.WasHomeGame ? 0 : 20;
                result.AwayScore = result.WasHomeGame ? 20 : 0;
            }
            else
            {
                result.HomeScore = result.WasHomeGame ? 20 : 0;
                result.AwayScore = result.WasHomeGame ? 0 : 20;
            }

            result.GameNarrative = "Game forfeited due to insufficient roster.";
            return result;
        }
    }
}
