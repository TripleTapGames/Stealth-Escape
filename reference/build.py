import json,random,collections,math,os
from pathlib import Path
OUT=Path('/mnt/data/shadow_steps')
DIRS=[(0,-1),(1,0),(0,1),(-1,0)] # NESW
ACTIONS=['N','E','S','W','WAIT','SMOKE','EMP']
def in_bounds(x,y,w,h):return 0<=x<w and 0<=y<h
def cone_visible(g,p,blocked):
    gx,gy,d,r=g; px,py=p; dx=px-gx;dy=py-gy
    if (px,py)==(gx,gy):return True
    if d==0: forward=-dy;side=dx
    elif d==1:forward=dx;side=dy
    elif d==2:forward=dy;side=dx
    else:forward=-dx;side=-dy
    if forward<1 or forward>r or abs(side)>forward:return False
    # LOS via supercover sampling: conservative check all touching cells, except endpoints
    steps=max(abs(dx),abs(dy))*12
    for i in range(1,steps):
        t=i/steps;xx=gx+0.5+dx*t;yy=gy+0.5+dy*t
        cx=int(math.floor(xx));cy=int(math.floor(yy))
        if (cx,cy)!=(gx,gy) and (cx,cy)!=(px,py) and (cx,cy) in blocked:return False
    return True

def guard_at(g,t,freeze=False):
    route=g['route']; mode=g['mode']; step=g.get('step',1)
    q= t//step
    if mode=='fixed': return (*route[0],g['facing'],g['range'])
    if mode=='rotate':return (*route[0],(g['facing']+q*g.get('rotation',1))%4,g['range'])
    if mode in ('patrol','patrol_rotate','loop'):
        if mode=='loop':idx=q%len(route)
        else:idx=(list(range(len(route)))+list(range(len(route)-2,0,-1)))[q%(2*len(route)-2)] if len(route)>1 else 0
        x,y=route[idx]
        if mode=='patrol_rotate': d=(g['facing']+q*g.get('rotation',1))%4
        else:
            if q==0:d=g['facing']
            else:
                prev=guard_at({**g,'mode':'loop' if mode=='loop' else 'patrol'},max(t-step,0)); dd=(x-prev[0],y-prev[1]);d=DIRS.index(dd) if dd in DIRS else g['facing']
        return x,y,d,g['range']
    raise ValueError(mode)

def period(level):
    import math
    p=1
    for g in level['guards']:
        n=len(g['route']); m=g['mode'];s=g.get('step',1)
        if m=='fixed': z=1
        elif m=='rotate': z=4*s//math.gcd(4,abs(g.get('rotation',1)))
        elif m=='loop':z=n*s
        else:z=max(1,2*n-2)*s
        if m=='patrol_rotate':z=math.lcm(z,4*s//math.gcd(4,abs(g.get('rotation',1))))
        p=math.lcm(p,z)
    return p

def solve(L,max_states=250000):
    w,h=L['size'];walls={tuple(v) for v in L['walls']};key=tuple(L['key']) if L.get('key') else None;door=tuple(L['door']) if L.get('door') else None;exit=tuple(L['exit']);start=tuple(L['start']);P=period(L)
    # state=(pos,turnmod,has_key,smoke_remaining,emp_remaining,smoke_active,emp_active)
    init=(start,0,False,L['smoke'],L['emp'],0,0)
    q=collections.deque([init]);back={init:None};act={}
    while q and len(back)<max_states:
        s=q.popleft();pos,t,k,sm,em,cloud,freeze=s
        if pos==exit and (not door or k):
            path=[]
            while back[s] is not None:path.append(act[s]);s=back[s]
            return list(reversed(path)),len(back)
        for a in ACTIONS:
            if a=='SMOKE' and sm==0:continue
            if a=='EMP' and em==0:continue
            x,y=pos;nx,ny=x,y
            if a in 'NESW' and len(a)==1:dx,dy=DIRS['NESW'.index(a)];nx+=dx;ny+=dy
            if not in_bounds(nx,ny,w,h) or (nx,ny) in walls:continue
            nk=k or (key is not None and (nx,ny)==key)
            if door and (nx,ny)==door and not nk:continue
            # After action: enemy advances unless EMP. Guard state is phase t+1 otherwise t (EMP pauses the whole cycle)
            nt=(t if a=='EMP' else (t+1)%P)
            current=[guard_at(g,nt) for g in L['guards']]
            previous=[guard_at(g,t) for g in L['guards']]
            if any((gx,gy)==(nx,ny) for gx,gy,_,_ in current):continue
            if any((nx,ny)==(old[0],old[1]) and pos==(new[0],new[1]) for old,new in zip(previous,current)):continue
            ncloud=2 if a=='SMOKE' else max(0,cloud-1)
            if ncloud==0:
                blocked=walls | ({door} if door and not nk else set())
                if any(cone_visible(g,(nx,ny),blocked) for g in current):continue
            ns=((nx,ny),nt,nk,sm-(a=='SMOKE'),em-(a=='EMP'),ncloud,0)
            if ns not in back:back[ns]=s;act[ns]=a;q.append(ns)
    return None,len(back)

# 20 authored configurations, deterministic generation searches for nontrivial playable wall sets.
# Each level has deliberate mechanics + obstacle density; accepted levels are frozen in levels.json.
specs=[]
for i in range(20):
    tier=i//4; w=5+tier//2;h=5+tier//2
    modes=[['fixed'],['rotate'],['patrol'],['patrol_rotate'],['fixed','rotate'],['patrol','rotate'],['loop'],['patrol_rotate','fixed'],['rotate','rotate'],['patrol','patrol'],['patrol_rotate','rotate'],['loop','fixed'],['fixed','patrol','rotate'],['loop','rotate'],['patrol_rotate','patrol'],['fixed','rotate','loop'],['patrol_rotate','rotate','fixed'],['patrol','loop','rotate'],['loop','patrol_rotate','fixed'],['patrol_rotate','loop','rotate']][i]
    specs.append(dict(id=i+1,size=[w,h],modes=modes,smoke=1 if i in [10,13,16,18,19] else 0,emp=1 if i in [11,14,17,19] else 0,keydoor=i in [8,9,12,15,18,19]))

def free_path_cells(L):
    w,h=L['size'];blocked=set(map(tuple,L['walls'])); start=tuple(L['start']);end=tuple(L['exit']);q=collections.deque([start]);seen={start}
    while q:
        p=q.popleft()
        if p==end:return True
        for dx,dy in DIRS:
            v=(p[0]+dx,p[1]+dy)
            if in_bounds(*v,w,h) and v not in blocked and v not in seen:seen.add(v);q.append(v)
    return False

def candidate(spec,seed):
    r=random.Random(seed);w,h=spec['size'];start=[0,h-1];exit=[w-1,0]
    free=[(x,y) for y in range(h) for x in range(w) if [x,y] not in (start,exit)]
    walls=set(r.sample(free,r.randint(2,5+spec['id']//5)))
    L=dict(id=spec['id'],name=['First Watch','Clockwork Eye','The Corridor','Double Rhythm','Two Watchers','Crossing Paths','Square Route','Side Step','The Brass Key','Locked Timing','Smoke Passage','Power Cut','Guarded Vault','Smoke and Mirrors','Blackout Window','The Service Door','Three Blind Angles','Shift Change','High Security','Final Extraction'][spec['id']-1],size=[w,h],start=start,exit=exit,walls=[list(p) for p in sorted(walls)],guards=[],smoke=spec['smoke'],emp=spec['emp'],key=None,door=None)
    if not free_path_cells(L):return None
    possible=[p for p in free if p not in walls and p!=tuple(start) and p!=tuple(exit)]
    if len(possible)<6:return None
    if spec['keydoor']:
        k=r.choice(possible);poss2=[p for p in possible if p!=k];d=r.choice(poss2)
        L['key']=list(k);L['door']=list(d)
    for mode in spec['modes']:
        available=[p for p in possible if p not in [tuple(g['route'][0]) for g in L['guards']] and p!=tuple(L['key'] or [-1,-1]) and p!=tuple(L['door'] or [-1,-1])]
        if not available:return None
        first=r.choice(available);route=[list(first)]
        if mode in ('patrol','patrol_rotate','loop'):
            if mode=='loop':
                squares=[[(first[0],first[1]),(first[0]+dx,first[1]),(first[0]+dx,first[1]+dy),(first[0],first[1]+dy)] for dx in (-1,1) for dy in (-1,1)]
                squares=[sq for sq in squares if all(in_bounds(*p,w,h) and p not in walls and p not in (tuple(start),tuple(exit)) for p in sq)]
                if not squares:return None
                route=[list(p) for p in r.choice(squares)]
            else:
                dx,dy=r.choice(DIRS); options=[]
                for sign in (1,-1):
                    line=[first]
                    for n in range(1,4):
                        nxt=(first[0]+dx*n*sign,first[1]+dy*n*sign)
                        if not in_bounds(*nxt,w,h) or nxt in walls or nxt in (tuple(start),tuple(exit)):break
                        line.append(nxt)
                    if len(line)>1:options.append(line)
                if not options:return None
                route=[list(p) for p in r.choice(options)]
        L['guards'].append(dict(mode=mode,route=route,facing=r.randrange(4),range=1 if spec['id']<=5 else (2 if spec['id']<14 else 3),step=1,rotation=r.choice([1,-1])))
    return L

levels=[];reports=[]
for s in specs:
    best=None
    for j in range(2500):
        L=candidate(s, s['id']*100003+j)
        if not L:continue
        path,states=solve(L)
        if path is None:continue
        steps=len(path)
        # avoid levels being trivially solved; minimum shortest solution relative to size
        minlen=6 if s['id']>=9 else 5
        if steps<minlen:continue
        required_gadget=(s['smoke'] or s['emp']) and s['id'] in [13,14,17,19,20]
        if required_gadget and not any(a in ('SMOKE','EMP') for a in path):continue
        # keep bounded complexity and good narrative pacing
        if steps>25:continue
        best=(L,path,states);break
    if best is None:
        print('FAIL',s['id']);continue
    L,path,n=best;levels.append(L);reports.append(dict(level=L['id'],name=L['name'],moves=len(path),actions=path,states_checked=n,period=period(L)))
    print(L['id'],L['name'],len(path),' '.join(path),'states',n)
(OUT/'levels.json').write_text(json.dumps({'format_version':1,'coordinate_system':'origin top-left, x right, y down','levels':levels},indent=2))
(OUT/'validation.json').write_text(json.dumps(reports,indent=2))
