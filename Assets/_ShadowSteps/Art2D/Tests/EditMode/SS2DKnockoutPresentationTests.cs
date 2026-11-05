using System.Collections.Generic;
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
    /// Knockout presentation on the real 2D renderer (D-040), fed exactly what the preview and the session bridge feed it:
    /// a legal target gets a disc, a knocked-out guard lies down with no fan, no rings, no next-step marker, and the
    /// surviving guards' hazards are unchanged. Stealth draws no target discs.
    /// </summary>
    public class SS2DKnockoutPresentationTests
    {
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib); return lib; }

        /// <summary>The preview controller's presentation sequence for a state.</summary>
        private static void Present(ShadowSteps2DRenderer r, Stage stage, StageState state)
        {
            int t = state.T;
            r.SetPlayer(state.Player);
            for (int i = 0; i < stage.Guards.Count; i++) { r.SetGuardDefeated(i, state.IsDefeated(i)); if (!state.IsDefeated(i)) r.SetGuard(i, GuardTrack.PoseAt(stage.Guards[i], t), GuardTrack.NextStep(stage.Guards[i], t)); }
            var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
            for (int i = 0; i < stage.Guards.Count; i++) { if (state.IsDefeated(i)) continue; foreach (var c in Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[i], t))) if (seen.Add(c)) guardCells.Add(c); }
            var camCells = new List<GridCoord>(); seen.Clear();
            for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, state, i, t)) if (seen.Add(c)) camCells.Add(c);
            r.SetVision(guardCells, camCells);
            r.ClearTargets();
            if (state.KnockoutsEnabled && !state.IsTerminal)
            {
                var cells = new List<GridCoord>(); foreach (int g in StageRules.KnockoutTargets(stage, state)) cells.Add(GuardTrack.PositionAt(stage.Guards[g], t));
                if (cells.Count > 0) r.SetKnockoutTargets(cells);
            }
        }

        private static int ActiveTargets(ShadowSteps2DRenderer r)
        {
            int n = 0; var root = r.transform.Find("Overlays");
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(false)) if (sr.gameObject.activeInHierarchy && sr.color == r.library.knockoutTarget) n++;
            return n;
        }

        [Test]
        public void Level1_Knockout_TargetDiscThenGuardDownWithNoFanOrRings()
        {
            var stage = Pack().FindByCode("LEVEL-1"); Assert.IsNotNull(stage); // D-046 "Blind Side": static guard at (3,2) facing W; start (0,2)
            Assert.AreEqual(new GridCoord(3, 2), stage.Guards[0].Anchor); Assert.AreEqual(Direction.West, stage.Guards[0].Facing);
            var go = new GameObject("test-renderer"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = Library(); r.Build(stage);
            try
            {
                var s = StageState.Initial(stage, StageMode.Knockout);
                foreach (var a in new[] { StageAction.Move(Direction.North), StageAction.Move(Direction.East), StageAction.Move(Direction.East), StageAction.Move(Direction.East) }) { var o = StageRules.TryApply(stage, s, a); Assert.IsTrue(o.Valid); s = o.After; } // (3,1): above the guard
                Present(r, stage, s);
                Assert.AreEqual(1, StageRules.KnockoutTargets(stage, s).Count, "beside the guard: legal");
                Assert.Greater(ActiveTargets(r), 0, "a knockout target disc is drawn under the guard");
                Assert.AreEqual(1, r.GuardCones.Count, "standing guard draws its fan");
                Assert.AreEqual(2, r.GuardOverlay.DrawnCells.Count, "(2,2) and (1,2)");
                Assert.IsFalse(r.IsGuardDefeated(0));

                var ko = StageRules.TryApply(stage, s, StageAction.Move(Direction.South)); // onto the guard = knockout
                Assert.IsTrue(ko.Valid && ko.Knockout); s = ko.After;
                Present(r, stage, s);
                Assert.IsTrue(r.IsGuardDefeated(0)); Assert.IsTrue(r.Guards[0].Defeated);
                Assert.IsFalse(r.Guards[0].gameObject.activeSelf, "a knocked-out guard is off the board (hidden)");
                Assert.AreEqual(0, r.GuardCones.Count, "no fan from a knocked-out guard");
                Assert.AreEqual(0, r.GuardOverlay.DrawnCells.Count, "no rings from a knocked-out guard");
                Assert.AreEqual(0, ActiveTargets(r), "nothing left to knock out");
                Assert.IsFalse(r.transform.Find("Markers/NextArrow_0").gameObject.activeSelf, "no next-step marker on a knocked-out guard");
                Assert.IsFalse(r.transform.Find("Markers/RotationMarker_0").gameObject.activeSelf);
                // Frozen: a later schedule pose must not move it.
                var frozen = r.Guards[0].transform.localPosition;
                r.SetGuard(0, new GuardPose3(new GridCoord(0, 0), Direction.West), Direction.North);
                Assert.AreEqual(frozen, r.Guards[0].transform.localPosition, "schedule updates are ignored while knocked out");
                // Fly-off frames: starts on the node, ends hidden, never alters the logical state.
                r.ApplyGuardFlyOff(0, new GridCoord(3, 2), new GridCoord(3, 1), 0f); Assert.IsTrue(r.Guards[0].gameObject.activeSelf); Assert.Less(Vector3.Distance(r.Guards[0].transform.localPosition, r.Pos(new GridCoord(3, 2))), 1e-3f);
                r.ApplyGuardFlyOff(0, new GridCoord(3, 2), new GridCoord(3, 1), 0.5f); Assert.Greater(Vector3.Distance(r.Guards[0].transform.localPosition, r.Pos(new GridCoord(3, 2))), 0.5f * r.Layout.NodeSpacing, "flying away");
                r.ApplyGuardFlyOff(0, new GridCoord(3, 2), new GridCoord(3, 1), 1f); Assert.IsFalse(r.Guards[0].gameObject.activeSelf, "gone");
                Assert.Less(Vector3.Distance(r.Player.transform.localPosition, r.Pos(new GridCoord(3, 2))), 1e-3f, "spy drawn on the guard's node");

                // Stealth on the same board: no discs, guard standing, fan present.
                var st = StageState.Initial(stage);
                foreach (var a in new[] { StageAction.Move(Direction.North), StageAction.Move(Direction.East), StageAction.Move(Direction.East), StageAction.Move(Direction.East) }) st = StageRules.TryApply(stage, st, a).After;
                Present(r, stage, st);
                Assert.AreEqual(0, ActiveTargets(r), "Stealth never shows knockout targets"); Assert.IsFalse(r.IsGuardDefeated(0)); Assert.AreEqual(1, r.GuardCones.Count);
                Assert.IsTrue(r.Guards[0].gameObject.activeSelf, "revived guard is back on the board"); Assert.AreEqual(Vector3.one, r.Guards[0].transform.localScale); Assert.AreEqual(Quaternion.identity, r.Guards[0].transform.localRotation);
                Assert.AreEqual(Color.white, r.Guards[0].body.color); Assert.Less(Vector3.Distance(r.Guards[0].transform.localPosition, r.Pos(stage.Guards[0].Anchor)), 1e-3f, "back on its node");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RotatingGuard_DrawsTwoArcsFromTheUserArrow_MirroredForClockwise()
        {
            var lib = Library();
            if (lib.rotationArrowArt == null) Assert.Ignore("rotation arrow art not imported (Shadow Steps ▸ 2D ▸ Import final art)");
            var stage = Pack().FindByCode("LEVEL-2"); Assert.IsNotNull(stage); Assert.AreEqual(GuardKind.Rotating, stage.Guards[0].Kind);
            var go = new GameObject("test-renderer"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = lib; r.Build(stage);
            try
            {
                var s = StageState.Initial(stage); Present(r, stage, s);
                var marker = r.transform.Find("Markers/RotationMarker_0"); Assert.IsNotNull(marker); Assert.IsTrue(marker.gameObject.activeSelf);
                var right = marker.Find("Right"); var left = marker.Find("Left"); Assert.IsNotNull(right, "right arc"); Assert.IsNotNull(left, "left arc");
                var rs = right.GetComponent<SpriteRenderer>(); var ls = left.GetComponent<SpriteRenderer>();
                Assert.AreEqual(lib.rotationArrowArt, rs.sprite); Assert.AreEqual(lib.rotationArrowArt, ls.sprite);
                Assert.AreEqual(90f, right.localRotation.eulerAngles.z, 0.01f, "right arc is the art turned 90°"); Assert.AreEqual(270f, left.localRotation.eulerAngles.z, 0.01f, "left arc turned 270°");
                Assert.AreEqual(stage.Guards[0].Clockwise, rs.flipX); Assert.AreEqual(stage.Guards[0].Clockwise, ls.flipX, "unflipped art reads counter-clockwise; clockwise guards mirror both arcs");
                float sp = r.Layout.NodeSpacing; var p = r.Pos(stage.Guards[0].Anchor);
                Assert.AreEqual(p.x, marker.localPosition.x, 1e-4f, "holder centred on the guard's node");
                Assert.AreEqual(p.y + lib.rotationArrowSideHeight * sp, marker.localPosition.y, 1e-4f, "arcs centred at the base-disc height");
                Assert.AreEqual(0f, lib.rotationArrowSideHeight, 1e-6f, "default: centred on the base disc");
                var rw = marker.TransformPoint(right.localPosition) - r.transform.position; var lw = marker.TransformPoint(left.localPosition) - r.transform.position;
                Assert.AreEqual(p.x + lib.rotationArrowSideOffset * sp, rw.x, 1e-3f, "right arc to the right of the node"); Assert.AreEqual(p.x - lib.rotationArrowSideOffset * sp, lw.x, 1e-3f, "left arc to the left");
                Assert.AreEqual(lib.rotationArrowSize * sp, rs.bounds.size.y, 0.02f * sp, "arc length (vertical once turned) in node spacings");
                // Not shown for a static guard, hidden when knocked out.
                var ko = StageState.Initial(stage, StageMode.Knockout);
                var down = new StageState(ko.Player, 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null, 0, StageMode.Knockout, 1UL);
                Present(r, stage, down); Assert.IsFalse(marker.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Level4_TwoGuards_OnlyTheKnockedOutOneLosesItsHazards()
        {
            var stage = Pack().FindByCode("LEVEL-4"); Assert.IsNotNull(stage); Assert.AreEqual(2, stage.Guards.Count);
            var go = new GameObject("test-renderer"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = Library(); r.Build(stage);
            try
            {
                var sol = StageSolver.Solve(stage, new SolveOptions { Mode = StageMode.Knockout }); Assert.IsTrue(sol.Found); Assert.GreaterOrEqual(sol.Knockouts, 1);
                var s = StageState.Initial(stage, StageMode.Knockout); ActionOutcome last = null;
                foreach (var a in sol.Actions) { last = StageRules.TryApply(stage, s, a); s = last.After; if (last.Knockout) break; }
                Assert.IsNotNull(last); Assert.IsTrue(last.Knockout);
                Present(r, stage, s);
                int down = last.KnockedOutGuard, up = 1 - down;
                Assert.IsTrue(r.IsGuardDefeated(down)); Assert.IsFalse(r.IsGuardDefeated(up));
                var expected = new HashSet<GridCoord>(Sight.GuardVision(stage, s, GuardTrack.PoseAt(stage.Guards[up], s.T)));
                Assert.IsTrue(expected.SetEquals(r.GuardOverlay.DrawnCells), "surviving guard's cells exactly");
                Assert.AreEqual(1, r.GuardCones.Count, "the surviving guard always draws its fixed fan (D-051)");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
