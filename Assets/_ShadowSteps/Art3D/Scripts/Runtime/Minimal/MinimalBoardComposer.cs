using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Minimalist tabletop prototype: the playable grid (built by the level renderer from the stage data) sits on a
    /// floating bevelled slab over a pastel ground far below; no buildings, walls, foliage or props of any kind.
    /// Everything here is procedural Unity geometry (chamfered boxes) and colours; the renderer supplies tiles, nodes,
    /// connection lines, cover obstacles, exit and characters, all unchanged in behaviour.
    /// </summary>
    public sealed class MinimalBoardComposer : MonoBehaviour
    {
        public SS3DArtPalette palette;

        [Header("Board slab (tiles)")]
        [Tooltip("Open slab margin beyond the grid on every side.")]
        public float margin = 0.7f;
        public float slabThickness = 0.32f;
        [Range(0f, 0.3f)] public float slabChamfer = 0.14f;
        public Color slabColor = new Color(0.86f, 0.79f, 0.69f);
        public Color slabSideColor = new Color(0.70f, 0.61f, 0.52f);
        [Tooltip("Thinner darker base under the slab, inset so the slab reads as a floating plate.")]
        public float baseThickness = 0.16f;
        public float baseInset = 0.25f;

        [Header("Tiles")]
        public Color tileA = new Color(0.90f, 0.85f, 0.77f), tileB = new Color(0.86f, 0.81f, 0.73f);
        public Color seamColor = new Color(0.70f, 0.62f, 0.53f);
        [Range(0.85f, 1f)] public float tileInset = 0.965f;
        [Range(0f, 1f)] public float tileSmoothness = 0.45f;

        [Header("Gameplay readability")]
        public Color connectionColor = new Color(0.58f, 0.50f, 0.42f, 1f);
        [Range(0.01f, 0.08f)] public float connectionWidth = 0.026f;
        [Range(0.5f, 3f)] public float exitScale = 1.5f;
        [Range(0.3f, 1.2f)] public float coverHeight = 0.62f;
        [Tooltip("Cover obstacle prefabs (crate, stone block from Art3D/Models); empty = palette list.")]
        public GameObject[] coverProps;

        [Header("Background")]
        public Color background = new Color(0.66f, 0.76f, 0.78f);
        public Color groundColor = new Color(0.60f, 0.72f, 0.70f);
        [Tooltip("Distance of the pastel ground plane below the slab (catches the soft drop shadow).")]
        public float groundDrop = 1.4f;

        [Header("Camera")]
        public bool orthographic = true;
        [Range(35f, 75f)] public float pitch = 46f;
        public float yaw = 0f;
        public float paddingTiles = 1.0f;
        [Range(0f, 0.5f)] public float topReserved = 0.14f;
        [Range(0f, 0.5f)] public float bottomReserved = 0.22f;

        [Header("Lighting (soft, pastel)")]
        public Color sunColor = new Color(1f, 0.95f, 0.86f);
        [Range(0.3f, 3f)] public float sunIntensity = 1.35f;
        public Vector2 sunAngles = new Vector2(48f, 30f);
        [Range(0f, 1f)] public float shadowStrength = 0.6f;
        [Range(0f, 1.5f)] public float fillIntensity = 0.25f;
        public Color ambientSky = new Color(0.66f, 0.74f, 0.80f), ambientEquator = new Color(0.72f, 0.68f, 0.62f), ambientGround = new Color(0.46f, 0.42f, 0.38f);
        public float postExposure = -0.05f;
        [Range(-100f, 100f)] public float saturation = 12f;
        [Range(-100f, 100f)] public float contrast = 10f;
        [Range(0f, 1f)] public float vignette = 0.14f;

        private Transform _root;

        public SS3DPropOverrides RendererOverrides() => new SS3DPropOverrides
        {
            coverProps = coverProps, coverHeight = coverHeight, tintFloor = true, floorA = tileA, floorB = tileB,
            overrideGrout = true, groutColor = seamColor, overrideConnection = true, connectionColor = connectionColor, connectionWidth = connectionWidth,
            exitScale = exitScale, plainTiles = true, tileSmoothness = tileSmoothness, tileInset = tileInset
        };

        public void Clear() { if (_root != null) SS3DLevelRenderer.SafeDestroy(_root.gameObject); _root = null; }

        public void ApplyCamera(SS3DBoardCameraFitter f)
        {
            if (f == null) return;
            f.usePerspective = !orthographic; f.downwardAngle = pitch; f.yaw = yaw; f.paddingTiles = paddingTiles;
            f.topReservedFraction = topReserved; f.bottomReservedFraction = bottomReserved; f.minWidthTiles = 0f; f.minDepthTiles = 0f;
        }

        public void ApplyLighting(Camera camera)
        {
            var sun = SS3DArtTuning.ApplyLighting(palette, camera);
            if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; sun.shadowStrength = shadowStrength; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(sunAngles.x, sunAngles.y, 0f); }
            RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator; RenderSettings.ambientGroundColor = ambientGround;
            if (camera != null) camera.backgroundColor = background;
            const string fillName = "Minimal_Fill_Light";
            var fillGo = GameObject.Find(fillName); var fill = fillGo != null ? fillGo.GetComponent<Light>() : null;
            if (fill == null) { fill = new GameObject(fillName).AddComponent<Light>(); fill.type = LightType.Directional; }
            fill.shadows = LightShadows.None; fill.color = new Color(0.95f, 0.97f, 1f); fill.intensity = fillIntensity; fill.enabled = fillIntensity > 0f;
            fill.transform.rotation = camera != null ? Quaternion.Euler(camera.transform.eulerAngles.x - 10f, camera.transform.eulerAngles.y + 30f, 0f) : Quaternion.Euler(40f, 30f, 0f);
            var volume = SS3DArtTuning.ApplyPostProcessing(palette, camera);
            if (volume != null && volume.sharedProfile != null)
            {
                if (volume.sharedProfile.TryGet<ColorAdjustments>(out var adj)) { adj.postExposure.Override(postExposure); adj.saturation.Override(saturation); adj.contrast.Override(contrast); }
                if (volume.sharedProfile.TryGet<Vignette>(out var vig)) vig.intensity.Override(vignette);
                if (volume.sharedProfile.TryGet<DepthOfField>(out var dof)) dof.active = false;
                if (volume.sharedProfile.TryGet<Bloom>(out var bloom)) bloom.intensity.Override(0.15f);
            }
        }

        [ContextMenu("Build Around Level")]
        public void Build(SS3DLevelRenderer board)
        {
            Clear();
            if (board == null || !board.IsBuilt || palette == null) return;
            var stage = board.Stage;
            _root = new GameObject("Minimal_Board_Decoration_Only").transform;
            _root.SetParent(transform, false);
            float s = board.tileSpacing;
            float w = stage.Width * s + 2f * margin * s, d = stage.Height * s + 2f * margin * s;
            var centre = board.transform.position;
            // Slab: top face at y = 0 (the tiles stand on it), bevel on the top edge, side colour on the vertical faces via a second slightly smaller box.
            Box("Slab_Top", centre + Vector3.down * slabThickness, w, slabThickness, d, slabChamfer, slabColor, 0.35f);
            Box("Slab_Side", centre + Vector3.down * (slabThickness + 0.002f), w - 0.004f, slabThickness - slabChamfer - 0.01f, d - 0.004f, 0f, slabSideColor, 0.25f);
            Box("Slab_Base", centre + Vector3.down * (slabThickness + baseThickness), w - 2f * baseInset * s, baseThickness, d - 2f * baseInset * s, 0.03f, slabSideColor * 0.9f, 0.25f);
            // Pastel ground far below, larger than any portrait frame, catches the soft drop shadow.
            Box("Ground", centre + Vector3.down * (slabThickness + baseThickness + groundDrop + 0.05f), 80f, 0.05f, 80f, 0f, groundColor, 0.15f);
        }

        private GameObject Box(string name, Vector3 p, float sizeX, float height, float sizeZ, float chamfer, Color color, float smoothness)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = p;
            go.AddComponent<MeshFilter>().sharedMesh = SS3DMeshes.ChamferedBox(Mathf.Max(0.02f, sizeX), Mathf.Max(0.02f, height), Mathf.Max(0.02f, sizeZ), chamfer);
            var mat = SS3DLevelRenderer.MakeLitMaterial("SS3D Minimal " + name, color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}
