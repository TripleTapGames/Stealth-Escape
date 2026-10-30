using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>Pure framing arithmetic for the fixed angled orthographic camera, so portrait fit and occlusion can be checked without rendering.</summary>
    public static class SS3DFraming
    {
        public sealed class Report
        {
            public float Aspect, Pitch, OrthoSize, UsedWidthFraction, UsedHeightFraction;
            public float WallOccludesNodeBehind, CoverOccludesNodeBehind, ConnectionOccludedByWall;
            public bool Fits => UsedWidthFraction <= 1.0001f && UsedHeightFraction <= 1.0001f;
            public override string ToString() =>
                "aspect " + Aspect.ToString("0.000") + " pitch " + Pitch + "°: orthoSize " + OrthoSize.ToString("0.00") +
                ", board uses " + (UsedWidthFraction * 100f).ToString("0") + "% of usable width and " + (UsedHeightFraction * 100f).ToString("0") + "% of usable height" +
                (Fits ? " (fits)" : " (DOES NOT FIT)") +
                "; a wall hides " + (WallOccludesNodeBehind * 100f).ToString("0") + "% of the node behind it, cover " + (CoverOccludesNodeBehind * 100f).ToString("0") + "%";
        }

        /// <summary>
        /// Computes the orthographic size the fitter will choose and how much of the usable band the board occupies, plus how
        /// far a wall/cover of the given height reaches up the tile behind it on screen (0 = node centre clear, ≥0.5 = node centre hidden).
        /// </summary>
        public static Report Compute(int width, int height, float pitchDegrees, float aspect, float topReserved, float bottomReserved, float paddingTiles, float minimumSize,
            float wallHeight, float coverHeight, float tile = 1f, float verticalExtent = 1.6f)
        {
            var r = new Report { Aspect = aspect, Pitch = pitchDegrees };
            float pad = paddingTiles * tile;
            var size = new Vector3(width * tile + pad * 2f, verticalExtent, height * tile + pad * 2f);
            var rot = Quaternion.Euler(pitchDegrees, 0f, 0f);
            var inv = Quaternion.Inverse(rot);
            float maxX = 0f, maxY = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(size.x * 0.5f * ((i & 1) == 0 ? -1 : 1), size.y * 0.5f * ((i & 2) == 0 ? -1 : 1), size.z * 0.5f * ((i & 4) == 0 ? -1 : 1));
                var p = inv * corner;
                maxX = Mathf.Max(maxX, Mathf.Abs(p.x)); maxY = Mathf.Max(maxY, Mathf.Abs(p.y));
            }
            float usableH = Mathf.Max(0.2f, 1f - topReserved - bottomReserved), usableW = 1f;
            r.OrthoSize = Mathf.Max(minimumSize, Mathf.Max(maxY / usableH, maxX / (usableW * aspect)));
            r.UsedHeightFraction = maxY / (r.OrthoSize * usableH);
            r.UsedWidthFraction = maxX / (r.OrthoSize * aspect * usableW);
            // On screen, one tile of depth spans tile·sin(pitch); a vertical block of height h spans h·cos(pitch).
            float depthSpan = tile * Mathf.Sin(pitchDegrees * Mathf.Deg2Rad);
            float Occl(float h) => h * Mathf.Cos(pitchDegrees * Mathf.Deg2Rad) / depthSpan;
            r.WallOccludesNodeBehind = Occl(wallHeight);
            r.CoverOccludesNodeBehind = Occl(coverHeight);
            r.ConnectionOccludedByWall = Occl(wallHeight) * 2f; // the connection midpoint sits half a tile behind
            return r;
        }
    }
}
