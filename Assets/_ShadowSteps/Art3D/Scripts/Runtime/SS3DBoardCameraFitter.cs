using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Fixed, slightly angled orthographic camera for portrait mobile play. Frames the whole board (plus the parapets)
    /// inside the viewport band left free by the HUD and the device safe area. No follow, zoom or panning; the angle only
    /// changes rendering, never the XZ node logic.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SS3DBoardCameraFitter : MonoBehaviour
    {
        [Range(35f, 89f)] public float downwardAngle = 63f;
        [Tooltip("Yaw of the camera around the board (0 = looking north along +Z).")]
        public float yaw = 0f;
        [Range(0f, 0.5f)] public float topReservedFraction = 0.14f;
        [Range(0f, 0.5f)] public float bottomReservedFraction = 0.22f;
        [Tooltip("Extra world margin around the board bounds, in tiles (covers parapets/canals).")]
        public float paddingTiles = 1.0f;
        public float minimumSize = 2.4f;
        public float cameraDistance = 18f;
        public bool respectSafeArea = true;
        public SS3DLevelRenderer board;

        private Camera _cam;
        private int _lastW, _lastH, _lastHash;

        private void Awake() { _cam = GetComponent<Camera>(); }

        private void LateUpdate()
        {
            if (board == null || !board.IsBuilt) return;
            int hash = board.Stage.GetHashCode();
            if (Screen.width == _lastW && Screen.height == _lastH && hash == _lastHash) return;
            Fit(board.BoardBounds());
            _lastW = Screen.width; _lastH = Screen.height; _lastHash = hash;
        }

        public void Fit(Bounds boardBounds)
        {
            _cam = _cam != null ? _cam : GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.transform.rotation = Quaternion.Euler(downwardAngle, yaw, 0f);
            float pad = paddingTiles * (board != null ? board.tileSpacing : 1f);
            var b = new Bounds(boardBounds.center, boardBounds.size + new Vector3(pad * 2f, 0f, pad * 2f));
            var inv = Quaternion.Inverse(_cam.transform.rotation);
            float maxX = 0f, maxY = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(b.center.x + b.extents.x * ((i & 1) == 0 ? -1 : 1), b.center.y + b.extents.y * ((i & 2) == 0 ? -1 : 1), b.center.z + b.extents.z * ((i & 4) == 0 ? -1 : 1));
                var projected = inv * (corner - b.center);
                maxX = Mathf.Max(maxX, Mathf.Abs(projected.x));
                maxY = Mathf.Max(maxY, Mathf.Abs(projected.y));
            }
            Rect safe = respectSafeArea ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            float safeBottom = safe.yMin / Mathf.Max(1, Screen.height), safeTop = 1f - safe.yMax / Mathf.Max(1, Screen.height);
            float safeLeft = safe.xMin / Mathf.Max(1, Screen.width), safeRight = 1f - safe.xMax / Mathf.Max(1, Screen.width);
            float usableH = Mathf.Max(0.2f, 1f - topReservedFraction - bottomReservedFraction - safeTop - safeBottom);
            float usableW = Mathf.Max(0.2f, 1f - safeLeft - safeRight);
            float aspect = Mathf.Max(0.4f, _cam.aspect);
            _cam.orthographicSize = Mathf.Max(minimumSize, Mathf.Max(maxY / usableH, maxX / (usableW * aspect)));
            float bandCentre = (bottomReservedFraction + safeBottom + (1f - topReservedFraction - safeTop)) * 0.5f;
            float offsetY = (0.5f - bandCentre) * 2f * _cam.orthographicSize;
            float bandCentreX = (safeLeft + (1f - safeRight)) * 0.5f;
            float offsetX = (0.5f - bandCentreX) * 2f * _cam.orthographicSize * aspect;
            _cam.transform.position = b.center - _cam.transform.forward * cameraDistance + _cam.transform.up * offsetY + _cam.transform.right * offsetX;
            _cam.nearClipPlane = 0.1f; _cam.farClipPlane = cameraDistance + 50f;
        }
    }
}
