using UnityEngine;

namespace ShadowSteps.Art2D
{
    /// <summary>
    /// Fixed top-down orthographic camera for the 2D presentation: fits the board panel (grid + margin) into the
    /// portrait band left free by the HUD and the device safe area, same arithmetic as the runtime
    /// <see cref="ShadowSteps.Runtime.CameraFitter"/> so taps resolve identically.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public sealed class SS2DCameraFitter : MonoBehaviour
    {
        public ShadowSteps2DRenderer board;
        [Range(0f, 0.5f)] public float topReservedFraction = 0.14f;
        [Range(0f, 0.5f)] public float bottomReservedFraction = 0.22f;
        [Tooltip("Extra margin around the board panel in node spacings.")]
        public float marginNodes = 0.25f;
        public bool respectSafeArea = true;
        private Camera _cam; private int _lastW, _lastH, _lastHash;

        private void LateUpdate()
        {
            if (board == null || !board.IsBuilt) return;
            int hash = board.Stage.GetHashCode();
            if (Screen.width == _lastW && Screen.height == _lastH && hash == _lastHash) return;
            Fit((float)Screen.width / Mathf.Max(1, Screen.height));
            _lastW = Screen.width; _lastH = Screen.height; _lastHash = hash;
        }

        public void Fit(float aspect)
        {
            if (board == null || !board.IsBuilt) return;
            _cam = _cam != null ? _cam : GetComponent<Camera>();
            _cam.orthographic = true; _cam.transform.rotation = Quaternion.identity;
            var layout = board.ViewLayout; float s = layout.NodeSpacing;
            if (board.library != null) { topReservedFraction = board.library.hudTop; bottomReservedFraction = board.library.hudBottom; }
            float margin = (board.library.boardMargin + marginNodes) * s;
            float boardW = layout.BoardSize.x + margin * 2f, boardH = layout.BoardSize.y + margin * 2f;
            Rect safe = respectSafeArea && Application.isPlaying ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            float safeBottom = safe.yMin / Mathf.Max(1, Screen.height), safeTop = 1f - safe.yMax / Mathf.Max(1, Screen.height);
            float safeLeft = safe.xMin / Mathf.Max(1, Screen.width), safeRight = 1f - safe.xMax / Mathf.Max(1, Screen.width);
            float usableH = Mathf.Max(0.2f, 1f - topReservedFraction - bottomReservedFraction - safeTop - safeBottom);
            float usableW = Mathf.Max(0.2f, 1f - safeLeft - safeRight);
            float size = Mathf.Max(boardH * 0.5f / usableH, boardW * 0.5f / (usableW * aspect));
            _cam.orthographicSize = size;
            float bandCentre = (bottomReservedFraction + safeBottom + (1f - topReservedFraction - safeTop)) * 0.5f;
            float offsetY = (0.5f - bandCentre) * 2f * size;
            float bandCentreX = (safeLeft + (1f - safeRight)) * 0.5f;
            float offsetX = (0.5f - bandCentreX) * 2f * size * aspect;
            var c = board.transform.position;
            _cam.transform.position = new Vector3(c.x + offsetX, c.y + offsetY, -10f);
            _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 50f;
        }
    }
}
