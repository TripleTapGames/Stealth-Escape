using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowSteps.Art3D
{
    /// <summary>Roles used to tint instantiated art. The imported .glb assets and the character pack are never modified.</summary>
    public enum SS3DTint { None, FloorA, FloorB, Wall, Stone, Water, Wood, Foliage, Exit, Cover }

    /// <summary>Scene lighting and per-instance material tinting driven by the palette (milestone 1: light and colour only).</summary>
    public static class SS3DArtTuning
    {
        /// <summary>Tints every renderer of an instantiated object by setting colours on its material instances.</summary>
        public static void Tint(GameObject instance, SS3DTint role, SS3DArtPalette palette)
        {
            if (instance == null || palette == null || !palette.tintMaterials || role == SS3DTint.None) return;
            Color color; float smooth = palette.stoneSmoothness; Color emission = Color.black;
            switch (role)
            {
                case SS3DTint.FloorA: color = palette.floorA_Cream; break;
                case SS3DTint.FloorB: color = palette.floorB_Cream; break;
                case SS3DTint.Wall: color = palette.wallIvory; break;
                case SS3DTint.Stone: color = palette.warmStone; break;
                case SS3DTint.Water: color = palette.waterTurquoise; smooth = palette.waterSmoothness; emission = palette.waterEmission; break;
                case SS3DTint.Wood: color = palette.woodBrown; smooth = 0.35f; break;
                case SS3DTint.Foliage: color = palette.foliageGreen; smooth = 0.15f; break;
                case SS3DTint.Exit: color = palette.exitGreen; emission = palette.exitGreen * 0.35f; break;
                case SS3DTint.Cover: color = palette.coverCrate; smooth = 0.2f; break;
                default: return;
            }
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials; // instances
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (m == null) continue;
                    // Keep authored accents (banners, lantern glass, flowers): only recolour materials close to the model's main surface, i.e. the first material of each renderer.
                    if (i > 0 && role != SS3DTint.Water) continue;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                    else if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                    if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
                    if (emission.maxColorComponent > 0f && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
                }
            }
        }

        /// <summary>Warm afternoon sun with soft shadows and a warm gradient ambient. Idempotent: reuses an existing sun named by this method.</summary>
        public static Light ApplyLighting(SS3DArtPalette palette, Camera camera)
        {
            const string sunName = "Warm_Directional_Sun";
            var existing = GameObject.Find(sunName);
            var sun = existing != null ? existing.GetComponent<Light>() : null;
            if (sun == null) { sun = new GameObject(sunName).AddComponent<Light>(); sun.type = LightType.Directional; }
            sun.color = palette.sunColor; sun.intensity = palette.sunIntensity;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = palette.sunShadowStrength; sun.shadowBias = 0.02f; sun.shadowNormalBias = 0.4f;
            sun.transform.rotation = Quaternion.Euler(palette.sunAngles.x, palette.sunAngles.y, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = palette.ambientSky; RenderSettings.ambientEquatorColor = palette.ambientEquator; RenderSettings.ambientGroundColor = palette.ambientGround;
            RenderSettings.sun = sun;
            if (camera != null) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = palette.backgroundColor; }
            return sun;
        }
    }
}
