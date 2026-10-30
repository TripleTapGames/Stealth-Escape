#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace ShadowSteps.Art3D.Editor
{
    /// <summary>
    /// Unity Editor utility to generate 2 art-only scenes using the imported GLBs.
    /// Requires Unity glTFast importer and com.unity.nuget.newtonsoft-json.
    /// </summary>
    public static class SS3DCreateDemoScenes
    {
        const string Base="Assets/_ShadowSteps/Art3D";
        const string Models=Base+"/Models/";
        const string Data=Base+"/Data/shadow_steps_levels_v4.json";
        const string SceneDir=Base+"/Demo";
        const string PalettePath=Base+"/SS3DArtPalette.asset";

        [MenuItem("Tools/Shadow Steps/3D/Create Both Demo Scenes")]
        public static void CreateBoth()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var pal=EnsurePalette();
            if(!pal.floorA || !pal.guardProxy || !pal.cameraModel)
            {
                EditorUtility.DisplayDialog("Shadow Steps 3D",
                    "3D model imports are missing. Install glTFast in Package Manager and allow the .glb files to import, then try again.","OK");
                return;
            }
            CreateScene(6,"Clockwise_Rotating_Guard",pal);
            CreateScene(20,"Camera_Basics",pal);
            EditorUtility.DisplayDialog("Shadow Steps 3D", "Created 2 static art-preview scenes in " + SceneDir + ". These are not gameplay-complete scenes.","OK");
        }

        [MenuItem("Tools/Shadow Steps/3D/Create / Update Art Palette")]
        public static SS3DArtPalette EnsurePalette()
        {
            var palette=AssetDatabase.LoadAssetAtPath<SS3DArtPalette>(PalettePath);
            if(!palette)
            {
                palette=ScriptableObject.CreateInstance<SS3DArtPalette>();
                AssetDatabase.CreateAsset(palette,PalettePath);
            }
            palette.floorA=Model("Floor_Tile_A");palette.floorB=Model("Floor_Tile_B");
            palette.wallBlock=Model("Wall_Block_1m");palette.wallStraight=Model("Wall_Straight_1m");
            palette.planter=Model("Obstacle_Planter_1x1");palette.crate=Model("Obstacle_Crate_1x1");
            palette.stoneBlock=Model("Obstacle_Stone_Block_1x1");
            palette.node=Model("Node_Marker");palette.exitPad=Model("Exit_Pad");
            palette.collectibleKey=Model("Collectible_Key");palette.lockedDoor=Model("Door_Locked_Path");
            palette.openedDoor=Model("Door_Open_Path");
            palette.spyProxy=Model("Character_Spy_PROXY");palette.guardProxy=Model("Character_Guard_PROXY");
            palette.cameraModel=Model("Security_Camera");
            palette.cameraMount=Model("Security_Camera_Mount");palette.cameraHead=Model("Security_Camera_Head");
            palette.archFrame=Model("Entrance_Arch_Frame");palette.palaceDoor=Model("Door_Double_Wood");
            palette.stairs=Model("Stairs_3Step_2m");palette.pillar=Model("Pillar_Square");
            palette.water=Model("Water_Tile");palette.bench=Model("Bench_Short");
            palette.lantern=Model("Lantern_Wall");palette.banner=Model("Banner_BlueGold");
            palette.flowerbed=Model("Flowerbed_OutsideBoard");
            EditorUtility.SetDirty(palette);AssetDatabase.SaveAssets();return palette;
        }

        static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Models+name+".glb");
        static void CreateScene(int index,string filename,SS3DArtPalette palette)
        {
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(Data);
            if(!asset)throw new FileNotFoundException("Copy V4 level JSON into " + Data);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("SS3D_Visual_Preview_Only");
            var renderer=root.AddComponent<SS3DLevelRenderer>();renderer.palette=palette;
            var preview=root.AddComponent<SS3DV4PreviewController>();preview.levelIndex=index;
            preview.levelRenderer=renderer;preview.v4LevelsJson=asset;
            var frame=root.AddComponent<SS3DCourtyardFrame>();frame.palette=palette;preview.exteriorFrame=frame;
            var camObj=new GameObject("Camera_Fixed_Ortho");var cam=camObj.AddComponent<Camera>();
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.67f,.84f,.87f);
            var fitter=camObj.AddComponent<SS3DBoardCameraFitter>();preview.boardFitter=fitter;
            var sunObj=new GameObject("Warm_Directional_Sun");var sun=sunObj.AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=1.3f;sun.color=new Color(1f,.91f,.76f);
            sunObj.transform.rotation=Quaternion.Euler(52f,-35f,0);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.79f,.82f,.86f);
            preview.BuildPreview();
            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene,SceneDir+"/"+filename+".unity");
            Debug.Log("3D art-preview scene created: "+filename);
        }
    }
}
#endif
