using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.Editor
{
    [InitializeOnLoad]
    public static class GameFontLivePreview
    {
        private sealed class Original
        {
            public Font legacy;
            public TMP_FontAsset tmp;
            public Material material;
        }

        private static readonly Dictionary<Component, Original> Originals = new Dictionary<Component, Original>();
        private static TMP_FontAsset tmpFont;
        private static Font legacyFont;
        private static bool running;
        private static double nextScan;

        static GameFontLivePreview()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) Restore();
            };
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
        }

        public static int Apply(TMP_FontAsset tmp, Font legacy)
        {
            if (!EditorApplication.isPlaying) return 0;
            Restore();
            tmpFont = tmp;
            legacyFont = legacy;
            running = tmp != null || legacy != null;
            nextScan = 0;
            return Scan();
        }

        public static void Restore()
        {
            running = false;
            foreach (var pair in Originals)
            {
                if (pair.Key == null) continue;
                var tmp = pair.Key as TMP_Text;
                var text = pair.Key as Text;
                var mesh = pair.Key as TextMesh;
                if (tmp != null)
                {
                    tmp.font = pair.Value.tmp;
                    tmp.fontSharedMaterial = pair.Value.material;
                }
                else if (text != null) text.font = pair.Value.legacy;
                else if (mesh != null)
                {
                    mesh.font = pair.Value.legacy;
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    if (renderer != null) renderer.sharedMaterial = pair.Value.material;
                }
            }
            Originals.Clear();
            tmpFont = null;
            legacyFont = null;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void Tick()
        {
            if (!running || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextScan) return;
            nextScan = EditorApplication.timeSinceStartup + 0.2;
            Scan();
        }

        private static bool IsLive(Component component) => component != null &&
            !EditorUtility.IsPersistent(component) && component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(component.gameObject.scene);

        private static int Scan()
        {
            int changed = 0;
            if (tmpFont != null)
                foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (!IsLive(text) || text.font == tmpFont) continue;
                    if (!Originals.ContainsKey(text)) Originals.Add(text, new Original { tmp = text.font, material = text.fontSharedMaterial });
                    text.font = tmpFont;
                    text.fontSharedMaterial = tmpFont.material;
                    changed++;
                }
            if (legacyFont != null)
            {
                foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
                {
                    if (!IsLive(text) || text.font == legacyFont) continue;
                    if (!Originals.ContainsKey(text)) Originals.Add(text, new Original { legacy = text.font });
                    text.font = legacyFont;
                    changed++;
                }
                foreach (var text in Resources.FindObjectsOfTypeAll<TextMesh>())
                {
                    if (!IsLive(text) || text.font == legacyFont) continue;
                    var renderer = text.GetComponent<MeshRenderer>();
                    if (!Originals.ContainsKey(text)) Originals.Add(text, new Original { legacy = text.font, material = renderer != null ? renderer.sharedMaterial : null });
                    text.font = legacyFont;
                    if (renderer != null) renderer.sharedMaterial = legacyFont.material;
                    changed++;
                }
            }
            if (changed > 0) EditorApplication.QueuePlayerLoopUpdate();
            return changed;
        }
    }
}
