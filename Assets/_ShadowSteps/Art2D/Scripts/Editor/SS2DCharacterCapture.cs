using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowSteps.Art2D.Editor
{
    /// <summary>
    /// Renders the characters_5_02 prefabs used by the game (spy = m_1, guard = m_2, as chosen on the 3D palette) into
    /// transparent PNG sprite frames: four facings (N = away from the viewer, E, S = toward the viewer, W) × idle and
    /// walk frames, sampled from the pack's own Idle/Walk clips through AnimationMode, framed identically so the frames
    /// line up. The original prefabs, clips and materials are never modified; the runtime uses only the PNGs.
    /// </summary>
    public static class SS2DCharacterCapture
    {
        public const string Dir = SS2DSpriteGenerator.Dir + "/Characters";
        public const string SpyPrefab = "Assets/characters_5_02/Prefabs/m_1.prefab";
        public const string GuardPrefab = "Assets/characters_5_02/Prefabs/m_2.prefab";
        public const int Size = 512;
        public const int IdleFrames = 2, WalkFrames = 6;
        public const float Pitch = 34f; // camera elevation above the ground plane: a 2.5D three-quarter view that still reads on a top-down board

        [System.Serializable] public class Report { public string generated; public List<string> files = new List<string>(); public List<string> problems = new List<string>(); public string idleClip, walkClip; public float idleLength, walkLength; }

        [MenuItem("Shadow Steps/2D/Capture character sprites (spy + guard)")]
        public static void CaptureMenu() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); if (lib == null) lib = SS2DSpriteGenerator.Generate(); Capture(lib, out _); }

        public static bool Capture(SS2DSpriteLibrary lib, out Report report)
        {
            report = new Report { generated = System.DateTime.Now.ToString("s") };
            Directory.CreateDirectory(Dir);
            bool ok = true;
            // NewScene unloads assets that are only referenced from C# locals, so the captured frames are collected first and the library is reloaded by path before assignment.
            var spy = new SS2DCharacterSet(); var guard = new SS2DCharacterSet();
            ok &= CaptureOne(SpyPrefab, "spy", spy, report);
            ok &= CaptureOne(GuardPrefab, "guard", guard, report);
            lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
            if (lib == null) { report.problems.Add("sprite library asset missing at " + SS2DSpriteLibrary.DefaultAssetPath); return false; }
            lib.spy = spy; lib.guard = guard;
            EditorUtility.SetDirty(lib); AssetDatabase.SaveAssets();
            Debug.Log("[SS2D] character capture: " + report.files.Count + " frames" + (report.problems.Count > 0 ? "; problems: " + string.Join("; ", report.problems) : ""));
            return ok;
        }

        private static bool CaptureOne(string prefabPath, string id, SS2DCharacterSet set, Report report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { report.problems.Add("prefab not found: " + prefabPath); return false; }
            set.sourcePrefab = prefabPath;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab); inst.name = id;
            var animator = inst.GetComponentInChildren<Animator>();
            AnimationClip idle = null, walk = null;
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var clip in animator.runtimeAnimatorController.animationClips) { if (clip.name == "Idle") idle = clip; else if (clip.name == "Walk") walk = clip; }
            if (idle == null) report.problems.Add(id + ": no 'Idle' clip; frame 0 uses the bind pose");
            if (walk == null) report.problems.Add(id + ": no 'Walk' clip; walk frames fall back to idle");
            if (idle != null) { report.idleClip = idle.name; report.idleLength = idle.length; }
            if (walk != null) { report.walkClip = walk.name; report.walkLength = walk.length; }

            // Lighting for the capture: soft key from the upper left plus a cool fill, so the frames match the flat 2D palette.
            var key = new GameObject("Key").AddComponent<Light>(); key.type = LightType.Directional; key.color = new Color(1f, 0.97f, 0.92f); key.intensity = 1.25f; key.shadows = LightShadows.None; key.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.color = new Color(0.80f, 0.88f, 1f); fill.intensity = 0.45f; fill.shadows = LightShadows.None; fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.64f);
            var camGo = new GameObject("CaptureCam"); var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f); cam.allowHDR = false; cam.allowMSAA = true;
            var data = cam.GetUniversalAdditionalCameraData(); if (data != null) { data.renderPostProcessing = false; data.renderShadows = false; data.antialiasing = AntialiasingMode.None; }
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.aspect = 1f;

            // Fixed framing from the bind-pose bounds so every frame shares one scale: the sprite is 1 unit tall, the feet sit near the bottom.
            SS3DBoundsOf(inst, out var bounds);
            float height = Mathf.Max(0.5f, bounds.size.y);
            float orthoSize = height * 0.62f; // frame = 1.24 × character height (room for arms/walk extents)
            float feetFraction = 0.12f; // feet at 12 % from the bottom of the frame -> sprite pivot

            AnimationMode.StartAnimationMode();
            try
            {
                for (int d = 0; d < 4; d++)
                {
                    var dir = (Direction)d;
                    float yaw = d == 0 ? 0f : d == 1 ? 90f : d == 2 ? 180f : -90f; // model forward +Z; N faces away from the camera (camera looks along +Z)
                    var frames = new SS2DDirectionFrames { idle = new Sprite[IdleFrames], walk = new Sprite[WalkFrames] };
                    for (int f = 0; f < IdleFrames; f++) frames.idle[f] = Frame(inst, animator, idle, idle != null ? (f * 0.45f) % Mathf.Max(0.01f, idle.length) : 0f, yaw, cam, rt, orthoSize, height, feetFraction, id + "_" + dir + "_idle_" + f, report);
                    for (int f = 0; f < WalkFrames; f++) frames.walk[f] = Frame(inst, animator, walk ?? idle, walk != null ? walk.length * f / WalkFrames : 0f, yaw, cam, rt, orthoSize, height, feetFraction, id + "_" + dir + "_walk_" + f, report);
                    set.facings[d] = frames;
                }
            }
            finally { AnimationMode.StopAnimationMode(); }
            cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
            return true;
        }

        private static Sprite Frame(GameObject inst, Animator animator, AnimationClip clip, float time, float yaw, Camera cam, RenderTexture rt, float orthoSize, float height, float feetFraction, string name, Report report)
        {
            inst.transform.rotation = Quaternion.identity; inst.transform.position = Vector3.zero;
            if (clip != null) { AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(inst, clip, time); AnimationMode.EndSampling(); }
            // Root motion in the clip may drift the character: re-centre the feet on the origin, then face the requested way.
            SS3DBoundsOf(inst, out var b);
            inst.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            inst.transform.RotateAround(Vector3.zero, Vector3.up, yaw);
            // Camera: elevated three-quarter view looking along +Z (so "N" shows the back), orthographic, framed on the fixed height.
            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            var look = new Vector3(0f, height * (0.5f - feetFraction) / 1.0f * 0.0f + height * 0.5f, 0f);
            cam.transform.rotation = rot;
            cam.orthographicSize = orthoSize;
            // Place the camera so that the ground point (origin) projects at feetFraction of the frame height.
            float frameH = orthoSize * 2f;
            var centreOffset = rot * new Vector3(0f, frameH * (0.5f - feetFraction), 0f); // move the view centre up so the feet sit low
            cam.transform.position = centreOffset - rot * Vector3.forward * 10f;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 30f;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            // Un-premultiply guard: pixels with alpha 0 are cleared so bilinear filtering never bleeds black.
            var px = tex.GetPixels32(); int opaque = 0;
            for (int i = 0; i < px.Length; i++) { if (px[i].a == 0) px[i] = new Color32(0, 0, 0, 0); else opaque++; }
            tex.SetPixels32(px); tex.Apply();
            if (opaque < 50) report.problems.Add(name + ": frame is empty (" + opaque + " opaque pixels)");
            string path = Dir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            report.files.Add(path);
            return SS2DSpriteGenerator.ImportCharacterSprite(path, Size, new Vector2(0.5f, feetFraction));
        }

        private static bool SS3DBoundsOf(GameObject go, out Bounds b)
        {
            b = default; bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return any;
        }
    }
}
