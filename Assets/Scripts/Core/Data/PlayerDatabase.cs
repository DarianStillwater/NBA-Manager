using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly string BASE_DATA_PATH = Path.Combine(Application.streamingAssetsPath, "Data");
        private static readonly string MOD_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "NBAHeadCoach", "Mods"
        );

        public IReadOnlyDictionary<string, Player> Players => _players;
        public int PlayerCount => _players.Count;

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
    }

    /// <summary>
    /// Wrapper for JSON serialization of player lists.
    /// </summary>
    [Serializable]
    public class PlayerListWrapper
    {
        public List<Player> Players;
    }
}
