using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.Art2D.Editor
{
    /// <summary>
    /// D-062: bakes the skinned HUD hierarchy into Assets/_ShadowSteps/Art2D/Prefabs/SS2DHud.prefab (the prefab the Game composer
    /// instantiates) so layout, sprites and sizes can be edited on the prefab. At runtime <see cref="SS2DHudCanvas.Build"/> keeps a baked
    /// hierarchy instead of rebuilding it. Re-baking regenerates the prefab from the code layout and discards hand edits.
    /// Rule for designers: keep the component fields on SS2DHudCanvas assigned (caption, title, counters, buttons, pills); object names are free.
    /// </summary>
    public static class SS2DHudBaker
    {
        public const string PrefabPath = SS2DPrototypeSetup.HudPrefabPath;

        [MenuItem("Shadow Steps/2D/Bake HUD prefab (from the code layout + skin)")]
        public static void BakeMenu() { var p = Bake(PrefabPath); if (p != null) Debug.Log("[SS2D] HUD prefab baked -> " + PrefabPath); }

        /// <summary>Batch entry: bake and exit non-zero on failure.</summary>
        public static void BakeBatch()
        {
            var p = Bake(PrefabPath);
            if (p == null) { Debug.LogError("[SS2D] HUD bake failed"); if (Application.isBatchMode) EditorApplication.Exit(1); }
            else Debug.Log("[SS2D] HUD prefab baked");
        }

        /// <summary>Builds the skinned HUD from code with the shipped sprite library and saves it as a prefab at <paramref name="path"/>. Returns null (and logs) when the skin is missing or a generated (unsaveable) sprite slipped in.</summary>
        public static GameObject Bake(string path)
        {
            var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
            if (lib == null || !lib.HasUiSkin) { Debug.LogError("[SS2D] HUD bake needs the sprite library with the HUD skin (Shadow Steps ▸ 2D ▸ Import final art)"); return null; }
            var go = new GameObject("SS2DHud"); var hud = go.AddComponent<SS2DHudCanvas>();
            try
            {
                hud.Library = lib; hud.MarkBaked(false); hud.Build(); hud.MarkBaked(true);
                hud.ShowMessage(string.Empty, 0f); // saved with the empty message pill hidden, as the game shows it
                var problems = new List<string>();
                foreach (var img in go.GetComponentsInChildren<Image>(true)) if (img.sprite != null && !AssetDatabase.Contains(img.sprite)) problems.Add(img.name + " uses a runtime-generated sprite");
                foreach (var t in go.GetComponentsInChildren<Text>(true)) if (t.font != null && !AssetDatabase.Contains(t.font) && t.font.name != "LegacyRuntime" && t.font.name != "Arial") problems.Add(t.name + " uses an unsaved font");
                if (!hud.IsBaked) problems.Add("HUD references incomplete after Build");
                if (problems.Count > 0) { Debug.LogError("[SS2D] HUD bake aborted:\n - " + string.Join("\n - ", problems)); return null; }
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
