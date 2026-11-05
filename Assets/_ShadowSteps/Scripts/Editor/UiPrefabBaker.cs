using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.UI.Editor
{
    /// <summary>
    /// D-056: bakes every code-built screen (with the current UI skin) into a prefab under Assets/_ShadowSteps/Prefabs/UI and records them in the
    /// UiPrefabSet asset in Resources, which the Home and Game scenes pick up automatically. Re-run after changing a screen's code or the skin;
    /// hand edits made in the prefabs survive until the next bake, so bake deliberately. The HUD stays code-built (it draws generated shapes).
    /// </summary>
    public static class UiPrefabBaker
    {
        public const string PrefabDir = "Assets/_ShadowSteps/Prefabs/UI/";

        [MenuItem("Shadow Steps/UI/Bake UI prefabs (screens → Prefabs/UI + Resources/UiPrefabSet)")]
        public static void BakeMenu() { var set = Bake(PrefabDir, UiPrefabSet.DefaultAssetPath); Debug.Log("[UI] baked " + Count(set) + " screen prefabs -> " + PrefabDir); }

        /// <summary>Batch entry: bake and exit non-zero on failure.</summary>
        public static void BakeBatch()
        {
            try { var set = Bake(PrefabDir, UiPrefabSet.DefaultAssetPath); Debug.Log("[UI] baked " + Count(set) + " screen prefabs"); if (Count(set) < 9) throw new Exception("not every screen baked"); }
            catch (Exception e) { Debug.LogError("[UI] bake failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        public static int Count(UiPrefabSet set)
        {
            if (set == null) return 0; int n = 0;
            foreach (var p in new[] { set.HomeScreen, set.LevelSelectScreen, set.SettingsScreen, set.PauseScreen, set.ResultsScreen, set.FailureScreen, set.OnboardingScreen, set.AlarmPopupScreen, set.GuardIntroScreen }) if (p != null) n++;
            return n;
        }

        /// <summary>Bakes into <paramref name="prefabDir"/> and writes/updates the set asset at <paramref name="setPath"/>. Returns the set.</summary>
        public static UiPrefabSet Bake(string prefabDir, string setPath)
        {
            Directory.CreateDirectory(prefabDir); Directory.CreateDirectory(Path.GetDirectoryName(setPath));
            var set = AssetDatabase.LoadAssetAtPath<UiPrefabSet>(setPath);
            if (set == null) { set = ScriptableObject.CreateInstance<UiPrefabSet>(); AssetDatabase.CreateAsset(set, setPath); }
            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(UiSkin.DefaultAssetPath); if (skin != null) UiSkin.Current = skin; // bake with the final art when it is imported
            var stage = new GameObject("UiBakeStage", typeof(RectTransform));
            try
            {
                var fresh = ScriptableObject.CreateInstance<UiPrefabSet>(); // always build from code, never from an older prefab
                set.HomeScreen = BakeOne<HomeScreen>(fresh, stage.transform, prefabDir);
                set.LevelSelectScreen = BakeOne<LevelSelectScreen>(fresh, stage.transform, prefabDir);
                set.SettingsScreen = BakeOne<SettingsScreen>(fresh, stage.transform, prefabDir);
                set.PauseScreen = BakeOne<PauseScreen>(fresh, stage.transform, prefabDir);
                set.ResultsScreen = BakeOne<ResultsScreen>(fresh, stage.transform, prefabDir);
                set.FailureScreen = BakeOne<FailureScreen>(fresh, stage.transform, prefabDir);
                set.OnboardingScreen = BakeOne<OnboardingScreen>(fresh, stage.transform, prefabDir);
                set.AlarmPopupScreen = BakeOne<AlarmPopupScreen>(fresh, stage.transform, prefabDir);
                set.GuardIntroScreen = BakeOne<GuardIntroScreen>(fresh, stage.transform, prefabDir);
                UnityEngine.Object.DestroyImmediate(fresh);
            }
            finally { UnityEngine.Object.DestroyImmediate(stage); }
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            return set;
        }

        static GameObject BakeOne<T>(UiPrefabSet builder, Transform stage, string dir) where T : UIScreen
        {
            var screen = builder.Create<T>(stage, null); // code-built hierarchy with serialized field references
            screen.gameObject.name = typeof(T).Name;
            string path = dir + typeof(T).Name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(screen.gameObject, path, out bool ok);
            if (!ok || prefab == null) throw new Exception("could not save " + path);
            UnityEngine.Object.DestroyImmediate(screen.gameObject);
            return prefab;
        }
    }
}
