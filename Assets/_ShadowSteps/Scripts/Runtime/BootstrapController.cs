using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowSteps.Runtime
{
    /// <summary>First scene: loads level packs and the save, then opens Home. Fails visibly (not silently) on bad data.</summary>
    public sealed class BootstrapController : MonoBehaviour
    {
        public string NextScene = "Home";
        public string Status { get; private set; } = "Loading…";
        public bool Failed { get; private set; }

        private IEnumerator Start()
        {
            bool ok = AppServices.IsInitialized || AppServices.TryInitializeSync();
            if (!ok) yield return AppServices.InitializeAsync(r => ok = r);
            if (!ok)
            {
                Failed = true;
                Status = "Could not load game data:\n" + AppServices.LastError;
                yield break;
            }
            AudioService.Ensure();
            SceneManager.LoadScene(NextScene);
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(18, Screen.height / 40), wordWrap = true };
            style.normal.textColor = Failed ? new Color(1f, 0.5f, 0.5f) : Color.white;
            GUI.Label(new Rect(Screen.width * 0.1f, Screen.height * 0.4f, Screen.width * 0.8f, Screen.height * 0.2f), Status, style);
        }
    }
}
