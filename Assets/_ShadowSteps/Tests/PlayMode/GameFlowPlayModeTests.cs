using System.Collections;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.UI;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowSteps.Tests.PlayMode
{
    /// <summary>V3 M5/M6: win → results + save (stars, Ghost), capture → failure → retry, lockdown → failure, pause locks input.</summary>
    public class GameFlowPlayModeTests
    {
        private GameSceneComposer _composer;
        private GameFlowController _flow;

        private IEnumerator Open(int stageId)
        {
            AppServices.ResetForTests();
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            var save = new SaveData(); GuardOnboarding.MarkAllSeen(save); AppServices.InstallSaveForTests(save);
            AppServices.PendingStageId = stageId;
            _composer = new GameObject("Composer").AddComponent<GameSceneComposer>();
            yield return null; yield return null;
            Assert.IsTrue(_composer.Ready);
            _flow = Object.FindObjectOfType<GameFlowController>();
            Assert.IsNotNull(_flow, "flow controller attaches to every composed Game scene");
            _flow.Router.PopAllModals(); // dismiss the onboarding card
            yield return null;
        }

        private IEnumerator Play(params string[] tokens)
        {
            foreach (var t in tokens)
            {
                Assert.IsTrue(StageAction.TryParse(t, out var a), t);
                Assert.IsTrue(_composer.Session.TrySubmit(a), "action " + t);
                while (_composer.Session.IsBusy) yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Win_ShowsResults_RecordsStars_AndUnlocksNext()
        {
            yield return Open(1);
            yield return Play("E", "E"); // FTUE-1 (D-048): two long edges, par 2
            yield return null;
            var results = _flow.Router.Get<ResultsScreen>();
            Assert.IsTrue(results.IsVisible);
            Assert.IsTrue(_composer.Session.InputLocked);
            Assert.AreEqual("★★★", results.StarsText.Text.Substring(0, 3), results.StarsText.Text);
            Assert.IsTrue(AppServices.Save.IsCompleted(1));
            Assert.AreEqual(2, AppServices.Save.GetResult(1, StageModeUtil.Default).BestTurns); // runs are recorded in the shipped mode's table (D-043)
            Assert.AreEqual(3, AppServices.Save.GetResult(1, StageModeUtil.Default).BestStars);
            Assert.IsTrue(StageProgression.IsUnlocked(AppServices.Stages, AppServices.Save, 2));
            Assert.IsFalse(StageProgression.IsUnlocked(AppServices.Stages, AppServices.Save, 3));
        }

        [UnityTest]
        public IEnumerator Capture_ShowsFailureWithCause_RetryRestores()
        {
            yield return Open(3); // FTUE-3 (D-049): guard at (2,0) facing S sees (2,2) across the dissolved (2,1); start (0,2), one long edge east
            yield return Play("E"); // into (2,2): seen before the guard update
            yield return null;
            var failure = _flow.Router.Get<FailureScreen>();
            Assert.IsTrue(failure.IsVisible);
            Assert.AreEqual("BUSTED", failure.TitleText.Text);
            StringAssert.Contains("before it moved", failure.ReasonText.Text);
            failure.OnRetry();
            yield return null;
            Assert.IsFalse(failure.IsVisible);
            Assert.IsFalse(_composer.Session.State.IsTerminal);
            Assert.IsFalse(_composer.Session.InputLocked);
            Assert.AreEqual(0, _composer.Session.State.T);
            Assert.AreEqual(_composer.Session.Stage.Start, _composer.Session.State.Player);
        }

        [UnityTest]
        public IEnumerator Lockdown_ShowsSecurityFailure_AndGhostIsRecordedOnAlarmFreeWins()
        {
            yield return Open(22); // LEVEL-17 Race the Alarm: budget 3, forced alarm
            yield return Play("E");  // alarm triggered at (2,3)
            yield return null;
            Assert.IsTrue(_composer.Session.State.AlarmActive);
            var popup = _flow.Router.Get<AlarmPopupScreen>();
            Assert.IsTrue(popup.IsVisible, "camera detection shows the countdown popup");
            StringAssert.Contains("3 actions", popup.BodyText.Text);
            Assert.IsTrue(_composer.Session.InputLocked);
            popup.OnDismiss();
            yield return null;
            Assert.IsFalse(popup.IsVisible);
            Assert.IsFalse(_composer.Session.InputLocked);
            yield return Play("WAIT", "WAIT", "WAIT"); // budget runs out away from the exit
            yield return null;
            var failure = _flow.Router.Get<FailureScreen>();
            Assert.IsTrue(failure.IsVisible);
            Assert.AreEqual("SECURITY LOCKDOWN", failure.TitleText.Text);

            yield return Open(21); // LEVEL-16 Camera Basics: Ghost offered, alarm-free route exists
            var ghostRoute = StageSolver.Solve(_composer.Session.Stage, new SolveOptions { AlarmFreeOnly = true });
            Assert.IsTrue(ghostRoute.Found);
            foreach (var a in ghostRoute.Actions) { Assert.IsTrue(_composer.Session.TrySubmit(a)); while (_composer.Session.IsBusy) yield return null; }
            yield return null;
            Assert.IsTrue(_flow.Router.Get<ResultsScreen>().IsVisible);
            Assert.IsTrue(AppServices.Save.GetResult(21, StageModeUtil.Default).Ghost, "alarm-free completion earns Ghost");
            Assert.AreEqual(StageScoring.Stars(ghostRoute.CountedActions, _composer.Session.Stage.ParFor(StageModeUtil.Default)), AppServices.Save.GetResult(21, StageModeUtil.Default).BestStars, "stars follow the solver par");
        }

        [UnityTest]
        public IEnumerator Pause_LocksInput_ResumeUnlocks()
        {
            yield return Open(1);
            _flow.Router.Push<PauseScreen>();
            yield return null;
            Assert.IsTrue(_composer.Session.InputLocked);
            Assert.IsFalse(_composer.Session.TrySubmit(StageAction.Move(Direction.East)));
            _flow.Router.Pop();
            yield return null;
            Assert.IsFalse(_composer.Session.InputLocked);
            Assert.IsTrue(_composer.Session.TrySubmit(StageAction.Move(Direction.East)));
        }
    }
}
