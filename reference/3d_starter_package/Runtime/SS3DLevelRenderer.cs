using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Visual-only V4 level renderer. Takes the *same* level coordinates as the authoritative game.
    /// X -> Unity +X, V4 row Y increasing downward -> Unity -Z. All gameplay occurs in XZ plane.
    /// No vision, movement, patrol, alarm, key or gadget decisions are made by this class.
    /// </summary>
    public sealed class SS3DLevelRenderer : MonoBehaviour
    {
        public SS3DArtPalette palette;
        [Min(.5f)] public float tileSpacing = 1f;
        public bool alternateFloorTiles = true;
        public bool renderNodeMarkers = true;
        public bool renderConnectionLines = true;
        public bool renderWalls = true;
        public bool renderCoverObstacles = true;
        public Color connectionColor = new Color(.16f, .20f, .28f, .8f);
        public float connectionWidth = .025f;

        SS3DLevel _level;
        Transform _content, _nodes, _lines, _actors, _overlays;
        readonly List<Transform> _guards = new List<Transform>();
        readonly List<Transform> _cameras = new List<Transform>();
        Transform _player;
        readonly List<Transform> _doors = new List<Transform>();
        Material _lineMaterial, _guardTint, _cameraTint, _guardBorder, _cameraBorder;

        public SS3DLevel Level => _level;
        public Vector3 ToWorld(int x, int y) => _level == null ? Vector3.zero :
            new Vector3((x - (_level.w - 1) * .5f) * tileSpacing, 0,
                        ((_level.h - 1) * .5f - y) * tileSpacing);

        public void Build(SS3DLevel level)
        {
            if (level == null || level.rows == null) throw new ArgumentNullException(nameof(level));
            if (palette == null) throw new InvalidOperationException("Assign an SS3DArtPalette asset first.");
            ClearGenerated();
            _level = level;
            _content = MakeRoot("Generated_Level_Geometry", transform);
            _nodes = MakeRoot("Movement_Nodes", transform);
            _lines = MakeRoot("Movement_Connections", transform);
            _actors = MakeRoot("Actor_Visuals", transform);
            _overlays = MakeRoot("Gameplay_Vision_Overlays", transform);
            _guards.Clear(); _cameras.Clear(); _doors.Clear(); _player = null;

            for (int y=0;y<level.h;y++) for (int x=0;x<level.w;x++)
            {
                var pos=ToWorld(x,y);
                var floor=(alternateFloorTiles && ((x+y)%2==1) && palette.floorB) ? palette.floorB : palette.floorA;
                Create(floor, $"Floor_{x}_{y}", pos, Quaternion.identity, _content);
                char state=level.rows[y][x];
                if (state == '#' && renderWalls) Create(palette.wallBlock,$"SolidWall_{x}_{y}",pos,Quaternion.identity,_content);
                if (state == 'X' && renderCoverObstacles)
                {
                    // 'X' is blocked solid cover, not merely a missing connection.
                    // The visual choice cannot change physics or visibility.
                    GameObject cover = (x+y)%3==0 ? palette.crate : ((x+y)%3==1 ? palette.planter : palette.stoneBlock);
                    Create(cover,$"Cover_X_{x}_{y}",pos,Quaternion.identity,_content);
                }
                if (level.IsWalkable(x,y) && renderNodeMarkers)
                    Create(palette.node,$"Node_{x}_{y}",pos,Quaternion.identity,_nodes);
            }

            if (renderConnectionLines)
            {
                var cuts = new HashSet<string>(level.cuts ?? Array.Empty<string>());
                for(int y=0;y<level.h;y++)for(int x=0;x<level.w;x++)
                {
                    if (!level.IsWalkable(x,y)) continue;
                    AddConnection(level,cuts,x,y,x+1,y);
                    AddConnection(level,cuts,x,y,x,y+1);
                }
            }

            foreach(var door in level.doors ?? Array.Empty<SS3DDoor>())
            {
                if(door.a == null || door.b == null)continue;
                Vector3 a=ToWorld(door.a[0],door.a[1]),b=ToWorld(door.b[0],door.b[1]);
                // Door model length axis +X; perpendicular to connection.
                Vector3 direction=(b-a).normalized;
                var rot=Quaternion.LookRotation(Vector3.Cross(direction,Vector3.up),Vector3.up);
                var obj=Create(palette.lockedDoor,"Locked_Door",(a+b)*.5f,rot,_content);
                if(obj)_doors.Add(obj.transform);
            }

            if (level.exit != null) Create(palette.exitPad,"Exit",ToWorld(level.exit[0],level.exit[1]),Quaternion.identity,_content);
            if (level.key != null) Create(palette.collectibleKey,"Key",ToWorld(level.key[0],level.key[1]),Quaternion.identity,_content);
            if (level.start != null)
            {
                var obj=Create(palette.spyProxy,"Spy",ToWorld(level.start[0],level.start[1]),Quaternion.identity,_actors);
                if(obj) _player=obj.transform;
            }
            foreach(var guard in level.guards ?? Array.Empty<SS3DGuard>())
            {
                int[] p = guard.at ?? (guard.path != null && guard.path.Length > 0 ? guard.path[0] : null);
                if(p == null)continue;
                var obj=Create(palette.guardProxy,"Guard",ToWorld(p[0],p[1]),Facing(guard.dir),_actors);
                if(obj) _guards.Add(obj.transform);
            }
            foreach(var cam in level.cams ?? Array.Empty<SS3DCamera>())
            {
                if (cam.wall==null)continue;
                var offset=Heading(cam.mountDir);
                // The wall tile's centre is the anchor; push to free side by 0.501 nodes.
                Vector3 basePoint=ToWorld(cam.wall[0],cam.wall[1])+offset*tileSpacing*.501f;
                if(palette.cameraMount && palette.cameraHead)
                {
                    // Static bracket faces the mounting direction. Only the lens/head rotates.
                    Create(palette.cameraMount,"Fixed_Wall_Bracket",basePoint,Facing(cam.mountDir),_actors);
                    var head=Create(palette.cameraHead,"Rotating_Camera_Head",basePoint,Facing(cam.dir),_actors);
                    if(head) _cameras.Add(head.transform);
                }
                else
                {
                    var obj=Create(palette.cameraModel,"WallMounted_Camera_Legacy",basePoint,Facing(cam.dir),_actors);
                    if(obj) _cameras.Add(obj.transform);
                }
            }
        }

        void AddConnection(SS3DLevel level,HashSet<string> cuts,int x,int y,int nx,int ny)
        {
            if (!level.IsWalkable(nx,ny)) return;
            string a=$"{x},{y}",b=$"{nx},{ny}";
            if(cuts.Contains(string.CompareOrdinal(a,b)<0?a+"|"+b:b+"|"+a)) return;
            var start=ToWorld(x,y);var end=ToWorld(nx,ny);
            var center=(start+end)*.5f+Vector3.up*.125f;
            var line=GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name=$"Path_{a}_to_{b}";line.transform.SetParent(_lines,false);
            line.transform.position=center;line.transform.rotation=Quaternion.LookRotation(end-start,Vector3.up);
            line.transform.localScale=new Vector3(connectionWidth,.012f,Vector3.Distance(start,end));
            var collider=line.GetComponent<Collider>();if(collider)SafeDestroy(collider);
            if (!_lineMaterial) _lineMaterial=MakeMaterial("SS3D Connection",connectionColor);
            line.GetComponent<Renderer>().sharedMaterial=_lineMaterial;
        }

        public void SetPlayerVisual(Vector2Int p)
        {
            if(_player != null) _player.position=ToWorld(p.x,p.y);
        }
        public void SetGuardVisual(int index,Vector2Int node,string facing)
        {
            if(index<0||index>=_guards.Count)return;
            _guards[index].SetPositionAndRotation(ToWorld(node.x,node.y),Facing(facing));
        }
        public void SetCameraVisual(int index,string facing,bool empDisabled)
        {
            if(index<0||index>=_cameras.Count)return;
            var obj=_cameras[index];obj.rotation=Facing(facing);
            // Illustrative tint; actual disabled effects and timeline driven by Unity gameplay controller.
            foreach(var r in obj.GetComponentsInChildren<Renderer>())
                if(r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor",empDisabled?new Color(.45f,.88f,.92f):Color.white);
        }
        public void SetDoorVisual(int index,bool unlocked)
        {
            if(index<0||index>=_doors.Count||!palette.openedDoor)return;
            Transform previous=_doors[index];if(previous==null)return;
            Vector3 p=previous.position;Quaternion rot=previous.rotation;
            SafeDestroy(previous.gameObject);
            var obj=Create(unlocked?palette.openedDoor:palette.lockedDoor,"Door_"+index,p,rot,_content);
            _doors[index]=obj?obj.transform:null;
        }

        /// <summary>
        /// Draw authoritative vision cells. Red guard and amber camera indications can overlap.
        /// Called by the CURRENT Unity gameplay rules engine, never predicted here.
        /// </summary>
        public void SetVision(IReadOnlyCollection<Vector2Int> guardCells,IReadOnlyCollection<Vector2Int> cameraCells)
        {
            if(!_overlays)return;
            ClearChildren(_overlays);
            if(guardCells != null)foreach(var p in guardCells)DrawDanger(p,new Color(.91f,.23f,.24f,.20f),"Guard_Vision",.165f);
            if(cameraCells != null)foreach(var p in cameraCells)DrawDanger(p,new Color(.95f,.70f,.17f,.20f),"Camera_Vision",.169f);
        }

        void DrawDanger(Vector2Int node,Color tint,string name,float height)
        {
            if(!_level.IsWalkable(node.x,node.y))return;
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name=name+"_"+node.x+"_"+node.y;
            quad.transform.SetParent(_overlays,false);
            quad.transform.position=ToWorld(node.x,node.y)+Vector3.up*height;
            quad.transform.rotation=Quaternion.Euler(-90,0,0);
            quad.transform.localScale=new Vector3(.87f*tileSpacing,.87f*tileSpacing,1f);
            var collider=quad.GetComponent<Collider>();if(collider) SafeDestroy(collider);
            var renderer=quad.GetComponent<Renderer>();
            if(name=="Guard_Vision" && !_guardTint)_guardTint=MakeOverlayMaterial(name,tint);
            if(name=="Camera_Vision" && !_cameraTint)_cameraTint=MakeOverlayMaterial(name,tint);
            renderer.sharedMaterial=name=="Guard_Vision"?_guardTint:_cameraTint;
            // Two separate rings are intentionally kept for overlapping guard + camera coverage.
            var lineObj=new GameObject(name+"_Danger_Border");lineObj.transform.SetParent(_overlays,false);
            lineObj.transform.position=ToWorld(node.x,node.y)+Vector3.up*(height+.014f);
            var lr=lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace=false;lr.loop=true;lr.positionCount=32;
            lr.widthMultiplier=.028f*tileSpacing;lr.numCornerVertices=2;
            bool guard=name=="Guard_Vision";
            if(guard&&!_guardBorder)_guardBorder=MakeMaterial("Guard Danger Border",new Color(.90f,.23f,.25f));
            if(!guard&&!_cameraBorder)_cameraBorder=MakeMaterial("Camera Danger Border",new Color(.98f,.67f,.13f));
            lr.sharedMaterial=guard?_guardBorder:_cameraBorder;
            float radius=(guard ? 0.44f : 0.34f)*tileSpacing;
            for(int i=0;i<32;i++){
                float angle=i*Mathf.PI*2f/32f;
                lr.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));
            }
        }
        static Vector3 Heading(string d)
        {
            switch(d){case "E":return Vector3.right;case "S":return Vector3.back;case "W":return Vector3.left;default:return Vector3.forward;}
        }
        static Quaternion Facing(string d) => Quaternion.LookRotation(Heading(d),Vector3.up);
        static Transform MakeRoot(string name,Transform parent)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;
        }
        static GameObject Create(GameObject prefab,string name,Vector3 pos,Quaternion rotation,Transform parent)
        {
            if(!prefab)return null;
            var obj=Instantiate(prefab,pos,rotation,parent);obj.name=name;
            return obj;
        }
        static Material MakeMaterial(string name,Color color)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");
            var m=new Material(shader){name=name,color=color};return m;
        }
        static Material MakeOverlayMaterial(string name,Color tint)
        {
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(!shader)shader=Shader.Find("Unlit/Color");
            var mat=new Material(shader){name=name};
            if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",tint);
            if(mat.HasProperty("_Color"))mat.SetColor("_Color",tint);
            mat.SetFloat("_Surface",1f);mat.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;
            return mat;
        }
        void ClearGenerated()
        {
            foreach(var name in new[]{"Generated_Level_Geometry","Movement_Nodes","Movement_Connections","Actor_Visuals","Gameplay_Vision_Overlays"})
            {
                var t=transform.Find(name);if(t)SafeDestroy(t.gameObject);
            }
        }
        static void ClearChildren(Transform t)
        {
            for(int i=t.childCount-1;i>=0;i--) SafeDestroy(t.GetChild(i).gameObject);
        }
        static void SafeDestroy(UnityEngine.Object o)
        {
            if(!o)return;
#if UNITY_EDITOR
            if(!Application.isPlaying){DestroyImmediate(o);return;}
#endif
            Destroy(o);
        }
        public Bounds BoardBounds(float maxVerticalExtent=1.6f)
        {
            if(_level==null)return new Bounds(transform.position,new Vector3(1,1,1));
            var b=new Bounds(transform.position,new Vector3(_level.w*tileSpacing,maxVerticalExtent,_level.h*tileSpacing));
            return b;
        }
    }
}
