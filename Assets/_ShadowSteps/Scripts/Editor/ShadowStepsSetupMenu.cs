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
        public const string HomeScenePath = Root + "/Scenes/Home.unity";
        public const string BootstrapScenePath = Root + "/Scenes/Bootstrap.unity";
        public const string PrefabSetPath = Root + "/ScriptableObjects/UiPrefabSet.asset";

        [MenuItem("Shadow Steps/Setup/Generate All (theme, HUD prefab, Game scene)")]
        public static void GenerateAll()
        {
            CreateTheme();
            CreateHudPrefab();
            CreateScreenPrefabs();
            CreateGameScene();
            CreateHomeScene();
            CreateBootstrapScene();
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

        [MenuItem("Shadow Steps/Setup/Create screen prefabs + UiPrefabSet")]
        public static UiPrefabSet CreateScreenPrefabs()
        {
            EnsureFolder(Path.GetDirectoryName(PrefabSetPath));
            var set = AssetDatabase.LoadAssetAtPath<UiPrefabSet>(PrefabSetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<UiPrefabSet>(); AssetDatabase.CreateAsset(set, PrefabSetPath); }
            set.GameHud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            set.HomeScreen = SaveScreenPrefab<HomeScreen>();
            set.LevelSelectScreen = SaveScreenPrefab<LevelSelectScreen>();
            set.SettingsScreen = SaveScreenPrefab<SettingsScreen>();
            set.PauseScreen = SaveScreenPrefab<PauseScreen>();
            set.ResultsScreen = SaveScreenPrefab<ResultsScreen>();
            set.FailureScreen = SaveScreenPrefab<FailureScreen>();
            set.OnboardingScreen = SaveScreenPrefab<OnboardingScreen>();
            set.AlarmPopupScreen = SaveScreenPrefab<AlarmPopupScreen>();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        private static GameObject SaveScreenPrefab<T>() where T : UIScreen
        {
            string path = Root + "/Prefabs/UI/" + typeof(T).Name + ".prefab";
            var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var screen = go.AddComponent<T>();
            screen.BuildHierarchy();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[ShadowSteps] wrote " + path);
            return prefab;
        }

        [MenuItem("Shadow Steps/Setup/Create Home scene")]
        public static void CreateHomeScene()
        {
            EnsureFolder(Path.GetDirectoryName(HomeScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera();
            var go = new GameObject("MenuSceneComposer");
            go.AddComponent<MenuSceneComposer>().Prefabs = AssetDatabase.LoadAssetAtPath<UiPrefabSet>(PrefabSetPath);
            EditorSceneManager.SaveScene(scene, HomeScenePath);
        }

        [MenuItem("Shadow Steps/Setup/Create Bootstrap scene")]
        public static void CreateBootstrapScene()
        {
            EnsureFolder(Path.GetDirectoryName(BootstrapScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera();
            new GameObject("Bootstrap").AddComponent<BootstrapController>();
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static Camera MakeCamera()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
            camGo.AddComponent<AudioListener>();
            return cam;
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
            composer.UiPrefabs = AssetDatabase.LoadAssetAtPath<UiPrefabSet>(PrefabSetPath);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log("[ShadowSteps] wrote " + GameScenePath);
        }

        [MenuItem("Shadow Steps/Setup/Add scenes to Build Settings")]
        public static void AddScenesToBuildSettings()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var path in new[] { BootstrapScenePath, HomeScenePath, GameScenePath })
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
