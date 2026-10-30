using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
                    bool textured = (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) || (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null);
                    if (textured)
                    {
                        // Kit material: keep the authored atlas, only bias it warm (or recolour water to the palette turquoise, keeping its alpha).
                        string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
                        if (prop == null) continue;
                        var current = m.GetColor(prop);
                        if (role == SS3DTint.Water) { m.SetColor(prop, new Color(color.r, color.g, color.b, current.a)); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth); }
                        else if (palette.kitMode) m.SetColor(prop, current * palette.kitTint);
                        continue;
                    }
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
                    else if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                    if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
                    if (emission.maxColorComponent > 0f && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
                }
            }
        }

        private static Texture2D _stone;
        /// <summary>Procedural cream limestone albedo: soft value noise, faint veining and a light edge darkening (generated once, 256²).</summary>
        public static Texture2D StoneTexture()
        {
            if (_stone != null) return _stone;
            const int n = 256;
            _stone = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "SS3D Stone", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave }; // regenerated on load; keeps it out of saved scenes
            var px = new Color[n * n];
            var rnd = new System.Random(1234);
            float Noise(float x, float y, float scale, int seed) => Mathf.PerlinNoise(x * scale + seed * 17.13f, y * scale + seed * 9.71f);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n;
                float grain = Noise(u, v, 18f, 1) * 0.5f + Noise(u, v, 47f, 2) * 0.3f + Noise(u, v, 120f, 3) * 0.2f;
                float vein = Mathf.Pow(1f - Mathf.Abs(Noise(u, v, 4f, 4) - 0.5f) * 2f, 8f) * 0.12f;
                float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 14f); // slight darkening toward the tile rim
                float val = 0.86f + (grain - 0.5f) * 0.16f - vein;
                val *= 0.92f + 0.08f * edge;
                float speck = ((float)rnd.NextDouble() < 0.004f) ? 0.9f : 1f;
                px[y * n + x] = new Color(val * speck, (val - 0.01f) * speck, (val - 0.04f) * speck, 1f);
            }
            _stone.SetPixels(px); _stone.Apply(true);
            return _stone;
        }

        /// <summary>
        /// Global post-processing volume built at runtime (no profile asset needed): ACES tonemapping, soft bloom, colour
        /// adjustments, vignette and optional Gaussian depth of field. The camera gets post-processing + SMAA enabled.
        /// SSAO is a renderer feature and is enabled by Shadow Steps ▸ 3D ▸ Configure URP rendering (editor only).
        /// </summary>
        public static Volume ApplyPostProcessing(SS3DArtPalette palette, Camera camera)
        {
            if (palette == null) return null;
            const string volumeName = "SS3D_PostProcessing";
            var existing = GameObject.Find(volumeName);
            var volume = existing != null ? existing.GetComponent<Volume>() : null;
            if (volume == null) { volume = new GameObject(volumeName).AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10f; }
            volume.enabled = palette.postProcessing;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "SS3D Runtime Profile";
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(palette.bloomIntensity); bloom.threshold.Override(palette.bloomThreshold); bloom.scatter.Override(0.65f);
            var adj = profile.Add<ColorAdjustments>(true); adj.postExposure.Override(palette.postExposure); adj.saturation.Override(palette.saturation); adj.contrast.Override(palette.contrast);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(palette.vignetteIntensity); vig.smoothness.Override(0.5f);
            if (palette.depthOfField && palette.depthOfFieldStrength > 0f && camera != null)
            {
                var dof = profile.Add<DepthOfField>(true);
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                float d = Vector3.Distance(camera.transform.position, Vector3.zero);
                float start = d + 2.5f; // past the far edge of a 7-8 tile board seen at ~58°; only the perimeter softens
                dof.gaussianStart.Override(start); dof.gaussianEnd.Override(start + 10f * (1.05f - palette.depthOfFieldStrength));
                dof.gaussianMaxRadius.Override(1.0f); dof.highQualitySampling.Override(false);
            }
            volume.sharedProfile = profile;
            if (camera != null)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = palette.postProcessing;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.requiresDepthOption = CameraOverrideOption.On;
                data.renderShadows = true;
            }
            return volume;
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
