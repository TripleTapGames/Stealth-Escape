using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Reusable 3D prefab palette (handover: modular prefabs so models can be replaced without touching level data).
    /// Filled by Tools ▸ Shadow Steps ▸ 3D ▸ Create / Update Art Palette from the imported .glb models and the
    /// characters_5_02 prefabs; any field can be overridden by hand. All models: Y up, +Z front, one tile = one unit.
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Steps/3D Art Palette", fileName = "SS3DArtPalette")]
    public sealed class SS3DArtPalette : ScriptableObject
    {
        [Header("Grid geometry (one tile = one Unity unit)")]
        public GameObject floorA, floorB, wallBlock, wallStraight;
        [Tooltip("Blocked cover 'X' variants, cycled per cell. All are 1×1 and visually solid.")]
        public GameObject[] coverObstacles;
        [Tooltip("Floor top height in world units (floor tile model top).")]
        public float floorTop = 0.112f;
        [Tooltip("Vertical scale applied to wall blocks so they cannot hide the node behind them at the camera pitch (1 = model height).")]
        [Range(0.3f, 1.2f)] public float wallHeightScale = 0.7f;
        [Tooltip("Vertical scale applied to blocked-cover obstacles.")]
        [Range(0.3f, 1.2f)] public float coverHeightScale = 0.85f;

        [Header("Interactables")]
        public GameObject node, exitPad, collectibleKey, lockedDoor, openedDoor;

        [Header("Characters (characters_5_02 prefabs; proxies are the fallback)")]
        [Tooltip("When on, palette rebuilds never touch spyCharacter/guardCharacter. Turned on automatically once you assign them.")]
        public bool lockCharacterSelection = true;
        public GameObject spyCharacter, guardCharacter;
        public GameObject spyProxy, guardProxy;
        [Tooltip("Characters are scaled so their renderer bounds reach this height (world units).")]
        public float characterTargetHeight = 0.95f;
        [Tooltip("Extra yaw (degrees) if the character model's forward axis is not +Z.")]
        public float characterYawOffset = 0f;
        public string idleStateName = "Idle", walkStateName = "Walk";

        [Header("Wall-mounted security cameras")]
        public GameObject cameraModel, cameraMount, cameraHead;
        [Tooltip("Cameras project their coverage as light: a spot light in the head plus soft light pools and rays on the detected cells only.")]
        public bool cameraProjectsLight = true;
        public Color cameraLightColor = new Color(1.00f, 0.78f, 0.35f, 1f);
        public float cameraLightIntensity = 2.2f;
        public float cameraLightRange = 4.0f;
        [Range(20f, 120f)] public float cameraLightSpotAngle = 75f;
        public Color cameraPoolColor = new Color(1.00f, 0.72f, 0.20f, 0.55f);
        public Color cameraRayColor = new Color(1.00f, 0.80f, 0.35f, 0.35f);
        public bool cameraShowRays = true;

        [Header("Decorative pieces (placed only outside the logical board)")]
        public GameObject archFrame, palaceDoor, stairs, pillar, water, bench, lantern, banner, flowerbed;

        [Header("Overlay colours (match the 2D board theme)")]
        public Color guardVisionFill = new Color(0.91f, 0.23f, 0.24f, 0.22f);
        public Color guardVisionBorder = new Color(0.90f, 0.23f, 0.25f, 1f);
        public Color cameraVisionFill = new Color(0.95f, 0.70f, 0.17f, 0.22f);
        public Color cameraVisionBorder = new Color(0.98f, 0.67f, 0.13f, 1f);
        public Color previewFill = new Color(1f, 0.85f, 0.4f, 0.10f);
        public Color previewBorder = new Color(1f, 0.85f, 0.4f, 0.7f);
        public Color reachable = new Color(0.38f, 0.85f, 0.85f, 1f);
        public Color smokeTarget = new Color(0.40f, 0.95f, 0.90f, 0.45f);
        public Color smokeCloud = new Color(0.88f, 0.92f, 0.95f, 0.75f);
        public Color nextMarker = new Color(0.20f, 0.85f, 1.00f, 1f);
        public Color rotationMarker = new Color(1.00f, 0.95f, 0.70f, 1f);
        public Color empDisabled = new Color(0.45f, 0.88f, 0.92f, 1f);
        public Color cameraHighlight = new Color(0.40f, 1.00f, 0.90f, 1f);
        public Color connection = new Color(0.16f, 0.20f, 0.28f, 0.8f);
        public Color previewSafe = new Color(0.40f, 1.00f, 0.60f, 0.8f);
        public Color previewDanger = new Color(1.00f, 0.20f, 0.20f, 0.9f);
        public Color previewAlarm = new Color(1.00f, 0.70f, 0.15f, 0.9f);

        [Header("Scene lighting (applied by the preview/bridge; sun + ambient only, no post effects)")]
        public Color backgroundColor = new Color(0.62f, 0.80f, 0.82f);
        public Color sunColor = new Color(1.00f, 0.93f, 0.80f);
        public float sunIntensity = 1.55f;
        public Vector2 sunAngles = new Vector2(52f, -34f);
        [Range(0f, 1f)] public float sunShadowStrength = 0.55f;
        public Color ambientSky = new Color(0.86f, 0.90f, 0.96f);
        public Color ambientEquator = new Color(0.92f, 0.86f, 0.78f);
        public Color ambientGround = new Color(0.70f, 0.62f, 0.52f);

        [Header("Material tints by role (applied to material INSTANCES, never to the imported assets)")]
        public bool tintMaterials = true;
        public Color floorA_Cream = new Color(0.949f, 0.871f, 0.741f);
        public Color floorB_Cream = new Color(0.914f, 0.824f, 0.690f);
        public Color wallIvory = new Color(0.914f, 0.835f, 0.682f);
        public Color warmStone = new Color(0.847f, 0.745f, 0.588f);
        public Color waterTurquoise = new Color(0.231f, 0.686f, 0.780f);
        [Range(0f, 1f)] public float waterSmoothness = 0.92f;
        public Color waterEmission = new Color(0.06f, 0.22f, 0.26f);
        public Color woodBrown = new Color(0.52f, 0.33f, 0.18f);
        public Color foliageGreen = new Color(0.40f, 0.66f, 0.30f);
        public Color exitGreen = new Color(0.231f, 0.769f, 0.510f);
        public Color coverCrate = new Color(0.72f, 0.52f, 0.30f);
        [Range(0f, 1f)] public float stoneSmoothness = 0.25f;
        [Header("Courtyard trim (milestone 3)")]
        public Color groutColor = new Color(0.72f, 0.60f, 0.46f);
        public Color trimIvory = new Color(0.96f, 0.91f, 0.80f);
        public Color plinthStone = new Color(0.80f, 0.69f, 0.54f);
        [Range(0.8f, 1f)] public float tileInset = 0.94f;

        /// <summary>Path of the shared palette asset (editor tooling loads it by path after scene switches, never by cached reference).</summary>
        public const string DefaultAssetPath = "Assets/_ShadowSteps/Art3D/SS3DArtPalette.asset";

        /// <summary>Lists everything that would make generation fail or fall back; returns true when the palette can build a stage.</summary>
        public bool Validate(System.Collections.Generic.List<string> problems)
        {
            bool ok = true;
            void Need(Object o, string slot) { if (o == null) { problems?.Add("palette slot '" + slot + "' is empty (run Shadow Steps ▸ 3D ▸ Create or update Art Palette after the .glb models imported)"); ok = false; } }
            Need(floorA, "floorA"); Need(wallBlock, "wallBlock"); Need(node, "node"); Need(exitPad, "exitPad");
            if (cameraModel == null && (cameraMount == null || cameraHead == null)) { problems?.Add("palette has no camera model (cameraModel or cameraMount + cameraHead)"); ok = false; }
            if (coverObstacles == null || coverObstacles.Length == 0 || System.Array.Exists(coverObstacles, o => o == null)) { problems?.Add("palette coverObstacles list is empty or has an empty entry; blocked 'X' cells would be invisible"); ok = false; }
            if (Spy == null) problems?.Add("no spy character or proxy assigned; the player will be invisible");
            if (Guard == null) problems?.Add("no guard character or proxy assigned; guards will be invisible");
            else if (guardCharacter == null) problems?.Add("guardCharacter is empty: the proxy guard is used (assign a characters_5_02 prefab on the palette)");
            return ok;
        }

        public GameObject Spy => spyCharacter != null ? spyCharacter : spyProxy;
        public GameObject Guard => guardCharacter != null ? guardCharacter : guardProxy;
        public bool HasCoreModels => floorA != null && (cameraModel != null || (cameraMount != null && cameraHead != null));
    }
}
