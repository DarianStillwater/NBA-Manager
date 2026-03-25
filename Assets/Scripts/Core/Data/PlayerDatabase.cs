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
        /// Registers a generated player (drafted rookie, etc.) with ID collision prevention.
        /// Sets the IsGenerated flag and ensures a unique ID.
        /// </summary>
        public void RegisterGeneratedPlayer(Player player)
        {
            if (player == null) return;

            player.IsGenerated = true;

            // Ensure unique ID - if collision, add suffix
            string baseId = player.PlayerId;
            if (string.IsNullOrEmpty(baseId))
            {
                // Generate ID from name if not set
                baseId = $"{player.FirstName.ToLower()}_{player.LastName.ToLower()}".Replace(" ", "_");
                player.PlayerId = baseId;
            }

            // Handle ID collision
            if (_players.ContainsKey(player.PlayerId))
            {
                int suffix = 1;
                string newId = $"{baseId}_{suffix}";
                while (_players.ContainsKey(newId))
                {
                    suffix++;
                    newId = $"{baseId}_{suffix}";
                }
                player.PlayerId = newId;
                Debug.Log($"[PlayerDatabase] ID collision resolved: {baseId} -> {newId}");
            }

            _players[player.PlayerId] = player;
            Debug.Log($"[PlayerDatabase] Registered generated player: {player.FullName} ({player.PlayerId})");
        }

        /// <summary>
        /// Checks if a player with the given ID exists in the database.
        /// </summary>
        public bool HasPlayer(string playerId)
        {
            return !string.IsNullOrEmpty(playerId) && _players.ContainsKey(playerId);
        }

        /// <summary>
        /// Gets all generated players (not from base database).
        /// </summary>
        public List<Player> GetGeneratedPlayers()
        {
            return _players.Values.Where(p => p.IsGenerated).ToList();
        }

        /// <summary>
        /// Removes a player from the database.
        /// </summary>
        public bool RemovePlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            return _players.Remove(playerId);
        }

        /// <summary>
        /// Clears all generated players from the database.
        /// Used when starting a new game to reset to base roster.
        /// </summary>
        public void ClearGeneratedPlayers()
        {
            var generatedIds = _players.Values
                .Where(p => p.IsGenerated)
                .Select(p => p.PlayerId)
                .ToList();

            foreach (var id in generatedIds)
            {
                _players.Remove(id);
            }

            Debug.Log($"[PlayerDatabase] Cleared {generatedIds.Count} generated players");
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

                // Preprocess: convert Position strings to enum ints for JsonUtility
                json = json.Replace("\"Position\": \"PG\"", "\"Position\": 1");
                json = json.Replace("\"Position\": \"SG\"", "\"Position\": 2");
                json = json.Replace("\"Position\": \"SF\"", "\"Position\": 3");
                json = json.Replace("\"Position\": \"PF\"", "\"Position\": 4");
                json = json.Replace("\"Position\": \"C\"", "\"Position\": 5");
                json = json.Replace("\"Position\":\"PG\"", "\"Position\":1");
                json = json.Replace("\"Position\":\"SG\"", "\"Position\":2");
                json = json.Replace("\"Position\":\"SF\"", "\"Position\":3");
                json = json.Replace("\"Position\":\"PF\"", "\"Position\":4");
                json = json.Replace("\"Position\":\"C\"", "\"Position\":5");

                // Extract PlayerId→BirthDate mapping before we mangle the JSON
                var birthDates = new Dictionary<string, string>();
                try
                {
                    // Use a simple JSON tokenizer approach
                    var matches = System.Text.RegularExpressions.Regex.Matches(json,
                        @"""PlayerId""\s*:\s*""([^""]+)""");
                    var bdMatches = System.Text.RegularExpressions.Regex.Matches(json,
                        @"""BirthDate""\s*:\s*""(\d{4}-\d{2}-\d{2})""");

                    // Both lists should be same length — one per player
                    int count = Math.Min(matches.Count, bdMatches.Count);
                    for (int i = 0; i < count; i++)
                        birthDates[matches[i].Groups[1].Value] = bdMatches[i].Groups[1].Value;

                    Debug.Log($"[PlayerDatabase] Extracted {birthDates.Count} birthdates");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayerDatabase] BirthDate extraction failed: {ex.Message}");
                }

                // Remove BirthDate lines entirely — JsonUtility can't parse ISO date strings to DateTime
                json = System.Text.RegularExpressions.Regex.Replace(json,
                    @"\s*""BirthDate""\s*:\s*""[^""]*""\s*,?", "");

                // Extract Attributes blocks per player (JsonUtility can't map nested objects to flat fields)
                var playerAttributes = new Dictionary<string, Dictionary<string, int>>();
                try
                {
                    // Find each player's Attributes block
                    var attrPattern = new System.Text.RegularExpressions.Regex(
                        @"""PlayerId""\s*:\s*""([^""]+)"".*?""Attributes""\s*:\s*\{([^}]+)\}",
                        System.Text.RegularExpressions.RegexOptions.Singleline);
                    var attrMatches = attrPattern.Matches(json);
                    foreach (System.Text.RegularExpressions.Match m in attrMatches)
                    {
                        string pid = m.Groups[1].Value;
                        string attrsBlock = m.Groups[2].Value;
                        var attrs = new Dictionary<string, int>();
                        var kvPattern = new System.Text.RegularExpressions.Regex(@"""(\w+)""\s*:\s*(\d+)");
                        foreach (System.Text.RegularExpressions.Match kv in kvPattern.Matches(attrsBlock))
                            attrs[kv.Groups[1].Value] = int.Parse(kv.Groups[2].Value);
                        playerAttributes[pid] = attrs;
                    }
                    Debug.Log($"[PlayerDatabase] Extracted attributes for {playerAttributes.Count} players");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayerDatabase] Attribute extraction failed: {ex.Message}");
                }

                // Remove Attributes blocks so JsonUtility doesn't choke on nested objects
                json = System.Text.RegularExpressions.Regex.Replace(json,
                    @"\s*""Attributes""\s*:\s*\{[^}]*\}\s*,?", "");
                // Clean up trailing commas before closing braces (invalid JSON)
                json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*}", "}");

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

                    // Post-deserialization: parse BirthDate from extracted strings
                    if (birthDates.TryGetValue(player.PlayerId, out string bdStr))
                    {
                        if (DateTime.TryParse(bdStr, out DateTime bd))
                            player.BirthDate = bd;
                    }

                    // Map attributes from extracted JSON block to Player fields
                    if (playerAttributes.TryGetValue(player.PlayerId, out var attrs))
                    {
                        // Physical
                        if (attrs.TryGetValue("Speed", out int v)) player.Speed = v;
                        if (attrs.TryGetValue("Acceleration", out v)) player.Acceleration = v;
                        if (attrs.TryGetValue("Strength", out v)) player.Strength = v;
                        if (attrs.TryGetValue("Vertical", out v)) player.Vertical = v;
                        if (attrs.TryGetValue("Stamina", out v)) player.Stamina = v;
                        if (attrs.TryGetValue("OverallDurability", out v)) player.Durability = v;

                        // Offense - Scoring
                        if (attrs.TryGetValue("InsideScoring", out v)) player.Finishing_Rim = v;
                        if (attrs.TryGetValue("Layups", out v)) player.Shot_Close = v;
                        if (attrs.TryGetValue("PostMoves", out v)) player.Finishing_PostMoves = v;
                        if (attrs.TryGetValue("MidRange", out v)) player.Shot_MidRange = v;
                        if (attrs.TryGetValue("ThreePoint", out v)) player.Shot_Three = v;
                        if (attrs.TryGetValue("FreeThrow", out v)) player.FreeThrow = v;

                        // Offense - Playmaking
                        if (attrs.TryGetValue("BallHandle", out v)) player.BallHandling = v;
                        if (attrs.TryGetValue("PassAccuracy", out v)) player.Passing = v;
                        if (attrs.TryGetValue("OffensiveIQ", out v)) player.OffensiveIQ = v;
                        if (attrs.TryGetValue("PassVision", out v)) player.SpeedWithBall = v; // closest mapping

                        // Defense
                        if (attrs.TryGetValue("PerimeterDefense", out v)) player.Defense_Perimeter = v;
                        if (attrs.TryGetValue("InteriorDefense", out v)) player.Defense_Interior = v;
                        if (attrs.TryGetValue("Steal", out v)) player.Steal = v;
                        if (attrs.TryGetValue("Block", out v)) player.Block = v;
                        if (attrs.TryGetValue("DefensiveRebound", out v)) player.DefensiveRebound = v;
                        if (attrs.TryGetValue("DefensiveIQ", out v)) player.DefensiveIQ = v;

                        // Mental
                        if (attrs.TryGetValue("Intangibles", out v)) player.BasketballIQ = v;
                        if (attrs.TryGetValue("OffensiveConsistency", out v)) player.Consistency = v;
                        if (attrs.TryGetValue("Hustle", out v)) player.WorkEthic = v;
                        if (attrs.TryGetValue("Potential", out v)) player.Coachability = v;
                        if (attrs.TryGetValue("DrawFoul", out v)) player.Clutch = v; // approximate

                        // Dunking (map to closest)
                        if (attrs.TryGetValue("StandingDunk", out int sd) && attrs.TryGetValue("DrivingDunk", out int dd))
                            player.Vertical = Math.Max(player.Vertical, Math.Max(sd, dd));
                    }

                    // Initialize dynamic state
                    if (player.Energy <= 0) player.Energy = 100f;
                    if (player.Morale <= 0) player.Morale = 75f;
                    if (player.Form <= 0) player.Form = 50f;

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

            // Sync RosterPlayerIds from Roster
            foreach (var team in _teams.Values)
            {
                team.RosterPlayerIds.Clear();
                foreach (var player in team.Roster)
                {
                    if (!string.IsNullOrEmpty(player?.PlayerId))
                        team.RosterPlayerIds.Add(player.PlayerId);
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
