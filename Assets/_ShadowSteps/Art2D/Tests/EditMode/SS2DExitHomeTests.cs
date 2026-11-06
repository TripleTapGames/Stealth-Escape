using System.IO;
using NUnit.Framework;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>D-063: with the user's Home / Home glow art imported, the exit node is the green disc over a pulsing glow ring; without it the generated highlight stays.</summary>
    public class SS2DExitHomeTests
    {
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib); return lib; }

        [Test]
        public void ShippedLibrary_HasTheHomeArt_AndTheExitDrawsDiscOverGlow()
        {
            var lib = Library(); if (!lib.HasExitHome) Assert.Ignore("Home art not imported (Shadow Steps ▸ 2D ▸ Import final art)");
            // Imported at 2048 px = 1 unit (Unity halves the reported PPU together with the 1024 max size, the world size stays one unit).
            Assert.AreEqual(1f, lib.exitHome.bounds.size.x, 0.02f, "disc sprite is one unit wide"); Assert.AreEqual(1f, lib.exitHomeGlow.bounds.size.x, 0.02f, "glow sprite is one unit wide");
            var stage = Pack().FindByCode("LEVEL-2"); var go = new GameObject("r"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = lib;
            try
            {
                r.Build(stage); float s = r.Layout.NodeSpacing;
                Assert.AreEqual(lib.exitHome, r.ExitMarker.sprite, "the disc is the exit marker"); Assert.IsNotNull(r.ExitGlow); Assert.AreEqual(lib.exitHomeGlow, r.ExitGlow.sprite);
                Assert.AreEqual(r.Pos(stage.Exit), r.ExitMarker.transform.localPosition); Assert.AreEqual(r.Pos(stage.Exit), r.ExitGlow.transform.localPosition);
                Assert.AreEqual(lib.exitHomeSize * s, r.ExitMarker.transform.localScale.x, 1e-4f); Assert.AreEqual(lib.exitHomeSize * lib.exitHomeGlowScale * s, r.ExitGlow.transform.localScale.x, 1e-4f);
                Assert.Less(r.ExitGlow.sortingOrder, r.ExitMarker.sortingOrder, "glow under the disc"); Assert.AreEqual(r.ExitGlow.sortingLayerName, r.ExitMarker.sortingLayerName);
                var gc = r.ExitGlow.color; Assert.AreEqual(lib.exitHomeGlowTint.r, gc.r, 1e-4f); Assert.AreEqual(lib.exitHomeGlowTint.g, gc.g, 1e-4f); Assert.AreEqual(lib.exitHomeGlowTint.b, gc.b, 1e-4f, "the pale glow art takes the green tint (alpha follows the breathing, checked below)");
                Assert.AreEqual(Color.white, r.ExitMarker.color, "the disc keeps its own colours");
                var anim = r.ExitGlow.GetComponent<SS2DExitGlowAnimator>(); Assert.IsNotNull(anim, "the glow is animated"); Assert.IsNull(r.ExitMarker.GetComponent<SS2DPulse>(), "the disc stays still");
                // D-064: breathing glow and an expanding, fading ripple, driven by a pure function of time.
                float g0 = r.ExitGlow.transform.localScale.x; anim.Apply(lib.exitHomePeriod * 0.5f);
                Assert.AreEqual(g0 * (1f + lib.exitHomePulse), r.ExitGlow.transform.localScale.x, 1e-4f, "biggest half a period in"); Assert.AreEqual(lib.exitHomeGlowTint.a, r.ExitGlow.color.a, 1e-4f, "and brightest");
                anim.Apply(0f); Assert.AreEqual(g0, r.ExitGlow.transform.localScale.x, 1e-4f); Assert.AreEqual(Mathf.Min(lib.exitHomeGlowAlphaMin, lib.exitHomeGlowTint.a), r.ExitGlow.color.a, 1e-4f, "dimmest at rest");
                if (lib.exitHomeRipple)
                {
                    Assert.IsNotNull(anim.ripple, "ripple ring"); Assert.Less(anim.ripple.sortingOrder, r.ExitGlow.sortingOrder, "ripple under the glow");
                    anim.Apply(0f); Assert.AreEqual(g0, anim.ripple.transform.localScale.x, 1e-4f); Assert.AreEqual(lib.exitHomeGlowTint.a, anim.ripple.color.a, 1e-4f, "ripple starts at the glow");
                    anim.Apply(lib.exitHomePeriod * 0.999f); Assert.Greater(anim.ripple.transform.localScale.x, g0 * (1f + lib.exitHomeRippleGrow * 0.99f), "grown"); Assert.Less(anim.ripple.color.a, 0.001f, "and faded out");
                    anim.Apply(lib.exitHomePeriod); Assert.AreEqual(g0, anim.ripple.transform.localScale.x, 1e-3f, "loops");
                }
                const float discFraction = 1265f / 2048f; // painted disc width in the 2048 px canvas (measured from the alpha)
                // The disc size is the designer's call on the library asset (set to 0.7 → a ≈ 0.43-spacing disc on 2026-11-06); keep it readable and inside the node cell.
                Assert.Greater(lib.exitHomeSize * discFraction, 0.3f * s, "disc at least ~0.3 spacing wide"); Assert.Less(lib.exitHomeSize * discFraction, 0.9f * s, "and under 0.9");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void WithoutTheHomeArt_TheGeneratedHighlightPulsesAsBefore()
        {
            var lib = Object.Instantiate(Library()); lib.exitHome = null; lib.exitHomeGlow = null;
            var stage = Pack().FindByCode("LEVEL-2"); var go = new GameObject("r"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = lib;
            try
            {
                r.Build(stage);
                Assert.IsNull(r.ExitGlow); Assert.AreEqual(lib.exitHighlight, r.ExitMarker.sprite); Assert.IsNotNull(r.ExitMarker.GetComponent<SS2DPulse>());
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(lib); }
        }
    }
}
