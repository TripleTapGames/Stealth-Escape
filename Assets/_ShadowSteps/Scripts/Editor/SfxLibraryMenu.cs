using ShadowSteps.Runtime;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Editor
{
    /// <summary>D-069: creates/refreshes the SfxLibrary asset in Resources with one slot per sound so clips can be assigned in the inspector.</summary>
    public static class SfxLibraryMenu
    {
        [MenuItem("Shadow Steps/Audio/Create or refresh SFX library (Resources/SfxLibrary)")]
        public static void CreateMenu() { var lib = CreateOrRefresh(); Selection.activeObject = lib; Debug.Log("[Audio] SFX library ready with " + lib.entries.Length + " slots -> " + SfxLibrary.DefaultAssetPath); }

        public static void CreateBatch() { var lib = CreateOrRefresh(); Debug.Log("[Audio] SFX library " + lib.entries.Length + " slots"); }

        /// <summary>Folder of the project's own synthesised clips (D-070), one per sound: sfx_&lt;sound&gt;.wav.</summary>
        public const string GeneratedDir = "Assets/_ShadowSteps/Audio/Sfx/";

        [MenuItem("Shadow Steps/Audio/Assign generated clips to empty slots")]
        public static void AssignGeneratedMenu() { int n = AssignGenerated(); Debug.Log("[Audio] assigned " + n + " generated clips"); }

        public static void AssignGeneratedBatch()
        {
            int n = AssignGenerated();
            if (n < System.Enum.GetValues(typeof(Sfx)).Length) { Debug.LogError("[Audio] only " + n + " clips assigned"); if (Application.isBatchMode) EditorApplication.Exit(1); }
            else Debug.Log("[Audio] all " + n + " slots have clips");
        }

        /// <summary>
        /// Points every slot that is empty, or that already holds a clip from <see cref="GeneratedDir"/>, at the project's synthesised clip
        /// for that sound (import: mono, decompress on load, no 3D). Slots holding clips from anywhere else (hand-picked recordings) are kept.
        /// Returns the number of slots that hold a clip afterwards.
        /// </summary>
        public static int AssignGenerated()
        {
            var lib = CreateOrRefresh(); int filled = 0;
            foreach (var e in lib.entries)
            {
                string path = GeneratedDir + "sfx_" + e.sound.ToString().ToLowerInvariant() + ".wav";
                bool ours = e.clip != null && AssetDatabase.GetAssetPath(e.clip).StartsWith(GeneratedDir);
                if (e.clip == null || ours)
                {
                    if (System.IO.File.Exists(path))
                    {
                        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                        if (imp != null)
                        {
                            var st = imp.defaultSampleSettings; bool changed = !imp.forceToMono || st.loadType != AudioClipLoadType.DecompressOnLoad || st.compressionFormat != AudioCompressionFormat.PCM;
                            if (changed) { imp.forceToMono = true; st.loadType = AudioClipLoadType.DecompressOnLoad; st.compressionFormat = AudioCompressionFormat.PCM; imp.defaultSampleSettings = st; imp.SaveAndReimport(); }
                        }
                        e.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    }
                }
                if (e.clip != null) filled++;
            }
            EditorUtility.SetDirty(lib); AssetDatabase.SaveAssets(); SfxLibrary.Current = null;
            return filled;
        }

        public static SfxLibrary CreateOrRefresh()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(SfxLibrary.DefaultAssetPath);
            if (lib == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SfxLibrary.DefaultAssetPath));
                lib = ScriptableObject.CreateInstance<SfxLibrary>(); AssetDatabase.CreateAsset(lib, SfxLibrary.DefaultAssetPath);
            }
            lib.EnsureAllEntries();
            EditorUtility.SetDirty(lib); AssetDatabase.SaveAssets();
            SfxLibrary.Current = null;
            return lib;
        }
    }
}
