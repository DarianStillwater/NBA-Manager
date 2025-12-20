using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Generates text-based scouting reports from hidden player attributes.
    /// Report accuracy varies based on scout skill and times scouted.
    /// </summary>
    public class ScoutingReportGenerator
    {
        private readonly System.Random _rng;

        // Comparison players by position and archetype
        private static readonly Dictionary<Position, List<string>> ComparisonPlayers = new()
        {
            { Position.PointGuard, new List<string> { "Chris Paul", "Damian Lillard", "Kyrie Irving", "Tyrese Haliburton", "Jrue Holiday", "Kyle Lowry", "Derrick Rose", "Tony Parker", "Jason Kidd", "Steve Nash" }},
            { Position.ShootingGuard, new List<string> { "Klay Thompson", "Donovan Mitchell", "Devin Booker", "Bradley Beal", "Zach LaVine", "DeMar DeRozan", "Ray Allen", "Manu Ginobili", "Joe Dumars", "Reggie Miller" }},
            { Position.SmallForward, new List<string> { "Paul George", "Kawhi Leonard", "Jayson Tatum", "Jimmy Butler", "Brandon Ingram", "Khris Middleton", "Scottie Pippen", "Grant Hill", "Paul Pierce", "Tracy McGrady" }},
            { Position.PowerForward, new List<string> { "Pascal Siakam", "Draymond Green", "Julius Randle", "Jaren Jackson Jr.", "Tobias Harris", "Blake Griffin", "Chris Bosh", "Rasheed Wallace", "Amar'e Stoudemire", "Zach Randolph" }},
            { Position.Center, new List<string> { "Bam Adebayo", "Rudy Gobert", "Nikola Vucevic", "Brook Lopez", "Clint Capela", "DeAndre Jordan", "Tyson Chandler", "Marc Gasol", "Al Horford", "Dwight Howard" }}
        };

        public ScoutingReportGenerator()
        {
            _rng = new System.Random();
        }

        public ScoutingReportGenerator(int seed)
        {
            _rng = new System.Random(seed);
        }

        /// <summary>
        /// Generates a complete scouting report for a player.
        /// </summary>
        public ScoutingReport GenerateReport(Player player, UnifiedCareerProfile scout, int timesScoutedPreviously, int gamesObserved)
        {
            int totalTimesScounted = timesScoutedPreviously + 1;

            // Calculate report accuracy based on scout skill and times scouted
            float baseAccuracy = scout.GetEffectivenessForTarget(
                player.YearsPro > 0 ? ScoutingTargetType.NBAPlayer : ScoutingTargetType.CollegeProspect);

            // More scouting visits = more accuracy (diminishing returns)
            float scoutingBonus = Mathf.Min(totalTimesScounted * 0.08f, 0.25f);

            // Games observed bonus
            float gamesBonus = Mathf.Min(gamesObserved * 0.02f, 0.15f);

            float accuracy = Mathf.Clamp01(baseAccuracy + scoutingBonus + gamesBonus);

            var report = new ScoutingReport
            {
                ReportId = $"RPT_{Guid.NewGuid().ToString().Substring(0, 8)}",
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                TargetType = player.YearsPro > 0 ? ScoutingTargetType.NBAPlayer : ScoutingTargetType.CollegeProspect,
                ScoutId = scout.ProfileId,
                ScoutName = scout.PersonName,
                ScoutSkillAtGeneration = scout.OverallRating,
                GeneratedDate = DateTime.Now,
                TimesScoutedTotal = totalTimesScounted,
                GamesObservedThisReport = gamesObserved,
                InternalAccuracy = accuracy,

                // Basic info (always accurate)
                Position = player.Position.ToString(),
                HeightDisplay = FormatHeight(player.HeightInches),
                WeightDisplay = $"{player.WeightLbs} lbs",
                Age = player.Age,
                YearsProOrCollege = player.YearsPro,
                CurrentTeamOrSchool = player.TeamId ?? "Free Agent"
            };

            // Generate assessments
            report.Physical = GeneratePhysicalAssessment(player, accuracy);
            report.OffensiveSkills = GenerateOffensiveAssessment(player, accuracy);
            report.DefensiveSkills = GenerateDefensiveAssessment(player, accuracy);
            report.PlaymakingSkills = GeneratePlaymakingAssessment(player, accuracy);
            report.AthleticProfile = GenerateAthleticAssessment(player, accuracy);
            report.MentalProfile = GenerateMentalAssessment(player, accuracy);
            report.Personality = GeneratePersonalityAssessment(player, accuracy, scout.CharacterJudgment);
            report.Potential = GeneratePotentialAssessment(player, accuracy, scout.PotentialAssessment);

            // Generate strengths and weaknesses
            GenerateStrengthsAndWeaknesses(player, report, accuracy);

            // Generate summaries
            report.OverallSummary = GenerateOverallSummary(player, report, accuracy);
            report.ProjectedRole = GenerateProjectedRole(player, accuracy);
            report.ComparisonPlayer = GenerateComparison(player, accuracy);
            report.BiggestStrength = report.Strengths.Count > 0 ? report.Strengths[0] : "No clear strength identified";
            report.BiggestConcern = report.Weaknesses.Count > 0 ? report.Weaknesses[0] : "No major concerns";

            // Generate recommendations
            if (player.YearsPro == 0)
            {
                report.DraftRec = GenerateDraftRecommendation(player, report, accuracy);
            }
            else
            {
                report.TradeRec = GenerateTradeRecommendation(player, report, accuracy);
            }

            return report;
        }

        // ==================== PHYSICAL ASSESSMENT ====================

        private PhysicalAssessment GeneratePhysicalAssessment(Player player, float accuracy)
        {
            int perceivedWingspan = ApplyAccuracyVariance(player.Wingspan, accuracy);

            string wingspanDesc = perceivedWingspan switch
            {
                >= 85 => "Elite wingspan - significant length advantage",
                >= 75 => "Plus wingspan for the position",
                >= 60 => "Average wingspan",
                >= 45 => "Below average length",
                _ => "Short wingspan - length concerns"
            };

            int perceivedStrength = ApplyAccuracyVariance(player.Strength, accuracy);
            string bodyType = perceivedStrength switch
            {
                >= 80 => "Powerful, muscular build",
                >= 70 => "Strong, well-built frame",
                >= 55 => "Average NBA body",
                >= 40 => "Slight frame, needs to add strength",
                _ => "Undersized, physical concerns"
            };

            int athleticAvg = (player.Speed + player.Acceleration + player.Vertical) / 3;
            int perceivedAthleticism = ApplyAccuracyVariance(athleticAvg, accuracy);
            string athleticGrade = SkillGradeDescriptors.GetGrade(perceivedAthleticism);

            return new PhysicalAssessment
            {
                Wingspan = wingspanDesc,
                BodyType = bodyType,
                AthleticismGrade = athleticGrade,
                Notes = GeneratePhysicalNotes(player, accuracy)
            };
        }

        private string GeneratePhysicalNotes(Player player, float accuracy)
        {
            var notes = new List<string>();

            int perceivedVertical = ApplyAccuracyVariance(player.Vertical, accuracy);
            if (perceivedVertical >= 80)
                notes.Add("Exceptional leaping ability - can play above the rim.");
            else if (perceivedVertical <= 40)
                notes.Add("Limited verticality affects finishing at the rim.");

            int perceivedSpeed = ApplyAccuracyVariance(player.Speed, accuracy);
            if (perceivedSpeed >= 85)
                notes.Add("Elite foot speed - one of the fastest at his position.");
            else if (perceivedSpeed <= 45)
                notes.Add("Lacks foot speed to stay in front of quicker players.");

            int perceivedDurability = ApplyAccuracyVariance(player.Durability, accuracy);
            if (perceivedDurability <= 50)
                notes.Add("Durability concerns - history suggests injury risk.");
            else if (perceivedDurability >= 85)
                notes.Add("Iron man - rarely misses games.");

            return notes.Count > 0 ? string.Join(" ", notes) : "Standard physical profile for the position.";
        }

        // ==================== OFFENSIVE ASSESSMENT ====================

        private SkillAssessment GenerateOffensiveAssessment(Player player, float accuracy)
        {
            var skills = new List<SkillGrade>
            {
                new SkillGrade
                {
                    SkillName = "Finishing at the Rim",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Finishing_Rim, accuracy, _rng),
                    Notes = GenerateFinishingNotes(player, accuracy)
                },
                new SkillGrade
                {
                    SkillName = "Mid-Range Game",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Shot_MidRange, accuracy, _rng),
                    Notes = GenerateMidRangeNotes(player, accuracy)
                },
                new SkillGrade
                {
                    SkillName = "Three-Point Shooting",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Shot_Three, accuracy, _rng),
                    Notes = GenerateThreePointNotes(player, accuracy)
                },
                new SkillGrade
                {
                    SkillName = "Free Throw Shooting",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.FreeThrow, accuracy, _rng),
                    Notes = ""
                },
                new SkillGrade
                {
                    SkillName = "Post Game",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Finishing_PostMoves, accuracy, _rng),
                    Notes = GeneratePostGameNotes(player, accuracy)
                }
            };

            int offenseAvg = (player.Finishing_Rim + player.Shot_Three + player.Shot_MidRange + player.OffensiveIQ) / 4;
            string summary = GenerateOffensiveSummary(player, offenseAvg, accuracy);

            return new SkillAssessment
            {
                Category = "Offensive Skills",
                Skills = skills,
                Summary = summary
            };
        }

        private string GenerateFinishingNotes(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.Finishing_Rim, accuracy);
            if (perceived >= 85) return "Finishes through contact with either hand. Dunker.";
            if (perceived >= 70) return "Reliable finisher with good touch.";
            if (perceived >= 55) return "Can finish in space but struggles through contact.";
            return "Finishing needs significant work.";
        }

        private string GenerateMidRangeNotes(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.Shot_MidRange, accuracy);
            if (perceived >= 85) return "Excellent pull-up game. Can score from anywhere.";
            if (perceived >= 70) return "Comfortable from mid-range. Good footwork.";
            if (perceived >= 55) return "Will take mid-range shots but inconsistent.";
            return "Mid-range is not part of his game.";
        }

        private string GenerateThreePointNotes(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.Shot_Three, accuracy);
            if (perceived >= 90) return "Elite shooter. Gravity changes defenses. Can shoot off movement.";
            if (perceived >= 80) return "Reliable three-point threat. Good catch-and-shoot.";
            if (perceived >= 65) return "Capable shooter but not a primary threat.";
            if (perceived >= 50) return "Will take open threes but streaky.";
            return "Defenses will sag off him. Limited range.";
        }

        private string GeneratePostGameNotes(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.Finishing_PostMoves, accuracy);
            if (perceived >= 80) return "Diverse post moves. Can score over either shoulder.";
            if (perceived >= 65) return "Has a few go-to moves in the post.";
            if (perceived >= 50) return "Will post up smaller defenders but limited moves.";
            return "Not a post scorer.";
        }

        private string GenerateOffensiveSummary(Player player, int offenseAvg, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(offenseAvg, accuracy);
            if (perceived >= 85)
                return "Elite offensive player. Can score at all three levels and create for himself and others.";
            if (perceived >= 75)
                return "Very good scorer with multiple ways to impact the game offensively.";
            if (perceived >= 65)
                return "Solid offensive contributor who can fill a role in an NBA offense.";
            if (perceived >= 55)
                return "Limited offensive game but can contribute in specific situations.";
            return "Offensive game needs significant development to be NBA-caliber.";
        }

        // ==================== DEFENSIVE ASSESSMENT ====================

        private SkillAssessment GenerateDefensiveAssessment(Player player, float accuracy)
        {
            var skills = new List<SkillGrade>
            {
                new SkillGrade
                {
                    SkillName = "Perimeter Defense",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Defense_Perimeter, accuracy, _rng),
                    Notes = GeneratePerimeterDefenseNotes(player, accuracy)
                },
                new SkillGrade
                {
                    SkillName = "Interior Defense",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Defense_Interior, accuracy, _rng),
                    Notes = GenerateInteriorDefenseNotes(player, accuracy)
                },
                new SkillGrade
                {
                    SkillName = "Shot Blocking",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Block, accuracy, _rng),
                    Notes = ""
                },
                new SkillGrade
                {
                    SkillName = "Steal/Ball Disruption",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Steal, accuracy, _rng),
                    Notes = ""
                },
                new SkillGrade
                {
                    SkillName = "Defensive Rebounding",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.DefensiveRebound, accuracy, _rng),
                    Notes = ""
                }
            };

            int defenseAvg = (player.Defense_Perimeter + player.Defense_Interior + player.DefensiveIQ) / 3;
            string summary = GenerateDefensiveSummary(player, defenseAvg, accuracy);

            return new SkillAssessment
            {
                Category = "Defensive Skills",
                Skills = skills,
                Summary = summary
            };
        }

        private string GeneratePerimeterDefenseNotes(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.Defense_Perimeter, accuracy);
            if (perceived >= 85) return "Lockdown perimeter defender. Can guard 1-3.";
            if (perceived >= 70) return "Good on-ball defender. Active hands.";
            if (perceived >= 55) return "Adequate defender who gives effort.";
            return "Struggles to stay in front of quicker guards.";
        }

        private string GenerateInteriorDefenseNotes(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.Defense_Interior, accuracy);
            if (perceived >= 85) return "Elite rim protector. Alters shots.";
            if (perceived >= 70) return "Good interior presence. Holds ground.";
            if (perceived >= 55) return "Serviceable interior defender.";
            return "Gets pushed around in the post.";
        }

        private string GenerateDefensiveSummary(Player player, int defenseAvg, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(defenseAvg, accuracy);
            if (perceived >= 85)
                return "Elite defender who can anchor a defense. DPOY-caliber potential.";
            if (perceived >= 75)
                return "Very good defender who can guard multiple positions.";
            if (perceived >= 65)
                return "Solid defender who won't hurt you on that end.";
            if (perceived >= 55)
                return "Average defender. Will need help in certain matchups.";
            return "Defensive liability. Will be targeted by opponents.";
        }

        // ==================== PLAYMAKING ASSESSMENT ====================

        private SkillAssessment GeneratePlaymakingAssessment(Player player, float accuracy)
        {
            var skills = new List<SkillGrade>
            {
                new SkillGrade
                {
                    SkillName = "Passing Vision",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Passing, accuracy, _rng),
                    Notes = GeneratePassingNotes(player, accuracy)
                },
                new SkillGrade
                {
                    SkillName = "Ball Handling",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.BallHandling, accuracy, _rng),
                    Notes = GenerateBallHandlingNotes(player, accuracy)
                },
                new SkillGrade
                {
                    SkillName = "Offensive IQ",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.OffensiveIQ, accuracy, _rng),
                    Notes = ""
                }
            };

            int playmakingAvg = (player.Passing + player.BallHandling + player.OffensiveIQ) / 3;

            return new SkillAssessment
            {
                Category = "Playmaking",
                Skills = skills,
                Summary = GeneratePlaymakingSummary(playmakingAvg, accuracy)
            };
        }

        private string GeneratePassingNotes(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.Passing, accuracy);
            if (perceived >= 85) return "Elite passer. Sees plays before they develop.";
            if (perceived >= 70) return "Good passer who can run an offense.";
            if (perceived >= 55) return "Makes the simple pass. Limited creativity.";
            return "Turnover prone. Limited court vision.";
        }

        private string GenerateBallHandlingNotes(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.BallHandling, accuracy);
            if (perceived >= 85) return "Elite handle. Can break down any defender.";
            if (perceived >= 70) return "Good ball handler. Can create separation.";
            if (perceived >= 55) return "Functional handle for his position.";
            return "Loose handle. Prone to turnovers under pressure.";
        }

        private string GeneratePlaymakingSummary(int playmakingAvg, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(playmakingAvg, accuracy);
            if (perceived >= 85)
                return "Elite playmaker who can run an offense. Makes teammates better.";
            if (perceived >= 70)
                return "Good secondary creator. Can make plays in pick-and-roll.";
            if (perceived >= 55)
                return "Limited playmaking but can make the right pass.";
            return "Not a creator. Needs plays run for him.";
        }

        // ==================== ATHLETIC ASSESSMENT ====================

        private SkillAssessment GenerateAthleticAssessment(Player player, float accuracy)
        {
            var skills = new List<SkillGrade>
            {
                new SkillGrade
                {
                    SkillName = "Speed",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Speed, accuracy, _rng),
                    Notes = ""
                },
                new SkillGrade
                {
                    SkillName = "Acceleration/First Step",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Acceleration, accuracy, _rng),
                    Notes = ""
                },
                new SkillGrade
                {
                    SkillName = "Vertical Leap",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Vertical, accuracy, _rng),
                    Notes = ""
                },
                new SkillGrade
                {
                    SkillName = "Strength",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Strength, accuracy, _rng),
                    Notes = ""
                },
                new SkillGrade
                {
                    SkillName = "Stamina/Motor",
                    Grade = SkillGradeDescriptors.GetGradeWithAccuracy(player.Stamina, accuracy, _rng),
                    Notes = ""
                }
            };

            return new SkillAssessment
            {
                Category = "Athletic Profile",
                Skills = skills,
                Summary = GenerateAthleticSummary(player, accuracy)
            };
        }

        private string GenerateAthleticSummary(Player player, float accuracy)
        {
            int athleticAvg = (player.Speed + player.Acceleration + player.Vertical + player.Strength) / 4;
            int perceived = ApplyAccuracyVariance(athleticAvg, accuracy);

            if (perceived >= 85) return "Elite athlete. Top-tier physical tools.";
            if (perceived >= 75) return "Very good athlete. Above average in most areas.";
            if (perceived >= 60) return "Solid athlete. Won't wow you but functional.";
            if (perceived >= 45) return "Below average athlete. Will need to rely on skill.";
            return "Limited athletic profile. Significant physical concerns.";
        }

        // ==================== MENTAL ASSESSMENT ====================

        private MentalAssessment GenerateMentalAssessment(Player player, float accuracy)
        {
            return new MentalAssessment
            {
                BasketballIQ = GetMentalGrade(player.BasketballIQ, accuracy, "basketball IQ"),
                Coachability = GetMentalGrade(player.Coachability, accuracy, "coachability"),
                WorkEthic = GetMentalGrade(player.WorkEthic, accuracy, "work ethic"),
                Composure = GetMentalGrade(player.Composure, accuracy, "composure"),
                Consistency = GetMentalGrade(player.Consistency, accuracy, "consistency"),
                Summary = GenerateMentalSummary(player, accuracy)
            };
        }

        private string GetMentalGrade(int value, float accuracy, string attribute)
        {
            int perceived = ApplyAccuracyVariance(value, accuracy);
            return attribute switch
            {
                "basketball IQ" => perceived switch
                {
                    >= 85 => "High IQ - sees the game at a different level",
                    >= 70 => "Good IQ - makes smart decisions",
                    >= 55 => "Average IQ - occasional mental lapses",
                    _ => "Needs coaching - makes too many mental errors"
                },
                "coachability" => perceived switch
                {
                    >= 85 => "Extremely coachable - sponge for knowledge",
                    >= 70 => "Very coachable - responds well to feedback",
                    >= 55 => "Coachable but can be stubborn at times",
                    _ => "Difficult to coach - resistant to change"
                },
                "work ethic" => perceived switch
                {
                    >= 85 => "Gym rat - first one in, last one out",
                    >= 70 => "Good worker - takes preparation seriously",
                    >= 55 => "Average work ethic - does what's required",
                    _ => "Questions about motor and dedication"
                },
                "composure" => perceived switch
                {
                    >= 85 => "Ice in his veins - thrives in big moments",
                    >= 70 => "Handles pressure well",
                    >= 55 => "Can get rattled in high-pressure situations",
                    _ => "Struggles with pressure - visible frustration"
                },
                "consistency" => perceived switch
                {
                    >= 85 => "Rock solid - know what you're getting",
                    >= 70 => "Generally consistent performer",
                    >= 55 => "Streaky - hot and cold stretches",
                    _ => "Highly inconsistent - hard to rely on"
                },
                _ => "Unknown"
            };
        }

        private string GenerateMentalSummary(Player player, float accuracy)
        {
            int mentalAvg = (player.BasketballIQ + player.Coachability + player.WorkEthic + player.Composure) / 4;
            int perceived = ApplyAccuracyVariance(mentalAvg, accuracy);

            if (perceived >= 85) return "Elite mental makeup. Coach's dream.";
            if (perceived >= 70) return "Strong mentals. Will maximize his talent.";
            if (perceived >= 55) return "Average mental profile. Some areas to work on.";
            return "Mental concerns. May not reach potential.";
        }

        // ==================== PERSONALITY ASSESSMENT ====================

        private PersonalityAssessment GeneratePersonalityAssessment(Player player, float accuracy, int characterJudgment)
        {
            // Character judgment affects how well we read personality
            float personalityAccuracy = accuracy * (characterJudgment / 100f);

            var traits = new List<string>();

            if (ApplyAccuracyVariance(player.Leadership, personalityAccuracy) >= 75)
                traits.Add("Natural leader");
            if (ApplyAccuracyVariance(player.Ego, personalityAccuracy) >= 80)
                traits.Add("High ego - wants the spotlight");
            if (ApplyAccuracyVariance(player.Aggression, personalityAccuracy) >= 75)
                traits.Add("Intense competitor");
            if (ApplyAccuracyVariance(player.Composure, personalityAccuracy) >= 80)
                traits.Add("Cool under pressure");

            int perceivedLeadership = ApplyAccuracyVariance(player.Leadership, personalityAccuracy);
            string leadershipProfile = perceivedLeadership switch
            {
                >= 85 => "Vocal leader - commands the locker room",
                >= 70 => "Leads by example",
                >= 55 => "Good teammate but not a leader",
                _ => "Follower - needs strong leadership around him"
            };

            int perceivedEgo = ApplyAccuracyVariance(player.Ego, personalityAccuracy);
            string characterConcerns = perceivedEgo >= 85 ? "Ego could be an issue - may cause locker room friction" :
                                       perceivedEgo <= 30 ? "Very low ego - may lack killer instinct" :
                                       "No significant character concerns";

            return new PersonalityAssessment
            {
                LeadershipProfile = leadershipProfile,
                CharacterConcerns = characterConcerns,
                TeammateDynamics = GenerateTeammateDynamics(player, personalityAccuracy),
                MediaPresence = GenerateMediaPresence(player, personalityAccuracy),
                MotivationAssessment = GenerateMotivation(player, personalityAccuracy),
                PersonalityTraitsIdentified = traits
            };
        }

        private string GenerateTeammateDynamics(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance((player.Leadership + (100 - player.Ego)) / 2, accuracy);
            if (perceived >= 75) return "Great teammate - lifts up those around him";
            if (perceived >= 55) return "Good teammate - fits into most locker rooms";
            return "Can be difficult - may need the right environment";
        }

        private string GenerateMediaPresence(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.Composure, accuracy);
            if (perceived >= 75) return "Handles media well - professional in interviews";
            if (perceived >= 50) return "Standard media presence";
            return "Avoids spotlight - may struggle with media attention";
        }

        private string GenerateMotivation(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.WorkEthic, accuracy);
            if (perceived >= 85) return "Extremely motivated - driven to be great";
            if (perceived >= 70) return "Well motivated - wants to improve";
            if (perceived >= 50) return "Adequately motivated";
            return "Questions about long-term motivation";
        }

        // ==================== POTENTIAL ASSESSMENT ====================

        private PotentialAssessment GeneratePotentialAssessment(Player player, float accuracy, int potentialJudgment)
        {
            // This is a rough estimate since we don't have the actual potential yet
            // In the real implementation, this would reference the hidden potential attribute
            float potentialAccuracy = accuracy * (potentialJudgment / 100f);

            int currentOverall = player.OverallRating;
            int estimatedPotential = currentOverall + _rng.Next(5, 20); // Placeholder
            int perceived = ApplyAccuracyVariance(estimatedPotential, potentialAccuracy);

            string ceiling = perceived switch
            {
                >= 95 => "Generational talent ceiling",
                >= 90 => "Franchise player potential",
                >= 85 => "All-Star ceiling",
                >= 80 => "High-end starter potential",
                >= 75 => "Quality starter ceiling",
                >= 70 => "Solid rotation player",
                >= 65 => "Role player ceiling",
                _ => "Limited upside"
            };

            string floor = currentOverall switch
            {
                >= 75 => "Floor is a quality rotation player",
                >= 65 => "At worst, a serviceable bench piece",
                >= 55 => "Floor is end of roster",
                _ => "May not stick in the league"
            };

            int yearsToProject = player.Age < 22 ? 3 : player.Age < 25 ? 2 : 1;
            string timeline = player.Age switch
            {
                <= 20 => "2-3 years from reaching potential",
                <= 22 => "1-2 years from peak development",
                <= 25 => "Should be approaching prime soon",
                _ => "What you see is what you get"
            };

            return new PotentialAssessment
            {
                CeilingDescription = ceiling,
                FloorDescription = floor,
                DevelopmentOutlook = GenerateDevelopmentOutlook(player, potentialAccuracy),
                TimelineEstimate = timeline,
                KeyDevelopmentAreas = GenerateKeyDevelopmentAreas(player),
                RiskLevel = GenerateRiskLevel(player, potentialAccuracy)
            };
        }

        private string GenerateDevelopmentOutlook(Player player, float accuracy)
        {
            int workEthic = ApplyAccuracyVariance(player.WorkEthic, accuracy);
            int coachability = ApplyAccuracyVariance(player.Coachability, accuracy);
            int avg = (workEthic + coachability) / 2;

            if (avg >= 80) return "Excellent development candidate - has the tools and mindset";
            if (avg >= 65) return "Good development potential - should continue improving";
            if (avg >= 50) return "May develop but nothing guaranteed";
            return "Development concerns - may not reach ceiling";
        }

        private string GenerateKeyDevelopmentAreas(Player player)
        {
            var areas = new List<string>();

            if (player.Shot_Three < 60) areas.Add("three-point shooting");
            if (player.Defense_Perimeter < 55) areas.Add("perimeter defense");
            if (player.BallHandling < 55) areas.Add("ball handling");
            if (player.Strength < 50) areas.Add("strength/physicality");
            if (player.FreeThrow < 65) areas.Add("free throw shooting");

            if (areas.Count == 0) return "Well-rounded - continue refining current skills";
            return $"Key areas: {string.Join(", ", areas)}";
        }

        private string GenerateRiskLevel(Player player, float accuracy)
        {
            int durability = ApplyAccuracyVariance(player.Durability, accuracy);
            int consistency = ApplyAccuracyVariance(player.Consistency, accuracy);

            if (durability < 50 || consistency < 45)
                return "High risk - injury or consistency concerns";
            if (durability < 65 || consistency < 55)
                return "Moderate risk - some concerns to monitor";
            return "Low risk - should be reliable contributor";
        }

        // ==================== STRENGTHS & WEAKNESSES ====================

        private void GenerateStrengthsAndWeaknesses(Player player, ScoutingReport report, float accuracy)
        {
            // Identify top attributes (strengths)
            var attributes = new Dictionary<string, int>
            {
                {"Three-point shooting", player.Shot_Three},
                {"Finishing at the rim", player.Finishing_Rim},
                {"Mid-range game", player.Shot_MidRange},
                {"Ball handling", player.BallHandling},
                {"Passing/playmaking", player.Passing},
                {"Perimeter defense", player.Defense_Perimeter},
                {"Interior defense", player.Defense_Interior},
                {"Shot blocking", player.Block},
                {"Rebounding", player.DefensiveRebound},
                {"Athleticism", (player.Speed + player.Vertical) / 2},
                {"Basketball IQ", player.BasketballIQ},
                {"Leadership", player.Leadership}
            };

            foreach (var attr in attributes)
            {
                int perceived = ApplyAccuracyVariance(attr.Value, accuracy);
                if (perceived >= 80)
                    report.Strengths.Add(attr.Key);
                else if (perceived >= 70)
                    report.AreasToWatch.Add($"{attr.Key} (solid)");
                else if (perceived <= 45)
                    report.Weaknesses.Add(attr.Key);
            }

            // Cap at reasonable numbers
            if (report.Strengths.Count > 5)
                report.Strengths = report.Strengths.GetRange(0, 5);
            if (report.Weaknesses.Count > 4)
                report.Weaknesses = report.Weaknesses.GetRange(0, 4);
        }

        // ==================== RECOMMENDATIONS ====================

        private DraftRecommendation GenerateDraftRecommendation(Player player, ScoutingReport report, float accuracy)
        {
            int overall = player.OverallRating;
            int perceived = ApplyAccuracyVariance(overall, accuracy);

            string range = perceived switch
            {
                >= 85 => "Top 5 pick",
                >= 80 => "Lottery selection (1-14)",
                >= 75 => "Mid first-round (15-22)",
                >= 70 => "Late first-round (23-30)",
                >= 65 => "Early second-round",
                >= 55 => "Late second-round flyer",
                _ => "Undrafted - training camp invite"
            };

            string verdict = perceived switch
            {
                >= 80 => "Strongly recommend - potential franchise piece",
                >= 70 => "Recommend - should contribute immediately",
                >= 60 => "Worth drafting at value - development project",
                >= 50 => "Proceed with caution - significant risk",
                _ => "Pass - doesn't project as NBA player"
            };

            return new DraftRecommendation
            {
                ProjectedRange = range,
                ValueAssessment = GenerateDraftValue(player, perceived),
                FitWithTeam = "Needs evaluation based on team needs",
                FinalVerdict = verdict
            };
        }

        private string GenerateDraftValue(Player player, int perceived)
        {
            if (perceived >= 75 && player.Age <= 20)
                return "High upside pick - worth reaching for";
            if (perceived >= 70)
                return "Good value - solid pick at projected position";
            if (perceived >= 60)
                return "Value pick - could outperform draft position";
            return "Low value - high bust potential";
        }

        private TradeRecommendation GenerateTradeRecommendation(Player player, ScoutingReport report, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.OverallRating, accuracy);

            string value = perceived switch
            {
                >= 85 => "Extremely high value - untouchable unless massive return",
                >= 75 => "High trade value - would require significant assets",
                >= 65 => "Solid trade value - useful piece",
                >= 55 => "Moderate value - moveable for right price",
                _ => "Low value - may need to attach assets to move"
            };

            string priority = perceived switch
            {
                >= 80 => "Top acquisition target",
                >= 70 => "Strong acquisition target",
                >= 60 => "Nice to have if price is right",
                >= 50 => "Depth piece - not a priority",
                _ => "Avoid acquiring"
            };

            return new TradeRecommendation
            {
                CurrentValue = value,
                ContractAssessment = "Contract details not evaluated in this report",
                AcquisitionPriority = priority,
                TradeNotes = GenerateTradeNotes(player, perceived)
            };
        }

        private string GenerateTradeNotes(Player player, int perceived)
        {
            if (perceived >= 80 && player.Age <= 26)
                return "Young cornerstone - building block for any team";
            if (perceived >= 70 && player.Age >= 30)
                return "Veteran contributor - good for win-now teams";
            if (perceived >= 60)
                return "Solid role player - fills a need for most teams";
            return "Marginal value - situational fit only";
        }

        // ==================== SUMMARY GENERATION ====================

        private string GenerateOverallSummary(Player player, ScoutingReport report, float accuracy)
        {
            var sb = new StringBuilder();

            int overall = ApplyAccuracyVariance(player.OverallRating, accuracy);

            // Opening
            if (overall >= 85)
                sb.Append($"{player.FullName} projects as an elite NBA player. ");
            else if (overall >= 75)
                sb.Append($"{player.FullName} is a quality NBA player who can contribute at a high level. ");
            else if (overall >= 65)
                sb.Append($"{player.FullName} projects as a solid rotation player. ");
            else if (overall >= 55)
                sb.Append($"{player.FullName} could carve out a role as a depth piece. ");
            else
                sb.Append($"{player.FullName} faces an uphill battle to stick in the NBA. ");

            // Best skill
            if (report.Strengths.Count > 0)
                sb.Append($"Best skill is {report.Strengths[0].ToLower()}. ");

            // Biggest concern
            if (report.Weaknesses.Count > 0)
                sb.Append($"Main concern is {report.Weaknesses[0].ToLower()}.");

            return sb.ToString();
        }

        private string GenerateProjectedRole(Player player, float accuracy)
        {
            int perceived = ApplyAccuracyVariance(player.OverallRating, accuracy);
            return perceived switch
            {
                >= 90 => "Franchise Player",
                >= 85 => "All-Star Starter",
                >= 80 => "Quality Starter",
                >= 75 => "Starter",
                >= 70 => "Sixth Man / Key Rotation",
                >= 65 => "Rotation Player",
                >= 60 => "End of Rotation",
                >= 55 => "End of Bench",
                _ => "Two-Way / G-League"
            };
        }

        private string GenerateComparison(Player player, float accuracy)
        {
            if (!ComparisonPlayers.TryGetValue(player.Position, out var comparisons))
                return "Unique player - no clear comparison";

            // Pick a somewhat random but position-appropriate comparison
            int index = Math.Abs(player.OverallRating + player.Age) % comparisons.Count;
            return $"Reminds me of a young {comparisons[index]}";
        }

        // ==================== UTILITY ====================

        private int ApplyAccuracyVariance(int trueValue, float accuracy)
        {
            // Higher accuracy = less variance
            float maxVariance = (1f - accuracy) * 20f;  // Up to 20 point variance at 0% accuracy
            int variance = (int)((_rng.NextDouble() * 2 - 1) * maxVariance);
            return Mathf.Clamp(trueValue + variance, 0, 100);
        }

        private string FormatHeight(float inches)
        {
            int feet = (int)(inches / 12);
            int remainingInches = (int)(inches % 12);
            return $"{feet}'{remainingInches}\"";
        }
    }
}
