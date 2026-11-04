using System.Collections.Generic;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art2D
{
    /// <summary>
    /// Fixed top-down orthographic camera for the 2D presentation: fits the board panel (grid + margin) into the
    /// portrait band left free by the HUD and the device safe area, same arithmetic as the runtime
    /// <see cref="ShadowSteps.Runtime.CameraFitter"/> so taps resolve identically. The zoom never goes tighter than
    /// <see cref="referenceGridNodes"/> (the pack's largest grid), so nodes, characters and the ground keep one size on
    /// every level; smaller boards sit centred in the band with more surround instead of being blown up.
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
        [Header("Consistent zoom across levels")]
        [Tooltip("Every level is framed at least as wide and as tall as this grid (portrait orientation: short side × long side, in nodes), so sprites read the same size on every level. The bridge and the preview set it to the loaded pack's largest grid (SetReferenceFromPack); 0 × 0 = fit each board on its own.")]
        public Vector2Int referenceGridNodes = new Vector2Int(9, 11);
        private Camera _cam; private int _lastW, _lastH, _lastHash;

        private void LateUpdate()
        {
            if (board == null || !board.IsBuilt) return;
            int hash = board.Stage.GetHashCode();
            if (Screen.width == _lastW && Screen.height == _lastH && hash == _lastHash) return;
            Fit((float)Screen.width / Mathf.Max(1, Screen.height));
            _lastW = Screen.width; _lastH = Screen.height; _lastHash = hash;
        }

        /// <summary>Largest grid in the pack in portrait orientation (short side, long side), the zoom every level is framed at.</summary>
        public static Vector2Int LargestPortraitGrid(IEnumerable<Stage> stages)
        {
            int w = 0, h = 0;
            if (stages != null)
                foreach (var st in stages)
                {
                    if (st == null) continue;
                    w = Mathf.Max(w, Mathf.Min(st.Width, st.Height)); h = Mathf.Max(h, Mathf.Max(st.Width, st.Height));
                }
            return new Vector2Int(w, h);
        }

        /// <summary>Frames every level at the zoom of the pack's largest grid; refits on the next frame.</summary>
        public void SetReferenceFromPack(IEnumerable<Stage> stages)
        {
            var g = LargestPortraitGrid(stages);
            if (g.x > 0 && g.y > 0) referenceGridNodes = g;
            _lastW = _lastH = _lastHash = 0;
        }

        /// <summary>Width and height, in world units, of the smallest view the fitter frames (reference grid + board margin + fitter margin); zero when no reference is set.</summary>
        public Vector2 ReferenceExtent(float spacing)
        {
            if (referenceGridNodes.x <= 0 || referenceGridNodes.y <= 0) return Vector2.zero;
            float m = board != null && board.library != null ? board.library.boardMargin : 0f; // same panel arithmetic as the renderer
            return new Vector2((referenceGridNodes.x + 2f * m + 2f * marginNodes) * spacing, (referenceGridNodes.y + 2f * m + 2f * marginNodes) * spacing);
        }

        public void Fit(float aspect)
        {
            if (board == null || !board.IsBuilt) return;
            _cam = _cam != null ? _cam : GetComponent<Camera>();
            _cam.orthographic = true; _cam.transform.rotation = Quaternion.identity;
            var layout = board.ViewLayout; float s = layout.NodeSpacing;
            if (board.library != null) { topReservedFraction = board.library.hudTop; bottomReservedFraction = board.library.hudBottom; }
            var vb = board.ViewBounds; // panel + door, already including the board margin
            float boardW = vb.width + marginNodes * s * 2f, boardH = vb.height + marginNodes * s * 2f;
            var reference = ReferenceExtent(s); boardW = Mathf.Max(boardW, reference.x); boardH = Mathf.Max(boardH, reference.y);
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
            var c = board.transform.TransformPoint(new Vector3(vb.center.x, vb.center.y, 0f));
            _cam.transform.position = new Vector3(c.x + offsetX, c.y + offsetY, -10f);
            board.ReferenceGridNodes = referenceGridNodes; board.FitBackground(_cam);
            _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 50f;
        }
    }
}
