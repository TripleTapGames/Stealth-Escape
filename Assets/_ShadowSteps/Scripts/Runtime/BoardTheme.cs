using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Every colour, size and timing the board presentation uses, in one asset a designer can edit without code.
    /// Sprite fields are optional overrides; when empty the procedural <see cref="SpriteFactory"/> shapes are used.
    /// Create via Assets ▸ Create ▸ Shadow Steps ▸ Board Theme, or let <see cref="CreateRuntimeDefault"/> build one.
    /// Colours follow the "board-first" direction: cool floor, teal interactables, amber danger, white indicators.
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Steps/Board Theme", fileName = "BoardTheme")]
    public sealed class BoardTheme : ScriptableObject
    {
        [Header("Node and edge geometry (fractions of nodeSpacing)")]
        public float NodeRadius = 0.34f;
        public float EdgeWidth = 0.10f;
        public float WallSize = 0.92f;
        public float CoverSize = 0.78f;
        public float CharacterSize = 0.62f;
        public float FacingArrowSize = 0.30f;
        public float PropSize = 0.50f;
        public float RouteDotSize = 0.10f;
        public float GhostAlpha = 0.35f;

        [Header("Colours")]
        public Color Background = new Color(0.07f, 0.09f, 0.13f);
        public Color Node = new Color(0.22f, 0.27f, 0.36f);
        public Color NodeCover = new Color(0.30f, 0.34f, 0.30f);
        public Color Edge = new Color(0.33f, 0.39f, 0.50f);
        public Color EdgeLocked = new Color(0.95f, 0.72f, 0.25f);
        public Color EdgeOneWay = new Color(0.55f, 0.70f, 0.85f);
        public Color Wall = new Color(0.12f, 0.14f, 0.19f);
        public Color WallOutline = new Color(0.45f, 0.50f, 0.60f);
        public Color Exit = new Color(0.25f, 0.85f, 0.55f);
        public Color Key = new Color(1.00f, 0.84f, 0.30f);
        public Color DoorLocked = new Color(0.95f, 0.55f, 0.20f);
        public Color DoorOpen = new Color(0.45f, 0.75f, 0.60f);
        public Color Player = new Color(0.30f, 0.85f, 0.95f);
        public Color GuardFixed = new Color(0.90f, 0.35f, 0.35f);
        public Color GuardRotate = new Color(0.95f, 0.50f, 0.30f);
        public Color GuardPatrol = new Color(0.85f, 0.30f, 0.55f);
        public Color GuardPatrolRotate = new Color(0.80f, 0.40f, 0.80f);
        public Color GuardLoop = new Color(0.95f, 0.40f, 0.20f);
        public Color FacingArrow = Color.white;
        public Color Cone = new Color(1.00f, 0.45f, 0.20f, 0.42f);
        public Color ConePreview = new Color(1.00f, 0.80f, 0.30f, 0.22f);
        public Color RouteDot = new Color(1f, 1f, 1f, 0.35f);
        public Color Smoke = new Color(0.85f, 0.90f, 0.95f, 0.55f);
        public Color EmpPulse = new Color(0.40f, 0.70f, 1.00f, 0.60f);
        public Color Captured = new Color(1.00f, 0.20f, 0.20f);
        public Color Won = new Color(0.40f, 1.00f, 0.60f);

        [Header("Timing (seconds)")]
        public float MoveDuration = 0.16f;
        public float RotateDuration = 0.12f;
        public float CaptureFlashDuration = 0.35f;
        public float WinPulseDuration = 0.45f;
        public float GadgetPulseDuration = 0.25f;

        [Header("Optional sprite overrides (leave empty for generated shapes)")]
        public Sprite NodeSprite;
        public Sprite WallSprite;
        public Sprite CoverSprite;
        public Sprite ExitSprite;
        public Sprite KeySprite;
        public Sprite DoorSprite;
        public Sprite PlayerSprite;
        public Sprite GuardSprite;
        public Sprite ArrowSprite;
        public Sprite ConeCellSprite;

        public Sprite NodeOrDefault => NodeSprite != null ? NodeSprite : SpriteFactory.Circle();
        public Sprite WallOrDefault => WallSprite != null ? WallSprite : SpriteFactory.RoundedSquare();
        public Sprite CoverOrDefault => CoverSprite != null ? CoverSprite : SpriteFactory.Diamond();
        public Sprite ExitOrDefault => ExitSprite != null ? ExitSprite : SpriteFactory.Ring();
        public Sprite KeyOrDefault => KeySprite != null ? KeySprite : SpriteFactory.Key();
        public Sprite DoorOrDefault => DoorSprite != null ? DoorSprite : SpriteFactory.Padlock();
        public Sprite PlayerOrDefault => PlayerSprite != null ? PlayerSprite : SpriteFactory.Circle();
        public Sprite GuardOrDefault => GuardSprite != null ? GuardSprite : SpriteFactory.RoundedSquare();
        public Sprite ArrowOrDefault => ArrowSprite != null ? ArrowSprite : SpriteFactory.Arrow();
        public Sprite ConeCellOrDefault => ConeCellSprite != null ? ConeCellSprite : SpriteFactory.RoundedSquare();

        public Color GuardColor(Core.GuardMode mode)
        {
            switch (mode)
            {
                case Core.GuardMode.Fixed: return GuardFixed;
                case Core.GuardMode.Rotate: return GuardRotate;
                case Core.GuardMode.Patrol: return GuardPatrol;
                case Core.GuardMode.PatrolRotate: return GuardPatrolRotate;
                default: return GuardLoop;
            }
        }

        /// <summary>Fallback when no asset is assigned (e.g. scene opened directly, tests). Values are the field defaults.</summary>
        public static BoardTheme CreateRuntimeDefault()
        {
            var t = CreateInstance<BoardTheme>();
            t.name = "BoardTheme (runtime default)";
            return t;
        }
    }
}
