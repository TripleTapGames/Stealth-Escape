using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Every colour, size and timing the board presentation uses, in one asset a designer can edit without code.
    /// Sprite fields are optional overrides; when empty the procedural <see cref="SpriteFactory"/> shapes are used.
    /// Placeholder visuals only (handover: art not finalised); all sprites are replaceable through this asset.
    /// Create via Assets ▸ Create ▸ Shadow Steps ▸ Board Theme, or let <see cref="CreateRuntimeDefault"/> build one.
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Steps/Board Theme", fileName = "BoardTheme")]
    public sealed class BoardTheme : ScriptableObject
    {
        [Header("Geometry (fractions of nodeSpacing)")]
        public float NodeRadius = 0.30f;
        public float ConnectionWidth = 0.07f;
        public float WallSize = 0.96f;
        public float CharacterSize = 0.58f;
        public float PlayerSize = 0.68f;
        public float CharacterOutline = 1.22f;
        public float FacingArrowSize = 0.26f;
        public float MarkerSize = 0.22f;
        public float PropSize = 0.46f;
        public float CameraBodySize = 0.34f;
        public float CloudSize = 0.90f;
        public float GhostAlpha = 0.35f;

        [Header("Board colours")]
        public Color Background = new Color(0.07f, 0.09f, 0.13f);
        public Color Node = new Color(0.22f, 0.27f, 0.36f);
        public Color NodeReachable = new Color(0.30f, 0.85f, 0.95f, 0.95f);
        public Color Connection = new Color(0.33f, 0.39f, 0.50f);
        public Color ConnectionReachable = new Color(0.45f, 0.85f, 0.95f);
        public Color Wall = new Color(0.12f, 0.14f, 0.19f);
        public Color WallOutline = new Color(0.45f, 0.50f, 0.60f);
        public Color Cover = new Color(0.62f, 0.46f, 0.28f);
        public Color CoverOutline = new Color(0.30f, 0.20f, 0.10f);
        public float CoverSize = 0.84f;
        public Color Exit = new Color(0.25f, 0.85f, 0.55f);
        public Color Key = new Color(1.00f, 0.84f, 0.30f);
        public Color DoorClosed = new Color(0.95f, 0.55f, 0.20f);
        public Color DoorOpen = new Color(0.45f, 0.75f, 0.60f);

        [Header("Characters and markers")]
        public Color Player = new Color(0.98f, 0.98f, 1.00f);
        public Color PlayerOutline = new Color(0.05f, 0.07f, 0.10f);
        public Color GuardOutline = new Color(0.05f, 0.07f, 0.10f, 0.9f);
        public Color GuardStatic = new Color(0.90f, 0.35f, 0.35f);
        public Color GuardRotating = new Color(0.95f, 0.50f, 0.30f);
        public Color GuardPatrol = new Color(0.85f, 0.30f, 0.55f);
        public Color GuardLoop = new Color(0.95f, 0.40f, 0.20f);
        public Color FacingArrow = Color.white;
        public Color NextMarker = new Color(0.20f, 0.85f, 1.00f);
        public Color RotationMarker = new Color(1.00f, 0.95f, 0.70f);
        public Color CameraBody = new Color(0.55f, 0.58f, 0.65f);
        public Color CameraLens = new Color(1.00f, 0.75f, 0.25f);
        public Color CameraDisabled = new Color(0.40f, 0.85f, 1.00f);
        public Color CameraHighlight = new Color(0.40f, 1.00f, 0.90f);

        [Header("Hazard overlays")]
        public Color GuardVisionFill = new Color(1.00f, 0.25f, 0.25f, 0.22f);
        public Color GuardVisionBorder = new Color(1.00f, 0.25f, 0.25f, 0.95f);
        public Color CameraVisionFill = new Color(1.00f, 0.70f, 0.15f, 0.20f);
        public Color CameraVisionBorder = new Color(1.00f, 0.70f, 0.15f, 0.95f);
        public Color PreviewFill = new Color(1.00f, 0.85f, 0.40f, 0.10f);
        public Color PreviewBorder = new Color(1.00f, 0.85f, 0.40f, 0.6f);
        public Color SmokeTarget = new Color(0.40f, 0.95f, 0.90f, 0.35f);
        public Color SmokeCloud = new Color(0.85f, 0.90f, 0.95f, 0.70f);
        public Color PreviewSafe = new Color(0.40f, 1.00f, 0.60f, 0.5f);
        public Color PreviewDanger = new Color(1.00f, 0.20f, 0.20f, 0.6f);
        public Color PreviewAlarm = new Color(1.00f, 0.70f, 0.15f, 0.6f);
        public Color Captured = new Color(1.00f, 0.20f, 0.20f);
        public Color Won = new Color(0.40f, 1.00f, 0.60f);
        public Color Lockdown = new Color(1.00f, 0.55f, 0.10f);

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
        public Sprite CameraSprite;
        public Sprite CloudSprite;
        public Sprite VisionCellSprite;

        public Sprite NodeOrDefault => NodeSprite != null ? NodeSprite : SpriteFactory.Circle();
        public Sprite WallOrDefault => WallSprite != null ? WallSprite : SpriteFactory.RoundedSquare();
        public Sprite CoverOrDefault => CoverSprite != null ? CoverSprite : SpriteFactory.Crate();
        public Sprite ExitOrDefault => ExitSprite != null ? ExitSprite : SpriteFactory.Ring();
        public Sprite KeyOrDefault => KeySprite != null ? KeySprite : SpriteFactory.Key();
        public Sprite DoorOrDefault => DoorSprite != null ? DoorSprite : SpriteFactory.Padlock();
        public Sprite PlayerOrDefault => PlayerSprite != null ? PlayerSprite : SpriteFactory.Circle();
        public Sprite GuardOrDefault => GuardSprite != null ? GuardSprite : SpriteFactory.RoundedSquare();
        public Sprite ArrowOrDefault => ArrowSprite != null ? ArrowSprite : SpriteFactory.Arrow();
        public Sprite CameraOrDefault => CameraSprite != null ? CameraSprite : SpriteFactory.RoundedSquare();
        public Sprite CloudOrDefault => CloudSprite != null ? CloudSprite : SpriteFactory.Circle();
        public Sprite VisionCellOrDefault => VisionCellSprite != null ? VisionCellSprite : SpriteFactory.RoundedSquare();

        public Color GuardColor(ShadowSteps.V3.GuardKind kind)
        {
            switch (kind)
            {
                case ShadowSteps.V3.GuardKind.Static: return GuardStatic;
                case ShadowSteps.V3.GuardKind.Rotating: return GuardRotating;
                case ShadowSteps.V3.GuardKind.PingPong: return GuardPatrol;
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
