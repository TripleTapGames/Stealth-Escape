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
    /// The 2D presentation may draw a wide stage turned 90°; these tests prove the drawing transform is a bijection
    /// on cells, that screen directions map back to the logical directions the rules receive (swipes, arrow keys,
    /// facing art), and that the hazard overlay shows exactly the cells the engine reports. Uses the shipped
    /// levels_v4.json and the generated sprite library (Assets/_ShadowSteps/Art2D/SS2DSpriteLibrary.asset).
    /// </summary>
    public class SS2DPortraitRotationTests
    {
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib, "sprite library missing; run Shadow Steps ▸ 2D ▸ Generate"); return lib; }

        private static ShadowSteps2DRenderer Build(Stage stage, bool rotate)
        {
            var go = new GameObject("test-renderer");
            var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = Library(); r.autoRotatePortrait = rotate;
            r.Build(stage);
            return r;
        }

        [Test]
        public void Level2_IsDrawnRotated_AndEveryCellRoundTrips()
        {
            var stage = Pack().FindByCode("LEVEL-2"); Assert.IsNotNull(stage);
            var r = Build(stage, true);
            try
            {
                Assert.IsTrue(r.Rotated, "6×4 must rotate for portrait");
                Assert.AreEqual(stage.Height, r.ViewLayout.Width); Assert.AreEqual(stage.Width, r.ViewLayout.Height);
                var seen = new HashSet<Vector2>();
                for (int y = 0; y < stage.Height; y++) for (int x = 0; x < stage.Width; x++)
                {
                    var c = new GridCoord(x, y);
                    var world = r.transform.TransformPoint(r.Pos(c));
                    Assert.AreEqual(c, r.ToGrid(world), "ToGrid(Pos(c)) must give c back for " + c);
                    Assert.AreEqual(c, r.ToGrid(world + new Vector3(0.3f, -0.3f, 0f)), "picking within the cell must stay on " + c);
                    Assert.IsTrue(seen.Add(new Vector2(Mathf.Round(world.x * 100f), Mathf.Round(world.y * 100f))), "two cells drawn at the same place");
                }
                // The drawn board is 4 wide × 6 tall (node centres span 3 × 5 units).
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (var c in stage.FloorNodes()) { var p = r.Pos(c); minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                Assert.AreEqual(3f, maxX - minX, 1e-4f); Assert.AreEqual(5f, maxY - minY, 1e-4f);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void DirectionMapping_IsConsistentWithDrawnPositions()
        {
            var stage = Pack().FindByCode("LEVEL-2");
            foreach (bool rotate in new[] { true, false })
            {
                var r = Build(stage, rotate);
                try
                {
                    foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
                    {
                        Assert.AreEqual(d, r.Logical(r.View(d)), "Logical(View(d)) round trip, rotated=" + rotate);
                        Assert.AreEqual(d, r.View(r.Logical(d)), "View(Logical(d)) round trip, rotated=" + rotate);
                        // Moving logically in d must move on screen in View(d): the screen delta of the neighbour equals the unit vector of View(d).
                        var a = new GridCoord(2, 1); var b = a.Neighbor(d);
                        var delta = r.Pos(b) - r.Pos(a);
                        var expected = new Vector3(DirectionUtil.DeltaX(r.View(d)), -DirectionUtil.DeltaY(r.View(d)), 0f);
                        Assert.AreEqual(expected.x, delta.x, 1e-4f, "screen x for " + d + " rotated=" + rotate); Assert.AreEqual(expected.y, delta.y, 1e-4f, "screen y for " + d + " rotated=" + rotate);
                        // A swipe drawn in screen direction s must request the logical direction whose drawn delta is s.
                        var swipe = r.View(d); var logical = r.Logical(swipe);
                        Assert.AreEqual(d, logical);
                    }
                    if (rotate) { Assert.AreEqual(Direction.West, r.View(Direction.North)); Assert.AreEqual(Direction.North, r.View(Direction.East)); Assert.AreEqual(Direction.East, r.Logical(Direction.North)); Assert.AreEqual(Direction.South, r.Logical(Direction.East)); }
                }
                finally { Object.DestroyImmediate(r.gameObject); }
            }
        }

        private static bool InsideFan(Vector3[] poly, Vector3 point)
        {
            var p = poly[0] + (point - poly[0]) * 0.999f; bool inside = false; int n = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++) { var a = poly[i]; var b = poly[j]; if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside; }
            return inside;
        }

        [Test]
        public void GuardVisionOverlay_ShowsExactlyTheEngineCells_AfterRotation()
        {
            var stage = Pack().FindByCode("LEVEL-2");
            var r = Build(stage, true);
            try
            {
                var state = StageState.Initial(stage);
                var cells = new List<GridCoord>();
                for (int i = 0; i < stage.Guards.Count; i++) cells.AddRange(Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[i], 0)));
                Assert.AreEqual(2, cells.Count, "LEVEL-2's rotating guard sees two nodes at t=0");
                r.SetGuard(0, GuardTrack.PoseAt(stage.Guards[0], 0), null);
                r.SetVision(cells, new List<GridCoord>());
                var drawn = new List<Vector3>();
                foreach (Transform t in r.transform.Find("Overlays/GuardVision")) if (t.name == "NodeRing" && t.gameObject.activeSelf) drawn.Add(t.position);
                Assert.AreEqual(cells.Count, drawn.Count, "one ring per engine cell");
                foreach (var c in cells)
                {
                    var p = r.transform.TransformPoint(r.Pos(c));
                    Assert.IsTrue(drawn.Exists(q => Vector3.Distance(q, p) < 1e-3f), "ring drawn at the rotated position of " + c);
                }
                Assert.AreEqual(1, r.GuardCones.Count, "one red fan for the guard that sees something");
                var fan = r.GuardCones[0]; var gp = r.Pos(GuardTrack.PoseAt(stage.Guards[0], 0).Position);
                Assert.Less(Vector3.Distance(fan[0], gp), 1e-4f, "fan apex on the guard's drawn node");
                foreach (var c in cells) Assert.IsTrue(InsideFan(fan, r.Pos(c)), "fan covers the seen node " + c);
                for (int y = 0; y < stage.Height; y++) for (int x = 0; x < stage.Width; x++) { var c = new GridCoord(x, y); if (stage.IsFloor(c) && !cells.Contains(c) && c != GuardTrack.PoseAt(stage.Guards[0], 0).Position) Assert.IsFalse(InsideFan(fan, r.Pos(c)), "fan must not cover the unseen node " + c); }
                // The guard faces logical East; drawn, that is screen North, and the lit cells lie above the guard on screen.
                var pose = GuardTrack.PoseAt(stage.Guards[0], 0);
                Assert.AreEqual(Direction.East, pose.Facing);
                Assert.AreEqual(Direction.North, r.View(pose.Facing));
                var g = r.Pos(pose.Position);
                foreach (var c in cells) { var p = r.Pos(c); Assert.AreEqual(g.x, p.x, 1e-4f); Assert.Greater(p.y, g.y); }
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void ExitDoor_IsOffByDefault_ExitHighlightStays()
        {
            var stage = Pack().FindByCode("LEVEL-2"); var r = Build(stage, true);
            try
            {
                if (r.library.showExitDoor) Assert.Ignore("library draws the door");
                Assert.IsFalse(r.HasExitDoor); Assert.IsNull(r.transform.Find("Props/Door")); Assert.IsNull(r.transform.Find("Paths/Door_Path"));
                Assert.IsNotNull(r.transform.Find("Props/Exit"), "the exit node highlight still marks the goal");
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void ExitDoor_IsDrawnBeyondTheExitNode_OnItsBoardEdge()
        {
            // The door is an optional presentation (library.showExitDoor, off for the courtyard); this test enables it on a copy of the library.
            var stage = Pack().FindByCode("LEVEL-2");
            var lib = Object.Instantiate(Library()); lib.showExitDoor = true;
            var go = new GameObject("test-renderer-door"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = lib; r.autoRotatePortrait = true; r.Build(stage);
            try
            {
                Assert.IsTrue(r.HasExitDoor);
                Assert.AreEqual(Direction.East, ShadowSteps2DRenderer.ExitEdge(stage), "exit (5,2) sits on the east edge");
                Assert.AreEqual(Direction.North, r.DoorOutward, "east edge is drawn at the top after rotation");
                var exit = r.Pos(stage.Exit);
                Assert.AreEqual(exit.x, r.DoorPosition.x, 1e-4f); Assert.Greater(r.DoorPosition.y, exit.y + 1f, "door at least one spacing beyond the node");
                Assert.IsFalse(stage.Inside(r.ToGrid(r.transform.TransformPoint(r.DoorPosition))), "door is off the logical board");
                Assert.IsNotNull(r.transform.Find("Props/Door")); Assert.IsNotNull(r.transform.Find("Props/Exit")); Assert.IsNotNull(r.transform.Find("Paths/Door_Path"));
                Assert.IsTrue(r.ViewBounds.Contains(new Vector2(r.DoorPosition.x, r.DoorPosition.y)), "camera frame includes the door");
                r.SetExitDoorOpen(true); Assert.AreEqual(r.library.exitDoorOpen, r.transform.Find("Props/Door").GetComponent<SpriteRenderer>().sprite);
                r.SetExitDoorOpen(false); Assert.AreEqual(r.library.exitDoorClosed, r.transform.Find("Props/Door").GetComponent<SpriteRenderer>().sprite);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(lib); }
        }

        [Test]
        public void TallOrSquareStages_AreNotRotated()
        {
            var pack = Pack();
            foreach (var st in pack.Stages)
            {
                if (st.Width > st.Height) continue;
                var r = Build(st, true);
                try { Assert.IsFalse(r.Rotated, st.Code); } finally { Object.DestroyImmediate(r.gameObject); }
                break;
            }
        }
    }
}
