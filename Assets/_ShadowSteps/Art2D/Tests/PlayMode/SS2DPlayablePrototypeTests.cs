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
            var save = new SaveData(); GuardOnboarding.MarkAllSeen(save); AppServices.InstallSaveForTests(save); // guard cards (D-041) are exercised by SS2DGuardIntroTests
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

        private IEnumerator Settle() { while (S.IsBusy || _bridge.TweenRunning) yield return null; yield return null; }

        private Vector2 ScreenOf(GridCoord c) => Cam.WorldToScreenPoint(R.transform.TransformPoint(R.Pos(c)));

        private HashSet<GridCoord> DrawnHazardCells()
        {
            // Guard hazards are now drawn by the guard overlay (red fan + rings on the seen nodes); cameras by the camera overlay.
            var set = new HashSet<GridCoord>(R.GuardOverlay.DrawnCells);
            return set;
        }

        [UnityTest]
        public IEnumerator SceneLoads_With2DPresentation_AndNo3DRenderers()
        {
            yield return Open();
            Assert.AreEqual("LEVEL-2", S.Stage.Code);
            Assert.IsTrue(R.Rotated, "7×4 stage drawn rotated for portrait");
            Assert.IsInstanceOf<SS2DHudCanvas>(_composer.Hud, "the 2D HUD prefab is the game HUD");
            foreach (var r in _composer.Board.GetComponentsInChildren<Renderer>(true)) Assert.IsFalse(r.enabled, "placeholder board hidden: " + r.name);
            foreach (var mr in Object.FindObjectsOfType<MeshRenderer>()) Assert.IsNotNull(mr.GetComponentInParent<CameraVisionOverlayRenderer>(), "only the flat camera-region meshes may exist: " + mr.name);
            Assert.AreEqual(0, Object.FindObjectsOfType<SkinnedMeshRenderer>().Length);
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
            // Tap (0,3): picked through the screen → node override the InputController uses, then the session tap. ((1,2) would be seen once the guard turns W: D-046 "Clockwise".)
            var pick = _composer.Input.ScreenToNodeOverride(ScreenOf(new GridCoord(0, 3)));
            Assert.AreEqual(new GridCoord(0, 3), pick, "screen point of the drawn node resolves to its logical cell");
            Assert.IsTrue(S.TapNode(pick.Value)); yield return Settle();
            Assert.AreEqual(new GridCoord(0, 3), S.State.Player); Assert.AreEqual(1, S.State.T); Assert.AreEqual("1", hud.actionsValue.text);
            // Non-adjacent node: nothing happens, no action counted.
            Assert.IsFalse(S.TapNode(_composer.Input.ScreenToNodeOverride(ScreenOf(new GridCoord(4, 0))).Value)); yield return Settle();
            Assert.AreEqual(1, S.State.T);
            Assert.IsTrue(S.TapNode(new GridCoord(1, 3))); yield return Settle(); Assert.AreEqual(2, S.State.T);
            // Blocked cover cell (2,3) next to the player: not connected, rejected, no action counted.
            Assert.IsFalse(S.TapNode(new GridCoord(2, 3))); yield return Settle();
            Assert.AreEqual(2, S.State.T); Assert.AreEqual(new GridCoord(1, 3), S.State.Player);
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
            // Swipe left → logical North: (0,2) → (0,1), drawn to the left. (Straight up to (1,2) is the D-046 trap: seen once the guard turns W.)
            var before = R.Pos(S.State.Player);
            Assert.IsTrue(S.TrySubmit(StageAction.Move(_composer.Input.ScreenToLogicalDirection(Direction.West)))); yield return Settle();
            Assert.AreEqual(new GridCoord(0, 1), S.State.Player, "swipe left → logical North");
            var after = R.Pos(S.State.Player);
            Assert.Less(after.x, before.x, "and the spy moved left on screen"); Assert.AreEqual(before.y, after.y, 1e-4f);
            // Swipe up → logical East: (0,1) → (1,1), drawn above.
            Assert.IsTrue(S.TrySubmit(StageAction.Move(_composer.Input.ScreenToLogicalDirection(Direction.North)))); yield return Settle();
            Assert.AreEqual(new GridCoord(1, 1), S.State.Player, "swipe up → logical East");
            Assert.Greater(R.Pos(S.State.Player).y, after.y); Assert.AreEqual(after.x, R.Pos(S.State.Player).x, 1e-4f);
            Assert.AreEqual(Direction.North, R.Logical(R.View(Direction.North)));
        }

        [UnityTest]
        public IEnumerator Wait_DoesNotExist_MovesRotateTheGuard_AndDrawExactlyTheEngineHazards()
        {
            yield return Open();
            var hud = (SS2DHudCanvas)_composer.Hud; var stage = S.Stage; var g = stage.Guards[0];
            Assert.AreEqual(GuardKind.Rotating, g.Kind); Assert.AreEqual(0, stage.WaitLimit, "D-044: no WAIT on any shipped stage");
            Assert.IsNull(hud.transform.Find("Canvas/WaitButton"), "no WAIT button"); Assert.IsFalse(hud.starsLabel.text.Contains("WAIT"), "no WAIT counter or hold hint");
            // WAIT is refused from every path: direct submission, a hold on the board, a tap on the player's own node.
            Assert.IsFalse(S.TrySubmit(StageAction.Wait)); Assert.IsFalse(_composer.Input.LongPress(ScreenOf(new GridCoord(0, 0)))); Assert.IsFalse(S.TapNode(S.State.Player));
            yield return null; Assert.AreEqual(0, S.State.T); Assert.IsTrue(hud.messageLabel.text.Contains("Waiting is not possible"));
            // Only moves advance time: each move turns the rotating guard by one schedule step, and the drawn fan is the engine's.
            var path = new[] { Direction.North, Direction.East, Direction.East, Direction.South }; // the D-046 par route up to the guard's side
            foreach (var d in path)
            {
                int tBefore = S.State.T;
                Assert.IsTrue(S.TrySubmit(StageAction.Move(d)), d + " at t=" + tBefore); yield return Settle();
                Assert.AreEqual(tBefore + 1, S.State.T, "each move is exactly one counted action");
                Assert.AreEqual(S.State.T.ToString(), hud.actionsValue.text);
                var pose = GuardTrack.PoseAt(g, S.State.T);
                Assert.AreEqual(R.View(pose.Facing), R.Guards[0].Facing, "drawn facing = rotated logical facing at t=" + S.State.T);
                var expected = new HashSet<GridCoord>(Sight.GuardVision(stage, S.State, pose));
                Assert.IsTrue(expected.SetEquals(DrawnHazardCells()), "hazard overlay == engine cells at t=" + S.State.T + " (" + string.Join(",", expected) + ")");
                Assert.IsTrue(R.Guards[0].transform.localPosition == R.Pos(pose.Position));
            }
            Assert.AreEqual(Direction.South, GuardTrack.PoseAt(g, 4).Facing, "period 4: back to the authored facing");
            Assert.AreEqual(new List<int> { 0 }, S.KnockoutTargets(), "(2,2) beside the guard at (3,2) as it faces S again: a legal knockout");
            Assert.AreEqual(0, S.State.WaitsUsed);
        }

        [UnityTest]
        public IEnumerator Capture_ShowsFailureAndRetry_ThenRetryResetsEverything()
        {
            yield return Open();
            var hud = (SS2DHudCanvas)_composer.Hud;
            // t0: guard (3,2) faces S. Move E to (1,2) → after the update the guard faces W and sees (2,2),(1,2): caught (the D-046 "Clockwise" trap).
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();
            Assert.AreEqual(StageResult.Caught, S.State.Result); Assert.AreEqual(0, S.State.T);
            // The failure screen waits for the guard to finish its turn and for the capture beat.
            Assert.IsTrue(_bridge.CaptureSequenceRunning || _flow.Router.AnyModalOpen, "capture choreography owns the timing");
            if (_bridge.CaptureSequenceRunning) Assert.IsFalse(_flow.Router.AnyModalOpen, "no failure screen while the guard is still finishing its move/turn and the capture beat plays");
            float waited = 0f; while (_bridge.CaptureSequenceRunning && waited < 5f) { waited += Time.deltaTime; yield return null; }
            yield return null;
            // The rules keep the clock at the old index on a capture; the guard that caught the player by turning is shown at T+1 (its new facing), and so is its fan.
            var caughtPose = GuardTrack.PoseAt(S.Stage.Guards[0], S.State.T + 1);
            Assert.AreEqual(R.View(caughtPose.Facing), R.Guards[0].Facing, "guard completed its turn and stays turned (no snap back)");
            Assert.AreNotEqual(R.View(GuardTrack.PoseAt(S.Stage.Guards[0], S.State.T).Facing), R.Guards[0].Facing, "not the pre-turn facing");
            Assert.IsTrue(new HashSet<GridCoord>(Sight.GuardVision(S.Stage, S.State, caughtPose)).SetEquals(DrawnHazardCells()), "fan drawn for the new facing");
            Assert.IsTrue(DrawnHazardCells().Contains(S.State.Player), "the fan covers the caught player");
            Assert.IsTrue(_flow.Router.AnyModalOpen, "failure screen with Retry is shown after the choreography");
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
            Assert.AreEqual(R.View(Direction.South), R.Guards[0].Facing, "guard back to its authored facing");
            var expected = new HashSet<GridCoord>(Sight.GuardVision(S.Stage, S.State, GuardTrack.PoseAt(S.Stage.Guards[0], 0)));
            Assert.IsTrue(expected.SetEquals(DrawnHazardCells()));
            // Restart from the pause card does the same.
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.North))); yield return Settle(); Assert.AreEqual(1, S.State.T);
            _flow.Router.Get<PauseScreen>().OnRestart(); yield return Settle(); // Restart lives on the pause card (the skinned HUD has no retry button)
            Assert.AreEqual(0, S.State.T);
        }

        [UnityTest]
        public IEnumerator SolverSolution_Wins_WithThreeStars_AndRecordsProgress()
        {
            yield return Open();
            foreach (var token in new[] { "N", "E", "E", "S", "KO#0", "E", "E", "E" }) // validation_v4.json LEVEL-2 (D-046), knockout par 8
            {
                Assert.IsTrue(StageAction.TryParse(token, out var a), token);
                Assert.IsTrue(S.TrySubmit(a), "action " + token + " at t=" + S.State.T);
                yield return Settle();
            }
            Assert.AreEqual(StageResult.Won, S.State.Result);
            Assert.AreEqual(8, S.State.T); Assert.AreEqual(1, S.State.DefeatedCount);
            Assert.AreEqual(3, StageScoring.Stars(S.State.T, S.Stage.ParFor(S.State.Mode)));
            int wonStageId = S.Stage.Id; var exitPos = R.Pos(S.Stage.Exit); var doorPos = R.DoorPosition;
            var result = AppServices.Save.GetResult(wonStageId, S.State.Mode); // the shipped game records Knockout runs (D-043)
            Assert.IsNotNull(result, "win recorded before the door sequence"); Assert.IsTrue(result.Completed); Assert.AreEqual(8, result.BestTurns); Assert.AreEqual(3, result.BestStars);
            Assert.IsFalse(result.Ghost, "Ghost is not offered on LEVEL-2");
            Assert.IsTrue(AppServices.Save.IsUnlocked(wonStageId + 1), "next stage unlocked");
            Assert.IsTrue(((SS2DHudCanvas)_composer.Hud).messageLabel.text.Contains("\u2605\u2605\u2605"));
            // No exit door any more: the spy stays on the exit node and the results screen appears straight away.
            Assert.IsFalse(R.library.playExitDoorSequence); Assert.IsFalse(_bridge.DoorSequenceRunning, "no door sequence");
            Assert.IsFalse(R.HasExitDoor); Assert.IsNull(R.transform.Find("Props/Door"));
            Assert.IsTrue(_flow.Router.AnyModalOpen, "results modal shown after the winning action");
            Assert.IsNotNull(_flow.Router.Get<ResultsScreen>()); Assert.IsTrue(_flow.Router.Get<ResultsScreen>().IsVisible);
            Assert.Less(Vector3.Distance(R.Player.transform.localPosition, exitPos), 1e-3f, "spy stays on the exit node");
            Assert.AreEqual(wonStageId, S.Stage.Id, "no auto-advance; the results screen offers Next");
            Assert.Greater(doorPos.y, exitPos.y, "the logical door anchor is still computed (unused)");
            // Next from the results screen: the next stage opens in the same scene; the HUD and renderer follow.
            var next = AppServices.Stages.NextAfter(wonStageId); Assert.IsNotNull(next);
            _flow.Router.PopAllModals(); _composer.OpenStage(next); yield return null; yield return null;
            Assert.AreEqual("LEVEL-3", S.Stage.Code); Assert.AreEqual(0, S.State.T); Assert.IsTrue(R.IsBuilt);
            var hud3 = (SS2DHudCanvas)_composer.Hud; Assert.IsTrue((hud3.levelCaption != null ? hud3.levelCaption.text : hud3.levelLabel.text).StartsWith("LEVEL 3"), "skinned HUD: caption carries the level number");
            if (hud3.levelCaption != null)
            {
                Assert.IsTrue(hud3.levelLabel.resizeTextForBestFit, "the title shrinks to fit the pill"); Assert.AreEqual(HorizontalWrapMode.Wrap, hud3.levelLabel.horizontalOverflow); Assert.AreEqual(VerticalWrapMode.Truncate, hud3.levelLabel.verticalOverflow);
                Assert.AreEqual(string.Empty, hud3.messageLabel.text, "the level intro is no longer shown on the HUD"); Assert.IsFalse(hud3.MessagePillVisible, "the slim pill hides while empty");
                hud3.ShowMessage("Tap a highlighted node to drop smoke", 0f); Assert.IsTrue(hud3.MessagePillVisible, "and shows for a message");
                hud3.ShowMessage(string.Empty, 0f); Assert.IsFalse(hud3.MessagePillVisible);
            }
            Assert.AreEqual(Color.white, R.Player.body.color); Assert.Less(Vector3.Distance(R.Player.transform.localPosition, R.Pos(S.State.Player)), 1e-3f);
        }

        [UnityTest]
        public IEnumerator LongPress_AndSpace_NeverAdvanceTime()
        {
            yield return Open();
            var input = _composer.Input; Assert.IsNotNull(input);
            var somewhere = ScreenOf(new GridCoord(0, 0)) + new Vector2(5f, 5f);
            string before = S.State.Key();
            for (int i = 0; i < 3; i++) { Assert.IsFalse(input.LongPress(somewhere), "a hold on the board does nothing (D-044)"); yield return null; }
            Assert.AreEqual(0, S.State.T); Assert.AreEqual(0, S.State.WaitsUsed); Assert.AreEqual(before, S.State.Key(), "state untouched");
            Assert.IsFalse(S.TrySubmit(StageAction.Wait), "a queued or replayed WAIT is refused by the session gate"); Assert.AreEqual(0, S.State.T);
            S.SetMode(InputMode.SmokeTarget); Assert.AreEqual(InputMode.Move, S.Mode); // LEVEL-2 has no smoke: targeting mode falls back to Move
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.North))); yield return Settle(); Assert.AreEqual(1, S.State.T, "moves still count");
        }

        [UnityTest]
        public IEnumerator Campaign_Main25ProceedsToMain26_AndTheNewLevelsCompose()
        {
            AppServices.ResetForTests();
            AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            var save = new SaveData(); GuardOnboarding.MarkAllSeen(save); AppServices.InstallSaveForTests(save); // guard cards (D-041) are exercised by SS2DGuardIntroTests
            AppServices.PendingStageId = 30; // LEVEL-25
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/_ShadowSteps/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null; yield return null; yield return null;
            var composer = Object.FindObjectOfType<GameSceneComposer>(); Assert.IsNotNull(composer);
            Assert.AreEqual("LEVEL-25", composer.Session.Stage.Code);
            var next = AppServices.Stages.NextAfter(composer.Session.Stage.Id); Assert.IsNotNull(next); Assert.AreEqual("LEVEL-26", next.Code);
            foreach (var expected in new[] { "LEVEL-26", "LEVEL-27", "LEVEL-28", "LEVEL-29", "LEVEL-30", "LEVEL-31", "LEVEL-32" })
            {
                composer.OpenStage(next); yield return null; yield return null;
                Assert.AreEqual(expected, composer.Session.Stage.Code);
                var bridge = Object.FindObjectOfType<SS2DSessionBridge>(); Assert.IsNotNull(bridge); Assert.IsTrue(bridge.Renderer.IsBuilt, expected + " drawn by the 2D layer");
                Assert.Less(Vector3.Distance(bridge.Renderer.Player.transform.localPosition, bridge.Renderer.Pos(composer.Session.State.Player)), 1e-3f);
                Assert.AreEqual(composer.Session.Stage.Guards.Count, bridge.Renderer.Guards.Count);
                next = AppServices.Stages.NextAfter(composer.Session.Stage.Id);
            }
            Assert.IsNull(next, "nothing after Main 32 (D-078): the flow returns to level select");
        }

        /// <summary>Levels opened from the menus load Assets/_ShadowSteps/Scenes/Game.unity; it must compose with the 2D layer too, not the placeholder board.</summary>
        [UnityTest]
        public IEnumerator GameScene_OpenedFromMenus_UsesThe2DPresentation()
        {
            AppServices.ResetForTests();
            AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            var save = new SaveData(); GuardOnboarding.MarkAllSeen(save); AppServices.InstallSaveForTests(save); // guard cards (D-041) are exercised by SS2DGuardIntroTests
            AppServices.PendingStageId = 21; // LEVEL-16, as the level-select would request it
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/_ShadowSteps/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null; yield return null; yield return null;
            var composer = Object.FindObjectOfType<GameSceneComposer>(); Assert.IsNotNull(composer);
            float t0 = Time.realtimeSinceStartup; while (!composer.Ready && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.IsTrue(composer.Ready);
            Assert.IsTrue(composer.Use2DArtPresentation, "Game scene flag"); Assert.IsFalse(composer.Use3DPresentation);
            Assert.AreEqual("LEVEL-16", composer.Session.Stage.Code);
            var bridge = Object.FindObjectOfType<SS2DSessionBridge>(); Assert.IsNotNull(bridge, "2D bridge attached in the Game scene"); Assert.IsTrue(bridge.Renderer.IsBuilt);
            Assert.IsInstanceOf<SS2DHudCanvas>(composer.Hud, "2D HUD prefab used by the Game scene");
            foreach (var r in composer.Board.GetComponentsInChildren<Renderer>(true)) Assert.IsFalse(r.enabled, "placeholder board hidden");
            Assert.Greater(bridge.Renderer.CameraOverlay.DrawnCells.Count, 0, "camera region drawn for LEVEL-16");
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
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.North))); yield return Settle(); Assert.AreEqual(1, S.State.T);
            // Skinned HUD (D-045): gadget buttons are always shown; without charges they are dimmed and not interactable.
            Assert.IsTrue(hud.smokeButton.gameObject.activeSelf); Assert.IsFalse(hud.smokeButton.GetComponent<UnityEngine.UI.Button>().interactable, "no smoke on LEVEL-2");
            Assert.IsTrue(hud.empButton.gameObject.activeSelf); Assert.IsFalse(hud.empButton.GetComponent<UnityEngine.UI.Button>().interactable, "no EMP on LEVEL-2");
            Assert.Less(hud.smokeButton.GetComponent<UnityEngine.UI.Image>().color.a, 0.9f, "dimmed at zero charges");
        }
    }
}
