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

        [Header("Interactables")]
        public GameObject node, exitPad, collectibleKey, lockedDoor, openedDoor;

        [Header("Characters (characters_5_02 prefabs; proxies are the fallback)")]
        public GameObject spyCharacter, guardCharacter;
        public GameObject spyProxy, guardProxy;
        [Tooltip("Characters are scaled so their renderer bounds reach this height (world units).")]
        public float characterTargetHeight = 0.95f;
        [Tooltip("Extra yaw (degrees) if the character model's forward axis is not +Z.")]
        public float characterYawOffset = 0f;
        public string idleStateName = "Idle", walkStateName = "Walk";

        [Header("Wall-mounted security cameras")]
        public GameObject cameraModel, cameraMount, cameraHead;

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

        public GameObject Spy => spyCharacter != null ? spyCharacter : spyProxy;
        public GameObject Guard => guardCharacter != null ? guardCharacter : guardProxy;
        public bool HasCoreModels => floorA != null && (cameraModel != null || (cameraMount != null && cameraHead != null));
    }
}
