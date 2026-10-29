using System;
using System.Collections;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.Networking;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Process-wide services (stage catalog, save data). Initialised once by the Bootstrap flow or lazily by any scene
    /// that is started directly in the editor. Kept deliberately small: no DI container, just a static entry point that
    /// junior developers can read top to bottom.
    /// </summary>
    public static class AppServices
    {
        /// <summary>Schema-3 stage pack (5 FTUE + 25 main). Scoring inside the file is solver-written.</summary>
        public const string StagePackRelativePath = "ShadowSteps/levels_v3.json";
        public const string SaveFileName = "shadowsteps_save.json";

        public static StageCatalog Stages { get; private set; }
        public static SaveRepository SaveRepo { get; private set; }
        public static SaveData Save { get; private set; }
        public static bool IsInitialized => Stages != null && Save != null;
        public static string LastError { get; private set; }
        /// <summary>Stage id the Game scene should open next (set by menus; 0 = first unfinished stage).</summary>
        public static int PendingStageId { get; set; }
        /// <summary>Which menu screen the Home scene should open (null = Home).</summary>
        public static string PendingMenu { get; set; }
        public const string MenuLevelSelect = "levels";

        public static string StagePackPath => Path.Combine(Application.streamingAssetsPath, StagePackRelativePath);
        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>Synchronous initialisation. Works wherever StreamingAssets is a plain folder (editor, desktop). Returns false on Android (use <see cref="InitializeAsync"/>).</summary>
        public static bool TryInitializeSync()
        {
            if (IsInitialized) return true;
            try
            {
                if (!File.Exists(StagePackPath)) { LastError = "levels_v3.json not found at " + StagePackPath; return false; }
                InstallStages(File.ReadAllText(StagePackPath));
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
            string path = StagePackPath;
            string uri = path.Contains("://") ? path : "file://" + path;
            string text;
            using (var req = UnityWebRequest.Get(uri))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    LastError = "could not read levels_v3.json: " + req.error;
                    Debug.LogError("[ShadowSteps] " + LastError);
                    done?.Invoke(false);
                    yield break;
                }
                text = req.downloadHandler.text;
            }
            try
            {
                InstallStages(text);
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

        /// <summary>Parses and structurally validates a schema-3 pack (the solver gates run in tests/tools, not at load).</summary>
        public static void InstallStages(string json)
        {
            var pack = StageJson.Parse(json);
            foreach (var w in pack.Warnings) Debug.LogWarning("[ShadowSteps] levels_v3.json: " + w);
            var report = StageValidator.ValidatePack(pack.Stages, new StageValidationOptions { RunSolver = false });
            foreach (var issue in report.Issues)
                if (issue.Severity == ValidationSeverity.Warning) Debug.LogWarning("[ShadowSteps] " + issue);
            if (!report.IsValid) throw new LevelDataException("levels_v3.json failed validation:\n" + report);
            Stages = new StageCatalog(pack.Stages);
            Debug.Log("[ShadowSteps] loaded " + Stages.Count + " stages (schema " + Stage.SchemaVersion + ")");
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
            Stages = null; SaveRepo = null; Save = null; LastError = null; PendingStageId = 0; PendingMenu = null;
        }
    }
}
