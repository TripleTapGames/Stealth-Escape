using System.IO;
using ShadowSteps.Runtime;
using ShadowSteps.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowSteps.Editor
{
    /// <summary>
    /// One-click project setup so nothing depends on hand-built editor state:
    /// theme asset, HUD prefab, Game scene with the composer, build settings. All steps are idempotent.
    /// Also callable headless: <c>Unity -batchmode -executeMethod ShadowSteps.Editor.ShadowStepsSetupMenu.GenerateAll</c>.
    /// </summary>
    public static class ShadowStepsSetupMenu
    {
        public const string Root = "Assets/_ShadowSteps";
        public const string ThemePath = Root + "/ScriptableObjects/BoardTheme.asset";
        public const string HudPrefabPath = Root + "/Prefabs/UI/GameHud.prefab";
        public const string GameScenePath = Root + "/Scenes/Game.unity";

        [MenuItem("Shadow Steps/Setup/Generate All (theme, HUD prefab, Game scene)")]
        public static void GenerateAll()
        {
            CreateTheme();
            CreateHudPrefab();
            CreateGameScene();
            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[ShadowSteps] setup complete");
        }

        [MenuItem("Shadow Steps/Setup/Create Board Theme asset")]
        public static BoardTheme CreateTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BoardTheme>(ThemePath);
            if (existing != null) return existing;
            EnsureFolder(Path.GetDirectoryName(ThemePath));
            var theme = ScriptableObject.CreateInstance<BoardTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
            Debug.Log("[ShadowSteps] created " + ThemePath);
            return theme;
        }

        [MenuItem("Shadow Steps/Setup/Create Game HUD prefab")]
        public static GameObject CreateHudPrefab()
        {
            EnsureFolder(Path.GetDirectoryName(HudPrefabPath));
            var canvas = UiBuilder.CreateCanvas("GameHud", 10);
            var hud = canvas.gameObject.AddComponent<GameHudController>();
            hud.BuildHierarchy();
            var prefab = PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, HudPrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
            Debug.Log("[ShadowSteps] wrote " + HudPrefabPath);
            return prefab;
        }

        [MenuItem("Shadow Steps/Setup/Create Game scene")]
        public static void CreateGameScene()
        {
            EnsureFolder(Path.GetDirectoryName(GameScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            var composerGo = new GameObject("GameSceneComposer");
            var composer = composerGo.AddComponent<GameSceneComposer>();
            composer.Theme = AssetDatabase.LoadAssetAtPath<BoardTheme>(ThemePath);
            composer.HudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log("[ShadowSteps] wrote " + GameScenePath);
        }

        [MenuItem("Shadow Steps/Setup/Add scenes to Build Settings")]
        public static void AddScenesToBuildSettings()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var path in new[] { GameScenePath })
                if (File.Exists(path)) list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        [MenuItem("Shadow Steps/Setup/Import TextMeshPro essentials")]
        public static void ImportTmpEssentials()
        {
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        [MenuItem("Shadow Steps/Open Game scene")]
        public static void OpenGameScene()
        {
            if (File.Exists(GameScenePath)) EditorSceneManager.OpenScene(GameScenePath);
            else Debug.LogWarning("Game scene missing; run Shadow Steps/Setup/Generate All");
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
