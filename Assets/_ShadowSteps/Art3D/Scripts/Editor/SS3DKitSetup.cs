using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;

namespace ShadowSteps.Art3D.Editor
{
    /// <summary>
    /// Maps the Synty POLYGON Fantasy Kingdom pack into the art palette: converts its Built-in Standard materials to
    /// URP Lit with Unity's own upgrader, then assigns walls, cover and every decorative kit slot by prefab path.
    /// Characters, cameras, node/exit/key/door models and the procedural stone floor are untouched. Runs automatically
    /// once per generator version when the pack folder exists; safe to run again after re-importing the pack.
    /// </summary>
    public static class SS3DKitSetup
    {
        public const string KitRoot = "Assets/PolygonFantasyKingdom";
        private static string Prefab(string sub) => KitRoot + "/Prefabs/" + sub + ".prefab";
        public static bool KitPresent => AssetDatabase.IsValidFolder(KitRoot + "/Prefabs");

        [MenuItem("Shadow Steps/3D/Kit/Convert Synty materials to URP Lit")]
        public static int ConvertMaterials()
        {
            var upgraders = new List<MaterialUpgrader> { new StandardUpgrader("Standard"), new StandardUpgrader("Standard (Specular setup)") };
            int converted = 0, skipped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { KitRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null) continue;
                string shader = m.shader.name;
                if (shader != "Standard" && shader != "Standard (Specular setup)") { skipped++; continue; }
                MaterialUpgrader.Upgrade(m, upgraders, MaterialUpgrader.UpgradeFlags.None);
                EditorUtility.SetDirty(m); converted++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[SS3D] kit materials: " + converted + " converted to URP Lit, " + skipped + " left as they were (already URP, particle or legacy shaders).");
            return converted;
        }

        [MenuItem("Shadow Steps/3D/Kit/Map Synty Fantasy Kingdom into Art Palette")]
        public static void MapKit()
        {
            if (!KitPresent) { Debug.LogWarning("[SS3D] " + KitRoot + " not found; import POLYGON Fantasy Kingdom first (Package Manager ▸ My Assets)."); return; }
            ConvertMaterials();
            var palette = SS3DSetupMenu.EnsurePalette();
            var missing = new List<string>();
            GameObject Get(string sub) { var go = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(sub)); if (go == null) missing.Add(sub); return go; }
            GameObject First(params string[] subs) { foreach (var s in subs) { var go = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(s)); if (go != null) return go; } missing.Add(subs[0]); return null; }

            // Grid pieces fitted to the cell at build time (bounds-based), so exact kit sizes do not matter.
            var wall = Get("Castle/SM_Bld_Castle_Wall_Block_01"); // light castle stone, same atlas as the façade
            if (wall != null) palette.wallBlock = wall;
            var variants = new List<GameObject>();
            foreach (var v in new[] { "Castle/SM_Bld_Castle_Wall_Block_01", "Castle/SM_Bld_Castle_Wall_Block_02", "Castle/SM_Bld_Castle_Wall_Block_03", "Castle/SM_Bld_Castle_Wall_Block_04", "Castle/SM_Bld_Castle_Wall_Block_05" })
            { var go = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(v)); if (go != null) variants.Add(go); }
            palette.kitWallVariants = variants.ToArray();
            var cover = new List<GameObject>();
            foreach (var c in new[] { "Props/SM_Prop_Crate_Wood_01", "Props/SM_Prop_Barrel_01", "Environments/SM_Env_Planter_Pot_01", "Props/SM_Prop_Hay_Bale_01", "Props/SM_Prop_Sack_Stack_01" })
            { var go = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(c)); if (go != null) cover.Add(go); else missing.Add(c); }
            if (cover.Count > 0) palette.coverObstacles = cover.ToArray();

            // Decorative slots (all optional; the frame falls back to procedural trim per slot).
            palette.kitGround = First("Environments/SM_Env_Ground_Flat_01", "Generic/SM_Generic_Ground_01");
            palette.kitParapetRail = Get("Castle/SM_Bld_Walkway_Stone_Railing_01");
            palette.kitParapetPost = Get("Castle/SM_Bld_Walkway_Stone_Railing_Pillar_01");
            palette.kitWallRun = First("Castle/SM_Bld_Castle_Wall_Window_Fancy_01", "Castle/SM_Bld_Castle_Wall_Window_01", "Castle/SM_Bld_Castle_Wall_01");
            palette.kitWallGate = First("Castle/SM_Bld_Castle_Wall_Gate_01", "Castle/SM_Bld_Castle_Wall_Door_Big_01");
            palette.kitBattlement = Get("Castle/SM_Bld_Castle_Battlements_01");
            palette.kitTower = Get("Castle/SM_Bld_Castle_Wall_Tower_S_01");
            palette.kitHedge = Get("Environments/SM_Env_Hedge_01");
            palette.kitTree = Get("Environments/SM_Env_Tree_Round_01");
            palette.kitUrn = Get("Environments/SM_Env_Planter_Pedestal_01");
            palette.kitStatue = First("Props/SM_Prop_Statue_Knight_01", "Props/SM_Prop_Statue_Hero_01");
            palette.kitStatuePlinth = Get("Props/SM_Prop_Statue_Plinth_01");
            palette.kitCarpet = Get("Props/SM_Prop_Rug_01");
            palette.kitLamp = Get("Props/SM_Prop_Street_Lamp_01");
            palette.kitBush = Get("Environments/SM_Env_Bush_Flowers_01");
            palette.kitFlowers = Get("Environments/SM_Env_Flowers_01");
            var bench = Get("Props/Furniture/SM_Prop_Bench_Seat_01"); if (bench != null) palette.bench = bench;
            var banner = Get("Props/Banners/SM_Prop_Banner_01"); if (banner != null) palette.banner = banner;
            var water = First("Generic/SM_Generic_Water_Plane_01", "Environments/SM_Env_Water_Plane_01"); if (water != null) palette.water = water;

            palette.kitMode = true;
            palette.kitScale = 0.5f;
            palette.proceduralFloorTiles = true; // cream stone board stays; the kit supplies walls, cover and the perimeter
            palette.wallHeightScale = Mathf.Clamp(palette.wallHeightScale, 0.5f, 0.8f);
            EditorUtility.SetDirty(palette); AssetDatabase.SaveAssets();
            if (missing.Count > 0) Debug.LogWarning("[SS3D] kit mapping: " + missing.Count + " prefab(s) not found (slot left as before): " + string.Join(", ", missing));
            Debug.Log("[SS3D] Synty Fantasy Kingdom mapped into " + SS3DArtPalette.DefaultAssetPath + " (kitMode on). Regenerate the demo scenes to see it: Shadow Steps ▸ 3D ▸ Create demo scenes.");
        }

        [MenuItem("Shadow Steps/3D/Kit/Map kit and regenerate demo scenes")]
        public static void MapAndRegenerate()
        {
            MapKit();
            SS3DSetupMenu.CreateTwoDemoScenes();
        }
    }
}
