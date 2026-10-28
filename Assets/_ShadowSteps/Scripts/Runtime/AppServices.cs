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
    /// <summary>
    /// Process-wide services (level catalog, save data). Initialised once by the Bootstrap flow or lazily by any scene
    /// that is started directly in the editor. Kept deliberately small: no DI container, just a static entry point that
    /// junior developers can read top to bottom.
    /// </summary>
    public static class AppServices
    {
        public const string LevelsRelativePath = "ShadowSteps/levels.json";
        /// <summary>Packs loaded in catalog order. Missing optional packs are skipped; duplicate ids are an error.</summary>
        public static readonly string[] PackRelativePaths = { "ShadowSteps/levels_tutorial_v2.json", "ShadowSteps/levels.json" };
        public const string SaveFileName = "shadowsteps_save.json";

        public static LevelCatalog Levels { get; private set; }
        public static SaveRepository SaveRepo { get; private set; }
        public static SaveData Save { get; private set; }
        public static bool IsInitialized => Levels != null && Save != null;
        public static string LastError { get; private set; }
        /// <summary>Level id the Game scene should open next (set by menus; 0 = first unfinished level).</summary>
        public static int PendingLevelId { get; set; }
        /// <summary>Which menu screen the Home scene should open (null = Home).</summary>
        public static string PendingMenu { get; set; }
        public const string MenuLevelSelect = "levels";
        /// <summary>Solver par per level from the validation files (empty when the files are missing).</summary>
        public static ParTable Pars { get; private set; } = new ParTable();
        public static readonly string[] ValidationRelativePaths = { "ShadowSteps/validation_tutorial_v2.json", "ShadowSteps/validation_v2.json" };

        public static string LevelsPath => Path.Combine(Application.streamingAssetsPath, LevelsRelativePath);
        public static string PackPath(string relative) => Path.Combine(Application.streamingAssetsPath, relative);
        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>Synchronous initialisation. Works wherever StreamingAssets is a plain folder (editor, desktop). Returns false on Android (use <see cref="InitializeAsync"/>).</summary>
        public static bool TryInitializeSync()
        {
            if (IsInitialized) return true;
            try
            {
                if (!File.Exists(LevelsPath)) { LastError = "levels.json not found at " + LevelsPath; return false; }
                var texts = new List<string>();
                foreach (var rel in PackRelativePaths) { var p = PackPath(rel); if (File.Exists(p)) texts.Add(File.ReadAllText(p)); }
                InstallLevels(texts);
                var pars = new List<string>();
                foreach (var rel in ValidationRelativePaths) { var p = PackPath(rel); if (File.Exists(p)) pars.Add(File.ReadAllText(p)); }
                InstallPars(pars);
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
            var texts = new List<string>();
            foreach (var rel in PackRelativePaths)
            {
                string path = PackPath(rel);
                string uri = path.Contains("://") ? path : "file://" + path;
                using (var req = UnityWebRequest.Get(uri))
                {
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        if (rel == LevelsRelativePath)
                        {
                            LastError = "could not read levels.json: " + req.error;
                            Debug.LogError("[ShadowSteps] " + LastError);
                            done?.Invoke(false);
                            yield break;
                        }
                        Debug.Log("[ShadowSteps] optional pack " + rel + " not present (" + req.error + ")");
                        continue;
                    }
                    texts.Add(req.downloadHandler.text);
                }
            }
            var parTexts = new List<string>();
            foreach (var rel in ValidationRelativePaths)
            {
                string path = PackPath(rel);
                using (var req = UnityWebRequest.Get(path.Contains("://") ? path : "file://" + path))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success) parTexts.Add(req.downloadHandler.text);
                }
            }
            try
            {
                InstallLevels(texts);
                InstallPars(parTexts);
                InstallSave();
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogError("[ShadowSteps] initialisation failed: " + e);
                done?.Invoke(false);
                yield break;
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

        /// <summary>Parses several pack files and merges them in order into one catalog.</summary>
        public static void InstallLevels(IReadOnlyList<string> jsonTexts)
        {
            var merged = new List<LevelDefinition>();
            var warnings = new List<string>();
            int format = 0;
            foreach (var json in jsonTexts)
            {
                var pack = LevelPackLoader.Parse(json);
                merged.AddRange(pack.Levels);
                warnings.AddRange(pack.Warnings);
                format = Math.Max(format, pack.FormatVersion);
            }
            InstallLevels(new LevelPack(format, "origin top-left, x right, y down", merged, warnings));
        }

        public static void InstallPars(IReadOnlyList<string> validationJsonTexts)
        {
            var table = new ParTable();
            foreach (var json in validationJsonTexts)
            {
                try { table.AddFromValidationJson(json); }
                catch (Exception e) { Debug.LogWarning("[ShadowSteps] validation file ignored: " + e.Message); }
            }
            Pars = table;
        }

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

        /// <summary>Tests only: use an in-memory save (nothing is written to disk) so scenes do not touch the player's real save.</summary>
        public static void InstallSaveForTests(SaveData data)
        {
            SaveRepo = null;
            Save = data ?? new SaveData();
        }

        /// <summary>Tests only: discard everything so the next scene re-initialises.</summary>
        public static void ResetForTests()
        {
            Levels = null; SaveRepo = null; Save = null; LastError = null; PendingLevelId = 0; PendingMenu = null; Pars = new ParTable();
        }
    }
}
