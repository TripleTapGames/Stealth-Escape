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
        [Tooltip("Perspective with a narrow field of view gives the mild depth of the reference; orthographic is the flat fallback.")]
        public bool usePerspective = true;
        [Range(15f, 60f)] public float fieldOfView = 26f;
        [Tooltip("Yaw of the camera around the board (0 = looking north along +Z).")]
        public float yaw = 0f;
        [Range(0f, 0.5f)] public float topReservedFraction = 0.14f;
        [Range(0f, 0.5f)] public float bottomReservedFraction = 0.22f;
        [Tooltip("Extra world margin around the board bounds, in tiles. Small on purpose: the board is width-bound in portrait, so this directly sets how large the grid reads (0.55 ≈ parapets at the screen edge).")]
        public float paddingTiles = 0.78f;
        [Tooltip("The framed area is never smaller than this many tiles (sunlit theme: 6 × 4, so small FTUE boards keep a proportional platform instead of a microscopic one).")]
        public float minWidthTiles = 0f, minDepthTiles = 0f; // half-tile terrace border + plinth + a sliver of moat: nothing of the platform is cropped
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

        /// <summary>Half-height of the view at the board (orthographic size, or the perspective half-height at the board distance) and the camera position for the given aspect (pure; does not move the camera).</summary>
        public float SizeFor(Bounds boardBounds, float aspect, out Vector3 position)
        {
            _cam = _cam != null ? _cam : GetComponent<Camera>();
            var rot = Quaternion.Euler(downwardAngle, yaw, 0f);
            float pad = paddingTiles * (board != null ? board.tileSpacing : 1f);
            float ts = board != null ? board.tileSpacing : 1f;
            var framed = new Vector3(Mathf.Max(boardBounds.size.x, minWidthTiles * ts), boardBounds.size.y, Mathf.Max(boardBounds.size.z, minDepthTiles * ts));
            var b = new Bounds(boardBounds.center, framed + new Vector3(pad * 2f, 0f, pad * 2f));
            var inv = Quaternion.Inverse(rot);
            var local = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(b.center.x + b.extents.x * ((i & 1) == 0 ? -1 : 1), b.center.y + b.extents.y * ((i & 2) == 0 ? -1 : 1), b.center.z + b.extents.z * ((i & 4) == 0 ? -1 : 1));
                local[i] = inv * (corner - b.center); // camera space relative to the board centre; z grows away from the camera
            }
            float tanHalf = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distance = cameraDistance;
            float maxX = 0f, maxY = 0f;
            for (int pass = 0; pass < (usePerspective ? 3 : 1); pass++)
            {
                maxX = 0f; maxY = 0f;
                foreach (var pr in local)
                {
                    float k = usePerspective ? distance / Mathf.Max(0.5f, distance + pr.z) : 1f; // perspective: extent as seen at the board-centre depth
                    maxX = Mathf.Max(maxX, Mathf.Abs(pr.x) * k); maxY = Mathf.Max(maxY, Mathf.Abs(pr.y) * k);
                }
                if (usePerspective) distance = SizeFromExtents(maxX, maxY, aspect) / tanHalf;
            }
            Rect safe = respectSafeArea && Application.isPlaying ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            float safeBottom = safe.yMin / Mathf.Max(1, Screen.height), safeTop = 1f - safe.yMax / Mathf.Max(1, Screen.height);
            float safeLeft = safe.xMin / Mathf.Max(1, Screen.width), safeRight = 1f - safe.xMax / Mathf.Max(1, Screen.width);
            float size = SizeFromExtents(maxX, maxY, aspect);
            float bandCentre = (bottomReservedFraction + safeBottom + (1f - topReservedFraction - safeTop)) * 0.5f;
            float offsetY = (0.5f - bandCentre) * 2f * size;
            float bandCentreX = (safeLeft + (1f - safeRight)) * 0.5f;
            float offsetX = (0.5f - bandCentreX) * 2f * size * aspect;
            if (usePerspective) distance = size / tanHalf;
            position = b.center - rot * Vector3.forward * distance + rot * Vector3.up * offsetY + rot * Vector3.right * offsetX;
            return size;
        }

        /// <summary>Half-height of the view at the board-centre depth that fits the given camera-space extents into the usable band.</summary>
        private float SizeFromExtents(float maxX, float maxY, float aspect)
        {
            Rect safe = respectSafeArea && Application.isPlaying ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            float safeBottom = safe.yMin / Mathf.Max(1, Screen.height), safeTop = 1f - safe.yMax / Mathf.Max(1, Screen.height);
            float safeLeft = safe.xMin / Mathf.Max(1, Screen.width), safeRight = 1f - safe.xMax / Mathf.Max(1, Screen.width);
            float usableH = Mathf.Max(0.2f, 1f - topReservedFraction - bottomReservedFraction - safeTop - safeBottom);
            float usableW = Mathf.Max(0.2f, 1f - safeLeft - safeRight);
            return Mathf.Max(minimumSize, Mathf.Max(maxY / usableH, maxX / (usableW * aspect)));
        }

        /// <summary>Direction of the view ray through a viewport point (u,v in −1..1), for either projection.</summary>
        private Vector3 RayDirection(Quaternion rot, float u, float v, float aspect)
        {
            if (!usePerspective) return rot * Vector3.forward;
            float t = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            return (rot * new Vector3(u * t * aspect, v * t, 1f)).normalized;
        }

        /// <summary>Where the four screen corners (bottom-left, bottom-right, top-left, top-right) hit the ground plane for the given aspect; false when the camera cannot be evaluated.</summary>
        public bool VisibleGroundCorners(float aspect, float planeY, Vector3[] corners)
        {
            if (!_hasBounds && board != null && board.IsBuilt) { _lastBounds = board.BoardBounds(); _hasBounds = true; }
            if (!_hasBounds || corners == null || corners.Length < 4) return false;
            float halfH = SizeFor(_lastBounds, aspect, out var centre), halfW = halfH * aspect;
            var rot = Quaternion.Euler(downwardAngle, yaw, 0f);
            Vector3 right = rot * Vector3.right, up = rot * Vector3.up;
            var uv = new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                var origin = usePerspective ? centre : centre + right * uv[i].x * halfW + up * uv[i].y * halfH;
                var dir = RayDirection(rot, uv[i].x, uv[i].y, aspect);
                if (Mathf.Abs(dir.y) < 1e-4f) return false;
                float t = (planeY - origin.y) / dir.y;
                corners[i] = origin + dir * t;
            }
            return true;
        }

        /// <summary>Ground-plane rectangle (XZ, world) that would be visible for the given aspect, used to extend decoration to the screen edges.</summary>
        public Rect VisibleGroundRect(float aspect, float planeY = 0f)
        {
            var c = new Vector3[4];
            if (!VisibleGroundCorners(aspect, planeY, c)) return Rect.MinMaxRect(-6f, -6f, 6f, 6f);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var hit in c) { minX = Mathf.Min(minX, hit.x); maxX = Mathf.Max(maxX, hit.x); minZ = Mathf.Min(minZ, hit.z); maxZ = Mathf.Max(maxZ, hit.z); }
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        public void Fit(Bounds boardBounds)
        {
            _cam = _cam != null ? _cam : GetComponent<Camera>();
            _lastBounds = boardBounds; _hasBounds = true;
            _cam.orthographic = !usePerspective;
            _cam.transform.rotation = Quaternion.Euler(downwardAngle, yaw, 0f);
            float size = SizeFor(boardBounds, Mathf.Max(0.4f, _cam.aspect), out var position);
            if (usePerspective) _cam.fieldOfView = fieldOfView; else _cam.orthographicSize = size;
            _cam.transform.position = position;
            _cam.nearClipPlane = 0.1f; _cam.farClipPlane = Vector3.Distance(position, boardBounds.center) + 60f;
        }
    }
}
