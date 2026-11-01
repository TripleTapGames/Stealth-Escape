using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.UI;
using ShadowSteps.V3;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>
    /// Loads the real 2D prototype scene in Play mode and drives it through the same entry points the touch input
    /// uses (node picking override, swipe direction mapping, session taps, HUD buttons), asserting against the rules
    /// engine: one counted action per move/wait, rejected moves cost nothing, guard poses and hazard cells follow the
    /// level definition after the 90° portrait rotation, capture → retry resets, and the solver's solution wins with
    /// the right stars and a recorded result. The save is in-memory (nothing is written to disk).
    /// </summary>
    public class SS2DPlayablePrototypeTests
    {
        private const string ScenePath = "Assets/_ShadowSteps/Art2D/Demo/ShadowSteps_2D_Prototype.unity";
        private GameSceneComposer _composer;
        private GameFlowController _flow;
        private SS2DSessionBridge _bridge;
        private ShadowSteps2DRenderer R => _bridge.Renderer;
        private GameSessionController S => _composer.Session;
        private Camera Cam => _composer.BoardCamera;

        private IEnumerator Open()
        {
            AppServices.ResetForTests();
            AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            AppServices.InstallSaveForTests(new SaveData());
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("the prototype scene is loaded by path in the editor only");
#endif
            yield return null; yield return null; yield return null;
            _composer = Object.FindObjectOfType<GameSceneComposer>();
            Assert.IsNotNull(_composer, "the prototype scene carries a GameSceneComposer");
            float t0 = Time.realtimeSinceStartup;
            while (!_composer.Ready && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.IsTrue(_composer.Ready, "composer ready");
            _flow = Object.FindObjectOfType<GameFlowController>();
            Assert.IsNotNull(_flow, "flow controller attaches to every composed scene");
            _flow.Router.PopAllModals(); // onboarding card
            yield return null;
            _bridge = Object.FindObjectOfType<SS2DSessionBridge>();
            Assert.IsNotNull(_bridge, "2D bridge attached (Use2DArtPresentation)");
            Assert.IsTrue(R.IsBuilt);
        }

        private IEnumerator Settle() { while (S.IsBusy) yield return null; yield return null; }

        private Vector2 ScreenOf(GridCoord c) => Cam.WorldToScreenPoint(R.transform.TransformPoint(R.Pos(c)));

        private HashSet<GridCoord> DrawnHazardCells()
        {
            var set = new HashSet<GridCoord>();
            foreach (var t in R.GetComponentsInChildren<Transform>(false))
                if (t.name == "Overlay_Fill" && t.gameObject.activeInHierarchy && t.GetComponent<SpriteRenderer>().sortingOrder < 4) set.Add(R.ToGrid(t.position));
            return set;
        }

        [UnityTest]
        public IEnumerator SceneLoads_With2DPresentation_AndNo3DRenderers()
        {
            yield return Open();
            Assert.AreEqual("LEVEL-2", S.Stage.Code);
            Assert.IsTrue(R.Rotated, "6×4 stage drawn rotated for portrait");
            Assert.IsInstanceOf<SS2DHudCanvas>(_composer.Hud, "the 2D HUD prefab is the game HUD");
            foreach (var r in _composer.Board.GetComponentsInChildren<Renderer>(true)) Assert.IsFalse(r.enabled, "placeholder board hidden: " + r.name);
            Assert.AreEqual(0, Object.FindObjectsOfType<MeshRenderer>().Length); Assert.AreEqual(0, Object.FindObjectsOfType<SkinnedMeshRenderer>().Length);
            Assert.IsNull(Object.FindObjectOfType<SS2DPreviewController>()?.boardRenderer.Stage, "art preview steps aside in play mode");
            Assert.AreEqual(new GridCoord(0, 2), S.State.Player); Assert.AreEqual(0, S.State.T);
            Assert.AreEqual("0", ((SS2DHudCanvas)_composer.Hud).actionsValue.text);
        }

        [UnityTest]
        public IEnumerator Tap_AdjacentNodeMovesOneStep_NonAdjacentAndBlockedCostNothing()
        {
            yield return Open();
            var hud = (SS2DHudCanvas)_composer.Hud;
            // Off-board move from the start column is rejected and costs nothing.
            Assert.IsFalse(S.TrySubmit(StageAction.Move(Direction.West))); Assert.AreEqual(0, S.State.T);
            // Tap (1,2): picked through the screen → node override the InputController uses, then the session tap.
            var pick = _composer.Input.ScreenToNodeOverride(ScreenOf(new GridCoord(1, 2)));
            Assert.AreEqual(new GridCoord(1, 2), pick, "screen point of the drawn node resolves to its logical cell");
            Assert.IsTrue(S.TapNode(pick.Value)); yield return Settle();
            Assert.AreEqual(new GridCoord(1, 2), S.State.Player); Assert.AreEqual(1, S.State.T); Assert.AreEqual("1", hud.actionsValue.text);
            // Non-adjacent node: nothing happens, no action counted.
            Assert.IsFalse(S.TapNode(_composer.Input.ScreenToNodeOverride(ScreenOf(new GridCoord(4, 0))).Value)); yield return Settle();
            Assert.AreEqual(1, S.State.T);
            // Blocked cover cell (2,2) next to the player: not connected, rejected, no action counted.
            Assert.IsFalse(S.TapNode(new GridCoord(2, 2))); yield return Settle();
            Assert.AreEqual(1, S.State.T); Assert.AreEqual(new GridCoord(1, 2), S.State.Player);
            // The drawn spy stands on the drawn node of its logical cell.
            Assert.Less(Vector3.Distance(R.Player.transform.localPosition, R.Pos(S.State.Player)), 1e-3f);
        }

        [UnityTest]
        public IEnumerator Swipe_ScreenDirectionsMapToRotatedLogicalMoves()
        {
            yield return Open();
            // Exit is logical East of the start row; on the rotated board it is drawn above, so an upward swipe must move logically East.
            Assert.AreEqual(Direction.East, _composer.Input.ScreenToLogicalDirection(Direction.North));
            Assert.AreEqual(Direction.North, _composer.Input.ScreenToLogicalDirection(Direction.West));
            var before = R.Pos(S.State.Player);
            Assert.IsTrue(S.TrySubmit(StageAction.Move(_composer.Input.ScreenToLogicalDirection(Direction.North)))); yield return Settle();
            Assert.AreEqual(new GridCoord(1, 2), S.State.Player, "swipe up → logical East");
            var after = R.Pos(S.State.Player);
            Assert.Greater(after.y, before.y, "and the spy moved up on screen"); Assert.AreEqual(before.x, after.x, 1e-4f);
            // Swipe left → logical North: (1,2) → (1,1), drawn to the left.
            Assert.IsTrue(S.TrySubmit(StageAction.Move(_composer.Input.ScreenToLogicalDirection(Direction.West)))); yield return Settle();
            Assert.AreEqual(new GridCoord(1, 1), S.State.Player);
            Assert.Less(R.Pos(S.State.Player).x, after.x);
            Assert.AreEqual(Direction.North, R.Logical(R.View(Direction.North)));
        }

        [UnityTest]
        public IEnumerator Wait_CountsOneAction_RotatesGuard_AndDrawsExactlyTheEngineHazards()
        {
            yield return Open();
            var hud = (SS2DHudCanvas)_composer.Hud; var stage = S.Stage; var g = stage.Guards[0];
            Assert.AreEqual(GuardKind.Rotating, g.Kind);
            for (int step = 0; step < 4; step++)
            {
                int tBefore = S.State.T;
                hud.waitButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return Settle();
                Assert.AreEqual(tBefore + 1, S.State.T, "each WAIT is exactly one counted action");
                Assert.AreEqual(S.State.T.ToString(), hud.actionsValue.text);
                var pose = GuardTrack.PoseAt(g, S.State.T);
                Assert.AreEqual(R.View(pose.Facing), R.Guards[0].Facing, "drawn facing = rotated logical facing at t=" + S.State.T);
                var expected = new HashSet<GridCoord>(Sight.GuardVision(stage, S.State, pose));
                Assert.IsTrue(expected.SetEquals(DrawnHazardCells()), "hazard overlay == engine cells at t=" + S.State.T + " (" + string.Join(",", expected) + ")");
                Assert.IsTrue(R.Guards[0].transform.localPosition == R.Pos(pose.Position));
            }
            Assert.AreEqual(Direction.East, GuardTrack.PoseAt(g, 4).Facing, "period 4: back to the authored facing");
        }

        [UnityTest]
        public IEnumerator Capture_ShowsFailureAndRetry_ThenRetryResetsEverything()
        {
            yield return Open();
            var hud = (SS2DHudCanvas)_composer.Hud;
            // t0: guard (3,1) faces E. Move E to (1,2) → guard turns S. Move N to (1,1) → after the update the guard faces W and sees (2,1),(1,1): caught.
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.North))); yield return Settle();
            Assert.AreEqual(StageResult.Caught, S.State.Result);
            Assert.IsTrue(_flow.Router.AnyModalOpen, "failure screen with Retry is shown");
            Assert.IsNotNull(_flow.Router.Get<FailureScreen>()); Assert.IsTrue(_flow.Router.Get<FailureScreen>().IsVisible);
            Assert.IsTrue(S.InputLocked, "input locked behind the modal");
            var lib = R.library;
            Assert.AreEqual(lib.captured, R.Player.body.color, "spy tinted as captured");
            Assert.IsFalse(S.TrySubmit(StageAction.Wait), "terminal state accepts no more actions");
            // Retry from the failure screen path.
            _flow.Router.PopAllModals(); S.Retry(); yield return Settle();
            Assert.AreEqual(0, S.State.T); Assert.AreEqual(new GridCoord(0, 2), S.State.Player); Assert.AreEqual(StageResult.Playing, S.State.Result);
            Assert.AreEqual(Color.white, R.Player.body.color); Assert.AreEqual("0", hud.actionsValue.text);
            Assert.IsFalse(S.InputLocked);
            Assert.AreEqual(R.View(Direction.East), R.Guards[0].Facing, "guard back to its authored facing");
            var expected = new HashSet<GridCoord>(Sight.GuardVision(S.Stage, S.State, GuardTrack.PoseAt(S.Stage.Guards[0], 0)));
            Assert.IsTrue(expected.SetEquals(DrawnHazardCells()));
            // HUD retry button does the same.
            Assert.IsTrue(S.TrySubmit(StageAction.Wait)); yield return Settle(); Assert.AreEqual(1, S.State.T);
            hud.retryButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return Settle();
            Assert.AreEqual(0, S.State.T);
        }

        [UnityTest]
        public IEnumerator SolverSolution_Wins_WithThreeStars_AndRecordsProgress()
        {
            yield return Open();
            foreach (var token in new[] { "E", "S", "E", "E", "E", "E", "N" }) // validation_v4.json LEVEL-2, par 7
            {
                Assert.IsTrue(StageAction.TryParse(token, out var a), token);
                Assert.IsTrue(S.TrySubmit(a), "action " + token + " at t=" + S.State.T);
                yield return Settle();
            }
            Assert.AreEqual(StageResult.Won, S.State.Result);
            Assert.AreEqual(7, S.State.T);
            Assert.AreEqual(3, StageScoring.Stars(S.State.T, S.Stage.Par));
            Assert.IsTrue(_flow.Router.Get<ResultsScreen>().IsVisible, "results screen shown");
            var result = AppServices.Save.GetResult(S.Stage.Id);
            Assert.IsNotNull(result); Assert.IsTrue(result.Completed); Assert.AreEqual(7, result.BestTurns); Assert.AreEqual(3, result.BestStars);
            Assert.IsFalse(result.Ghost, "Ghost is not offered on LEVEL-2");
            Assert.IsTrue(AppServices.Save.IsUnlocked(S.Stage.Id + 1), "next stage unlocked");
            Assert.IsTrue(((SS2DHudCanvas)_composer.Hud).messageLabel.text.Contains("★★★"));
        }

        [UnityTest]
        public IEnumerator PauseButton_OpensPauseScreenAndLocksInput_ResumeUnlocks()
        {
            yield return Open();
            var hud = (SS2DHudCanvas)_composer.Hud;
            hud.pauseButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
            Assert.IsTrue(_flow.Router.Get<PauseScreen>().IsVisible); Assert.IsTrue(S.InputLocked);
            Assert.IsFalse(S.TrySubmit(StageAction.Wait)); Assert.AreEqual(0, S.State.T);
            _flow.Router.Pop(); yield return null;
            Assert.IsFalse(S.InputLocked);
            Assert.IsTrue(S.TrySubmit(StageAction.Wait)); yield return Settle(); Assert.AreEqual(1, S.State.T);
            Assert.IsFalse(hud.smokeButton.gameObject.activeSelf, "no smoke on LEVEL-2"); Assert.IsFalse(hud.empButton.gameObject.activeSelf, "no EMP on LEVEL-2");
        }
    }
}
