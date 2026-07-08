using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// The player team's development desk. Wires the three designed-but-dormant
    /// systems into the week: a Wednesday practice (focus + intensity set by the
    /// player) whose gains flow through PlayerDevelopmentManager, mentorship
    /// sessions riding on practice quality, and weekly tendency training.
    /// Owns persistence for pairings, tendency training, and practice settings.
    /// </summary>
    public class DevelopmentSystem : IDailyTickable, ISaveSection
    {
        public string SystemId => "DevelopmentDesk";
        public int TickOrder => Manager.TickOrder.Mentorship - 10; // practice before Monday decay

        private readonly PlayerDatabase _db;
        private readonly PlayerDevelopmentManager _dev;
        private readonly InjuryManager _injuries;
        private readonly Func<string, Team> _teamLookup;

        // Player-facing weekly settings
        public PracticeFocus WeeklyFocus = PracticeFocus.Development;
        public PracticeIntensity WeeklyIntensity = PracticeIntensity.Normal;

        // Last-run summaries for the UI
        public string LastPracticeSummary { get; private set; } = "";
        public List<TrainingResult> LastTrainingResults { get; private set; } = new List<TrainingResult>();

        public DevelopmentSystem(
            PlayerDatabase db,
            PlayerDevelopmentManager dev,
            InjuryManager injuries,
            Func<string, Team> teamLookup)
        {
            _db = db;
            _dev = dev;
            _injuries = injuries;
            _teamLookup = teamLookup;

            // The practice and tendency engines are lazy singletons that do
            // nothing until their dependency providers are set.
            PracticeManager.Instance.Initialize(
                id => _db?.GetPlayer(id),
                id => null,                    // legacy Coach registry — quality falls back
                id => _teamLookup?.Invoke(id),
                id => null);
            TendencyCoachingManager.Instance.Initialize(
                id => _db?.GetPlayer(id),
                id => null);
        }

        public static DevelopmentSystem CreateDefault(GameManager gm)
        {
            return new DevelopmentSystem(gm.PlayerDatabase, gm.Development, gm.InjuryManager, gm.GetTeam);
        }

        // ==================== WEEKLY CYCLE ====================

        public void DailyTick(in DailyTickContext ctx)
        {
            if (OffseasonManager.Instance != null && OffseasonManager.Instance.EngineActive) return;
            if (ctx.Date.DayOfWeek != DayOfWeek.Wednesday) return;
            if (string.IsNullOrEmpty(ctx.PlayerTeamId)) return;

            RunWeeklyDevelopment(ctx.PlayerTeamId, ctx.Date, ctx.Game);
        }

        /// <summary>
        /// One development week for the player's team: practice (skipped on game
        /// days), mentorship sessions, tendency training. Public for tests.
        /// </summary>
        public void RunWeeklyDevelopment(string teamId, DateTime date, GameManager gm)
        {
            var team = _teamLookup?.Invoke(teamId);
            if (team == null) return;

            bool gameDay = gm?.SeasonController?.GetTodaysGame() != null;

            int practiceQuality = 50;
            if (!gameDay)
                practiceQuality = RunPractice(team, date);

            RunMentorshipSessions(team, practiceQuality);
            RunTendencyTraining(team);
        }

        private int RunPractice(Team team, DateTime date)
        {
            var pm = PracticeManager.Instance;
            var context = new ScheduleContext
            {
                IsGameDay = false,
                DaysUntilNextGame = 2,
                IsPlayoffs = PlayoffManager.Instance?.IsPlayoffsActive ?? false
            };

            var session = pm.GenerateRecommendedPractice(team.TeamId, date, context);
            if (session == null) return 50;

            session.PrimaryFocus = WeeklyFocus;
            session.Intensity = WeeklyIntensity;

            var results = pm.ExecutePractice(session);
            if (results == null) return 50;

            // Skill gains land through the development manager (potential caps,
            // development logs) — practice itself only tracks progress.
            _dev?.ProcessWeeklyPracticeDevelopment(team.TeamId,
                new List<PracticeResults> { results },
                PersonnelManager.Instance?.GetDevelopmentQuality(team.TeamId) ?? 0.5f);

            // Practice injuries are real
            foreach (var injury in results.Injuries ?? new List<PracticeInjury>())
            {
                var player = _db?.GetPlayer(injury.PlayerId);
                if (player == null || player.IsInjured) continue;
                _injuries?.ApplyInjury(player, injury.InjuryType, injury.Severity);
                InboxService.Instance?.Publish(InboxMessageType.Injury, "Training Staff",
                    $"{player.FullName} injured in practice",
                    injury.Description ?? "Picked up a knock during the session.",
                    highPriority: true, deepLinkPanelId: "Roster", deepLinkPayload: player.PlayerId);
            }

            int gainers = results.PlayerGains?.Count(g => g.Value.SkillProgress?.Count > 0) ?? 0;
            LastPracticeSummary =
                $"{date:MMM d}: {session.PrimaryFocus} practice ({session.Intensity}) — " +
                $"quality {QualityWord(results.OverallQuality)}, {gainers} players progressed" +
                (results.Injuries?.Count > 0 ? $", {results.Injuries.Count} injured" : "") + ".";

            return results.OverallQuality;
        }

        private void RunMentorshipSessions(Team team, int practiceQuality)
        {
            var mm = MentorshipManager.Instance;
            if (mm == null) return;

            var roster = team.RosterPlayerIds
                .Select(id => _db?.GetPlayer(id))
                .Where(p => p != null)
                .ToList();

            var sessions = mm.ProcessPracticeMentorshipSessions(team.TeamId, roster, practiceQuality);
            foreach (var session in sessions ?? new List<MentorshipSessionResult>())
            {
                var mentee = _db?.GetPlayer(session.MenteeId);
                if (mentee == null) continue;
                _dev?.ProcessMentorshipPracticeGains(mentee,
                    new PlayerPracticeGains { PlayerId = session.MenteeId }, session);
            }
        }

        private void RunTendencyTraining(Team team)
        {
            var tcm = TendencyCoachingManager.Instance;
            var inTraining = team.RosterPlayerIds
                .Where(id => _db?.GetPlayer(id)?.Tendencies?.CurrentTrainingFocus?.Count > 0)
                .ToList();
            if (inTraining.Count == 0) { LastTrainingResults = new List<TrainingResult>(); return; }

            float intensity = WeeklyIntensity switch
            {
                PracticeIntensity.VeryLight => TendencyCoachingManager.INTENSITY_LIGHT,
                PracticeIntensity.Light => TendencyCoachingManager.INTENSITY_LIGHT,
                PracticeIntensity.High => TendencyCoachingManager.INTENSITY_HEAVY,
                PracticeIntensity.Intense => TendencyCoachingManager.INTENSITY_INTENSE,
                _ => TendencyCoachingManager.INTENSITY_NORMAL
            };

            LastTrainingResults = tcm.ProcessWeeklyTraining(inTraining, team.HeadCoachId, intensity)
                ?? new List<TrainingResult>();
        }

        private static string QualityWord(int quality)
        {
            if (quality >= 80) return "excellent";
            if (quality >= 65) return "good";
            if (quality >= 45) return "solid";
            if (quality >= 30) return "flat";
            return "poor";
        }

        // ==================== SAVE ====================

        public void WriteSave(SaveData data)
        {
            var save = new DevelopmentSaveData
            {
                WeeklyFocus = (int)WeeklyFocus,
                WeeklyIntensity = (int)WeeklyIntensity,
                LastPracticeSummary = LastPracticeSummary
            };

            foreach (var rel in MentorshipManager.Instance?.ActiveRelationships
                     ?? new List<MentorshipRelationship>())
            {
                if (rel == null || !rel.IsActive) continue;
                save.Pairings.Add(new MentorshipPairingRecord
                {
                    MentorId = rel.MentorPlayerId,
                    MenteeId = rel.MenteePlayerId,
                    TeamId = rel.TeamId,
                    Focus = (int)rel.PrimaryFocus,
                    Strength = rel.RelationshipStrength,
                    Compatibility = rel.CompatibilityScore,
                    Sessions = rel.SessionsCompleted,
                    WasAssigned = rel.WasAssigned
                });
            }

            // Tendency training state lives on Player.Tendencies, which the core
            // player save does not carry — persist the coachable block for anyone
            // who has ever been coached (focus set or progress banked).
            foreach (var p in _db?.GetAllPlayers() ?? new List<Player>())
            {
                var t = p?.Tendencies;
                if (t == null) continue;
                bool coached = (t.CurrentTrainingFocus?.Count ?? 0) > 0 ||
                               (t.TrainingProgress?.Count ?? 0) > 0;
                if (!coached) continue;

                save.TendencyStates.Add(new TendencyTrainingRecord
                {
                    PlayerId = p.PlayerId,
                    Focuses = new List<string>(t.CurrentTrainingFocus ?? new List<string>()),
                    ScreenSetting = t.ScreenSetting,
                    HelpDefenseAggression = t.HelpDefenseAggression,
                    TransitionDefense = t.TransitionDefense,
                    PostDefenseAggression = t.PostDefenseAggression,
                    CloseoutControl = t.CloseoutControl,
                    OffBallMovement = t.OffBallMovement,
                    BoxOutDiscipline = t.BoxOutDiscipline,
                    PnRCoverageExecution = t.PnRCoverageExecution,
                    DefensiveCommunication = t.DefensiveCommunication,
                    ShotDiscipline = t.ShotDiscipline
                });
            }

            data.DevelopmentData = save;
        }

        public void ReadSave(SaveData data, in SaveReadContext ctx)
        {
            var save = data.DevelopmentData;
            if (save == null) return; // legacy save — defaults stand

            WeeklyFocus = (PracticeFocus)save.WeeklyFocus;
            WeeklyIntensity = (PracticeIntensity)save.WeeklyIntensity;
            LastPracticeSummary = save.LastPracticeSummary ?? "";

            var mm = MentorshipManager.Instance;
            if (mm != null && save.Pairings != null)
            {
                mm.ActiveRelationships.Clear();
                foreach (var record in save.Pairings)
                {
                    var mentor = _db?.GetPlayer(record.MentorId);
                    var mentee = _db?.GetPlayer(record.MenteeId);
                    if (mentor == null || mentee == null) continue;

                    var (ok, _, rel) = mm.AssignMentor(mentor, mentee, record.TeamId,
                        (MentorFocusArea)record.Focus);
                    if (!ok || rel == null) continue;
                    rel.RelationshipStrength = record.Strength;
                    rel.CompatibilityScore = record.Compatibility;
                    rel.SessionsCompleted = record.Sessions;
                    rel.WasAssigned = record.WasAssigned;
                }
            }

            foreach (var record in save.TendencyStates ?? new List<TendencyTrainingRecord>())
            {
                var t = _db?.GetPlayer(record.PlayerId)?.Tendencies;
                if (t == null) continue;
                t.CurrentTrainingFocus = new List<string>(record.Focuses ?? new List<string>());
                t.ScreenSetting = record.ScreenSetting;
                t.HelpDefenseAggression = record.HelpDefenseAggression;
                t.TransitionDefense = record.TransitionDefense;
                t.PostDefenseAggression = record.PostDefenseAggression;
                t.CloseoutControl = record.CloseoutControl;
                t.OffBallMovement = record.OffBallMovement;
                t.BoxOutDiscipline = record.BoxOutDiscipline;
                t.PnRCoverageExecution = record.PnRCoverageExecution;
                t.DefensiveCommunication = record.DefensiveCommunication;
                t.ShotDiscipline = record.ShotDiscipline;
            }
        }
    }

    // ==================== SAVE SHAPES ====================

    [Serializable]
    public class DevelopmentSaveData
    {
        public int WeeklyFocus;
        public int WeeklyIntensity = 2; // PracticeIntensity.Normal
        public string LastPracticeSummary;
        public List<MentorshipPairingRecord> Pairings = new List<MentorshipPairingRecord>();
        public List<TendencyTrainingRecord> TendencyStates = new List<TendencyTrainingRecord>();
    }

    [Serializable]
    public class MentorshipPairingRecord
    {
        public string MentorId;
        public string MenteeId;
        public string TeamId;
        public int Focus;
        public int Strength;
        public int Compatibility;
        public int Sessions;
        public bool WasAssigned;
    }

    [Serializable]
    public class TendencyTrainingRecord
    {
        public string PlayerId;
        public List<string> Focuses = new List<string>();
        public int ScreenSetting, HelpDefenseAggression, TransitionDefense, PostDefenseAggression,
                   CloseoutControl, OffBallMovement, BoxOutDiscipline, PnRCoverageExecution,
                   DefensiveCommunication, ShotDiscipline;
    }
}
