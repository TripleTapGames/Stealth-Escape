"""Shadow Steps procedural 3D starter kit. Requires numpy & trimesh.
Run: python Tools_generate_models.py
All exported meshes use X right, Y UP, Z forward (+Z north).
All origins are at ground footprint centers except special objects noted in README.
These are geometric prototypes, not polished production models.
"""
from pathlib import Path
import math
import numpy as np
import trimesh
from trimesh.visual.material import PBRMaterial

OUT=Path(__file__).resolve().parent / 'UnityDrop' / 'Assets' / '_ShadowSteps' / 'Art3D' / 'Models'
OUT.mkdir(parents=True,exist_ok=True)
PAL={
'ivory':'#E9D5AE','floor':'#F2DEBD','tile_alt':'#E9D2B0','stone_shadow':'#C5A983',
'cream':'#FFF2D7','stone':'#D8BE96','stone_dark':'#A68C6F',
'water':'#3BAFC7','water_deep':'#227AAB',
'wood':'#9A5834','wood_dark':'#613D2A','gold':'#D6A33E','gold_light':'#FFE198',
'blue':'#2B75BC','blue_dark':'#19456B','navy':'#22334B',
'black':'#20242A','skin':'#F3BD92','skin_alt':'#E8AC7D','white':'#FFFFFF',
'leaf':'#4F9A43','leaf_light':'#85B44D','leaf_dark':'#286E45',
'flower':'#F29EB8','flower2':'#FFE58C',
'red':'#D95F5A','exit':'#3BC482','cyan':'#62D8D9','grey':'#718798',
'grey_dark':'#4E6378','smoke':'#B7D4DD','shadow':'#9A8A75',
}

def rgba(col):
 h=PAL.get(col,col).lstrip('#');return [int(h[i:i+2],16) for i in (0,2,4)]+[255]

def yrot(mesh, angle):
 if angle: mesh.apply_transform(trimesh.transformations.rotation_matrix(angle,[0,1,0]));return mesh
 return mesh

class Model:
 def __init__(self):self.scene=trimesh.Scene();self.serial=0
 def mesh(self, m, col, name):
  m.visual=trimesh.visual.TextureVisuals(material=PBRMaterial(baseColorFactor=rgba(col),metallicFactor=0.0,roughnessFactor=.85,doubleSided=True,name=col))
  self.scene.add_geometry(m,node_name=f'{name}_{self.serial}',geom_name=f'{name}_{self.serial}')
  self.serial+=1
  return self
 def box(self,name, size, pos, col, rot=0):
  m=trimesh.creation.box(extents=size)
  yrot(m,rot);m.apply_translation(pos);return self.mesh(m,col,name)
 def cyl(self,name,r,h,pos,col,sections=14,rot=0):
  m=trimesh.creation.cylinder(radius=r,height=h,sections=sections)
  # primitive is Z oriented; transform to Y up
  m.apply_transform(trimesh.transformations.rotation_matrix(-math.pi/2,[1,0,0]))
  yrot(m,rot);m.apply_translation(pos);return self.mesh(m,col,name)
 def cone(self,name,r,h,pos,col,sections=12):
  m=trimesh.creation.cone(radius=r,height=h,sections=sections)
  m.apply_transform(trimesh.transformations.rotation_matrix(-math.pi/2,[1,0,0]))
  # center-aligned as if base at pos.y-h/2
  m.apply_translation([pos[0],pos[1]-h/2,pos[2]]);return self.mesh(m,col,name)
 def sph(self,name,r,pos,col,sub=1,scale=(1,1,1)):
  m=trimesh.creation.icosphere(subdivisions=sub,radius=r);m.apply_scale(scale);m.apply_translation(pos);return self.mesh(m,col,name)
 def save(self, filename):
  output=OUT/(filename+'.glb');output.write_bytes(trimesh.exchange.gltf.export_glb(self.scene));print(filename, output.stat().st_size,'bytes',len(self.scene.geometry),'meshes')
  return output

# canonical footprint for floor is 1x1, tile thickness 0.10, surface y=0.10
m=Model();m.box('floor',(1,.10,1),(0,.05,0),'floor');m.box('grout',(.95,.012,.95),(0,.106,0),'tile_alt');m.save('Floor_Tile_A')
m=Model();m.box('floor',(1,.10,1),(0,.05,0),'floor');m.box('grout',(.95,.012,.95),(0,.106,0),'cream');m.save('Floor_Tile_B')
m=Model();m.box('wall',(.98,.85,.22),(0,.425,0),'ivory');m.box('cap',(1.08,.12,.32),(0,.89,0),'cream');m.box('base',(1.02,.12,.28),(0,.08,0),'stone');m.save('Wall_Straight_1m')
m=Model();m.box('core',(.95,.80,.95),(0,.40,0),'ivory');m.box('cap',(1.06,.12,1.06),(0,.87,0),'cream');m.save('Wall_Block_1m')
m=Model();m.box('base',(.43,.18,.43),(0,.09,0),'stone');m.cyl('shaft',.14,.88,(0,.60,0),'ivory',sections=10);m.box('cap',(.53,.16,.53),(0,1.13,0),'cream');m.sph('finial',.17,(0,1.35,0),'cream');m.save('Pillar_Square')
m=Model();m.box('planter',(.84,.37,.84),(0,.185,0),'ivory');m.box('rim',(.92,.09,.92),(0,.37,0),'cream');m.box('soil',(.71,.035,.71),(0,.416,0),'wood_dark');
for dx,dz,r,c in [(-.19,-.10,.24,'leaf'),(.17,.08,.22,'leaf_dark'),(-.02,.17,.23,'leaf_light'),(.20,-.21,.19,'leaf')]:m.sph('bush',r,(dx,.58,dz),c,sub=1,scale=(1,.67,1))
for x,z in [(-.12,.18),(.25,-.01)]:m.sph('flower',.045,(x,.74,z),'flower')
m.save('Obstacle_Planter_1x1')
m=Model();m.box('crate',(.83,.80,.83),(0,.40,0),'wood');m.box('top',(.92,.12,.92),(0,.82,0),'wood_dark');
for d in [-.31,.31]:
 m.box('slat',(.09,.65,.88),(d,.40,0),'wood_dark');m.box('slat',(.88,.65,.09),(0,.40,d),'wood_dark')
m.save('Obstacle_Crate_1x1')
m=Model();m.box('body',(.88,.35,.86),(0,.175,0),'stone');m.box('trim',(.96,.11,.96),(0,.34,0),'cream');m.save('Obstacle_Stone_Block_1x1')
m=Model();m.box('seat',(.96,.11,.37),(0,.44,0),'wood');m.box('back',(.96,.49,.12),(0,.70,-.18),'wood_dark');
for x in [-.36,.36]:m.box('leg',(.10,.40,.11),(x,.20,0),'wood_dark')
m.save('Bench_Short')
m=Model();m.box('stepA',(2,.12,.56),(0,.06,.65),'ivory');m.box('stepB',(2,.24,.56),(0,.12,.05),'stone');m.box('stepC',(2,.36,.56),(0,.18,-.55),'ivory');m.box('trim',(2,.08,.26),(0,.37,-.75),'cream');m.save('Stairs_3Step_2m')
m=Model();m.box('frameLeft',(.18,2,.3),(-.62,1,0),'ivory');m.box('frameRight',(.18,2,.3),(.62,1,0),'ivory');m.box('lintel',(1.5,.26,.4),(0,2.04,0),'cream');m.box('threshold',(1.5,.12,.36),(0,.06,.03),'stone');m.save('Entrance_Arch_Frame')
m=Model();m.box('left',(.57,1.72,.13),(-.30,.87,0),'wood');m.box('right',(.57,1.72,.13),(.30,.87,0),'wood_dark');
for x in [-.3,.3]:m.box('panel',(.42,1.1,.04),(x,.92,.10),'wood');m.sph('handle',.05,(x*.27,.95,.19),'gold_light')
m.save('Door_Double_Wood')
m=Model();m.box('door',(.96,.84,.15),(0,.42,0),'gold');m.box('rail',(.11,.92,.20),(-.52,.46,0),'stone_dark');m.box('rail',(.11,.92,.20),(.52,.46,0),'stone_dark');m.box('stripe',(.90,.12,.02),(0,.45,.091),'wood_dark');m.save('Door_Locked_Path')
m=Model();m.box('rail',(.12,.92,.20),(-.52,.46,0),'stone_dark');m.box('rail',(.12,.92,.20),(.52,.46,0),'stone_dark');m.box('gate',(.90,.84,.12),(-.52,.42,-.42),'gold');m.save('Door_Open_Path')
m=Model();m.cyl('base',.19,.08,(0,.04,0),'stone_dark');m.cyl('ring',.14,.025,(0,.098,0),'exit');m.sph('bright',.10,(0,.125,0),'leaf_light');m.save('Exit_Pad')
m=Model();m.cyl('node',.10,.035,(0,.13,0),'grey_dark');m.cyl('core',.07,.017,(0,.159,0),'cream');m.save('Node_Marker')
m=Model();m.cyl('keyRing',.07,.033,(0,.20,0),'gold',sections=12);m.box('shaft',(.07,.07,.25),(0,.20,.14),'gold');m.box('tooth',(.13,.07,.065),(.03,.20,.27),'gold');m.save('Collectible_Key')
m=Model();m.box('mount',(.24,.18,.15),(0,.91,-.1),'stone_dark');m.box('arm',(.12,.12,.23),(0,.93,.08),'grey_dark');m.box('cam',(.37,.22,.37),(0,.95,.26),'grey');m.box('lens',(.26,.14,.05),(0,.95,.47),'navy');m.sph('glass',.047,(0,.95,.505),'cyan');m.save('Security_Camera')
m=Model();m.box('stand',(.08,.60,.08),(0,.30,0),'wood');m.box('rail',(.36,.09,.12),(0,.60,0),'wood_dark');m.box('lantern',(.24,.29,.22),(0,.72,0),'gold');m.box('glow',(.18,.21,.17),(0,.75,.09),'gold_light');m.cone('roof',.19,.18,(0,.98,0),'navy');m.save('Lantern_Wall')
m=Model();m.box('pole',(.10,1.26,.10),(0,.63,0),'gold');m.box('fabric',(.63,.95,.05),(0,1.0,.10),'blue');m.box('sigil',(.30,.46,.02),(0,1.03,.137),'gold');m.save('Banner_BlueGold')
m=Model();m.box('water',(1,.08,1),(0,.04,0),'water');m.box('wave1',(.72,.02,.10),(0,.09,-.22),'cyan');m.box('wave2',(.50,.02,.09),(.10,.09,.22),'cyan');m.save('Water_Tile')
m=Model();m.box('flowerbed',(.91,.32,.91),(0,.16,0),'stone');
for dx,dz,col in [(-.18,-.19,'leaf'),(.15,-.19,'leaf_dark'),(.17,.18,'leaf_light'),(-.18,.14,'leaf')]:m.sph('leaves',.24,(dx,.43,dz),col,sub=1,scale=(1,.74,1))
for dx,dz,col in [(-.15,-.12,'flower'),(.21,.13,'flower2'),(.13,-.20,'flower')]:m.sph('petal',.06,(dx,.59,dz),col)
m.save('Flowerbed_OutsideBoard')
# simple capsule-inspired, non-rigged proxy characters (animation-ready replacements needed)
m=Model();m.cyl('feet',.18,.065,(0,.033,0),'black');m.box('torso',(.32,.40,.25),(0,.50,0),'black');m.sph('head',.18,(0,.91,0),'skin');m.box('pants',(.26,.33,.22),(0,.25,0),'navy');m.box('jacket',(.35,.25,.29),(0,.64,0),'black');m.box('tie',(.065,.26,.04),(0,.67,.153),'red');m.sph('hair',.18,(0,1.03,-.015),'wood_dark',scale=(1,.33,1));m.save('Character_Spy_PROXY')
m=Model();m.cyl('feet',.18,.065,(0,.033,0),'blue_dark');m.box('pants',(.25,.34,.22),(0,.25,0),'blue_dark');m.box('torso',(.37,.42,.30),(0,.54,0),'blue');m.sph('head',.18,(0,.94,0),'skin_alt');m.box('cap',(.39,.09,.31),(0,1.08,0),'blue_dark');m.box('capbrim',(.36,.04,.15),(0,1.04,.18),'blue_dark');m.save('Character_Guard_PROXY')
m=Model();m.box('mount',(.24,.18,.15),(0,.91,-.1),'stone_dark');m.box('arm',(.12,.12,.23),(0,.93,.08),'grey_dark');m.save('Security_Camera_Mount')
m=Model();m.box('cam',(.37,.22,.37),(0,.95,.26),'grey');m.box('lens',(.26,.14,.05),(0,.95,.47),'navy');m.sph('glass',.047,(0,.95,.505),'cyan');m.save('Security_Camera_Head')
print('\nModel kit complete:',len(list(OUT.glob('*.glb'))),'GLBs in',OUT)
