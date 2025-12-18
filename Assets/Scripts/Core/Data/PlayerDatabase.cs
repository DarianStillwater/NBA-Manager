using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Loads and manages player data from JSON files.
    /// Supports modding through user override folders.
    /// </summary>
    public class PlayerDatabase
    {
        private Dictionary<string, Player> _players = new Dictionary<string, Player>();
        private Dictionary<string, Team> _teams = new Dictionary<string, Team>();
        private static readonly string BASE_DATA_PATH = Path.Combine(Application.streamingAssetsPath, "Data");
        private static readonly string MOD_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NBAHeadCoach", "Mods"
        );

        public IReadOnlyDictionary<string, Player> Players => _players;
        public IReadOnlyDictionary<string, Team> Teams => _teams;
        public int PlayerCount => _players.Count;

        /// <summary>
        /// Gets all players as a list.
        /// </summary>
        public List<Player> GetAllPlayers() => new List<Player>(_players.Values);

        /// <summary>
        /// Adds a player to the database at runtime.
        /// </summary>
        public void AddPlayer(Player player)
        {
            if (player != null && !string.IsNullOrEmpty(player.PlayerId))
            {
                _players[player.PlayerId] = player;
            }
        }

        /// <summary>
        /// Loads all players from base data and applies any mods.
        /// </summary>
        public void LoadAllPlayers()
        {
            _players.Clear();

            // 1. Load base fictional players
            string basePath = Path.Combine(BASE_DATA_PATH, "players.json");
            if (File.Exists(basePath))
            {
                LoadPlayersFromFile(basePath, overwrite: false);
                Debug.Log($"Loaded {_players.Count} base players");
            }
            else
            {
                Debug.LogWarning($"Base players file not found: {basePath}");
            }

            // 2. Apply mods (overwrite or add)
            if (Directory.Exists(MOD_PATH))
            {
                string[] modFiles = Directory.GetFiles(MOD_PATH, "*_players*.json");
                foreach (var modFile in modFiles)
                {
                    int before = _players.Count;
                    LoadPlayersFromFile(modFile, overwrite: true);
                    Debug.Log($"Applied mod: {Path.GetFileName(modFile)} (+{_players.Count - before} players)");
                }
            }
        }

        /// <summary>
        /// Loads players from a JSON file.
        /// </summary>
        private void LoadPlayersFromFile(string filePath, bool overwrite)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var wrapper = JsonUtility.FromJson<PlayerListWrapper>(json);

                if (wrapper?.Players == null)
                {
                    Debug.LogError($"Invalid player data in: {filePath}");
                    return;
                }

                foreach (var player in wrapper.Players)
                {
                    if (string.IsNullOrEmpty(player.PlayerId))
                    {
                        Debug.LogWarning($"Skipping player with no ID in {filePath}");
                        continue;
                    }

                    if (_players.ContainsKey(player.PlayerId))
                    {
                        if (overwrite)
                        {
                            _players[player.PlayerId] = player;
                        }
                    }
                    else
                    {
                        _players[player.PlayerId] = player;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading players from {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a player by ID.
        /// </summary>
        public Player GetPlayer(string playerId)
        {
            return _players.TryGetValue(playerId, out var player) ? player : null;
        }

        /// <summary>
        /// Gets all players of a specific position.
        /// </summary>
        public List<Player> GetPlayersByPosition(Position position)
        {
            var result = new List<Player>();
            foreach (var player in _players.Values)
            {
                if (player.Position == position)
                {
                    result.Add(player);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets players sorted by overall rating.
        /// </summary>
        public List<Player> GetTopPlayers(int count)
        {
            var sorted = new List<Player>(_players.Values);
            sorted.Sort((a, b) => b.OverallRating.CompareTo(a.OverallRating));
            return sorted.GetRange(0, Math.Min(count, sorted.Count));
        }

        /// <summary>
        /// Searches players by name.
        /// </summary>
        public List<Player> SearchByName(string searchTerm)
        {
            var results = new List<Player>();
            searchTerm = searchTerm.ToLower();

            foreach (var player in _players.Values)
            {
                if (player.FullName.ToLower().Contains(searchTerm))
                {
                    results.Add(player);
                }
            }
            return results;
        }

        /// <summary>
        /// Saves all players to JSON (for debugging/editing).
        /// </summary>
        public void SaveToFile(string filePath)
        {
            var wrapper = new PlayerListWrapper
            {
                Players = new List<Player>(_players.Values)
            };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(filePath, json);
        }

        // ==================== ASYNC LOADING ====================

        /// <summary>
        /// Loads all game data asynchronously (players and teams).
        /// </summary>
        public IEnumerator LoadAllDataAsync()
        {
            // Load players
            LoadAllPlayers();
            yield return null;

            // Load teams
            LoadAllTeams();
            yield return null;

            // Associate players with teams
            AssignPlayersToTeams();
            yield return null;

            Debug.Log($"[PlayerDatabase] Loaded {_players.Count} players, {_teams.Count} teams");
        }

        /// <summary>
        /// Loads all teams from JSON files.
        /// </summary>
        private void LoadAllTeams()
        {
            _teams.Clear();

            string teamsPath = Path.Combine(BASE_DATA_PATH, "teams.json");
            if (File.Exists(teamsPath))
            {
                try
                {
                    string json = File.ReadAllText(teamsPath);
                    var wrapper = JsonUtility.FromJson<TeamListWrapper>(json);
                    if (wrapper?.Teams != null)
                    {
                        foreach (var team in wrapper.Teams)
                        {
                            if (!string.IsNullOrEmpty(team.TeamId))
                            {
                                _teams[team.TeamId] = team;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading teams: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"Teams file not found: {teamsPath}");
                // Create default teams if needed
                CreateDefaultTeams();
            }
        }

        /// <summary>
        /// Creates default NBA teams if no data file exists.
        /// </summary>
        private void CreateDefaultTeams()
        {
            var defaultTeams = new (string id, string city, string name, string conference, string division, string arena)[]
            {
                ("ATL", "Atlanta", "Hawks", "Eastern", "Southeast", "State Farm Arena"),
                ("BOS", "Boston", "Celtics", "Eastern", "Atlantic", "TD Garden"),
                ("BKN", "Brooklyn", "Nets", "Eastern", "Atlantic", "Barclays Center"),
                ("CHA", "Charlotte", "Hornets", "Eastern", "Southeast", "Spectrum Center"),
                ("CHI", "Chicago", "Bulls", "Eastern", "Central", "United Center"),
                ("CLE", "Cleveland", "Cavaliers", "Eastern", "Central", "Rocket Mortgage FieldHouse"),
                ("DAL", "Dallas", "Mavericks", "Western", "Southwest", "American Airlines Center"),
                ("DEN", "Denver", "Nuggets", "Western", "Northwest", "Ball Arena"),
                ("DET", "Detroit", "Pistons", "Eastern", "Central", "Little Caesars Arena"),
                ("GSW", "Golden State", "Warriors", "Western", "Pacific", "Chase Center"),
                ("HOU", "Houston", "Rockets", "Western", "Southwest", "Toyota Center"),
                ("IND", "Indiana", "Pacers", "Eastern", "Central", "Gainbridge Fieldhouse"),
                ("LAC", "Los Angeles", "Clippers", "Western", "Pacific", "Intuit Dome"),
                ("LAL", "Los Angeles", "Lakers", "Western", "Pacific", "Crypto.com Arena"),
                ("MEM", "Memphis", "Grizzlies", "Western", "Southwest", "FedExForum"),
                ("MIA", "Miami", "Heat", "Eastern", "Southeast", "Kaseya Center"),
                ("MIL", "Milwaukee", "Bucks", "Eastern", "Central", "Fiserv Forum"),
                ("MIN", "Minnesota", "Timberwolves", "Western", "Northwest", "Target Center"),
                ("NOP", "New Orleans", "Pelicans", "Western", "Southwest", "Smoothie King Center"),
                ("NYK", "New York", "Knicks", "Eastern", "Atlantic", "Madison Square Garden"),
                ("OKC", "Oklahoma City", "Thunder", "Western", "Northwest", "Paycom Center"),
                ("ORL", "Orlando", "Magic", "Eastern", "Southeast", "Kia Center"),
                ("PHI", "Philadelphia", "76ers", "Eastern", "Atlantic", "Wells Fargo Center"),
                ("PHX", "Phoenix", "Suns", "Western", "Pacific", "Footprint Center"),
                ("POR", "Portland", "Trail Blazers", "Western", "Northwest", "Moda Center"),
                ("SAC", "Sacramento", "Kings", "Western", "Pacific", "Golden 1 Center"),
                ("SAS", "San Antonio", "Spurs", "Western", "Southwest", "Frost Bank Center"),
                ("TOR", "Toronto", "Raptors", "Eastern", "Atlantic", "Scotiabank Arena"),
                ("UTA", "Utah", "Jazz", "Western", "Northwest", "Delta Center"),
                ("WAS", "Washington", "Wizards", "Eastern", "Southeast", "Capital One Arena")
            };

            foreach (var t in defaultTeams)
            {
                _teams[t.id] = new Team
                {
                    TeamId = t.id,
                    City = t.city,
                    Nickname = t.name,
                    Conference = t.conference,
                    Division = t.division,
                    ArenaName = t.arena,
                    Roster = new List<Player>(),
                    RosterPlayerIds = new List<string>(),
                    Wins = 0,
                    Losses = 0
                };
            }

            Debug.Log($"[PlayerDatabase] Created {_teams.Count} default teams");
        }

        /// <summary>
        /// Associates players with their teams based on TeamId.
        /// </summary>
        private void AssignPlayersToTeams()
        {
            // Clear existing rosters
            foreach (var team in _teams.Values)
            {
                team.Roster = team.Roster ?? new List<Player>();
                team.Roster.Clear();
            }

            // Assign players to teams
            foreach (var player in _players.Values)
            {
                if (!string.IsNullOrEmpty(player.TeamId) && _teams.TryGetValue(player.TeamId, out var team))
                {
                    team.Roster.Add(player);
                }
            }
        }

        // ==================== TEAM ACCESS ====================

        /// <summary>
        /// Gets all teams as a list.
        /// </summary>
        public List<Team> GetAllTeams() => new List<Team>(_teams.Values);

        /// <summary>
        /// Gets a team by ID.
        /// </summary>
        public Team GetTeam(string teamId)
        {
            return _teams.TryGetValue(teamId, out var team) ? team : null;
        }

        /// <summary>
        /// Gets all players on a specific team.
        /// </summary>
        public List<Player> GetPlayersByTeam(string teamId)
        {
            if (_teams.TryGetValue(teamId, out var team))
            {
                return team.Roster ?? new List<Player>();
            }
            return _players.Values.Where(p => p.TeamId == teamId).ToList();
        }
    }

    /// <summary>
    /// Wrapper for JSON serialization of player lists.
    /// </summary>
    [Serializable]
    public class PlayerListWrapper
    {
        public List<Player> Players;
    }

    /// <summary>
    /// Wrapper for JSON serialization of team lists.
    /// </summary>
    [Serializable]
    public class TeamListWrapper
    {
        public List<Team> Teams;
    }
}
