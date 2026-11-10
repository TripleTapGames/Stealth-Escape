using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>
    /// Spotlight tutorial overlay (D-077): dims the whole screen except one rectangular cut-out (the gadget button, a node or
    /// a camera), frames the cut-out with a pulsing accent border, points at it with a tapping hand and shows one line of
    /// copy beside it. The four dim panels are raycast targets, so nothing outside the cut-out can be tapped (board taps are
    /// ignored over UI, HUD buttons are covered); the cut-out itself is empty, so the thing underneath receives the tap.
    /// Not a modal: gameplay input stays unlocked. The cut-out follows a provider every frame (camera fit, screen rotation).
    /// Shown and hidden directly by <see cref="GameFlowController"/>, never through the router stack.
    /// </summary>
    public sealed class FtueSpotlightScreen : UIScreen
    {
        public Image Top, Bottom, Left, Right;
        public RectTransform Frame;
        public RectTransform Hand;
        public RectTransform MessagePill;
        public UiText MessageText;
        public Color DimColor = new Color(0.02f, 0.03f, 0.06f, 0.78f);
        public Color AccentColor = UiBuilder.ButtonAccent;
        [Tooltip("Extra pixels around the target left open.")] public float Padding = 14f;

        private Func<Rect?> _hole;
        private Rect? _lastHole;
        public bool ReducedMotion;

        /// <summary>The current cut-out in screen pixels (null = nothing visible yet).</summary>
        public Rect? Hole => _lastHole;

        public override void BuildHierarchy()
        {
            IsModal = false;
            FadeDuration = 0.1f;
            UiBuilder.Anchor(Rect, 0f, 0f, 1f, 1f);
            Top = Panel("Top"); Bottom = Panel("Bottom"); Left = Panel("Left"); Right = Panel("Right");
            Frame = UiBuilder.CreateRect("Frame", transform);
            Centered(Frame);
            foreach (var side in new[] { "N", "S", "W", "E" }) { var bar = UiBuilder.CreatePanel("Bar" + side, Frame, AccentColor); bar.GetComponent<Image>().raycastTarget = false; Centered(bar); }
            Hand = BuildHand();
            MessagePill = UiBuilder.CreatePanel("MessagePill", transform, new Color(0.09f, 0.11f, 0.16f, 0.96f));
            MessagePill.GetComponent<Image>().raycastTarget = false;
            Centered(MessagePill);
            MessageText = UiBuilder.CreateText("Message", MessagePill, "", 34f, TextAnchor.MiddleCenter, true);
            UiBuilder.Anchor(MessageText.Rect, 0f, 0f, 1f, 1f, 18f);
            MessageText.Rect.GetComponent<UiText>();
        }

        private Image Panel(string name)
        {
            var rt = UiBuilder.CreatePanel(name, transform, DimColor);
            Centered(rt);
            var img = rt.GetComponent<Image>(); img.raycastTarget = true;
            return img;
        }

        private static void Centered(RectTransform rt) { rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); }

        /// <summary>The user's hand art from the skin when present (pivot on the fingertip), else a simple pointing hand built from three shapes.</summary>
        private RectTransform BuildHand()
        {
            var skin = UiSkin.Current;
            if (skin != null && skin.tapHand != null)
            {
                var art = UiBuilder.CreateRect("Hand", transform);
                art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
                art.pivot = skin.tapHandTip; // the fingertip sits on anchoredPosition
                var img = art.gameObject.AddComponent<Image>(); img.sprite = skin.tapHand; img.raycastTarget = false; img.preserveAspect = true;
                const float height = 230f; var sz = skin.tapHand.rect.size;
                art.sizeDelta = new Vector2(height * sz.x / Mathf.Max(1f, sz.y), height);
                HandIsArt = true;
                return art;
            }
            var hand = UiBuilder.CreateRect("Hand", transform); Centered(hand);
            hand.sizeDelta = new Vector2(120f, 180f);
            var circle = CircleSprite();
            var shadow = Shape("Shadow", hand, circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(14f, -118f), new Vector2(104f, 104f));
            var palm = Shape("Palm", hand, circle, Color.white, new Vector2(8f, -112f), new Vector2(96f, 96f));
            var finger = Shape("Finger", hand, null, Color.white, new Vector2(0f, -48f), new Vector2(44f, 110f));
            var tip = Shape("Tip", hand, circle, Color.white, Vector2.zero, new Vector2(48f, 48f));
            var nail = Shape("Nail", hand, circle, AccentColor, Vector2.zero, new Vector2(22f, 22f));
            _ = shadow; _ = palm; _ = finger; _ = tip; _ = nail;
            return hand;
        }

        private static RectTransform Shape(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size)
        {
            var rt = UiBuilder.CreatePanel(name, parent, color);
            Centered(rt);
            var img = rt.GetComponent<Image>(); img.raycastTarget = false; img.sprite = sprite; img.type = Image.Type.Simple; img.preserveAspect = sprite != null;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>True when the hand is the skin's sprite rather than the procedural shapes.</summary>
        public bool HandIsArt { get; private set; }

        private static Sprite _circle;
        /// <summary>A soft-edged white disc generated once at runtime (no asset dependency).</summary>
        public static Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            const int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                }
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _circle;
        }

        private bool _wired;
        protected override void Awake() { base.Awake(); _wired = true; }

        /// <summary>Shows the overlay around whatever <paramref name="hole"/> returns (screen pixels); a null hole dims everything without a cut-out.</summary>
        public void Present(Func<Rect?> hole, string message)
        {
            _hole = hole;
            if (MessageText != null) MessageText.Text = message ?? string.Empty;
            if (MessagePill != null) MessagePill.gameObject.SetActive(!string.IsNullOrEmpty(message));
            Layout();
        }

        private void LateUpdate() { if (IsVisible) Layout(); }

        private void Layout()
        {
            if (Top == null) return;
            var full = Rect.rect;
            Rect? screen = _hole != null ? _hole() : null;
            _lastHole = screen;
            if (!screen.HasValue)
            {
                Place(Top.rectTransform, full.xMin, full.yMin, full.xMax, full.yMax);
                Place(Bottom.rectTransform, 0, 0, 0, 0); Place(Left.rectTransform, 0, 0, 0, 0); Place(Right.rectTransform, 0, 0, 0, 0);
                Frame.gameObject.SetActive(false); Hand.gameObject.SetActive(false);
                if (MessagePill.gameObject.activeSelf) Place(MessagePill, full.xMin + 60f, full.yMin + 420f, full.xMax - 60f, full.yMin + 520f);
                return;
            }
            var r = screen.Value;
            var a = ToLocal(new Vector2(r.xMin - Padding, r.yMin - Padding));
            var b = ToLocal(new Vector2(r.xMax + Padding, r.yMax + Padding));
            float hx0 = Mathf.Max(full.xMin, Mathf.Min(a.x, b.x)), hx1 = Mathf.Min(full.xMax, Mathf.Max(a.x, b.x));
            float hy0 = Mathf.Max(full.yMin, Mathf.Min(a.y, b.y)), hy1 = Mathf.Min(full.yMax, Mathf.Max(a.y, b.y));
            Place(Top.rectTransform, full.xMin, hy1, full.xMax, full.yMax);
            Place(Bottom.rectTransform, full.xMin, full.yMin, full.xMax, hy0);
            Place(Left.rectTransform, full.xMin, hy0, hx0, hy1);
            Place(Right.rectTransform, hx1, hy0, full.xMax, hy1);

            // Pulsing frame around the cut-out.
            float t = Time.unscaledTime;
            float grow = ReducedMotion ? 0f : 6f + 6f * Mathf.Sin(t * 4f);
            float cx = (hx0 + hx1) * 0.5f, cy = (hy0 + hy1) * 0.5f, w = hx1 - hx0 + grow * 2f, h = hy1 - hy0 + grow * 2f;
            const float thick = 5f;
            Frame.gameObject.SetActive(true);
            Frame.anchoredPosition = new Vector2(cx, cy); Frame.sizeDelta = new Vector2(w, h);
            Bar(Frame, 0, new Vector2(0f, h * 0.5f - thick * 0.5f), new Vector2(w, thick));
            Bar(Frame, 1, new Vector2(0f, -h * 0.5f + thick * 0.5f), new Vector2(w, thick));
            Bar(Frame, 2, new Vector2(-w * 0.5f + thick * 0.5f, 0f), new Vector2(thick, h));
            Bar(Frame, 3, new Vector2(w * 0.5f - thick * 0.5f, 0f), new Vector2(thick, h));

            // Hand: fingertip on the target, bobbing in from the lower right as a tap; mirrored to come from above when the target sits near the bottom edge.
            Hand.gameObject.SetActive(true);
            float bob = ReducedMotion ? 0f : Mathf.Abs(Mathf.Sin(t * 3f)) * 26f;
            bool fromAbove = cy - 220f < full.yMin;
            float dir = fromAbove ? -1f : 1f;
            Hand.anchoredPosition = new Vector2(cx + 30f + bob * 0.6f, cy - dir * (34f + bob));
            float press = ReducedMotion ? 1f : 1f - 0.06f * Mathf.Abs(Mathf.Sin(t * 3f)); // slight press at the top of the bob
            Hand.localScale = new Vector3(press, press * dir, 1f);

            // Message below the cut-out (above it when the cut-out sits in the lower part of the screen).
            if (MessagePill.gameObject.activeSelf)
            {
                float pillH = 104f, margin = 150f;
                bool below = hy0 - margin - pillH > full.yMin + 40f;
                float py = below ? hy0 - margin - pillH : hy1 + margin;
                Place(MessagePill, full.xMin + 60f, py, full.xMax - 60f, py + pillH);
            }
        }

        private static void Bar(RectTransform frame, int i, Vector2 pos, Vector2 size) { var rt = (RectTransform)frame.GetChild(i); rt.anchoredPosition = pos; rt.sizeDelta = size; }

        private Vector2 ToLocal(Vector2 screen)
        {
            var canvas = GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, cam, out var local);
            return local;
        }

        private static void Place(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            float w = Mathf.Max(0f, x1 - x0), h = Mathf.Max(0f, y1 - y0);
            rt.anchoredPosition = new Vector2(x0 + w * 0.5f, y0 + h * 0.5f);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
