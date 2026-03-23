using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Simulates a single Warriors vs Lakers game and prints a detailed box score
    /// to verify that tactics produce realistic stat lines.
    /// Attach to any GameObject and it runs on Start.
    /// </summary>
    public class TacticsBoxScoreTest : MonoBehaviour
    {
        [SerializeField] private bool _runOnStart = true;

        private void Start()
        {
            if (_runOnStart)
                RunTest();
        }

        [ContextMenu("Run Warriors vs Lakers Box Score Test")]
        public void RunTest()
        {
            // Get real NBA players
            var allPlayers = NBAPlayerData.GetAllPlayers();
            var gswPlayers = allPlayers.Where(p => p.TeamId == "GSW").ToList();
            var lalPlayers = allPlayers.Where(p => p.TeamId == "LAL").ToList();

            if (gswPlayers.Count < 5 || lalPlayers.Count < 5)
            {
                Debug.LogError($"Not enough players! GSW: {gswPlayers.Count}, LAL: {lalPlayers.Count}");
                return;
            }

            // Create Player objects (full attribute mapping)
            var gswRoster = gswPlayers.OrderByDescending(p => p.OverallRating).Select(ConvertToPlayer).ToList();
            var lalRoster = lalPlayers.OrderByDescending(p => p.OverallRating).Select(ConvertToPlayer).ToList();

            // Create teams with strategy templates
            var homeTeam = new Team
            {
                TeamId = "GSW",
                City = "Golden State",
                Nickname = "Warriors",
                RosterPlayerIds = gswRoster.Select(p => p.PlayerId).ToList(),
                OffensiveStrategy = TeamStrategy.CreateFromTemplate("GSW", StrategyTemplate.WarriorsMotion),
                DefensiveStrategy = TeamStrategy.CreateFromTemplate("GSW", StrategyTemplate.WarriorsMotion)
            };
            for (int i = 0; i < 5 && i < gswRoster.Count; i++)
                homeTeam.StartingLineupIds[i] = gswRoster[i].PlayerId;

            var awayTeam = new Team
            {
                TeamId = "LAL",
                City = "Los Angeles",
                Nickname = "Lakers",
                RosterPlayerIds = lalRoster.Select(p => p.PlayerId).ToList(),
                OffensiveStrategy = TeamStrategy.CreateDefault("LAL"),
                DefensiveStrategy = TeamStrategy.CreateDefault("LAL")
            };
            for (int i = 0; i < 5 && i < lalRoster.Count; i++)
                awayTeam.StartingLineupIds[i] = lalRoster[i].PlayerId;

            // Create player database
            var playerDb = new PlayerDatabase();
            foreach (var p in gswRoster.Concat(lalRoster))
                playerDb.AddPlayer(p);

            // Print strategy comparison
            Debug.Log("\n" + new string('=', 70));
            Debug.Log("  WARRIORS (Home) vs LAKERS (Away) — Tactics Box Score Test");
            Debug.Log(new string('=', 70));

            Debug.Log("\n--- Warriors Strategy: Motion Heavy ---");
            var gswOff = homeTeam.OffensiveStrategy;
            Debug.Log($"  Pace: {gswOff.TargetPace}  |  3PT Freq: {gswOff.ThreePointFrequency}%  |  Mid: {gswOff.MidRangeFrequency}%  |  Rim: {gswOff.RimAttackFrequency}%");
            Debug.Log($"  PnR: {gswOff.PickAndRollFrequency}%  |  Iso: {gswOff.OffensiveSystem.IsolationFrequency}%  |  BallMovement: {gswOff.OffensiveSystem.BallMovementPriority}");
            Debug.Log($"  Spacing: {gswOff.OffensiveSystem.SpacingWidth}  |  Transition: {gswOff.TransitionOffense}");
            Debug.Log($"  OREB Focus: {gswOff.OffensiveReboundingFocus}  |  Senders: {gswOff.SendersOnOffensiveGlass}");

            Debug.Log("\n--- Lakers Strategy: Balanced Default ---");
            var lalOff = awayTeam.OffensiveStrategy;
            Debug.Log($"  Pace: {lalOff.TargetPace}  |  3PT Freq: {lalOff.ThreePointFrequency}%  |  Mid: {lalOff.MidRangeFrequency}%  |  Rim: {lalOff.RimAttackFrequency}%");
            Debug.Log($"  PnR: {lalOff.PickAndRollFrequency}%  |  Iso: {lalOff.OffensiveSystem.IsolationFrequency}%  |  BallMovement: {lalOff.OffensiveSystem.BallMovementPriority}");
            Debug.Log($"  Spacing: {lalOff.OffensiveSystem.SpacingWidth}  |  Transition: {lalOff.TransitionOffense}");

            // Print starting lineups
            Debug.Log("\n--- GSW Starting 5 ---");
            for (int i = 0; i < 5; i++)
            {
                var p = playerDb.GetPlayer(homeTeam.StartingLineupIds[i]);
                Debug.Log($"  {PositionLabel(i)}: {p.FirstName} {p.LastName} (3PT:{p.Shot_Three} MID:{p.Shot_MidRange} RIM:{p.Finishing_Rim} DEF:{p.Defense_Perimeter} REB:{p.DefensiveRebound})");
            }
            Debug.Log("\n--- LAL Starting 5 ---");
            for (int i = 0; i < 5; i++)
            {
                var p = playerDb.GetPlayer(awayTeam.StartingLineupIds[i]);
                Debug.Log($"  {PositionLabel(i)}: {p.FirstName} {p.LastName} (3PT:{p.Shot_Three} MID:{p.Shot_MidRange} RIM:{p.Finishing_Rim} DEF:{p.Defense_Perimeter} REB:{p.DefensiveRebound})");
            }

            // Simulate game
            Debug.Log("\n--- Simulating Game... ---\n");
            var simulator = new GameSimulator(playerDb, seed: 42);
            var result = simulator.SimulateGame(homeTeam, awayTeam);

            // Print final score
            Debug.Log(new string('=', 70));
            Debug.Log($"  FINAL SCORE: Warriors {result.HomeScore} — Lakers {result.AwayScore}" +
                      (result.WentToOvertime ? $" (OT x{result.Quarters - 4})" : ""));
            Debug.Log(new string('=', 70));

            // Print detailed box scores
            PrintTeamBoxScore("GOLDEN STATE WARRIORS", homeTeam, result.BoxScore, playerDb);
            PrintTeamBoxScore("LOS ANGELES LAKERS", awayTeam, result.BoxScore, playerDb);

            // Print team totals comparison
            PrintTeamTotals(result.BoxScore, homeTeam, awayTeam, playerDb);

            // Realism checks
            PrintRealismChecks(result);
        }

        private void PrintTeamBoxScore(string teamName, Team team, BoxScore boxScore, PlayerDatabase db)
        {
            Debug.Log($"\n--- {teamName} ---");
            Debug.Log($"{"PLAYER",-20} {"MIN",4} {"PTS",4} {"FG",7} {"3PT",7} {"FT",6} {"OREB",5} {"DREB",5} {"REB",4} {"AST",4} {"STL",4} {"BLK",4} {"TO",3} {"PF",3}");
            Debug.Log(new string('-', 95));

            var starters = team.StartingLineupIds.ToList();
            var allStats = boxScore.PlayerStats
                .Where(kvp => team.RosterPlayerIds.Contains(kvp.Key))
                .Select(kvp => kvp.Value)
                .Where(s => s.Minutes > 0 || s.Points > 0 || s.TotalFGA > 0 || s.TotalRebounds > 0)
                .OrderByDescending(s => s.Minutes)
                .ThenByDescending(s => s.Points)
                .ToList();

            int teamPts = 0, teamFGA = 0, teamFGM = 0, team3PA = 0, team3PM = 0;
            int teamFTA = 0, teamFTM = 0, teamOREB = 0, teamDREB = 0;
            int teamAST = 0, teamSTL = 0, teamBLK = 0, teamTO = 0, teamPF = 0;
            int teamMIN = 0;

            foreach (var s in allStats)
            {
                var player = db.GetPlayer(s.PlayerId);
                string name = player != null ? $"{player.FirstName[0]}. {player.LastName}" : s.PlayerId;
                if (name.Length > 19) name = name.Substring(0, 19);

                string fg = $"{s.TotalFGM}/{s.TotalFGA}";
                string three = $"{s.ThreePointMade}/{s.ThreePointAttempts}";
                string ft = $"{s.FreeThrowsMade}/{s.FreeThrowAttempts}";

                Debug.Log($"{name,-20} {s.Minutes,4} {s.Points,4} {fg,7} {three,7} {ft,6} {s.OffensiveRebounds,5} {s.DefensiveRebounds,5} {s.TotalRebounds,4} {s.Assists,4} {s.Steals,4} {s.Blocks,4} {s.Turnovers,3} {s.PersonalFouls,3}");
                teamMIN += s.Minutes;

                teamPts += s.Points; teamFGA += s.TotalFGA; teamFGM += s.TotalFGM;
                team3PA += s.ThreePointAttempts; team3PM += s.ThreePointMade;
                teamFTA += s.FreeThrowAttempts; teamFTM += s.FreeThrowsMade;
                teamOREB += s.OffensiveRebounds; teamDREB += s.DefensiveRebounds;
                teamAST += s.Assists; teamSTL += s.Steals; teamBLK += s.Blocks;
                teamTO += s.Turnovers; teamPF += s.PersonalFouls;
            }

            Debug.Log(new string('-', 95));
            float fgPct = teamFGA > 0 ? (float)teamFGM / teamFGA * 100 : 0;
            float threePct = team3PA > 0 ? (float)team3PM / team3PA * 100 : 0;
            float ftPct = teamFTA > 0 ? (float)teamFTM / teamFTA * 100 : 0;
            Debug.Log($"{"TOTALS",-20} {teamMIN,4} {teamPts,4} {teamFGM + "/" + teamFGA,7} {team3PM + "/" + team3PA,7} {teamFTM + "/" + teamFTA,6} {teamOREB,5} {teamDREB,5} {teamOREB + teamDREB,4} {teamAST,4} {teamSTL,4} {teamBLK,4} {teamTO,3} {teamPF,3}");
            Debug.Log($"{"SHOOTING",-20}      FG: {fgPct:F1}%  3PT: {threePct:F1}%  FT: {ftPct:F1}%");
        }

        private void PrintTeamTotals(BoxScore box, Team home, Team away, PlayerDatabase db)
        {
            Debug.Log($"\n{'=',-70}");
            Debug.Log("  TEAM COMPARISON");
            Debug.Log($"{'=',-70}");

            var homeStats = AggregateTeamStats(box, home);
            var awayStats = AggregateTeamStats(box, away);

            Debug.Log($"{"Stat",-20} {"Warriors",10} {"Lakers",10}");
            Debug.Log(new string('-', 42));
            Debug.Log($"{"Points",-20} {homeStats.pts,10} {awayStats.pts,10}");
            Debug.Log($"{"FG%",-20} {homeStats.fgPct,9:F1}% {awayStats.fgPct,9:F1}%");
            Debug.Log($"{"3PT Made/Att",-20} {homeStats.tpm + "/" + homeStats.tpa,10} {awayStats.tpm + "/" + awayStats.tpa,10}");
            Debug.Log($"{"3PT%",-20} {homeStats.tpPct,9:F1}% {awayStats.tpPct,9:F1}%");
            Debug.Log($"{"FT Made/Att",-20} {homeStats.ftm + "/" + homeStats.fta,10} {awayStats.ftm + "/" + awayStats.fta,10}");
            Debug.Log($"{"Off Rebounds",-20} {homeStats.oreb,10} {awayStats.oreb,10}");
            Debug.Log($"{"Def Rebounds",-20} {homeStats.dreb,10} {awayStats.dreb,10}");
            Debug.Log($"{"Total Rebounds",-20} {homeStats.oreb + homeStats.dreb,10} {awayStats.oreb + awayStats.dreb,10}");
            Debug.Log($"{"Assists",-20} {homeStats.ast,10} {awayStats.ast,10}");
            Debug.Log($"{"Steals",-20} {homeStats.stl,10} {awayStats.stl,10}");
            Debug.Log($"{"Blocks",-20} {homeStats.blk,10} {awayStats.blk,10}");
            Debug.Log($"{"Turnovers",-20} {homeStats.to,10} {awayStats.to,10}");
            Debug.Log($"{"Fouls",-20} {homeStats.pf,10} {awayStats.pf,10}");
        }

        private (int pts, int fgm, int fga, float fgPct, int tpm, int tpa, float tpPct, int ftm, int fta, int oreb, int dreb, int ast, int stl, int blk, int to, int pf)
            AggregateTeamStats(BoxScore box, Team team)
        {
            int pts=0, fgm=0, fga=0, tpm=0, tpa=0, ftm=0, fta=0, oreb=0, dreb=0, ast=0, stl=0, blk=0, to=0, pf=0;
            foreach (var id in team.RosterPlayerIds)
            {
                if (!box.PlayerStats.TryGetValue(id, out var s)) continue;
                pts += s.Points; fgm += s.TotalFGM; fga += s.TotalFGA;
                tpm += s.ThreePointMade; tpa += s.ThreePointAttempts;
                ftm += s.FreeThrowsMade; fta += s.FreeThrowAttempts;
                oreb += s.OffensiveRebounds; dreb += s.DefensiveRebounds;
                ast += s.Assists; stl += s.Steals; blk += s.Blocks;
                to += s.Turnovers; pf += s.PersonalFouls;
            }
            float fgPct = fga > 0 ? (float)fgm/fga*100 : 0;
            float tpPct = tpa > 0 ? (float)tpm/tpa*100 : 0;
            return (pts, fgm, fga, fgPct, tpm, tpa, tpPct, ftm, fta, oreb, dreb, ast, stl, blk, to, pf);
        }

        private void PrintRealismChecks(NBAHeadCoach.Core.Simulation.GameResult result)
        {
            Debug.Log($"\n{'=',-70}");
            Debug.Log("  REALISM CHECKS");
            Debug.Log($"{'=',-70}");

            int homeScore = result.HomeScore;
            int awayScore = result.AwayScore;
            var box = result.BoxScore;

            // Score range
            bool scoreOk = homeScore >= 85 && homeScore <= 140 && awayScore >= 85 && awayScore <= 140;
            Debug.Log($"  Score range (85-140):     {(scoreOk ? "PASS" : "FAIL")} — {homeScore}, {awayScore}");

            // Assists
            int homeAst = box.PlayerStats.Where(kvp => kvp.Value.Assists > 0).Sum(kvp => kvp.Value.Assists);
            // We need team-specific assists
            var homeIds = new HashSet<string>(box.HomeTeamId == "GSW" ?
                box.PlayerStats.Keys.Where(k => box.PlayerStats[k].Assists >= 0) : new string[0]);
            // Simpler: just check total assists across all players
            int totalAst = box.PlayerStats.Values.Sum(s => s.Assists);
            bool astOk = totalAst >= 30; // Combined ~15+ per team
            Debug.Log($"  Assists > 0 (total):      {(astOk ? "PASS" : "FAIL")} — {totalAst} total assists");

            // Rebounds
            int totalOreb = box.PlayerStats.Values.Sum(s => s.OffensiveRebounds);
            int totalDreb = box.PlayerStats.Values.Sum(s => s.DefensiveRebounds);
            bool rebOk = totalDreb > 0 && totalOreb > 0;
            Debug.Log($"  Rebounds (OREB+DREB > 0): {(rebOk ? "PASS" : "FAIL")} — {totalOreb} OREB, {totalDreb} DREB");

            // Steals
            int totalStl = box.PlayerStats.Values.Sum(s => s.Steals);
            bool stlOk = totalStl >= 5;
            Debug.Log($"  Steals >= 5:              {(stlOk ? "PASS" : "FAIL")} — {totalStl} steals");

            // Blocks
            int totalBlk = box.PlayerStats.Values.Sum(s => s.Blocks);
            Debug.Log($"  Blocks:                   INFO  — {totalBlk} blocks");

            // Turnovers
            int totalTo = box.PlayerStats.Values.Sum(s => s.Turnovers);
            bool toOk = totalTo >= 15 && totalTo <= 45;
            Debug.Log($"  Turnovers (15-45):        {(toOk ? "PASS" : "FAIL")} — {totalTo} turnovers");

            // 3PT attempts (Warriors should be high)
            int gsw3PA = 0, lal3PA = 0;
            foreach (var kvp in box.PlayerStats)
            {
                // We can't easily distinguish teams from box score alone,
                // but the formatted output above shows it
                gsw3PA += kvp.Value.ThreePointAttempts;
            }
            Debug.Log($"  Total 3PA:                INFO  — {gsw3PA} combined 3-point attempts");

            // Fouls
            int totalFouls = box.PlayerStats.Values.Sum(s => s.PersonalFouls);
            bool foulOk = totalFouls >= 20 && totalFouls <= 60;
            Debug.Log($"  Fouls (20-60):            {(foulOk ? "PASS" : "FAIL")} — {totalFouls} fouls");

            Debug.Log("");
        }

        private Player ConvertToPlayer(PlayerData data)
        {
            return new Player
            {
                PlayerId = data.PlayerId,
                FirstName = data.FirstName,
                LastName = data.LastName,
                JerseyNumber = data.JerseyNumber,
                Position = (Position)data.Position,
                BirthDate = System.DateTime.Now.AddYears(-data.Age),
                HeightInches = data.HeightInches,
                WeightLbs = data.WeightLbs,
                TeamId = data.TeamId,

                // Offense
                Finishing_Rim = data.Finishing_Rim,
                Finishing_PostMoves = data.Finishing_PostMoves,
                Shot_Close = data.Shot_Close,
                Shot_MidRange = data.Shot_MidRange,
                Shot_Three = data.Shot_Three,
                FreeThrow = data.FreeThrow,

                // Playmaking
                Passing = data.Passing,
                BallHandling = data.BallHandling,
                OffensiveIQ = data.OffensiveIQ,
                SpeedWithBall = data.SpeedWithBall,

                // Defense
                Defense_Perimeter = data.Defense_Perimeter,
                Defense_Interior = data.Defense_Interior,
                Defense_PostDefense = data.Defense_PostDefense,
                Steal = data.Steal,
                Block = data.Block,
                DefensiveIQ = data.DefensiveIQ,
                DefensiveRebound = data.DefensiveRebound,

                // Physical
                Speed = data.Speed,
                Acceleration = data.Acceleration,
                Strength = data.Strength,
                Vertical = data.Vertical,
                Stamina = data.Stamina,

                // Mental
                BasketballIQ = data.BasketballIQ,
                Clutch = data.Clutch,
                Leadership = data.Leadership,
                Composure = data.Composure,
                Aggression = data.Aggression,

                // Dynamic state
                Energy = 100,
                Morale = 75,
                Form = 70
            };
        }

        private string PositionLabel(int i) => i switch
        {
            0 => "PG",
            1 => "SG",
            2 => "SF",
            3 => "PF",
            4 => "C",
            _ => "??"
        };
    }
}
