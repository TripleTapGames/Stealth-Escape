import json,math,random,collections,heapq,itertools,copy,os,time
from pathlib import Path
ROOT=Path(__file__).parent
D={'N':(0,-1),'E':(1,0),'S':(0,1),'W':(-1,0)}
FACE='NESW'
def add(a,b):return (a[0]+b[0],a[1]+b[1])
def arr(p):return list(p) if p is not None else None

def G(kind, route, face='N', clockwise=True, offset=0):
 o={'id':'', 'kind':kind, 'route':[list(p) for p in route], 'facing':face}
 if kind=='rotating':o['clockwise']=clockwise
 if kind in ('pingPong','loop'):o['offset']=offset
 return o

def C(wall,face, kind='fixed',clockwise=True):
 o={'id':'','wall':list(wall),'mountFace':face,'facing':face,'kind':kind,'clockwise':clockwise}
 return o

def B(code,title,intro,W,H, rowmap, vcols, *, start,exit,key=None,doors=(),guards=(),cams=(),covers=(),walls=(),smoke=0,emp=0, alarm=3,notes=(),cuts=(),remove_edges=()):
 # rowmap: y : [x nodes]; auto horizontal when adjacent by 2; explicit vertical allowed (x,y0,y1) through 1 cell
 ns={(x,y) for y,xs in rowmap.items() for x in xs}
 assert len(ns)<=25,(code,len(ns))
 edges=set()
 def edge(a,b):
  a,b=tuple(a),tuple(b)
  assert a in ns and b in ns,(code,a,b)
  assert (a[0]==b[0] or a[1]==b[1]) and abs(a[0]-b[0])+abs(a[1]-b[1]) in (1,2),(code,a,b)
  edges.add(tuple(sorted((a,b))))
 for y,xs in rowmap.items():
  for x in xs:
   if x+2 in xs:edge((x,y),(x+2,y))
 for v in vcols:
  x,y0,y1=v
  edge((x,y0),(x,y1))
 for a,b in remove_edges:edges.discard(tuple(sorted((tuple(a),tuple(b)))))
 grid=[['_']*W for _ in range(H)]
 for x,y in ns:grid[y][x]='.'
 for a,b in edges:
  x=(a[0]+b[0])//2;y=(a[1]+b[1])//2
  if abs(a[0]-b[0])==2:grid[y][x]='-'
  elif abs(a[1]-b[1])==2:grid[y][x]='|'
  # direct adjacent automatic always
 for x,y in covers:assert grid[y][x]=='_',(code,'badcover',(x,y),grid[y][x]);grid[y][x]='X'
 for x,y in walls:assert grid[y][x]=='_',(code,'badwall',(x,y),grid[y][x]);grid[y][x]='#'
 for ca in cams:
  x,y=ca['wall']
  assert grid[y][x] in ('_','#'),(code,'badcam',(x,y),grid[y][x]);grid[y][x]='#'
 for i,g in enumerate(guards):g['id']='g'+str(i+1)
 for i,c in enumerate(cams):c['id']='cam'+str(i+1)
 x={'base':'LEVEL-30','name':f'NEW_{code.replace("-", "_")}', 'title':title,'intro':intro,
    'rows':[''.join(r) for r in grid],'start':arr(start),'exit':arr(exit),'key':arr(key),'cuts':[[arr(a),arr(b)]for a,b in cuts],
    'doors':[{'a':arr(a),'b':arr(b)}for a,b in doors],'guards':list(guards),'cameras':list(cams),
    'gadgets':{'smoke':smoke,'emp':emp},'alarmBudget':alarm,
    'notes':[str(n) for n in notes]}
 return x

def edges_of(rows):
 H=len(rows);W=len(rows[0]);ns={(x,y)for y in range(H)for x in range(W)if rows[y][x]=='.'}
 ad={p:[] for p in ns}; edges=set()
 for x,y in ns:
  for f,(dx,dy) in D.items():
   for n in (1,2):
    nx=x+dx*n;ny=y+dy*n
    if not(0<=nx<W and 0<=ny<H):break
    c=rows[ny][nx]
    if c=='.':
     if n==1 or (rows[y+dy][x+dx] in ('-','+') if dx else rows[y+dy][x+dx] in ('|','+')):
      ad[(x,y)].append(((nx,ny),f));edges.add(tuple(sorted(((x,y),(nx,ny)))))
     break
    if not(c in ('-','+') if dx else c in ('|','+')):break
 return ns,ad,edges

def validate(x):
 rows=x['rows'];W=len(rows[0]);H=len(rows)
 E=[]
 if not (1<=W<=16 and 1<=H<=16):E.append('size')
 if any(len(r)!=W for r in rows):E.append('unequal rows')
 if any(c not in '.#X-|+_' for r in rows for c in r):E.append('glyph')
 ns,adj,edges=edges_of(rows)
 if len(ns)>25:E.append('too many nodes')
 for p in (x['start'],x['exit'],x['key']):
  if p is not None and tuple(p)not in ns:E.append(f'bad spot {p}')
 for y,r in enumerate(rows):
  for ix,c in enumerate(r):
   if c in ('-','+'):
    if not(ix>0 and ix<W-1 and r[ix-1]=='.' and r[ix+1]=='.'):E.append(f'dangling horizontal {ix,y}')
   if c in ('|','+'):
    if not(y>0 and y<H-1 and rows[y-1][ix]=='.' and rows[y+1][ix]=='.'):E.append(f'dangling vertical {ix,y}')
 door_set={tuple(sorted((tuple(d['a']),tuple(d['b'])))) for d in x['doors']}
 for e in door_set:
  if e not in edges:E.append(f'door missing edge {e}')
 if door_set and x['key'] is None:E.append('door without key')
 for g in x['guards']:
  if tuple(g['route'][0])==tuple(x['start']):E.append('guard on start')
  r=[tuple(p) for p in g['route']]
  if len(set(r))!=len(r):E.append('duplicate route node')
  if g['kind']in('pingPong','loop') and len(r)<2:E.append('short patrol')
  if g['kind']in('static','rotating') and len(r)!=1:E.append('long static')
  for a,b in zip(r,r[1:]):
   if tuple(sorted((a,b))) not in edges or tuple(sorted((a,b))) in door_set:E.append(f'bad route {a,b}')
  if g['kind']=='loop' and tuple(sorted((r[-1],r[0]))) not in edges:E.append('loop not closed')
 for c in x['cameras']:
  (wx,wy)=c['wall'];dx,dy=D[c['mountFace']]
  if rows[wy][wx]!='#' or (wx+dx,wy+dy) not in ns:E.append('camera mount')
 if len(x['guards']):
  period=1
  for g in x['guards']:
   cy=4 if g['kind']=='rotating' else (2*(len(g['route'])-1) if g['kind']=='pingPong' else len(g['route']) if g['kind']=='loop' else 1)
   period=math.lcm(period,cy)
  period=math.lcm(period,4) if any(c['kind']!='fixed' for c in x['cameras']) else period
  for t in range(period):
   pp=[guard_at(g,t)[0]for g in x['guards']]
   if len(pp)!=len(set(pp)):E.append(f'guard overlap t{t}');break
 return E

def guard_at(g,t):
 r=[tuple(p)for p in g['route']];kind=g['kind']
 if kind=='static':return r[0],g['facing']
 if kind=='rotating':return r[0],FACE[(FACE.index(g['facing'])+(t if g.get('clockwise',True)else -t))%4]
 if kind=='pingPong':
  n=len(r); k=(t+g.get('offset',0))%(2*n-2)
  i=k if k<n else 2*n-2-k
  # facing direction of previous move unless t==0
  if t==0:return r[i],g['facing']
  kk=(t-1+g.get('offset',0))%(2*n-2)
  j=kk if kk<n else 2*n-2-kk
  ax,ay=r[j];bx,by=r[i]
  f=next((f for f,(dx,dy)in D.items() if dx*(bx-ax)+dy*(by-ay)>0),g['facing'])
  return r[i],f
 if kind=='loop':
  n=len(r);i=(t+g.get('offset',0))%n;p=r[i]
  if t==0:return p,g['facing']
  q=r[(i-1)%n]
  return p,next((f for f,(dx,dy) in D.items() if dx*(p[0]-q[0])+dy*(p[1]-q[1])>0),g['facing'])
 raise ValueError(kind)

def cam_at(c,t):
 k=c['kind'];f=c['facing']
 if k=='fixed':return f
 if k=='rotating':return FACE[(FACE.index(f)+(t if c.get('clockwise',True)else -t))%4]
 if k=='sweeping':return FACE[(FACE.index(f)+((t//2)%2)*(1 if c.get('clockwise',True)else -1))%4]
 raise ValueError(k)

def blocked(rows,x,y,smoke_pos):
 ix=int(math.floor(x+0.5));iy=int(math.floor(y+0.5))
 return (ix,iy)==smoke_pos or (0<=iy<len(rows) and 0<=ix<len(rows[0]) and rows[iy][ix] in '#X')

def segment_door_hits(a,b,doors,opened):
 # intersection near door centre along sampled LOS, grid edge always horizontal or vertical, simple distance cross
 if opened:return False
 for d in doors:
  aa,bb=tuple(d['a']),tuple(d['b'])
  # edge drawn along axis nodes a and b; door crosses line halfway and blocks any LOS crossing it
  mx=(aa[0]+bb[0])*0.5;my=(aa[1]+bb[1])*0.5
  if aa[0]!=bb[0]:
   if (a[0]-mx)*(b[0]-mx)<0:
    t=(mx-a[0])/(b[0]-a[0]); yy=a[1]+t*(b[1]-a[1]);
    if abs(yy-my)<.48:return True
  elif aa[1]!=bb[1]:
   if (a[1]-my)*(b[1]-my)<0:
    t=(my-a[1])/(b[1]-a[1]);xx=a[0]+t*(b[0]-a[0]);
    if abs(xx-mx)<.48:return True
 return False

def LOS(rows,a,b,smoke_pos=None, doors=(),opened=False):
 if segment_door_hits(a,b,doors,opened):return False
 # sample supercover cells along segment; ignore start and target occupancy unless smoke at target
 steps=int(max(abs(a[0]-b[0]),abs(a[1]-b[1]))*25)+1
 for i in range(1,steps):
  t=i/steps; xx=a[0]+t*(b[0]-a[0]);yy=a[1]+t*(b[1]-a[1])
  ix=int(math.floor(xx+.5));iy=int(math.floor(yy+.5))
  if (ix,iy)==(round(a[0]),round(a[1])):continue
  if (ix,iy)==(round(b[0]),round(b[1])):continue
  if blocked(rows,xx,yy,smoke_pos):return False
 return smoke_pos!=tuple(b)

def guard_sees(level,g,t,target,smoke_pos=None,open_door=False):
 p,f=guard_at(g,t);dx,dy=D[f]
 diff=(target[0]-p[0],target[1]-p[1]);forward=diff[0]*dx+diff[1]*dy;side=diff[0]*dy-diff[1]*dx
 if 0<forward<=2 and side==0:
  return LOS(level['rows'],p,target,smoke_pos,level['doors'],open_door)
 return False

def cam_sees(level,c,t,target,smoke_pos=None,open_door=False):
 wall=tuple(c['wall']);mount=D[c['mountFace']];origin=(wall[0]+mount[0]*.5,wall[1]+mount[1]*.5)
 face=D[cam_at(c,t)];delta=(target[0]-origin[0],target[1]-origin[1]);fw=delta[0]*face[0]+delta[1]*face[1];side=abs(delta[0]*face[1]-delta[1]*face[0]);
 if not 0<fw<=3.001 or side>fw*math.tan(math.radians(35))+1e-7:return False
 # wall origin is between wall and node. Ignore camera mount cell itself via start sample
 return LOS(level['rows'],origin,target,smoke_pos,level['doors'],open_door)

# state = (pos, tmod, key, opened mask, ko mask, smoke_charges, emp_charges, smoke_idx, smoke_duration, emp_idx, emp_duration, alarm_remaining, alarm_triggered)
# alarm_remaining=-1 means untriggered. Consecutive turn camera checks trigger on any camera seen.
# smoke idx -1 absent, camera idx -1 absent.

def solve(level, *, no_knockouts=False,no_alarm=False,no_emp=False,no_smoke=False,require_knockout=False,require_emp=False,require_smoke=False,maxstates=180000):
 rows=level['rows'];ns,adj,edges=edges_of(rows);guards=level['guards'];cams=level['cameras'];doors=level['doors'];guardperiod=1
 for g in guards:guardperiod=math.lcm(guardperiod,(4 if g['kind']=='rotating' else 2*(len(g['route'])-1) if g['kind']=='pingPong' else len(g['route']) if g['kind']=='loop' else 1))
 if any(c['kind']!='fixed' for c in cams):guardperiod=math.lcm(guardperiod,4)
 start=tuple(level['start']);goal=tuple(level['exit']);keyp=tuple(level['key'])if level['key']else None
 a=(-1 if not cams else -1)
 init=(start,0,False,0,0,level['gadgets']['smoke'],level['gadgets']['emp'],-1,0,-1,0,-1,False)
 q=collections.deque([init]);seen={init};prev={};next_action={};limit=False
 def reset_emp(ei,et):return (ei,et) if et>0 else (-1,0)
 def move_actions(s):
  pos,t,key,openmask,ko,smc,emc,smpos,smtime,empidx,emptime,alarm,det=s
  for nxt,face in adj[pos]:
   ei=tuple(sorted((pos,nxt)))
   di=next((i for i,d in enumerate(doors) if tuple(sorted((tuple(d['a']),tuple(d['b']))))==ei),-1)
   if di>=0 and not (openmask&(1<<di)) and not key:continue
   newopen=openmask|(1<<di) if di>=0 else openmask
   newkey=key or nxt==keyp
   kotarget=-1
   valid=True
   for gi,g in enumerate(guards):
    if ko&(1<<gi):continue
    gp,gf=guard_at(g,t)
    if nxt==gp:
     dx,dy=D[gf];v=(pos[0]-gp[0],pos[1]-gp[1]);front=v[0]*dx+v[1]*dy
     if front>0 or no_knockouts:valid=False;break
     kotarget=gi
   if not valid:continue
   newko=ko|((1<<kotarget)if kotarget>=0 else 0)
   newt=t+1;newt_mod=newt%guardperiod
   new_smpos=smpos if smtime>0 else -1
   # smoke acts during this move, check after decrement for expiry/rechecks, best-effort
   for gi,g in enumerate(guards):
    if newko&(1<<gi):continue
    gp,f=guard_at(g,t);gn,fn=guard_at(g,newt)
    if nxt==gn or (pos==gn and nxt==gp):valid=False;break
    smoke_point=None if new_smpos<0 else tuple(nodes_sorted[new_smpos])
    if guard_sees(level,g,t,nxt,smoke_point,bool(newopen)) or guard_sees(level,g,newt,nxt,smoke_point if smtime>1 else None,bool(newopen)):
     valid=False;break
   if not valid:continue
   smoked=tuple(nodes_sorted[new_smpos]) if new_smpos>=0 else None
   detected=False
   for ci,c in enumerate(cams):
    # EMP disabled during next two counted steps but check reactivation at the end of 2nd counted step
    disabled_before=(empidx==ci and emptime>0)
    disabled_after=(empidx==ci and emptime>1)
    if (not disabled_before and cam_sees(level,c,t,nxt,smoked,bool(newopen))) or (not disabled_after and cam_sees(level,c,newt,nxt,smoked if smtime>1 else None,bool(newopen))):
     detected=True;break
   if no_alarm and detected:continue
   if alarm<0:
    newalarm=level['alarmBudget'] if detected else -1
   else:newalarm=alarm-1
   if newalarm<=0 and newalarm!=-1 and nxt!=goal:continue
   # win at last budget allowed
   smrem=max(0,smtime-1);smidx=smpos if smrem>0 else -1
   emrem=max(0,emptime-1);emidx=empidx if emrem>0 else -1
   ns2=(nxt,newt_mod,newkey,newopen,newko,smc,emc,smidx,smrem,emidx,emrem,newalarm,det or detected)
   yield ns2, ('MOVE',nxt,'KO '+guards[kotarget]['id'] if kotarget>=0 else '')
  if smc and not no_smoke:
   for i,target in enumerate(nodes_sorted):
    if target==tuple(nodes_sorted[smpos]) if smpos>=0 else False:continue
    dx=target[0]-pos[0];dy=target[1]-pos[1]
    if max(abs(dx),abs(dy))<=1:
     if not LOS(rows,pos,target,None,doors,bool(openmask)) and target!=pos:continue
    elif (abs(dx)==2 and dy==0 or abs(dy)==2 and dx==0) and any(p==target for p,_ in adj[pos]):
     if not LOS(rows,pos,target,None,doors,bool(openmask)):continue
    else:continue
    yield (pos,t,key,openmask,ko,smc-1,emc,i,2,empidx,emptime,alarm,det),('SMOKE',target,'')
  if emc and cams and not no_emp:
   for i in range(len(cams)):
    yield (pos,t,key,openmask,ko,smc,emc-1,smpos,smtime,i,2,alarm,det),('EMP',cams[i]['id'],'')
 nodes_sorted=sorted(ns)
 # avoid zero-cost BFS error by 0/1 BFS for free gadgets (activation cost 0)
 dist={init:0}; q=collections.deque([init]);expanded=0
 while q:
  state=q.popleft();d=dist[state];expanded+=1
  if expanded>maxstates:return {'solved':False,'reason':'search cap','expanded':expanded}
  pos,t,key,op,ko,smc,emc,smi,smt,emi,emt,alarm,det=state
  if pos==goal and (not require_knockout or ko!=0) and (not require_smoke or smc<level['gadgets']['smoke']) and (not require_emp or emc<level['gadgets']['emp']):
   events=[];curr=state
   while curr!=init:
    events.append(next_action[curr]);curr=prev[curr]
   events.reverse()
   return {'solved':True,'actions':d,'expanded':expanded,'events':events,'knockouts':ko.bit_count(),'triggeredAlarm':det,'smokeUsed':level['gadgets']['smoke']-smc,'empUsed':level['gadgets']['emp']-emc,'route':[list(start)]+[list(event[1])for event in events if event[0]=='MOVE']}
  for state2,event in move_actions(state):
   cost=1 if event[0]=='MOVE' else 0
   alt=d+cost
   if alt<dist.get(state2,10**9):
    dist[state2]=alt;prev[state2]=state;next_action[state2]=event
    if cost:q.append(state2)
    else:q.appendleft(state2)
 return {'solved':False,'reason':'no solution','expanded':expanded}


def V(rowmap, band):
 return [(x,y,y+2)for y,xs in band.items() for x in xs if x in rowmap.get(y,[]) and x in rowmap.get(y+2,[])]

def lvl(code,title,intro,W,H,R,vertical,**kw):
 return B(code,title,intro,W,H,R,V(R,vertical),**kw)

LEVELS=[]
def addlevel(x):LEVELS.append(x)
# 38 — double-flank courtyard, no cameras yet. Two guards watch complementary paths.
R={1:[1,3,5,7,9],3:[1,3,5,7,9],5:[1,3,5,7,9],7:[1,3,5,7,9]}
addlevel(lvl('LEVEL-38','Crossed Blades','The centre guard turns while a patrol closes the upper path.',11,9,R,
 {1:[1,3,7,9],3:[1,3,5,7,9],5:[1,3,5,7,9]},
 start=(1,5),exit=(9,5),guards=[G('rotating',[(5,5)],'W'),G('pingPong',[(3,3),(5,3),(7,3)],'E',offset=0)],
 covers=[(4,4),(6,6)],alarm=4,notes=['Two approaches converge at the same guarded junction; KO option versus an outer loop.','Upper patrol keeps moving every action; centre guard rotates every action.'] ))

# 39 — first late-game camera + two guards; exits in cam watched eastern wing.
R={1:[1,3,5,7,9,11],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11],7:[1,3,5,9,11]}
addlevel(lvl('LEVEL-39','Eyes on the Junction','A fixed camera watches the eastern junction; guards control the centre.',13,9,R,
 {1:[1,3,7,9],3:[1,3,5,7,9,11],5:[1,3,5,9,11]},
 start=(1,5),exit=(11,5),guards=[G('static',[(7,5)],'W'),G('pingPong',[(3,1),(5,1),(7,1)],'E',offset=0)],
 cams=[C((12,3),'W')],covers=[(6,4),(8,6)],alarm=3,
 notes=['A camera watches the right-hand junction; the lower crossing is longer but less exposed.','An upper patrol creates a timing problem if players take the high route.']))

# 40 — first sweeping camera, only two guards.
R={1:[1,3,5,9,11],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11],7:[1,3,5,7,9,11]}
addlevel(lvl('LEVEL-40','Sweep and Strike','Cross the centre while the sweeping lens and guard turn away.',13,9,R,
 {1:[1,3,9,11],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11]},
 start=(1,7),exit=(11,3),guards=[G('rotating',[(5,5)],'W',clockwise=False),G('pingPong',[(3,3),(5,3),(7,3)],'E',offset=2)],
 cams=[C((9,0),'S','sweeping',clockwise=False)],covers=[(6,4),(8,6)],alarm=3,
 notes=['Sweep phases repeat every four actions, but the patrol may be on a different cycle.','Players can flank the centre guard or take a longer southern route.']))

# 41 — key forces lower detour while the upper camera watches timing.
R={1:[1,3,5,7,9,11],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11],7:[1,3,5,9,11],9:[3,5]}
addlevel(lvl('LEVEL-41','Key Under Watch','Collect the lower key, then pass the rotating guard to unlock exit.',13,11,R,
 {1:[1,3,5,9,11],3:[1,3,5,7,9,11],5:[1,3,5,9,11],7:[3,5]},
 start=(1,5),exit=(11,5),key=(3,9),doors=[((9,5),(11,5))],
 guards=[G('rotating',[(5,5)],'E'),G('pingPong',[(7,3),(9,3),(11,3)],'W',offset=1)],
 cams=[C((5,0),'S','rotating',clockwise=True)],covers=[(6,6),(8,4)],alarm=4,
 notes=['The key is mandatory and off the direct route; unlock the exit edge only after collecting it.','The rotating camera complicates the return from the key branch.']))

# 42 — add third guard, remote sentry on the upper route.
R={1:[3,5,7,9],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11],7:[1,3,5,7,9,11],9:[3,5,9]}
addlevel(lvl('LEVEL-42','Three-Way Watch','Three guards divide the routes; find a flank at the junction.',13,11,R,
 {1:[3,5,7,9],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11],7:[3,5,9]},
 start=(1,7),exit=(11,3),guards=[G('static',[(5,5)],'W'),G('rotating',[(9,5)],'N',clockwise=False),G('pingPong',[(3,1),(5,1),(7,1)],'E',offset=0)],
 cams=[C((12,7),'W')],covers=[(6,4),(8,6),(10,8)],alarm=4,
 notes=['First level with three guards alongside a camera.','Sentries occupy distinct areas so each influences a different route.']))

# 43 — use single smoke placement to cross a watched node, more guards.
R={1:[1,3,5,7,9],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11],7:[1,3,5,7,9,11],9:[3,5]}
addlevel(lvl('LEVEL-43','Smoke at the Fork','Spend one Smoke charge to hide a dangerous crossing.',13,11,R,
 {1:[1,3,5,7,9],3:[1,3,5,7,9],5:[1,3,5,7,9,11],7:[3,5]},
 start=(1,7),exit=(11,3),guards=[G('static',[(5,5)],'W'),G('rotating',[(7,3)],'S'),G('pingPong',[(9,7),(11,7)],'W',offset=0)],
 cams=[C((12,5),'W')],covers=[(6,6),(8,4)],smoke=1,alarm=3,
 notes=['Smoke can block guard or camera LOS for two counted moves; consider when to place it.','Do not claim Smoke mandatory until the C# solver confirms.']))

# 44 — two cams but spread among three guards.
R={1:[1,3,5,7,9,11],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11],7:[1,3,5,7,9,11],9:[9]}
addlevel(lvl('LEVEL-44','Overlapping Lenses','Two cameras watch different exits from a three-guard courtyard.',13,11,R,
 {1:[1,3,5,9,11],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11],7:[9]},
 start=(1,7),exit=(11,3),guards=[G('static',[(5,5)],'W'),G('rotating',[(7,3)],'N'),G('pingPong',[(3,1),(5,1),(7,1)],'E',offset=0)],
 cams=[C((12,5),'W'),C((9,0),'S','sweeping',clockwise=False)],covers=[(6,4),(8,6)],alarm=3,
 notes=['Camera angles overlap near one of the central junctions.','Choose whether to take a lockdown dash or detour around surveillance.']))

# 45 — horizontal camera pan right wing, key is in bottom left.
R={1:[3,5,7,9,11],3:[1,3,5,7,9,11,13],5:[1,3,5,7,9,11,13],7:[1,3,9,11],9:[3,9]}
addlevel(lvl('LEVEL-45','Gatehouse Circuit','Get the key, then flank the guards in the far right wing.',15,11,R,
 {1:[3,5,9,11],3:[1,3,5,7,9,11,13],5:[1,3,9,11,13],7:[3,9]},
 start=(1,5),exit=(13,5),key=(3,9),doors=[((11,5),(13,5))],
 guards=[G('static',[(5,5)],'W'),G('rotating',[(9,3)],'E'),G('pingPong',[(3,1),(5,1),(7,1)],'W',offset=1)],
 cams=[C((14,3),'W'),C((9,0),'S','sweeping',clockwise=True)],covers=[(6,4),(10,6),(4,8)],alarm=3,
 notes=['First 15-column level: camera follows player horizontally; pan without consuming actions.','The far-right locked exit cannot be reached until the key branch is visited.']))

# 46 — EMP choice at camera gate.
R={1:[1,3,5,7,9,11],3:[1,3,5,7,9,11,13],5:[1,3,5,7,9,11,13],7:[1,3,7,11],9:[7]}
addlevel(lvl('LEVEL-46','Power Cut','Time an EMP to disable a camera guarding the last crossing.',15,11,R,
 {1:[1,3,5,7,9,11],3:[1,3,5,7,9,11,13],5:[1,3,7,11],7:[7]},
 start=(1,7),exit=(13,3),guards=[G('rotating',[(5,5)],'W'),G('static',[(9,3)],'S'),G('pingPong',[(3,1),(5,1),(7,1)],'E',offset=0)],
 cams=[C((14,5),'W'),C((11,0),'S','sweeping',clockwise=False)],covers=[(6,4),(8,6)],emp=1,alarm=2,
 notes=['An EMP can silence exactly one camera for two counted moves.','EMP effects do not change guard schedules or turn count; use timing at the far side.']))

# 47 — 3 guards, 2 cams, gadgets & key; playable wide.
R={1:[3,5,7,9,11],3:[1,3,5,7,9,11,13],5:[1,3,5,7,9,11,13],7:[1,3,5,9,11],9:[5]}
addlevel(lvl('LEVEL-47','Broken Line of Sight','Use cover, a key and gadgets to reach the eastern door.',15,11,R,
 {1:[3,5,7,9,11],3:[1,3,5,7,9,11,13],5:[1,3,5,9,11],7:[5]},
 start=(1,7),exit=(13,3),key=(5,9),doors=[((11,3),(13,3))],
 guards=[G('static',[(5,5)],'W'),G('rotating',[(9,5)],'E',clockwise=False),G('pingPong',[(3,1),(5,1),(7,1)],'W',offset=2)],
 cams=[C((14,5),'W'),C((9,0),'S','rotating',clockwise=True)],covers=[(6,6),(8,4)],smoke=1,emp=1,alarm=2,
 notes=['The key detour requires leaving the shortest apparent corridor.','Smoke and EMP are available as different tools; assess their real necessity via solver.']))

# 48 — 4 guards, 2 cams in taller circuit.
R={1:[3,5,7],3:[1,3,5,7,9,11,13],5:[1,3,5,7,9,11,13],7:[1,3,5,9,11],9:[3,9],11:[9]}
addlevel(lvl('LEVEL-48','The Four Sentinels','Four guards guard separate loops; use the lower circuit to flank.',15,13,R,
 {1:[3,5,7],3:[1,3,5,7,9,11,13],5:[1,3,5,9,11],7:[3,9],9:[9]},
 start=(1,7),exit=(13,3),guards=[G('static',[(5,5)],'W'),G('rotating',[(9,5)],'W'),G('pingPong',[(3,3),(5,3),(7,3)],'E',offset=1),G('static',[(9,9)],'E')],
 cams=[C((14,5),'W'),C((3,0),'S','sweeping',clockwise=True)],covers=[(6,6),(10,6),(4,8)],smoke=1,alarm=3,
 notes=['Four separate guards control distinct parts of the board.','The lower ring creates an alternate flanking route, not just a longer straight path.']))

# 49 — 4 guards, 3 cams + key EMP, different shape.
R={1:[1,3,5,7],3:[1,3,5,7,9,11],5:[1,3,5,7,9,11,13],7:[3,5,7,9,11],9:[3,9],11:[9]}
addlevel(lvl('LEVEL-49','Three Eyes, One Key','Three cameras divide the level; get the key before lockdown.',15,13,R,
 {1:[1,3,5,7],3:[1,3,5,7,9,11],5:[3,5,7,9,11],7:[3,9],9:[9]},
 start=(1,5),exit=(13,5),key=(9,11),doors=[((11,5),(13,5))],
 guards=[G('static',[(5,5)],'W'),G('rotating',[(9,5)],'S'),G('pingPong',[(1,3),(3,3),(5,3)],'E',offset=0),G('static',[(3,9)],'E')],
 cams=[C((14,5),'W'),C((3,0),'S','rotating'),C((7,0),'S','sweeping',clockwise=False)],
 covers=[(6,6),(10,6),(4,8)],emp=1,alarm=3,
 notes=['Three cameras cover independent zones while four guards control the route network.','Unlock the only exit door using the key from the southern branch.']))

# 50 — final, 4 guards + 3 cams + key + both gadgets.
R={1:[3,5,7,9],3:[1,3,5,7,9,11,13],5:[1,3,5,7,9,11,13],7:[1,3,5,9,11],9:[3],11:[3]}
addlevel(lvl('LEVEL-50','The Last Exit','Outthink four guards and three cameras to open the final door.',15,13,R,
 {1:[3,5,7,9],3:[1,3,5,7,9,11,13],5:[1,3,5,9,11],7:[3],9:[3]},
 start=(1,5),exit=(13,5),key=(3,11),doors=[((11,5),(13,5))],
 guards=[G('static',[(5,5)],'W'),G('rotating',[(9,5)],'N'),G('pingPong',[(3,3),(5,3),(7,3)],'E',offset=2),G('static',[(11,7)],'W')],
 cams=[C((14,3),'W'),C((5,0),'S','sweeping',clockwise=True),C((0,3),'E','rotating',clockwise=False)],
 covers=[(6,6),(8,4),(10,6)],smoke=1,emp=1,alarm=3,
 notes=['Final multi-mechanic test: required key, guards, 70-degree cameras, Smoke/EMP choices.','Reach the lower key branch, then navigate back to a protected exit.']) )

if __name__=='__main__':
 for L in LEVELS:
  errs=validate(L)
  ns,adj,e=edges_of(L['rows'])
  # directed reachability from start ignoring guards/doors
  q=[tuple(L['start'])];visited=set(q)
  for p in q:
   for n,d in adj[p]:
    if n not in visited:visited.add(n);q.append(n)
  print(L['name'],L['title'],f'{len(L["rows"][0])}x{len(L["rows"])}',f'nodes {len(ns)}', 'guards',len(L['guards']),'cams',len(L['cameras']),'reach',len(visited),'ERR',errs)
  if errs:continue
  r=solve(L,maxstates=150000)
  print('   ',{k:v for k,v in r.items() if k!='events' and k!='route'})
  if r['solved']:
   print('    route',r['route'])
   out=ROOT/(L['name']+'.json');out.write_text(json.dumps([L],indent=2))
 (ROOT/'Levels_38_to_50_Candidates.json').write_text(json.dumps(LEVELS,indent=2))
