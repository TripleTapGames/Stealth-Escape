using ShadowSteps.Core;
using UnityEngine;

namespace ShadowSteps.Art2D
{
    /// <summary>
    /// One character token drawn from captured frames: body sprite (pivot at the feet), soft contact shadow, optional
    /// facing chevron. Plays idle or walk frames for the current facing; the renderer sets position, facing and
    /// walking from the authoritative state. Y-sorted inside the Actors layer so a token lower on screen draws in front.
    /// </summary>
    public sealed class SS2DCharacter : MonoBehaviour
    {
        public SS2DCharacterSet set;
        public SpriteRenderer body, shadow, chevron, ring;
        public Direction Facing { get; private set; } = Direction.South;
        public bool Walking { get; private set; }
        private float _clock;
        private int _frame;
        private Color _tint = Color.white;
        private float _markerY = 1f;
        public float MarkerY => _markerY;
        private Sprite _placeholder;
        private float _spacing = 1f;
        private Vector3 _bodyScale = Vector3.one, _shadowScale = Vector3.one; private Color _shadowColor = Color.white; private float _lift;

        public void Setup(SS2DSpriteLibrary lib, SS2DCharacterSet characterSet, Color accent, bool showChevron, float spacing, Sprite placeholder)
        {
            set = characterSet; _placeholder = placeholder; _spacing = spacing;
            if (body == null)
            {
                shadow = Make("Shadow", lib.shadowBlob, SS2DLayers.Actors, 0); shadow.color = lib.shadowTint;
                ring = Make("BaseRing", lib.nodeRing, SS2DLayers.Actors, 1); ring.color = new Color(accent.r, accent.g, accent.b, 0.9f);
                body = Make("Body", null, SS2DLayers.Actors, 2);
                chevron = Make("Facing", lib.arrow, SS2DLayers.Markers, 4); chevron.color = new Color(accent.r, accent.g, accent.b, 0.95f);
            }
            bool castShadow = lib.characterCastShadow != null;
            if (castShadow)
            {
                // Final cast shadow: screen-space light from the upper right (matches the planters), pivot on the base-disc centre, never rotated with the board.
                shadow.sprite = lib.characterCastShadow; shadow.color = lib.characterCastShadowTint;
                shadow.transform.localScale = Vector3.one * lib.characterCastShadowSize * spacing;
                shadow.transform.localPosition = Vector3.zero;
            }
            else
            {
                shadow.sprite = lib.shadowBlob; shadow.color = lib.shadowTint;
                shadow.transform.localScale = new Vector3(lib.characterShadow * spacing, lib.characterShadow * 0.55f * spacing, 1f);
                shadow.transform.localPosition = new Vector3(0f, -0.02f * spacing, 0f);
            }
            ring.transform.localScale = new Vector3(0.56f * spacing, 0.34f * spacing, 1f);
            ring.transform.localPosition = new Vector3(0f, 0.0f, 0f);
            body.transform.localScale = Vector3.one * lib.characterHeight * spacing;
            _bodyScale = body.transform.localScale; _shadowScale = shadow.transform.localScale; _shadowColor = shadow.color; _lift = 0f;
            // Final art paints its own base disc and shadow: the generated ring and blob would double it.
            bool ownBase = set == null || !set.includesBase;
            _showShadow = ownBase || castShadow; _showRing = ownBase; _showChevron = showChevron;
            shadow.gameObject.SetActive(_showShadow); ring.gameObject.SetActive(_showRing);
            chevron.gameObject.SetActive(showChevron);
            chevron.transform.localScale = Vector3.one * 0.24f * spacing;
            _markerY = lib.characterHeight * 0.92f * spacing;
            SetFacing(Facing);
            Apply();
        }

        private SpriteRenderer Make(string name, Sprite sprite, string layer, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingLayerName = layer; sr.sortingOrder = order;
            return sr;
        }

        public void SetFacing(Direction d)
        {
            Facing = d;
            if (chevron != null)
            {
                // Facing badge floats above the head so it reads for every direction, including "up" where a ground chevron would hide behind the body.
                chevron.transform.localPosition = new Vector3(0f, _markerY, 0f);
                chevron.transform.localRotation = Quaternion.Euler(0f, 0f, -90f * (int)d);
            }
            Apply();
        }

        /// <summary>
        /// D-065 hop pose: the token (body sprite, which carries its own base disc in the final art) lifts by <paramref name="pose"/>.Lift
        /// spacings and is stretched/squashed; the cast shadow stays on the ground, shrinking and fading with height; the facing badge rides along.
        /// </summary>
        public void ApplyHop(SS2DHop.Pose pose)
        {
            if (body == null) return;
            _lift = pose.Lift * _spacing;
            body.transform.localPosition = new Vector3(0f, _lift, 0f);
            body.transform.localScale = new Vector3(_bodyScale.x * pose.ScaleX, _bodyScale.y * pose.ScaleY, _bodyScale.z);
            if (shadow != null) { shadow.transform.localScale = _shadowScale * pose.ShadowScale; var c = _shadowColor; c.a *= pose.ShadowAlpha; shadow.color = c; }
            if (chevron != null) chevron.transform.localPosition = new Vector3(0f, _markerY + _lift, 0f);
        }
        /// <summary>Back on the ground at rest (called when a move tween ends or is cancelled).</summary>
        public void ClearHop() => ApplyHop(SS2DHop.Rest);
        public float Lift => _lift;

        public void SetWalking(bool walking) { if (Walking != walking) { Walking = walking; _clock = 0f; _frame = 0; Apply(); } }

        public void SetTint(Color? tint) { _tint = tint ?? Color.white; if (body != null) body.color = _tint; }

        public bool Defeated { get; private set; }
        /// <summary>Knocked-out pose (decorative only): the body lies on its side, dimmed, facing badge and base ring hidden, drawn under standing characters.</summary>
        public void SetDefeated(bool down, Color tint)
        {
            Defeated = down;
            if (body == null) return;
            body.transform.localRotation = Quaternion.identity; // the fly-off (renderer) animates the whole token; nothing lies on the board afterwards
            if (chevron != null) chevron.gameObject.SetActive(!down && _showChevron);
            if (ring != null) ring.gameObject.SetActive(!down && _showRing);
            if (shadow != null) shadow.gameObject.SetActive(!down && _showShadow);
            SetWalking(false);
            SetTint(down ? tint : (Color?)null);
        }
        private bool _showChevron, _showRing, _showShadow;

        public void SetSortingOrderFromY() { if (body != null) { int o = Mathf.RoundToInt(-transform.position.y * 10f) * 4 - (Defeated ? 40 : 0); body.sortingOrder = o + 2; shadow.sortingOrder = o; ring.sortingOrder = o + 1; } }

        /// <summary>Facing badge height follows the drawn sprite (its top edge plus a small gap), so any frame size or pivot works.</summary>
        private void PlaceMarker()
        {
            if (body != null && body.sprite != null)
                _markerY = body.sprite.bounds.max.y * _bodyScale.y + 0.10f * _spacing;
            if (chevron != null) chevron.transform.localPosition = new Vector3(0f, _markerY + _lift, 0f);
        }

        private void Update()
        {
            if (!Application.isPlaying || set == null) return;
            int n = set.FrameCount(Facing, Walking);
            if (n <= 1) return;
            float rate = Walking ? set.walkFrameRate : set.idleFrameRate;
            _clock += Time.deltaTime * rate;
            int f = Mathf.FloorToInt(_clock) % n;
            if (f != _frame) { _frame = f; Apply(); }
        }

        private void Apply()
        {
            if (body == null) return;
            var s = set != null ? set.Frame(Facing, Walking, _frame) : null;
            body.sprite = s != null ? s : _placeholder;
            body.color = _tint;
            PlaceMarker();
        }
    }

    /// <summary>
    /// D-065 movement feel, a pure function of time so it is testable: during the move (0..dur) the token rises and falls on a sine arc,
    /// stretching a little at the top while its shadow shrinks and fades; after touchdown (dur..dur+settle) it squashes and recovers.
    /// Original motion design for Shadow Steps (a lifted board piece), not copied from any other game.
    /// </summary>
    public static class SS2DHop
    {
        public struct Pose { public float Lift, ScaleX, ScaleY, ShadowScale, ShadowAlpha; }
        public static readonly Pose Rest = new Pose { Lift = 0f, ScaleX = 1f, ScaleY = 1f, ShadowScale = 1f, ShadowAlpha = 1f };

        /// <summary>Eased travel progress (0..1) at time <paramref name="t"/> of a move lasting <paramref name="dur"/>: fast in the middle, soft at both ends.</summary>
        public static float Travel(float t, float dur) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.0001f, dur)));

        public static Pose At(float t, float dur, float settle, float height, float stretch, float squash, float shadowShrink)
        {
            dur = Mathf.Max(0.0001f, dur);
            if (t <= 0f) return Rest;
            if (t < dur)
            {
                float a = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI); // 0 at take-off, 1 at the top, 0 at touchdown
                return new Pose { Lift = height * a, ScaleX = 1f - stretch * 0.5f * a, ScaleY = 1f + stretch * a, ShadowScale = 1f - shadowShrink * a, ShadowAlpha = 1f - 0.5f * shadowShrink * a };
            }
            if (settle > 0f && t < dur + settle)
            {
                float b = Mathf.Sin(Mathf.Clamp01((t - dur) / settle) * Mathf.PI); // one squash bounce
                return new Pose { Lift = 0f, ScaleX = 1f + squash * 0.6f * b, ScaleY = 1f - squash * b, ShadowScale = 1f, ShadowAlpha = 1f };
            }
            return Rest;
        }

        public static Pose At(SS2DSpriteLibrary lib, float t) => At(t, lib.moveDuration, lib.moveSettle, lib.moveHopHeight, lib.moveStretch, lib.moveSquash, lib.moveShadowShrink);
        public static float Total(SS2DSpriteLibrary lib) => Mathf.Max(0.01f, lib.moveDuration) + Mathf.Max(0f, lib.moveSettle);
    }
}
