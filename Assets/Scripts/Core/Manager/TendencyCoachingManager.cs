using System;
using System.Collections.Generic;
using System.Linq;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages the training and development of player coachable tendencies.
    /// Handles focus area assignment, progress tracking, and improvement calculations.
    /// </summary>
    public class TendencyCoachingManager
    {
        private static TendencyCoachingManager _instance;
        public static TendencyCoachingManager Instance => _instance ??= new TendencyCoachingManager();

        // Training intensity levels
        public const float INTENSITY_LIGHT = 0.5f;
        public const float INTENSITY_NORMAL = 1.0f;
        public const float INTENSITY_HEAVY = 1.5f;
        public const float INTENSITY_INTENSE = 2.0f;

        // Maximum focus areas for effective training
        public const int MAX_EFFECTIVE_FOCUS_AREAS = 3;

        // Events
        public event Action<string, string, int> OnTendencyImproved; // playerId, tendencyName, newValue
        public event Action<string, string> OnTrainingFailed; // playerId, tendencyName
        public event Action<string, string> OnPlayerResisted; // playerId, tendencyName

        // Dependencies
        private Func<string, Player> _getPlayer;
        private Func<string, Coach> _getCoach;

        /// <summary>
        /// Training difficulty for each coachable tendency.
        /// Higher values = more weeks required to see improvement.
        /// </summary>
        private readonly Dictionary<string, float> _tendencyDifficulty = new Dictionary<string, float>
        {
            { "ScreenSetting", 1.0f },
            { "HelpDefenseAggression", 1.5f },
            { "TransitionDefense", 1.2f },
            { "PostDefenseAggression", 1.3f },
            { "CloseoutControl", 1.4f },
            { "OffBallMovement", 1.1f },
            { "BoxOutDiscipline", 1.0f },
            { "PnRCoverageExecution", 1.8f },
            { "DefensiveCommunication", 1.2f },
            { "ShotDiscipline", 1.6f }
        };

        /// <summary>
        /// Progress required to gain 1 point in a tendency.
        /// </summary>
        private const float PROGRESS_PER_POINT = 100f;

        /// <summary>
        /// Initializes the manager with dependency providers.
        /// </summary>
        public void Initialize(Func<string, Player> getPlayer, Func<string, Coach> getCoach)
        {
            _getPlayer = getPlayer;
            _getCoach = getCoach;
        }

        /// <summary>
        /// Assigns a training focus area to a player.
        /// Returns success/failure and reason.
        /// </summary>
        public (bool success, string message) AssignTrainingFocus(string playerId, string tendencyName)
        {
            var player = _getPlayer?.Invoke(playerId);
            if (player == null)
                return (false, "Player not found");

            if (player.Tendencies == null)
                return (false, "Player has no tendencies data");

            if (!_tendencyDifficulty.ContainsKey(tendencyName))
                return (false, $"'{tendencyName}' is not a coachable tendency");

            // Check if already at max focus areas
            if (player.Tendencies.CurrentTrainingFocus.Count >= MAX_EFFECTIVE_FOCUS_AREAS)
                return (false, $"Player already has {MAX_EFFECTIVE_FOCUS_AREAS} focus areas. Remove one first.");

            // Check if already training this tendency
            if (player.Tendencies.CurrentTrainingFocus.Contains(tendencyName))
                return (false, $"Already training {tendencyName}");

            // Check if tendency is already maxed
            int currentValue = GetTendencyValue(player.Tendencies, tendencyName);
            if (currentValue >= 95)
                return (false, $"{tendencyName} is already at maximum level");

            // Add to training focus
            player.Tendencies.CurrentTrainingFocus.Add(tendencyName);

            // Initialize progress if not exists
            if (!player.Tendencies.TrainingProgress.ContainsKey(tendencyName))
                player.Tendencies.TrainingProgress[tendencyName] = 0f;

            return (true, $"Now training {tendencyName}");
        }

        /// <summary>
        /// Removes a training focus area from a player.
        /// </summary>
        public (bool success, string message) RemoveTrainingFocus(string playerId, string tendencyName)
        {
            var player = _getPlayer?.Invoke(playerId);
            if (player?.Tendencies == null)
                return (false, "Player not found or no tendencies data");

            if (!player.Tendencies.CurrentTrainingFocus.Contains(tendencyName))
                return (false, $"Not currently training {tendencyName}");

            player.Tendencies.CurrentTrainingFocus.Remove(tendencyName);
            return (true, $"Stopped training {tendencyName}");
        }

        /// <summary>
        /// Processes weekly training for all players on a team.
        /// Call this during the weekly simulation update.
        /// </summary>
        public List<TrainingResult> ProcessWeeklyTraining(List<string> playerIds, string headCoachId, float trainingIntensity = INTENSITY_NORMAL)
        {
            var results = new List<TrainingResult>();
            var coach = _getCoach?.Invoke(headCoachId);

            foreach (var playerId in playerIds)
            {
                var player = _getPlayer?.Invoke(playerId);
                if (player?.Tendencies == null) continue;

                foreach (var tendencyName in player.Tendencies.CurrentTrainingFocus.ToList())
                {
                    var result = ProcessIndividualTraining(player, tendencyName, coach, trainingIntensity);
                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// Processes training for a single player's tendency.
        /// </summary>
        private TrainingResult ProcessIndividualTraining(Player player, string tendencyName, Coach coach, float intensity)
        {
            var result = new TrainingResult
            {
                PlayerId = player.Id,
                PlayerName = player.FullName,
                TendencyName = tendencyName
            };

            // Calculate training effectiveness factors
            float coachFactor = CalculateCoachFactor(coach, tendencyName);
            float coachabilityFactor = CalculateCoachabilityFactor(player);
            float difficultyFactor = 1f / _tendencyDifficulty.GetValueOrDefault(tendencyName, 1f);
            float focusCountPenalty = CalculateFocusCountPenalty(player.Tendencies.CurrentTrainingFocus.Count);
            float ageFactor = CalculateAgeFactor(player.Age);

            // Base weekly progress
            float baseProgress = 10f * intensity;

            // Calculate total progress this week
            float totalProgress = baseProgress * coachFactor * coachabilityFactor * difficultyFactor * focusCountPenalty * ageFactor;

            // Add randomness (0.7 to 1.3)
            var random = new Random();
            totalProgress *= 0.7f + (float)random.NextDouble() * 0.6f;

            result.ProgressGained = totalProgress;

            // Check for resistance (low coachability players may resist)
            if (coachabilityFactor < 0.6f && random.NextDouble() < 0.2f)
            {
                result.Resisted = true;
                result.Message = $"{player.FullName} resisted training on {tendencyName}";
                OnPlayerResisted?.Invoke(player.Id, tendencyName);
                return result;
            }

            // Add progress
            if (!player.Tendencies.TrainingProgress.ContainsKey(tendencyName))
                player.Tendencies.TrainingProgress[tendencyName] = 0f;

            player.Tendencies.TrainingProgress[tendencyName] += totalProgress;

            // Check if threshold reached for improvement
            float currentProgress = player.Tendencies.TrainingProgress[tendencyName];
            int pointsGained = (int)(currentProgress / PROGRESS_PER_POINT);

            if (pointsGained > 0)
            {
                // Apply improvement
                int currentValue = GetTendencyValue(player.Tendencies, tendencyName);
                int newValue = Math.Min(100, currentValue + pointsGained);
                int actualGain = newValue - currentValue;

                if (actualGain > 0)
                {
                    SetTendencyValue(player.Tendencies, tendencyName, newValue);
                    player.Tendencies.TrainingProgress[tendencyName] -= pointsGained * PROGRESS_PER_POINT;

                    result.Improved = true;
                    result.PointsGained = actualGain;
                    result.NewValue = newValue;
                    result.Message = $"{player.FullName}'s {tendencyName} improved to {newValue}!";

                    OnTendencyImproved?.Invoke(player.Id, tendencyName, newValue);
                }
            }
            else
            {
                result.Message = $"{player.FullName} made progress on {tendencyName} ({currentProgress:F0}/100)";
            }

            return result;
        }

        /// <summary>
        /// Calculates the coach's effectiveness for training a specific tendency.
        /// </summary>
        private float CalculateCoachFactor(Coach coach, string tendencyName)
        {
            if (coach == null) return 0.8f; // No coach penalty

            // Base from coach's development skill
            float baseFactor = 0.5f + (coach.DevelopingPlayers / 200f); // 0.5 to 1.0

            // Bonus from relevant specialties
            bool hasRelevantSpecialty = false;

            switch (tendencyName)
            {
                case "ScreenSetting":
                case "OffBallMovement":
                    hasRelevantSpecialty = coach.CoachingSpecialties?.Contains(CoachingSpecialty.Offense) == true;
                    break;

                case "HelpDefenseAggression":
                case "TransitionDefense":
                case "PostDefenseAggression":
                case "CloseoutControl":
                case "PnRCoverageExecution":
                case "DefensiveCommunication":
                    hasRelevantSpecialty = coach.CoachingSpecialties?.Contains(CoachingSpecialty.Defense) == true;
                    break;

                case "BoxOutDiscipline":
                    hasRelevantSpecialty = coach.CoachingSpecialties?.Contains(CoachingSpecialty.Rebounding) == true;
                    break;

                case "ShotDiscipline":
                    hasRelevantSpecialty = coach.CoachingSpecialties?.Contains(CoachingSpecialty.Shooting) == true;
                    break;
            }

            if (hasRelevantSpecialty)
                baseFactor += 0.2f;

            return Math.Min(1.5f, baseFactor);
        }

        /// <summary>
        /// Calculates how coachable a player is based on their attributes.
        /// </summary>
        private float CalculateCoachabilityFactor(Player player)
        {
            // Based on work ethic, basketball IQ, coachability, and personality traits
            float workEthic = player.WorkEthic / 100f;
            float bbIQ = player.BasketballIQ / 100f;
            float coachability = player.Coachability / 100f;

            // Personality traits affect coachability
            float personalityFactor = 1.0f;
            if (player.Personality != null)
            {
                // Team-oriented players are more coachable
                if (player.Personality.HasTrait(PersonalityTrait.TeamPlayer))
                    personalityFactor += 0.15f;
                if (player.Personality.HasTrait(PersonalityTrait.Professional))
                    personalityFactor += 0.2f;
                if (player.Personality.HasTrait(PersonalityTrait.Competitor))
                    personalityFactor += 0.1f;

                // Difficult personalities are harder to coach
                if (player.Personality.HasTrait(PersonalityTrait.BallHog))
                    personalityFactor -= 0.1f;
                if (player.Personality.HasTrait(PersonalityTrait.Volatile))
                    personalityFactor -= 0.2f;
                if (player.Personality.HasTrait(PersonalityTrait.Loner))
                    personalityFactor -= 0.1f;
            }

            float baseFactor = (workEthic * 0.3f + bbIQ * 0.2f + coachability * 0.3f + 0.2f) * personalityFactor;
            return Math.Max(0.3f, Math.Min(1.5f, baseFactor));
        }

        /// <summary>
        /// Calculates penalty for having multiple focus areas.
        /// Training is most effective with 1-2 focus areas.
        /// </summary>
        private float CalculateFocusCountPenalty(int focusCount)
        {
            switch (focusCount)
            {
                case 1: return 1.0f;
                case 2: return 0.9f;
                case 3: return 0.75f;
                default: return 0.5f; // Shouldn't happen due to max limit
            }
        }

        /// <summary>
        /// Younger players learn faster, older players slower.
        /// </summary>
        private float CalculateAgeFactor(int age)
        {
            if (age <= 23) return 1.2f;
            if (age <= 27) return 1.0f;
            if (age <= 30) return 0.85f;
            if (age <= 33) return 0.7f;
            return 0.5f;
        }

        /// <summary>
        /// Gets the current value of a coachable tendency.
        /// </summary>
        public int GetTendencyValue(PlayerTendencies tendencies, string name)
        {
            return name switch
            {
                "ScreenSetting" => tendencies.ScreenSetting,
                "HelpDefenseAggression" => tendencies.HelpDefenseAggression,
                "TransitionDefense" => tendencies.TransitionDefense,
                "PostDefenseAggression" => tendencies.PostDefenseAggression,
                "CloseoutControl" => tendencies.CloseoutControl,
                "OffBallMovement" => tendencies.OffBallMovement,
                "BoxOutDiscipline" => tendencies.BoxOutDiscipline,
                "PnRCoverageExecution" => tendencies.PnRCoverageExecution,
                "DefensiveCommunication" => tendencies.DefensiveCommunication,
                "ShotDiscipline" => tendencies.ShotDiscipline,
                _ => 50
            };
        }

        /// <summary>
        /// Sets the value of a coachable tendency.
        /// </summary>
        private void SetTendencyValue(PlayerTendencies tendencies, string name, int value)
        {
            switch (name)
            {
                case "ScreenSetting":
                    tendencies.ScreenSetting = value;
                    break;
                case "HelpDefenseAggression":
                    tendencies.HelpDefenseAggression = value;
                    break;
                case "TransitionDefense":
                    tendencies.TransitionDefense = value;
                    break;
                case "PostDefenseAggression":
                    tendencies.PostDefenseAggression = value;
                    break;
                case "CloseoutControl":
                    tendencies.CloseoutControl = value;
                    break;
                case "OffBallMovement":
                    tendencies.OffBallMovement = value;
                    break;
                case "BoxOutDiscipline":
                    tendencies.BoxOutDiscipline = value;
                    break;
                case "PnRCoverageExecution":
                    tendencies.PnRCoverageExecution = value;
                    break;
                case "DefensiveCommunication":
                    tendencies.DefensiveCommunication = value;
                    break;
                case "ShotDiscipline":
                    tendencies.ShotDiscipline = value;
                    break;
            }
        }

        /// <summary>
        /// Gets a summary of training status for a player.
        /// </summary>
        public TrainingStatusSummary GetPlayerTrainingStatus(string playerId)
        {
            var player = _getPlayer?.Invoke(playerId);
            if (player?.Tendencies == null)
                return null;

            var summary = new TrainingStatusSummary
            {
                PlayerId = playerId,
                PlayerName = player.FullName,
                CurrentFocusAreas = new List<TrainingFocusStatus>()
            };

            foreach (var focusName in player.Tendencies.CurrentTrainingFocus)
            {
                var status = new TrainingFocusStatus
                {
                    TendencyName = focusName,
                    CurrentValue = GetTendencyValue(player.Tendencies, focusName),
                    Progress = player.Tendencies.TrainingProgress.GetValueOrDefault(focusName, 0f),
                    ProgressToNextPoint = PROGRESS_PER_POINT,
                    Difficulty = _tendencyDifficulty.GetValueOrDefault(focusName, 1f)
                };
                summary.CurrentFocusAreas.Add(status);
            }

            // Get suggested focus areas based on coaching needs
            summary.SuggestedFocusAreas = player.Tendencies.GetCoachingNeeds()
                .Where(need => !player.Tendencies.CurrentTrainingFocus.Contains(need.Name))
                .Take(3)
                .ToList();

            return summary;
        }

        /// <summary>
        /// Gets all coachable tendency names.
        /// </summary>
        public List<string> GetAllCoachableTendencies()
        {
            return _tendencyDifficulty.Keys.ToList();
        }

        /// <summary>
        /// Estimates weeks to improve a tendency by a certain amount.
        /// </summary>
        public int EstimateWeeksToImprove(string playerId, string tendencyName, int targetImprovement, float intensity = INTENSITY_NORMAL)
        {
            var player = _getPlayer?.Invoke(playerId);
            if (player?.Tendencies == null) return -1;

            var coach = _getCoach?.Invoke("current"); // Would need actual coach ID
            float weeklyProgress = CalculateEstimatedWeeklyProgress(player, tendencyName, coach, intensity);

            if (weeklyProgress <= 0) return -1;

            float totalProgressNeeded = targetImprovement * PROGRESS_PER_POINT;
            return (int)Math.Ceiling(totalProgressNeeded / weeklyProgress);
        }

        private float CalculateEstimatedWeeklyProgress(Player player, string tendencyName, Coach coach, float intensity)
        {
            float coachFactor = CalculateCoachFactor(coach, tendencyName);
            float coachabilityFactor = CalculateCoachabilityFactor(player);
            float difficultyFactor = 1f / _tendencyDifficulty.GetValueOrDefault(tendencyName, 1f);
            float focusCountPenalty = CalculateFocusCountPenalty(player.Tendencies.CurrentTrainingFocus.Count);
            float ageFactor = CalculateAgeFactor(player.Age);

            return 10f * intensity * coachFactor * coachabilityFactor * difficultyFactor * focusCountPenalty * ageFactor;
        }
    }

    /// <summary>
    /// Result of a single training session for a tendency.
    /// </summary>
    [Serializable]
    public class TrainingResult
    {
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public string TendencyName { get; set; }
        public float ProgressGained { get; set; }
        public bool Improved { get; set; }
        public int PointsGained { get; set; }
        public int NewValue { get; set; }
        public bool Resisted { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Summary of a player's training status.
    /// </summary>
    [Serializable]
    public class TrainingStatusSummary
    {
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public List<TrainingFocusStatus> CurrentFocusAreas { get; set; }
        public List<CoachingArea> SuggestedFocusAreas { get; set; }
    }

    /// <summary>
    /// Status of a single training focus area.
    /// </summary>
    [Serializable]
    public class TrainingFocusStatus
    {
        public string TendencyName { get; set; }
        public int CurrentValue { get; set; }
        public float Progress { get; set; }
        public float ProgressToNextPoint { get; set; }
        public float Difficulty { get; set; }
        public float ProgressPercentage => (Progress / ProgressToNextPoint) * 100f;
    }
}
