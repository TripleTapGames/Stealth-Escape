using System.Collections;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowSteps.Tests.PlayMode
{
    /// <summary>V2 gate E/M2: the composed Game scene renders the logical board faithfully and moves exactly one node spacing.</summary>
    public class GameScenePlayModeTests
    {
        private GameSceneComposer _composer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return ResetScene();
            AppServices.PendingLevelId = 1;
            var go = new GameObject("Composer");
            _composer = go.AddComponent<GameSceneComposer>();
            yield return null;
            yield return null;
            Assert.IsTrue(_composer.Ready, "composer did not open a level: " + AppServices.LastError);
        }

        private static IEnumerator ResetScene()
        {
            AppServices.ResetForTests();
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            var root = TestRoot();
            AppServices.InstallLevels(new[] { System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Assets/StreamingAssets/ShadowSteps/levels_tutorial_v2.json")), System.IO.File.ReadAllText(AppServices.LevelsPath) });
            AppServices.InstallPars(new[] { System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Assets/StreamingAssets/ShadowSteps/validation_tutorial_v2.json")), System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Assets/StreamingAssets/ShadowSteps/validation_v2.json")) });
            AppServices.InstallSaveForTests(new SaveData());
        }

        private static string TestRoot()
        {
            var dir = new System.IO.DirectoryInfo(Application.dataPath).Parent;
            return dir.FullName;
        }

        [UnityTest]
        public IEnumerator Level1_BoardMatchesDefinition()
        {
            var board = _composer.Board;
            var level = board.Level;
            Assert.AreEqual(1, level.Id);
            Assert.AreEqual(level.Graph.NodeCount, board.Nodes.Count);
            foreach (var node in board.Nodes)
                Assert.AreEqual(board.Layout.WorldPosition(node.Coord), node.transform.position, "node " + node.Coord);
            Assert.AreEqual(level.Guards.Count, board.Guards.Count);
            Assert.AreEqual(board.Layout.WorldPosition(level.Start), board.Player.transform.position);
            Assert.AreEqual(board.Layout.WorldPosition(level.Exit), board.ExitProp.transform.position);
            // One drawn edge per undirected pair.
            int pairs = 0;
            foreach (var e in level.Graph.Edges) if (level.Graph.HasEdge(e.To, e.From)) pairs++; else pairs += 2;
            Assert.AreEqual(pairs / 2, board.Edges.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisionOverlay_EqualsRulesEngineCells()
        {
            var board = _composer.Board;
            var expected = RulesEngine.GetAllVisionCells(board.Level, _composer.Session.State);
            CollectionAssert.AreEquivalent(expected, board.Vision.ShownCells);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Move_TravelsExactlyOneNodeSpacing_AndUpdatesHud()
        {
            var session = _composer.Session;
            var board = _composer.Board;
            var before = board.Player.transform.position;
            Assert.IsTrue(session.TrySubmit(GameAction.North));
            Assert.IsFalse(session.TrySubmit(GameAction.North), "input must be refused while the turn animates");
            float timeout = Time.time + 3f;
            while (session.IsBusy && Time.time < timeout) yield return null;
            Assert.IsFalse(session.IsBusy);
            var after = board.Player.transform.position;
            Assert.AreEqual(board.Layout.NodeSpacing, Vector3.Distance(before, after), 1e-4f);
            Assert.AreEqual(board.Layout.WorldPosition(session.State.PlayerPosition), after);
            Assert.AreEqual(1, session.State.TurnCount);
            var hud = _composer.Hud as ShadowSteps.UI.GameHudController;
            Assert.IsNotNull(hud);
            Assert.AreEqual("Turn 1", hud.TurnText.Text);
            CollectionAssert.AreEquivalent(RulesEngine.GetAllVisionCells(board.Level, session.State), board.Vision.ShownCells);
        }

        [UnityTest]
        public IEnumerator InvalidMove_DoesNotAnimateOrAdvance()
        {
            var session = _composer.Session;
            Assert.IsFalse(session.TrySubmit(GameAction.South)); // off the board from (0,4)
            Assert.IsFalse(session.IsBusy);
            Assert.AreEqual(0, session.State.TurnCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UndoAndRestart_ResyncBoard()
        {
            var session = _composer.Session;
            var board = _composer.Board;
            session.TrySubmit(GameAction.North);
            while (session.IsBusy) yield return null;
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(board.Layout.WorldPosition(board.Level.Start), board.Player.transform.position);
            session.TrySubmit(GameAction.North);
            while (session.IsBusy) yield return null;
            session.Restart();
            Assert.AreEqual(0, session.State.TurnCount);
            Assert.AreEqual(board.Layout.WorldPosition(board.Level.Start), board.Player.transform.position);
        }

        [UnityTest]
        public IEnumerator ReferenceSolution_WinsThroughTheSceneController()
        {
            var session = _composer.Session;
            foreach (var token in new[] { "N", "N", "E", "E", "N", "E", "N", "E" })
            {
                Assert.IsTrue(session.TrySubmit(GameActionUtil.Parse(token)), "action " + token);
                while (session.IsBusy) yield return null;
            }
            Assert.AreEqual(GameOutcome.Won, session.State.Outcome);
        }

        [UnityTest]
        public IEnumerator Camera_IsTopDownOrthographic_AndBoardFitsViewport()
        {
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
