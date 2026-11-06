using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowSteps.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace ShadowSteps.Editor
{
    public sealed class GameFontUpdaterWindow : EditorWindow
    {
        [SerializeField] private TMP_FontAsset tmpFont;
        [SerializeField] private Font legacyFont;
        [SerializeField] private DefaultAsset folder;
        [SerializeField] private bool updateTmpDefault = true;
        [SerializeField] private string lastBackup;
        [SerializeField] private bool livePreview = true;
        [SerializeField] private bool initialized;
        private TextField report;
        private const string PendingSaveKey = "ShadowSteps.GameFonts.PendingSave";

        [Serializable]
        private sealed class PendingSave
        {
            public string tmpPath;
            public string legacyPath;
            public string folderPath;
            public bool updateDefault;
        }

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeEvents()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode && state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.delayCall += () =>
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<GameFontUpdaterWindow>()) window.CreateGUI();
                if (state == PlayModeStateChange.EnteredEditMode) SaveQueuedFonts();
            };
        }

        private static void SaveQueuedFonts()
        {
            string json = SessionState.GetString(PendingSaveKey, "");
            if (string.IsNullOrEmpty(json) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var pending = JsonUtility.FromJson<PendingSave>(json);
            var window = GetWindow<GameFontUpdaterWindow>("Game Fonts");
            window.tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(pending.tmpPath);
            window.legacyFont = AssetDatabase.LoadAssetAtPath<Font>(pending.legacyPath);
            window.folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(pending.folderPath);
            window.updateTmpDefault = pending.updateDefault;
            window.initialized = true;
            window.CreateGUI();
            if ((!string.IsNullOrEmpty(pending.tmpPath) && window.tmpFont == null) ||
                (!string.IsNullOrEmpty(pending.legacyPath) && window.legacyFont == null))
            {
                window.report.value = "Queued save stopped: a selected font asset is missing. Choose the fonts again and click Update All Game Fonts.";
                return;
            }
            window.Run(true);
        }

        [MenuItem("Shadow Steps/UI/Update All Game Fonts")]
        public static void Open() => GetWindow<GameFontUpdaterWindow>("Game Fonts");

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            minSize = new Vector2(480, 410);
            var settings = GameFontSettings.Current;
            if (!initialized && settings != null) { tmpFont = settings.tmpFont; legacyFont = settings.legacyFont; }
            initialized = true;
            if (folder == null) folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/_ShadowSteps");

            rootVisualElement.Add(new HelpBox(
                "In Play mode, change fonts to preview the running game immediately. Includes inactive and newly spawned text. " +
                "Save After Play Mode keeps your choice when you stop playing, with a backup. In Edit mode, includes saved scenes and prefabs, " +
                "plus code-built menus and HUDs. Sizes, colours and layout stay the same. " +
                "TMP text switches to the new font's default material; custom outline/glow presets need reassignment.",
                HelpBoxMessageType.Info));
            var tmpField = new ObjectField("TextMeshPro font") { objectType = typeof(TMP_FontAsset), allowSceneObjects = false, value = tmpFont };
            var legacyField = new ObjectField("Standard / HUD font") { objectType = typeof(Font), allowSceneObjects = false, value = legacyFont };
            tmpField.RegisterValueChangedCallback(e =>
            {
                tmpFont = e.newValue as TMP_FontAsset;
                if (tmpFont != null && tmpFont.sourceFontFile != null)
                {
                    legacyFont = tmpFont.sourceFontFile;
                    legacyField.SetValueWithoutNotify(legacyFont);
                }
                RefreshLivePreview();
            });
            legacyField.RegisterValueChangedCallback(e => { legacyFont = e.newValue as Font; RefreshLivePreview(); });
            rootVisualElement.Add(tmpField);
            rootVisualElement.Add(legacyField);
            rootVisualElement.Add(new HelpBox("Leave either font empty to keep that text type unchanged. Assign both to cover menus and HUD.", HelpBoxMessageType.Info));
            var folderField = new ObjectField("Scene / prefab folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = folder };
            folderField.RegisterValueChangedCallback(e => folder = e.newValue as DefaultAsset);
            rootVisualElement.Add(folderField);
            rootVisualElement.Add(new HelpBox("The folder limits saved scene/prefab edits. Runtime game fonts and the optional TMP default apply throughout the game.", HelpBoxMessageType.Info));
            var defaultToggle = new UnityEngine.UIElements.Toggle("Also set TextMeshPro default") { value = updateTmpDefault };
            defaultToggle.RegisterValueChangedCallback(e => updateTmpDefault = e.newValue);
            rootVisualElement.Add(defaultToggle);
            var liveToggle = new UnityEngine.UIElements.Toggle("Live preview when changing fonts in Play mode") { value = livePreview };
            liveToggle.RegisterValueChangedCallback(e =>
            {
                livePreview = e.newValue;
                if (livePreview) RefreshLivePreview(); else GameFontLivePreview.Restore();
            });
            rootVisualElement.Add(liveToggle);
            rootVisualElement.Add(new UnityEngine.UIElements.Button(() => Run(false)) { text = EditorApplication.isPlaying ? "Apply to Running Game" : "Preview Changes" });
            rootVisualElement.Add(new UnityEngine.UIElements.Button(() => Run(true)) { text = EditorApplication.isPlaying ? "Save Fonts After Play Mode (with backup)" : "Update All Game Fonts (with backup)" });
            rootVisualElement.Add(new UnityEngine.UIElements.Button(() =>
            {
                GameFontLivePreview.Restore();
                SessionState.EraseString(PendingSaveKey);
                report.value = "Live preview restored. Pending save cancelled.";
            }) { text = "Reset Live Preview / Cancel Pending Save" });
            rootVisualElement.Add(new UnityEngine.UIElements.Button(() =>
            {
                if (!string.IsNullOrEmpty(lastBackup) && Directory.Exists(lastBackup)) EditorUtility.RevealInFinder(lastBackup);
            }) { text = "Show Last Backup" });
            report = new TextField { multiline = true, isReadOnly = true, value =
                string.IsNullOrEmpty(SessionState.GetString(PendingSaveKey, "")) ? "Ready. Play mode previews are temporary until you choose Save Fonts After Play Mode." :
                "Font save pending. Stop Play mode to save; if scenes need saving, save them and click Update All Game Fonts." };
            rootVisualElement.Add(report);
        }

        private void RefreshLivePreview()
        {
            if (!livePreview || !EditorApplication.isPlaying) return;
            if (tmpFont == null && legacyFont == null)
            {
                GameFontLivePreview.Restore();
                report.value = "Live preview restored. Select a font to preview again.";
                return;
            }
            Run(false);
        }

        private void ValidateFonts()
        {
            if (tmpFont == null && legacyFont == null)
                throw new InvalidOperationException("Choose at least one replacement font.");
            if (tmpFont != null && tmpFont.material == null)
                throw new InvalidOperationException("The TextMeshPro font needs a valid material. Select a generated TMP font asset.");
            if ((tmpFont != null && !EditorUtility.IsPersistent(tmpFont)) || (legacyFont != null && !EditorUtility.IsPersistent(legacyFont)))
                throw new InvalidOperationException("Choose saved font assets from the Project window.");
        }

        private void Run(bool apply)
        {
            try
            {
                if (EditorApplication.isCompiling)
                    throw new InvalidOperationException("Wait for compilation first.");
                if (EditorApplication.isPlaying)
                {
                    ValidateFonts();
                    int count = GameFontLivePreview.Apply(tmpFont, legacyFont);
                    report.value = "Live preview active: updated " + count + " text component(s). Newly spawned text also follows this preview.\nNo scene or prefab files saved.";
                    if (apply)
                    {
                        string scopePath = AssetDatabase.GetAssetPath(folder);
                        if (!AssetDatabase.IsValidFolder(scopePath) || (scopePath != "Assets" && !scopePath.StartsWith("Assets/", StringComparison.Ordinal)))
                            throw new InvalidOperationException("Select a folder under Assets for the saved update.");
                        SessionState.SetString(PendingSaveKey, JsonUtility.ToJson(new PendingSave
                        {
                            tmpPath = AssetDatabase.GetAssetPath(tmpFont), legacyPath = AssetDatabase.GetAssetPath(legacyFont),
                            folderPath = scopePath, updateDefault = updateTmpDefault
                        }));
                        report.value += "\nPermanent update queued for when you stop Play mode. Your selected fonts will then be saved with a backup.";
                    }
                    else if (!string.IsNullOrEmpty(SessionState.GetString(PendingSaveKey, "")))
                        report.value += "\nAn earlier font choice is queued for saving. Click Save Fonts After Play Mode again to save this choice instead.";
                    return;
                }
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Wait for Play mode to finish switching.");
                if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                    throw new InvalidOperationException("Close Prefab Mode first.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Save your open scenes first, then run the tool again.");
                string scope = AssetDatabase.GetAssetPath(folder);
                if (string.IsNullOrEmpty(scope) || !AssetDatabase.IsValidFolder(scope) || (scope != "Assets" && !scope.StartsWith("Assets/", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Select a folder under Assets.");
                ValidateFonts();

                var prefabs = FindPaths("t:Prefab", scope).Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).ToArray();
                var scenes = FindPaths("t:Scene", scope);
                var backup = apply ? Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "Backups", "Fonts", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6)) : null;
                // Back up the complete candidate set before the first write, including nested prefab sources in scope.
                if (apply)
                {
                    var paths = prefabs.Concat(scenes).Concat(new[] { GameFontSettings.AssetPath });
                    if (tmpFont != null && updateTmpDefault && TMP_Settings.instance != null)
                        paths = paths.Concat(new[] { AssetDatabase.GetAssetPath(TMP_Settings.instance) });
                    foreach (var path in paths.Distinct()) BackupFile(path, backup);
                    lastBackup = backup;
                    Directory.CreateDirectory(backup);
                    File.WriteAllText(Path.Combine(backup, "README.txt"),
                        "Original files before Update All Game Fonts. Copy the Assets folder back into the project to restore.\n" +
                        "If GameFontSettings.asset is absent here, it was newly created: remove it and its .meta when restoring.\n");
                }

                int textCount = 0, assetCount = 0;
                var lines = new List<string>();
                foreach (string path in prefabs)
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        int count = UpdateHierarchy(root, tmpFont, legacyFont, apply);
                        if (count == 0) continue;
                        if (apply)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                            if (!success) throw new IOException("Could not save " + path);
                        }
                        textCount += count; assetCount++;
                        lines.Add(count + " text(s): " + path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                var active = SceneManager.GetActiveScene();
                try
                {
                    foreach (string path in scenes)
                    {
                        var scene = SceneManager.GetSceneByPath(path);
                        bool opened = !scene.IsValid() || !scene.isLoaded;
                        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        try
                        {
                            int count = 0;
                            foreach (var root in scene.GetRootGameObjects()) count += UpdateHierarchy(root, tmpFont, legacyFont, apply);
                            if (count == 0) continue;
                            if (apply && !EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                            textCount += count; assetCount++;
                            lines.Add(count + " text(s): " + path);
                        }
                        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
                    }
                }
                finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }

                if (apply)
                {
                    var settings = AssetDatabase.LoadAssetAtPath<GameFontSettings>(GameFontSettings.AssetPath);
                    if (settings == null)
                    {
                        Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                            Path.GetDirectoryName(GameFontSettings.AssetPath)));
                        AssetDatabase.Refresh();
                        settings = CreateInstance<GameFontSettings>();
                        AssetDatabase.CreateAsset(settings, GameFontSettings.AssetPath);
                    }
                    if (tmpFont != null) settings.tmpFont = tmpFont;
                    if (legacyFont != null) settings.legacyFont = legacyFont;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                    if (tmpFont != null && updateTmpDefault && TMP_Settings.instance != null)
                    {
                        var serialized = new SerializedObject(TMP_Settings.instance);
                        var property = serialized.FindProperty("m_defaultFontAsset");
                        if (property == null) throw new InvalidOperationException("TMP Settings has no default font property.");
                        property.objectReferenceValue = tmpFont;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        AssetDatabase.SaveAssetIfDirty(TMP_Settings.instance);
                    }
                }
                string summary = (apply ? "Updated " : "Would update ") + textCount + " text component(s) in " + assetCount + " saved asset(s).\n" +
                    "Runtime menus and HUD: " + (apply ? "settings saved." : "will use the selected fonts.") +
                    (apply ? "\nBackup: " + backup : "\nPreview only; nothing saved.");
                report.value = summary + "\n\n" + string.Join("\n", lines);
                if (apply) SessionState.EraseString(PendingSaveKey);
                Debug.Log("[Game Fonts] " + summary);
            }
            catch (Exception e)
            {
                report.value = "Stopped: " + e.Message + (apply && !string.IsNullOrEmpty(lastBackup) ? "\nBackup: " + lastBackup : "");
                if (!string.IsNullOrEmpty(SessionState.GetString(PendingSaveKey, "")))
                    report.value += "\nThe pending save is retained. Resolve the issue and click Update All Game Fonts, or cancel it.";
                if (!(e is InvalidOperationException)) Debug.LogException(e);
            }
        }

        private static string[] FindPaths(string filter, string scope) => AssetDatabase.FindAssets(filter, new[] { scope })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();

        private static void BackupFile(string path, string destination)
        {
            foreach (string suffix in new[] { "", ".meta" })
            {
                string source = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path + suffix);
                if (!File.Exists(source)) continue;
                string target = Path.Combine(destination, path + suffix);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, false);
            }
        }

        public static int UpdateHierarchy(GameObject root, TMP_FontAsset tmp, Font legacy, bool apply)
        {
            int count = 0;
            if (tmp != null)
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.font == tmp) continue;
                    count++;
                    if (!apply) continue;
                    text.font = tmp;
                    text.fontSharedMaterial = tmp.material;
                    EditorUtility.SetDirty(text);
                    if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                }
            if (legacy != null)
            {
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    if (text.font == legacy) continue;
                    count++;
                    if (!apply) continue;
                    text.font = legacy;
                    EditorUtility.SetDirty(text);
                    if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                }
                foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
                {
                    if (text.font == legacy) continue;
                    count++;
                    if (!apply) continue;
                    text.font = legacy;
                    var renderer = text.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = legacy.material;
                        EditorUtility.SetDirty(renderer);
                        if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    EditorUtility.SetDirty(text);
                    if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                }
            }
            return count;
        }
    }
}
