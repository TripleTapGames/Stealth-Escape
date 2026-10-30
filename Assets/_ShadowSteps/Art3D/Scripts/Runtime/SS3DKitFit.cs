using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Fits any instantiated prefab to a requested size from its measured renderer bounds, then aligns it so the bounds
    /// centre sits on a ground point (XZ) and the bounds bottom on the ground height. Pivot position, authored scale and
    /// unit system of the source kit therefore do not matter. Sizes are given in the object's own (unrotated) axes.
    /// Visual only: nothing here touches level data, colliders are already stripped by the callers.
    /// </summary>
    public static class SS3DKitFit
    {
        public static bool TryBounds(GameObject go, out Bounds b)
        {
            b = default; bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        public static Vector3 WorldSize(GameObject go) => TryBounds(go, out var b) ? b.size : Vector3.zero;

        /// <summary>
        /// Per-axis fit. Pass NaN for an axis to leave it proportional (it follows the Y factor when Y is given, else the
        /// mean of the given factors). With <paramref name="uniform"/> the smallest factor is used on every axis.
        /// </summary>
        public static void Fit(GameObject go, Vector3 ground, float sizeX, float sizeY, float sizeZ, bool uniform)
        {
            if (go == null) return;
            var rot = go.transform.rotation; go.transform.rotation = Quaternion.identity;
            if (!TryBounds(go, out var b)) { go.transform.rotation = rot; return; }
            float fx = Factor(sizeX, b.size.x), fy = Factor(sizeY, b.size.y), fz = Factor(sizeZ, b.size.z);
            float given = 0f; int n = 0;
            foreach (var f in new[] { fx, fy, fz }) if (!float.IsNaN(f)) { given += f; n++; }
            float fallback = !float.IsNaN(fy) ? fy : (n > 0 ? given / n : 1f);
            if (float.IsNaN(fx)) fx = fallback; if (float.IsNaN(fy)) fy = fallback; if (float.IsNaN(fz)) fz = fallback;
            if (uniform) fx = fy = fz = Mathf.Min(fx, Mathf.Min(fy, fz));
            var ls = go.transform.localScale;
            go.transform.localScale = new Vector3(ls.x * fx, ls.y * fy, ls.z * fz);
            go.transform.rotation = rot;
            Ground(go, ground);
        }

        /// <summary>Uniform scale so the footprint is at most <paramref name="maxFootprint"/> and the height at most <paramref name="maxHeight"/> (use +inf to ignore one).</summary>
        public static void FitUniform(GameObject go, Vector3 ground, float maxFootprint, float maxHeight)
        {
            if (go == null) return;
            var rot = go.transform.rotation; go.transform.rotation = Quaternion.identity;
            if (!TryBounds(go, out var b)) { go.transform.rotation = rot; return; }
            float f = float.PositiveInfinity;
            if (!float.IsInfinity(maxFootprint)) f = Mathf.Min(f, Mathf.Min(Factor(maxFootprint, b.size.x), Factor(maxFootprint, b.size.z)));
            if (!float.IsInfinity(maxHeight)) f = Mathf.Min(f, Factor(maxHeight, b.size.y));
            if (float.IsInfinity(f) || float.IsNaN(f)) f = 1f;
            go.transform.localScale *= f;
            go.transform.rotation = rot;
            Ground(go, ground);
        }

        /// <summary>Uniform kit scale (kit metres → tile units) plus ground alignment.</summary>
        public static void Scale(GameObject go, Vector3 ground, float factor)
        {
            if (go == null) return;
            go.transform.localScale *= factor;
            Ground(go, ground);
        }

        /// <summary>Moves the object so its bounds centre (XZ) and bottom (Y) coincide with <paramref name="ground"/>.</summary>
        public static void Ground(GameObject go, Vector3 ground)
        {
            if (!TryBounds(go, out var b)) return;
            go.transform.position += ground - new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static float Factor(float wanted, float measured) => float.IsNaN(wanted) ? float.NaN : (measured > 1e-4f ? wanted / measured : 1f);
    }
}
