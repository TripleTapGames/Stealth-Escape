import build_levels as m
from copy import deepcopy

def cut(L,a,b):
 a,b=tuple(a),tuple(b)
 assert a[0]==b[0] or a[1]==b[1],(L['name'],a,b)
 assert abs(a[0]-b[0])+abs(a[1]-b[1])==2
 mx=(a[0]+b[0])//2;my=(a[1]+b[1])//2
 rows=[list(r)for r in L['rows']]
 assert rows[my][mx] in ('-','|'),(L['name'],(a,b),rows[my][mx])
 rows[my][mx]='_';L['rows']=[''.join(r)for r in rows]

def cut_possible(L,a,b):
 pair={tuple(a),tuple(b)}
 for g in L['guards']:
  route=[tuple(p) for p in g['route']]
  if any(set((p,q))==pair for p,q in zip(route,route[1:])):
   return
  if g['kind']=='loop' and set((route[-1],route[0]))==pair:
   return
 try:cut(L,a,b)
 except AssertionError:pass

def isolate_exit(L):
 if not L['doors']:return
 goal=tuple(L['exit']);doorEdges={tuple(sorted((tuple(d['a']),tuple(d['b']))))for d in L['doors']}
 _,ad,_=m.edges_of(L['rows'])
 for n,d in list(ad[goal]):
  if tuple(sorted((goal,n))) not in doorEdges:
   if abs(goal[0]-n[0])+abs(goal[1]-n[1])==2:cut(L,goal,n)
   else:L['cuts'].append([list(goal),list(n)])

# 1. Exact locked exit semantics: the locked entrance must be the ONLY entry into exit.
for L in m.LEVELS:isolate_exit(L)

# 2. Rework specific bridges for tactical choke points. Keep other loops interesting.
for L in m.LEVELS:
 n=int(L['name'].split('_')[-1])
 # scattered cuts: prevent unchallenged sprint along y3, give lower loops strategic purpose
 if n in (39,40,42,44,45,46,47,48,49,50):
  cut_possible(L,(7,3),(9,3))
 if n in (42,43,44,45,47,48,49,50):
  cut_possible(L,(3,3),(5,3))
 if n in (43,45,46,47,48,49,50):
  cut_possible(L,(9,7),(11,7))
 if n in (45,46,47,49,50):
  cut_possible(L,(1,3),(3,3))
 if n in (49,):
  cut_possible(L,(7,5),(9,5))

# 3. Make the key branch, gadget barriers, and final mechanics intentional.
def cam_replace(L,index,pos,f,kind='fixed',clockwise=True):
 old=L['cameras'][index]
 L['cameras'][index]={'id':old['id'],'wall':list(pos),'mountFace':f,'facing':f,'kind':kind,'clockwise':clockwise}
 rows=[list(r)for r in L['rows']]
 xx,yy=pos
 assert rows[yy][xx] in ('_','#'),(L['name'],'mount has obstruction',pos,rows[yy][xx])
 rows[yy][xx]='#';L['rows']=[''.join(r) for r in rows]

# A stronger strategic decision: a no-knockout bypass now takes more moves.
L42=m.LEVELS[42-38]
cut_possible(L42,(7,7),(9,7))
L44=m.LEVELS[44-38]
cut_possible(L44,(5,7),(7,7))

# First security encounter: camera timing can be avoided by choosing the right phase.
L39=m.LEVELS[39-38]
cam_replace(L39,0,(12,5),'W','sweeping',False)
L39['intro']='Time the eastern camera while the guards protect the junction.'

# Lower guard looks toward the actual lower loop instead of empty floor.
L48=m.LEVELS[48-38]
L48['guards'][3]['facing']='N'
L49=m.LEVELS[49-38]
L49['guards'][3]['facing']='N'

L45=m.LEVELS[45-38]
L45['guards'][0]['facing']='N'  # flankable from the key return

# Level 43: guard on isolated lookout watches only crossing between two courtyard halves.
L43=m.LEVELS[43-38]
L43['guards'][1]={'id':'g2','kind':'static','route':[[7,3]],'facing':'S'}
L43['guards'][2]={'id':'g3','kind':'static','route':[[11,5]],'facing':'W'}
for a,b in [((5,1),(7,1)),((5,3),(7,3)),((7,3),(9,3)),((7,1),(7,3)),((7,3),(7,5)),((5,7),(7,7))]:cut_possible(L43,a,b)
L43['notes']+=['The isolated static guard at (7,3) cannot be knocked out: no walkable edge leads into that node.','Crossing (7,5) from the left requires placing Smoke on that node unless another genuine route exists; verify in C# solver.']

# Level 46: an EMP should silence a camera on the only centre crossing; level has 2 cams.
L46=m.LEVELS[46-38]
L46['guards'][2]['route']=[[1,1],[3,1],[5,1]]
L46['guards'][2]['facing']='E'
for a,b in [((5,1),(7,1)),((5,3),(7,3)),((7,3),(7,5))]:cut_possible(L46,a,b)
cam_replace(L46,0,(7,4),'S')
L46['alarmBudget']=1
L46['guards'][1]['facing']='N'
L46['notes']+=['The first camera is physically wall-mounted at (7,4) and watches (7,5), a bridge node.','With a one-action alarm, crossing this camera without EMP should fail; verify the restriction against bypass routes.']

# Level 50: protected smoke sentry on west bridge; separate EMP-controlled camera on east bridge.
L50=m.LEVELS[50-38]
L50['guards'][0]={'id':'g1','kind':'static','route':[[5,3]],'facing':'S'}
L50['guards'][2]={'id':'g3','kind':'pingPong','route':[[9,3],[11,3],[13,3]],'facing':'E','offset':0}
for a,b in [((3,1),(5,1)),((3,3),(5,3)),((5,3),(7,3)),((5,1),(5,3)),((5,3),(5,5)),((3,7),(5,7)),((7,1),(9,1)),((7,3),(9,3)),((9,3),(9,5))]:cut_possible(L50,a,b)
# Ensure the western guarded bridge cannot be bypassed via upper rows.
cam_replace(L50,0,(9,4),'S')
L50['alarmBudget']=1
L50['notes']+=['Sentry g1 is geometrically inaccessible at (5,3) and watches the sole west-to-east bridge (5,5). Smoke that bridge to pass.','Camera cam1 at (9,4) controls the next bridge (9,5). EMP is intended to allow crossing it before lockdown.','All alternatives must be checked by the Unity solver; do not claim mandatory gadgets until verified.']

# Final handcrafted bottlenecks: remove uninteresting straight shortcuts only where
# the local solver still finds genuine alternatives and guard/gadget dependencies remain.
for number,a,b in [
 (39,(9,5),(11,5)),
 (41,(7,5),(9,5)),
 (42,(3,7),(5,7)),
 (44,(5,5),(7,5)),
 (45,(9,5),(11,5)),
 (46,(3,5),(5,5)),
 (50,(3,5),(3,7))
]:
 cut_possible(m.LEVELS[number-38],a,b)

if __name__=='__main__':
 for L in m.LEVELS:
  errs=m.validate(L)
  noK=m.solve(L,no_knockouts=True,maxstates=60000)if not errs else {'solved':False}
  r=m.solve(L,maxstates=100000)if not errs else {'solved':False}
  nogad=m.solve(L,no_emp=True,no_smoke=True,maxstates=40000)if not errs else {'solved':False}
  print(L['name'], 'valid',not errs,'d',r.get('actions','-'),'KO',r.get('knockouts','-'),'ghost',not r.get('triggeredAlarm',True),'noKO',noK.get('actions','-')if noK.get('solved')else 'X','noGad',nogad.get('actions','-')if nogad.get('solved')else 'X', 'issues',errs,flush=True)
  if not r.get('solved'):print('     FAIL',r,flush=True)
