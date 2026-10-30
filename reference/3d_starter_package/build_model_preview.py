from pathlib import Path
import numpy as np, trimesh, math
from PIL import Image, ImageDraw, ImageFont
BASE=Path(__file__).parent
FILES=sorted((BASE/'UnityDrop/Assets/_ShadowSteps/Art3D/Models').glob('*.glb'))
W,H=1250,7*240+90; im=Image.new('RGB',(W,H),(236,246,248));draw=ImageDraw.Draw(im)
try:
 titlefont=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',20)
 font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',14)
except: titlefont=font=ImageFont.load_default()
draw.text((24,20),'SHADOW STEPS · 3D MODULAR STARTER ASSET KIT',fill=(32,69,93),font=titlefont)
draw.text((24,48),'Real GLB models · geometry-only first pass · not final character art',fill=(81,118,132),font=font)
view=np.array([1.45,1.25,1.6],float);view/=np.linalg.norm(view)
right=np.cross(np.array([0,1,0.]),view);right/=np.linalg.norm(right)
up=np.cross(view,right);up/=np.linalg.norm(up)
light=np.array([.3,1,.6]);light/=np.linalg.norm(light)
for k,path in enumerate(FILES):
 col,row=k%4,k//4;x0=18+col*307;y0=88+row*240
 draw.rounded_rectangle((x0,y0,x0+292,y0+227),radius=15,fill=(255,255,255),outline=(205,222,230),width=2)
 scene=trimesh.load(path,force='scene')
 b=scene.bounds;center=(b[0]+b[1])/2
 # respect ground / project mid-height center
 triangles=[];ptsall=[]
 for n in scene.graph.nodes_geometry:
  xf,key=scene.graph[n];mesh=scene.geometry[key]
  v=trimesh.transform_points(mesh.vertices,xf)
  screen=np.column_stack(((v-center)@right,(v-center)@up))
  ptsall.append(screen)
  base=getattr(getattr(mesh.visual,'material',None),'baseColorFactor',[158,172,187,255])
  rgba=np.array(base,dtype=float)
  for f in mesh.faces:
   verts=v[f];nrm=np.cross(verts[1]-verts[0],verts[2]-verts[0]);length=np.linalg.norm(nrm)
   if length<1e-9:continue
   nrm=nrm/length
   dot=max(0,float(np.dot(nrm,light)))
   shade=.69+.31*dot
   rgb=tuple(int(min(255,x*shade)) for x in rgba[:3])
   depth=float(np.mean(verts@view))
   triangles.append((depth,screen[f],rgb))
 pts=np.concatenate(ptsall,axis=0)
 rangeuv=pts.max(axis=0)-pts.min(axis=0)
 scale=min(254/max(.05,rangeuv[0]),160/max(.05,rangeuv[1]))
 mid=(pts.max(axis=0)+pts.min(axis=0))/2
 cx=x0+146;cy=y0+96
 triangles.sort(key=lambda t:t[0])
 for depth,v,col_rgb in triangles:
  points=[(cx+(pt[0]-mid[0])*scale,cy-(pt[1]-mid[1])*scale) for pt in v]
  draw.polygon(points,fill=col_rgb)
  draw.line(points+[points[0]],fill=tuple(int(z*.89) for z in col_rgb),width=1)
 name=path.stem.replace('_',' ')
 if len(name)>31:name=name[:28]+'...'
 draw.text((x0+14,y0+186),name,fill=(32,61,85),font=font)
 draw.text((x0+14,y0+204),f'{len(scene.geometry)} parts   ·   {len(triangles)} triangles',fill=(103,136,152),font=font)
OUT=BASE/'Model_Kit_Contact_Sheet.png';im.save(OUT);print(OUT,OUT.stat().st_size)
