using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Fixed, slightly angled orthographic camera for portrait mobile play. Frames the whole board (plus the parapets)
    /// inside the viewport band left free by the HUD and the device safe area. No follow, zoom or panning; the angle only
    /// changes rendering, never the XZ node logic.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class SS3DBoardCameraFitter : MonoBehaviour
    {
        [Range(35f, 89f)] public float downwardAngle = 58f;
        [Tooltip("Yaw of the camera around the board (0 = looking north along +Z).")]
        public float yaw = 0f;
        [Range(0f, 0.5f)] public float topReservedFraction = 0.14f;
        [Range(0f, 0.5f)] public float bottomReservedFraction = 0.22f;
        [Tooltip("Extra world margin around the board bounds, in tiles (covers the parapets).")]
        public float paddingTiles = 0.9f;
        [Tooltip("Tallest portrait aspect (width/height) the decoration must fill; 9:19.5 covers modern phones.")]
        public float designAspect = 9f / 19.5f;
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

        private Bounds _lastBounds; private bool _hasBounds;

        /// <summary>Orthographic size and camera position the fitter would choose for the given aspect (pure; does not move the camera).</summary>
        public float SizeFor(Bounds boardBounds, float aspect, out Vector3 position)
        {
            _cam = _cam != null ? _cam : GetComponent<Camera>();
            var rot = Quaternion.Euler(downwardAngle, yaw, 0f);
            float pad = paddingTiles * (board != null ? board.tileSpacing : 1f);
            var b = new Bounds(boardBounds.center, boardBounds.size + new Vector3(pad * 2f, 0f, pad * 2f));
            var inv = Quaternion.Inverse(rot);
            float maxX = 0f, maxY = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(b.center.x + b.extents.x * ((i & 1) == 0 ? -1 : 1), b.center.y + b.extents.y * ((i & 2) == 0 ? -1 : 1), b.center.z + b.extents.z * ((i & 4) == 0 ? -1 : 1));
                var projected = inv * (corner - b.center);
                maxX = Mathf.Max(maxX, Mathf.Abs(projected.x)); maxY = Mathf.Max(maxY, Mathf.Abs(projected.y));
            }
            Rect safe = respectSafeArea && Application.isPlaying ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            float safeBottom = safe.yMin / Mathf.Max(1, Screen.height), safeTop = 1f - safe.yMax / Mathf.Max(1, Screen.height);
            float safeLeft = safe.xMin / Mathf.Max(1, Screen.width), safeRight = 1f - safe.xMax / Mathf.Max(1, Screen.width);
            float usableH = Mathf.Max(0.2f, 1f - topReservedFraction - bottomReservedFraction - safeTop - safeBottom);
            float usableW = Mathf.Max(0.2f, 1f - safeLeft - safeRight);
            float size = Mathf.Max(minimumSize, Mathf.Max(maxY / usableH, maxX / (usableW * aspect)));
            float bandCentre = (bottomReservedFraction + safeBottom + (1f - topReservedFraction - safeTop)) * 0.5f;
            float offsetY = (0.5f - bandCentre) * 2f * size;
            float bandCentreX = (safeLeft + (1f - safeRight)) * 0.5f;
            float offsetX = (0.5f - bandCentreX) * 2f * size * aspect;
            position = b.center - rot * Vector3.forward * cameraDistance + rot * Vector3.up * offsetY + rot * Vector3.right * offsetX;
            return size;
        }

        /// <summary>Ground-plane rectangle (XZ, world) that would be visible for the given aspect, used to extend decoration to the screen edges.</summary>
        public Rect VisibleGroundRect(float aspect, float planeY = 0f)
        {
            if (!_hasBounds && board != null && board.IsBuilt) { _lastBounds = board.BoardBounds(); _hasBounds = true; }
            float halfH = SizeFor(_lastBounds, aspect, out var centre), halfW = halfH * aspect;
            var rot = Quaternion.Euler(downwardAngle, yaw, 0f);
            Vector3 right = rot * Vector3.right, up = rot * Vector3.up, dir = rot * Vector3.forward;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var corner in new[] { new Vector2(-halfW, -halfH), new Vector2(halfW, -halfH), new Vector2(-halfW, halfH), new Vector2(halfW, halfH) })
            {
                var origin = centre + right * corner.x + up * corner.y;
                if (Mathf.Abs(dir.y) < 1e-4f) continue;
                float t = (planeY - origin.y) / dir.y;
                var hit = origin + dir * t;
                minX = Mathf.Min(minX, hit.x); maxX = Mathf.Max(maxX, hit.x); minZ = Mathf.Min(minZ, hit.z); maxZ = Mathf.Max(maxZ, hit.z);
            }
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        public void Fit(Bounds boardBounds)
        {
            _cam = _cam != null ? _cam : GetComponent<Camera>();
            _lastBounds = boardBounds; _hasBounds = true;
            _cam.orthographic = true;
            _cam.transform.rotation = Quaternion.Euler(downwardAngle, yaw, 0f);
            _cam.orthographicSize = SizeFor(boardBounds, Mathf.Max(0.4f, _cam.aspect), out var position);
            _cam.transform.position = position;
            _cam.nearClipPlane = 0.1f; _cam.farClipPlane = cameraDistance + 50f;
        }
    }
}
