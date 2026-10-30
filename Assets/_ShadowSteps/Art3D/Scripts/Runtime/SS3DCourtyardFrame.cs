using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Cosmetic palace courtyard around the board: parapets, entrance arch and door, staircase, pillars, lanterns,
    /// banners, canals and flowerbeds. Everything is placed strictly OUTSIDE the logical tile centres, under a separate
    /// toggleable root, with colliders removed. It introduces no walls, blocked cells or vision blockers.
    /// </summary>
    public sealed class SS3DCourtyardFrame : MonoBehaviour
    {
        public SS3DArtPalette palette;
        public bool generateExterior = true;
        public bool decorateWithOutsidePlants = true;
        [Tooltip("Gap between the last tile centre and the parapet line, in tiles.")]
        public float borderOffset = 0.85f;
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

            Place(palette.archFrame, "Palace_Entrance", new Vector3(middle, 0f, north + 0.65f * s));
            Place(palette.palaceDoor, "Palace_Door", new Vector3(middle, 0f, north + 0.67f * s));
            Place(palette.stairs, "Entry_Staircase", new Vector3(middle, 0f, south - 0.72f * s), Quaternion.Euler(0f, 180f, 0f));
            int column = 0;
            for (float x = left; x <= right + 1e-3f; x += s, column++)
            {
                if (Mathf.Abs(x - middle) < s * 0.85f) { Place(palette.water, "North_Water", new Vector3(x, 0f, north + 1.15f * s)); Place(palette.water, "South_Water", new Vector3(x, 0f, south - 1.15f * s)); continue; }
                Place(palette.wallStraight, "North_Parapet", new Vector3(x, 0f, north));
                Place(palette.wallStraight, "South_Parapet", new Vector3(x, 0f, south));
                Place(palette.water, "North_Water", new Vector3(x, 0f, north + 1.15f * s));
                Place(palette.water, "South_Water", new Vector3(x, 0f, south - 1.15f * s));
                if (decorateWithOutsidePlants && column % 3 == 0)
                {
                    Place(palette.flowerbed, "Outside_Flowerbed", new Vector3(x, 0f, north + 0.75f * s));
                    Place(palette.flowerbed, "Outside_Flowerbed", new Vector3(x, 0f, south - 0.75f * s));
                }
            }
            int row = 0;
            for (float z = south; z <= north + 1e-3f; z += s, row++)
            {
                Place(palette.wallStraight, "West_Border", new Vector3(left, 0f, z), Quaternion.Euler(0f, 90f, 0f));
                Place(palette.wallStraight, "East_Border", new Vector3(right, 0f, z), Quaternion.Euler(0f, 90f, 0f));
                Place(palette.water, "West_Canal", new Vector3(left - 1.1f * s, 0f, z));
                Place(palette.water, "East_Canal", new Vector3(right + 1.1f * s, 0f, z));
                if (decorateWithOutsidePlants && row % 3 == 1)
                {
                    Place(palette.flowerbed, "Outside_Flowerbed", new Vector3(left - 0.75f * s, 0f, z));
                    Place(palette.flowerbed, "Outside_Flowerbed", new Vector3(right + 0.75f * s, 0f, z));
                }
            }
            Place(palette.pillar, "Corner_Pillar_NW", new Vector3(left, 0f, north));
            Place(palette.pillar, "Corner_Pillar_NE", new Vector3(right, 0f, north));
            Place(palette.pillar, "Corner_Pillar_SW", new Vector3(left, 0f, south));
            Place(palette.pillar, "Corner_Pillar_SE", new Vector3(right, 0f, south));
            Place(palette.lantern, "Lamp_Left", new Vector3(middle - 1.05f * s, 0f, north + 0.72f * s));
            Place(palette.lantern, "Lamp_Right", new Vector3(middle + 1.05f * s, 0f, north + 0.72f * s));
            Place(palette.banner, "Banner_Left", new Vector3(middle - 1.5f * s, 0f, north + 0.72f * s));
            Place(palette.banner, "Banner_Right", new Vector3(middle + 1.5f * s, 0f, north + 0.72f * s));
            Place(palette.bench, "Bench_Left", new Vector3(left - 0.75f * s, 0f, south - 0.75f * s));
            Place(palette.bench, "Bench_Right", new Vector3(right + 0.75f * s, 0f, south - 0.75f * s));
        }

        private void Place(GameObject prefab, string name, Vector3 p, Quaternion? rotation = null)
        {
            if (prefab == null) return;
            var obj = Instantiate(prefab, transform.TransformPoint(p), rotation ?? Quaternion.identity, _frame);
            obj.name = name;
            foreach (var c in obj.GetComponentsInChildren<Collider>(true)) SS3DLevelRenderer.SafeDestroy(c);
        }
    }
}
