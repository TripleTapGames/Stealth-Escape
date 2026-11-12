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
    /// D-080: a board the renderer flags as <see cref="ShadowSteps2DRenderer.PanHorizontal"/> is zoomed to its height only;
    /// the width that does not fit becomes a horizontal pan range. The view follows the player (smoothly, after every
    /// move) and can be dragged by hand; both only move this camera and never touch the session.
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
        private float _homeX, _homeY, _offsetX, _panX;

        /// <summary>Half the horizontal overflow of the board beyond the view in world units; 0 when the whole board is in view (every board narrower than the pan threshold).</summary>
        public float PanRange { get; private set; }
        /// <summary>Current pan, in world units, within ±<see cref="PanRange"/> (0 = board centred).</summary>
        public float PanX => _panX;
        /// <summary>When true the view glides toward the player every frame (set again by the bridge at every counted action); a manual drag turns it off until the next move.</summary>
        public bool FollowPlayer { get; set; } = true;
        /// <summary>True while a drag gesture is in progress (the follow waits for it to finish).</summary>
        public bool Dragging { get; set; }

        private void LateUpdate()
        {
            if (board == null || !board.IsBuilt) return;
            int hash = board.Stage.GetHashCode();
            if (Screen.width != _lastW || Screen.height != _lastH || hash != _lastHash)
            {
                Fit((float)Screen.width / Mathf.Max(1, Screen.height));
                _lastW = Screen.width; _lastH = Screen.height; _lastHash = hash;
            }
            if (PanRange > 0f && FollowPlayer && !Dragging && Application.isPlaying) FollowStep(Time.unscaledDeltaTime);
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

        /// <summary>
        /// Picks the zoom reference for the pack: the library's <see cref="SS2DSpriteLibrary.zoomReferenceGrid"/> (D-057: boards up to that grid share one
        /// zoom and read big; bigger boards zoom out to stay fully visible), or the pack's largest grid when the library asks for it (D-039).
        /// Refits on the next frame.
        /// </summary>
        public void SetReferenceFromPack(IEnumerable<Stage> stages)
        {
            var lib = board != null ? board.library : null;
            var g = lib != null && !lib.zoomReferenceFromPackLargest ? lib.zoomReferenceGrid : LargestPortraitGrid(stages);
            referenceGridNodes = g.x > 0 && g.y > 0 ? g : Vector2Int.zero;
            _lastW = _lastH = _lastHash = 0;
        }

        /// <summary>The reference the pack would use with this library (for tests and tools).</summary>
        public static Vector2Int ReferenceFor(SS2DSpriteLibrary lib, IEnumerable<Stage> stages) => lib != null && !lib.zoomReferenceFromPackLargest ? lib.zoomReferenceGrid : LargestPortraitGrid(stages);

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
            var reference = ReferenceExtent(s); float framedW = Mathf.Max(boardW, reference.x), framedH = Mathf.Max(boardH, reference.y);
            Rect safe = respectSafeArea && Application.isPlaying ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            float safeBottom = safe.yMin / Mathf.Max(1, Screen.height), safeTop = 1f - safe.yMax / Mathf.Max(1, Screen.height);
            float safeLeft = safe.xMin / Mathf.Max(1, Screen.width), safeRight = 1f - safe.xMax / Mathf.Max(1, Screen.width);
            float usableH = Mathf.Max(0.2f, 1f - topReservedFraction - bottomReservedFraction - safeTop - safeBottom);
            float usableW = Mathf.Max(0.2f, 1f - safeLeft - safeRight);
            float size;
            if (board.PanHorizontal)
            {
                // D-080: the height (and the shared reference zoom) set the zoom; the width beyond the view is panned, never shrunk.
                size = Mathf.Max(framedH * 0.5f / usableH, reference.x * 0.5f / (usableW * aspect));
                float viewW = 2f * size * aspect * usableW;
                PanRange = Mathf.Max(0f, (boardW - viewW) * 0.5f);
            }
            else
            {
                size = Mathf.Max(framedH * 0.5f / usableH, framedW * 0.5f / (usableW * aspect));
                PanRange = 0f;
            }
            _cam.orthographicSize = size;
            float bandCentre = (bottomReservedFraction + safeBottom + (1f - topReservedFraction - safeTop)) * 0.5f;
            float offsetY = (0.5f - bandCentre) * 2f * size;
            float bandCentreX = (safeLeft + (1f - safeRight)) * 0.5f;
            _offsetX = (0.5f - bandCentreX) * 2f * size * aspect;
            var c = board.transform.TransformPoint(new Vector3(vb.center.x, vb.center.y, 0f));
            _homeX = c.x + _offsetX; _homeY = c.y + offsetY;
            _panX = Mathf.Clamp(_panX, -PanRange, PanRange);
            ApplyPan();
            board.ReferenceGridNodes = referenceGridNodes; board.PanRange = PanRange; board.PanHomeX = _homeX; board.FitBackground(_cam);
            _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 50f;
        }

        private void ApplyPan()
        {
            if (_cam == null) return;
            _cam.transform.position = new Vector3(_homeX + _panX, _homeY, -10f);
        }

        /// <summary>Pan the player wants, in world units: the player's drawn x centred in the band, clamped to the board.</summary>
        public float PlayerPanTarget()
        {
            if (board == null || !board.IsBuilt || board.Player == null) return 0f;
            float boardCentreX = _homeX - _offsetX;
            return Mathf.Clamp(board.Player.transform.position.x - boardCentreX, -PanRange, PanRange);
        }

        /// <summary>One frame of the glide toward the player (exponential ease by <see cref="SS2DSpriteLibrary.panFollowSmoothing"/>).</summary>
        public void FollowStep(float dt)
        {
            if (PanRange <= 0f) return;
            float target = PlayerPanTarget();
            float k = board != null && board.library != null ? board.library.panFollowSmoothing : 6f;
            _panX = Mathf.Lerp(_panX, target, 1f - Mathf.Exp(-Mathf.Max(0f, k) * Mathf.Max(0f, dt)));
            if (Mathf.Abs(_panX - target) < 1e-4f) _panX = target;
            ApplyPan();
        }

        /// <summary>Jumps the view onto the player (stage load, retry) and resumes following.</summary>
        public void SnapToPlayer()
        {
            FollowPlayer = true; Dragging = false;
            if (PanRange <= 0f) { _panX = 0f; ApplyPan(); return; }
            _panX = PlayerPanTarget(); ApplyPan();
        }

        /// <summary>Manual pan by a screen-space drag (pixels, positive = finger moved right, so the board follows the finger). Stops the follow until the next counted action. Never a game action.</summary>
        public void PanByScreenPixels(float dx)
        {
            if (PanRange <= 0f || _cam == null) return;
            float worldPerPixel = _cam.orthographicSize * 2f * _cam.aspect / Mathf.Max(1, _cam.pixelWidth);
            PanByWorld(-dx * worldPerPixel);
        }

        /// <summary>Manual pan in world units (positive = view moves right). Stops the follow until the next counted action.</summary>
        public void PanByWorld(float dx)
        {
            if (PanRange <= 0f) return;
            FollowPlayer = false;
            _panX = Mathf.Clamp(_panX + dx, -PanRange, PanRange);
            ApplyPan();
        }
    }
}
