using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages advance scouting - opponent analysis reports prepared before games.
    /// Scouts can be assigned to opposing teams to generate detailed game prep reports.
    /// </summary>
    public class AdvanceScoutingManager : MonoBehaviour
    {
        public static AdvanceScoutingManager Instance { get; private set; }

        #region Configuration

        [Header("Report Settings")]
        [SerializeField] private int _minGamesToScout = 3;    // Minimum games watched for a report
        [SerializeField] private int _maxGameHistory = 10;     // Games to analyze
        [SerializeField] private float _reportExpirationDays = 14f;

        [Header("Scout Development")]
        [SerializeField] private float _experiencePerReport = 0.5f;
        [SerializeField] private float _accuracyImprovementRate = 0.02f;  // 2% per season
        [SerializeField] private float _maxAccuracyImprovement = 0.15f;   // Cap at 15%

        #endregion

        #region State

        private Dictionary<string, List<AdvanceScoutingReport>> _reports = new Dictionary<string, List<AdvanceScoutingReport>>();
        private Dictionary<string, ScoutAssignment> _activeAssignments = new Dictionary<string, ScoutAssignment>();
        private Dictionary<string, ScoutDevelopmentData> _scoutDevelopment = new Dictionary<string, ScoutDevelopmentData>();

        #endregion

        #region Events

        public event Action<AdvanceScoutingReport> OnReportGenerated;
        public event Action<UnifiedCareerProfile, ScoutDevelopmentEvent> OnScoutDeveloped;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            GameManager.Instance?.RegisterAdvanceScoutingManager(this);
        }

        #endregion

        #region Assignment Management

        /// <summary>
        /// Assign a scout to watch an opponent team.
        /// </summary>
        public bool AssignScoutToTeam(string scoutId, string opponentTeamId, int gamesToWatch = 3)
        {
            if (string.IsNullOrEmpty(scoutId) || string.IsNullOrEmpty(opponentTeamId))
                return false;

            var scout = PersonnelManager.Instance?.GetProfile(scoutId);
            if (scout == null || scout.CurrentRole != UnifiedRole.Scout)
                return false;

            // Check if scout is already assigned
            if (_activeAssignments.ContainsKey(scout.ProfileId))
            {
                Debug.LogWarning($"Scout {scout.PersonName} is already on assignment");
                return false;
            }

            var assignment = new ScoutAssignment
            {
                ScoutId = scout.ProfileId,
                OpponentTeamId = opponentTeamId,
                GamesToWatch = gamesToWatch,
                GamesWatched = 0,
                StartDate = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                GameNotes = new List<GameObservation>()
            };

            _activeAssignments[scout.ProfileId] = assignment;
            return true;
        }

        /// <summary>
        /// Cancel an active scout assignment.
        /// </summary>
        public void CancelAssignment(string scoutId)
        {
            _activeAssignments.Remove(scoutId);
        }

        /// <summary>
        /// Record that a scout watched a game (call after opponent games).
        /// </summary>
        public void RecordGameWatched(string scoutId, GameResult gameResult, bool isHome)
        {
            if (!_activeAssignments.TryGetValue(scoutId, out var assignment))
                return;

            var scout = PersonnelManager.Instance?.GetProfile(scoutId);
            if (scout == null) return;

            // Generate observation from this game
            var observation = GenerateGameObservation(scout, gameResult, isHome);
            assignment.GameNotes.Add(observation);
            assignment.GamesWatched++;

            // Check if assignment is complete
            if (assignment.GamesWatched >= assignment.GamesToWatch)
            {
                CompleteAssignment(scout, assignment);
            }
        }

        private void CompleteAssignment(UnifiedCareerProfile scout, ScoutAssignment assignment)
        {
            // Generate the advance report
            var report = GenerateAdvanceReport(scout, assignment);

            // Store the report
            if (!_reports.ContainsKey(assignment.OpponentTeamId))
                _reports[assignment.OpponentTeamId] = new List<AdvanceScoutingReport>();

            _reports[assignment.OpponentTeamId].Add(report);

            // Award scout experience
            AddScoutExperience(scout.ProfileId, _experiencePerReport);

            // Clear assignment
            _activeAssignments.Remove(scout.ProfileId);

            // Fire event
            OnReportGenerated?.Invoke(report);
        }

        #endregion

        #region Report Generation

        private GameObservation GenerateGameObservation(UnifiedCareerProfile scout, GameResult result, bool isHome)
        {
            var observation = new GameObservation
            {
                GameDate = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                OpponentScore = isHome ? result.HomeScore : result.AwayScore,
                TheirScore = isHome ? result.AwayScore : result.HomeScore,
                Won = isHome ? result.AwayScore > result.HomeScore : result.HomeScore > result.AwayScore
            };

            // Analyze box score for tendencies (simplified)
            var boxScore = result.BoxScore;
            if (boxScore != null)
            {
                // Track shooting tendencies
                int totalFGA = boxScore.AllPlayerStats.Sum(s => s.FieldGoalsAttempted);
                int threePtAttempts = boxScore.AllPlayerStats.Sum(s => s.ThreePointersAttempted);
                observation.ThreePointRate = totalFGA > 0 ? (float)threePtAttempts / totalFGA : 0;

                // Track pace
                observation.EstimatedPossessions = boxScore.AllPlayerStats.Sum(s => s.FieldGoalsAttempted +
                    (int)(s.FreeThrowsAttempted * 0.44f) + s.Turnovers);

                // Track top scorers
                var topScorer = boxScore.AllPlayerStats
                    .OrderByDescending(s => s.Points)
                    .FirstOrDefault();
                if (topScorer != null)
                {
                    observation.TopScorer = topScorer.PlayerId;
                    observation.TopScorerPoints = topScorer.Points;
                }
            }

            return observation;
        }

        private AdvanceScoutingReport GenerateAdvanceReport(UnifiedCareerProfile scout, ScoutAssignment assignment)
        {
            var opponent = GameManager.Instance?.GetTeam(assignment.OpponentTeamId);
            float scoutAccuracy = GetScoutEffectiveAccuracy(scout);

            var report = new AdvanceScoutingReport
            {
                ReportId = Guid.NewGuid().ToString(),
                OpponentTeamId = assignment.OpponentTeamId,
                OpponentName = opponent?.Name ?? "Unknown",
                ScoutId = scout.ProfileId,
                ScoutName = scout.PersonName,
                GeneratedDate = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                GamesAnalyzed = assignment.GamesWatched,
                Confidence = CalculateReportConfidence(assignment.GamesWatched, scoutAccuracy)
            };

            // Analyze collected data
            AnalyzeOffensiveTendencies(report, assignment, scoutAccuracy);
            AnalyzeDefensiveTendencies(report, assignment, scoutAccuracy);
            IdentifyKeyPlayers(report, opponent, scoutAccuracy);
            GenerateGamePlan(report, assignment, scoutAccuracy);

            return report;
        }

        private void AnalyzeOffensiveTendencies(AdvanceScoutingReport report, ScoutAssignment assignment, float accuracy)
        {
            var tendencies = new OffensiveTendencies();

            // Aggregate from game observations
            if (assignment.GameNotes.Count > 0)
            {
                tendencies.ThreePointRate = assignment.GameNotes.Average(g => g.ThreePointRate);
                tendencies.AveragePossessions = assignment.GameNotes.Average(g => g.EstimatedPossessions);

                // Classify pace
                if (tendencies.AveragePossessions > 105)
                    tendencies.PaceDescription = "Fast-paced team that pushes in transition";
                else if (tendencies.AveragePossessions < 95)
                    tendencies.PaceDescription = "Methodical half-court offense, patient shot selection";
                else
                    tendencies.PaceDescription = "Average pace, balanced approach";

                // Classify shooting style
                if (tendencies.ThreePointRate > 0.40f)
                    tendencies.ShootingStyle = "Three-point heavy offense. Close out hard.";
                else if (tendencies.ThreePointRate < 0.25f)
                    tendencies.ShootingStyle = "Paint-focused attack. Pack the paint.";
                else
                    tendencies.ShootingStyle = "Balanced shot selection.";
            }

            // Add accuracy variance
            tendencies.ConfidenceLevel = accuracy > 0.75f ? "High" : accuracy > 0.6f ? "Medium" : "Low";

            report.OffensiveTendencies = tendencies;
        }

        private void AnalyzeDefensiveTendencies(AdvanceScoutingReport report, ScoutAssignment assignment, float accuracy)
        {
            var tendencies = new DefensiveTendencies();

            if (assignment.GameNotes.Count > 0)
            {
                float avgPointsAllowed = assignment.GameNotes.Average(g => g.TheirScore);

                if (avgPointsAllowed < 105)
                    tendencies.DefensiveRating = "Elite defense. Be patient on offense.";
                else if (avgPointsAllowed < 112)
                    tendencies.DefensiveRating = "Solid defensive team. Execute plays.";
                else
                    tendencies.DefensiveRating = "Below average defense. Attack aggressively.";

                // Estimate scheme based on results
                float avgThreeRate = assignment.GameNotes.Average(g => g.ThreePointRate);
                if (avgThreeRate > 0.38f)
                    tendencies.SchemeNotes = "They allow a lot of threes. Likely switching or drop coverage.";
                else
                    tendencies.SchemeNotes = "Good at running teams off the three-point line.";
            }

            tendencies.ConfidenceLevel = accuracy > 0.75f ? "High" : accuracy > 0.6f ? "Medium" : "Low";
            report.DefensiveTendencies = tendencies;
        }

        private void IdentifyKeyPlayers(AdvanceScoutingReport report, Team opponent, float accuracy)
        {
            report.KeyPlayers = new List<KeyPlayerNote>();

            if (opponent?.Roster == null) return;

            // Find their best players
            var topPlayers = opponent.Roster
                .OrderByDescending(p => p.GetOverallRating())
                .Take(3)
                .ToList();

            foreach (var player in topPlayers)
            {
                var note = new KeyPlayerNote
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.DisplayName,
                    Position = player.Position.ToString(),
                    ThreatLevel = player.GetOverallRating() >= 85 ? "Primary" :
                                 player.GetOverallRating() >= 75 ? "Secondary" : "Tertiary"
                };

                // Generate scouting notes based on attributes
                var strengths = new List<string>();
                var weaknesses = new List<string>();

                if (player.Shot_Three >= 80) strengths.Add("Elite three-point shooter");
                else if (player.Shot_Three < 65) weaknesses.Add("Limited range");

                if (player.Finishing_Rim >= 80) strengths.Add("Finishes strong at the rim");
                if (player.BallHandling >= 80) strengths.Add("Elite ball handler");

                if (player.Defense_Perimeter < 65) weaknesses.Add("Can be attacked off the dribble");
                if (player.Defense_Interior < 65) weaknesses.Add("Struggles protecting the rim");

                // Apply accuracy variance (worse scouts may miss things)
                if (accuracy < 0.7f && weaknesses.Count > 0)
                {
                    // Low accuracy scout might miss a weakness
                    if (UnityEngine.Random.value > accuracy)
                        weaknesses.RemoveAt(UnityEngine.Random.Range(0, weaknesses.Count));
                }

                note.Strengths = strengths;
                note.Weaknesses = weaknesses;
                note.DefensiveStrategy = GeneratePlayerDefenseStrategy(player, accuracy);

                report.KeyPlayers.Add(note);
            }
        }

        private string GeneratePlayerDefenseStrategy(Player player, float accuracy)
        {
            var strategies = new List<string>();

            if (player.Shot_Three >= 80)
                strategies.Add("Don't leave him open behind the arc");
            if (player.Finishing_Rim >= 80)
                strategies.Add("Help defense on drives, contest at the rim");
            if (player.BallHandling >= 80)
                strategies.Add("Force him to his weak hand");
            if (player.Playmaking_Passing >= 80)
                strategies.Add("Stay in passing lanes when he has the ball");

            if (strategies.Count == 0)
                strategies.Add("Play straight up, don't over-help");

            return string.Join(". ", strategies);
        }

        private void GenerateGamePlan(AdvanceScoutingReport report, ScoutAssignment assignment, float accuracy)
        {
            report.GamePlan = new GamePlanRecommendation();

            // Offensive recommendations
            var offRecs = new List<string>();
            if (report.DefensiveTendencies?.DefensiveRating?.Contains("Elite") == true)
            {
                offRecs.Add("Be patient in the half court");
                offRecs.Add("Move the ball to find open looks");
            }
            else
            {
                offRecs.Add("Attack early in the shot clock");
                offRecs.Add("Push the pace when possible");
            }
            report.GamePlan.OffensiveRecommendations = offRecs;

            // Defensive recommendations
            var defRecs = new List<string>();
            if (report.OffensiveTendencies?.ThreePointRate > 0.38f)
            {
                defRecs.Add("Close out hard on shooters");
                defRecs.Add("Consider running them off the three-point line");
            }
            else
            {
                defRecs.Add("Protect the paint");
                defRecs.Add("Help on drives");
            }
            report.GamePlan.DefensiveRecommendations = defRecs;

            // Key matchups
            if (report.KeyPlayers?.Count > 0)
            {
                report.GamePlan.KeyMatchups = report.KeyPlayers
                    .Select(p => $"Limit {p.PlayerName}'s impact")
                    .ToList();
            }
        }

        private ReportConfidence CalculateReportConfidence(int gamesWatched, float scoutAccuracy)
        {
            if (gamesWatched >= 5 && scoutAccuracy >= 0.8f)
                return ReportConfidence.VeryHigh;
            if (gamesWatched >= 3 && scoutAccuracy >= 0.7f)
                return ReportConfidence.High;
            if (gamesWatched >= 2 && scoutAccuracy >= 0.6f)
                return ReportConfidence.Medium;
            return ReportConfidence.Low;
        }

        #endregion

        #region Scout Development

        /// <summary>
        /// Process scout development at end of season.
        public void ProcessSeasonDevelopment(List<UnifiedCareerProfile> scouts)
        {
            foreach (var scout in scouts)
            {
                if (!_scoutDevelopment.TryGetValue(scout.ProfileId, out var data))
                {
                    data = new ScoutDevelopmentData { ScoutId = scout.ProfileId };
                    _scoutDevelopment[scout.ProfileId] = data;
                }

                // Calculate improvement based on reports generated
                float improvement = data.ReportsGeneratedThisSeason * _accuracyImprovementRate;
                improvement = Mathf.Min(improvement, _maxAccuracyImprovement);

                if (improvement > 0)
                {
                    // Apply improvement to evaluation skills
                    float proEvalGain = improvement * 100f * 0.5f;
                    float prospectEvalGain = improvement * 100f * 0.3f;

                    // Apply gains to the unified profile
                    scout.ProEvaluation += Mathf.RoundToInt(proEvalGain);
                    scout.ProspectEvaluation += Mathf.RoundToInt(prospectEvalGain);
                    
                    data.AccuracyBonus += improvement;

                    var devEvent = new ScoutDevelopmentEvent
                    {
                        ScoutId = scout.ProfileId,
                        EventType = ScoutDevelopmentEventType.SeasonImprovement,
                        Description = $"Improved evaluation accuracy by {improvement:P1}",
                        AttributeGains = new Dictionary<string, float>
                        {
                            { "ProEvaluation", proEvalGain },
                            { "ProspectEvaluation", prospectEvalGain }
                        }
                    };

                    OnScoutDeveloped?.Invoke(scout, devEvent);
                }

                // Reset season counter
                data.ReportsGeneratedThisSeason = 0;
                data.SeasonsOfService++;
            }
        }

        private void AddScoutExperience(string scoutId, float experience)
        {
            if (!_scoutDevelopment.TryGetValue(scoutId, out var data))
            {
                data = new ScoutDevelopmentData { ScoutId = scoutId };
                _scoutDevelopment[scoutId] = data;
            }

            data.TotalExperience += experience;
            data.ReportsGeneratedThisSeason++;
        }

        private float GetScoutEffectiveAccuracy(UnifiedCareerProfile scout)
        {
            float baseAccuracy = scout.EvaluationAccuracy / 100f;

            // Add bonus from development
            if (_scoutDevelopment.TryGetValue(scout.ProfileId, out var data))
            {
                baseAccuracy += data.AccuracyBonus;
            }

            return Mathf.Clamp(baseAccuracy, 0.4f, 0.95f);
        }

        #endregion

        #region Queries

        /// <summary>
        /// Gets the most recent advance scouting report for an opponent.
        /// </summary>
        public AdvanceScoutingReport GetLatestReport(string opponentTeamId)
        {
            if (!_reports.TryGetValue(opponentTeamId, out var reports) || reports.Count == 0)
                return null;

            var currentDate = GameManager.Instance?.CurrentDate ?? DateTime.Now;
            var validReports = reports
                .Where(r => (currentDate - r.GeneratedDate).TotalDays <= _reportExpirationDays)
                .OrderByDescending(r => r.GeneratedDate)
                .ToList();

            return validReports.FirstOrDefault();
        }

        /// <summary>
        /// Gets all reports for an opponent.
        /// </summary>
        public List<AdvanceScoutingReport> GetAllReports(string opponentTeamId)
        {
            return _reports.GetValueOrDefault(opponentTeamId, new List<AdvanceScoutingReport>());
        }

        /// <summary>
        /// Gets active scout assignment.
        /// </summary>
        public ScoutAssignment GetActiveAssignment(string scoutId)
        {
            return _activeAssignments.GetValueOrDefault(scoutId);
        }

        private UnifiedCareerProfile GetScout(string scoutId)
        {
            return PersonnelManager.Instance?.GetProfile(scoutId);
        }

        #endregion
    }

    #region Supporting Types

    [Serializable]
    public class ScoutAssignment
    {
        public string ScoutId;
        public string OpponentTeamId;
        public int GamesToWatch;
        public int GamesWatched;
        public DateTime StartDate;
        public List<GameObservation> GameNotes;
    }

    [Serializable]
    public class GameObservation
    {
        public DateTime GameDate;
        public int OpponentScore;
        public int TheirScore;
        public bool Won;
        public float ThreePointRate;
        public float EstimatedPossessions;
        public string TopScorer;
        public int TopScorerPoints;
    }

    [Serializable]
    public class AdvanceScoutingReport
    {
        public string ReportId;
        public string OpponentTeamId;
        public string OpponentName;
        public string ScoutId;
        public string ScoutName;
        public DateTime GeneratedDate;
        public int GamesAnalyzed;
        public ReportConfidence Confidence;

        public OffensiveTendencies OffensiveTendencies;
        public DefensiveTendencies DefensiveTendencies;
        public List<KeyPlayerNote> KeyPlayers;
        public GamePlanRecommendation GamePlan;
    }

    [Serializable]
    public class OffensiveTendencies
    {
        public float ThreePointRate;
        public float AveragePossessions;
        public string PaceDescription;
        public string ShootingStyle;
        public string ConfidenceLevel;
    }

    [Serializable]
    public class DefensiveTendencies
    {
        public string DefensiveRating;
        public string SchemeNotes;
        public string ConfidenceLevel;
    }

    [Serializable]
    public class KeyPlayerNote
    {
        public string PlayerId;
        public string PlayerName;
        public string Position;
        public string ThreatLevel;
        public List<string> Strengths;
        public List<string> Weaknesses;
        public string DefensiveStrategy;
    }

    [Serializable]
    public class GamePlanRecommendation
    {
        public List<string> OffensiveRecommendations;
        public List<string> DefensiveRecommendations;
        public List<string> KeyMatchups;
    }

    [Serializable]
    public class ScoutDevelopmentData
    {
        public string ScoutId;
        public float TotalExperience;
        public float AccuracyBonus;
        public int ReportsGeneratedThisSeason;
        public int SeasonsOfService;
    }

    [Serializable]
    public class ScoutDevelopmentEvent
    {
        public string ScoutId;
        public ScoutDevelopmentEventType EventType;
        public string Description;
        public Dictionary<string, float> AttributeGains;
    }

    public enum ScoutDevelopmentEventType
    {
        SeasonImprovement,
        SpecialAssignment,
        MentorBonus,
        HighProfileHit
    }

    public enum ReportConfidence
    {
        Low,
        Medium,
        High,
        VeryHigh
    }

    #endregion
}
