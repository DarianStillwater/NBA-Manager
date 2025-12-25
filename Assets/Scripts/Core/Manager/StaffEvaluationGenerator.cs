using System.Collections.Generic;
using System.Linq;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Generates text-based evaluations for staff members.
    /// Follows the "no visible attributes" design philosophy - all numeric attributes
    /// are converted to descriptive text assessments.
    /// </summary>
    public class StaffEvaluationGenerator
    {
        private static StaffEvaluationGenerator _instance;
        public static StaffEvaluationGenerator Instance => _instance ??= new StaffEvaluationGenerator();

        /// <summary>
        /// Generate a complete evaluation for a staff member.
        /// </summary>
        public StaffEvaluation GenerateEvaluation(UnifiedCareerProfile profile)
        {
            if (profile == null) return null;

            var eval = new StaffEvaluation
            {
                ProfileId = profile.ProfileId,
                PersonName = profile.PersonName,
                Role = profile.CurrentRole,
                Tier = EvaluationTierExtensions.FromRating(profile.OverallRating)
            };

            if (profile.CurrentTrack == UnifiedCareerTrack.Coaching)
            {
                GenerateCoachingEvaluation(profile, eval);
            }
            else if (profile.CurrentTrack == UnifiedCareerTrack.FrontOffice)
            {
                GenerateScoutingEvaluation(profile, eval);
            }

            eval.CareerTrajectory = GenerateCareerTrajectory(profile);
            eval.PotentialAssessment = GeneratePotentialAssessment(profile);

            return eval;
        }

        #region Coaching Evaluation

        private void GenerateCoachingEvaluation(UnifiedCareerProfile profile, StaffEvaluation eval)
        {
            // Generate skill assessments
            eval.SkillAssessments.Add(GenerateSkillAssessment("Offensive Schemes", profile.OffensiveScheme, GenerateOffensiveSchemeNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Defensive Schemes", profile.DefensiveScheme, GenerateDefensiveSchemeNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Game Management", profile.GameManagement, GenerateGameManagementNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Player Development", profile.PlayerDevelopment, GeneratePlayerDevelopmentNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Motivation", profile.Motivation, GenerateMotivationNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Communication", profile.Communication, GenerateCommunicationNotes));

            // Generate summaries
            eval.OverallAssessment = GenerateCoachingOverallAssessment(profile);
            eval.StrengthsSummary = GenerateCoachingStrengths(profile);
            eval.WeaknessesSummary = GenerateCoachingWeaknesses(profile);
        }

        private string GenerateCoachingOverallAssessment(UnifiedCareerProfile profile)
        {
            int rating = profile.OverallRating;
            string experience = profile.TotalCoachingYears > 15 ? "veteran" :
                                profile.TotalCoachingYears > 7 ? "experienced" :
                                profile.TotalCoachingYears > 3 ? "developing" : "young";

            return rating switch
            {
                >= 90 => $"An elite {experience} coach capable of leading championship contenders.",
                >= 85 => $"An excellent {experience} coach who consistently gets the most out of his teams.",
                >= 80 => $"A very good {experience} coach with strong fundamentals across the board.",
                >= 75 => $"A quality {experience} coach who can lead competitive teams.",
                >= 70 => $"A solid {experience} coach with some notable strengths.",
                >= 65 => $"An adequate {experience} coach still developing his craft.",
                >= 60 => $"A serviceable {experience} coach with room for improvement.",
                >= 55 => $"A below-average {experience} coach who struggles in key areas.",
                _ => $"A {experience} coach who needs significant development."
            };
        }

        private string GenerateCoachingStrengths(UnifiedCareerProfile profile)
        {
            var strengths = new List<string>();

            if (profile.OffensiveScheme >= 75)
                strengths.Add("excellent offensive play design");
            else if (profile.OffensiveScheme >= 65)
                strengths.Add("solid offensive concepts");

            if (profile.DefensiveScheme >= 75)
                strengths.Add("strong defensive systems");
            else if (profile.DefensiveScheme >= 65)
                strengths.Add("sound defensive principles");

            if (profile.PlayerDevelopment >= 75)
                strengths.Add("exceptional player development");
            else if (profile.PlayerDevelopment >= 65)
                strengths.Add("good track record developing talent");

            if (profile.GameManagement >= 75)
                strengths.Add("excellent in-game decision making");
            else if (profile.GameManagement >= 65)
                strengths.Add("solid game management");

            if (profile.Motivation >= 75)
                strengths.Add("ability to inspire and motivate players");

            if (profile.Communication >= 75)
                strengths.Add("clear communication with players and staff");

            if (strengths.Count == 0)
                return "Still searching for signature strengths. Needs time to develop a clear coaching identity.";

            return $"Known for {string.Join(", ", strengths.Take(3))}. {GetStrengthContext(profile)}";
        }

        private string GetStrengthContext(UnifiedCareerProfile profile)
        {
            if (profile.OverallRating >= 80)
                return "Coaches consistently praise his preparation and attention to detail.";
            if (profile.OverallRating >= 70)
                return "Has shown the ability to get players to buy into his system.";
            return "Shows flashes of potential that could develop further.";
        }

        private string GenerateCoachingWeaknesses(UnifiedCareerProfile profile)
        {
            var weaknesses = new List<string>();

            if (profile.OffensiveScheme < 55)
                weaknesses.Add("offensive schemes that can be predictable");
            else if (profile.OffensiveScheme < 65)
                weaknesses.Add("occasional struggles with offensive adjustments");

            if (profile.DefensiveScheme < 55)
                weaknesses.Add("defensive systems that can be exploited");
            else if (profile.DefensiveScheme < 65)
                weaknesses.Add("inconsistent defensive schemes");

            if (profile.GameManagement < 55)
                weaknesses.Add("questionable in-game decisions");
            else if (profile.GameManagement < 65)
                weaknesses.Add("sometimes slow to make adjustments");

            if (profile.PlayerDevelopment < 55)
                weaknesses.Add("limited track record developing young players");

            if (profile.Motivation < 55)
                weaknesses.Add("difficulty keeping players engaged");

            if (profile.Communication < 55)
                weaknesses.Add("communication issues with players");

            if (weaknesses.Count == 0)
                return "No significant weaknesses identified. A well-rounded coach.";

            return $"Areas of concern include {string.Join(", ", weaknesses.Take(3))}. {GetWeaknessContext(profile)}";
        }

        private string GetWeaknessContext(UnifiedCareerProfile profile)
        {
            if (profile.TotalCoachingYears < 5)
                return "These are common areas for young coaches to develop.";
            if (profile.OverallRating < 60)
                return "Has struggled to address these issues throughout his career.";
            return "Manageable concerns that can be offset by his strengths.";
        }

        #endregion

        #region Scouting Evaluation

        private void GenerateScoutingEvaluation(UnifiedCareerProfile profile, StaffEvaluation eval)
        {
            // Generate skill assessments
            eval.SkillAssessments.Add(GenerateSkillAssessment("Evaluation Accuracy", profile.EvaluationAccuracy, GenerateEvaluationAccuracyNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Prospect Scouting", profile.ProspectEvaluation, GenerateProspectScoutingNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Pro Scouting", profile.ProEvaluation, GenerateProScoutingNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Potential Assessment", profile.PotentialAssessment, GeneratePotentialScoutingNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Work Rate", profile.WorkRate, GenerateWorkRateNotes));
            eval.SkillAssessments.Add(GenerateSkillAssessment("Attention to Detail", profile.AttentionToDetail, GenerateAttentionToDetailNotes));

            // Generate summaries
            eval.OverallAssessment = GenerateScoutingOverallAssessment(profile);
            eval.StrengthsSummary = GenerateScoutingStrengths(profile);
            eval.WeaknessesSummary = GenerateScoutingWeaknesses(profile);
        }

        private string GenerateScoutingOverallAssessment(UnifiedCareerProfile profile)
        {
            int rating = profile.OverallRating;
            string experience = profile.TotalFrontOfficeYears > 12 ? "veteran" :
                                profile.TotalFrontOfficeYears > 6 ? "experienced" :
                                profile.TotalFrontOfficeYears > 2 ? "developing" : "young";

            return rating switch
            {
                >= 90 => $"An elite {experience} scout with an exceptional track record of finding talent.",
                >= 85 => $"An excellent {experience} scout whose evaluations are highly trusted.",
                >= 80 => $"A very good {experience} scout with strong instincts for talent.",
                >= 75 => $"A quality {experience} scout who provides valuable insights.",
                >= 70 => $"A solid {experience} scout with reliable evaluations.",
                >= 65 => $"An adequate {experience} scout still refining his approach.",
                >= 60 => $"A serviceable {experience} scout with room for growth.",
                _ => $"A {experience} scout who needs to improve evaluation accuracy."
            };
        }

        private string GenerateScoutingStrengths(UnifiedCareerProfile profile)
        {
            var strengths = new List<string>();

            if (profile.EvaluationAccuracy >= 75)
                strengths.Add("highly accurate player evaluations");

            if (profile.ProspectEvaluation >= 75)
                strengths.Add("excellent eye for college talent");
            else if (profile.ProspectEvaluation >= 65)
                strengths.Add("solid prospect evaluation");

            if (profile.ProEvaluation >= 75)
                strengths.Add("strong pro player assessment");

            if (profile.PotentialAssessment >= 75)
                strengths.Add("exceptional ability to project player ceilings");

            if (profile.WorkRate >= 75)
                strengths.Add("tireless work ethic covering players");

            if (profile.AttentionToDetail >= 75)
                strengths.Add("meticulous attention to detail");

            if (strengths.Count == 0)
                return "Still establishing himself in the scouting world. Needs more experience.";

            return $"Known for {string.Join(", ", strengths.Take(3))}. Reports are thorough and actionable.";
        }

        private string GenerateScoutingWeaknesses(UnifiedCareerProfile profile)
        {
            var weaknesses = new List<string>();

            if (profile.EvaluationAccuracy < 60)
                weaknesses.Add("inconsistent evaluation accuracy");

            if (profile.ProspectEvaluation < 60)
                weaknesses.Add("struggles evaluating college players");

            if (profile.ProEvaluation < 60)
                weaknesses.Add("limited pro scouting experience");

            if (profile.PotentialAssessment < 60)
                weaknesses.Add("difficulty projecting player growth");

            if (profile.WorkRate < 60)
                weaknesses.Add("limited coverage territory");

            if (weaknesses.Count == 0)
                return "No significant weaknesses. A reliable evaluator across the board.";

            return $"Areas of concern: {string.Join(", ", weaknesses.Take(2))}. May need mentorship in these areas.";
        }

        #endregion

        #region Skill Note Generators

        private delegate string NoteGenerator(int value);

        private StaffSkillAssessment GenerateSkillAssessment(string skillName, int value, NoteGenerator noteGenerator)
        {
            return new StaffSkillAssessment
            {
                SkillName = skillName,
                Grade = GetGrade(value),
                Description = noteGenerator(value)
            };
        }

        private string GetGrade(int value)
        {
            return value switch
            {
                >= 90 => "Elite",
                >= 80 => "Excellent",
                >= 70 => "Very Good",
                >= 60 => "Solid",
                >= 50 => "Average",
                >= 40 => "Below Average",
                _ => "Poor"
            };
        }

        // Coaching skill notes
        private string GenerateOffensiveSchemeNotes(int value) => value switch
        {
            >= 85 => "Innovative play designer who creates mismatches. Elite at exploiting defensive weaknesses.",
            >= 75 => "Strong offensive mind with creative schemes. Players thrive in his system.",
            >= 65 => "Solid understanding of offensive principles. Runs efficient sets.",
            >= 55 => "Adequate offensive concepts but can be predictable.",
            >= 45 => "Limited offensive creativity. Struggles against aggressive defenses.",
            _ => "Offensive schemes are a significant weakness. Needs support."
        };

        private string GenerateDefensiveSchemeNotes(int value) => value switch
        {
            >= 85 => "Defensive mastermind who can shut down any offense. Players buy into his system.",
            >= 75 => "Strong defensive principles that hold up in big games.",
            >= 65 => "Sound defensive schemes with good fundamentals.",
            >= 55 => "Adequate defensive concepts but can be exploited.",
            >= 45 => "Struggles to implement effective defensive schemes.",
            _ => "Defensive systems are a liability. Teams exploit his schemes."
        };

        private string GenerateGameManagementNotes(int value) => value switch
        {
            >= 85 => "Masterful at managing games. Makes the right calls in crunch time.",
            >= 75 => "Excellent decision-maker. Rarely makes costly mistakes.",
            >= 65 => "Solid game manager who makes good substitutions and timeouts.",
            >= 55 => "Adequate but sometimes slow to make adjustments.",
            >= 45 => "Questionable decisions in crucial moments.",
            _ => "Poor game management is a significant concern."
        };

        private string GeneratePlayerDevelopmentNotes(int value) => value switch
        {
            >= 85 => "Exceptional at developing young talent. Players make huge leaps under his guidance.",
            >= 75 => "Strong track record of player improvement. Maximizes potential.",
            >= 65 => "Good at identifying areas for player growth.",
            >= 55 => "Average development results. Some players improve.",
            >= 45 => "Struggles to help players reach their potential.",
            _ => "Poor development track record. Young players don't progress."
        };

        private string GenerateMotivationNotes(int value) => value switch
        {
            >= 85 => "Inspirational leader who gets players to exceed their abilities.",
            >= 75 => "Motivates players effectively. Locker room responds to him.",
            >= 65 => "Solid motivator who keeps players engaged.",
            >= 55 => "Average at motivation. Some players respond, others don't.",
            >= 45 => "Struggles to inspire players in tough situations.",
            _ => "Motivation is a weakness. Players don't respond to him."
        };

        private string GenerateCommunicationNotes(int value) => value switch
        {
            >= 85 => "Exceptional communicator. Players always know their roles.",
            >= 75 => "Clear and effective communication with players and staff.",
            >= 65 => "Good communicator. Gets his message across.",
            >= 55 => "Adequate communication but sometimes unclear.",
            >= 45 => "Communication issues cause confusion.",
            _ => "Poor communication is a significant weakness."
        };

        // Scouting skill notes
        private string GenerateEvaluationAccuracyNotes(int value) => value switch
        {
            >= 85 => "Highly accurate evaluator. His assessments are trusted throughout the league.",
            >= 75 => "Very reliable evaluations with few misses.",
            >= 65 => "Solid accuracy on most evaluations.",
            >= 55 => "Average accuracy. Some hits, some misses.",
            >= 45 => "Inconsistent evaluations raise questions.",
            _ => "Poor accuracy undermines his usefulness."
        };

        private string GenerateProspectScoutingNotes(int value) => value switch
        {
            >= 85 => "Elite eye for college talent. Has found multiple gems.",
            >= 75 => "Strong prospect evaluator with good projection skills.",
            >= 65 => "Solid at identifying college talent.",
            >= 55 => "Average prospect evaluation.",
            >= 45 => "Struggles to identify future NBA talent.",
            _ => "Prospect evaluation is a significant weakness."
        };

        private string GenerateProScoutingNotes(int value) => value switch
        {
            >= 85 => "Excellent at evaluating NBA players. Knows the league inside out.",
            >= 75 => "Strong pro scouting with deep league knowledge.",
            >= 65 => "Good at assessing pro players.",
            >= 55 => "Adequate pro scouting abilities.",
            >= 45 => "Limited experience evaluating NBA players.",
            _ => "Pro scouting needs significant improvement."
        };

        private string GeneratePotentialScoutingNotes(int value) => value switch
        {
            >= 85 => "Exceptional at projecting player ceilings and development curves.",
            >= 75 => "Very good at identifying upside and growth potential.",
            >= 65 => "Solid potential assessment abilities.",
            >= 55 => "Average at projecting player development.",
            >= 45 => "Struggles to accurately project player ceilings.",
            _ => "Poor potential assessment. Often misses on projections."
        };

        private string GenerateWorkRateNotes(int value) => value switch
        {
            >= 85 => "Tireless worker who covers more ground than anyone.",
            >= 75 => "Strong work ethic. Always prepared.",
            >= 65 => "Solid worker who meets expectations.",
            >= 55 => "Average work rate.",
            >= 45 => "Could put in more effort.",
            _ => "Work rate is a concern."
        };

        private string GenerateAttentionToDetailNotes(int value) => value switch
        {
            >= 85 => "Meticulous attention to detail. Nothing escapes his notice.",
            >= 75 => "Very thorough in his evaluations.",
            >= 65 => "Good attention to detail.",
            >= 55 => "Adequate detail in reports.",
            >= 45 => "Sometimes misses important details.",
            _ => "Lack of attention to detail hurts his evaluations."
        };

        #endregion

        #region Career & Potential

        private string GenerateCareerTrajectory(UnifiedCareerProfile profile)
        {
            int years = profile.CurrentTrack == UnifiedCareerTrack.Coaching
                ? profile.TotalCoachingYears
                : profile.TotalFrontOfficeYears;

            int age = profile.CurrentAge;

            if (years < 3 && profile.OverallRating >= 70)
                return "Rising star with significant upside.";
            if (years < 5 && profile.OverallRating >= 65)
                return "Promising young professional still developing.";
            if (years >= 15 && profile.OverallRating >= 80)
                return "Established veteran at the peak of his career.";
            if (years >= 10 && profile.OverallRating >= 70)
                return "Experienced professional with a proven track record.";
            if (age >= 55)
                return "Veteran nearing the end of his career.";
            if (profile.OverallRating < 55)
                return "Career journeyman unlikely to reach higher levels.";

            return "Steady professional continuing to build his resume.";
        }

        private string GeneratePotentialAssessment(UnifiedCareerProfile profile)
        {
            int age = profile.CurrentAge;
            int rating = profile.OverallRating;

            if (age <= 40 && rating >= 75)
                return "Has the potential to become one of the best in the league.";
            if (age <= 45 && rating >= 70)
                return "Could develop into an elite-level professional.";
            if (age <= 50 && rating >= 65)
                return "Still has room to grow and improve.";
            if (age > 55 || rating >= 80)
                return "What you see is what you get at this point.";

            return "Ceiling appears limited but may surprise.";
        }

        #endregion
    }
}
