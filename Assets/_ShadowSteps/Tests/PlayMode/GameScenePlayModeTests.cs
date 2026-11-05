using System.Collections;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowSteps.Tests.PlayMode
{
    /// <summary>V3 M5: the composed Game scene renders the logical stage faithfully, moves exactly one node, and overlays equal the shared geometry.</summary>
    public class GameScenePlayModeTests
    {
        private GameSceneComposer _composer;

        private IEnumerator Open(int stageId)
        {
            yield return ResetScene();
            AppServices.PendingStageId = stageId;
            var go = new GameObject("Composer");
            _composer = go.AddComponent<GameSceneComposer>();
            yield return null;
            yield return null;
            Assert.IsTrue(_composer.Ready, "composer did not open a stage: " + AppServices.LastError);
        }

        private static IEnumerator ResetScene()
        {
            AppServices.ResetForTests();
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            var save = new SaveData(); GuardOnboarding.MarkAllSeen(save); AppServices.InstallSaveForTests(save);
        }

        private IEnumerator WaitIdle()
        {
            float timeout = Time.time + 3f;
            while (_composer.Session.IsBusy && Time.time < timeout) yield return null;
            Assert.IsFalse(_composer.Session.IsBusy);
        }

        [UnityTest]
        public IEnumerator Ftue3_BoardMatchesStage_AndOverlaysEqualSight()
        {
            yield return Open(3); // Stay Out of Sight: static guard at (2,0) facing S
            var board = _composer.Board; var stage = board.Stage; var state = _composer.Session.State;
            Assert.AreEqual("FTUE-3", stage.Code);
            Assert.AreEqual(stage.Width * stage.Height, board.Nodes.Count);
            foreach (var node in board.Nodes) Assert.AreEqual(board.Layout.WorldPosition(node.Coord), node.transform.position, "node " + node.Coord);
            Assert.AreEqual(stage.Guards.Count, board.Guards.Count);
            Assert.AreEqual(board.Layout.WorldPosition(stage.Start), board.Player.transform.position);
            Assert.AreEqual(board.Layout.WorldPosition(stage.Exit), board.ExitProp.transform.position);
            int connections = 0;
            foreach (var a in stage.FloorNodes()) { if (stage.IsConnected(a, a.Neighbor(Direction.East))) connections++; if (stage.IsConnected(a, a.Neighbor(Direction.South))) connections++; }
            Assert.AreEqual(connections, board.Connections.Count);
            var expected = Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[0], 0));
            CollectionAssert.AreEquivalent(expected, board.GuardVision.ShownCells);
            CollectionAssert.AreEqual(new[] { new GridCoord(2, 1), new GridCoord(2, 2) }, expected);
        }

        [UnityTest]
        public IEnumerator Move_TravelsExactlyOneNode_AndCountsOneAction()
        {
            yield return Open(1);
            var session = _composer.Session; var board = _composer.Board;
            var before = board.Player.transform.position;
            Assert.IsTrue(session.TrySubmit(StageAction.Move(Direction.East)));
            Assert.IsFalse(session.TrySubmit(StageAction.Move(Direction.East)), "input must be refused while the action animates");
            yield return WaitIdle();
            var after = board.Player.transform.position;
            Assert.AreEqual(board.Layout.NodeSpacing, Vector3.Distance(before, after), 1e-4f);
            Assert.AreEqual(board.Layout.WorldPosition(session.State.Player), after);
            Assert.AreEqual(1, session.State.T);
            var hud = _composer.Hud as ShadowSteps.UI.GameHudController;
            Assert.IsNotNull(hud);
            Assert.AreEqual("Actions 1", hud.ActionsText.Text);
        }

        [UnityTest]
        public IEnumerator InvalidMove_IsFree_AndDoesNotAnimate()
        {
            yield return Open(1);
            var session = _composer.Session;
            Assert.IsFalse(session.TrySubmit(StageAction.Move(Direction.North))); // off the 4x1 board
            Assert.IsFalse(session.IsBusy);
            Assert.AreEqual(0, session.State.T);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SolverSolution_WinsThroughTheSceneController_WithParActions()
        {
            yield return Open(8); // LEVEL-3 One Step Ahead: WAIT WAIT E E E E E
            var session = _composer.Session;
            var solved = StageSolver.Solve(session.Stage);
            foreach (var a in solved.Actions)
            {
                Assert.IsTrue(session.TrySubmit(a), "action " + a);
                yield return WaitIdle();
            }
            Assert.AreEqual(StageResult.Won, session.State.Result);
            Assert.AreEqual(session.Stage.Par, session.State.T);
        }

        [UnityTest]
        public IEnumerator Retry_RestoresTheInitialState()
        {
            yield return Open(1);
            var session = _composer.Session; var board = _composer.Board;
            session.TrySubmit(StageAction.Move(Direction.East));
            yield return WaitIdle();
            session.Retry();
            Assert.AreEqual(0, session.State.T);
            Assert.AreEqual(board.Layout.WorldPosition(board.Stage.Start), board.Player.transform.position);
            Assert.AreEqual(0, session.HistoryDepth);
        }

        [UnityTest]
        public IEnumerator SmokeTargeting_IsAFreeAction_AndNeverMovesThePlayer()
        {
            yield return Open(26); // LEVEL-21 Smoke Screen
            var session = _composer.Session; var board = _composer.Board;
            Assert.AreEqual(InputMode.SmokeTarget, session.ToggleMode(InputMode.SmokeTarget));
            Assert.IsTrue(board.Targets.ShownCells.Count > 0, "valid smoke targets are highlighted");
            Assert.IsFalse(session.TapNode(new GridCoord(5, 2)), "far node is not a valid target; mode stays");
            Assert.AreEqual(InputMode.SmokeTarget, session.Mode);
            Assert.IsTrue(session.TapNode(new GridCoord(1, 2)));
            yield return WaitIdle();
            Assert.AreEqual(0, session.State.T, "deployment is free");
            Assert.AreEqual(session.Stage.Start, session.State.Player);
            Assert.AreEqual(InputMode.Move, session.Mode, "returns to movement after deployment");
            Assert.AreEqual(1, session.State.Clouds.Count);
        }

        [UnityTest]
        public IEnumerator CameraStage_ShowsAmberFootprint_AndEmpDisablesIt()
        {
            yield return Open(28); // LEVEL-23 EMP Training
            var session = _composer.Session; var board = _composer.Board;
            Assert.AreEqual(1, board.Cameras.Count);
            CollectionAssert.AreEquivalent(Sight.CameraVision(session.Stage, session.State, 0, 0), board.CameraVision.ShownCells);
            Assert.AreEqual(InputMode.EmpTarget, session.ToggleMode(InputMode.EmpTarget));
            Assert.IsTrue(session.TapCamera(0));
            yield return WaitIdle();
            Assert.AreEqual(0, session.State.T);
            Assert.IsTrue(session.State.IsCameraDisabled(0));
            Assert.AreEqual(0, board.CameraVision.ShownCells.Count, "disabled camera shows no active hazard");
        }

        [UnityTest]
        public IEnumerator Camera_IsTopDownOrthographic_AndBoardFitsViewport()
        {
            yield return Open(21); // LEVEL-16, 7 columns with wall cameras
            var cam = _composer.BoardCamera;
            Assert.IsTrue(cam.orthographic);
            Assert.AreEqual(Quaternion.identity, cam.transform.rotation);
            yield return null; // CameraFitter runs in LateUpdate
            var layout = _composer.Board.Layout;
            foreach (var corner in new[] { new GridCoord(0, 0), new GridCoord(layout.Width - 1, 0), new GridCoord(0, layout.Height - 1), new GridCoord(layout.Width - 1, layout.Height - 1) })
            {
                var vp = cam.WorldToViewportPoint(layout.WorldPosition(corner));
                Assert.IsTrue(vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f, "corner " + corner + " at viewport " + vp);
            }
        }
    }
}
