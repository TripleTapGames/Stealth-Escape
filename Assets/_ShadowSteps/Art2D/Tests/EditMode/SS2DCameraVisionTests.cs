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
    /// The camera overlay must show exactly the nodes <see cref="Sight.CameraVision"/> reports for every camera kind
    /// and effect, drawn at the rotated portrait positions: fixed, rotating (90° per counted action), sweeping (two
    /// adjacent facings held for two actions each), blocked by walls / solid cover, blocked by a closed door and
    /// released when it opens, blocked by smoke, EMP-disabled for exactly two counted actions, and overlapping guard
    /// vision (both indicators kept). Lens colour, head rotation and type markers are checked alongside.
    /// </summary>
    public class SS2DCameraVisionTests
    {
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib); Assert.IsNotNull(lib.cameraLens, "camera lens sprite generated"); return lib; }

        private static ShadowSteps2DRenderer Build(Stage stage)
        {
            var go = new GameObject("test-renderer"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = Library(); r.Build(stage); return r;
        }

        /// <summary>Feeds the renderer exactly what the session bridge feeds it for a state.</summary>
        private static void Present(ShadowSteps2DRenderer r, Stage stage, StageState state)
        {
            int t = state.T;
            for (int i = 0; i < stage.Guards.Count; i++) r.SetGuard(i, GuardTrack.PoseAt(stage.Guards[i], t), GuardTrack.NextStep(stage.Guards[i], t));
            for (int i = 0; i < stage.Cameras.Count; i++) r.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], t), state.EmpLeftOn(i), false);
            for (int i = 0; i < stage.Doors.Count; i++) r.SetDoor(i, state.IsDoorOpen(i));
            r.SetClouds(state.Clouds);
            var guard = new List<GridCoord>(); for (int i = 0; i < stage.Guards.Count; i++) guard.AddRange(Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[i], t)));
            var cam = new List<GridCoord>(); for (int i = 0; i < stage.Cameras.Count; i++) cam.AddRange(Sight.CameraVision(stage, state, i, t));
            r.SetVision(guard, cam);
        }

        private static HashSet<GridCoord> EngineCameraCells(Stage stage, StageState state)
        {
            var set = new HashSet<GridCoord>(); for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, state, i, state.T)) set.Add(c); return set;
        }

        private static void AssertOverlayMatchesEngine(ShadowSteps2DRenderer r, Stage stage, StageState state, string label)
        {
            var expected = EngineCameraCells(stage, state);
            Assert.IsTrue(expected.SetEquals(r.CameraOverlay.DrawnCells), label + ": drawn {" + string.Join(",", r.CameraOverlay.DrawnCells) + "} vs engine {" + string.Join(",", expected) + "}");
            foreach (var c in expected) { Assert.IsTrue(stage.IsFloor(c), label + ": only floor nodes"); Assert.IsFalse(stage.IsBlocked(c)); Assert.IsFalse(stage.IsWall(c)); }
            // Node rings sit on the drawn positions of the detected nodes; mesh bounds cover them.
            var fill = r.transform.Find("Overlays/CameraVision/Region_Fill").GetComponent<MeshFilter>().sharedMesh;
            // D-042: the drawn cone is the full nominal geometry (it may paint over covered nodes); it always contains every detected node, and only detected nodes get rings.
            foreach (var c in expected) { Assert.IsTrue(fill.bounds.Contains(r.Pos(c)), label + ": region covers " + c); Assert.IsTrue(PolyContains(r.CameraCones, r.Pos(c)), label + ": cone polygon contains " + c); }
            if (r.library.fullCameraCone) foreach (var poly in r.CameraCones) Assert.AreEqual(3, poly.Length, label + ": nominal cone = apex + two far corners");
            int activeCams = 0; for (int i = 0; i < stage.Cameras.Count; i++) if (!state.IsCameraDisabled(i)) activeCams++;
            Assert.AreEqual(activeCams, r.CameraCones.Count, label + ": one cone per active camera, none for EMP-disabled ones");
            int rings = 0; var ringT = r.transform.Find("Overlays/CameraVision");
            foreach (Transform t in ringT) if (t.name == "NodeRing" && t.gameObject.activeSelf) { rings++; bool onNode = false; foreach (var c in expected) if (Vector3.Distance(t.localPosition, r.Pos(c)) < 1e-3f) onNode = true; Assert.IsTrue(onNode, label + ": ring off a detected node"); }
            int visibleMarkers = 0;
            foreach (var c in expected) if (r.CameraOverlay.ShowsNodeMarker(c)) visibleMarkers++;
            Assert.AreEqual(visibleMarkers, rings, label + ": one ring per visible detected node");
            Assert.IsFalse(r.CameraOverlay.ShowsNodeMarker(stage.Exit), label + ": no camera marker on the exit");
        }

        private static Stage PoleStage()
        {
            var b = new StageBuilder { Id = 990, Code = "T-POLE", Title = "pole", Start = new GridCoord(1, 1), Exit = new GridCoord(5, 3), AlarmBudget = 3 }
                .FromRows(new[] { "#######", "#.....#", "#..#..#", "#.....#", "#######" });
            b.Cameras.Add(new CameraSpec("cam1", new GridCoord(3, 2), Direction.South, Direction.South, CameraKind.Fixed));
            return b.Build();
        }

        /// <summary>No mount or camera sprite may sit on a floor node: every floor node centre stays at least 0.45 spacings from the mount/bracket/head positions.</summary>
        private static void AssertMountClearOfNodes(ShadowSteps2DRenderer r, Stage stage, int cam)
        {
            var holder = r.transform.Find("Props/Camera_" + cam);
            float s = r.Layout.NodeSpacing;
            foreach (var part in new[] { "Mount", "Bracket", "Head" })
            {
                var t = holder.Find(part); if (t == null || !t.gameObject.activeSelf) continue;
                for (int y = 0; y < stage.Height; y++) for (int x = 0; x < stage.Width; x++)
                {
                    var c = new GridCoord(x, y); if (!stage.IsFloor(c)) continue;
                    Assert.Greater(Vector3.Distance(t.position, r.transform.TransformPoint(r.Pos(c))), 0.45f * s, part + " of camera " + cam + " sits on node " + c);
                }
            }
        }

        [Test]
        public void WallMount_Level19_PerimeterCamera_SitsOnAWallStubAtTheEdge_ClearOfNodes()
        {
            var stage = Unlimited(Pack().FindByCode("LEVEL-19")); var r = Build(stage); var cam = stage.Cameras[0];
            try
            {
                Assert.IsFalse(ShadowSteps2DRenderer.UsesPoleMount(stage, cam)); Assert.IsFalse(r.IsPoleMounted(0));
                var mount = r.transform.Find("Props/Camera_0/Mount").GetComponent<SpriteRenderer>();
                var art = r.library.FindLevelArt("LEVEL-19");
                bool stone = r.library.wallSide != null && (art != null && art.FindCamera("cam1") != null && art.FindCamera("cam1").mount == SS2DCameraMountArt.StoneWall || art == null && r.library.preferStoneCameraMounts);
                Assert.AreEqual(stone ? r.library.wallSide : r.library.cameraWallPlate, mount.sprite, stone ? "final stone piece along the rotated boundary" : "generated wall stub");
                if (r.UsesDirectionalCameraArt) Assert.IsNotNull(r.transform.Find("Props/Camera_0/Head/Body"), "final camera art body"); else Assert.IsTrue(r.transform.Find("Props/Camera_0/Bracket").gameObject.activeSelf, "wall bracket shown");
                Assert.IsNull(r.transform.Find("Props/Wall_3_0"), "no grey block under the wall-mounted camera when perimeter walls are hidden");
                // Anchor: the wall-cell edge toward the lens node, after rotation; the stub lies just inside the wall cell.
                var edge = r.Pos(cam.WallCell) + (r.Pos(cam.WallCell.Neighbor(cam.MountFace)) - r.Pos(cam.WallCell)) * 0.5f;
                Assert.Less(Vector3.Distance(r.transform.Find("Props/Camera_0").localPosition, edge), 1e-3f, "camera on the wall edge");
                Assert.Less(Vector3.Dot(mount.transform.localPosition, (r.Pos(cam.WallCell.Neighbor(cam.MountFace)) - r.Pos(cam.WallCell)).normalized), 0f, "mount piece behind the camera, inside the wall cell");
                if (!stone) Assert.Less(Quaternion.Angle(r.RotationFor(cam.MountFace), mount.transform.localRotation), 0.01f, "stub turned with the mount face");
                AssertMountClearOfNodes(r, stage, 0);
                var state = StageState.Initial(stage); Present(r, stage, state); AssertOverlayMatchesEngine(r, stage, state, "LEVEL-19 wall mount");
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void PoleMount_LoneWallCell_UsesAnUprightPost_HidesTheBlock_AndKeepsTheEngineRegion()
        {
            var stage = PoleStage(); var r = Build(stage); var cam = stage.Cameras[0];
            try
            {
                Assert.IsTrue(ShadowSteps2DRenderer.UsesPoleMount(stage, cam)); Assert.IsTrue(r.IsPoleMounted(0));
                var mount = r.transform.Find("Props/Camera_0/Mount").GetComponent<SpriteRenderer>();
                Assert.AreEqual(r.library.cameraPole, mount.sprite, "pole sprite");
                Assert.IsFalse(r.transform.Find("Props/Camera_0/Bracket").gameObject.activeSelf, "no wall bracket on a pole");
                Assert.IsNull(r.transform.Find("Props/Wall_3_2"), "the pole base replaces the grey block");
                Assert.Less(Quaternion.Angle(Quaternion.identity, mount.transform.localRotation), 0.01f, "post stays screen-upright (board is rotated for this wide stage)");
                Assert.IsTrue(r.Rotated);
                // Base of the post on the blocked cell centre; head at the top of the post.
                Assert.Less(Vector3.Distance(mount.transform.position, r.transform.TransformPoint(r.Pos(cam.WallCell))), 1e-3f, "post base on the cell");
                Assert.Less(Vector3.Distance(r.transform.Find("Props/Camera_0").localPosition, r.Pos(cam.WallCell) + Vector3.up * r.library.cameraPoleHeight * r.Layout.NodeSpacing), 1e-3f);
                AssertMountClearOfNodes(r, stage, 0);
                var state = StageState.Initial(stage); Present(r, stage, state);
                Assert.Less(Quaternion.Angle(r.RotationFor(Direction.South), r.CameraHeads[0].localRotation), 0.01f, "head faces the logical facing");
                AssertOverlayMatchesEngine(r, stage, state, "pole mount");
                CollectionAssert.AreEquivalent(new[] { new GridCoord(3, 3) }, r.CameraOverlay.DrawnCells, "cone: only the lens node below the lone wall (its neighbours sit at 63°, row 4 is wall)");
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void PerimeterWalls_DrawWhenEnabled_AndHideWhenTheBackgroundPaintsThem()
        {
            var stage = Unlimited(Pack().FindByCode("LEVEL-19"));
            var lib = Object.Instantiate(Library()); lib.showPerimeterWalls = true;
            var go = new GameObject("test-renderer-walls"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = lib;
            try
            {
                r.Build(stage);
                Assert.IsNotNull(r.transform.Find("Props/Wall_0_0")); Assert.IsNotNull(r.transform.Find("Props/Wall_3_0"), "wall block drawn under the camera when perimeter walls are on");
                Assert.IsNotNull(r.transform.Find("Props/Camera_0/Mount"), "the wall stub is still drawn on top of it");
                lib.showPerimeterWalls = false; r.Build(stage);
                Assert.IsNull(r.transform.Find("Props/Wall_0_0")); Assert.IsNull(r.transform.Find("Props/Wall_3_0"));
                Assert.IsNotNull(r.transform.Find("Props/Camera_0/Mount"));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(lib); }
        }

        /// <summary>Point-in-polygon (even-odd) against any of the drawn cone fans. The point is pulled 0.1% toward the apex first: a node exactly on the forward-reach edge (forward = 2.0, accepted by the engine's tolerance) counts as covered.</summary>
        private static bool PolyContains(IReadOnlyList<Vector3[]> cones, Vector3 point)
        {
            foreach (var poly in cones)
            {
                var p = poly[0] + (point - poly[0]) * 0.999f;
                bool inside = false; int n = poly.Length;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    var a = poly[i]; var b = poly[j];
                    if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                }
                if (inside) return true;
            }
            return false;
        }

        [Test]
        public void ConeOutline_FollowsThePortraitRotation_AndTheLensDirection()
        {
            // LEVEL-17 is wide (rotated): camera on the right wall facing W. The drawn apex is the rotated wall-face point and the cone's
            // central ray points along the drawn facing, two spacings out; the head sprite turns the same way.
            var stage = Pack().FindByCode("LEVEL-17"); var r = Build(stage); var cam = stage.Cameras[0];
            try
            {
                Assert.IsTrue(r.Rotated);
                var state = StageState.Initial(stage); Present(r, stage, state);
                Assert.AreEqual(1, r.CameraCones.Count);
                var poly = r.CameraCones[0];
                var origin = Sight.CameraOrigin(cam);
                Assert.Less(Vector3.Distance(poly[0], r.PosF(origin.X, origin.Y)), 1e-4f, "apex on the rotated wall face");
                var facing = CameraTrack.FacingAt(cam, 0);
                var drawnDir = r.RotationFor(facing) * Vector3.up;
                if (r.library.fullCameraCone)
                {
                    Assert.AreEqual(3, poly.Length, "nominal cone: apex + two far corners (D-042)");
                    var centre = (poly[1] + poly[2]) * 0.5f;
                    var expectedCentre = poly[0] + drawnDir * (float)Sight.CameraRange * r.Layout.NodeSpacing;
                    Assert.Less(Vector3.Distance(centre, expectedCentre), 1e-3f, "far edge centred along the drawn facing, never clipped");
                    float halfWidth = (float)(Sight.CameraRange * System.Math.Tan(Sight.CameraHalfAngleDegrees * System.Math.PI / 180.0)) * r.Layout.NodeSpacing;
                    Assert.AreEqual(halfWidth * 2f, Vector3.Distance(poly[1], poly[2]), 1e-3f, "70° total: far edge width = 2·D·tan 35°");
                }
                else
                {
                    Assert.AreEqual(Sight.ConeOutlineRays + 2, poly.Length, "traced silhouette (D-054): apex + one point per ray");
                    var central = poly[1 + Sight.ConeOutlineRays / 2];
                    Assert.Less(Vector3.Distance(central, poly[0] + drawnDir * (float)Sight.CameraRange * r.Layout.NodeSpacing), 1e-3f, "nothing in front of this camera: the central ray reaches the full radius along the drawn facing");
                }
                AssertOverlayMatchesEngine(r, stage, state, "LEVEL-17 rotated cone");
                Assert.Less(Quaternion.Angle(r.RotationFor(facing), r.CameraHeads[0].localRotation), 0.01f);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void LevelArt_Level19_HasNoCoverCells_AndAStoneCameraMount()
        {
            // D-074 (2026-11-10): the redesigned "Sweep Window" is a 3x3 long-edge board with no X cells; the camera moved to the bottom wall (3,6).
            var stage = Unlimited(Pack().FindByCode("LEVEL-19")); var lib = Library();
            Assert.IsNotNull(lib.bush, "obstacle bush imported"); Assert.IsNotNull(lib.wallSide); Assert.IsNotNull(lib.wallFront);
            var art = lib.FindLevelArt("LEVEL-19"); Assert.IsNotNull(art, "LEVEL-19 layout registered on the library");
            var r = Build(stage);
            try
            {
                Assert.IsTrue(r.Rotated); Assert.AreSame(art, r.ArtLayout);
                // Gameplay data drives the cells: exactly the authored X cells get a cover sprite, nothing else.
                var covers = new List<Transform>(); foreach (Transform t in r.transform.Find("Props")) if (t.name.StartsWith("Cover_X_") && !t.name.EndsWith("_Shadow")) covers.Add(t);
                Assert.AreEqual(stage.BlockedCount, covers.Count); Assert.AreEqual(0, covers.Count, "D-074 board has no cover cells");
                Assert.AreEqual(1, stage.Guards.Count, "the rotating guard beside the camera");
                // Camera: logical anchor unchanged (wall-face edge), mounted on the stone side piece (drawn vertical along the rotated boundary).
                var cam = stage.Cameras[0];
                var edge = r.Pos(cam.WallCell) + (r.Pos(cam.WallCell.Neighbor(cam.MountFace)) - r.Pos(cam.WallCell)) * 0.5f;
                Assert.Less(Vector3.Distance(r.transform.Find("Props/Camera_0").localPosition, edge), 1e-3f);
                var mount = r.transform.Find("Props/Camera_0/Mount").GetComponent<SpriteRenderer>();
                Assert.AreEqual(lib.wallSide, mount.sprite, "stone piece along the boundary (logical X) is drawn vertical → side piece");
                Assert.IsFalse(r.IsPoleMounted(0));
                // Final cast shadow under the player (and guards): the user's shadow art, pivot on the base disc, screen-space (not rotated), under the body.
                if (lib.characterCastShadow != null)
                {
                    Assert.IsTrue(r.Player.shadow.gameObject.activeSelf, "cast shadow shown even though the frames include a base disc");
                    Assert.AreEqual(lib.characterCastShadow, r.Player.shadow.sprite);
                    Assert.AreEqual(Vector3.zero, r.Player.shadow.transform.localPosition);
                    Assert.Less(Quaternion.Angle(Quaternion.identity, r.Player.shadow.transform.localRotation), 0.01f);
                    Assert.Less(r.Player.shadow.sortingOrder, r.Player.body.sortingOrder, "shadow under the body");
                }
                var state = StageState.Initial(stage); Present(r, stage, state); AssertOverlayMatchesEngine(r, stage, state, "LEVEL-19 with level art");
                CollectionAssert.AreEquivalent(Sight.CameraVision(stage, state, 0, 0), r.CameraOverlay.DrawnCells, "decoration never changes detection");
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void LevelArt_StonePieceOrientation_FollowsTheDrawnAxis_UnrotatedBoardUsesTheSidePiece()
        {
            var stage = Unlimited(Pack().FindByCode("LEVEL-19"));
            var go = new GameObject("test-renderer-unrotated"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = Library(); r.autoRotatePortrait = false;
            try
            {
                r.Build(stage); Assert.IsFalse(r.Rotated);
                Assert.AreEqual(r.library.wallSide, r.transform.Find("Props/Cover_X_4_1").GetComponent<SpriteRenderer>().sprite, "logical Y run drawn vertical on an unrotated board");
                Assert.AreEqual(r.library.wallFront, r.transform.Find("Props/Camera_0/Mount").GetComponent<SpriteRenderer>().sprite, "the boundary (logical X) is drawn horizontal → front piece");
                Assert.AreEqual(2, stage.BlockedCount);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LevelArt_LevelsWithoutALayout_UseTheDefaultObstacleArt()
        {
            var stage = Pack().FindByCode("LEVEL-16"); var lib = Library();
            Assert.IsNull(lib.FindLevelArt("LEVEL-16"));
            var r = Build(stage);
            try
            {
                var expected = lib.defaultObstacleArt == SS2DObstacleArt.BushPlanter ? lib.bush : lib.crate;
                foreach (Transform t in r.transform.Find("Props")) if (t.name.StartsWith("Cover_X_") && !t.name.EndsWith("_Shadow")) Assert.AreEqual(expected, t.GetComponent<SpriteRenderer>().sprite, t.name);
                Assert.AreEqual(stage.BlockedCount, CountCovers(r));
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void InteriorWalls_Level23_DrawAsAStoneRunHuggingTheCameraFace_NoGreyBlocks_CellsUnchanged()
        {
            var stage = Pack().FindByCode("LEVEL-23"); var lib = Library();
            Assert.AreEqual(SS2DObstacleArt.StoneWall, lib.interiorWallArt);
            var r = Build(stage);
            try
            {
                int interior = 0, stone = 0, grey = 0;
                for (int y = 1; y < stage.Height - 1; y++) for (int x = 1; x < stage.Width - 1; x++)
                {
                    var c = new GridCoord(x, y); if (!stage.IsWall(c)) continue; interior++;
                    var t = r.transform.Find("Props/Wall_" + x + "_" + y); Assert.IsNotNull(t, "interior wall " + c + " drawn");
                    var sr = t.GetComponent<SpriteRenderer>();
                    if (sr.sprite == lib.wallSide || sr.sprite == lib.wallFront) stone++; else if (sr.sprite == lib.wall) grey++;
                    Assert.IsTrue(r.IsStoneWallCell(c));
                    // The run runs along logical X (row 1) and is slid toward the camera's face (S) by the hug distance; still inside the wall cell.
                    var expected = r.Pos(c) + (r.Pos(new GridCoord(x, y + 1)) - r.Pos(c)) * lib.wallRunCameraHug;
                    Assert.Less(Vector3.Distance(t.localPosition, expected), 1e-4f, "piece " + c + " hugged toward the corridor");
                }
                Assert.AreEqual(5, interior); Assert.AreEqual(5, stone); Assert.AreEqual(0, grey, "no grey placeholder blocks");
                Assert.AreEqual(lib.wallSide, r.transform.Find("Props/Wall_2_1").GetComponent<SpriteRenderer>().sprite, "row run drawn vertical on the rotated board → side piece");
                // Camera on the run: no separate mount piece, logical anchor unchanged, detection unchanged.
                var cam = stage.Cameras[0]; Assert.IsTrue(r.IsStoneWallCell(cam.WallCell));
                Assert.IsFalse(r.transform.Find("Props/Camera_0/Mount").gameObject.activeSelf, "the run is the mounting surface");
                var edge = r.Pos(cam.WallCell) + (r.Pos(cam.WallCell.Neighbor(cam.MountFace)) - r.Pos(cam.WallCell)) * 0.5f;
                Assert.Less(Vector3.Distance(r.transform.Find("Props/Camera_0").localPosition, edge), 1e-3f);
                var state = StageState.Initial(stage); Present(r, stage, state); AssertOverlayMatchesEngine(r, stage, state, "LEVEL-23 stone run");
                // Rules untouched: every authored wall is still a wall, nothing else is.
                int walls = 0; for (int y = 0; y < stage.Height; y++) for (int x = 0; x < stage.Width; x++) if (stage.IsWall(new GridCoord(x, y))) walls++;
                Assert.AreEqual(22, walls, "rows '#######','#######','......#','#######' → 7 + 7 + 1 + 7 wall cells");
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void InteriorWalls_LoneCells_BecomePlanters()
        {
            var stage = Pack().FindByCode("LEVEL-11"); var lib = Library();
            var r = Build(stage);
            try
            {
                foreach (var c in new[] { new GridCoord(3, 1), new GridCoord(3, 3) })
                {
                    Assert.IsTrue(stage.IsWall(c));
                    var sr = r.transform.Find("Props/Wall_" + c.X + "_" + c.Y).GetComponent<SpriteRenderer>();
                    Assert.AreEqual(lib.bush, sr.sprite, "lone interior wall " + c + " drawn as a planter");
                    Assert.Less(Vector3.Distance(sr.transform.localPosition, r.Pos(c)), 1e-4f);
                    Assert.IsFalse(r.IsStoneWallCell(c));
                }
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void GuardFan_IsAlwaysTwoSquaresLong_RingsFollowTheRule()
        {
            // 7×5 room, static guard at (1,2) facing E: sees (2,2) and (3,2). D-051: the fan is a fixed two-square sector (plus overshoot) whatever lies ahead; only the rings follow the rule.
            var open = new StageBuilder { Id = 991, Code = "T-GUARDFAN", Title = "fan", Start = new GridCoord(1, 1), Exit = new GridCoord(5, 3), AlarmBudget = 3 }.FromRows(new[] { "#######", "#.....#", "#.....#", "#.....#", "#######" });
            open.Guards.Add(new GuardSpec("g1", GuardKind.Static, new[] { new GridCoord(1, 2) }, Direction.East));
            var stage = open.Build();
            var go = new GameObject("test-renderer-fan"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = Library(); r.autoRotatePortrait = false; r.Build(stage);
            try
            {
                Assert.IsFalse(r.Rotated);
                var state = StageState.Initial(stage); var pose = GuardTrack.PoseAt(stage.Guards[0], 0);
                r.SetGuard(0, pose, null); r.SetVision(Sight.GuardVision(stage, state, pose), new List<GridCoord>());
                Assert.AreEqual(1, r.GuardCones.Count); var fan = r.GuardCones[0];
                Assert.Less(Vector3.Distance(fan[0], r.Pos(new GridCoord(1, 2))), 1e-4f);
                Assert.IsTrue(PolyContains(r.GuardCones, r.Pos(new GridCoord(2, 2)))); Assert.IsTrue(PolyContains(r.GuardCones, r.Pos(new GridCoord(3, 2))));
                Assert.IsFalse(PolyContains(r.GuardCones, r.Pos(new GridCoord(4, 2))), "two nodes only"); Assert.IsFalse(PolyContains(r.GuardCones, r.Pos(new GridCoord(2, 1)))); Assert.IsFalse(PolyContains(r.GuardCones, r.Pos(new GridCoord(3, 3))));
                float farX = float.MinValue; foreach (var p in fan) farX = Mathf.Max(farX, p.x);
                Assert.AreEqual(r.Pos(new GridCoord(1, 2)).x + (2f + r.library.guardConeOvershoot) * r.Layout.NodeSpacing, farX, 1e-3f, "fan ends 'guardConeOvershoot' spacings past the second square");
                // Library controls: narrower angle and zero overshoot shrink the fan without changing the rings (the rule).
                var lib2 = Object.Instantiate(r.library); lib2.guardConeHalfAngleDegrees = 12f; lib2.guardConeOvershoot = 0f;
                var go3 = new GameObject("test-renderer-fan3"); var r3 = go3.AddComponent<ShadowSteps2DRenderer>(); r3.library = lib2; r3.autoRotatePortrait = false; r3.Build(stage);
                try
                {
                    r3.SetGuard(0, pose, null); r3.SetVision(Sight.GuardVision(stage, state, pose), new List<GridCoord>());
                    float far3 = float.MinValue, top3 = float.MinValue; foreach (var p in r3.GuardCones[0]) { far3 = Mathf.Max(far3, p.x); top3 = Mathf.Max(top3, p.y); }
                    Assert.AreEqual(r3.Pos(new GridCoord(3, 2)).x, far3, 1e-3f, "zero overshoot: fan ends on the second square's centre");
                    Assert.Less(top3 - r3.Pos(new GridCoord(1, 2)).y, 2.0f * Mathf.Tan(12f * Mathf.Deg2Rad) * r3.Layout.NodeSpacing + 1e-3f, "12° half-angle: lateral extent follows the library");
                    CollectionAssert.AreEquivalent(Sight.GuardVision(stage, state, pose), r3.GuardOverlay.DrawnCells, "rings are the rule's nodes regardless of the fan settings");
                }
                finally { Object.DestroyImmediate(go3); Object.DestroyImmediate(lib2); }
                // Smoke on the guard: nothing seen (no rings), but the fixed fan still shows where he looks.
                var smoked = new StageState(new GridCoord(1, 1), 0, false, 0, new[] { new SmokeCloud(new GridCoord(1, 2), 2) }, null, 0, 0, -1, false, StageResult.Playing, null);
                r.SetClouds(smoked.Clouds); r.SetVision(Sight.GuardVision(stage, smoked, pose), new List<GridCoord>());
                Assert.AreEqual(1, r.GuardCones.Count); Assert.AreEqual(0, r.GuardOverlay.DrawnCells.Count);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
            var walled = new StageBuilder { Id = 992, Code = "T-GUARDFAN2", Title = "fan", Start = new GridCoord(1, 1), Exit = new GridCoord(5, 3), AlarmBudget = 3 }.FromRows(new[] { "#######", "#.....#", "#..#..#", "#.....#", "#######" });
            walled.Guards.Add(new GuardSpec("g1", GuardKind.Static, new[] { new GridCoord(1, 2) }, Direction.East));
            var ws = walled.Build();
            var go2 = new GameObject("test-renderer-fan2"); var r2 = go2.AddComponent<ShadowSteps2DRenderer>(); r2.library = Library(); r2.autoRotatePortrait = false; r2.Build(ws);
            try
            {
                var pose = GuardTrack.PoseAt(ws.Guards[0], 0); var seen = Sight.GuardVision(ws, StageState.Initial(ws), pose);
                CollectionAssert.AreEquivalent(new[] { new GridCoord(2, 2) }, seen);
                r2.SetGuard(0, pose, null); r2.SetVision(seen, new List<GridCoord>());
                float farX = float.MinValue; foreach (var p in r2.GuardCones[0]) farX = Mathf.Max(farX, p.x);
                Assert.AreEqual(r2.Pos(new GridCoord(1, 2)).x + (2f + r2.library.guardConeOvershoot) * r2.Layout.NodeSpacing, farX, 1e-3f, "D-051: the wall does not shorten the drawn fan; only the rings stop at it");
                Assert.IsTrue(PolyContains(r2.GuardCones, r2.Pos(new GridCoord(3, 2)))); CollectionAssert.AreEquivalent(new[] { new GridCoord(2, 2) }, r2.GuardOverlay.DrawnCells);
            }
            finally { Object.DestroyImmediate(r2.gameObject); }
        }

        [Test]
        public void DirectionalCameraArt_SwapsTheSpritePerDrawnFacing_StaysUpright_AndTintsWhenDisabled()
        {
            var stage = Unlimited(Pack().FindByCode("LEVEL-18")); var lib = Library();
            if (!lib.HasCameraArt) Assert.Ignore("no final camera art imported");
            var r = Build(stage); var cam = stage.Cameras[0];
            try
            {
                var body = r.transform.Find("Props/Camera_0/Head/Body").GetComponent<SpriteRenderer>();
                Assert.IsFalse(r.transform.Find("Props/Camera_0/Head/Lens").gameObject.activeSelf, "lens is baked into the art");
                Assert.IsFalse(r.transform.Find("Props/Camera_0/Bracket").gameObject.activeSelf);
                var state = StageState.Initial(stage);
                for (int t = 0; t < 4; t++)
                {
                    Present(r, stage, state);
                    var facing = CameraTrack.FacingAt(cam, t);
                    Assert.AreEqual(lib.cameraArt[(int)r.View(facing)], body.sprite, "sprite for the drawn facing at t=" + t);
                    Assert.Less(Quaternion.Angle(Quaternion.identity, body.transform.rotation), 0.01f, "body upright on screen at t=" + t);
                    Assert.Less(Quaternion.Angle(r.RotationFor(facing), r.CameraHeads[0].localRotation), 0.01f, "logical head rotation kept");
                    state = After(stage, state, "WAIT");
                }
                Assert.AreEqual(Color.white, body.color);
                r.SetCamera(0, cam.Facing, 2, false); Assert.AreEqual(lib.cameraArtDisabledTint, body.color, "EMP tint on the body");
                r.SetCamera(0, cam.Facing, 0, false); Assert.AreEqual(Color.white, body.color);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        private static int CountCovers(ShadowSteps2DRenderer r) { int n = 0; foreach (Transform t in r.transform.Find("Props")) if (t.name.StartsWith("Cover_X_") && !t.name.EndsWith("_Shadow")) n++; return n; }

        /// <summary>Copy of a shipped stage without its WAIT limit: these tests step the camera schedules with many WAITs (the limit itself is covered by the core tests).</summary>
        private static Stage Unlimited(Stage stage) { var b = stage.ToBuilder(); b.WaitLimit = -1; return b.Build(); }

        private static StageState After(Stage stage, StageState state, params string[] tokens)
        {
            foreach (var tok in tokens) { Assert.IsTrue(StageAction.TryParse(tok, out var a), tok); var o = StageRules.TryApply(stage, state, a); Assert.IsTrue(o.Valid, tok + " rejected: " + o.Reason); state = o.After; }
            return state;
        }

        [Test]
        public void FixedCamera_Level16_MatchesEngine_AndIsBlockedBySolidCover()
        {
            var stage = Unlimited(Pack().FindByCode("LEVEL-16")); var r = Build(stage); // shipped stages have no WAIT (D-044); this art test steps the clock with WAITs on an unlimited copy
            try
            {
                var state = StageState.Initial(stage); Present(r, stage, state);
                AssertOverlayMatchesEngine(r, stage, state, "LEVEL-16 t0");
                Assert.IsTrue(r.Rotated, "7×5 stage drawn rotated");
                Assert.IsFalse(r.CameraOverlay.Contains(new GridCoord(3, 2)), "solid cover X at (3,2) is never highlighted");
                Assert.Greater(r.CameraOverlay.DrawnCells.Count, 0);
                // Same area after a WAIT (fixed camera, nothing else changed).
                var s1 = After(stage, state, "WAIT"); Present(r, stage, s1);
                Assert.IsTrue(EngineCameraCells(stage, state).SetEquals(r.CameraOverlay.DrawnCells));
                // Lens amber, head rotation = drawn facing.
                var lens = r.transform.Find("Props/Camera_0/Head/Lens").GetComponent<SpriteRenderer>(); Assert.AreEqual(r.library.cameraLensActive, lens.color);
                Assert.Less(Quaternion.Angle(r.RotationFor(Direction.South), r.CameraHeads[0].localRotation), 0.01f);
                Assert.IsFalse(r.transform.Find("Props/Camera_0/TypeMarker").gameObject.activeSelf, "fixed camera has no type marker");
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void RotatingCamera_Level18_TurnsEveryCountedAction()
        {
            var stage = Unlimited(Pack().FindByCode("LEVEL-18")); var r = Build(stage); var cam = stage.Cameras[0];
            try
            {
                Assert.AreEqual(CameraKind.Rotating, cam.Kind);
                var state = StageState.Initial(stage);
                var seen = new List<HashSet<GridCoord>>();
                for (int t = 0; t < 5; t++)
                {
                    Present(r, stage, state);
                    AssertOverlayMatchesEngine(r, stage, state, "LEVEL-18 t" + t);
                    Assert.AreEqual(DirectionUtil.Rotate(cam.Facing, cam.Clockwise ? t : -t), CameraTrack.FacingAt(cam, t), "one quarter turn per counted action in the authored direction");
                    Assert.Less(Quaternion.Angle(r.RotationFor(CameraTrack.FacingAt(cam, t)), r.CameraHeads[0].localRotation), 0.01f, "head turned with the facing at t=" + t);
                    seen.Add(new HashSet<GridCoord>(r.CameraOverlay.DrawnCells));
                    state = After(stage, state, "WAIT");
                }
                Assert.IsTrue(seen[0].SetEquals(seen[4]), "period 4");
                var marker = r.transform.Find("Props/Camera_0/TypeMarker").GetComponent<SpriteRenderer>();
                Assert.IsTrue(marker.gameObject.activeSelf); Assert.AreEqual(r.library.rotationMarker, marker.sprite); Assert.AreEqual(!cam.Clockwise, marker.flipX);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void CameraBodyAndTypeMarker_DrawOnTheCameraVisionLayer_AboveTheCone()
        {
            var stage = Pack().FindByCode("LEVEL-18"); var r = Build(stage);
            try
            {
                if (!r.UsesDirectionalCameraArt) Assert.Ignore("the library has no directional camera art, so there is no Body sprite");
                Present(r, stage, StageState.Initial(stage));
                var body = r.transform.Find("Props/Camera_0/Head/Body").GetComponent<SpriteRenderer>();
                var marker = r.transform.Find("Props/Camera_0/TypeMarker").GetComponent<SpriteRenderer>();
                Assert.AreEqual(SS2DLayers.CameraVision, body.sortingLayerName, "the camera body shares the Camera Vision layer");
                Assert.AreEqual(r.library.cameraConePropsOrder + SS2DLayers.CameraArtworkOrderOffset, body.sortingOrder);
                Assert.AreEqual(SS2DLayers.CameraVision, marker.sortingLayerName, "the type marker sits on the same layer as the body");
                Assert.AreEqual(body.sortingOrder + 1, marker.sortingOrder);
                var fill = r.transform.Find("Overlays/CameraVision/Region_Fill").GetComponent<MeshRenderer>();
                var outline = r.transform.Find("Overlays/CameraVision/Region_Outline").GetComponent<MeshRenderer>();
                if (r.library.cameraConeAboveObstacles)
                {
                    Assert.AreEqual(SS2DLayers.CameraVision, fill.sortingLayerName, "the cone draws on the Camera Vision layer");
                    Assert.Greater(body.sortingOrder, outline.sortingOrder, "the camera artwork stays in front of the cone fill and outline");
                }
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void SweepingCamera_Level19_HoldsEachFacingForTwoActions()
        {
            var stage = Unlimited(Pack().FindByCode("LEVEL-19")); var r = Build(stage); var cam = stage.Cameras[0];
            try
            {
                Assert.AreEqual(CameraKind.Sweeping, cam.Kind);
                var state = StageState.Initial(stage); var facings = new List<Direction>();
                for (int t = 0; t < 6; t++)
                {
                    Present(r, stage, state); AssertOverlayMatchesEngine(r, stage, state, "LEVEL-19 t" + t);
                    facings.Add(CameraTrack.FacingAt(cam, t));
                    Assert.Less(Quaternion.Angle(r.RotationFor(facings[t]), r.CameraHeads[0].localRotation), 0.01f);
                    state = After(stage, state, "WAIT");
                }
                var other = DirectionUtil.Rotate(cam.Facing, cam.Clockwise ? 1 : -1);
                // D-073: a 3-facing sweep goes on to the far facing (A A B B C C) instead of coming straight back (A A B B A A).
                var third = cam.SweepFacings >= 3 ? DirectionUtil.Rotate(cam.Facing, cam.Clockwise ? 2 : -2) : cam.Facing;
                Assert.AreEqual(3, cam.SweepFacings, "LEVEL-19 ships the 3-facing sweep (D-074)");
                CollectionAssert.AreEqual(new[] { cam.Facing, cam.Facing, other, other, third, third }, facings, "adjacent facings, two actions each");
                var marker = r.transform.Find("Props/Camera_0/TypeMarker").GetComponent<SpriteRenderer>();
                Assert.IsTrue(marker.gameObject.activeSelf); Assert.AreEqual(r.library.sweepMarker, marker.sprite);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void ClosedDoor_DoesNotBlockCameraVision()
        {
            // 5×4: camera on the north wall (2,0) looking south down column 2; a door between (2,1) and (2,2).
            var b = new StageBuilder { Id = 901, Code = "T-DOOR", Title = "door", Start = new GridCoord(0, 2), Exit = new GridCoord(4, 2), Key = new GridCoord(0, 1), AlarmBudget = 3 };
            b.FromRows(new[] { "#####", ".....", ".....", "....." });
            b.Doors.Add(new DoorSpec(new GridCoord(2, 1), new GridCoord(2, 2)));
            b.Cameras.Add(new CameraSpec("c", new GridCoord(2, 0), Direction.South, Direction.South, CameraKind.Fixed));
            var stage = b.Build(); var r = Build(stage);
            try
            {
                var closed = StageState.Initial(stage); Present(r, stage, closed); AssertOverlayMatchesEngine(r, stage, closed, "door closed");
                var opened = new StageState(closed.Player, 0, true, 1, null, null, 0, 0, -1, false, StageResult.Playing, null); Present(r, stage, opened); AssertOverlayMatchesEngine(r, stage, opened, "door open");
                var seenClosed = EngineCameraCells(stage, closed); var seenOpen = EngineCameraCells(stage, opened);
                CollectionAssert.AreEquivalent(seenClosed, seenOpen);
                Assert.IsTrue(seenClosed.Contains(new GridCoord(2, 2)));
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void Smoke_BlocksCameraVision_ThroughTheRealSmokeAction()
        {
            var b = new StageBuilder { Id = 902, Code = "T-SMOKE", Title = "smoke", Start = new GridCoord(2, 3), Exit = new GridCoord(4, 3), SmokeCharges = 1, AlarmBudget = 3 };
            b.FromRows(new[] { "#####", ".....", ".....", "....." });
            b.Cameras.Add(new CameraSpec("c", new GridCoord(2, 0), Direction.South, Direction.South, CameraKind.Fixed));
            var stage = b.Build(); var r = Build(stage);
            try
            {
                var s0 = StageState.Initial(stage); Present(r, stage, s0); AssertOverlayMatchesEngine(r, stage, s0, "no smoke");
                var before = EngineCameraCells(stage, s0); Assert.IsTrue(before.Contains(new GridCoord(2, 2)), "footprint starts one node out from the lens: (2,2) is the first seen node");
                var s1 = After(stage, s0, "SMOKE@2,2"); Present(r, stage, s1); AssertOverlayMatchesEngine(r, stage, s1, "smoked");
                Assert.AreEqual(1, s1.Clouds.Count); Assert.IsFalse(r.CameraOverlay.Contains(new GridCoord(2, 2)), "smoked node not highlighted");
                Assert.Less(r.CameraOverlay.DrawnCells.Count, before.Count);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void Emp_DisablesForExactlyTwoCountedActions_ThenRestores()
        {
            var stage = Unlimited(Pack().FindByCode("LEVEL-23")); var r = Build(stage); // unlimited copy: the EMP countdown is stepped with WAITs (no WAIT in the shipped game, D-044)
            try
            {
                var s0 = StageState.Initial(stage); Present(r, stage, s0); AssertOverlayMatchesEngine(r, stage, s0, "active");
                var active = EngineCameraCells(stage, s0); Assert.Greater(active.Count, 0);
                var lens = r.transform.Find("Props/Camera_0/Head/Lens").GetComponent<SpriteRenderer>();
                var s1 = After(stage, s0, "EMP#0"); Present(r, stage, s1);
                Assert.AreEqual(2, s1.EmpLeftOn(0)); Assert.AreEqual(0, r.CameraOverlay.DrawnCells.Count, "disabled camera draws no region"); Assert.AreEqual(r.library.cameraLensDisabled, lens.color);
                Assert.IsTrue(r.transform.Find("Props/Camera_0/Disabled").gameObject.activeSelf); Assert.IsTrue(r.transform.Find("Props/Camera_0/Pip0").gameObject.activeSelf); Assert.IsTrue(r.transform.Find("Props/Camera_0/Pip1").gameObject.activeSelf);
                var s2 = After(stage, s1, "WAIT"); Present(r, stage, s2);
                Assert.AreEqual(1, s2.EmpLeftOn(0)); Assert.AreEqual(0, r.CameraOverlay.DrawnCells.Count); Assert.IsFalse(r.transform.Find("Props/Camera_0/Pip1").gameObject.activeSelf);
                var s3 = After(stage, s2, "WAIT"); Present(r, stage, s3);
                Assert.AreEqual(0, s3.EmpLeftOn(0)); Assert.IsTrue(active.SetEquals(r.CameraOverlay.DrawnCells), "vision restored after two counted actions"); Assert.AreEqual(r.library.cameraLensActive, lens.color);
                Assert.IsFalse(r.transform.Find("Props/Camera_0/Disabled").gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void TurningCameraMarkers_StayCenteredAboveTheArtworkInEveryFacing()
        {
            var stage = Pack().FindByCode("LEVEL-30");
            var renderer = Build(stage);
            try
            {
                if (!renderer.UsesDirectionalCameraArt) Assert.Ignore("No directional camera artwork.");
                for (int i = 0; i < stage.Cameras.Count; i++)
                {
                    if (stage.Cameras[i].Kind == CameraKind.Fixed) continue;
                    var body = renderer.transform.Find("Props/Camera_" + i + "/Head/Body").GetComponent<SpriteRenderer>();
                    var marker = renderer.transform.Find("Props/Camera_" + i + "/TypeMarker").GetComponent<SpriteRenderer>();
                    Assert.IsTrue(marker.gameObject.activeSelf);
                    for (int facing = 0; facing < 4; facing++)
                    {
                        renderer.SetCameraVisualFacing(i, (Direction)facing);
                        var vertices = body.sprite.vertices;
                        var visibleArt = new Bounds(body.transform.TransformPoint(vertices[0]), Vector3.zero);
                        foreach (var vertex in vertices) visibleArt.Encapsulate(body.transform.TransformPoint(vertex));
                        Assert.AreEqual(visibleArt.center.x, marker.bounds.center.x, 1e-4f);
                        Assert.Greater(marker.bounds.min.y, visibleArt.max.y);
                        Assert.AreEqual(body.sortingLayerID, marker.sortingLayerID);
                        Assert.Greater(marker.sortingOrder, body.sortingOrder);
                    }
                }
            }
            finally { Object.DestroyImmediate(renderer.gameObject); }
        }

        [Test]
        public void DetectedPlayer_DrawsUnderCameraFill_AndReturnsToActorsWhenCameraIsDisabled()
        {
            var stage = Pack().FindByCode("LEVEL-16");
            var state = StageState.Initial(stage);
            var detected = new List<GridCoord>(Sight.CameraVision(stage, state, 0, state.T));
            Assert.IsNotEmpty(detected);
            var renderer = Build(stage);
            try
            {
                Present(renderer, stage, state);
                var fill = renderer.CameraOverlay.transform.Find("Region_Fill").GetComponent<MeshRenderer>();
                // Touchdown moves the player before the vision state is republished.
                renderer.SetPlayer(detected[0]);
                Assert.AreEqual(fill.sortingLayerID, renderer.Player.body.sortingLayerID);
                Assert.Less(renderer.Player.body.sortingOrder, fill.sortingOrder);
                Assert.Less(renderer.Player.shadow.sortingOrder, renderer.Player.body.sortingOrder);
                Assert.Less(renderer.Player.ring.sortingOrder, renderer.Player.body.sortingOrder);

                renderer.SetCamera(0, CameraTrack.FacingAt(stage.Cameras[0], state.T), 2, false);
                renderer.SetVision(null, null);
                Assert.AreEqual(SS2DLayers.Actors, renderer.Player.body.sortingLayerName);
                Assert.AreEqual(SS2DLayers.Actors, renderer.Player.ring.sortingLayerName);
                Assert.AreEqual(SS2DLayers.Actors, renderer.Player.shadow.sortingLayerName);
            }
            finally { Object.DestroyImmediate(renderer.gameObject); }
        }

        [Test]
        public void GuardAndCameraOverlap_KeepsBothIndicators()
        {
            var stage = Pack().FindByCode("LEVEL-25"); var r = Build(stage);
            try
            {
                var s0 = StageState.Initial(stage); Present(r, stage, s0); AssertOverlayMatchesEngine(r, stage, s0, "LEVEL-25");
                var guard = new HashSet<GridCoord>(Sight.GuardVision(stage, s0, GuardTrack.PoseAt(stage.Guards[0], 0)));
                var cam = EngineCameraCells(stage, s0);
                var both = new HashSet<GridCoord>(guard); both.IntersectWith(cam);
                Assert.Greater(both.Count, 0, "the static guard and the camera share at least one node at t=0");
                int redRings = 0;
                foreach (Transform t in r.transform.Find("Overlays/GuardVision"))
                    if (t.name == "NodeRing" && t.gameObject.activeSelf)
                    {
                        redRings++;
                        var ring = t.GetComponent<SpriteRenderer>();
                        Assert.AreEqual(SS2DLayers.GuardVision, ring.sortingLayerName, "guard rings use their own layer");
                        Assert.AreEqual(r.library.guardVisionOrder + 2, ring.sortingOrder, "guard rings use the guard draw order");
                        foreach (var c in both) if (Vector3.Distance(t.localPosition, r.Pos(c)) < 1e-3f) Assert.IsTrue(r.CameraOverlay.Contains(c), "camera detection also covers " + c);
                    }
                Assert.AreEqual(guard.Count, redRings, "one red ring per guard-seen node");
                Assert.IsTrue(guard.SetEquals(r.GuardOverlay.DrawnCells)); Assert.AreEqual(1, r.GuardCones.Count, "one red fan for the static guard");
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }

        [Test]
        public void RotatedBoard_DrawsCameraRegionAtRotatedPositions_AndNeverOverNonNodes()
        {
            var stage = Pack().FindByCode("LEVEL-17"); var r = Build(stage); // camera on the east wall facing west
            try
            {
                Assert.IsTrue(r.Rotated);
                var s0 = StageState.Initial(stage); Present(r, stage, s0); AssertOverlayMatchesEngine(r, stage, s0, "LEVEL-17");
                foreach (var c in r.CameraOverlay.DrawnCells) { var p = r.Pos(c); Assert.AreEqual(c, r.ToGrid(r.transform.TransformPoint(p)), "rotated position maps back to the cell"); Assert.IsTrue(r.HasNode(c)); }
                Assert.Less(Quaternion.Angle(r.RotationFor(Direction.West), r.CameraHeads[0].localRotation), 0.01f);
            }
            finally { Object.DestroyImmediate(r.gameObject); }
        }
    }
}
