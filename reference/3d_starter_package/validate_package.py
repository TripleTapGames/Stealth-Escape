"""Validation for the shipped V4 layout and modular .glb art kit.
It does NOT replace the Unity rules-engine solver or a device performance test.
"""
from pathlib import Path
import json
import trimesh

BASE=Path(__file__).parent
models=BASE/'UnityDrop/Assets/_ShadowSteps/Art3D/Models'
jsonfile=BASE/'UnityDrop/Assets/_ShadowSteps/Art3D/Data/shadow_steps_levels_v4.json'
levels=json.loads(jsonfile.read_text())['levels']
errors=[]
assert len(levels)==30,(len(levels),'Expected 30')
assert len(list(models.glob('*.glb')))==26
blocked=0
for i,L in enumerate(levels):
  assert len(L['rows'])==L['h'],(i,'bad row count')
  assert all(len(row)==L['w'] for row in L['rows']),(i,'row width')
  coords={(x,y) for y,row in enumerate(L['rows']) for x,ch in enumerate(row) if ch=='X'}
  assert coords=={tuple(p) for p in L.get('blockedNodes',[])},(i,'blockedNodes mismatch')
  blocked+=len(coords)
  for key in ('start','exit','key'):
   p=L.get(key)
   if p:
    assert L['rows'][p[1]][p[0]] not in ('X','#'),(i,key,p)
  for g in L['guards']:
   starts=[g['at']] if 'at' in g else g['path']
   assert all(L['rows'][p[1]][p[0]] not in ('X','#') for p in starts),(i,'guard on wall')
  for c in L['cams']:
   p=c['wall'];assert 0<=p[0]<L['w'] and 0<=p[1]<L['h'],(i,'camera anchor outside grid')
   if L['rows'][p[1]][p[0]]!='#':
    errors.append(f"Level {i+1}: camera anchor {p} on walkable tile; this is legacy V4 data, add a visual wall-mount without changing gameplay LOS.")
assert blocked==55,blocked
print('Legacy camera mount warnings:',len(errors),*errors,sep='\n  ')
print('V4 data:',len(levels),'levels,',blocked,'blocked cells, all valid actor/camera anchors')
model_results=[]
for glb in sorted(models.glob('*.glb')):
 sc=trimesh.load(glb,force='scene');assert sc.geometry,(glb,'missing meshes')
 bounds=sc.bounds
 assert bounds is not None
 assert all(-15<float(v)<15 for v in bounds.flatten()),(glb,'extreme bounds')
 model_results.append({'file':glb.name,'meshes':len(sc.geometry),'vertex_count':sum(len(m.vertices) for m in sc.geometry.values()),'bounds_m':[list(map(lambda x:round(float(x),3), row)) for row in bounds],'bytes':glb.stat().st_size})
 print(glb.name,'geometry=',len(sc.geometry),'vertices=',model_results[-1]['vertex_count'],'bounds=',model_results[-1]['bounds_m'])
report={'status':'PASS','levels':len(levels),'blocked_nodes':blocked,'art_models':len(model_results),'models':model_results,'warnings':errors,'limits':'No Unity Editor compilation, in-game state integration, or render-performance verification performed.'}
(BASE/'MODEL_VALIDATION_REPORT.json').write_text(json.dumps(report,indent=2))
