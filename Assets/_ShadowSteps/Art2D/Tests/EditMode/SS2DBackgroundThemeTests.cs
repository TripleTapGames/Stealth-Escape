using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>
    /// D-058 backdrop: the shipped library paints every level with the courtyard v2 (the user's call: one setting), framed by the floor
    /// rect measured from that image; the theme list exists for a later second setting and is empty. Uses the shipped levels_v4.json and
    /// the imported sprite library.
    /// </summary>
    public class SS2DBackgroundThemeTests
    {
        private const float Aspect = 9f / 16f;
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib, "sprite library missing"); return lib; }

        private sealed class Rig : System.IDisposable
        {
            public readonly ShadowSteps2DRenderer Renderer; public readonly SS2DCameraFitter Fitter; public readonly Camera Camera;
            public Rig(Stage stage, StagePack pack, SS2DSpriteLibrary lib = null)
            {
                var go = new GameObject("test-renderer"); Renderer = go.AddComponent<ShadowSteps2DRenderer>(); Renderer.library = lib != null ? lib : Library(); Renderer.Build(stage);
                var camGo = new GameObject("test-camera"); Camera = camGo.AddComponent<Camera>(); Camera.orthographic = true; Fitter = camGo.AddComponent<SS2DCameraFitter>(); Fitter.board = Renderer;
                if (pack != null) Fitter.SetReferenceFromPack(pack.Stages); else Fitter.referenceGridNodes = Vector2Int.zero;
                Fitter.Fit(Aspect); Renderer.FitBackground(Camera);
            }
            /// <summary>World-space rect of the backdrop's painted floor (its board rect after the uniform scale and placement).</summary>
            public Rect FloorWorld()
            {
                var bg = Renderer.BackgroundRenderer; Assert.IsNotNull(bg, "background drawn");
                var size = bg.sprite.bounds.size; float k = bg.transform.localScale.x; var p = (Vector2)bg.transform.position;
                var bl = p - new Vector2(size.x, size.y) * 0.5f * k; var r = Renderer.BackgroundBoardRect;
                return new Rect(bl.x + r.x * size.x * k, bl.y + r.y * size.y * k, r.width * size.x * k, r.height * size.y * k);
            }
            public void Dispose() { Object.DestroyImmediate(Renderer.gameObject); Object.DestroyImmediate(Camera.gameObject); }
        }

        [Test]
        public void ShippedLibrary_PaintsEveryLevelWithTheCourtyardV2()
        {
            var lib = Library(); var pack = Pack();
            Assert.IsNotNull(lib.backgroundSprite, "background sprite");
            Assert.AreEqual("Courtyard-Natural-Sides-1400x2000-v2", lib.backgroundSprite.name, "the user's v2 courtyard is the backdrop");
            Assert.AreEqual(256f, lib.backgroundSprite.pixelsPerUnit, 0.01f, "imported at the background PPU");
            Assert.AreEqual(1400, lib.backgroundSprite.texture.width); Assert.AreEqual(2000, lib.backgroundSprite.texture.height);
            Assert.AreEqual(SS2DBackgroundFit.FrameBoard, lib.backgroundFit);
            // Floor between the inner wall faces of the v2 painting (x 334–1036, y 532–1686 from the top), not the v1 rect (0.277, 0.344, 0.432, 0.368).
            Assert.AreEqual(0.240f, lib.backgroundBoardRect.x, 0.005f, "floor left"); Assert.AreEqual(0.157f, lib.backgroundBoardRect.y, 0.005f, "floor bottom");
            Assert.AreEqual(0.499f, lib.backgroundBoardRect.width, 0.005f, "floor width"); Assert.AreEqual(0.577f, lib.backgroundBoardRect.height, 0.005f, "floor height");
            Assert.IsTrue(lib.backgroundThemes == null || lib.backgroundThemes.Length == 0, "one setting only: no alternate themes");
            foreach (var st in pack.Stages) Assert.IsNull(lib.ThemeFor(st.Id), st.Code + " uses the single background");
        }

        [Test]
        public void Renderer_UsesATheme_OnlyForIdsInsideItsRange()
        {
            var pack = Pack(); var lib = Object.Instantiate(Library());
            try
            {
                var alt = new Texture2D(140, 200, TextureFormat.RGBA32, false); // a small portrait stand-in (the shipped texture is not readable, so it cannot be copied)
                var altSprite = Sprite.Create(alt, new Rect(0f, 0f, alt.width, alt.height), new Vector2(0.5f, 0.5f), 25.6f, 0, SpriteMeshType.FullRect);
                var theme = new SS2DBackgroundTheme { name = "Alt", sprite = altSprite, boardRect = new Rect(0.3f, 0.3f, 0.4f, 0.4f), firstLevelId = 50, lastLevelId = 60, clearColor = Color.magenta };
                lib.backgroundThemes = new[] { theme };
                var b = new StageBuilder { Id = 55, Code = "BG-IN", Title = "inside" }.FromRows(new[] { "...", "...", "..." }); b.Start = new GridCoord(0, 0); b.Exit = new GridCoord(2, 2);
                using (var rig = new Rig(b.Build(), pack, lib))
                {
                    Assert.AreSame(theme, rig.Renderer.ActiveTheme, "id 55 is inside the range");
                    Assert.AreEqual(altSprite, rig.Renderer.BackgroundRenderer.sprite); Assert.AreEqual(theme.boardRect, rig.Renderer.BackgroundBoardRect); Assert.AreEqual(Color.magenta, rig.Renderer.BackgroundClearColor);
                }
                var shipped = pack.FindByCode("LEVEL-15");
                using (var rig = new Rig(shipped, pack, lib))
                {
                    Assert.IsNull(rig.Renderer.ActiveTheme, "a shipped id is outside the range");
                    Assert.AreEqual(lib.backgroundSprite, rig.Renderer.BackgroundRenderer.sprite, "the single background is drawn");
                    Assert.AreEqual(lib.backgroundBoardRect, rig.Renderer.BackgroundBoardRect); Assert.AreEqual(lib.background, rig.Renderer.BackgroundClearColor);
                }
                Object.DestroyImmediate(altSprite); Object.DestroyImmediate(alt);
            }
            finally { Object.DestroyImmediate(lib); }
        }

        [Test]
        public void EveryLevel_PlayableNodesSitInsideThePaintedFloor_AndTheViewIsCovered()
        {
            var lib = Library(); Assert.AreEqual(SS2DBackgroundFit.FrameBoard, lib.backgroundFit); var pack = Pack();
            foreach (var st in pack.Stages)
                using (var rig = new Rig(st, pack))
                {
                    var floor = rig.FloorWorld(); float s = rig.Renderer.Layout.NodeSpacing;
                    int ring = lib.backgroundFrameInsideBoundaryWalls && ShadowSteps2DRenderer.BoundaryIsAllWalls(st) ? 1 : 0;
                    // The grid (node centres ± half a spacing) is framed inside the floor rect; cells on an all-wall boundary ring lie under the painted walls.
                    var inset = new Rect(floor.xMin + 0.49f * s, floor.yMin + 0.49f * s, floor.width - 0.98f * s, floor.height - 0.98f * s);
                    for (int y = ring; y < st.Height - ring; y++)
                        for (int x = ring; x < st.Width - ring; x++)
                        {
                            var c = new GridCoord(x, y); if (!st.IsFloor(c) && !st.IsBlocked(c)) continue; // nodes and cover crates both sit on the painted floor
                            var p = (Vector2)rig.Renderer.Pos(c);
                            Assert.IsTrue(inset.Contains(p), st.Code + ": node " + c + " at " + p + " lies inside the painted floor " + floor);
                        }
                    // The scaled image still covers the whole camera view (no clear colour showing).
                    var bg = rig.Renderer.BackgroundRenderer; var size = bg.sprite.bounds.size * bg.transform.localScale.x; var centre = (Vector2)bg.transform.position;
                    float viewH = rig.Camera.orthographicSize * 2f, viewW = viewH * Aspect; var camP = (Vector2)rig.Camera.transform.position;
                    Assert.LessOrEqual(camP.x + viewW * 0.5f, centre.x + size.x * 0.5f + 1e-3f, st.Code + ": right edge covered"); Assert.GreaterOrEqual(camP.x - viewW * 0.5f, centre.x - size.x * 0.5f - 1e-3f, st.Code + ": left edge covered");
                    Assert.LessOrEqual(camP.y + viewH * 0.5f, centre.y + size.y * 0.5f + 1e-3f, st.Code + ": top edge covered"); Assert.GreaterOrEqual(camP.y - viewH * 0.5f, centre.y - size.y * 0.5f - 1e-3f, st.Code + ": bottom edge covered");
                }
        }
    }
}
