using System.IO;
using NUnit.Framework;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>
    /// D-057 zoom policy: every level is framed at least at the library's reference grid (5×7 by default), so boards up to that
    /// size share one zoom and read big, while a bigger board zooms out exactly as a plain fit would so it stays fully visible.
    /// Uses the shipped levels_v4.json and the generated sprite library.
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
        public void EveryLevel_IsFramedAtTheReferenceZoom_OrFitsItselfWhenBigger()
        {
            var pack = Pack(); var lib = Library(); var reference = SS2DCameraFitter.ReferenceFor(lib, pack.Stages);
            Assert.AreEqual(new Vector2Int(5, 7), reference, "D-057: the shipped reference is the common 5×7 board");
            Stage typical = null; foreach (var st in pack.Stages) if (Mathf.Min(st.Width, st.Height) == reference.x && Mathf.Max(st.Width, st.Height) == reference.y) { typical = st; break; }
            Assert.IsNotNull(typical, "a level with the reference grid exists");
            float referenceZoom; float plainTypical;
            using (var rig = new Rig(typical)) { plainTypical = rig.Fit(null); referenceZoom = rig.Fit(pack); }
            Assert.AreEqual(plainTypical, referenceZoom, 1e-4f, "a reference-sized level is framed exactly as a plain fit frames it");
            int shared = 0, bigger = 0;
            foreach (var st in pack.Stages)
                using (var rig = new Rig(st))
                {
                    float plain = rig.Fit(null), framed = rig.Fit(pack);
                    Assert.AreEqual(Mathf.Max(plain, referenceZoom), framed, 1e-4f, st.Code + ": the reference zoom, unless the board needs more room");
                    Assert.LessOrEqual(plain, framed + 1e-4f, st.Code + ": the whole board always stays in view");
                    Assert.AreEqual(1f, rig.Renderer.transform.lossyScale.x, 1e-5f, "node spacing in world units is the same on every board");
                    if (Mathf.Abs(framed - referenceZoom) < 1e-4f) shared++; else bigger++;
                }
            Assert.Greater(shared, bigger, "most of the campaign shares the reference zoom");
            Assert.Greater(bigger, 0, "the big late levels zoom out to fit");
            // The D-039 behaviour is still one switch away: frame everything at the largest grid.
            var largestLib = Object.Instantiate(lib); largestLib.zoomReferenceFromPackLargest = true;
            try { Assert.AreEqual(SS2DCameraFitter.LargestPortraitGrid(pack.Stages), SS2DCameraFitter.ReferenceFor(largestLib, pack.Stages)); }
            finally { Object.DestroyImmediate(largestLib); }
        }

        [Test]
        public void SmallLevel_ShowsSpritesAtTheSameScreenSizeAsATypicalLevel()
        {
            var pack = Pack(); var small = pack.FindByCode("FTUE-2"); Assert.IsNotNull(small); Assert.AreEqual(3, small.Width); Assert.AreEqual(3, small.Height);
            var reference = SS2DCameraFitter.ReferenceFor(Library(), pack.Stages);
            float zoomSmall, zoomSmallPlain;
            using (var rig = new Rig(small)) { zoomSmallPlain = rig.Fit(null); zoomSmall = rig.Fit(pack); }
            Assert.Greater(zoomSmall, zoomSmallPlain * 1.4f, "a plain fit of the 3×3 board would still blow sprites up well past the shared size");
            // One node spacing in viewport height: identical for the small board and a reference-sized board.
            Stage typical = null; foreach (var st in pack.Stages) if (Mathf.Min(st.Width, st.Height) == reference.x && Mathf.Max(st.Width, st.Height) == reference.y) { typical = st; break; }
            float zoomTypical; using (var rig = new Rig(typical)) zoomTypical = rig.Fit(pack);
            Assert.AreEqual(1f / (2f * zoomTypical), 1f / (2f * zoomSmall), 1e-5f, "a node covers the same fraction of the screen on both boards");
            // The 9×11 finale is bigger than the reference: it zooms out to its own plain fit so nothing is cut off.
            var finale = pack.FindByCode("LEVEL-30"); float plainFinale, framedFinale;
            using (var rig = new Rig(finale)) { plainFinale = rig.Fit(null); framedFinale = rig.Fit(pack); }
            Assert.AreEqual(plainFinale, framedFinale, 1e-4f); Assert.Greater(framedFinale, zoomTypical, "further out than the shared zoom");
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
