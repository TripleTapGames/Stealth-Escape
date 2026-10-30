using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// One town environment look: ground and pavement, buildings for the far band, perimeter decoration, foliage,
    /// street furniture, vehicles, lighting and a seed for deterministic variation. Purely visual; the level renderer
    /// still draws every gameplay object from the stage data, and only authored X / # cells may block anything.
    /// </summary>
    [CreateAssetMenu(fileName = "TownEnvironmentProfile", menuName = "Shadow Steps/Town Environment Profile")]
    public sealed class TownEnvironmentProfile : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Town";
        [TextArea] public string description;
        public int seed = 1;

        [Header("Ground and pavement")]
        [Tooltip("Large ground prefab fitted to the visible area (grass, concrete); empty = tinted procedural slab.")]
        public GameObject groundPrefab;
        public Color groundTint = Color.white;
        [Tooltip("Plaza floor tints for the playable area tiles (procedural stone) and its pavement margin.")]
        public Color floorA = new Color(0.84f, 0.83f, 0.80f), floorB = new Color(0.80f, 0.79f, 0.76f);
        public Color kerbColor = new Color(0.78f, 0.78f, 0.76f);
        [Tooltip("Open pavement margin between the playable grid and the surroundings, in tiles.")]
        [Range(0.5f, 3f)] public float marginTiles = 1.2f;

        [Header("Gameplay props (authored cells only)")]
        public GameObject[] coverProps;
        public GameObject wallProp;
        [Range(0.3f, 1.2f)] public float wallHeight = 0.6f;

        [Header("North band (far side): road, sidewalks, buildings, vehicles")]
        public bool roadNorth = true;
        public GameObject roadTile, sidewalkTile;
        public GameObject[] buildings;
        [Tooltip("Height the tallest building is fitted to, in tiles.")]
        [Range(1.5f, 6f)] public float buildingHeight = 3.2f;
        public GameObject[] roadVehicles;

        [Header("South band (near side): low decoration only")]
        public bool roadSouth = false;
        public GameObject[] southVehicles;
        public GameObject[] southProps;
        [Range(0, 8)] public int southPropCount = 4;

        [Header("Perimeter: fences and hedges along the sides of the near band")]
        public GameObject fenceSegment, fencePost, fenceGate;
        public GameObject hedge;

        [Header("Foliage and furniture")]
        public GameObject[] trees;
        [Range(0, 8)] public int treeCount = 4;
        public GameObject[] bushes;
        [Range(0, 10)] public int bushCount = 4;
        public GameObject streetLamp, bench, bin;
        public GameObject[] accents;
        [Range(0, 10)] public int accentCount = 4;

        [Header("Lighting")]
        public Color sunColor = new Color(1f, 0.95f, 0.85f);
        [Range(0.3f, 3f)] public float sunIntensity = 1.5f;
        public Vector2 sunAngles = new Vector2(52f, -34f);
        public Color background = new Color(0.62f, 0.8f, 0.86f);
        public Color ambientSky = new Color(0.86f, 0.9f, 0.96f), ambientEquator = new Color(0.9f, 0.88f, 0.82f), ambientGround = new Color(0.6f, 0.6f, 0.56f);

        public SS3DPropOverrides RendererOverrides() => new SS3DPropOverrides { coverProps = coverProps, wallProp = wallProp, wallHeight = wallHeight, tintFloor = true, floorA = floorA, floorB = floorB };

        /// <summary>Profile sun and ambient on top of the palette lighting (same sun object, same camera settings).</summary>
        public void ApplyLighting(SS3DArtPalette palette, Camera camera)
        {
            SS3DArtTuning.ApplyLighting(palette, camera);
            var sun = GameObject.Find("Warm_Directional_Sun");
            var light = sun != null ? sun.GetComponent<Light>() : null;
            if (light != null) { light.color = sunColor; light.intensity = sunIntensity; light.transform.rotation = Quaternion.Euler(sunAngles.x, sunAngles.y, 0f); }
            RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator; RenderSettings.ambientGroundColor = ambientGround;
            if (camera != null) camera.backgroundColor = background;
        }
    }
}
