using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Keeps a strict top-down orthographic camera (looking down -Z at the XY board plane) sized so the whole board
    /// fits inside the screen area left free by the HUD and the device safe area, on any portrait aspect ratio.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        public BoardView Board;
        [Range(0f, 0.5f)] public float TopReservedFraction = 0.14f;
        [Range(0f, 0.5f)] public float BottomReservedFraction = 0.22f;
        [Tooltip("Extra margin around the board in node spacings.")]
        public float MarginNodes = 0.6f;
        public bool RespectSafeArea = true;

        private Camera _cam;
        private int _lastW, _lastH;
        private int _lastBoardHash;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.transform.rotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            if (Board == null || !Board.IsBuilt) return;
            int hash = Board.Stage.GetHashCode() ^ Board.Layout.Width * 31 ^ Board.Layout.Height * 1031;
            if (Screen.width == _lastW && Screen.height == _lastH && hash == _lastBoardHash) return;
            Fit();
            _lastW = Screen.width; _lastH = Screen.height; _lastBoardHash = hash;
        }

        public void Fit()
        {
            if (Board == null || !Board.IsBuilt) return;
            _cam = _cam != null ? _cam : GetComponent<Camera>();
            var layout = Board.Layout;
            float margin = MarginNodes * layout.NodeSpacing;
            float boardW = layout.BoardSize.x + margin * 2f;
            float boardH = layout.BoardSize.y + margin * 2f;

            // Usable viewport fractions after safe area and HUD reservations.
            Rect safe = RespectSafeArea ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            float safeBottom = safe.yMin / Screen.height, safeTop = 1f - safe.yMax / Screen.height;
            float safeLeft = safe.xMin / Screen.width, safeRight = 1f - safe.xMax / Screen.width;
            float usableH = Mathf.Max(0.2f, 1f - TopReservedFraction - BottomReservedFraction - safeTop - safeBottom);
            float usableW = Mathf.Max(0.2f, 1f - safeLeft - safeRight);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);

            float sizeForHeight = boardH * 0.5f / usableH;
            float sizeForWidth = boardW * 0.5f / (usableW * aspect);
            _cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

            // Centre the board in the usable band: shift camera so that the band centre maps to the board centre.
            float bandCentre = (BottomReservedFraction + safeBottom + (1f - TopReservedFraction - safeTop)) * 0.5f; // 0..1 from bottom
            float offsetY = (0.5f - bandCentre) * 2f * _cam.orthographicSize;
            float bandCentreX = (safeLeft + (1f - safeRight)) * 0.5f;
            float offsetX = (0.5f - bandCentreX) * 2f * _cam.orthographicSize * aspect;
            var boardCentre = Board.transform.position;
            _cam.transform.position = new Vector3(boardCentre.x + offsetX, boardCentre.y + offsetY, -10f);
        }
    }
}
