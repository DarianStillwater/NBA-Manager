using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages media interactions including press conferences, interviews,
    /// and their consequences on morale, reputation, and relationships.
    /// </summary>
    public class MediaManager : MonoBehaviour
    {
        public static MediaManager Instance { get; private set; }

        #region Configuration

        [Header("Press Conference Settings")]
        [SerializeField] private float _postGameConferenceChance = 1.0f;  // Always after games
        [SerializeField] private float _randomInterviewChance = 0.15f;    // 15% daily chance
        [SerializeField] private int _maxStoredHeadlines = 20;

        [Header("Reputation Impact")]
        [SerializeField] private int _maxReputationChange = 10;
        [SerializeField] private float _reputationDecayRate = 0.5f;  // Daily decay toward neutral

        #endregion

        #region State

        private List<MediaHeadline> _recentHeadlines = new List<MediaHeadline>();
        private List<PressConference> _upcomingConferences = new List<PressConference>();
        private Dictionary<string, int> _mediaReputation = new Dictionary<string, int>(); // -100 to +100
        private Dictionary<string, List<MediaEvent>> _mediaHistory = new Dictionary<string, List<MediaEvent>>();

        // Coach response tracking for consistency
        private Dictionary<string, CoachMediaPersona> _coachPersonas = new Dictionary<string, CoachMediaPersona>();

        #endregion

        #region Events

        public event Action<PressConference> OnPressConferenceAvailable;
        public event Action<MediaHeadline> OnHeadlineGenerated;
        public event Action<string, int> OnReputationChanged;

        #endregion

        #region Properties

        public List<MediaHeadline> RecentHeadlines => _recentHeadlines;
        public bool HasPendingConference => _upcomingConferences.Count > 0;
        public PressConference NextConference => _upcomingConferences.FirstOrDefault();

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
            GameManager.Instance?.RegisterMediaManager(this);
        }

        #endregion

        #region Press Conference Generation

        /// <summary>
        /// Generates a post-game press conference.
        /// </summary>
        public PressConference GeneratePostGameConference(GameResult result, Team playerTeam, bool isPlayerTeam)
        {
            if (!isPlayerTeam) return null;

            bool won = (result.HomeTeamId == playerTeam.TeamId && result.HomeScore > result.AwayScore) ||
                       (result.AwayTeamId == playerTeam.TeamId && result.AwayScore > result.HomeScore);

            int scoreDiff = Math.Abs(result.HomeScore - result.AwayScore);
            var opponent = result.HomeTeamId == playerTeam.TeamId
                ? GameManager.Instance?.GetTeam(result.AwayTeamId)
                : GameManager.Instance?.GetTeam(result.HomeTeamId);

            var conference = new PressConference
            {
                Type = PressConferenceType.PostGame,
                Date = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                Context = new PressConferenceContext
                {
                    Won = won,
                    ScoreDifference = scoreDiff,
                    OpponentName = opponent?.Name ?? "Opponent",
                    IsPlayoff = GameManager.Instance?.CurrentState == GameState.Match &&
                                GameManager.Instance?.PlayoffManager?.IsPlayoffActive == true
                }
            };

            // Generate 3-4 questions based on context
            conference.Questions = GeneratePostGameQuestions(conference.Context, result, playerTeam);

            _upcomingConferences.Add(conference);
            OnPressConferenceAvailable?.Invoke(conference);

            return conference;
        }

        /// <summary>
        /// Generates a random media event (interview request, rumor response, etc.)
        /// </summary>
        public PressConference GenerateRandomMediaEvent(Team playerTeam)
        {
            if (UnityEngine.Random.value > _randomInterviewChance)
                return null;

            var eventTypes = new[] {
                PressConferenceType.WeeklyInterview,
                PressConferenceType.RumorResponse,
                PressConferenceType.PlayerQuestion
            };

            var type = eventTypes[UnityEngine.Random.Range(0, eventTypes.Length)];

            var conference = new PressConference
            {
                Type = type,
                Date = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                Context = new PressConferenceContext()
            };

            conference.Questions = type switch
            {
                PressConferenceType.WeeklyInterview => GenerateWeeklyQuestions(playerTeam),
                PressConferenceType.RumorResponse => GenerateRumorQuestions(playerTeam),
                PressConferenceType.PlayerQuestion => GeneratePlayerQuestions(playerTeam),
                _ => new List<MediaQuestion>()
            };

            if (conference.Questions.Count > 0)
            {
                _upcomingConferences.Add(conference);
                OnPressConferenceAvailable?.Invoke(conference);
                return conference;
            }

            return null;
        }

        #endregion

        #region Question Generation

        private List<MediaQuestion> GeneratePostGameQuestions(PressConferenceContext context, GameResult result, Team team)
        {
            var questions = new List<MediaQuestion>();

            // Question 1: Game result reaction
            if (context.Won)
            {
                if (context.ScoreDifference >= 20)
                {
                    questions.Add(CreateQuestion(
                        QuestionTopic.GameResult,
                        $"Coach, that was a dominant performance against {context.OpponentName}. What was working so well tonight?",
                        new[] {
                            CreateResponse("Credit team execution", ResponseTone.Professional, 0, 3, "The players executed the game plan perfectly. Credit to them for their preparation."),
                            CreateResponse("Praise specific players", ResponseTone.Supportive, 2, 5, "I have to call out our starters - they were locked in from the opening tip."),
                            CreateResponse("Stay humble", ResponseTone.Reserved, -1, 1, "It's just one game. We'll enjoy this tonight but we've got work to do."),
                            CreateResponse("Dismiss opponent", ResponseTone.Arrogant, 5, -5, "We're just a better team. Simple as that.")
                        }
                    ));
                }
                else
                {
                    questions.Add(CreateQuestion(
                        QuestionTopic.GameResult,
                        $"A close win tonight. What got you over the hump against {context.OpponentName}?",
                        new[] {
                            CreateResponse("Credit team toughness", ResponseTone.Professional, 0, 3, "These guys showed real mental toughness down the stretch."),
                            CreateResponse("Highlight clutch plays", ResponseTone.Supportive, 1, 4, "Our veterans made plays when it mattered. That's experience."),
                            CreateResponse("Focus on improvements", ResponseTone.Reserved, 0, 2, "We'll take the win, but we need to close games out better."),
                            CreateResponse("Show frustration", ResponseTone.Aggressive, 3, -2, "We shouldn't have let it get that close. We need to be better.")
                        }
                    ));
                }
            }
            else
            {
                if (context.ScoreDifference >= 20)
                {
                    questions.Add(CreateQuestion(
                        QuestionTopic.GameResult,
                        $"A tough loss to {context.OpponentName}. What went wrong out there?",
                        new[] {
                            CreateResponse("Take responsibility", ResponseTone.Professional, 2, 5, "That's on me. I didn't have them prepared. I'll be better."),
                            CreateResponse("Protect players", ResponseTone.Supportive, 0, 4, "These guys will bounce back. One game doesn't define us."),
                            CreateResponse("Deflect blame", ResponseTone.Deflecting, -2, -3, "They hit some tough shots. Sometimes that happens."),
                            CreateResponse("Express anger", ResponseTone.Aggressive, 4, -4, "Unacceptable. We didn't compete. That can't happen again.")
                        }
                    ));
                }
                else
                {
                    questions.Add(CreateQuestion(
                        QuestionTopic.GameResult,
                        $"Another close one slips away. What's the message to the team?",
                        new[] {
                            CreateResponse("Stay positive", ResponseTone.Supportive, -1, 3, "We're right there. Keep working, the wins will come."),
                            CreateResponse("Demand more", ResponseTone.Aggressive, 3, 0, "We need to find a way to win these games. Period."),
                            CreateResponse("Analyze calmly", ResponseTone.Professional, 0, 2, "Close games come down to execution. We'll look at the film."),
                            CreateResponse("Show frustration", ResponseTone.Frustrated, 2, -2, "I'm tired of moral victories. We need real ones.")
                        }
                    ));
                }
            }

            // Question 2: Specific player performance
            var topScorer = GetTopScorer(result.BoxScore, team);
            if (topScorer != null)
            {
                var stats = result.BoxScore.GetPlayerStats(topScorer.PlayerId);
                questions.Add(CreateQuestion(
                    QuestionTopic.PlayerPerformance,
                    $"{topScorer.DisplayName} had {stats.Points} points tonight. Talk about their performance.",
                    new[] {
                        CreateResponse("Praise publicly", ResponseTone.Supportive, 0, 5, $"{topScorer.FirstName} was incredible. That's the player we know they can be.", topScorer.PlayerId, 8),
                        CreateResponse("Credit team context", ResponseTone.Professional, 0, 3, "Great individual performance, but it happened because teammates found them.", topScorer.PlayerId, 3),
                        CreateResponse("Set expectations", ResponseTone.Reserved, 1, 1, "That's what we expect from a player of their caliber.", topScorer.PlayerId, -2),
                        CreateResponse("Critique aspects", ResponseTone.Demanding, 2, -2, "Good scoring, but I want to see that effort on the defensive end too.", topScorer.PlayerId, -5)
                    }
                ));
            }

            // Question 3: Strategy/Coaching question
            questions.Add(CreateQuestion(
                QuestionTopic.Strategy,
                "Walk us through some of your decisions in the fourth quarter.",
                new[] {
                    CreateResponse("Explain in detail", ResponseTone.Professional, 0, 2, "We went small to switch everything defensively. Wanted to take away their sets."),
                    CreateResponse("Deflect", ResponseTone.Reserved, -1, 0, "I trust my guys to make plays. I just put them in position."),
                    CreateResponse("Credit assistants", ResponseTone.Supportive, 0, 3, "My staff does tremendous work. That sequence was all them."),
                    CreateResponse("Refuse to reveal", ResponseTone.Aggressive, 2, -1, "I'm not going to give away our game plan. Next question.")
                }
            ));

            return questions;
        }

        private List<MediaQuestion> GenerateWeeklyQuestions(Team team)
        {
            var questions = new List<MediaQuestion>();
            var record = GetTeamRecord(team.TeamId);

            // Question about season progress
            questions.Add(CreateQuestion(
                QuestionTopic.SeasonProgress,
                $"You're {record.wins}-{record.losses} on the season. Where does this team stand right now?",
                new[] {
                    CreateResponse("Confident", ResponseTone.Confident, 2, 3, "We're right where we want to be. This team is going to make some noise."),
                    CreateResponse("Process-focused", ResponseTone.Professional, 0, 2, "We're improving every day. That's all I can ask."),
                    CreateResponse("Concerned", ResponseTone.Reserved, -1, 0, "We have work to do. Not where we expected to be."),
                    CreateResponse("Deflect expectations", ResponseTone.Deflecting, -2, -1, "Records don't matter until April. We'll be ready when it counts.")
                }
            ));

            // Question about upcoming opponent
            questions.Add(CreateQuestion(
                QuestionTopic.UpcomingGame,
                "What are you expecting from your next opponent?",
                new[] {
                    CreateResponse("Show respect", ResponseTone.Professional, 0, 2, "They're a quality team. We'll need to be sharp."),
                    CreateResponse("Express confidence", ResponseTone.Confident, 2, 2, "If we play our game, we'll be fine."),
                    CreateResponse("Downplay", ResponseTone.Arrogant, 3, -3, "Just another game on the schedule."),
                    CreateResponse("Focus on self", ResponseTone.Reserved, 0, 1, "I worry about us, not them.")
                }
            ));

            return questions;
        }

        private List<MediaQuestion> GenerateRumorQuestions(Team team)
        {
            var questions = new List<MediaQuestion>();

            // Find a player who might be unhappy (for trade rumor)
            Player unhappyPlayer = null;
            foreach (var player in team.Roster)
            {
                if (player.Morale < 40)
                {
                    unhappyPlayer = player;
                    break;
                }
            }

            if (unhappyPlayer != null)
            {
                questions.Add(CreateQuestion(
                    QuestionTopic.TradeRumor,
                    $"There are rumors that {unhappyPlayer.DisplayName} is unhappy. Any truth to that?",
                    new[] {
                        CreateResponse("Deny publicly", ResponseTone.Professional, 0, 2, $"I talk to {unhappyPlayer.FirstName} every day. We're good.", unhappyPlayer.PlayerId, 5),
                        CreateResponse("Acknowledge privately", ResponseTone.Reserved, 1, 0, "We handle our issues internally. Next question.", unhappyPlayer.PlayerId, 0),
                        CreateResponse("Confirm rumors", ResponseTone.Honest, 3, -4, "There have been some conversations. We're working through it.", unhappyPlayer.PlayerId, -8),
                        CreateResponse("Attack media", ResponseTone.Aggressive, 4, -3, "Where do you get this stuff? Focus on basketball.", unhappyPlayer.PlayerId, 2)
                    }
                ));
            }
            else
            {
                // Generic rumor question
                questions.Add(CreateQuestion(
                    QuestionTopic.TradeRumor,
                    "With the trade deadline approaching, is this team looking to make moves?",
                    new[] {
                        CreateResponse("Open to deals", ResponseTone.Professional, 1, 0, "We're always looking to improve. That's our job."),
                        CreateResponse("Satisfied with roster", ResponseTone.Confident, 0, 3, "I love this group. We have what we need."),
                        CreateResponse("Deflect", ResponseTone.Reserved, 0, 1, "That's a front office question. I coach who's here."),
                        CreateResponse("Aggressive stance", ResponseTone.Aggressive, 2, -1, "If you're not helping, you're gone. Simple.")
                    }
                ));
            }

            return questions;
        }

        private List<MediaQuestion> GeneratePlayerQuestions(Team team)
        {
            var questions = new List<MediaQuestion>();

            // Random player development question
            var youngPlayers = team.Roster.Where(p => p.YearsInLeague <= 3).ToList();
            if (youngPlayers.Count > 0)
            {
                var player = youngPlayers[UnityEngine.Random.Range(0, youngPlayers.Count)];
                questions.Add(CreateQuestion(
                    QuestionTopic.PlayerDevelopment,
                    $"How do you see {player.DisplayName}'s development progressing?",
                    new[] {
                        CreateResponse("Praise publicly", ResponseTone.Supportive, 0, 4, $"{player.FirstName} has a bright future. Working hard every day.", player.PlayerId, 10),
                        CreateResponse("Set expectations", ResponseTone.Professional, 1, 2, "Still learning. Long way to go but the talent is there.", player.PlayerId, 3),
                        CreateResponse("Demand more", ResponseTone.Demanding, 2, -1, "I need to see more consistency. Potential doesn't win games.", player.PlayerId, -5),
                        CreateResponse("Protect from pressure", ResponseTone.Reserved, 0, 2, "Let them develop in peace. Stop putting so much on young players.", player.PlayerId, 5)
                    }
                ));
            }

            return questions;
        }

        #endregion

        #region Response Processing

        /// <summary>
        /// Process the coach's response to a question.
        /// </summary>
        public MediaResponseResult ProcessResponse(MediaQuestion question, MediaResponse response)
        {
            var result = new MediaResponseResult
            {
                Question = question,
                Response = response,
                Consequences = new List<MediaConsequence>()
            };

            string coachId = GameManager.Instance?.Career?.CoachId ?? "player";

            // Update coach persona tracking
            UpdateCoachPersona(coachId, response.Tone);

            // Apply reputation change
            int reputationChange = response.ReputationImpact;

            // Modify based on consistency with persona
            var persona = GetCoachPersona(coachId);
            if (IsConsistentWithPersona(response.Tone, persona))
            {
                reputationChange += 1; // Small bonus for consistency
            }
            else if (IsContradictoryToPersona(response.Tone, persona))
            {
                reputationChange -= 2; // Penalty for being inconsistent
                result.Consequences.Add(new MediaConsequence
                {
                    Type = ConsequenceType.InconsistentPersona,
                    Description = "Media notes your response contradicts your usual demeanor."
                });
            }

            // Apply to media reputation
            AdjustReputation(coachId, reputationChange);
            result.ReputationChange = reputationChange;

            // Apply player morale effect if applicable
            if (!string.IsNullOrEmpty(response.AffectedPlayerId) && response.MoraleImpact != 0)
            {
                var moraleManager = MoraleChemistryManager.Instance;
                if (moraleManager != null)
                {
                    if (response.MoraleImpact > 0)
                    {
                        moraleManager.PraisePlayer(response.AffectedPlayerId);
                    }
                    else
                    {
                        moraleManager.CriticizePlayer(response.AffectedPlayerId);
                    }
                }

                result.Consequences.Add(new MediaConsequence
                {
                    Type = response.MoraleImpact > 0 ? ConsequenceType.PlayerMoraleBoost : ConsequenceType.PlayerMoraleDrop,
                    PlayerId = response.AffectedPlayerId,
                    Description = response.MoraleImpact > 0
                        ? "Player appreciates the public support."
                        : "Player is upset by public criticism."
                });
            }

            // Apply team morale effect for aggressive/supportive responses
            if (response.Tone == ResponseTone.Aggressive && response.TeamMoraleImpact < 0)
            {
                result.Consequences.Add(new MediaConsequence
                {
                    Type = ConsequenceType.TeamMoraleDrop,
                    Description = "Some players uncomfortable with aggressive public stance."
                });
            }
            else if (response.Tone == ResponseTone.Supportive && response.TeamMoraleImpact > 0)
            {
                result.Consequences.Add(new MediaConsequence
                {
                    Type = ConsequenceType.TeamMoraleBoost,
                    Description = "Team appreciates coach having their back publicly."
                });
            }

            // Generate headline
            var headline = GenerateHeadline(question, response, result);
            _recentHeadlines.Insert(0, headline);
            if (_recentHeadlines.Count > _maxStoredHeadlines)
            {
                _recentHeadlines.RemoveAt(_recentHeadlines.Count - 1);
            }
            result.Headline = headline;
            OnHeadlineGenerated?.Invoke(headline);

            // Mark conference complete
            _upcomingConferences.RemoveAll(c => c.Questions.Contains(question));

            return result;
        }

        /// <summary>
        /// Skip a press conference (has negative consequences).
        /// </summary>
        public void SkipPressConference(PressConference conference)
        {
            string coachId = GameManager.Instance?.Career?.CoachId ?? "player";

            // Reputation hit for skipping
            AdjustReputation(coachId, -5);

            // Generate negative headline
            var headline = new MediaHeadline
            {
                Date = conference.Date,
                HeadlineText = $"Coach {GameManager.Instance?.Career?.LastName ?? "Coach"} Dodges Media After Game",
                Tone = HeadlineTone.Negative,
                IsAboutPlayer = false
            };

            _recentHeadlines.Insert(0, headline);
            OnHeadlineGenerated?.Invoke(headline);

            _upcomingConferences.Remove(conference);
        }

        #endregion

        #region Headline Generation

        private MediaHeadline GenerateHeadline(MediaQuestion question, MediaResponse response, MediaResponseResult result)
        {
            string coachName = GameManager.Instance?.Career?.LastName ?? "Coach";
            string text;
            HeadlineTone tone;

            switch (response.Tone)
            {
                case ResponseTone.Aggressive:
                case ResponseTone.Frustrated:
                    text = $"Coach {coachName} Fires Back: \"{GetHeadlineQuote(response.ResponseText)}\"";
                    tone = HeadlineTone.Controversial;
                    break;

                case ResponseTone.Arrogant:
                    text = $"Bold Claims from {coachName}: \"{GetHeadlineQuote(response.ResponseText)}\"";
                    tone = HeadlineTone.Controversial;
                    break;

                case ResponseTone.Supportive:
                    text = $"{coachName} Praises Team: \"{GetHeadlineQuote(response.ResponseText)}\"";
                    tone = HeadlineTone.Positive;
                    break;

                case ResponseTone.Demanding:
                    text = $"{coachName} Demands More: \"{GetHeadlineQuote(response.ResponseText)}\"";
                    tone = HeadlineTone.Neutral;
                    break;

                default:
                    text = $"{coachName} on Tonight's Game: \"{GetHeadlineQuote(response.ResponseText)}\"";
                    tone = HeadlineTone.Neutral;
                    break;
            }

            return new MediaHeadline
            {
                Date = GameManager.Instance?.CurrentDate ?? DateTime.Now,
                HeadlineText = text,
                Tone = tone,
                IsAboutPlayer = !string.IsNullOrEmpty(response.AffectedPlayerId),
                PlayerId = response.AffectedPlayerId
            };
        }

        private string GetHeadlineQuote(string fullResponse)
        {
            // Take first sentence or truncate
            int periodIndex = fullResponse.IndexOf('.');
            if (periodIndex > 0 && periodIndex < 50)
            {
                return fullResponse.Substring(0, periodIndex);
            }
            if (fullResponse.Length > 40)
            {
                return fullResponse.Substring(0, 37) + "...";
            }
            return fullResponse;
        }

        #endregion

        #region Coach Persona

        private void UpdateCoachPersona(string coachId, ResponseTone tone)
        {
            if (!_coachPersonas.TryGetValue(coachId, out var persona))
            {
                persona = new CoachMediaPersona();
                _coachPersonas[coachId] = persona;
            }

            persona.TotalResponses++;

            switch (tone)
            {
                case ResponseTone.Aggressive:
                case ResponseTone.Frustrated:
                    persona.AggressiveCount++;
                    break;
                case ResponseTone.Supportive:
                    persona.SupportiveCount++;
                    break;
                case ResponseTone.Professional:
                case ResponseTone.Reserved:
                    persona.ProfessionalCount++;
                    break;
                case ResponseTone.Arrogant:
                case ResponseTone.Confident:
                    persona.ConfidentCount++;
                    break;
            }
        }

        private CoachMediaPersona GetCoachPersona(string coachId)
        {
            return _coachPersonas.GetValueOrDefault(coachId, new CoachMediaPersona());
        }

        private bool IsConsistentWithPersona(ResponseTone tone, CoachMediaPersona persona)
        {
            if (persona.TotalResponses < 5) return true; // Not enough data

            float threshold = 0.4f;
            return tone switch
            {
                ResponseTone.Aggressive or ResponseTone.Frustrated =>
                    persona.AggressiveRatio > threshold,
                ResponseTone.Supportive =>
                    persona.SupportiveRatio > threshold,
                ResponseTone.Professional or ResponseTone.Reserved =>
                    persona.ProfessionalRatio > threshold,
                _ => true
            };
        }

        private bool IsContradictoryToPersona(ResponseTone tone, CoachMediaPersona persona)
        {
            if (persona.TotalResponses < 5) return false;

            float highThreshold = 0.5f;
            float lowThreshold = 0.1f;

            // If you're usually aggressive but suddenly supportive, that's notable
            if ((tone == ResponseTone.Supportive) && persona.AggressiveRatio > highThreshold)
                return true;
            if ((tone == ResponseTone.Aggressive) && persona.SupportiveRatio > highThreshold)
                return true;

            return false;
        }

        #endregion

        #region Reputation Management

        private void AdjustReputation(string coachId, int amount)
        {
            int current = _mediaReputation.GetValueOrDefault(coachId, 0);
            int newValue = Mathf.Clamp(current + amount, -100, 100);
            _mediaReputation[coachId] = newValue;

            OnReputationChanged?.Invoke(coachId, newValue);
        }

        /// <summary>
        /// Gets the coach's media reputation (-100 to +100).
        /// </summary>
        public int GetMediaReputation(string coachId)
        {
            return _mediaReputation.GetValueOrDefault(coachId, 0);
        }

        /// <summary>
        /// Process daily reputation decay toward neutral.
        /// </summary>
        public void ProcessDailyReputation()
        {
            var keys = _mediaReputation.Keys.ToList();
            foreach (var key in keys)
            {
                int current = _mediaReputation[key];
                if (current > 0)
                {
                    _mediaReputation[key] = (int)Math.Max(0, current - _reputationDecayRate);
                }
                else if (current < 0)
                {
                    _mediaReputation[key] = (int)Math.Min(0, current + _reputationDecayRate);
                }
            }
        }

        #endregion

        #region Helpers

        private MediaQuestion CreateQuestion(QuestionTopic topic, string text, MediaResponse[] responses)
        {
            return new MediaQuestion
            {
                Topic = topic,
                QuestionText = text,
                PossibleResponses = responses.ToList()
            };
        }

        private MediaResponse CreateResponse(string label, ResponseTone tone, int teamMorale, int reputation, string text, string playerId = null, int playerMorale = 0)
        {
            return new MediaResponse
            {
                Label = label,
                Tone = tone,
                TeamMoraleImpact = teamMorale,
                ReputationImpact = reputation,
                ResponseText = text,
                AffectedPlayerId = playerId,
                MoraleImpact = playerMorale
            };
        }

        private Player GetTopScorer(BoxScore boxScore, Team team)
        {
            Player topScorer = null;
            int topPoints = 0;

            foreach (var player in team.Roster)
            {
                var stats = boxScore.GetPlayerStats(player.PlayerId);
                if (stats != null && stats.Points > topPoints)
                {
                    topPoints = stats.Points;
                    topScorer = player;
                }
            }

            return topScorer;
        }

        private (int wins, int losses) GetTeamRecord(string teamId)
        {
            var team = GameManager.Instance?.GetTeam(teamId);
            return (team?.Wins ?? 0, team?.Losses ?? 0);
        }

        #endregion
    }

    #region Data Types

    [Serializable]
    public class PressConference
    {
        public PressConferenceType Type;
        public DateTime Date;
        public PressConferenceContext Context;
        public List<MediaQuestion> Questions = new List<MediaQuestion>();
    }

    [Serializable]
    public class PressConferenceContext
    {
        public bool Won;
        public int ScoreDifference;
        public string OpponentName;
        public bool IsPlayoff;
        public int WinStreak;
        public int LossStreak;
    }

    [Serializable]
    public class MediaQuestion
    {
        public QuestionTopic Topic;
        public string QuestionText;
        public List<MediaResponse> PossibleResponses = new List<MediaResponse>();
    }

    [Serializable]
    public class MediaResponse
    {
        public string Label;
        public ResponseTone Tone;
        public int TeamMoraleImpact;
        public int ReputationImpact;
        public string ResponseText;
        public string AffectedPlayerId;
        public int MoraleImpact;
    }

    [Serializable]
    public class MediaResponseResult
    {
        public MediaQuestion Question;
        public MediaResponse Response;
        public int ReputationChange;
        public List<MediaConsequence> Consequences = new List<MediaConsequence>();
        public MediaHeadline Headline;
    }

    [Serializable]
    public class MediaConsequence
    {
        public ConsequenceType Type;
        public string PlayerId;
        public string Description;
    }

    [Serializable]
    public class MediaHeadline
    {
        public DateTime Date;
        public string HeadlineText;
        public HeadlineTone Tone;
        public bool IsAboutPlayer;
        public string PlayerId;
    }

    [Serializable]
    public class MediaEvent
    {
        public DateTime Date;
        public PressConferenceType Type;
        public string Summary;
    }

    [Serializable]
    public class CoachMediaPersona
    {
        public int TotalResponses;
        public int AggressiveCount;
        public int SupportiveCount;
        public int ProfessionalCount;
        public int ConfidentCount;

        public float AggressiveRatio => TotalResponses > 0 ? (float)AggressiveCount / TotalResponses : 0;
        public float SupportiveRatio => TotalResponses > 0 ? (float)SupportiveCount / TotalResponses : 0;
        public float ProfessionalRatio => TotalResponses > 0 ? (float)ProfessionalCount / TotalResponses : 0;
        public float ConfidentRatio => TotalResponses > 0 ? (float)ConfidentCount / TotalResponses : 0;
    }

    public enum PressConferenceType
    {
        PostGame,
        PreGame,
        WeeklyInterview,
        RumorResponse,
        PlayerQuestion,
        TradeDeadline,
        PlayoffPreview
    }

    public enum QuestionTopic
    {
        GameResult,
        PlayerPerformance,
        Strategy,
        SeasonProgress,
        UpcomingGame,
        TradeRumor,
        PlayerDevelopment,
        TeamChemistry,
        Injury
    }

    public enum ResponseTone
    {
        Professional,
        Supportive,
        Aggressive,
        Reserved,
        Confident,
        Arrogant,
        Deflecting,
        Frustrated,
        Honest,
        Demanding
    }

    public enum ConsequenceType
    {
        PlayerMoraleBoost,
        PlayerMoraleDrop,
        TeamMoraleBoost,
        TeamMoraleDrop,
        ReputationChange,
        InconsistentPersona,
        OwnerNotice
    }

    public enum HeadlineTone
    {
        Positive,
        Negative,
        Neutral,
        Controversial
    }

    #endregion
}
