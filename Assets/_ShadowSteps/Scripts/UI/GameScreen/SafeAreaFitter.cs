using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>Shrinks a full-screen RectTransform to the device safe area (notches, gesture bars). Re-applies on resolution change.</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _last;
        private RectTransform _rt;

        private void Awake() { _rt = (RectTransform)transform; Apply(); }
        private void Update() { if (Screen.safeArea != _last) Apply(); }

        public void Apply()
        {
            _last = Screen.safeArea;
            var min = _last.position;
            var max = _last.position + _last.size;
            min.x /= Screen.width; min.y /= Screen.height; max.x /= Screen.width; max.y /= Screen.height;
            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
