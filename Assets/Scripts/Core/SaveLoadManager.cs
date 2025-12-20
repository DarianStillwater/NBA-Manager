using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Manages saving and loading game state to/from disk.
    /// Supports Ironman mode (single auto-save, no manual saves, save-on-load deletion).
    /// </summary>
    public class SaveLoadManager
    {
        private const string SAVE_FOLDER = "Saves";
        private const string SAVE_EXTENSION = ".nbahc";
        private const string AUTO_SAVE_NAME = "AutoSave";
        private const string IRONMAN_SAVE_NAME = "Ironman";
        private const int MAX_AUTO_SAVES = 3;

        private string _savePath;
        private int _autoSaveIndex;

        // Ironman mode tracking
        private bool _isIronmanMode;
        private string _currentIronmanSlot;

        public bool IsIronmanMode => _isIronmanMode;

        public SaveLoadManager()
        {
            // Use persistent data path for saves
            _savePath = Path.Combine(Application.persistentDataPath, SAVE_FOLDER);

            // Ensure save directory exists
            if (!Directory.Exists(_savePath))
            {
                Directory.CreateDirectory(_savePath);
                Debug.Log($"[SaveLoadManager] Created save directory: {_savePath}");
            }
        }

        #region Ironman Mode

        /// <summary>
        /// Enable Ironman mode for a new game.
        /// </summary>
        public void EnableIronmanMode(string careerName)
        {
            _isIronmanMode = true;
            _currentIronmanSlot = $"{IRONMAN_SAVE_NAME}_{SanitizeName(careerName)}";
            Debug.Log($"[SaveLoadManager] Ironman mode enabled: {_currentIronmanSlot}");
        }

        /// <summary>
        /// Disable Ironman mode (when returning to menu).
        /// </summary>
        public void DisableIronmanMode()
        {
            _isIronmanMode = false;
            _currentIronmanSlot = null;
        }

        /// <summary>
        /// Save in Ironman mode - single slot, overwrites each time.
        /// </summary>
        public bool IronmanSave(SaveData data)
        {
            if (!_isIronmanMode || string.IsNullOrEmpty(_currentIronmanSlot))
            {
                Debug.LogError("[SaveLoadManager] Not in Ironman mode");
                return false;
            }

            data.IsIronman = true;
            data.SaveName = $"Ironman - {data.Career?.PersonName ?? "Unknown"}";
            return SaveGame(data, _currentIronmanSlot);
        }

        /// <summary>
        /// Load an Ironman save - creates backup before loading, deletes after successful load.
        /// This prevents save-scumming by ensuring you can't reload the same save repeatedly.
        /// </summary>
        public SaveData LoadIronmanGame(string slotName)
        {
            try
            {
                string filePath = GetSaveFilePath(slotName);
                string backupPath = filePath + ".backup";

                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[SaveLoadManager] Ironman save not found: {filePath}");
                    return null;
                }

                // Create a temporary backup in case of crash during load
                File.Copy(filePath, backupPath, overwrite: true);

                // Load the save
                string json = File.ReadAllText(filePath);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data == null || !data.IsIronman)
                {
                    Debug.LogError("[SaveLoadManager] Invalid Ironman save data");
                    return null;
                }

                // Set Ironman mode state
                _isIronmanMode = true;
                _currentIronmanSlot = slotName;

                // Delete the save file to prevent reloading
                // The game will auto-save on next action, creating a new save point
                File.Delete(filePath);

                // Delete backup after successful load
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                Debug.Log($"[SaveLoadManager] Ironman save loaded and consumed: {slotName}");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadManager] Ironman load failed: {ex.Message}");

                // Restore from backup if it exists
                string filePath = GetSaveFilePath(slotName);
                string backupPath = filePath + ".backup";
                if (File.Exists(backupPath) && !File.Exists(filePath))
                {
                    File.Move(backupPath, filePath);
                    Debug.Log("[SaveLoadManager] Restored from backup after failed load");
                }

                return null;
            }
        }

        /// <summary>
        /// Check if a save is an Ironman save.
        /// </summary>
        public bool IsIronmanSave(string slotName)
        {
            return slotName.StartsWith(IRONMAN_SAVE_NAME);
        }

        /// <summary>
        /// Get all Ironman saves.
        /// </summary>
        public List<SaveSlotInfo> GetIronmanSaves()
        {
            return GetAllSaves()
                .Where(s => s.IsIronman || s.SlotName.StartsWith(IRONMAN_SAVE_NAME))
                .ToList();
        }

        /// <summary>
        /// Delete an Ironman career completely (no undo).
        /// </summary>
        public bool DeleteIronmanCareer(string slotName)
        {
            if (!IsIronmanSave(slotName))
            {
                Debug.LogWarning("[SaveLoadManager] Attempting to delete non-Ironman save as Ironman");
                return false;
            }

            return DeleteSave(slotName);
        }

        private string SanitizeName(string name)
        {
            return string.Join("_", name.Split(Path.GetInvalidFileNameChars()))
                .Replace(" ", "_")
                .Substring(0, Math.Min(name.Length, 20));
        }

        #endregion

        #region Save Operations

        /// <summary>
        /// Save game to a named slot
        /// </summary>
        public bool SaveGame(SaveData data, string slotName)
        {
            try
            {
                data.SaveSlot = slotName;
                data.SaveTimestamp = DateTime.Now;

                string filePath = GetSaveFilePath(slotName);
                string json = JsonUtility.ToJson(data, prettyPrint: true);

                File.WriteAllText(filePath, json);
                Debug.Log($"[SaveLoadManager] Game saved to: {filePath}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadManager] Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Auto-save (rotates through AUTO_SAVE_NAME_1, _2, _3)
        /// </summary>
        public bool AutoSave(SaveData data)
        {
            _autoSaveIndex = (_autoSaveIndex % MAX_AUTO_SAVES) + 1;
            string slotName = $"{AUTO_SAVE_NAME}_{_autoSaveIndex}";
            data.SaveName = $"Auto Save {DateTime.Now:MMM dd, HH:mm}";

            return SaveGame(data, slotName);
        }

        /// <summary>
        /// Quick save to default slot
        /// </summary>
        public bool QuickSave(SaveData data)
        {
            data.SaveName = $"Quick Save {DateTime.Now:MMM dd, HH:mm}";
            return SaveGame(data, "QuickSave");
        }

        #endregion

        #region Load Operations

        /// <summary>
        /// Load game from a named slot
        /// </summary>
        public SaveData LoadGame(string slotName)
        {
            try
            {
                string filePath = GetSaveFilePath(slotName);

                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[SaveLoadManager] Save file not found: {filePath}");
                    return null;
                }

                string json = File.ReadAllText(filePath);
                var data = JsonUtility.FromJson<SaveData>(json);

                // Version check
                if (data.SaveVersion != SaveData.CURRENT_VERSION)
                {
                    Debug.LogWarning($"[SaveLoadManager] Save version mismatch: {data.SaveVersion} vs {SaveData.CURRENT_VERSION}");
                    // Could add migration logic here
                }

                Debug.Log($"[SaveLoadManager] Game loaded from: {filePath}");
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadManager] Load failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load the most recent auto-save
        /// </summary>
        public SaveData LoadLatestAutoSave()
        {
            var autoSaves = GetAutoSaves();
            if (autoSaves.Count == 0) return null;

            var latest = autoSaves.OrderByDescending(s => s.SaveTimestamp).First();
            return LoadGame(latest.SlotName);
        }

        #endregion

        #region Save Slot Management

        /// <summary>
        /// Get all save slot info (for save/load UI)
        /// </summary>
        public List<SaveSlotInfo> GetAllSaves()
        {
            var saves = new List<SaveSlotInfo>();

            if (!Directory.Exists(_savePath))
                return saves;

            foreach (var file in Directory.GetFiles(_savePath, $"*{SAVE_EXTENSION}"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var data = JsonUtility.FromJson<SaveData>(json);

                    if (data != null)
                    {
                        saves.Add(data.CreateSlotInfo());
                    }
                }
                catch
                {
                    // Skip corrupted saves
                }
            }

            return saves.OrderByDescending(s => s.SaveTimestamp).ToList();
        }

        /// <summary>
        /// Get only auto-saves
        /// </summary>
        public List<SaveSlotInfo> GetAutoSaves()
        {
            return GetAllSaves()
                .Where(s => s.SlotName.StartsWith(AUTO_SAVE_NAME))
                .ToList();
        }

        /// <summary>
        /// Get only manual saves (non-auto)
        /// </summary>
        public List<SaveSlotInfo> GetManualSaves()
        {
            return GetAllSaves()
                .Where(s => !s.SlotName.StartsWith(AUTO_SAVE_NAME))
                .ToList();
        }

        /// <summary>
        /// Check if a save slot exists
        /// </summary>
        public bool SaveExists(string slotName)
        {
            return File.Exists(GetSaveFilePath(slotName));
        }

        /// <summary>
        /// Delete a save file
        /// </summary>
        public bool DeleteSave(string slotName)
        {
            try
            {
                string filePath = GetSaveFilePath(slotName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.Log($"[SaveLoadManager] Deleted save: {slotName}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadManager] Delete failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the next available save slot number
        /// </summary>
        public string GetNextSaveSlotName()
        {
            int slot = 1;
            while (SaveExists($"Save_{slot:D2}"))
            {
                slot++;
            }
            return $"Save_{slot:D2}";
        }

        #endregion

        #region Helper Methods

        private string GetSaveFilePath(string slotName)
        {
            // Sanitize slot name
            string safeName = string.Join("_", slotName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_savePath, $"{safeName}{SAVE_EXTENSION}");
        }

        /// <summary>
        /// Get the save directory path
        /// </summary>
        public string GetSaveDirectory() => _savePath;

        /// <summary>
        /// Export save to a specific path (for backup)
        /// </summary>
        public bool ExportSave(string slotName, string exportPath)
        {
            try
            {
                string sourcePath = GetSaveFilePath(slotName);
                if (!File.Exists(sourcePath)) return false;

                File.Copy(sourcePath, exportPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadManager] Export failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Import save from external path
        /// </summary>
        public bool ImportSave(string importPath, string slotName)
        {
            try
            {
                if (!File.Exists(importPath)) return false;

                string destPath = GetSaveFilePath(slotName);
                File.Copy(importPath, destPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadManager] Import failed: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
