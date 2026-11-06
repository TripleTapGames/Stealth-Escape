using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>D-065 movement feel: the hop pose is a pure function of time; a character applies and clears it without drifting.</summary>
    public class SS2DHopTests
    {
        [Test]
        public void Pose_RisesOnASineArc_StretchesAtTheTop_ShadowShrinks_ThenSquashesOnLanding_AndReturnsToRest()
        {
            const float dur = 0.24f, settle = 0.10f, h = 0.22f, stretch = 0.06f, squash = 0.12f, shrink = 0.35f;
            var p0 = SS2DHop.At(0f, dur, settle, h, stretch, squash, shrink);
            Assert.AreEqual(0f, p0.Lift); Assert.AreEqual(1f, p0.ScaleX); Assert.AreEqual(1f, p0.ScaleY); Assert.AreEqual(1f, p0.ShadowScale);
            var top = SS2DHop.At(dur * 0.5f, dur, settle, h, stretch, squash, shrink);
            Assert.AreEqual(h, top.Lift, 1e-4f, "peak lift half-way"); Assert.AreEqual(1f + stretch, top.ScaleY, 1e-4f, "stretched at the top"); Assert.Less(top.ScaleX, 1f);
            Assert.AreEqual(1f - shrink, top.ShadowScale, 1e-4f, "shadow smallest at the top"); Assert.Less(top.ShadowAlpha, 1f);
            var quarter = SS2DHop.At(dur * 0.25f, dur, settle, h, stretch, squash, shrink); Assert.Greater(quarter.Lift, 0f); Assert.Less(quarter.Lift, top.Lift);
            var land = SS2DHop.At(dur + settle * 0.5f, dur, settle, h, stretch, squash, shrink);
            Assert.AreEqual(0f, land.Lift, "on the ground while settling"); Assert.AreEqual(1f - squash, land.ScaleY, 1e-4f, "squashed"); Assert.Greater(land.ScaleX, 1f, "and wider"); Assert.AreEqual(1f, land.ShadowScale);
            var rest = SS2DHop.At(dur + settle + 0.01f, dur, settle, h, stretch, squash, shrink);
            Assert.AreEqual(0f, rest.Lift); Assert.AreEqual(1f, rest.ScaleX); Assert.AreEqual(1f, rest.ScaleY);
            Assert.AreEqual(0f, SS2DHop.Travel(0f, dur)); Assert.AreEqual(1f, SS2DHop.Travel(dur, dur)); Assert.AreEqual(0.5f, SS2DHop.Travel(dur * 0.5f, dur), 1e-4f);
            var lib = ScriptableObject.CreateInstance<SS2DSpriteLibrary>(); lib.moveDuration = dur; lib.moveSettle = settle;
            try { Assert.AreEqual(dur + settle, SS2DHop.Total(lib), 1e-5f); } finally { Object.DestroyImmediate(lib); }
        }

        [Test]
        public void Topple_FallsWithABounce_RestsFlat_ThenFadesAndSinks()
        {
            const float fade = 0.6f, angle = 90f, slide = 0.3f;
            var p0 = SS2DTopple.At(0f, fade, angle, slide); Assert.AreEqual(0f, p0.Angle); Assert.AreEqual(0f, p0.Slide); Assert.AreEqual(1f, p0.Alpha);
            var early = SS2DTopple.At(SS2DTopple.FallEnd * 0.4f, fade, angle, slide); Assert.Greater(early.Angle, 0f); Assert.Less(early.Angle, angle * 0.5f, "gravity: slow at first");
            var impact = SS2DTopple.At(SS2DTopple.FallEnd * 0.9f, fade, angle, slide); Assert.Greater(impact.Angle, angle, "overshoots on impact"); Assert.Less(impact.Angle, angle * 1.1f);
            var rest = SS2DTopple.At(0.5f, fade, angle, slide); Assert.AreEqual(angle, rest.Angle, 1e-4f); Assert.AreEqual(slide, rest.Slide, 1e-4f); Assert.AreEqual(1f, rest.Alpha); Assert.AreEqual(0f, rest.Sink);
            var gone = SS2DTopple.At(1f, fade, angle, slide); Assert.AreEqual(0f, gone.Alpha, 1e-4f); Assert.Less(gone.Scale, 1f); Assert.Greater(gone.Sink, 0f); Assert.AreEqual(angle, gone.Angle, 1e-4f, "stays flat while fading");
        }

        [Test]
        public void Launch_ShootsOutFast_ArcsHighWhileSpinning_AndLandsOnItsSide()
        {
            const float dist = 4f, height = 2.2f, spins = 2f;
            var p0 = SS2DLaunch.At(0f, dist, height, spins); Assert.AreEqual(0f, p0.Distance); Assert.AreEqual(0f, p0.Height); Assert.AreEqual(0f, p0.Angle); Assert.AreEqual(1f, p0.Scale);
            var q = SS2DLaunch.At(0.25f, dist, height, spins); Assert.Greater(q.Distance, dist * 0.4f, "fast start: nearly half the way after a quarter of the time");
            var top = SS2DLaunch.At(SS2DLaunch.Landing * 0.5f, dist, height, spins); Assert.AreEqual(height, top.Height, 1e-3f, "peak half-way to touchdown"); Assert.Greater(top.Scale, 1.4f);
            Assert.AreEqual(dist * 0.75f, SS2DLaunch.At(0.5f, dist, height, spins).Distance, 1e-4f);
            var down = SS2DLaunch.At(SS2DLaunch.Landing, dist, height, spins); Assert.AreEqual(0f, down.Height, 1e-3f, "touches the ground at Landing");
            var skip = SS2DLaunch.At(SS2DLaunch.Landing + (1f - SS2DLaunch.Landing) * 0.5f, dist, height, spins); Assert.Greater(skip.Height, 0.03f * height, "a small skip after touching down"); Assert.Less(skip.Height, 0.1f * height);
            var end = SS2DLaunch.At(1f, dist, height, spins); Assert.AreEqual(dist, end.Distance, 1e-4f); Assert.AreEqual(0f, end.Height, 1e-3f); Assert.AreEqual(1f, end.Scale, 1e-3f); Assert.AreEqual(1f, end.Alpha);
            Assert.AreEqual(90f, Mathf.Repeat(end.Angle, 360f), 1e-3f, "two full spins plus a quarter turn: lying on its side");
        }

        [Test]
        public void Character_AppliesAndClearsTheHop_WithoutDrift()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib);
            var go = new GameObject("c"); var c = go.AddComponent<SS2DCharacter>();
            try
            {
                c.Setup(lib, lib.spy, Color.cyan, true, 1f, lib.node);
                var bodyScale = c.body.transform.localScale; var shadowScale = c.shadow.transform.localScale; var shadowColor = c.shadow.color; var badgeY = c.chevron.transform.localPosition.y;
                c.ApplyHop(SS2DHop.At(0.12f, 0.24f, 0.1f, 0.22f, 0.06f, 0.12f, 0.35f));
                Assert.AreEqual(0.22f, c.body.transform.localPosition.y, 1e-4f, "body lifted by the pose (spacing 1)"); Assert.AreEqual(0.22f, c.Lift, 1e-4f);
                Assert.Greater(c.body.transform.localScale.y, bodyScale.y, "stretched"); Assert.Less(c.shadow.transform.localScale.x, shadowScale.x, "shadow shrunk"); Assert.Less(c.shadow.color.a, shadowColor.a);
                Assert.AreEqual(badgeY + 0.22f, c.chevron.transform.localPosition.y, 1e-4f, "facing badge rides with the body");
                c.ClearHop();
                Assert.AreEqual(0f, c.body.transform.localPosition.y, 1e-6f); Assert.AreEqual(bodyScale, c.body.transform.localScale); Assert.AreEqual(shadowScale, c.shadow.transform.localScale); Assert.AreEqual(shadowColor, c.shadow.color);
                Assert.AreEqual(badgeY, c.chevron.transform.localPosition.y, 1e-4f);
                // Applying twice must not compound (the pose is absolute, not incremental).
                c.ApplyHop(SS2DHop.At(0.12f, 0.24f, 0.1f, 0.22f, 0.06f, 0.12f, 0.35f)); c.ApplyHop(SS2DHop.At(0.12f, 0.24f, 0.1f, 0.22f, 0.06f, 0.12f, 0.35f));
                Assert.AreEqual(bodyScale.y * 1.06f, c.body.transform.localScale.y, 1e-4f);
                c.ClearHop(); Assert.AreEqual(bodyScale, c.body.transform.localScale);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
