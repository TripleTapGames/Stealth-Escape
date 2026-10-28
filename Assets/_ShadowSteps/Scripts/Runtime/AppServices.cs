using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace ShadowSteps.Runtime
{
    /// <summary>Read-only access to the loaded level pack plus lookups by id/index.</summary>
    public sealed class LevelCatalog
    {
        public LevelPack Pack { get; }
        public IReadOnlyList<LevelDefinition> Levels => Pack.Levels;
        public int Count => Pack.Levels.Count;
        public LevelCatalog(LevelPack pack) { Pack = pack; }
        public LevelDefinition ById(int id) => Pack.FindById(id);
        public LevelDefinition ByIndex(int index) => index >= 0 && index < Count ? Pack.Levels[index] : null;
        public int IndexOf(int id) => Pack.IndexOf(id);
        public LevelDefinition NextAfter(int id) { int i = IndexOf(id); return i >= 0 ? ByIndex(i + 1) : null; }
    }

    /// <summary>
    /// Process-wide services (level catalog, save data). Initialised once by the Bootstrap flow or lazily by any scene
    /// that is started directly in the editor. Kept deliberately small: no DI container, just a static entry point that
    /// junior developers can read top to bottom.
    /// </summary>
    public static class AppServices
    {
        public const string LevelsRelativePath = "ShadowSteps/levels.json";
        public const string SaveFileName = "shadowsteps_save.json";

        public static LevelCatalog Levels { get; private set; }
        public static SaveRepository SaveRepo { get; private set; }
        public static SaveData Save { get; private set; }
        public static bool IsInitialized => Levels != null && Save != null;
        public static string LastError { get; private set; }
        /// <summary>Level id the Game scene should open next (set by menus; 0 = use the save's last played level).</summary>
        public static int PendingLevelId { get; set; }

        public static string LevelsPath => Path.Combine(Application.streamingAssetsPath, LevelsRelativePath);
        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>Synchronous initialisation. Works wherever StreamingAssets is a plain folder (editor, desktop). Returns false on Android (use <see cref="InitializeAsync"/>).</summary>
        public static bool TryInitializeSync()
        {
            if (IsInitialized) return true;
            try
            {
                if (!File.Exists(LevelsPath)) { LastError = "levels.json not found at " + LevelsPath; return false; }
                InstallLevels(File.ReadAllText(LevelsPath));
                InstallSave();
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogError("[ShadowSteps] initialisation failed: " + e);
                return false;
            }
        }

        /// <summary>Coroutine initialisation using UnityWebRequest so it also works for StreamingAssets inside an APK.</summary>
        public static IEnumerator InitializeAsync(Action<bool> done)
        {
            if (IsInitialized) { done?.Invoke(true); yield break; }
            string uri = LevelsPath.Contains("://") ? LevelsPath : "file://" + LevelsPath;
            using (var req = UnityWebRequest.Get(uri))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    LastError = "could not read levels.json: " + req.error;
                    Debug.LogError("[ShadowSteps] " + LastError);
                    done?.Invoke(false);
                    yield break;
                }
                try
                {
                    InstallLevels(req.downloadHandler.text);
                    InstallSave();
                }
                catch (Exception e)
                {
                    LastError = e.Message;
                    Debug.LogError("[ShadowSteps] initialisation failed: " + e);
                    done?.Invoke(false);
                    yield break;
                }
            }
            done?.Invoke(true);
        }

        /// <summary>Installs an already-parsed pack (tests, editor tools).</summary>
        public static void InstallLevels(LevelPack pack)
        {
            var report = LevelValidator.ValidatePack(pack.Levels);
            foreach (var w in pack.Warnings) Debug.LogWarning("[ShadowSteps] levels.json: " + w);
            foreach (var issue in report.Issues)
                if (issue.Severity == ValidationSeverity.Warning) Debug.LogWarning("[ShadowSteps] " + issue);
            if (!report.IsValid) throw new LevelDataException("levels.json failed validation:\n" + report);
            Levels = new LevelCatalog(pack);
            Debug.Log("[ShadowSteps] loaded " + Levels.Count + " levels (schema " + pack.FormatVersion + ")");
        }

        private static void InstallLevels(string json) => InstallLevels(LevelPackLoader.Parse(json));

        private static void InstallSave()
        {
            SaveRepo = new SaveRepository(SavePath);
            Save = SaveRepo.Load();
            Debug.Log("[ShadowSteps] save: " + SaveRepo.LastLoadStatus + " (" + SaveRepo.LastMessage + ")");
        }

        public static void PersistSave()
        {
            if (SaveRepo == null || Save == null) return;
            if (!SaveRepo.Save(Save)) Debug.LogError("[ShadowSteps] " + SaveRepo.LastMessage);
        }

        /// <summary>Tests only: discard everything so the next scene re-initialises.</summary>
        public static void ResetForTests()
        {
            Levels = null; SaveRepo = null; Save = null; LastError = null; PendingLevelId = 0;
        }
    }
}
