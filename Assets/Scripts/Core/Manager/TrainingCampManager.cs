using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages training camp and preseason activities.
    /// Handles roster cuts, playbook installation, chemistry building, and preseason games.
    /// </summary>
    public class TrainingCampManager : MonoBehaviour
    {
        public static TrainingCampManager Instance { get; private set; }

        #region Configuration

        [Header("Training Camp Settings")]
        [SerializeField] private int _maxRosterSize = 15;
        [SerializeField] private int _twoWayContractSlots = 2;
        [SerializeField] private int _preseasonGames = 4;
        [SerializeField] private float _basePlaybookFamiliarityGain = 5f;
        [SerializeField] private float _baseChemistryGain = 2f;

        #endregion

        #region State

        [Header("Camp State")]
        [SerializeField] private TrainingCampPhase _currentPhase = TrainingCampPhase.NotStarted;
        [SerializeField] private int _campDay = 0;
        [SerializeField] private int _totalCampDays = 14;

        private Dictionary<string, CampPlayerStatus> _playerStatuses = new Dictionary<string, CampPlayerStatus>();
        private List<PreseasonGame> _preseasonSchedule = new List<PreseasonGame>();
        private List<RosterCutDecision> _pendingCuts = new List<RosterCutDecision>();

        #endregion

        #region Events

        public event Action<TrainingCampPhase> OnPhaseChanged;
        public event Action<CampDayReport> OnDayComplete;
        public event Action<RosterCutDecision> OnPlayerCut;
        public event Action<PreseasonGame> OnPreseasonGameComplete;
        public event Action OnCampComplete;

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
            GameManager.Instance?.RegisterTrainingCampManager(this);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Start training camp for a team
        /// </summary>
        public void StartTrainingCamp(Team team, List<Player> rosterPlayers)
        {
            Debug.Log($"[TrainingCamp] Starting training camp for {team.Name}");

            _currentPhase = TrainingCampPhase.EarlyCamp;
            _campDay = 0;
            _playerStatuses.Clear();
            _pendingCuts.Clear();

            // Initialize player statuses
            foreach (var player in rosterPlayers)
            {
                _playerStatuses[player.PlayerId] = new CampPlayerStatus
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.FullName,
                    ContractType = player.Contract?.Type ?? ContractType.Standard,
                    IsGuaranteed = player.Contract?.IsGuaranteed ?? false,
                    CampGrade = CalculateInitialCampGrade(player),
                    PracticePerformance = 50f,
                    InjuryRisk = CalculateInjuryRisk(player),
                    IsCompetingForSpot = !player.Contract?.IsGuaranteed ?? true
                };
            }

            // Generate preseason schedule
            GeneratePreseasonSchedule(team);

            OnPhaseChanged?.Invoke(_currentPhase);
        }

        /// <summary>
        /// Advance one day of training camp
        /// </summary>
        public CampDayReport AdvanceCampDay(Team team, List<Player> players, TrainingFocus focus)
        {
            _campDay++;

            var report = new CampDayReport
            {
                Day = _campDay,
                Phase = _currentPhase,
                Focus = focus
            };

            // Simulate practice
            foreach (var player in players)
            {
                if (!_playerStatuses.ContainsKey(player.PlayerId)) continue;

                var status = _playerStatuses[player.PlayerId];
                SimulatePracticeDay(player, status, focus, report);
            }

            // Chemistry building
            float chemistryGain = _baseChemistryGain * GetFocusMultiplier(focus, TrainingFocus.TeamBuilding);
            team.TeamChemistry = Math.Min(100, team.TeamChemistry + chemistryGain);
            report.ChemistryGain = chemistryGain;

            // Check for phase transitions
            UpdatePhase();

            // Generate standout performers
            report.StandoutPerformers = GetStandoutPerformers(players).Take(3).ToList();
            report.StrugglingPlayers = GetStrugglingPlayers(players).Take(2).ToList();

            OnDayComplete?.Invoke(report);

            return report;
        }

        /// <summary>
        /// Make roster cut decision
        /// </summary>
        public RosterCutResult CutPlayer(string playerId, Team team, List<Player> roster)
        {
            var player = roster.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null)
            {
                return new RosterCutResult { Success = false, Reason = "Player not found" };
            }

            if (!_playerStatuses.ContainsKey(playerId))
            {
                return new RosterCutResult { Success = false, Reason = "Player not in camp" };
            }

            var status = _playerStatuses[playerId];

            // Check if player can be cut (non-guaranteed contracts only)
            if (status.IsGuaranteed)
            {
                return new RosterCutResult
                {
                    Success = false,
                    Reason = "Cannot cut player with guaranteed contract without dead cap hit"
                };
            }

            var decision = new RosterCutDecision
            {
                PlayerId = playerId,
                PlayerName = player.FullName,
                CampGrade = status.CampGrade,
                WasCut = true,
                Reason = "Released during training camp"
            };

            _pendingCuts.Add(decision);
            _playerStatuses.Remove(playerId);

            OnPlayerCut?.Invoke(decision);

            return new RosterCutResult
            {
                Success = true,
                Decision = decision,
                DeadCap = 0 // Non-guaranteed = no dead cap
            };
        }

        /// <summary>
        /// Get roster cut recommendations
        /// </summary>
        public List<RosterCutRecommendation> GetCutRecommendations(Team team, List<Player> roster)
        {
            var recommendations = new List<RosterCutRecommendation>();

            int currentRosterSize = roster.Count;
            int spotsToOpen = currentRosterSize - _maxRosterSize;

            if (spotsToOpen <= 0) return recommendations;

            // Sort by camp performance and contract status
            var candidates = roster
                .Where(p => _playerStatuses.ContainsKey(p.PlayerId) && !_playerStatuses[p.PlayerId].IsGuaranteed)
                .Select(p => new
                {
                    Player = p,
                    Status = _playerStatuses[p.PlayerId]
                })
                .OrderBy(x => x.Status.CampGrade)
                .Take(spotsToOpen + 2) // Show a few extra options
                .ToList();

            foreach (var c in candidates)
            {
                recommendations.Add(new RosterCutRecommendation
                {
                    PlayerId = c.Player.PlayerId,
                    PlayerName = c.Player.FullName,
                    Position = c.Player.Position.ToString(),
                    CampGrade = c.Status.CampGrade,
                    ContractType = c.Status.ContractType,
                    Reason = GetCutRecommendationReason(c.Player, c.Status)
                });
            }

            return recommendations;
        }

        /// <summary>
        /// Simulate a preseason game
        /// </summary>
        public PreseasonGameResult SimulatePreseasonGame(PreseasonGame game, Team playerTeam, Team opponent,
            List<Player> playerRoster, List<Player> opponentRoster)
        {
            // Simple simulation - focus on player evaluation
            var result = new PreseasonGameResult
            {
                GameId = game.GameId,
                PlayerTeamScore = UnityEngine.Random.Range(85, 120),
                OpponentScore = UnityEngine.Random.Range(85, 120),
                PlayerPerformances = new List<PreseasonPlayerPerformance>()
            };

            result.Won = result.PlayerTeamScore > result.OpponentScore;

            // Evaluate each player
            foreach (var player in playerRoster)
            {
                if (!_playerStatuses.ContainsKey(player.PlayerId)) continue;

                var perf = SimulatePlayerPreseasonGame(player, _playerStatuses[player.PlayerId]);
                result.PlayerPerformances.Add(perf);

                // Update camp status based on performance
                UpdateCampStatusFromGame(player.PlayerId, perf);
            }

            // Mark game complete
            game.IsComplete = true;
            game.PlayerTeamScore = result.PlayerTeamScore;
            game.OpponentScore = result.OpponentScore;

            OnPreseasonGameComplete?.Invoke(game);

            return result;
        }

        /// <summary>
        /// Get current training camp status
        /// </summary>
        public TrainingCampStatus GetStatus()
        {
            return new TrainingCampStatus
            {
                Phase = _currentPhase,
                Day = _campDay,
                TotalDays = _totalCampDays,
                PlayerStatuses = _playerStatuses.Values.ToList(),
                PreseasonSchedule = _preseasonSchedule,
                PendingCuts = _pendingCuts,
                RosterSpotsRemaining = _maxRosterSize - _playerStatuses.Count(s => !_pendingCuts.Any(c => c.PlayerId == s.Key))
            };
        }

        /// <summary>
        /// Complete training camp and finalize roster
        /// </summary>
        public void CompleteCamp()
        {
            _currentPhase = TrainingCampPhase.Complete;
            OnPhaseChanged?.Invoke(_currentPhase);
            OnCampComplete?.Invoke();

            Debug.Log($"[TrainingCamp] Camp complete. Final roster: {_playerStatuses.Count} players");
        }

        #endregion

        #region Playbook Management

        /// <summary>
        /// Install/practice a play during camp
        /// </summary>
        public void PracticePlay(PlayBook playbook, string playId, float practiceIntensity = 1f)
        {
            var play = playbook.Plays.FirstOrDefault(p => p.PlayId == playId);
            if (play == null) return;

            float gain = _basePlaybookFamiliarityGain * practiceIntensity;
            play.Familiarity = Math.Min(100, play.Familiarity + gain);

            Debug.Log($"[TrainingCamp] Practiced {play.PlayName}. Familiarity: {play.Familiarity:F0}%");
        }

        /// <summary>
        /// Install a new play to the playbook
        /// </summary>
        public void InstallNewPlay(PlayBook playbook, SetPlay newPlay)
        {
            if (playbook.Plays.Any(p => p.PlayId == newPlay.PlayId)) return;

            newPlay.Familiarity = 10f; // Start with minimal familiarity
            playbook.Plays.Add(newPlay);

            Debug.Log($"[TrainingCamp] Installed new play: {newPlay.PlayName}");
        }

        /// <summary>
        /// Get plays that need the most practice
        /// </summary>
        public List<SetPlay> GetPlaysPrioritizedForPractice(PlayBook playbook)
        {
            return playbook.Plays
                .Where(p => p.Familiarity < 80)
                .OrderBy(p => p.Familiarity)
                .ToList();
        }

        #endregion

        #region Private Methods

        private float CalculateInitialCampGrade(Player player)
        {
            // Based on overall rating with some variance
            float baseGrade = player.OverallRating * 0.8f;
            float variance = UnityEngine.Random.Range(-10f, 10f);
            return Mathf.Clamp(baseGrade + variance, 0, 100);
        }

        private float CalculateInjuryRisk(Player player)
        {
            // Higher risk for older players and those with injury history
            float baseRisk = 5f;

            if (player.Age > 32) baseRisk += 5f;
            if (player.Age > 35) baseRisk += 10f;

            if (player.InjuryHistory != null && player.InjuryHistory.Count > 2)
                baseRisk += 10f;

            return Mathf.Clamp(baseRisk, 0, 50);
        }

        private void SimulatePracticeDay(Player player, CampPlayerStatus status, TrainingFocus focus, CampDayReport report)
        {
            // Performance variance
            float dailyPerformance = status.PracticePerformance + UnityEngine.Random.Range(-15f, 15f);

            // Update camp grade based on daily performance
            float gradeChange = (dailyPerformance - 50) * 0.1f * GetFocusMultiplier(focus, TrainingFocus.PlayerEvaluation);
            status.CampGrade = Mathf.Clamp(status.CampGrade + gradeChange, 0, 100);

            // Small injury chance during practice
            if (UnityEngine.Random.value < status.InjuryRisk * 0.001f)
            {
                report.Injuries.Add(new CampInjury
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.FullName,
                    Severity = "Minor",
                    DaysOut = UnityEngine.Random.Range(1, 5)
                });
            }

            // Attribute development based on focus
            ApplyFocusDevelopment(player, focus);
        }

        private void ApplyFocusDevelopment(Player player, TrainingFocus focus)
        {
            float gain = 0.1f; // Small daily gain

            switch (focus)
            {
                case TrainingFocus.Offense:
                    player.InsideScoring = Math.Min(99, player.InsideScoring + gain);
                    player.MidRange = Math.Min(99, player.MidRange + gain);
                    break;
                case TrainingFocus.Defense:
                    player.PerimeterDefense = Math.Min(99, player.PerimeterDefense + gain);
                    player.InteriorDefense = Math.Min(99, player.InteriorDefense + gain);
                    break;
                case TrainingFocus.Conditioning:
                    player.Stamina = Math.Min(99, player.Stamina + gain);
                    player.Speed = Math.Min(99, player.Speed + gain * 0.5f);
                    break;
                case TrainingFocus.Shooting:
                    player.ThreePoint = Math.Min(99, player.ThreePoint + gain);
                    player.FreeThrow = Math.Min(99, player.FreeThrow + gain);
                    break;
            }
        }

        private float GetFocusMultiplier(TrainingFocus current, TrainingFocus target)
        {
            return current == target ? 1.5f : 1f;
        }

        private void UpdatePhase()
        {
            if (_campDay >= 5 && _currentPhase == TrainingCampPhase.EarlyCamp)
            {
                _currentPhase = TrainingCampPhase.MidCamp;
                OnPhaseChanged?.Invoke(_currentPhase);
            }
            else if (_campDay >= 10 && _currentPhase == TrainingCampPhase.MidCamp)
            {
                _currentPhase = TrainingCampPhase.Preseason;
                OnPhaseChanged?.Invoke(_currentPhase);
            }
            else if (_campDay >= _totalCampDays && _currentPhase == TrainingCampPhase.Preseason)
            {
                _currentPhase = TrainingCampPhase.FinalCuts;
                OnPhaseChanged?.Invoke(_currentPhase);
            }
        }

        private void GeneratePreseasonSchedule(Team team)
        {
            _preseasonSchedule.Clear();

            for (int i = 0; i < _preseasonGames; i++)
            {
                _preseasonSchedule.Add(new PreseasonGame
                {
                    GameId = $"preseason_{i + 1}",
                    GameNumber = i + 1,
                    IsHome = i % 2 == 0,
                    OpponentTeamId = "TBD", // Would be filled with actual opponent
                    IsComplete = false
                });
            }
        }

        private List<CampPlayerStatus> GetStandoutPerformers(List<Player> players)
        {
            return _playerStatuses.Values
                .Where(s => s.CampGrade >= 75)
                .OrderByDescending(s => s.CampGrade)
                .ToList();
        }

        private List<CampPlayerStatus> GetStrugglingPlayers(List<Player> players)
        {
            return _playerStatuses.Values
                .Where(s => s.CampGrade < 50 && s.IsCompetingForSpot)
                .OrderBy(s => s.CampGrade)
                .ToList();
        }

        private string GetCutRecommendationReason(Player player, CampPlayerStatus status)
        {
            if (status.CampGrade < 40)
                return "Poor camp performance";
            if (player.Age > 34)
                return "Veteran with declining skills";
            if (status.ContractType == ContractType.TrainingCamp)
                return "Non-guaranteed camp contract";

            return "Roster depth consideration";
        }

        private PreseasonPlayerPerformance SimulatePlayerPreseasonGame(Player player, CampPlayerStatus status)
        {
            float performanceMultiplier = status.CampGrade / 75f;

            return new PreseasonPlayerPerformance
            {
                PlayerId = player.PlayerId,
                PlayerName = player.FullName,
                Minutes = UnityEngine.Random.Range(12, 28),
                Points = (int)(UnityEngine.Random.Range(4, 18) * performanceMultiplier),
                Rebounds = (int)(UnityEngine.Random.Range(1, 8) * performanceMultiplier),
                Assists = (int)(UnityEngine.Random.Range(0, 6) * performanceMultiplier),
                PerformanceGrade = status.CampGrade + UnityEngine.Random.Range(-10f, 10f)
            };
        }

        private void UpdateCampStatusFromGame(string playerId, PreseasonPlayerPerformance perf)
        {
            if (!_playerStatuses.ContainsKey(playerId)) return;

            var status = _playerStatuses[playerId];
            float gameImpact = (perf.PerformanceGrade - 50) * 0.2f;
            status.CampGrade = Mathf.Clamp(status.CampGrade + gameImpact, 0, 100);
        }

        #endregion

        #region Save/Load

        public TrainingCampSaveData CreateSaveData()
        {
            return new TrainingCampSaveData
            {
                Phase = _currentPhase,
                CampDay = _campDay,
                PlayerStatuses = _playerStatuses,
                PreseasonSchedule = _preseasonSchedule,
                PendingCuts = _pendingCuts
            };
        }

        public void RestoreFromSave(TrainingCampSaveData data)
        {
            if (data == null) return;

            _currentPhase = data.Phase;
            _campDay = data.CampDay;
            _playerStatuses = data.PlayerStatuses ?? new Dictionary<string, CampPlayerStatus>();
            _preseasonSchedule = data.PreseasonSchedule ?? new List<PreseasonGame>();
            _pendingCuts = data.PendingCuts ?? new List<RosterCutDecision>();
        }

        #endregion
    }

    #region Data Classes

    public enum TrainingCampPhase
    {
        NotStarted,
        EarlyCamp,      // Days 1-5: Conditioning, system installation
        MidCamp,        // Days 6-10: Scrimmages, roster evaluation
        Preseason,      // Days 11-14: Exhibition games
        FinalCuts,      // Final roster decisions
        Complete
    }

    public enum TrainingFocus
    {
        Offense,
        Defense,
        Conditioning,
        Shooting,
        PlaybookInstallation,
        TeamBuilding,
        PlayerEvaluation
    }

    [Serializable]
    public class CampPlayerStatus
    {
        public string PlayerId;
        public string PlayerName;
        public ContractType ContractType;
        public bool IsGuaranteed;
        public float CampGrade;
        public float PracticePerformance;
        public float InjuryRisk;
        public bool IsCompetingForSpot;
    }

    [Serializable]
    public class CampDayReport
    {
        public int Day;
        public TrainingCampPhase Phase;
        public TrainingFocus Focus;
        public float ChemistryGain;
        public List<CampPlayerStatus> StandoutPerformers = new List<CampPlayerStatus>();
        public List<CampPlayerStatus> StrugglingPlayers = new List<CampPlayerStatus>();
        public List<CampInjury> Injuries = new List<CampInjury>();
    }

    [Serializable]
    public class CampInjury
    {
        public string PlayerId;
        public string PlayerName;
        public string Severity;
        public int DaysOut;
    }

    [Serializable]
    public class RosterCutDecision
    {
        public string PlayerId;
        public string PlayerName;
        public float CampGrade;
        public bool WasCut;
        public string Reason;
    }

    [Serializable]
    public class RosterCutRecommendation
    {
        public string PlayerId;
        public string PlayerName;
        public string Position;
        public float CampGrade;
        public ContractType ContractType;
        public string Reason;
    }

    [Serializable]
    public class RosterCutResult
    {
        public bool Success;
        public string Reason;
        public RosterCutDecision Decision;
        public float DeadCap;
    }

    [Serializable]
    public class PreseasonGame
    {
        public string GameId;
        public int GameNumber;
        public bool IsHome;
        public string OpponentTeamId;
        public bool IsComplete;
        public int PlayerTeamScore;
        public int OpponentScore;
    }

    [Serializable]
    public class PreseasonGameResult
    {
        public string GameId;
        public bool Won;
        public int PlayerTeamScore;
        public int OpponentScore;
        public List<PreseasonPlayerPerformance> PlayerPerformances;
    }

    [Serializable]
    public class PreseasonPlayerPerformance
    {
        public string PlayerId;
        public string PlayerName;
        public int Minutes;
        public int Points;
        public int Rebounds;
        public int Assists;
        public float PerformanceGrade;
    }

    [Serializable]
    public class TrainingCampStatus
    {
        public TrainingCampPhase Phase;
        public int Day;
        public int TotalDays;
        public List<CampPlayerStatus> PlayerStatuses;
        public List<PreseasonGame> PreseasonSchedule;
        public List<RosterCutDecision> PendingCuts;
        public int RosterSpotsRemaining;
    }

    [Serializable]
    public class TrainingCampSaveData
    {
        public TrainingCampPhase Phase;
        public int CampDay;
        public Dictionary<string, CampPlayerStatus> PlayerStatuses;
        public List<PreseasonGame> PreseasonSchedule;
        public List<RosterCutDecision> PendingCuts;
    }

    #endregion
}
