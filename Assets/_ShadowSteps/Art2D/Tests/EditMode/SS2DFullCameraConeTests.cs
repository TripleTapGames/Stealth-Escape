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
    /// Full camera cone graphics (D-042) on the real renderer: the drawn cone is the nominal 70° / two-spacing triangle
    /// for fixed, rotating and sweeping cameras in every facing; obstacles, closed doors, smoke and the board edge never
    /// trim it (mesh vertices AND renderer bounds), while the detected-node rings and the engine's detection stay
    /// occluded; EMP keeps hiding the cone; the cone meshes sort above obstacle art and below actors; and the rotating
    /// guard arcs are bound to the real on-screen turn direction.
    /// </summary>
    public class SS2DFullCameraConeTests
    {
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib); return lib; }
        private static ShadowSteps2DRenderer Build(Stage stage) { var go = new GameObject("test-renderer"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = Library(); r.Build(stage); return r; }

        private static void Present(ShadowSteps2DRenderer r, Stage stage, StageState state)
        {
            int t = state.T;
            for (int i = 0; i < stage.Cameras.Count; i++) r.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], t), state.EmpLeftOn(i), false);
            for (int i = 0; i < stage.Doors.Count; i++) r.SetDoor(i, state.IsDoorOpen(i));
            r.SetClouds(state.Clouds);
            var cam = new List<GridCoord>(); for (int i = 0; i < stage.Cameras.Count; i++) cam.AddRange(Sight.CameraVision(stage, state, i, t));
            r.SetVision(new List<GridCoord>(), cam);
        }

        private static void AssertNominal(ShadowSteps2DRenderer r, Stage stage, int camIndex, Direction facing, Vector3[] poly, string label)
        {
            var cam = stage.Cameras[camIndex]; var o = Sight.CameraOrigin(cam); float s = r.Layout.NodeSpacing;
            Assert.AreEqual(3, poly.Length, label + ": apex + two far corners");
            Assert.Less(Vector3.Distance(poly[0], r.PosF(o.X, o.Y)), 1e-4f, label + ": apex on the logical wall face");
            var drawnDir = r.RotationFor(facing) * Vector3.up;
            var centre = (poly[1] + poly[2]) * 0.5f;
            Assert.Less(Vector3.Distance(centre, poly[0] + drawnDir * (float)Sight.CameraRange * s), 1e-3f, label + ": flat far edge two spacings forward");
            float half = (float)(Sight.CameraRange * System.Math.Tan(Sight.CameraHalfAngleDegrees * System.Math.PI / 180.0)) * s;
            Assert.AreEqual(2f * half, Vector3.Distance(poly[1], poly[2]), 1e-3f, label + ": 70° total");
            Assert.AreEqual(0f, Vector3.Dot(poly[1] - poly[2], drawnDir), 1e-3f, label + ": far edge perpendicular to forward");
        }

        [Test]
        public void EveryShippedCamera_DrawsTheNominalConeInEveryFacing_IndependentOfOccluders()
        {
            var lib = Library(); if (!lib.fullCameraCone) Assert.Ignore("fullCameraCone is off in the library");
            int cameras = 0;
            foreach (var stage in Pack().Stages)
            {
                if (stage.Cameras.Count == 0) continue;
                var r = Build(stage);
                try
                {
                    for (int t = 0; t < 4; t++)
                    {
                        var state = new StageState(stage.Start, t, false, 0, null, null, stage.SmokeCharges, stage.EmpCharges, -1, false, StageResult.Playing, null);
                        Present(r, stage, state);
                        Assert.AreEqual(stage.Cameras.Count, r.CameraCones.Count, stage.Code + " t=" + t);
                        for (int i = 0; i < stage.Cameras.Count; i++) AssertNominal(r, stage, i, CameraTrack.FacingAt(stage.Cameras[i], t), r.CameraCones[i], stage.Code + " cam" + i + " t=" + t);
                        // Rings only on the engine's detected nodes (occluded), never on every node the nominal cone paints.
                        var detected = new HashSet<GridCoord>(); for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, state, i, t)) detected.Add(c);
                        Assert.IsTrue(detected.SetEquals(r.CameraOverlay.DrawnCells), stage.Code + " t=" + t + ": rings = detection");
                    }
                    cameras += stage.Cameras.Count;
                }
                finally { Object.DestroyImmediate(r.gameObject); }
            }
            Assert.Greater(cameras, 5);
        }

        [Test]
        public void ObstacleInsideTheCone_DoesNotTrimTheDrawing_ButStillBlocksDetection()
        {
            // LEVEL-19 at t=2 the camera faces the authored cover cell (4,1): detection is clipped by the crate; the drawn cone is not.
            var stage = Pack().FindByCode("LEVEL-19"); Assert.IsNotNull(stage); var r = Build(stage);
            try
            {
                var open = Pack().FindByCode("LEVEL-19");
                var s2 = new StageState(stage.Start, 2, false, 0, null, null, stage.SmokeCharges, stage.EmpCharges, -1, false, StageResult.Playing, null);
                Present(r, stage, s2);
                var poly = r.CameraCones[0]; var facing = CameraTrack.FacingAt(stage.Cameras[0], 2);
                AssertNominal(r, stage, 0, facing, poly, "LEVEL-19 t2");
                var detected = Sight.CameraVision(stage, s2, 0, 2);
                var traced = Sight.CameraConeOutline(stage, 0, null, stage.Cameras[0], facing);
                float tracedReach = 0f; foreach (var p in traced) tracedReach = Mathf.Max(tracedReach, Vector3.Distance(r.PosF(p.X, p.Y), poly[0]));
                Assert.Less(tracedReach, (float)Sight.CameraRange * r.Layout.NodeSpacing - 0.05f, "the engine's occluded silhouette is shorter than two spacings here (cover in the way)");
                Assert.Greater(Vector3.Distance((poly[1] + poly[2]) * 0.5f, poly[0]), tracedReach + 0.05f, "the drawn cone is NOT shortened by the obstacle");
                Assert.IsTrue(new HashSet<GridCoord>(detected).SetEquals(r.CameraOverlay.DrawnCells), "detection rings still respect the cover");
                // The cone paints straight over the cover cell in front of the lens (an 'X' cell, never a node); any floor node it shades without detection has no ring.
                var cover = new GridCoord(4, 1); Assert.IsTrue(stage.IsBlocked(cover), "LEVEL-19 authored cover at (4,1)");
                Assert.IsTrue(Contains(poly, Vector3.Lerp(r.Pos(cover), poly[0], 0.05f)), "the full cone paints over the obstacle itself");
                foreach (var c in stage.FloorNodes())
                    if (Contains(poly, Vector3.Lerp(r.Pos(c), poly[0], 0.001f)) && !detected.Contains(c)) Assert.IsFalse(r.CameraOverlay.Contains(c), "painted-but-covered node " + c + " has no ring");
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void DoorsAndSmoke_ChangeDetectionOnly_NeverTheDrawnGeometry()
        {
            var b = new StageBuilder { Id = 900, Code = "CONE", Title = "cone" }.FromRows(new[] { "###.###", "#.....#", "#.....#", "#.....#", "#######" });
            b.Start = new GridCoord(1, 3); b.Exit = new GridCoord(5, 3); b.Key = new GridCoord(1, 1); b.AlarmBudget = 3; b.SmokeCharges = 1;
            b.Cameras.Add(new CameraSpec("cam", new GridCoord(3, 0), Direction.South, Direction.South, CameraKind.Fixed));
            b.Doors.Add(new DoorSpec(new GridCoord(3, 1), new GridCoord(3, 2)));
            var stage = b.Build(); var r = Build(stage);
            try
            {
                var closed = StageState.Initial(stage);
                Present(r, stage, closed); var polyClosed = (Vector3[])r.CameraCones[0].Clone(); var cellsClosed = new HashSet<GridCoord>(r.CameraOverlay.DrawnCells);
                var opened = new StageState(stage.Start, 0, true, 1, null, null, 1, 0, -1, false, StageResult.Playing, null);
                Present(r, stage, opened); var polyOpen = r.CameraCones[0]; var cellsOpen = new HashSet<GridCoord>(r.CameraOverlay.DrawnCells);
                for (int k = 0; k < 3; k++) Assert.Less(Vector3.Distance(polyClosed[k], polyOpen[k]), 1e-5f, "opening the door leaves the drawn cone identical");
                Assert.IsFalse(cellsClosed.SetEquals(cellsOpen), "but the detected nodes change (the door no longer blocks)");
                Assert.IsTrue(cellsOpen.Contains(new GridCoord(3, 2)) && !cellsClosed.Contains(new GridCoord(3, 2)));
                var smoked = new StageState(stage.Start, 0, false, 0, new[] { new SmokeCloud(new GridCoord(3, 1), 2) }, null, 0, 0, -1, false, StageResult.Playing, null);
                Present(r, stage, smoked); var polySmoke = r.CameraCones[0];
                for (int k = 0; k < 3; k++) Assert.Less(Vector3.Distance(polyClosed[k], polySmoke[k]), 1e-5f, "smoke leaves the drawn cone identical");
                Assert.AreEqual(0, r.CameraOverlay.DrawnCells.Count, "smoke on the lens node blanks the detection");
                // EMP: disabled camera draws no cone at all (existing treatment kept).
                var emp = new StageState(stage.Start, 0, false, 0, null, new[] { new EmpEffect(0, 2) }, 0, 0, -1, false, StageResult.Playing, null);
                Present(r, stage, emp); Assert.AreEqual(0, r.CameraCones.Count); Assert.AreEqual(0, r.CameraOverlay.DrawnCells.Count);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void BoardEdgeCamera_ConeAndRendererBoundsExtendBeyondTheGrid_AndSortAboveObstacles()
        {
            // Camera on the top wall of a 3-wide board facing S: the far corners lie outside the board's x range.
            var b = new StageBuilder { Id = 901, Code = "EDGE", Title = "edge" }.FromRows(new[] { "#..", "...", "...", "..." });
            b.Start = new GridCoord(0, 3); b.Exit = new GridCoord(2, 3); b.AlarmBudget = 3;
            b.Cameras.Add(new CameraSpec("cam", new GridCoord(0, 0), Direction.East, Direction.East, CameraKind.Rotating)); // top-left wall looking along the top row: a far corner lies above the board
            var stage = b.Build(); var r = Build(stage); var lib = r.library;
            try
            {
                Present(r, stage, StageState.Initial(stage));
                var poly = r.CameraCones[0]; float s = r.Layout.NodeSpacing;
                // Grid footprint (drawn): the corner cells plus half a spacing; a far corner must fall outside it.
                var p00 = r.Pos(new GridCoord(0, 0)); var p11 = r.Pos(new GridCoord(stage.Width - 1, stage.Height - 1));
                float gridMinX = Mathf.Min(p00.x, p11.x) - 0.5f * s, gridMaxX = Mathf.Max(p00.x, p11.x) + 0.5f * s, gridMinY = Mathf.Min(p00.y, p11.y) - 0.5f * s, gridMaxY = Mathf.Max(p00.y, p11.y) + 0.5f * s;
                bool Outside(Vector3 q) => q.x < gridMinX - 0.1f * s || q.x > gridMaxX + 0.1f * s || q.y < gridMinY - 0.1f * s || q.y > gridMaxY + 0.1f * s;
                Assert.IsTrue(Outside(poly[1]) || Outside(poly[2]), "a far corner lies beyond the grid footprint (corners " + poly[1] + " / " + poly[2] + ")");
                var fill = r.transform.Find("Overlays/CameraVision/Region_Fill"); Assert.IsNotNull(fill);
                var mesh = fill.GetComponent<MeshFilter>().sharedMesh; var mr = fill.GetComponent<MeshRenderer>();
                var mb = mesh.bounds; mb.Expand(1e-3f); var rb = mr.bounds; rb.Expand(1e-3f); // flat meshes: zero z extent, allow float noise
                Assert.IsTrue(mb.Contains(fill.InverseTransformPoint(r.transform.TransformPoint(poly[1]))) && mb.Contains(fill.InverseTransformPoint(r.transform.TransformPoint(poly[2]))), "mesh bounds include both off-grid corners (" + mb + " vs " + fill.InverseTransformPoint(r.transform.TransformPoint(poly[1])) + ")");
                Assert.IsTrue(rb.Contains(r.transform.TransformPoint(poly[1])) && rb.Contains(r.transform.TransformPoint(poly[2])), "renderer (culling) bounds include the off-grid corners");
                if (lib.fullCameraCone && lib.cameraConeAboveObstacles)
                {
                    Assert.AreEqual(SS2DLayers.Props, mr.sortingLayerName, "cone fill sorts with the props, above obstacle art");
                    Assert.AreEqual(lib.cameraConePropsOrder, mr.sortingOrder);
                    var outline = r.transform.Find("Overlays/CameraVision/Region_Outline").GetComponent<MeshRenderer>();
                    Assert.AreEqual(SS2DLayers.Props, outline.sortingLayerName); Assert.AreEqual(lib.cameraConePropsOrder + 1, outline.sortingOrder);
                    foreach (var sr in r.transform.Find("Props").GetComponentsInChildren<SpriteRenderer>(true)) Assert.Less(sr.sortingOrder, lib.cameraConePropsOrder, "every prop sprite sorts below the cone: " + sr.name);
                    int propsLayer = SortingLayer.GetLayerValueFromName(SS2DLayers.Props), actorsLayer = SortingLayer.GetLayerValueFromName(SS2DLayers.Actors), markers = SortingLayer.GetLayerValueFromName(SS2DLayers.Markers);
                    Assert.Less(propsLayer, actorsLayer, "actors stay above the cone"); Assert.Less(propsLayer, markers, "markers stay above the cone");
                }
                Assert.IsNull(fill.GetComponent<Collider2D>()); Assert.IsNull(fill.GetComponent<Collider>());
                // Rotating camera: all four facings nominal.
                for (int t = 0; t < 4; t++) { Present(r, stage, new StageState(stage.Start, t, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null)); AssertNominal(r, stage, 0, CameraTrack.FacingAt(stage.Cameras[0], t), r.CameraCones[0], "edge t=" + t); }
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void RotatingGuardArcs_FollowTheRealOnScreenTurnDirection_ForBothSchedules()
        {
            var lib = Library(); if (lib.rotationArrowArt == null) Assert.Ignore("rotation arrow art not imported");
            foreach (bool clockwise in new[] { true, false })
                foreach (bool wide in new[] { true, false }) // wide boards are drawn rotated for portrait: the arcs must still match the drawn turn
                {
                    var b = new StageBuilder { Id = 902, Code = "ROT", Title = "rot" }.FromRows(wide ? new[] { "......", "......", "......" } : new[] { "...", "...", "...", "...", "...", "..." });
                    b.Start = new GridCoord(0, 0); b.Exit = wide ? new GridCoord(5, 2) : new GridCoord(2, 5);
                    b.Guards.Add(new GuardSpec("g", GuardKind.Rotating, new[] { new GridCoord(1, 1) }, Direction.North, clockwise));
                    var stage = b.Build(); var r = Build(stage);
                    try
                    {
                        Assert.AreEqual(wide, r.Rotated);
                        var right = r.transform.Find("Markers/RotationMarker_0/Right").GetComponent<SpriteRenderer>(); var left = r.transform.Find("Markers/RotationMarker_0/Left").GetComponent<SpriteRenderer>();
                        // The real next 90° change as drawn: a +90° step in the drawn direction enum is clockwise on screen (N up, E right).
                        var now = r.View(GuardTrack.PoseAt(stage.Guards[0], 0).Facing); var next = r.View(GuardTrack.PoseAt(stage.Guards[0], 1).Facing);
                        bool screenClockwise = next == DirectionUtil.Rotate(now, 1);
                        Assert.AreEqual(clockwise, screenClockwise, "the portrait rotation preserves handedness");
                        Assert.AreEqual(screenClockwise, r.IsScreenClockwise(stage.Guards[0]));
                        Assert.AreEqual(screenClockwise, right.flipX, "unflipped art = anticlockwise; mirrored for a clockwise turn"); Assert.AreEqual(screenClockwise, left.flipX);
                        float a0 = r.RotationFor(GuardTrack.PoseAt(stage.Guards[0], 0).Facing).eulerAngles.z, a1 = r.RotationFor(GuardTrack.PoseAt(stage.Guards[0], 1).Facing).eulerAngles.z;
                        float delta = Mathf.DeltaAngle(a0, a1);
                        Assert.AreEqual(clockwise ? -90f : 90f, delta, 0.01f, "drawn facing turns 90° in the arcs' direction after one action");
                    }
                    finally { Object.DestroyImmediate(r.gameObject); }
                }
        }

        private static bool Contains(Vector3[] poly, Vector3 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
            return inside;
        }
    }
}
