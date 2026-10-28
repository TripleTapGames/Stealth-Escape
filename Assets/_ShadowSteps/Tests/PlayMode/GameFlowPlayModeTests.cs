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
        
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return ResetScene();
            AppServices.PendingLevelId = 1;
            _composer = new GameObject("Composer").AddComponent<GameSceneComposer>();
            yield return null; yield return null;
            Assert.IsTrue(_composer.Ready);
            _flow = Object.FindObjectOfType<GameFlowController>();
            Assert.IsNotNull(_flow, "flow controller attaches to every composed Game scene");
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
            Assert.AreEqual("★★★", results.StarsText.Text.Substring(0, 3), "level 1 par is 8 from validation_v2.json: " + results.StarsText.Text);
            Assert.IsTrue(AppServices.Save.IsCompleted(1));
            Assert.AreEqual(8, AppServices.Save.GetResult(1).BestTurns);
        }

        [UnityTest]
        public IEnumerator Capture_ShowsFailure_UndoRecovers()
        {
            // Level 1: fixed guard at (2,0) facing N range 1 sees nothing on board; walk into its node instead (occupancy capture).
            // (0,4) -> (0,3) -> (0,2) -> (1,2) -> (2,2) -> (2,1); (0,0) and (1,1) are walls.
            yield return Play("N", "N", "E", "E", "N");
            Assert.IsFalse(_composer.Session.State.IsTerminal);
            yield return Play("N"); // into (2,0), the guard's node -> occupied
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
