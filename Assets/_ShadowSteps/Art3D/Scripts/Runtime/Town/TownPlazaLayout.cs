using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// One deliberately composed town environment: every decorative object is an authored placement anchored to the
    /// edges of the plaza that surrounds the playable grid, so the composition survives a different board size without
    /// random placement. The grid itself, nodes, cover props and actors are still built by the level renderer from the
    /// stage data; a layout only decorates outside the grid and chooses the look of the authored X / # cells.
    /// </summary>
    [CreateAssetMenu(fileName = "TownPlazaLayout", menuName = "Shadow Steps/Town Plaza Layout")]
    public sealed class TownPlazaLayout : ScriptableObject
    {
        public enum FitMode
        {
            /// <summary>Uniform scale by <see cref="kitScale"/> (kit metres to tiles): faithful kit proportions.</summary>
            KitScale,
            /// <summary>Uniform scale so the measured height equals size.y tiles.</summary>
            Height,
            /// <summary>Uniform scale so the footprint is at most size.x and the height at most size.y tiles.</summary>
            Footprint,
            /// <summary>Per-axis fit to size (tiles); a zero component stays proportional.</summary>
            Box
        }

        [System.Serializable]
        public sealed class Placement
        {
            [Tooltip("Scene object name; also the key in the mapping report.")]
            public string name = "Prop";
            [Tooltip("Visual element of the approved mock this placement stands for (documented mapping).")]
            public string mockElement;
            public GameObject prefab;
            [Tooltip("Plaza-edge anchor: x = -1 west edge … +1 east edge, y = -1 south edge … +1 north edge (0,0 = plaza centre).")]
            public Vector2 anchor;
            [Tooltip("Offset from the anchor in tiles (world X, world Z). Positive x = east, positive y = north.")]
            public Vector2 offset;
            [Tooltip("Height of the bounds bottom above the ground, in tiles (wall lights, posters, roof-only pieces).")]
            public float elevation;
            [Tooltip("Rotation about Y in degrees (0 = the prefab's authored facing).")]
            public float yaw;
            public FitMode fit = FitMode.KitScale;
            [Tooltip("Size in tiles for Height / Footprint / Box fits (see FitMode).")]
            public Vector3 size = new Vector3(1f, 1f, 1f);
            [Tooltip("Multiplied into the instance's material colours (white = untouched). Textured kit materials keep their atlas, only the multiply changes.")]
            public Color tint = Color.white;
            [Tooltip("Repeat the placement along the world axis given by 'repeatStep' (tiles) this many extra times (fence runs, planter rows).")]
            public int repeat;
            public Vector2 repeatStep;
        }

        [Header("Identity")]
        public string displayName = "Town Plaza";
        [TextArea] public string description;
        [Tooltip("Stage shown in the generated scene (gameplay data is read from levels_v4.json; never stored here).")]
        public string stageCode = "LEVEL-4";

        [Header("Plaza (open pavement around the grid, in tiles)")]
        public float marginWest = 1.0f, marginEast = 1.0f, marginNorth = 1.0f, marginSouth = 0.8f;
        public Color floorA = new Color(0.86f, 0.80f, 0.70f), floorB = new Color(0.81f, 0.75f, 0.65f);
        public Color pavementA = new Color(0.82f, 0.77f, 0.68f), pavementB = new Color(0.78f, 0.73f, 0.64f);
        public Color groutColor = new Color(0.66f, 0.58f, 0.48f);
        public Color kerbColor = new Color(0.76f, 0.72f, 0.66f);
        [Tooltip("Raised-plaza look: a lower, wider step ring outside the kerb (tiles; 0 = none).")]
        public float stepWidth = 0.45f;
        public Color stepColor = new Color(0.72f, 0.68f, 0.62f);
        [Tooltip("Cover the visible ground outside the plaza with darker flagstone pavers (one per tile) instead of a flat slab.")]
        public bool outerPavement = true;
        public Color outerPavementA = new Color(0.70f, 0.66f, 0.60f), outerPavementB = new Color(0.68f, 0.64f, 0.58f);
        [Range(0.25f, 2f)] public float outerPaverSize = 0.5f;
        [Tooltip("Ground under everything outside the plaza, sized to the visible area (prefab fitted flat; empty = tinted slab).")]
        public GameObject groundPrefab;
        public Color groundTint = new Color(0.80f, 0.76f, 0.70f);
        [Tooltip("Kit metres per tile for KitScale fits (Synty Town is 1 m per unit; 0.5 keeps characters at 0.95 tiles).")]
        [Range(0.2f, 1.5f)] public float kitScale = 0.5f;

        [Header("Gameplay props (authored X / # cells only; the rule uses the whole cell regardless)")]
        public GameObject[] coverProps;
        [Range(0.3f, 1.2f)] public float coverHeight = 0.62f;
        public GameObject wallProp;
        [Range(0.3f, 1.2f)] public float wallHeight = 0.6f;
        public Color connectionColor = new Color(0.98f, 0.97f, 0.94f, 0.95f);
        [Range(0.02f, 0.12f)] public float connectionWidth = 0.05f;
        [Tooltip("Visual scale of the exit pad model (gameplay unchanged).")]
        [Range(0.5f, 3f)] public float exitScale = 1.6f;

        [Header("Camera (fixed; portrait)")]
        public bool orthographic = true;
        [Range(35f, 75f)] public float pitch = 50f;
        [Tooltip("Yaw around the board; 45° gives the diamond lattice of the reference (west edge at the bottom-left).")]
        public float yaw = 45f;
        [Range(15f, 60f)] public float fieldOfView = 26f;
        [Tooltip("Extra framed margin beyond the grid, in tiles; the plaza and façades fill the rest of the frame.")]
        public float paddingTiles = 0.9f;
        [Tooltip("Framing band: the board is fitted and centred between these fractions (top can exceed the HUD so the board sits low and the town fills the top).")]
        [Range(0f, 0.5f)] public float topReserved = 0.14f;
        [Range(0f, 0.5f)] public float bottomReserved = 0.22f;
        [Tooltip("HUD safe areas drawn by the render guides and kept clear of nodes.")]
        [Range(0f, 0.4f)] public float hudTop = 0.14f;
        [Range(0f, 0.4f)] public float hudBottom = 0.22f;

        [Header("Lighting (warm afternoon)")]
        public Color sunColor = new Color(1f, 0.93f, 0.80f);
        [Range(0.3f, 3f)] public float sunIntensity = 1.7f;
        public Vector2 sunAngles = new Vector2(48f, -58f);
        [Range(0f, 1f)] public float shadowStrength = 0.72f;
        [Tooltip("Shadowless fill light along the camera direction so the plaza-facing façades read even when the sun is behind them.")]
        [Range(0f, 1.5f)] public float fillIntensity = 0.45f;
        public Color fillColor = new Color(1f, 0.95f, 0.88f);
        public Color background = new Color(0.72f, 0.80f, 0.84f);
        public Color ambientSky = new Color(0.78f, 0.84f, 0.92f), ambientEquator = new Color(0.86f, 0.80f, 0.70f), ambientGround = new Color(0.52f, 0.46f, 0.40f);
        public float postExposure = 0.2f;
        [Range(-100f, 100f)] public float saturation = 18f;
        [Range(-100f, 100f)] public float contrast = 12f;
        [Range(0f, 1f)] public float vignette = 0.2f;
        [Range(0f, 1f)] public float depthOfField = 0.35f;

        [Header("Authored placements (built in order, all outside the grid)")]
        public Placement[] placements;

        public SS3DPropOverrides RendererOverrides() => new SS3DPropOverrides
        {
            coverProps = coverProps, wallProp = wallProp, wallHeight = wallHeight, coverHeight = coverHeight,
            tintFloor = true, floorA = floorA, floorB = floorB, groutColor = groutColor, overrideGrout = true,
            overrideConnection = true, connectionColor = connectionColor, connectionWidth = connectionWidth, exitScale = exitScale, flatStone = true
        };
    }
}
