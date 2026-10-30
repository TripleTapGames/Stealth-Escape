using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Optional cosmetic exterior frame. Every generated item is OUTSIDE valid node centres.
    /// No new logical walls, blocked cells, or vision blockers are introduced by this decorator.
    /// Do not rely on this geometry for the authoritative LOS system.
    /// </summary>
    public sealed class SS3DCourtyardFrame : MonoBehaviour
    {
        public SS3DArtPalette palette;
        public bool generateExterior = true;
        public bool decorateWithOutsidePlants = true;
        Transform _frame;

        [ContextMenu("Build Around Level")]
        public void Build(SS3DLevelRenderer board)
        {
            if(!board||board.Level==null||!palette)return;
            if(_frame){
#if UNITY_EDITOR
                if(!Application.isPlaying)DestroyImmediate(_frame.gameObject);
                else Destroy(_frame.gameObject);
#else
                Destroy(_frame.gameObject);
#endif
            }
            var root=new GameObject("Exterior_Decoration_Only");root.transform.SetParent(transform,false);_frame=root.transform;
            if(!generateExterior)return;
            float s=board.tileSpacing;
            float north=board.ToWorld(0,0).z+s*.85f;
            float south=board.ToWorld(0,board.Level.h-1).z-s*.85f;
            float left=board.ToWorld(0,0).x-s*.85f;
            float right=board.ToWorld(board.Level.w-1,0).x+s*.85f;
            float middle=(left+right)*.5f;
            Place(palette.archFrame,"Palace_Entrance",new Vector3(middle,0,north+.65f*s));
            Place(palette.palaceDoor,"Palace_Door",new Vector3(middle,0,north+.67f*s));
            Place(palette.stairs,"Entry_Staircase",new Vector3(middle,0,south-.72f*s),Quaternion.Euler(0,180,0));
            for(float x=left;x<=right;x+=s)
            {
                if(Mathf.Abs(x-middle)<s*.85f)continue;
                Place(palette.wallStraight,"North_Parapet",new Vector3(x,0,north));
                Place(palette.wallStraight,"South_Parapet",new Vector3(x,0,south));
                Place(palette.water,"North_Water",new Vector3(x,0,north+1.15f*s));
                Place(palette.water,"South_Water",new Vector3(x,0,south-1.15f*s));
                if(decorateWithOutsidePlants && (Mathf.RoundToInt((x-left)/s)%3==0))
                {
                    Place(palette.flowerbed,"Outside_Flowerbed",new Vector3(x,0,north+.75f*s));
                    Place(palette.flowerbed,"Outside_Flowerbed",new Vector3(x,0,south-.75f*s));
                }
            }
            for(float z=south;z<=north;z+=s)
            {
                Place(palette.wallStraight,"West_Border",new Vector3(left,0,z),Quaternion.Euler(0,90,0));
                Place(palette.wallStraight,"East_Border",new Vector3(right,0,z),Quaternion.Euler(0,90,0));
                Place(palette.water,"West_Canal",new Vector3(left-1.1f*s,0,z));
                Place(palette.water,"East_Canal",new Vector3(right+1.1f*s,0,z));
            }
            Place(palette.pillar,"Stair_Pillar_Left",new Vector3(middle-1.1f*s,0,south));
            Place(palette.pillar,"Stair_Pillar_Right",new Vector3(middle+1.1f*s,0,south));
            Place(palette.lantern,"Lamp_Left",new Vector3(middle-1.05f*s,0,north+.72f*s));
            Place(palette.lantern,"Lamp_Right",new Vector3(middle+1.05f*s,0,north+.72f*s));
            Place(palette.banner,"Banner_Left",new Vector3(middle-1.5f*s,0,north+.72f*s));
            Place(palette.banner,"Banner_Right",new Vector3(middle+1.5f*s,0,north+.72f*s));
        }
        void Place(GameObject prefab,string name,Vector3 p,Quaternion? rotation=null)
        {
            if(!prefab)return;var obj=Instantiate(prefab,p,rotation??Quaternion.identity,_frame);obj.name=name;
        }
    }
}
