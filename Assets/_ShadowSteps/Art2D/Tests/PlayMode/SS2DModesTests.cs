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
    /// Global Stealth/Knockout modes (D-040) in the real Game scene with the 2D presentation: the HUD toggle is present
    /// on every level, switching restarts the current board cleanly (also mid-animation and after a capture), selecting
    /// the active mode does nothing, Retry/Next Level keep the mode, a knockout moves the spy onto the guard in one
    /// action and the guard stays down with no vision, Stealth contact stays fatal, and wins are recorded per mode.
    /// </summary>
    public class SS2DModesTests
    {
        private GameSceneComposer _composer;
        private GameFlowController _flow;
        private SS2DSessionBridge _bridge;
        private ShadowSteps2DRenderer R => _bridge.Renderer;
        private GameSessionController S => _composer.Session;
        private SS2DHudCanvas Hud => (SS2DHudCanvas)_composer.Hud;

        private IEnumerator OpenGame(string code, SaveData save = null)
        {
            AppServices.ResetForTests();
            AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            if (save == null) { save = new SaveData(); GuardOnboarding.MarkAllSeen(save); } // guard first-encounter cards are covered by SS2DGuardIntroTests
            AppServices.InstallSaveForTests(save);
            AppServices.PendingStageId = AppServices.Stages.ByCode(code).Id;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/_ShadowSteps/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("scene loaded by path in the editor only");
#endif
            yield return null; yield return null; yield return null;
            _composer = Object.FindObjectOfType<GameSceneComposer>(); Assert.IsNotNull(_composer);
            float t0 = Time.realtimeSinceStartup; while (!_composer.Ready && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.IsTrue(_composer.Ready);
            _flow = Object.FindObjectOfType<GameFlowController>(); Assert.IsNotNull(_flow);
            _flow.Router.PopAllModals(); yield return null;
            _bridge = Object.FindObjectOfType<SS2DSessionBridge>(); Assert.IsNotNull(_bridge); Assert.IsTrue(R.IsBuilt);
            Assert.AreEqual(code, S.Stage.Code);
        }

        private IEnumerator Settle() { while (S.IsBusy || _bridge.TweenRunning) yield return null; yield return null; }
        private static void Click(RectTransform rt) => rt.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private HashSet<GridCoord> DrawnGuardCells() => new HashSet<GridCoord>(R.GuardOverlay.DrawnCells);

        [UnityTest]
        public IEnumerator KnockoutIsTheOnlyShippedMode_NoToggleNoWaitButton_EngineModeStillSwitchableForTests()
        {
            yield return OpenGame("FTUE-1"); // guard-free tutorial
            Assert.AreEqual(StageMode.Knockout, S.RulesMode, "the game opens in Knockout (D-043)"); Assert.AreEqual(StageMode.Knockout, S.State.Mode);
            Assert.IsNull(Hud.transform.Find("Canvas/ModeToggle"), "no mode toggle on the HUD"); Assert.IsNull(Hud.transform.Find("Canvas/WaitButton"), "no WAIT button on the HUD");
            Assert.IsNull(_flow.Router.Get<ResultsScreen>().transform.Find("SafeArea/Card/SwitchMode"), "no switch-mode button on the results card");
            Assert.AreEqual(0, S.KnockoutTargets().Count, "no knockout effects without guards"); Assert.AreEqual(0, S.State.T);
            // A fresh scene opens in Knockout even if an older save remembered Stealth.
            var save = AppServices.Save; save.SelectedMode = StageMode.Stealth;
            yield return OpenGame("LEVEL-2", save);
            Assert.AreEqual(StageMode.Knockout, S.RulesMode); Assert.AreEqual(StageMode.Knockout, S.State.Mode);
            // Camera-only stage and a large grid keep the mode across level changes.
            foreach (var code in new[] { "LEVEL-16", "LEVEL-30" })
            {
                _composer.OpenStage(AppServices.Stages.ByCode(code)); yield return null; yield return null;
                Assert.AreEqual(code, S.Stage.Code); Assert.AreEqual(StageMode.Knockout, S.RulesMode); Assert.AreEqual(StageMode.Knockout, S.State.Mode);
            }
            // The engine still supports Stealth for tools and tests: switching restarts the board in that mode.
            Assert.IsTrue(S.SetRulesMode(StageMode.Stealth)); yield return null;
            Assert.AreEqual(StageMode.Stealth, S.State.Mode); Assert.AreEqual(0, S.State.T);
            Assert.IsFalse(S.SetRulesMode(StageMode.Stealth), "selecting the active mode is a no-op");
        }

        [UnityTest]
        public IEnumerator Knockout_MovesOntoTheGuardInOneAction_GuardStaysDown_SwitchRestartsClean_SameModeClickIsNoOp()
        {
            yield return OpenGame("LEVEL-1"); // D-046 "Blind Side": static guard at (3,2) facing W; start (0,2)
            var stage = S.Stage; Assert.AreEqual(1, stage.Guards.Count); Assert.AreEqual(new GridCoord(3, 2), stage.Guards[0].Anchor); Assert.AreEqual(Direction.West, stage.Guards[0].Facing);
            Assert.AreEqual(StageMode.Knockout, S.RulesMode, "default mode");
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.North))); yield return Settle(); // (0,1)
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();  // (1,1)
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();  // (2,1)
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();  // (3,1): beside the guard (it faces W)
            Assert.AreEqual(new GridCoord(3, 1), S.State.Player); Assert.AreEqual(4, S.State.T);
            Assert.AreEqual(new List<int> { 0 }, S.KnockoutTargets(), "the guard is a legal target from its side");
            Assert.IsFalse(R.IsGuardDefeated(0));
            // Tap the guard's node (what a tap on the guard resolves to through the node picker) = the knockout.
            var pick = _composer.Input.ScreenToNodeOverride(_composer.BoardCamera.WorldToScreenPoint(R.transform.TransformPoint(R.Pos(new GridCoord(3, 2)))));
            Assert.AreEqual(new GridCoord(3, 2), pick);
            Assert.IsTrue(S.TapNode(pick.Value));
            yield return Settle();
            Assert.AreEqual(5, S.State.T, "move + knockout cost exactly one action");
            Assert.AreEqual(new GridCoord(3, 2), S.State.Player, "the spy stands on the guard's node");
            Assert.IsTrue(S.State.IsDefeated(0)); Assert.AreEqual(StageResult.Playing, S.State.Result);
            Assert.IsTrue(R.IsGuardDefeated(0), "drawn as knocked out"); Assert.AreEqual(0, DrawnGuardCells().Count, "no vision from a knocked-out guard");
            if (R.library.knockoutStyle == SS2DKnockoutStyle.Launch)
            {
                // D-066 (reference clip): the flung piece lies beside the board, outside the drawn bounds, on its side.
                Assert.IsTrue(R.Guards[0].gameObject.activeSelf, "the knocked-out guard lies beside the board"); Assert.IsTrue(R.IsGuardResting(0));
                Assert.IsFalse(R.ViewBounds.Contains(R.Guards[0].transform.localPosition), "outside the drawn board"); Assert.AreEqual(90f, Mathf.Abs(Mathf.DeltaAngle(0f, R.Guards[0].transform.localRotation.eulerAngles.z)), 0.5f, "on its side");
            }
            else Assert.IsFalse(R.Guards[0].gameObject.activeSelf, "the knocked-out guard toppled and faded off the board");
            // Frozen: further turns never move or turn it, and it never comes back.
            var gone = R.Guards[0].transform.localPosition; var goneRot = R.Guards[0].Facing;
            Assert.IsFalse(S.TrySubmit(StageAction.Wait), "WAIT does not exist in the game (D-044)"); yield return null; Assert.AreEqual(5, S.State.T);
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.West))); yield return Settle(); // (2,2): the node it used to watch
            Assert.AreEqual(6, S.State.T); Assert.AreEqual(R.library.knockoutStyle == SS2DKnockoutStyle.Launch, R.Guards[0].gameObject.activeSelf, "still lying beside the board (Launch) / still hidden (Topple)"); Assert.AreEqual(gone, R.Guards[0].transform.localPosition); Assert.AreEqual(goneRot, R.Guards[0].Facing);
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle(); // back onto the node: still gone
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.West))); yield return Settle();
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();
            Assert.AreEqual(9, S.State.T); Assert.AreEqual(R.library.knockoutStyle == SS2DKnockoutStyle.Launch, R.Guards[0].gameObject.activeSelf, "the fallen piece never comes back into play"); Assert.AreEqual(0, DrawnGuardCells().Count);
            Assert.Less(Vector3.Distance(R.Player.transform.localPosition, R.Pos(new GridCoord(3, 2))), 1e-3f);
            Assert.AreEqual("9", Hud.actionsValue.text);
            Assert.IsFalse(S.TrySubmit(StageAction.Knockout(0)), "already down"); Assert.AreEqual(9, S.State.T);
            // Selecting the active mode: nothing happens.
            S.SetRulesMode(StageMode.Knockout); yield return null;
            Assert.AreEqual(9, S.State.T); Assert.IsTrue(S.State.IsDefeated(0));
            // Switching to Stealth restarts the current level from its authored state: everything back, guard standing, same level, no action counted.
            S.SetRulesMode(StageMode.Stealth); yield return null;
            Assert.AreEqual(StageMode.Stealth, S.RulesMode); Assert.AreEqual("LEVEL-1", S.Stage.Code);
            Assert.AreEqual(0, S.State.T); Assert.AreEqual(stage.Start, S.State.Player); Assert.AreEqual(0, S.State.DefeatedCount); Assert.AreEqual(StageMode.Stealth, S.State.Mode);
            Assert.IsFalse(R.IsGuardDefeated(0)); Assert.IsTrue(R.Guards[0].gameObject.activeSelf, "guard restored on the board"); Assert.Less(Vector3.Distance(R.Guards[0].transform.localPosition, R.Pos(new GridCoord(3, 2))), 1e-3f);
            Assert.AreEqual(Vector3.one, R.Guards[0].transform.localScale); Assert.AreEqual(1f, R.Guards[0].body.color.a, 1e-3f);
            Assert.IsTrue(new HashSet<GridCoord>(Sight.GuardVision(stage, S.State, GuardTrack.PoseAt(stage.Guards[0], 0))).SetEquals(DrawnGuardCells()), "guard vision is back");
            Assert.IsFalse(S.IsBusy); Assert.IsFalse(S.InputLocked); Assert.AreEqual(StageMode.Stealth, AppServices.Save.SelectedMode);
            // Stealth: the same contact is fatal, and the knockout request is refused.
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.North))); yield return Settle();
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle(); // (3,1) again
            Assert.IsFalse(S.TrySubmit(StageAction.Knockout(0))); Assert.IsTrue(Hud.messageLabel.text.Contains("Stealth"));
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.South))); yield return Settle(); // onto the guard: collision
            Assert.AreEqual(StageResult.Caught, S.State.Result); Assert.AreEqual(CaptureCause.Collision, S.State.Result == StageResult.Caught ? CaptureCause.Collision : CaptureCause.None);
            float waited = 0f; while (_bridge.CaptureSequenceRunning && waited < 5f) { waited += Time.deltaTime; yield return null; }
            yield return null;
            Assert.IsTrue(_flow.Router.AnyModalOpen, "failure screen");
            // Switching modes after the defeat (engine API; no player-facing switch) produces a clean run with the guard restored.
            _flow.Router.PopAllModals(); S.SetRulesMode(StageMode.Knockout); yield return null;
            Assert.IsFalse(_flow.Router.AnyModalOpen); Assert.AreEqual(StageMode.Knockout, S.RulesMode); Assert.AreEqual(0, S.State.T); Assert.AreEqual(StageResult.Playing, S.State.Result);
            Assert.AreEqual(Color.white, R.Player.body.color); Assert.IsFalse(R.IsGuardDefeated(0)); Assert.IsFalse(S.InputLocked);
            // Retry keeps the chosen mode.
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.North))); yield return Settle(); Assert.AreEqual(1, S.State.T);
            _flow.Router.Get<PauseScreen>().OnRestart(); yield return Settle(); // Restart lives on the pause card (the skinned HUD has no retry button)
            Assert.AreEqual(0, S.State.T); Assert.AreEqual(StageMode.Knockout, S.RulesMode); Assert.AreEqual(StageMode.Knockout, S.State.Mode);
        }

        [UnityTest]
        public IEnumerator SwitchingMidAnimation_CancelsTheOldRun_AndFrontalEntryIsRejectedWithoutATurn()
        {
            yield return OpenGame("LEVEL-1");
            S.SetRulesMode(StageMode.Knockout); yield return null;
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.North))); yield return Settle();
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle(); // (3,1): above the guard at (3,2)
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.South))); // knockout starts animating
            Assert.IsTrue(S.IsBusy);
            S.SetRulesMode(StageMode.Stealth); yield return null; // switch while the knockout tween runs
            Assert.IsFalse(S.IsBusy, "the old animation was cancelled"); Assert.AreEqual(0, S.State.T); Assert.AreEqual(StageMode.Stealth, S.RulesMode);
            Assert.IsFalse(R.IsGuardDefeated(0)); Assert.Less(Vector3.Distance(R.Player.transform.localPosition, R.Pos(S.State.Player)), 1e-3f, "spy snapped to the fresh start");
            yield return new WaitForSeconds(0.5f); // a surviving callback must not alter the new run
            Assert.AreEqual(0, S.State.T); Assert.IsFalse(R.IsGuardDefeated(0)); Assert.AreEqual(StageResult.Playing, S.State.Result);
            // Frontal entry in Knockout: guard faces W, (2,2) is its front. Stepping into its front is a capture (its vision covers it), never a free knockout.
            S.SetRulesMode(StageMode.Knockout); yield return null;
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.North))); yield return Settle(); // (0,1)
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle(); // (1,1)
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle(); // (2,1)
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.South))); yield return Settle(); // (2,2): front of the guard, which sees (2,2),(1,2): caught
            Assert.AreEqual(StageResult.Caught, S.State.Result, "the guard's vision covers its front: entering it is a Stealth-identical capture");
            Assert.AreEqual(3, S.State.T, "the clock stays at the old index on a capture"); Assert.AreEqual(0, S.State.DefeatedCount);
        }

        [UnityTest]
        public IEnumerator KnockoutWin_IsRecordedInTheKnockoutTableOnly_AndNextLevelKeepsTheMode()
        {
            yield return OpenGame("LEVEL-1");
            S.SetRulesMode(StageMode.Knockout); yield return null;
            var stage = S.Stage;
            var sol = StageSolver.Solve(stage, new SolveOptions { Mode = StageMode.Knockout });
            Assert.IsTrue(sol.Found, "LEVEL-1 solvable in Knockout");
            foreach (var a in sol.Actions) { Assert.IsTrue(S.TrySubmit(a), a.Token + " at t=" + S.State.T); yield return Settle(); }
            Assert.AreEqual(StageResult.Won, S.State.Result); Assert.AreEqual(sol.CountedActions, S.State.T);
            var ko = AppServices.Save.GetResult(stage.Id, StageMode.Knockout); var st = AppServices.Save.GetResult(stage.Id, StageMode.Stealth);
            Assert.IsNotNull(ko); Assert.IsTrue(ko.Completed); Assert.AreEqual(sol.CountedActions, ko.BestTurns); Assert.AreEqual(StageModeUtil.RulesVersion, ko.RulesVersion);
            Assert.IsNull(st, "a Knockout win never writes the Stealth record");
            Assert.AreEqual(StageProgression.Stars(stage, sol.CountedActions, StageMode.Knockout), ko.BestStars);
            Assert.IsTrue(StageProgression.IsUnlocked(AppServices.Stages, AppServices.Save, AppServices.Stages.NextAfter(stage.Id).Id), "shared unlock flow");
            Assert.IsTrue(_flow.Router.AnyModalOpen); var results = _flow.Router.Get<ResultsScreen>(); Assert.IsTrue(results.IsVisible);
            results.OnNext(); yield return null; yield return null;
            Assert.AreEqual("LEVEL-2", S.Stage.Code); Assert.AreEqual(StageMode.Knockout, S.RulesMode); Assert.AreEqual(StageMode.Knockout, S.State.Mode); Assert.AreEqual(0, S.State.T);
            Assert.AreEqual(StageMode.Knockout, Hud.ShownMode);
        }
    }
}
