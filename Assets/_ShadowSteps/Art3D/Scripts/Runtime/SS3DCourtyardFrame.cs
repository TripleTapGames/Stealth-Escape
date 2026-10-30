using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Cosmetic palace courtyard around the board, composed for portrait phones: palace façade with doors, lanterns and
    /// banners at the north (top of screen), a terrace with the staircase at the south (bottom), parapets with benches on
    /// the sides, then canals and flowerbeds repeated outward until the screen edge of the tallest supported aspect.
    /// Everything is placed strictly OUTSIDE the logical tile centres, under one toggleable root, with colliders removed;
    /// it introduces no walls, blocked cells or vision blockers.
    /// </summary>
    public sealed class SS3DCourtyardFrame : MonoBehaviour
    {
        public SS3DArtPalette palette;
        public bool generateExterior = true;
        public bool decorateWithOutsidePlants = true;
        [Tooltip("Gap between the last tile centre and the parapet line, in tiles.")]
        public float borderOffset = 0.85f;
        [Tooltip("Optional fitter; when set, decoration extends to the visible ground rectangle of its design aspect.")]
        public SS3DBoardCameraFitter fitter;
        [Tooltip("Rows of water/flowerbeds generated beyond the parapets when no fitter is available.")]
        public int fallbackRings = 3;
        private Transform _frame;

        [ContextMenu("Build Around Level")]
        public void Build(SS3DLevelRenderer board)
        {
            if (board == null || !board.IsBuilt || palette == null) return;
            if (_frame != null) SS3DLevelRenderer.SafeDestroy(_frame.gameObject);
            var root = new GameObject("Exterior_Decoration_Only");
            root.transform.SetParent(transform, false);
            _frame = root.transform;
            if (!generateExterior) return;
            var stage = board.Stage;
            float s = board.tileSpacing;
            float north = board.ToWorld(0, 0).z + s * borderOffset;
            float south = board.ToWorld(0, stage.Height - 1).z - s * borderOffset;
            float left = board.ToWorld(0, 0).x - s * borderOffset;
            float right = board.ToWorld(stage.Width - 1, 0).x + s * borderOffset;
            float middle = (left + right) * 0.5f;

            // Visible ground extent for the tallest supported portrait aspect (plus one tile so nothing pops at the edge).
            Rect visible = fitter != null ? fitter.VisibleGroundRect(fitter.designAspect, palette.floorTop) : Rect.MinMaxRect(left - fallbackRings * s, south - fallbackRings * s, right + fallbackRings * s, north + fallbackRings * s);
            float farNorth = Mathf.Max(visible.yMax + s, north + 2.5f * s), farSouth = Mathf.Min(visible.yMin - s, south - 2.5f * s);
            float farWest = Mathf.Min(visible.xMin - s, left - 2f * s), farEast = Mathf.Max(visible.xMax + s, right + 2f * s);

            // ---- North: façade line one tile behind the parapet, doors/lanterns/banners centred, water basins at the corners.
            float facadeZ = north + 1.0f * s;
            for (float x = left; x <= right + 1e-3f; x += s)
            {
                if (Mathf.Abs(x - middle) < 0.55f * s) continue;
                Stone(palette.wallStraight, "Facade_Wall", new Vector3(x, 0f, facadeZ), SS3DTint.Wall);
                Stone(palette.wallBlock, "Facade_Block", new Vector3(x, 0f, facadeZ + 0.5f * s), SS3DTint.Wall);
            }
            Stone(palette.archFrame, "Palace_Entrance", new Vector3(middle, 0f, facadeZ), SS3DTint.Stone);
            Wood(palette.palaceDoor, "Palace_Door", new Vector3(middle, 0f, facadeZ + 0.05f * s));
            Stone(palette.pillar, "Entrance_Pillar_L", new Vector3(middle - 1.15f * s, 0f, facadeZ), SS3DTint.Stone);
            Stone(palette.pillar, "Entrance_Pillar_R", new Vector3(middle + 1.15f * s, 0f, facadeZ), SS3DTint.Stone);
            Place(palette.lantern, "Lamp_Left", new Vector3(middle - 0.95f * s, 0f, facadeZ - 0.3f * s));
            Place(palette.lantern, "Lamp_Right", new Vector3(middle + 0.95f * s, 0f, facadeZ - 0.3f * s));
            Place(palette.banner, "Banner_Left", new Vector3(middle - 1.9f * s, 0f, facadeZ - 0.2f * s));
            Place(palette.banner, "Banner_Right", new Vector3(middle + 1.9f * s, 0f, facadeZ - 0.2f * s));
            for (float z = facadeZ + 1.5f * s; z <= farNorth; z += s)
                for (float x = farWest; x <= farEast + 1e-3f; x += s)
                    AltRow(x, z, left, right, s);
            // red carpet strip from the door to the parapet gap
            for (float z = north + 0.1f * s; z < facadeZ; z += 0.45f * s) Tinted(palette.floorA, "Carpet", new Vector3(middle, 0.02f, z), new Color(0.80f, 0.20f, 0.22f), 0.5f, 0.45f);

            // ---- Parapets with the north gap for the entrance and the south gap for the stairs.
            for (float x = left; x <= right + 1e-3f; x += s)
            {
                if (Mathf.Abs(x - middle) >= 0.55f * s) { Stone(palette.wallStraight, "North_Parapet", new Vector3(x, 0f, north), SS3DTint.Wall); Stone(palette.wallStraight, "South_Parapet", new Vector3(x, 0f, south), SS3DTint.Wall); }
            }
            for (float z = south; z <= north + 1e-3f; z += s)
            {
                Stone(palette.wallStraight, "West_Parapet", new Vector3(left, 0f, z), SS3DTint.Wall, Quaternion.Euler(0f, 90f, 0f));
                Stone(palette.wallStraight, "East_Parapet", new Vector3(right, 0f, z), SS3DTint.Wall, Quaternion.Euler(0f, 90f, 0f));
            }
            Stone(palette.pillar, "Corner_NW", new Vector3(left, 0f, north), SS3DTint.Stone); Stone(palette.pillar, "Corner_NE", new Vector3(right, 0f, north), SS3DTint.Stone);
            Stone(palette.pillar, "Corner_SW", new Vector3(left, 0f, south), SS3DTint.Stone); Stone(palette.pillar, "Corner_SE", new Vector3(right, 0f, south), SS3DTint.Stone);
            Wood(palette.bench, "Bench_W", new Vector3(left + 0.45f * s, 0f, (north + south) * 0.5f), Quaternion.Euler(0f, 90f, 0f));
            Wood(palette.bench, "Bench_E", new Vector3(right - 0.45f * s, 0f, (north + south) * 0.5f), Quaternion.Euler(0f, -90f, 0f));

            // ---- South: terrace, double staircase, sphere-topped pillars, then water and greenery to the bottom edge.
            Stone(palette.stairs, "Entry_Stairs_Upper", new Vector3(middle, 0f, south - 0.95f * s), SS3DTint.Stone, Quaternion.Euler(0f, 180f, 0f));
            Stone(palette.stairs, "Entry_Stairs_Lower", new Vector3(middle, -0.38f, south - 2.6f * s), SS3DTint.Stone, Quaternion.Euler(0f, 180f, 0f));
            Stone(palette.pillar, "Stair_Pillar_L", new Vector3(middle - 1.15f * s, 0f, south - 0.3f * s), SS3DTint.Stone);
            Stone(palette.pillar, "Stair_Pillar_R", new Vector3(middle + 1.15f * s, 0f, south - 0.3f * s), SS3DTint.Stone);
            for (float z = south - 1.0f * s; z >= farSouth; z -= s)
                for (float x = farWest; x <= farEast + 1e-3f; x += s)
                {
                    if (Mathf.Abs(x - middle) < 1.6f * s && z >= south - 3.4f * s) continue; // keep the staircase lane clear
                    AltRow(x, z, left, right, s);
                }

            // ---- East/West: canal, then flowerbeds and water to the screen edge.
            for (float z = south; z <= north + 1e-3f; z += s)
            {
                for (float x = left - s; x >= farWest; x -= s) AltCol(x, z, left, s);
                for (float x = right + s; x <= farEast + 1e-3f; x += s) AltCol(x, z, right, s);
            }
        }

        private void AltRow(float x, float z, float left, float right, float s)
        {
            int ring = Mathf.RoundToInt(Mathf.Abs(z) / s);
            bool plant = decorateWithOutsidePlants && ((Mathf.RoundToInt(x / s) + ring) % 3 == 0);
            if (plant) Foliage(palette.flowerbed, "Outside_Flowerbed", new Vector3(x, 0f, z)); else Water(new Vector3(x, 0f, z));
        }

        private void AltCol(float x, float z, float edge, float s)
        {
            int ring = Mathf.RoundToInt(Mathf.Abs(x - edge) / s);
            bool plant = decorateWithOutsidePlants && ring >= 2 && ((Mathf.RoundToInt(z / s) + ring) % 2 == 0);
            if (plant) Foliage(palette.flowerbed, "Outside_Flowerbed", new Vector3(x, 0f, z)); else Water(new Vector3(x, 0f, z));
        }

        private void Water(Vector3 p) => Tinted(palette.water, "Canal", p, SS3DTint.Water);
        private void Foliage(GameObject prefab, string name, Vector3 p) => Tinted(prefab, name, p, SS3DTint.Foliage);
        private void Stone(GameObject prefab, string name, Vector3 p, SS3DTint tint, Quaternion? rot = null) => Tinted(prefab, name, p, tint, rot);
        private void Wood(GameObject prefab, string name, Vector3 p, Quaternion? rot = null) => Tinted(prefab, name, p, SS3DTint.Wood, rot);

        private GameObject Place(GameObject prefab, string name, Vector3 p, Quaternion? rotation = null)
        {
            if (prefab == null) return null;
            var obj = Instantiate(prefab, transform.TransformPoint(p), rotation ?? Quaternion.identity, _frame);
            obj.name = name;
            foreach (var c in obj.GetComponentsInChildren<Collider>(true)) SS3DLevelRenderer.SafeDestroy(c);
            return obj;
        }

        private void Tinted(GameObject prefab, string name, Vector3 p, SS3DTint tint, Quaternion? rotation = null)
        {
            var obj = Place(prefab, name, p, rotation);
            SS3DArtTuning.Tint(obj, tint, palette);
        }

        private void Tinted(GameObject prefab, string name, Vector3 p, Color color, float scaleX, float scaleZ)
        {
            var obj = Place(prefab, name, p);
            if (obj == null) return;
            obj.transform.localScale = new Vector3(scaleX, 0.2f, scaleZ);
            foreach (var r in obj.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f); }
        }
    }
}
