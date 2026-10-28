using System.Collections;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowSteps.Tests.PlayMode
{
    /// <summary>V2 M6: win → results + save, capture → failure, pause locks input, undo from failure works.</summary>
    public class GameFlowPlayModeTests
    {
        private GameSceneComposer _composer;
        private GameFlowController _flow;
        private string _savePath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            AppServices.ResetForTests();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
            yield return null;
            var pack = LevelPackLoader.Parse(System.IO.File.ReadAllText(AppServices.LevelsPath));
            AppServices.InstallLevels(pack);
            AppServices.PendingLevelId = 1;
            _composer = new GameObject("Composer").AddComponent<GameSceneComposer>();
            yield return null; yield return null;
            Assert.IsTrue(_composer.Ready);
            _flow = Object.FindObjectOfType<GameFlowController>();
            Assert.IsNotNull(_flow, "flow controller attaches to every composed Game scene");
        }

        private IEnumerator Play(params string[] tokens)
        {
            foreach (var t in tokens)
            {
                Assert.IsTrue(_composer.Session.TrySubmit(GameActionUtil.Parse(t)), "action " + t);
                while (_composer.Session.IsBusy) yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Win_ShowsResults_AndLocksInput()
        {
            yield return Play("N", "N", "E", "E", "N", "E", "N", "E");
            yield return null;
            var results = _flow.Router.Get<ResultsScreen>();
            Assert.IsTrue(results.IsVisible);
            Assert.IsTrue(_composer.Session.InputLocked);
            Assert.AreEqual("★★★", results.StarsText.Text.Substring(0, 3), "level 1 par is 8 when validation_v2.json is present: " + results.StarsText.Text);
        }

        [UnityTest]
        public IEnumerator Capture_ShowsFailure_UndoRecovers()
        {
            // Level 1: fixed guard at (2,0) facing N range 1 sees nothing on board; walk into it instead: (2,0) is occupied.
            yield return Play("N", "N", "N", "N", "E"); // (1,0)
            Assert.IsFalse(_composer.Session.State.IsTerminal);
            yield return Play("E"); // into the guard's node -> occupied
            yield return null;
            var failure = _flow.Router.Get<FailureScreen>();
            Assert.IsTrue(failure.IsVisible);
            failure.OnUndo();
            yield return null;
            Assert.IsFalse(failure.IsVisible);
            Assert.IsFalse(_composer.Session.State.IsTerminal);
            Assert.IsFalse(_composer.Session.InputLocked);
            Assert.AreEqual(5, _composer.Session.State.TurnCount);
        }

        [UnityTest]
        public IEnumerator Pause_LocksInput_ResumeUnlocks()
        {
            _flow.Router.Push<PauseScreen>();
            yield return null;
            Assert.IsTrue(_composer.Session.InputLocked);
            Assert.IsFalse(_composer.Session.TrySubmit(GameAction.North));
            _flow.Router.Pop();
            yield return null;
            Assert.IsFalse(_composer.Session.InputLocked);
            Assert.IsTrue(_composer.Session.TrySubmit(GameAction.North));
        }
    }
}
