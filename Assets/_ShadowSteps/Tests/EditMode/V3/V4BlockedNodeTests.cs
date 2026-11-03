using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using static ShadowSteps.Tests.EditMode.V3.V3TestKit;

namespace ShadowSteps.Tests.EditMode.V3
{
    /// <summary>Handover V4 §11.1 mandatory fixtures: blocked cover 'X' is non-walkable, fully opaque and never a smoke target.</summary>
    public class V4BlockedNodeTests
    {
        [Test]
        public void Rows_ParseWallsBlockedAndFloor_AndRejectUnknownCharacters()
        {
            var b = Board("..X", ".#.", "..."); b.Start = C(0, 0); b.Exit = C(2, 2);
            var stage = b.Build();
            Assert.IsTrue(stage.IsBlocked(C(2, 0))); Assert.IsFalse(stage.IsFloor(C(2, 0))); Assert.IsFalse(stage.IsWall(C(2, 0)));
            Assert.IsTrue(stage.IsWall(C(1, 1))); Assert.IsFalse(stage.IsFloor(C(1, 1)));
            Assert.AreEqual(1, stage.BlockedCount);
            CollectionAssert.AreEqual(new[] { "..X", ".#.", "..." }, StageBuilder.RowsOf(stage), "rows round-trip keeps X");
            var ex = Assert.Throws<System.ArgumentException>(() => Board("..?", "...", "..."));
            StringAssert.Contains("(2, 0)", ex.Message);
        }

        [Test]
        public void MovingOntoBlockedOrWall_IsInvalidAndFree_NoEnemyUpdate()
        {
            var b = Board("....", ".X#.", "...."); b.Start = C(1, 0); b.Exit = C(3, 2);
            b.Guards.Add(Rotating(C(3, 0), Direction.West));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            var ontoX = StageRules.TryApply(stage, s, S);
            Assert.IsFalse(ontoX.Valid); Assert.AreEqual(RejectReason.Blocked, ontoX.Reason); Assert.AreSame(s, ontoX.After); Assert.AreEqual(0, s.T);
            var s2 = new StageState(C(2, 0), 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null);
            var ontoWall = StageRules.TryApply(stage, s2, S);
            Assert.IsFalse(ontoWall.Valid); Assert.AreEqual(RejectReason.Blocked, ontoWall.Reason);
            Assert.IsFalse(stage.IsConnected(C(1, 0), C(1, 1)), "no connection is ever generated into X");
        }

        [Test]
        public void GuardSight_StopsAtBlockedCover_AndHidesEverythingBehind()
        {
            var b = Open(6, 3, C(0, 2), C(5, 2)); b.Blocked.Add(C(3, 1));
            var stage = b.Build();
            CollectionAssert.IsEmpty(Sight.GuardVision(stage, StageState.Initial(stage), new GuardPose3(C(2, 1), Direction.East)), "X directly ahead");
            var b2 = Open(6, 3, C(0, 2), C(5, 2)); b2.Blocked.Add(C(4, 1));
            var stage2 = b2.Build();
            CollectionAssert.AreEqual(Cells((3, 1)), Sight.GuardVision(stage2, StageState.Initial(stage2), new GuardPose3(C(2, 1), Direction.East)), "X at the second node hides it");
        }

        [Test]
        public void CameraFootprint_IsFilteredPerNode_ByBlockedCover_IncludingCornerContact()
        {
            // LEVEL-16 geometry: camera on the top wall facing S, cover at (3,2) and (3,3).
            var b = Board("#######", "#.....#", "#..X..#", "#..X..#", "#######"); b.Start = C(1, 3); b.Exit = C(5, 3);
            b.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var stage = b.Build();
            CollectionAssert.AreEquivalent(Cells((3, 1), (2, 2), (4, 2)), Sight.CameraVision(stage, StageState.Initial(stage), 0, 0),
                "cone: lens node visible; (3,2) is cover and hides itself, (2,2) and (4,2) are reached beside it");
            var beside = Board("#######", "#...X.#", "#.....#", "#.....#", "#######"); beside.Start = C(1, 3); beside.Exit = C(5, 3);
            beside.Cameras.Add(Camera(C(3, 0), Direction.South, Direction.South));
            var bs = beside.Build();
            CollectionAssert.AreEquivalent(Cells((3, 1), (2, 2), (3, 2)), Sight.CameraVision(bs, StageState.Initial(bs), 0, 0),
                "cover beside the lens at (4,1) sits on the ray to (4,2) (it enters that cell at (3.5, 1.25))");
        }

        [Test]
        public void CornerGrazingRay_IsBlocked_ByStrictCornerRule()
        {
            var b = Board("...", ".X.", "..."); b.Start = C(0, 1); b.Exit = C(2, 2); b.SmokeCharges = 1;
            var stage = b.Build();
            var s = StageState.Initial(stage);
            Assert.IsTrue(Sight.Obstructed(stage, s, C(0, 0), C(2, 2)), "the diagonal through the crate's centre");
            Assert.IsTrue(Sight.Obstructed(stage, s, C(0, 2), C(2, 0)), "the other diagonal");
            Assert.IsTrue(Sight.Obstructed(stage, s, C(0, 1), C(1, 2)), "ray from (0,1) to (1,2) grazes the corner at (0.5, 1.5)");
            Assert.IsFalse(Sight.Obstructed(stage, s, C(0, 0), C(2, 0)), "a ray along the top row never touches the crate");
        }

        [Test]
        public void Smoke_CannotTargetCover_OrThrowThroughIt_ButMayLandOnAGuardsFloorNode()
        {
            var b = Board("....", ".X..", "...."); b.Start = C(0, 1); b.Exit = C(3, 2); b.SmokeCharges = 1;
            b.Guards.Add(Static(C(0, 0), Direction.East));
            var stage = b.Build();
            var s = StageState.Initial(stage);
            var ontoX = StageRules.TryApply(stage, s, StageAction.Smoke(C(1, 1)));
            Assert.IsFalse(ontoX.Valid); Assert.AreEqual(RejectReason.BadSmokeTarget, ontoX.Reason); Assert.AreEqual(1, s.SmokeCharges);
            var behind = StageRules.TryApply(stage, s, StageAction.Smoke(C(1, 2)));
            Assert.IsFalse(behind.Valid, "diagonal throw to (1,2) grazes the crate corner");
            CollectionAssert.DoesNotContain(StageRules.SmokeTargets(stage, s), C(1, 1));
            CollectionAssert.DoesNotContain(StageRules.SmokeTargets(stage, s), C(1, 2));
            var onGuard = StageRules.TryApply(stage, s, StageAction.Smoke(C(0, 0)));
            Assert.IsTrue(onGuard.Valid, "a guard's floor node is a valid target"); Assert.IsTrue(onGuard.Free);
        }

        [Test]
        public void MissingConnection_BlocksTravelNotSight_WhileCoverBlocksBoth()
        {
            var b = Open(4, 1, C(0, 0), C(3, 0)); b.Cuts.Add(new EdgeKey(C(1, 0), C(2, 0)));
            var cut = b.Build();
            Assert.AreEqual(RejectReason.NoConnection, StageRules.TryApply(cut, new StageState(C(1, 0), 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null), E).Reason);
            Assert.IsFalse(Sight.Obstructed(cut, StageState.Initial(cut), C(0, 0), C(3, 0)), "a cut has no physical presence");
            var b2 = Open(4, 1, C(0, 0), C(3, 0)); b2.Blocked.Add(C(2, 0));
            var cover = b2.Build();
            Assert.AreEqual(RejectReason.Blocked, StageRules.TryApply(cover, new StageState(C(1, 0), 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null), E).Reason);
            Assert.IsTrue(Sight.Obstructed(cover, StageState.Initial(cover), C(0, 0), C(3, 0)));
        }

        [Test]
        public void Validator_RejectsEntitiesOnCover_AndCamerasMountedOnCover()
        {
            var b = Open(5, 3, C(0, 1), C(4, 1)); b.Blocked.Add(C(2, 1)); b.Guards.Add(PingPong(0, C(1, 1), C(2, 1), C(3, 1)));
            var r = StageValidator.Validate(b.Build(), new StageValidationOptions { RunSolver = false });
            Assert.IsFalse(r.IsValid); StringAssert.Contains("blocked cover 'X'", r.ToString());

            var k = Open(5, 3, C(0, 1), C(4, 1)); k.Blocked.Add(C(2, 0)); k.Key = C(2, 0); k.Doors.Add(new DoorSpec(C(3, 1), C(4, 1)));
            Assert.IsFalse(StageValidator.Validate(k.Build(), new StageValidationOptions { RunSolver = false }).IsValid, "key on X");

            var cam = Open(5, 3, C(0, 1), C(4, 1)); cam.Blocked.Add(C(2, 0)); cam.Cameras.Add(Camera(C(2, 0), Direction.South, Direction.South));
            var cr = StageValidator.Validate(cam.Build(), new StageValidationOptions { RunSolver = false });
            Assert.IsFalse(cr.IsValid); StringAssert.Contains("cannot host a camera", cr.ToString());

            var ok = Open(5, 3, C(0, 1), C(4, 1)); ok.Blocked.Add(C(2, 1));
            Assert.IsTrue(StageValidator.Validate(ok.Build(), new StageValidationOptions { RunSolver = false }).IsValid);
        }

        [Test]
        public void Importer_RejectsBlockedNodesThatDisagreeWithRows()
        {
            string json = "{\"version\":\"v4\",\"levels\":[{\"id\":1,\"label\":\"FTUE 1\",\"title\":\"t\",\"rows\":[\"..X.\"],\"w\":4,\"h\":1,\"start\":[0,0],\"exit\":[3,0],\"blockedNodes\":[[1,0]],\"optimalActions\":3}]}";
            var ex = Assert.Throws<LevelDataException>(() => BrowserLevelImporter.Import(json));
            StringAssert.Contains("(1, 0)", ex.Message);
            string ok = json.Replace("[[1,0]]", "[[2,0]]");
            var imported = BrowserLevelImporter.Import(ok);
            Assert.AreEqual(1, imported[0].Stage.BlockedCount);
            Assert.AreEqual(3, imported[0].BrowserPar, "V4 key optimalActions is read as the browser par");
        }
    }
}
