using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>Level-selection preview: one scene, ten (stage, profile) pairs; pick an index to rebuild the view. Editor inspector adds buttons.</summary>
    [ExecuteAlways]
    public sealed class TownPreviewSwitcher : MonoBehaviour
    {
        [System.Serializable] public struct Entry { public string stageCode; public string label; public TownEnvironmentProfile profile; }
        public Entry[] entries;
        public SS3DPreviewController preview;
        public TownEnvironmentGenerator environment;
        [Tooltip("Which environment is shown; change it (or use the inspector buttons) to rebuild.")]
        public int index;
        private int _shown = -1;

        public void Show(int i)
        {
            if (entries == null || entries.Length == 0 || preview == null || environment == null) return;
            index = Mathf.Clamp(i, 0, entries.Length - 1);
            var e = entries[index];
            preview.stageCode = e.stageCode; environment.profile = e.profile; preview.townEnvironment = environment;
            preview.BuildPreview();
            _shown = index;
            name = "Town_Preview_Switcher [" + (index + 1) + "/" + entries.Length + " " + e.label + "]";
        }

        private void OnValidate() { if (!Application.isPlaying && index != _shown && isActiveAndEnabled && _shown >= 0) Show(index); }
        private void OnEnable() { if (_shown < 0 && entries != null && entries.Length > 0) _shown = index; }
    }
}
