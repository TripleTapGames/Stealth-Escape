using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>
    /// Final art for the menu and result screens (D-055): the user's pieces from Scripts/UI/{LevelComplete,LevelFailed,LevelSelect,SettingsMenu},
    /// trimmed and imported as single sprites by the UI skin importer, referenced from one asset in Resources so both the Home and the Game
    /// scene find it. When the asset is missing or incomplete every screen falls back to its code-built placeholder look.
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Steps/UI Skin", fileName = "UiSkin")]
    public sealed class UiSkin : ScriptableObject
    {
        public const string ResourceName = "UiSkin";
        public const string DefaultAssetPath = "Assets/_ShadowSteps/Resources/UiSkin.asset";

        [Header("Cards and rows")] public Sprite card; public Sprite cardTall; public Sprite statBox;
        [Header("Buttons")] public Sprite buttonGreen; public Sprite buttonWhite; public Sprite backButton; public Sprite backArrow; public Sprite chevron; public Sprite toggleOn; public Sprite toggleOff;
        [Header("Level select")] public Sprite headerPill; public Sprite smallPill; public Sprite levelBoxOpen; public Sprite levelBoxLocked; public Sprite lockIcon;
        [Header("Icons and art")] public Sprite starFull; public Sprite starBlank; public Sprite cop;
        [Header("Level complete overrides (optional; the results screen falls back to the shared pieces above)")]
        public Sprite resultsCard; public Sprite resultsStatBox; public Sprite resultsButtonGreen; public Sprite resultsButtonWhite; public Sprite resultsStarFull; public Sprite resultsStarBlank;
        [Header("Colours")]
        public Color navy = new Color(0.20f, 0.29f, 0.42f);
        public Color navyDim = new Color(0.20f, 0.29f, 0.42f, 0.72f);
        public Color muted = new Color(0.46f, 0.50f, 0.57f);
        public Color starGrey = new Color(0.58f, 0.61f, 0.68f);
        public Color starDark = new Color(0.33f, 0.38f, 0.47f);
        public Color gold = new Color(1.00f, 0.78f, 0.22f);
        public Color backdrop = new Color(0f, 0f, 0f, 0.28f);
        public Color lockedText = new Color(0.36f, 0.41f, 0.50f);

        public bool IsComplete => card && cardTall && statBox && buttonGreen && buttonWhite && backButton && backArrow && chevron && toggleOn && toggleOff
            && headerPill && smallPill && levelBoxOpen && levelBoxLocked && lockIcon && starFull && starBlank && cop;

        private static UiSkin _current;
        /// <summary>The skin in Resources (cached once found); assign to override, e.g. in tests.</summary>
        public static UiSkin Current
        {
            get { if (_current == null) _current = Resources.Load<UiSkin>(ResourceName); return _current; }
            set => _current = value;
        }
        public static bool Available => Current != null && Current.IsComplete;
    }

    /// <summary>Builders for the skinned screens: sliced sprites, centred content-sized cards, pill buttons, rows, icons and stars.</summary>
    public static class UiSkinKit
    {
        public const float ReferenceWidth = 1080f, ReferenceHeight = 1920f;

        public static RectTransform Backdrop(UIScreen screen, UiSkin skin)
        {
            var backdrop = UiBuilder.CreatePanel("Backdrop", screen.transform, skin.backdrop);
            UiBuilder.Anchor(backdrop, 0f, 0f, 1f, 1f);
            return backdrop;
        }

        public static RectTransform SafeArea(Transform parent)
        {
            var safe = UiBuilder.CreateRect("SafeArea", parent);
            UiBuilder.Anchor(safe, 0f, 0f, 1f, 1f);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return safe;
        }

        /// <summary>An Image child; sliced when the sprite carries a 9-slice border (pills, boxes, cards), otherwise simple with its aspect kept (icons, art).</summary>
        public static Image Image(string name, Transform parent, Sprite sprite, Color tint, bool raycast = false)
        {
            var rt = UiBuilder.CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = tint; img.raycastTarget = raycast;
            bool sliced = sprite != null && (sprite.border.x > 0f || sprite.border.y > 0f);
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.preserveAspect = !sliced;
            return img;
        }

        /// <summary>Keeps round caps round on a pill drawn at <paramref name="height"/> reference pixels (same rule as the HUD).</summary>
        public static void FitCaps(Image img, float height)
        {
            if (img.sprite == null || img.sprite.border.x <= 0f) return;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.05f, (img.sprite.border.x * 2f) / Mathf.Max(1f, height));
        }

        /// <summary>Backdrop + safe area + a centred card of the given width whose height follows its content (vertical stack).</summary>
        public static RectTransform Card(UIScreen screen, UiSkin skin, Sprite sprite, float widthFraction, float padX, float padTop, float padBottom, float spacing)
        {
            Backdrop(screen, skin);
            var safe = SafeArea(screen.transform);
            var img = Image("Card", safe, sprite, Color.white, true);
            var card = img.rectTransform;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f); card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(ReferenceWidth * widthFraction, 100f); card.anchoredPosition = Vector2.zero;
            var stack = card.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset((int)padX, (int)padX, (int)padTop, (int)padBottom); stack.spacing = spacing;
            stack.childAlignment = TextAnchor.UpperCenter; stack.childControlWidth = true; stack.childControlHeight = true; stack.childForceExpandWidth = true; stack.childForceExpandHeight = false; // control height so every child's LayoutElement height applies
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card;
        }

        public static UiText Text(Transform parent, string name, string text, float size, Color color, bool bold, TextAnchor align, float height)
        {
            var t = UiBuilder.CreateText(name, parent, text, size, align, bold);
            t.Color = color;
            Fixed(t.Rect, height);
            return t;
        }

        public static void Fixed(RectTransform rt, float height)
        {
            var le = rt.gameObject.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height; le.flexibleWidth = 1f;
        }

        /// <summary>A pill button (sliced sprite, round caps) with a centred bold label.</summary>
        public static UiButton Button(Transform parent, string name, Sprite sprite, string label, Color textColor, float height, float textSize)
        {
            var img = Image(name, parent, sprite, Color.white, true); FitCaps(img, height);
            var rt = img.rectTransform; Fixed(rt, height);
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = img;
            var colors = button.colors; colors.disabledColor = new Color(1f, 1f, 1f, 0.45f); colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f); colors.pressedColor = new Color(0.82f, 0.82f, 0.82f); button.colors = colors;
            var text = UiBuilder.CreateText("Label", rt, label, textSize, TextAnchor.MiddleCenter, true); text.Color = textColor;
            UiBuilder.Anchor(text.Rect, 0f, 0f, 1f, 1f, 8f);
            var ui = rt.gameObject.AddComponent<UiButton>(); ui.Button = button; ui.Label = text;
            return ui;
        }

        /// <summary>A grey row box (sliced) of a fixed height for settings rows and stat strips.</summary>
        public static RectTransform Row(Transform parent, string name, Sprite sprite, float height, bool raycast = false)
        {
            var img = Image(name, parent, sprite, Color.white, raycast); FitCaps(img, height);
            Fixed(img.rectTransform, height);
            return img.rectTransform;
        }

        /// <summary>A horizontal strip (layout group) of a fixed height, centred children.</summary>
        public static RectTransform Strip(Transform parent, string name, float height, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = UiBuilder.CreateRect(name, parent); Fixed(rt, height);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = align; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false; // control sizes so LayoutElement icon sizes apply
            return rt;
        }

        /// <summary>Scales a box's 9-slice so its drawn corner radius is <paramref name="cornerFraction"/> of <paramref name="height"/> (level boxes drawn far smaller than their art).</summary>
        public static void FitCorners(Image img, float height, float cornerFraction)
        {
            if (img.sprite == null || img.sprite.border.y <= 0f) return;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.05f, img.sprite.border.y / Mathf.Max(1f, height * cornerFraction));
        }

        /// <summary>A square icon of a fixed size inside a layout group.</summary>
        public static Image Icon(Transform parent, string name, Sprite sprite, float size, Color tint)
        {
            var img = Image(name, parent, sprite, tint);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minWidth = size; le.minHeight = size; le.preferredWidth = size; le.preferredHeight = size;
            return img;
        }

        /// <summary>An icon anchored inside a parent by fractions (no layout group).</summary>
        public static Image IconAt(Transform parent, string name, Sprite sprite, float xMin, float yMin, float xMax, float yMax, Color tint)
        {
            var img = Image(name, parent, sprite, tint);
            UiBuilder.Anchor(img.rectTransform, xMin, yMin, xMax, yMax);
            return img;
        }

        /// <summary>A label anchored inside a parent by fractions (no layout group).</summary>
        public static UiText TextAt(Transform parent, string name, string text, float size, Color color, bool bold, TextAnchor align, float xMin, float yMin, float xMax, float yMax)
        {
            var t = UiBuilder.CreateText(name, parent, text, size, align, bold); t.Color = color;
            UiBuilder.Anchor(t.Rect, xMin, yMin, xMax, yMax);
            return t;
        }

        /// <summary>Art drawn at its own aspect, centred, of a fixed height.</summary>
        public static Image Art(Transform parent, string name, Sprite sprite, float height)
        {
            var holder = UiBuilder.CreateRect(name, parent); Fixed(holder, height);
            var img = Image("Image", holder, sprite, Color.white);
            UiBuilder.Anchor(img.rectTransform, 0f, 0f, 1f, 1f);
            return img;
        }
    }
}
