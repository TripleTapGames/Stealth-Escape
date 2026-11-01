using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowSteps.Art3D.Editor
{
    /// <summary>
    /// Headless rendering for art review without the Game view: renders a preview scene's main camera to a PNG at a
    /// portrait resolution, or renders a list of prefabs into a contact sheet with a bounds report. Both run from the
    /// command line (-batchmode without -nographics) or from the Shadow Steps ▸ 3D ▸ Render menu inside the editor.
    ///
    ///   Unity -batchmode -projectPath . -executeMethod ShadowSteps.Art3D.Editor.SS3DBatchRender.RenderScene
    ///         -ssScene Assets/.../Scene.unity -ssOut Docs/Reports/Renders/scene.png [-ssWidth 1080 -ssHeight 1920] [-ssGuides] -quit
    ///   Unity -batchmode -projectPath . -executeMethod ShadowSteps.Art3D.Editor.SS3DBatchRender.PrefabSheet
    ///         -ssList Docs/Reports/Renders/list.txt -ssOut Docs/Reports/Renders/sheet.png [-ssCell 220 -ssCols 8] -quit
    ///
    /// Visual review only: nothing here changes assets, scenes or gameplay data.
    /// </summary>
    public static class SS3DBatchRender
    {
        private static string Arg(string name, string fallback = null)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }
        private static bool Flag(string name) { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == name) return true; return false; }
        private static int ArgInt(string name, int fallback) => int.TryParse(Arg(name), out int v) ? v : fallback;

        public static void RenderScene()
        {
            string scene = Arg("-ssScene"), outPath = Arg("-ssOut", "Docs/Reports/Renders/scene.png");
            int w = ArgInt("-ssWidth", 1080), h = ArgInt("-ssHeight", 1920);
            try
            {
                RenderSceneTo(scene, outPath, w, h, Flag("-ssGuides"));
            }
            catch (System.Exception e) { Debug.LogError("[SS3D] render failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        /// <summary>Opens the scene, rebuilds its preview (so the frame reflects the current scripts), fits the camera to the output aspect and writes the PNG.</summary>
        public static string RenderSceneTo(string scenePath, string outPath, int width, int height, bool guides)
        {
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath)) throw new FileNotFoundException("scene not found: " + scenePath);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var preview = Object.FindObjectOfType<SS3DPreviewController>();
            if (preview != null) preview.BuildPreview();
            var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (cam == null) throw new System.InvalidOperationException("no camera in " + scenePath);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;
            var fitter = cam.GetComponent<SS3DBoardCameraFitter>();
            if (fitter != null && fitter.board != null && fitter.board.IsBuilt) fitter.Fit(fitter.board.BoardBounds());
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = preview == null || preview.palette == null || preview.palette.postProcessing;
            cam.Render();
            var tex = ReadBack(rt);
            cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
            var composer = Object.FindObjectOfType<TownPlazaComposer>();
            if (guides) DrawGuides(tex, fitter, composer != null && composer.layout != null ? composer.layout.hudTop : -1f, composer != null && composer.layout != null ? composer.layout.hudBottom : -1f);
            Save(tex, outPath);
            Debug.Log("[SS3D] rendered " + scenePath + " -> " + outPath + " (" + width + "x" + height + ")");
            return outPath;
        }

        /// <summary>HUD bands (top/bottom reserved fractions) and a centre line, drawn over the frame so the composition can be judged against the phone layout.</summary>
        private static void DrawGuides(Texture2D tex, SS3DBoardCameraFitter fitter, float hudTop = -1f, float hudBottom = -1f)
        {
            float top = hudTop >= 0f ? hudTop : fitter != null ? fitter.topReservedFraction : 0.14f, bottom = hudBottom >= 0f ? hudBottom : fitter != null ? fitter.bottomReservedFraction : 0.22f;
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            int topY = Mathf.RoundToInt(h * (1f - top)), bottomY = Mathf.RoundToInt(h * bottom);
            for (int y = 0; y < h; y++)
            {
                bool band = y >= topY || y < bottomY;
                bool line = Mathf.Abs(y - topY) <= 1 || Mathf.Abs(y - bottomY) <= 1 || Mathf.Abs(y - h / 2) <= 0;
                if (!band && !line) continue;
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    if (line) px[y * w + x] = new Color32(255, 40, 40, 255);
                    else px[y * w + x] = new Color32((byte)(c.r * 0.55f), (byte)(c.g * 0.55f), (byte)(c.b * 0.55f), 255);
                }
            }
            for (int y = 0; y < h; y++) px[y * w + w / 2] = new Color32(255, 40, 40, 255);
            tex.SetPixels32(px); tex.Apply();
        }

        public static void PrefabSheet()
        {
            string list = Arg("-ssList"), outPath = Arg("-ssOut", "Docs/Reports/Renders/sheet.png");
            int cell = ArgInt("-ssCell", 220), cols = ArgInt("-ssCols", 8);
            try
            {
                var paths = new List<string>();
                foreach (var line in File.ReadAllLines(list)) if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#")) paths.Add(line.Trim());
                PrefabSheetTo(paths, outPath, cell, cols);
            }
            catch (System.Exception e) { Debug.LogError("[SS3D] prefab sheet failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        [System.Serializable] private class PrefabInfo { public int index; public string path, name; public Vector3 size; public int renderers, materials; public string shader; }
        [System.Serializable] private class SheetReport { public int cell, cols; public List<PrefabInfo> prefabs = new List<PrefabInfo>(); }

        /// <summary>Each prefab rendered from a 3/4 view (yaw 35°, pitch 28°) into one cell, row-major; a JSON next to the PNG lists index, name, world size and material state.</summary>
        public static string PrefabSheetTo(List<string> prefabPaths, string outPath, int cell, int cols)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1f, 0.96f, 0.9f); sun.intensity = 1.4f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.8f, 0.85f, 0.9f); RenderSettings.ambientEquatorColor = new Color(0.8f, 0.78f, 0.74f); RenderSettings.ambientGroundColor = new Color(0.5f, 0.5f, 0.48f);
            var camObj = new GameObject("SheetCam"); var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.93f, 0.93f, 0.91f);
            cam.nearClipPlane = 0.05f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Floor"; floor.transform.localScale = Vector3.one * 100f;
            floor.GetComponent<Renderer>().sharedMaterial = SS3DLevelRenderer.MakeLitMaterial("Sheet Floor", new Color(0.86f, 0.86f, 0.84f));
            int rows = Mathf.CeilToInt(prefabPaths.Count / (float)cols);
            var sheet = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false);
            var fill = new Color32[cols * cell * rows * cell]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(40, 40, 40, 255);
            sheet.SetPixels32(fill);
            var rt = new RenderTexture(cell, cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.aspect = 1f;
            var report = new SheetReport { cell = cell, cols = cols };
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                var info = new PrefabInfo { index = i, path = prefabPaths[i], name = Path.GetFileNameWithoutExtension(prefabPaths[i]) };
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
                GameObject inst = null;
                if (prefab != null)
                {
                    inst = Object.Instantiate(prefab);
                    foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    if (SS3DKitFit.TryBounds(inst, out var b))
                    {
                        info.size = b.size;
                        var rs = inst.GetComponentsInChildren<Renderer>(true); info.renderers = rs.Length;
                        var mats = new HashSet<Material>(); foreach (var r in rs) foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
                        info.materials = mats.Count; foreach (var m in mats) { info.shader = m.shader != null ? m.shader.name : "null"; break; }
                        inst.transform.position = new Vector3(-b.center.x, -b.min.y, -b.center.z); // bounds centre on the origin, bottom on the floor
                        SS3DKitFit.TryBounds(inst, out b);
                        float radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 1.15f + 0.05f;
                        var rot = Quaternion.Euler(28f, 35f, 0f);
                        cam.transform.rotation = rot;
                        cam.transform.position = b.center - rot * Vector3.forward * (radius * 4f);
                        cam.orthographicSize = radius; cam.farClipPlane = radius * 10f;
                        cam.Render();
                        var tex = ReadBack(rt);
                        int cx = (i % cols) * cell, cy = (rows - 1 - i / cols) * cell; // row 0 at the top of the image
                        sheet.SetPixels32(cx, cy, cell, cell, tex.GetPixels32());
                        Object.DestroyImmediate(tex);
                    }
                    Object.DestroyImmediate(inst);
                }
                else info.shader = "PREFAB NOT FOUND";
                report.prefabs.Add(info);
            }
            sheet.Apply();
            cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
            Save(sheet, outPath);
            File.WriteAllText(Path.ChangeExtension(outPath, ".json"), JsonUtility.ToJson(report, true));
            Debug.Log("[SS3D] prefab sheet: " + prefabPaths.Count + " prefabs -> " + outPath + " (+ .json)");
            return outPath;
        }

        private static Texture2D ReadBack(RenderTexture rt)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        private static void Save(Texture2D tex, string outPath)
        {
            string full = Path.IsPathRooted(outPath) ? outPath : Path.Combine(Path.GetDirectoryName(Application.dataPath), outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // ---- in-editor entry points (same code, so the user can produce the identical frame from the menu)

        [MenuItem("Shadow Steps/3D/Render/Render open scene to PNG (1080x1920, with HUD guides)")]
        public static void RenderOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) { Debug.LogError("[SS3D] save the scene first"); return; }
            string outPath = "Docs/Reports/Renders/" + Path.GetFileNameWithoutExtension(scene.path) + "_1080x1920.png";
            RenderSceneTo(scene.path, outPath, 1080, 1920, true);
            EditorUtility.RevealInFinder(Path.Combine(Path.GetDirectoryName(Application.dataPath), outPath));
        }
    }
}
