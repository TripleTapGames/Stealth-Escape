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
            // Final art paints its own base disc and shadow: the generated ring and blob would double it.
            bool ownBase = set == null || !set.includesBase;
            shadow.gameObject.SetActive(ownBase || castShadow); ring.gameObject.SetActive(ownBase);
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

        public void SetWalking(bool walking) { if (Walking != walking) { Walking = walking; _clock = 0f; _frame = 0; Apply(); } }

        public void SetTint(Color? tint) { _tint = tint ?? Color.white; if (body != null) body.color = _tint; }

        public void SetSortingOrderFromY() { if (body != null) { int o = Mathf.RoundToInt(-transform.position.y * 10f) * 4; body.sortingOrder = o + 2; shadow.sortingOrder = o; ring.sortingOrder = o + 1; } }

        /// <summary>Facing badge height follows the drawn sprite (its top edge plus a small gap), so any frame size or pivot works.</summary>
        private void PlaceMarker()
        {
            if (body != null && body.sprite != null)
                _markerY = body.sprite.bounds.max.y * body.transform.localScale.y + 0.10f * _spacing;
            if (chevron != null) chevron.transform.localPosition = new Vector3(0f, _markerY, 0f);
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
}
