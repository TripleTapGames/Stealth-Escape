using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>D-071: the user's arrowhead peeks out from under a patrol's base disc on the side of its NEXT step (not its facing); turning and static guards show none; the player has none.</summary>
    public class SS2DFacingArrowTests
    {
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib); return lib; }

        [Test]
        public void Patrol_ShowsTheRimArrow_TowardItsNextStep_NotItsFacing_UnderTheBody()
        {
            var lib = Library(); if (lib.facingArrow == null) Assert.Ignore("cap_arrow not imported (Shadow Steps ▸ 2D ▸ Import final art)");
            Assert.AreEqual("cap_arrow", lib.facingArrow.name);
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json"))); var stage = pack.FindByCode("LEVEL-8");
            var patrol = stage.Guards[0]; Assert.AreEqual(GuardKind.PingPong, patrol.Kind); var turner = stage.Guards[1]; Assert.AreEqual(GuardKind.Rotating, turner.Kind);
            var go = new GameObject("r"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = lib;
            try
            {
                r.Build(stage); float s = r.Layout.NodeSpacing; var g = r.Guards[0];
                Assert.IsTrue(g.UsesRimArrow); Assert.AreEqual(lib.facingArrow, g.chevron.sprite);
                Assert.AreEqual(SS2DLayers.Actors, g.chevron.sortingLayerName); Assert.Less(g.chevron.sortingOrder, g.body.sortingOrder, "under the body (only the tip shows past the disc)"); Assert.Greater(g.chevron.sortingOrder, g.shadow.sortingOrder, "over the cast shadow");
                Assert.AreEqual(lib.facingArrowTint, g.chevron.color);
                // t = 1: the patrol stands at the end of its route facing the way it came (N) while its next step goes back (S).
                var pose = GuardTrack.PoseAt(patrol, 1); var next = GuardTrack.NextStep(patrol, 1); Assert.IsTrue(next.HasValue); Assert.AreNotEqual(pose.Facing, next.Value, "endpoint: facing and next step differ");
                r.SetGuard(0, pose, next);
                Assert.IsTrue(g.chevron.gameObject.activeSelf, "arrow shown for a patrol with a next step"); Assert.AreEqual(r.View(next.Value), g.RimArrowDirection);
                var dir = SS2DCharacter.ScreenDir(r.View(next.Value)); var p = (Vector2)g.chevron.transform.localPosition;
                Assert.AreEqual(lib.facingArrowOffset * s, p.magnitude, 1e-4f, "pivot at the authored offset from the disc centre"); Assert.Greater(Vector2.Dot(p.normalized, dir), 0.999f, "on the next-step side, not the facing side");
                var pointing = (Vector2)(g.chevron.transform.localRotation * Vector3.right); Assert.Greater(Vector2.Dot(pointing, dir), 0.999f, "the arrow (authored pointing east) points toward the next node");
                Assert.IsFalse(r.transform.Find("Markers/NextArrow_0").gameObject.activeSelf, "the cyan next badge is retired with the arrow art");
                // Every drawn direction places and orients it consistently.
                foreach (var d in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
                {
                    g.SetRimArrow(d); var dd = SS2DCharacter.ScreenDir(d); var q = (Vector2)g.chevron.transform.localPosition;
                    Assert.Greater(Vector2.Dot(q.normalized, dd), 0.999f, d.ToString()); Assert.Greater(Vector2.Dot((Vector2)(g.chevron.transform.localRotation * Vector3.right), dd), 0.999f, d.ToString());
                }
                // Tip past the rim, tail under the disc: arrow half-length ≈ 0.38 unit × size.
                float half = 0.385f * lib.facingArrowSize * s, disc = 0.34f * s;
                Assert.Greater(lib.facingArrowOffset * s + half, disc + 0.03f * s, "tip outside the painted disc"); Assert.Less(lib.facingArrowOffset * s - half, disc * 0.6f, "tail hidden under the disc");
                // Turning guard: no next step, no arrow (its rotation arcs show the turn). Player: never.
                r.SetGuard(1, GuardTrack.PoseAt(turner, 0), GuardTrack.NextStep(turner, 0));
                Assert.IsFalse(r.Guards[1].chevron.gameObject.activeSelf, "a rotating guard shows no step arrow"); Assert.IsNull(r.Guards[1].RimArrowDirection);
                Assert.IsFalse(r.Player.chevron.gameObject.activeSelf, "the spy carries no arrow");
                // Rides with the hop; hidden once knocked out.
                g.SetRimArrow(Direction.South); g.ApplyHop(new SS2DHop.Pose { Lift = 0.2f, ScaleX = 1f, ScaleY = 1f, ShadowScale = 1f, ShadowAlpha = 1f });
                Assert.AreEqual(-lib.facingArrowOffset * s + 0.2f * s, g.chevron.transform.localPosition.y, 1e-4f, "rides with the hop"); g.ClearHop();
                r.SetGuardDefeated(0, true); Assert.IsFalse(g.chevron.gameObject.activeSelf, "no arrow on a knocked-out guard"); r.SetGuardDefeated(0, false);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
