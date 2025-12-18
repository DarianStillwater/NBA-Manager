using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Current phase of the playoff tournament
    /// </summary>
    [Serializable]
    public enum PlayoffPhase
    {
        NotStarted,
        PlayIn,
        FirstRound,
        ConferenceSemis,
        ConferenceFinals,
        Finals,
        Complete
    }

    /// <summary>
    /// Playoff round identifiers
    /// </summary>
    [Serializable]
    public enum PlayoffRound
    {
        PlayIn = 0,
        FirstRound = 1,
        ConferenceSemis = 2,
        ConferenceFinals = 3,
        Finals = 4
    }

    /// <summary>
    /// NBA Play-In Tournament for both conferences
    /// </summary>
    [Serializable]
    public class PlayInTournament
    {
        public int Season;
        public PlayInBracket East;
        public PlayInBracket West;
        public bool IsComplete => East?.IsComplete == true && West?.IsComplete == true;

        public PlayInTournament(int season)
        {
            Season = season;
            East = new PlayInBracket("Eastern");
            West = new PlayInBracket("Western");
        }

        /// <summary>
        /// Initialize the play-in tournament with standings
        /// </summary>
        public void Initialize(List<TeamStandings> eastStandings, List<TeamStandings> westStandings)
        {
            East.Initialize(eastStandings);
            West.Initialize(westStandings);
        }

        /// <summary>
        /// Get all play-in games that need to be played
        /// </summary>
        public List<PlayInGame> GetPendingGames()
        {
            var games = new List<PlayInGame>();
            games.AddRange(East.GetPendingGames());
            games.AddRange(West.GetPendingGames());
            return games;
        }

        /// <summary>
        /// Get the next play-in game to be played
        /// </summary>
        public PlayInGame GetNextGame()
        {
            return East.GetNextGame() ?? West.GetNextGame();
        }
    }

    /// <summary>
    /// Play-In bracket for a single conference
    /// Format:
    /// Game 1: #7 vs #8 → Winner = 7 seed, Loser → Game 3
    /// Game 2: #9 vs #10 → Winner → Game 3, Loser = eliminated
    /// Game 3: Loser G1 vs Winner G2 → Winner = 8 seed, Loser = eliminated
    /// </summary>
    [Serializable]
    public class PlayInBracket
    {
        public string Conference;

        // Teams in seeds 7-10
        public string Seed7TeamId;
        public string Seed8TeamId;
        public string Seed9TeamId;
        public string Seed10TeamId;

        // Play-In Games
        public PlayInGame SevenVsEight;     // Game 1: Winner becomes 7 seed
        public PlayInGame NineVsTen;         // Game 2: Winner advances to Game 3
        public PlayInGame EightSeedGame;     // Game 3: Winner becomes 8 seed

        // Final results
        public string SevenSeedTeamId;       // Final 7 seed for playoffs
        public string EightSeedTeamId;       // Final 8 seed for playoffs

        public bool IsComplete => !string.IsNullOrEmpty(SevenSeedTeamId) && !string.IsNullOrEmpty(EightSeedTeamId);

        public PlayInBracket(string conference)
        {
            Conference = conference;
        }

        /// <summary>
        /// Initialize with top 10 teams from conference standings
        /// </summary>
        public void Initialize(List<TeamStandings> standings)
        {
            if (standings == null || standings.Count < 10)
            {
                Debug.LogError($"[PlayInBracket] Not enough teams for {Conference} conference");
                return;
            }

            // Seeds 7-10 go to play-in
            Seed7TeamId = standings[6].TeamId;
            Seed8TeamId = standings[7].TeamId;
            Seed9TeamId = standings[8].TeamId;
            Seed10TeamId = standings[9].TeamId;

            // Create play-in games
            SevenVsEight = new PlayInGame
            {
                GameId = $"PLAYIN_{Conference}_78",
                HigherSeed = 7,
                LowerSeed = 8,
                HigherSeedTeamId = Seed7TeamId,
                LowerSeedTeamId = Seed8TeamId,
                Conference = Conference,
                GameType = PlayInGameType.SevenVsEight
            };

            NineVsTen = new PlayInGame
            {
                GameId = $"PLAYIN_{Conference}_910",
                HigherSeed = 9,
                LowerSeed = 10,
                HigherSeedTeamId = Seed9TeamId,
                LowerSeedTeamId = Seed10TeamId,
                Conference = Conference,
                GameType = PlayInGameType.NineVsTen
            };

            // Game 3 teams are determined after Games 1 and 2
            EightSeedGame = new PlayInGame
            {
                GameId = $"PLAYIN_{Conference}_8SEED",
                Conference = Conference,
                GameType = PlayInGameType.EightSeedDecider
            };
        }

        /// <summary>
        /// Record result of a play-in game
        /// </summary>
        public void RecordGameResult(PlayInGame game, int higherSeedScore, int lowerSeedScore)
        {
            game.HigherSeedScore = higherSeedScore;
            game.LowerSeedScore = lowerSeedScore;
            game.IsComplete = true;

            bool higherSeedWon = higherSeedScore > lowerSeedScore;
            game.WinnerTeamId = higherSeedWon ? game.HigherSeedTeamId : game.LowerSeedTeamId;
            game.LoserTeamId = higherSeedWon ? game.LowerSeedTeamId : game.HigherSeedTeamId;

            // Process result based on game type
            switch (game.GameType)
            {
                case PlayInGameType.SevenVsEight:
                    // Winner becomes 7 seed
                    SevenSeedTeamId = game.WinnerTeamId;
                    // Loser goes to Game 3
                    EightSeedGame.HigherSeedTeamId = game.LoserTeamId;
                    EightSeedGame.HigherSeed = higherSeedWon ? 8 : 7;
                    break;

                case PlayInGameType.NineVsTen:
                    // Winner goes to Game 3
                    EightSeedGame.LowerSeedTeamId = game.WinnerTeamId;
                    EightSeedGame.LowerSeed = higherSeedWon ? 9 : 10;
                    // Loser is eliminated (nothing to do)
                    break;

                case PlayInGameType.EightSeedDecider:
                    // Winner becomes 8 seed
                    EightSeedTeamId = game.WinnerTeamId;
                    // Loser is eliminated (nothing to do)
                    break;
            }
        }

        /// <summary>
        /// Get pending games that can be played
        /// </summary>
        public List<PlayInGame> GetPendingGames()
        {
            var games = new List<PlayInGame>();

            // Games 1 and 2 can be played simultaneously
            if (SevenVsEight != null && !SevenVsEight.IsComplete)
                games.Add(SevenVsEight);
            if (NineVsTen != null && !NineVsTen.IsComplete)
                games.Add(NineVsTen);

            // Game 3 can only be played after Games 1 and 2
            if (EightSeedGame != null && !EightSeedGame.IsComplete &&
                SevenVsEight?.IsComplete == true && NineVsTen?.IsComplete == true)
            {
                games.Add(EightSeedGame);
            }

            return games;
        }

        /// <summary>
        /// Get the next game to play
        /// </summary>
        public PlayInGame GetNextGame()
        {
            if (SevenVsEight != null && !SevenVsEight.IsComplete)
                return SevenVsEight;
            if (NineVsTen != null && !NineVsTen.IsComplete)
                return NineVsTen;
            if (EightSeedGame != null && !EightSeedGame.IsComplete &&
                SevenVsEight?.IsComplete == true && NineVsTen?.IsComplete == true)
                return EightSeedGame;
            return null;
        }
    }

    /// <summary>
    /// Type of play-in game
    /// </summary>
    [Serializable]
    public enum PlayInGameType
    {
        SevenVsEight,      // Winner = 7 seed, Loser to Game 3
        NineVsTen,         // Winner to Game 3, Loser eliminated
        EightSeedDecider   // Winner = 8 seed, Loser eliminated
    }

    /// <summary>
    /// A single play-in tournament game
    /// </summary>
    [Serializable]
    public class PlayInGame
    {
        public string GameId;
        public string Conference;
        public PlayInGameType GameType;

        public int HigherSeed;
        public int LowerSeed;
        public string HigherSeedTeamId;
        public string LowerSeedTeamId;

        public int HigherSeedScore;
        public int LowerSeedScore;

        public bool IsComplete;
        public string WinnerTeamId;
        public string LoserTeamId;

        public DateTime Date;

        /// <summary>
        /// Get home team (higher seed hosts)
        /// </summary>
        public string HomeTeamId => HigherSeedTeamId;
        public string AwayTeamId => LowerSeedTeamId;
    }

    /// <summary>
    /// Complete playoff bracket for both conferences
    /// </summary>
    [Serializable]
    public class PlayoffBracket
    {
        public int Season;
        public PlayoffPhase CurrentPhase = PlayoffPhase.NotStarted;

        // Play-In Tournament
        public PlayInTournament PlayIn;

        // Conference brackets
        public ConferenceBracket Eastern;
        public ConferenceBracket Western;

        // NBA Finals
        public PlayoffSeries Finals;
        public string ChampionTeamId;
        public string FinalsRunnerUpTeamId;
        public string FinalsMVPPlayerId;

        public bool IsComplete => CurrentPhase == PlayoffPhase.Complete;

        public PlayoffBracket(int season)
        {
            Season = season;
            PlayIn = new PlayInTournament(season);
            Eastern = new ConferenceBracket("Eastern");
            Western = new ConferenceBracket("Western");
        }

        /// <summary>
        /// Get all active series that have games remaining
        /// </summary>
        public List<PlayoffSeries> GetActiveSeries()
        {
            var active = new List<PlayoffSeries>();
            active.AddRange(Eastern.GetActiveSeries());
            active.AddRange(Western.GetActiveSeries());
            if (Finals != null && !Finals.IsComplete)
                active.Add(Finals);
            return active;
        }

        /// <summary>
        /// Advance to the next round when current round is complete
        /// </summary>
        public void TryAdvanceRound()
        {
            switch (CurrentPhase)
            {
                case PlayoffPhase.PlayIn:
                    if (PlayIn.IsComplete)
                    {
                        // Set up first round with play-in winners
                        Eastern.SetPlayInResults(PlayIn.East.SevenSeedTeamId, PlayIn.East.EightSeedTeamId);
                        Western.SetPlayInResults(PlayIn.West.SevenSeedTeamId, PlayIn.West.EightSeedTeamId);
                        CurrentPhase = PlayoffPhase.FirstRound;
                    }
                    break;

                case PlayoffPhase.FirstRound:
                    if (Eastern.IsRoundComplete(PlayoffRound.FirstRound) &&
                        Western.IsRoundComplete(PlayoffRound.FirstRound))
                    {
                        Eastern.SetupConferenceSemis();
                        Western.SetupConferenceSemis();
                        CurrentPhase = PlayoffPhase.ConferenceSemis;
                    }
                    break;

                case PlayoffPhase.ConferenceSemis:
                    if (Eastern.IsRoundComplete(PlayoffRound.ConferenceSemis) &&
                        Western.IsRoundComplete(PlayoffRound.ConferenceSemis))
                    {
                        Eastern.SetupConferenceFinals();
                        Western.SetupConferenceFinals();
                        CurrentPhase = PlayoffPhase.ConferenceFinals;
                    }
                    break;

                case PlayoffPhase.ConferenceFinals:
                    if (Eastern.ConferenceFinals?.IsComplete == true &&
                        Western.ConferenceFinals?.IsComplete == true)
                    {
                        SetupFinals();
                        CurrentPhase = PlayoffPhase.Finals;
                    }
                    break;

                case PlayoffPhase.Finals:
                    if (Finals?.IsComplete == true)
                    {
                        ChampionTeamId = Finals.WinnerTeamId;
                        FinalsRunnerUpTeamId = Finals.LoserTeamId;
                        CurrentPhase = PlayoffPhase.Complete;
                    }
                    break;
            }
        }

        private void SetupFinals()
        {
            string eastChamp = Eastern.ConferenceFinals.WinnerTeamId;
            string westChamp = Western.ConferenceFinals.WinnerTeamId;
            int eastWins = Eastern.GetTeamTotalWins(eastChamp);
            int westWins = Western.GetTeamTotalWins(westChamp);

            // Better regular season record gets home court
            bool eastHasHomeCourt = eastWins >= westWins;

            Finals = new PlayoffSeries
            {
                SeriesId = $"FINALS_{Season}",
                Round = PlayoffRound.Finals,
                Conference = "Finals",
                HigherSeedTeamId = eastHasHomeCourt ? eastChamp : westChamp,
                LowerSeedTeamId = eastHasHomeCourt ? westChamp : eastChamp,
                HigherSeed = 1,
                LowerSeed = 1
            };
        }
    }

    /// <summary>
    /// Playoff bracket for a single conference
    /// </summary>
    [Serializable]
    public class ConferenceBracket
    {
        public string Conference;

        // Seeds 1-6 (direct qualifiers) + 7-8 from play-in
        public string[] Seeds = new string[8]; // Index 0-7 = Seeds 1-8

        // First Round (4 series)
        public PlayoffSeries[] FirstRound = new PlayoffSeries[4];

        // Conference Semifinals (2 series)
        public PlayoffSeries[] ConferenceSemis = new PlayoffSeries[2];

        // Conference Finals (1 series)
        public PlayoffSeries ConferenceFinals;

        public string ConferenceChampion => ConferenceFinals?.WinnerTeamId;

        public ConferenceBracket(string conference)
        {
            Conference = conference;
        }

        /// <summary>
        /// Initialize with standings (seeds 1-6)
        /// </summary>
        public void Initialize(List<TeamStandings> standings)
        {
            if (standings == null || standings.Count < 6)
            {
                Debug.LogError($"[ConferenceBracket] Not enough teams for {Conference}");
                return;
            }

            // Set seeds 1-6
            for (int i = 0; i < 6; i++)
            {
                Seeds[i] = standings[i].TeamId;
            }
        }

        /// <summary>
        /// Set play-in tournament results (seeds 7 and 8)
        /// </summary>
        public void SetPlayInResults(string seed7TeamId, string seed8TeamId)
        {
            Seeds[6] = seed7TeamId;
            Seeds[7] = seed8TeamId;
            SetupFirstRound();
        }

        /// <summary>
        /// Create first round matchups
        /// Format: 1v8, 4v5, 3v6, 2v7
        /// </summary>
        private void SetupFirstRound()
        {
            // 1 vs 8
            FirstRound[0] = CreateSeries(1, 8, PlayoffRound.FirstRound);
            // 4 vs 5
            FirstRound[1] = CreateSeries(4, 5, PlayoffRound.FirstRound);
            // 3 vs 6
            FirstRound[2] = CreateSeries(3, 6, PlayoffRound.FirstRound);
            // 2 vs 7
            FirstRound[3] = CreateSeries(2, 7, PlayoffRound.FirstRound);
        }

        /// <summary>
        /// Set up Conference Semifinals matchups
        /// Winners of 1v8 plays winner of 4v5
        /// Winners of 3v6 plays winner of 2v7
        /// </summary>
        public void SetupConferenceSemis()
        {
            string winner1 = FirstRound[0].WinnerTeamId;
            string winner4 = FirstRound[1].WinnerTeamId;
            string winner3 = FirstRound[2].WinnerTeamId;
            string winner2 = FirstRound[3].WinnerTeamId;

            int seed1 = FirstRound[0].WinnerSeed;
            int seed4 = FirstRound[1].WinnerSeed;
            int seed3 = FirstRound[2].WinnerSeed;
            int seed2 = FirstRound[3].WinnerSeed;

            // Bracket 1: Winner of 1v8 vs Winner of 4v5
            ConferenceSemis[0] = CreateSeriesFromTeams(
                seed1 < seed4 ? winner1 : winner4,
                seed1 < seed4 ? winner4 : winner1,
                Math.Min(seed1, seed4),
                Math.Max(seed1, seed4),
                PlayoffRound.ConferenceSemis);

            // Bracket 2: Winner of 3v6 vs Winner of 2v7
            ConferenceSemis[1] = CreateSeriesFromTeams(
                seed2 < seed3 ? winner2 : winner3,
                seed2 < seed3 ? winner3 : winner2,
                Math.Min(seed2, seed3),
                Math.Max(seed2, seed3),
                PlayoffRound.ConferenceSemis);
        }

        /// <summary>
        /// Set up Conference Finals matchup
        /// </summary>
        public void SetupConferenceFinals()
        {
            string winner1 = ConferenceSemis[0].WinnerTeamId;
            string winner2 = ConferenceSemis[1].WinnerTeamId;
            int seed1 = ConferenceSemis[0].WinnerSeed;
            int seed2 = ConferenceSemis[1].WinnerSeed;

            ConferenceFinals = CreateSeriesFromTeams(
                seed1 < seed2 ? winner1 : winner2,
                seed1 < seed2 ? winner2 : winner1,
                Math.Min(seed1, seed2),
                Math.Max(seed1, seed2),
                PlayoffRound.ConferenceFinals);
        }

        private PlayoffSeries CreateSeries(int higherSeed, int lowerSeed, PlayoffRound round)
        {
            return new PlayoffSeries
            {
                SeriesId = $"{Conference}_{round}_{higherSeed}v{lowerSeed}",
                Round = round,
                Conference = Conference,
                HigherSeed = higherSeed,
                LowerSeed = lowerSeed,
                HigherSeedTeamId = Seeds[higherSeed - 1],
                LowerSeedTeamId = Seeds[lowerSeed - 1]
            };
        }

        private PlayoffSeries CreateSeriesFromTeams(string higherTeam, string lowerTeam,
            int higherSeed, int lowerSeed, PlayoffRound round)
        {
            return new PlayoffSeries
            {
                SeriesId = $"{Conference}_{round}_{higherSeed}v{lowerSeed}",
                Round = round,
                Conference = Conference,
                HigherSeed = higherSeed,
                LowerSeed = lowerSeed,
                HigherSeedTeamId = higherTeam,
                LowerSeedTeamId = lowerTeam
            };
        }

        /// <summary>
        /// Check if a round is complete
        /// </summary>
        public bool IsRoundComplete(PlayoffRound round)
        {
            return round switch
            {
                PlayoffRound.FirstRound => FirstRound.All(s => s?.IsComplete == true),
                PlayoffRound.ConferenceSemis => ConferenceSemis.All(s => s?.IsComplete == true),
                PlayoffRound.ConferenceFinals => ConferenceFinals?.IsComplete == true,
                _ => false
            };
        }

        /// <summary>
        /// Get all active series in this conference
        /// </summary>
        public List<PlayoffSeries> GetActiveSeries()
        {
            var active = new List<PlayoffSeries>();

            foreach (var series in FirstRound)
                if (series != null && !series.IsComplete)
                    active.Add(series);

            foreach (var series in ConferenceSemis)
                if (series != null && !series.IsComplete)
                    active.Add(series);

            if (ConferenceFinals != null && !ConferenceFinals.IsComplete)
                active.Add(ConferenceFinals);

            return active;
        }

        /// <summary>
        /// Get total wins for a team in this conference's playoffs
        /// </summary>
        public int GetTeamTotalWins(string teamId)
        {
            int wins = 0;
            foreach (var series in FirstRound)
                if (series != null)
                    wins += series.GetTeamWins(teamId);
            foreach (var series in ConferenceSemis)
                if (series != null)
                    wins += series.GetTeamWins(teamId);
            if (ConferenceFinals != null)
                wins += ConferenceFinals.GetTeamWins(teamId);
            return wins;
        }
    }

    /// <summary>
    /// A playoff series (best of 7)
    /// </summary>
    [Serializable]
    public class PlayoffSeries
    {
        public string SeriesId;
        public PlayoffRound Round;
        public string Conference;

        public int HigherSeed;
        public int LowerSeed;
        public string HigherSeedTeamId;
        public string LowerSeedTeamId;

        public int HigherSeedWins;
        public int LowerSeedWins;

        public List<PlayoffGame> Games = new List<PlayoffGame>();

        public bool IsComplete => HigherSeedWins == 4 || LowerSeedWins == 4;
        public string WinnerTeamId => HigherSeedWins == 4 ? HigherSeedTeamId :
                                      LowerSeedWins == 4 ? LowerSeedTeamId : null;
        public string LoserTeamId => HigherSeedWins == 4 ? LowerSeedTeamId :
                                     LowerSeedWins == 4 ? HigherSeedTeamId : null;
        public int WinnerSeed => HigherSeedWins == 4 ? HigherSeed : LowerSeed;
        public int TotalGamesPlayed => HigherSeedWins + LowerSeedWins;
        public int NextGameNumber => TotalGamesPlayed + 1;

        /// <summary>
        /// Home court pattern: 2-2-1-1-1
        /// Higher seed hosts Games 1, 2, 5, 7
        /// </summary>
        private static readonly int[] HOME_COURT_PATTERN = { 0, 0, 1, 1, 0, 1, 0 };

        /// <summary>
        /// Get home team for a specific game number
        /// </summary>
        public string GetHomeTeamForGame(int gameNumber)
        {
            if (gameNumber < 1 || gameNumber > 7) return HigherSeedTeamId;
            return HOME_COURT_PATTERN[gameNumber - 1] == 0 ? HigherSeedTeamId : LowerSeedTeamId;
        }

        /// <summary>
        /// Record a game result
        /// </summary>
        public void RecordGameResult(int gameNumber, int homeScore, int awayScore)
        {
            var game = Games.FirstOrDefault(g => g.GameNumber == gameNumber);
            if (game == null)
            {
                game = new PlayoffGame
                {
                    GameNumber = gameNumber,
                    HomeTeamId = GetHomeTeamForGame(gameNumber),
                    AwayTeamId = GetHomeTeamForGame(gameNumber) == HigherSeedTeamId
                        ? LowerSeedTeamId : HigherSeedTeamId
                };
                Games.Add(game);
            }

            game.HomeScore = homeScore;
            game.AwayScore = awayScore;
            game.IsComplete = true;

            // Update series wins
            bool homeWon = homeScore > awayScore;
            string winner = homeWon ? game.HomeTeamId : game.AwayTeamId;

            if (winner == HigherSeedTeamId)
                HigherSeedWins++;
            else
                LowerSeedWins++;
        }

        /// <summary>
        /// Create the next game in the series
        /// </summary>
        public PlayoffGame CreateNextGame(DateTime date)
        {
            if (IsComplete) return null;

            int gameNum = NextGameNumber;
            string homeTeam = GetHomeTeamForGame(gameNum);

            var game = new PlayoffGame
            {
                GameNumber = gameNum,
                Date = date,
                HomeTeamId = homeTeam,
                AwayTeamId = homeTeam == HigherSeedTeamId ? LowerSeedTeamId : HigherSeedTeamId,
                Round = Round,
                SeriesId = SeriesId
            };

            Games.Add(game);
            return game;
        }

        /// <summary>
        /// Get wins for a specific team in this series
        /// </summary>
        public int GetTeamWins(string teamId)
        {
            if (teamId == HigherSeedTeamId) return HigherSeedWins;
            if (teamId == LowerSeedTeamId) return LowerSeedWins;
            return 0;
        }

        /// <summary>
        /// Get series status string (e.g., "BOS leads 3-1")
        /// </summary>
        public string GetStatusString(Func<string, string> teamNameLookup)
        {
            string higher = teamNameLookup?.Invoke(HigherSeedTeamId) ?? HigherSeedTeamId;
            string lower = teamNameLookup?.Invoke(LowerSeedTeamId) ?? LowerSeedTeamId;

            if (HigherSeedWins == 4)
                return $"{higher} wins 4-{LowerSeedWins}";
            if (LowerSeedWins == 4)
                return $"{lower} wins 4-{HigherSeedWins}";
            if (HigherSeedWins > LowerSeedWins)
                return $"{higher} leads {HigherSeedWins}-{LowerSeedWins}";
            if (LowerSeedWins > HigherSeedWins)
                return $"{lower} leads {LowerSeedWins}-{HigherSeedWins}";
            return $"Series tied {HigherSeedWins}-{LowerSeedWins}";
        }

        /// <summary>
        /// Check if this is an elimination game for either team
        /// </summary>
        public bool IsEliminationGame()
        {
            return HigherSeedWins == 3 || LowerSeedWins == 3;
        }

        /// <summary>
        /// Check if this is Game 7
        /// </summary>
        public bool IsGame7()
        {
            return HigherSeedWins == 3 && LowerSeedWins == 3;
        }
    }

    /// <summary>
    /// A single playoff game
    /// </summary>
    [Serializable]
    public class PlayoffGame
    {
        public string GameId;
        public string SeriesId;
        public PlayoffRound Round;
        public int GameNumber;

        public DateTime Date;
        public string HomeTeamId;
        public string AwayTeamId;

        public int HomeScore;
        public int AwayScore;

        public bool IsComplete;
        public bool WasOvertime;
        public int OvertimePeriods;

        public string WinnerTeamId => IsComplete
            ? (HomeScore > AwayScore ? HomeTeamId : AwayTeamId)
            : null;
        public string LoserTeamId => IsComplete
            ? (HomeScore > AwayScore ? AwayTeamId : HomeTeamId)
            : null;

        /// <summary>
        /// Generate game ID
        /// </summary>
        public void GenerateId()
        {
            GameId = $"{SeriesId}_G{GameNumber}";
        }
    }
}
