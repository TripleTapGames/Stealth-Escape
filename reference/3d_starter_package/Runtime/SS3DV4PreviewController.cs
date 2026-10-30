using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Art review only: loads the 30 V4 level definitions and places placeholder 3D models.
    /// Not a playable runtime. Claude must connect SS3DLevelRenderer to existing gameplay state.
    /// </summary>
    [ExecuteAlways]
    public sealed class SS3DV4PreviewController : MonoBehaviour
    {
        public TextAsset v4LevelsJson;
        public SS3DLevelRenderer levelRenderer;
        [Range(0,29)]public int levelIndex=6;
        public SS3DBoardCameraFitter boardFitter;
        public SS3DCourtyardFrame exteriorFrame;
        [ContextMenu("Build Preview")]
        public void BuildPreview()
        {
            if(!v4LevelsJson || !levelRenderer){Debug.LogError("Assign V4 JSON and levelRenderer.");return;}
            var set=SS3DV4Reader.Parse(v4LevelsJson.text);
            int index=Mathf.Clamp(levelIndex,0,set.levels.Count-1);
            levelRenderer.Build(set.levels[index]);
            if(exteriorFrame) exteriorFrame.Build(levelRenderer);
            if(boardFitter)boardFitter.Fit(levelRenderer.BoardBounds(),levelRenderer.transform);
            Debug.Log($"Shadow Steps 3D visual preview: {set.levels[index].label} {set.levels[index].title}");
        }
    }
}
