using System.IO;
using NUnit.Framework;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>
    /// Every level is framed at the zoom of the pack's largest grid: a node, a character and the painted ground read
    /// the same size on a 3×3 tutorial board as on the 9×11 finale, and the largest level itself is framed exactly as
    /// a plain fit would frame it. Uses the shipped levels_v4.json and the generated sprite library.
    /// </summary>
    public class SS2DCameraFitterTests
    {
        private const float Aspect = 9f / 16f;
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib, "sprite library missing; run Shadow Steps ▸ 2D ▸ Generate"); return lib; }

        private sealed class Rig : System.IDisposable
        {
            public readonly ShadowSteps2DRenderer Renderer; public readonly SS2DCameraFitter Fitter; public readonly Camera Camera;
            public Rig(Stage stage)
            {
                var go = new GameObject("test-renderer"); Renderer = go.AddComponent<ShadowSteps2DRenderer>(); Renderer.library = Library(); Renderer.Build(stage);
                var camGo = new GameObject("test-camera"); Camera = camGo.AddComponent<Camera>(); Fitter = camGo.AddComponent<SS2DCameraFitter>(); Fitter.board = Renderer;
            }
            public float Fit(StagePack pack) { if (pack != null) Fitter.SetReferenceFromPack(pack.Stages); else Fitter.referenceGridNodes = Vector2Int.zero; Fitter.Fit(Aspect); return Camera.orthographicSize; }
            public void Dispose() { Object.DestroyImmediate(Renderer.gameObject); Object.DestroyImmediate(Camera.gameObject); }
        }

        [Test]
        public void LargestPortraitGrid_IsTheMaxShortAndLongSideOverThePack()
        {
            var pack = Pack(); int w = 0, h = 0;
            foreach (var st in pack.Stages) { w = Mathf.Max(w, Mathf.Min(st.Width, st.Height)); h = Mathf.Max(h, Mathf.Max(st.Width, st.Height)); }
            Assert.AreEqual(new Vector2Int(w, h), SS2DCameraFitter.LargestPortraitGrid(pack.Stages));
            Assert.GreaterOrEqual(w, 7, "pack is at least 7 nodes wide"); Assert.GreaterOrEqual(h, 9, "pack is at least 9 nodes tall");
        }

        [Test]
        public void EveryLevel_IsFramedAtTheLargestGridZoom_AndTheLargestLevelIsUnchanged()
        {
            var pack = Pack(); var reference = SS2DCameraFitter.LargestPortraitGrid(pack.Stages);
            Stage largest = null; foreach (var st in pack.Stages) if (Mathf.Min(st.Width, st.Height) == reference.x && Mathf.Max(st.Width, st.Height) == reference.y) largest = st;
            Assert.IsNotNull(largest, "a level with the largest grid in both axes exists");
            float expected; float plainLargest;
            using (var rig = new Rig(largest)) { plainLargest = rig.Fit(null); expected = rig.Fit(pack); }
            Assert.AreEqual(plainLargest, expected, 1e-4f, "the largest level is framed exactly as a plain fit frames it");
            foreach (var st in pack.Stages)
                using (var rig = new Rig(st))
                {
                    float plain = rig.Fit(null), framed = rig.Fit(pack);
                    Assert.AreEqual(expected, framed, 1e-4f, st.Code + ": same zoom as the largest grid");
                    Assert.LessOrEqual(plain, framed + 1e-4f, st.Code + ": the reference only ever zooms out");
                    Assert.AreEqual(1f, rig.Renderer.transform.lossyScale.x, 1e-5f, "node spacing in world units is the same on every board");
                }
        }

        [Test]
        public void SmallLevel_ShowsSpritesAtTheSameScreenSizeAsTheLargest()
        {
            var pack = Pack(); var small = pack.FindByCode("FTUE-2"); Assert.IsNotNull(small); Assert.AreEqual(3, small.Width); Assert.AreEqual(3, small.Height);
            var reference = SS2DCameraFitter.LargestPortraitGrid(pack.Stages);
            float zoomSmall, zoomSmallPlain;
            using (var rig = new Rig(small)) { zoomSmallPlain = rig.Fit(null); zoomSmall = rig.Fit(pack); }
            Assert.Greater(zoomSmall, zoomSmallPlain * 2f, "a plain fit of the 3×3 board blows sprites up to more than twice the size");
            // One node spacing in viewport height: identical for the small and the largest board once the reference applies.
            Stage largest = null; foreach (var st in pack.Stages) if (Mathf.Min(st.Width, st.Height) == reference.x && Mathf.Max(st.Width, st.Height) == reference.y) largest = st;
            float zoomLargest; using (var rig = new Rig(largest)) zoomLargest = rig.Fit(pack);
            Assert.AreEqual(1f / (2f * zoomLargest), 1f / (2f * zoomSmall), 1e-5f, "a node covers the same fraction of the screen on both boards");
        }

        [Test]
        public void ZeroReference_FallsBackToFittingEachBoard()
        {
            var pack = Pack(); var small = pack.FindByCode("FTUE-2");
            using (var rig = new Rig(small))
            {
                float plain = rig.Fit(null);
                var layout = rig.Renderer.ViewLayout; float s = layout.NodeSpacing; var lib = rig.Renderer.library;
                float boardH = (layout.Height + 2f * lib.boardMargin + 2f * rig.Fitter.marginNodes) * s, boardW = (layout.Width + 2f * lib.boardMargin + 2f * rig.Fitter.marginNodes) * s;
                float usableH = 1f - lib.hudTop - lib.hudBottom;
                Assert.AreEqual(Mathf.Max(boardH * 0.5f / usableH, boardW * 0.5f / Aspect), plain, 1e-4f, "plain fit arithmetic unchanged");
            }
        }
    }
}
