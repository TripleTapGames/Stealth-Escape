import json,os,sys,copy,math,zipfile,collections
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import tune_levels as t
import build_levels as m

ROOT=Path(__file__).parent
OUT=ROOT/'Individual_Levels';OUT.mkdir(exist_ok=True)
PREF=Path('/mnt/data')
FONT='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf';BOLD='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
# Don't silently mutate LEVEL-36 and LEVEL-37, import unchanged.
old=[]
for f in ['ShadowSteps_LEVEL-36_Garden_Gambit.json','ShadowSteps_LEVEL-37_The_Locked_Flank_V2.json']:
 data=json.loads((PREF/f).read_text());assert len(data)==1;old+=data

report=[];sollist={}
for L in m.LEVELS:
 i=int(L['name'].rsplit('_',1)[1]);L['name']=f'L{i}_'+L['title'].upper().replace(' ','_').replace(',','')
 n=len(L['rows']);w=len(L['rows'][0]);
 assert w<=16 and n<=16
 errors=m.validate(L)
 assert not errors, (L['name'],errors)
 start=tuple(L['start']);assert not any(m.guard_sees(L,g,0,start)for g in L['guards'])
 assert not any(m.cam_sees(L,c,0,start)for c in L['cameras'])
 r=m.solve(L,maxstates=180000)
 assert r['solved'],(L['name'],r)
 noKO=m.solve(L,no_knockouts=True,maxstates=110000)
 noghost=m.solve(L,no_alarm=True,maxstates=110000)
 nosmoke=m.solve(L,no_smoke=True,maxstates=110000) if L['gadgets']['smoke'] else {'solved':True}
 noemp=m.solve(L,no_emp=True,maxstates=110000) if L['gadgets']['emp'] else {'solved':True}
 noGad=m.solve(L,no_emp=True,no_smoke=True,maxstates=110000)if L['gadgets']['emp'] or L['gadgets']['smoke'] else {'solved':True}
 noKey=copy.deepcopy(L)
 if L['key']:
  noKey['key']=None
  withnok=m.solve(noKey,maxstates=80000)
  assert not withnok['solved'],('key optional!',L['name'],withnok)
 # Capture mechanics-sensitive claims only when proven with this provisional solver
 if i in (43,50):assert not nosmoke['solved'],('smoke not required',L['name'])
 if i in (46,50):assert not noemp['solved'],('emp not required',L['name'])
 ns,adj,ed=m.edges_of(L['rows'])
 notes=L['notes']
 notes.append(f'Local Python search: shortest demonstrated winning route {r["actions"]} counted actions; {r["knockouts"]} KOs; Smoke activations {r["smokeUsed"]}; EMP activations {r["empUsed"]}; alarm triggered: {r["triggeredAlarm"]}. This is provisional, NOT the Unity C# official par.')
 notes.append('Local sample move sequence: '+str(r['route']))
 notes.append(f'Independent checks: no-KO shortest={noKO.get("actions") if noKO["solved"] else "no solution"}; Ghost shortest={noghost.get("actions") if noghost["solved"] else "no solution"}; no-Smoke solvable={nosmoke["solved"]}; no-EMP solvable={noemp["solved"]}.')
 # `ghostOffered` intentionally not included in schema: C# solver/scoring needs to set this
 L['base']='LEVEL-30' # only template, never overwrite
 f=OUT/f'LEVEL-{i}_{L["title"].replace(" ","_")}.json';f.write_text(json.dumps([L],indent=2),encoding='utf-8')
 report.append({'level':i,'title':L['title'],'width':w,'height':n,'nodes':len(ns),'edges':len(ed),'guards':len(L['guards']),'cameras':len(L['cameras']),'key':bool(L['key']),'smokeCharges':L['gadgets']['smoke'],'empCharges':L['gadgets']['emp'],'alarmBudget':L['alarmBudget'],
               'provisionalShortest':r['actions'],'knockoutsInExample':r['knockouts'],'alarmInExample':r['triggeredAlarm'],'ghostPossibleLocally':noghost['solved'],
               'noKnockoutShortest':noKO.get('actions')if noKO['solved']else None,'smokeRequiredLocally':not nosmoke['solved']if L['gadgets']['smoke'] else False,'empRequiredLocally':not noemp['solved']if L['gadgets']['emp']else False,
               'requiredKey':bool(L['key']),'smokeUsedInExample':r['smokeUsed'],'empUsedInExample':r['empUsed'],'expandedStates':r['expanded'],'status':'Structural validation + provisional Python solver PASS; Unity solver PENDING'})
 sollist[str(i)]=r

(ROOT/'Levels_38_to_50_Candidates.json').write_text(json.dumps(m.LEVELS,indent=2))
(ROOT/'Levels_36_to_50_Combined_REFERENCE.json').write_text(json.dumps(old+m.LEVELS,indent=2))
(ROOT/'LOCAL_VALIDATION.json').write_text(json.dumps(report,indent=2))
(ROOT/'EXAMPLE_SOLUTIONS.json').write_text(json.dumps(sollist,indent=2))

# Design report
summary='''# Shadow Steps — Original level designs 38–50

**Version:** R1, 11 November 2026  
**Status:** CANDIDATES ONLY. Exact imported C# rules, actual level solvability and official pars must be checked with Claude's Unity solver before production approval.

Levels 36 (Garden Gambit) and 37 (The Locked Flank V2) are preserved without changes and are included in the combined reference JSON.

## Campaign difficulty curve

| Level | Title | Board | Nodes | Guards | Cameras | Local shortest* | Tactical purpose |
|---:|---|---|---:|---:|---:|---:|---|
'''
focus={38:'KO flank vs longer no-KO bypass; crossing patrol schedule',39:'Camera exposure versus longer Ghost escape; main KO required',40:'Sweeping camera and patrol cadence',41:'Mandatory key retrieval + rotating camera',42:'Three-guard junction; KO route rewarded over detour',43:'Mandatory Smoke over a watched central bridge; unreachable watch guard',44:'Two-camera overlapping surveillance; three-guard escape',45:'First 15-column horizontal panning; mandatory key and guarded exit',46:'Mandatory EMP to cross a central camera with one-action alarm',47:'Long key detour, 3 guards, 2 cameras; gadgets optional strategy',48:'Four distinct guards and tactical flanking on a tall board',49:'Three security cameras, four guards, mandatory southern key',50:'Final key + required Smoke + required EMP + guarded bridges'}
for v in report:
 summary+=f"| {v['level']} | {v['title']} | {v['width']}×{v['height']} | {v['nodes']} | {v['guards']} | {v['cameras']} | {v['provisionalShortest']} | {focus[v['level']]} |\n"
summary+='''
*Local shortest = independent provisional Python simulation, **not** authoritative Unity C# par. The browser reviewer is for visual and phase inspection, not a replacement for Unity gameplay validation.

## Important design decisions

- **No WAIT**; movement/knockout consumes one action; activation of Smoke/EMP does not.
- Knockouts only when entering an occupied guard node from behind or a side. Never from in front. Enemy phase updates after each counted action.
- Guard sight is two raw grid squares straight forward, checked both before and after movement.
- Camera sight is a **70° cone reaching 3 grid squares from the wall face**, occluded by walls `#`, cover `X`, closed doors and Smoke. Alarm starts a countdown rather than immediate capture.
- Cameras sit on `#` wall cells with `mountFace` adjacent to a real `.` node. Their 2D wall/pole sprite is decorative only.
- **Levels 43 and 50 contain isolated guard sentries** who cannot be knocked out through the graph geometry. This is deliberate: do not reconnect their nodes with automatic walkway creation.
- Smoke is required by local solver on Levels 43 and 50. EMP is required on Levels 46 and 50; this must be independently proved in the C# solver too.
- Levels 45–50 are 15 columns wide and intentionally require horizontal camera panning in portrait. Do not vertically pan; use two-finger horizontal drag and player follow.
- Use the four existing background art themes with an explicit per-level theme mapping; adapt/extend visually, do not stretch a portrait image to a 15-column world.
- All levels have at most 25 playable nodes, and map rows are at most 16×16. No new player abilities or guard immunity rules.

## Review considerations

1. **Correctness first:** run C# structural validation and solver, rederive optimal actions, stars, Ghost offers, required Smoke/EMP, and lockdown feasibility. Independent results can differ due to exact occlusion/timing edge cases.
2. **Playtest decision quality:** does each extra enemy alter routing or timing? If any guard/camera can be removed with no effect, flag it for redesign instead of decorating the board with empty threats.
3. **Performance/readability:** guard and camera facing, NEXT tiles, walls, cover, the exit door and camera cones must remain legible in portrait view at correct scale.
4. **Puzzle constraints:** Never silently change existing levels 1–37, the camera cone rules, node topology, selected background, or knockout gameplay.
5. **No score invention:** local shortest-route estimates are provided for QA only. Official par, star thresholds and Ghost badge must come from actual project solver.

## File organization

- `Levels_38_to_50_Candidates.json`: importer-ready candidate array, 13 new levels.
- `Levels_36_to_50_Combined_REFERENCE.json`: exact previous 36/37 plus new 38–50, for continuity; don't re-import 36/37 if already registered.
- `Individual_Levels/`: each level separately.
- `LOCAL_VALIDATION.json` and `EXAMPLE_SOLUTIONS.json`: provisional test results.
- `ShadowSteps_38_50_Interactive_Review.html`: standalone offline visual review with selectable stages, guard/camera phases, action-by-action route playback and horizontal board panning.
- `ShadowSteps_38_50_Atlas.png`: visual grid index. `LEVEL_50_Preview.png`: detailed final layout.
- `CLAUDE_CODE_IMPORT.md`: one-step integration instructions for Claude.
- `build_levels.py`, `tune_levels.py`, `publish_pack.py`: independent reference scripts for reproducible design/provisional test; no Unity runtime dependency.
'''
(ROOT/'README_START_HERE.md').write_text(summary)

prompt='''# Claude Code — import Shadow Steps LEVEL-38 to LEVEL-50 (candidates)

I have supplied `ShadowSteps_Levels_38_to_50_Design_Handoff.zip`. Unzip it in a temporary work folder.

1. Read `README_START_HERE.md` and `LOCAL_VALIDATION.json` before editing the Unity project.
2. Inspect the actual Unity stage importer, existing stage registry, graph validator, deterministic C# solver, Ghost/score policies, camera projection and occlusion, and art/theme configuration. Do not infer code paths or APIs.
3. Treat `Levels_38_to_50_Candidates.json` as **13 NEW levels only** (LEVEL-38...LEVEL-50). `base:"LEVEL-30"` is only an inheritance/example template: **DO NOT OVERWRITE LEVEL-30 OR 1–37**. 36/37 were previously separately handed off; leave them unchanged.
4. Preserve all supplied graph layouts, patrol positions, camera mounts, doors, keys, gadgets and code/title/intro text. If a level fails to import/solve or an edge is misinterpreted, REPORT the precise mismatch and proposed correction BEFORE editing geometry.
5. Re-run the **authoritative C# validator/solver** for ALL 13 levels. Verify guard schedules, no overlapping guards, no illegal move or WAIT; camera 70° width, 3-grid-square reach from wall face; before/after visibility; locked-door occlusion; two-turn Smoke/EMP duration and alarm final-action rule.
6. Prove or disprove these *provisional* claims with the real solver: Level43 mandatory Smoke, Level46 mandatory EMP, Level50 BOTH mandatory Smoke and EMP, keys on 41/45/47/49/50 mandatory; knockout shorter than no-KO on 38,40,42; 39 offers a longer clean route.
7. Compute official optimal actions, 3-star/2-star thresholds, valid Ghost availability, and flag any uninteresting/unneeded cameras or guards. Do NOT copy legacy LEVEL-30 scoring or Ghost field from `base`; compute from new level.
8. Integrate background art (4 visual chapters), stone walls and bush planters at logical `#`/`X` cells, natural wall/pole camera mounting. Do not alter gameplay obstruction based on decorative art. Portrait viewport must pan horizontally only for 15-column levels; pan gestures consume no turns.
9. Deliver a validation report and screenshots for LEVEL-43, LEVEL-46, LEVEL-49 and LEVEL-50 with overlaid vision, before marking candidate set accepted. Stop and request approval if solver results contradict the local report. No silent level-redesign.
'''
(ROOT/'CLAUDE_CODE_IMPORT.md').write_text(prompt)

# Art diagrams
BG=(235,221,194); FLOOR=(248,237,217); GRID=(222,206,178); EDGE=(139,123,103)
def font(sz,bold=False):return ImageFont.truetype(BOLD if bold else FONT,sz)
def dgrid(l, dw=620, dh=390,step=0,legend=True):
 rows=l['rows'];W=len(rows[0]);H=len(rows);im=Image.new('RGB',(dw,dh),'#F6F2EA');dr=ImageDraw.Draw(im,'RGBA')
 pad=35;top=15
 unit=min((dw-2*pad)/W,(dh-2*top)/H)
 bx=(dw-W*unit)/2;by=(dh-H*unit)/2
 def p(a):return (bx+(a[0]+.5)*unit,by+(a[1]+.5)*unit)
 dr.rounded_rectangle((bx-4,by-4,bx+W*unit+4,by+H*unit+4),radius=10,fill=(*BG,255),outline=(185,162,133,255),width=3)
 for y in range(H):
  for x in range(W):
   xx=bx+x*unit;yy=by+y*unit
   c=rows[y][x]
   dr.rectangle((xx+1,yy+1,xx+unit-1,yy+unit-1),fill=(*FLOOR,255) if (x+y)%2 else (242,226,203,255))
   if c in ('X','#'):
    if c=='X':
     dr.rounded_rectangle((xx+unit*.16,yy+unit*.20,xx+unit*.84,yy+unit*.88),radius=4,fill=(186,157,120,255),outline=(143,116,86,255),width=1)
     dr.ellipse((xx+unit*.18,yy+unit*.05,xx+unit*.82,yy+unit*.72),fill=(53,157,64,255),outline=(31,117,51,255),width=2)
     dr.ellipse((xx+unit*.3,yy+unit*.13,xx+unit*.68,yy+unit*.54),fill=(100,205,78,255))
    else:dr.rounded_rectangle((xx+unit*.08,yy+unit*.16,xx+unit*.92,yy+unit*.85),radius=2,fill=(195,178,164,255),outline=(126,109,96,255),width=2)
 ns,adj,edges=m.edges_of(rows)
 for a,b in edges:
  px,py=p(a);qx,qy=p(b)
  dr.line((px,py,qx,qy),fill=(*EDGE,255),width=max(2,int(unit*.085)))
  dr.line((px,py,qx,qy),fill=(254,249,232,200),width=max(1,int(unit*.03)))
 for a in ns:
  cx,cy=p(a);rr=unit*.14
  dr.ellipse((cx-rr,cy-rr,cx+rr,cy+rr),fill=(255,252,241,255),outline=(137,127,106,255),width=2)
 # Camera watched tiles and guard line of sight from phase step
 guard_poses={}
 for g in l['guards']:
  po,fa=m.guard_at(g,step);guard_poses[g['id']]=(po,fa)
  for tgt in ns:
   if m.guard_sees(l,g,step,tgt):
    xx,yy=p(tgt);rr=unit*.24
    dr.ellipse((xx-rr,yy-rr,xx+rr,yy+rr),fill=(239,91,71,65),outline=(211,87,69,170),width=2)
 for c in l['cameras']:
  for tgt in ns:
   if m.cam_sees(l,c,step,tgt):
    xx,yy=p(tgt);rr=unit*.22
    dr.ellipse((xx-rr,yy-rr,xx+rr,yy+rr),fill=(255,191,74,72),outline=(234,160,49,185),width=2)
 for ca in l['cameras']:
  x,y=ca['wall'];px,py=p((x,y));rr=unit*.26
  dr.ellipse((px-rr,py-rr,px+rr,py+rr),fill=(65,80,108,255),outline=(245,199,72,255),width=2)
  vec=m.D[m.cam_at(ca,step)]
  dr.line((px,py,px+vec[0]*unit*.45,py+vec[1]*unit*.45),fill=(255,202,58,255),width=3)
 for gu in l['guards']:
  po,fa=guard_poses[gu['id']];x,y=p(po);rr=unit*.28
  dr.ellipse((x-rr,y-rr,x+rr,y+rr),fill=(40,105,181,255),outline=(236,244,255,255),width=2)
  vec=m.D[fa]
  dr.line((x,y,x+vec[0]*unit*.31,y+vec[1]*unit*.31),fill=(255,239,215,255),width=max(2,int(unit*.12)))
 if l['key']:
  x,y=p(l['key']);r=unit*.15
  dr.ellipse((x-r,y-r,x+r,y+r),fill=(237,195,53,255),outline=(174,123,35,255),width=2)
  dr.text((x,y),"K",font=font(max(9,int(unit*.26)),True),fill=(67,42,7,255),anchor='mm')
 for door in l['doors']:
  a,b=door['a'],door['b'];x1,y1=p(a);x2,y2=p(b);cx=(x1+x2)/2;cy=(y1+y2)/2
  dr.rounded_rectangle((cx-unit*.22,cy-unit*.20,cx+unit*.22,cy+unit*.20),radius=2,fill=(241,183,65,255),outline=(120,75,20,255),width=2)
  dr.text((cx,cy),'D',font=font(max(8,int(unit*.24)),True),fill=(70,48,24,255),anchor='mm')
 for tag,loc,color in [('S',l['start'],(28,60,82,255)),('E',l['exit'],(30,123,95,255))]:
  x,y=p(loc);r=unit*.34
  dr.ellipse((x-r,y-r,x+r,y+r),fill=color,outline=(245,245,240,255),width=2)
  dr.text((x,y),tag,anchor='mm',font=font(max(11,int(unit*.36)),True),fill='white')
 return im

cards=[]
for L,meta in zip(m.LEVELS,report):
 i=meta['level'];w=530;h=455
 art=dgrid(L,dw=w,dh=365)
 card=Image.new('RGB',(w,h),'#FBFAF7');dr=ImageDraw.Draw(card)
 dr.rounded_rectangle((1,1,w-1,h-1),radius=12,outline='#D3CCBE',width=2)
 dr.text((15,12),f'{i}  {L["title"]}',font=font(20,True),fill='#203B4C')
 dr.text((15,42),f'{meta["width"]}×{meta["height"]} • {meta["guards"]} guards • {meta["cameras"]} cameras • {meta["nodes"]} nodes',font=font(15),fill='#5B6870')
 card.paste(art,(0,62))
 cards.append(card)
 big=dgrid(L,dw=1250,dh=1000)
 if i==50:big.save(ROOT/'LEVEL_50_Preview.png')

cols=3;rows_needed=math.ceil(len(cards)/cols)
atlas=Image.new('RGB',(cols*530+16,(rows_needed)*455+16),'#EDF0EF')
for i,im in enumerate(cards):atlas.paste(im,((i%cols)*530+8,(i//cols)*455+8))
atlas.save(ROOT/'ShadowSteps_38_50_Atlas.png')

# Interactive offline reviewer: not a full authoritative game, allows phase and route replay and horizontal X-pan.
levels_js=json.dumps(m.LEVELS,separators=(',',':')).replace('</','<\\/')
sols_js=json.dumps(sollist,separators=(',',':')).replace('</','<\\/')
html='''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Shadow Steps — Levels 38–50 Design Review</title><style>
:root{color-scheme:dark;font:15px system-ui,-apple-system,Arial,sans-serif;background:#101b2b;color:#ebf0f6}*{box-sizing:border-box}
body{margin:0 auto;padding:16px;max-width:1080px}h1{font-size:24px;margin:8px 0 4px}p{color:#b1c2d2;line-height:1.55}
.head{display:flex;gap:12px;flex-wrap:wrap;align-items:end;justify-content:space-between;margin-bottom:12px}
.panel{background:#1d3046;border:1px solid #344960;border-radius:15px;padding:12px;margin-bottom:12px}
select,button,input{font:inherit;background:#344b62;border:1px solid #5b7086;color:white;border-radius:9px;padding:9px;cursor:pointer}
button:disabled{opacity:.4}input[type=range]{cursor:pointer;width:100%}
.controls{display:flex;gap:7px;align-items:center;flex-wrap:wrap;margin:10px 0}
.view{width:100%;aspect-ratio:1.10;background:#d8c2a5;border-radius:12px;overflow:hidden;touch-action:pan-y;cursor:grab;user-select:none}
.view svg{display:block;width:100%;height:100%}.view:active{cursor:grabbing}
.legend{display:flex;flex-wrap:wrap;gap:10px;font-size:12px;color:#b9c8d6}.note{font-size:12px;color:#bdcddb}
.kpi{display:flex;gap:8px;flex-wrap:wrap}.kpi span{background:#15283d;padding:6px 9px;border-radius:7px;font-size:13px}
#path{background:#132339;padding:10px;border-radius:10px;min-height:70px;font-size:13px;line-height:1.5;color:#c5d5e5;overflow-wrap:anywhere}
#subtitle{color:#b1c2d2;font-size:13px;line-height:1.5}.crumb{font-size:12px;color:#98c2d4}
</style></head><body>
<header class="head"><div><div class="crumb">SHADOW STEPS • LEVEL DESIGN REVIEW</div><h1>Levels 38–50</h1><p>Prototype level graphs, camera cones and example route playback. These are candidates awaiting the Unity C# solver.</p></div>
<div><label for="pick">Choose level</label><br><select id="pick"></select></div></header>
<div class="panel"><div class="kpi" id="kpis"></div><h2 id="title"></h2><p id="subtitle"></p>
<div class="view" id="view"><svg id="board" aria-label="Puzzle board"></svg></div>
<div class="controls"><button id="back">◀ Previous</button><button id="forward">Next step ▶</button><button id="restart">↺ Restart</button><span id="turn">Action 0</span></div>
<div class="note">Pan horizontally by dragging board left/right, or use the slider. The viewport stays fixed vertically.</div>
<input type="range" id="pan" min="0" max="100" value="0" aria-label="Horizontal board position">
<div class="legend"><span>● dark = start</span><span>● green = exit</span><span>● blue = guard</span><span>● amber = camera coverage</span><span>● red halo = guard sight</span><span>■ green = cover</span><span>■ stone = wall</span></div></div>
<div class="panel"><b>Route playback</b><div id="path"></div><p class="note">Route playback uses provisional locally validated sample solutions. Exact camera occlusion, gadget expiry, par and Ghost scoring must be confirmed in the Unity solver.</p></div>
<script>
const LEVELS=__LEVELS__, SOLS=__SOLS__;
const el=id=>document.getElementById(id),D={N:[0,-1],E:[1,0],S:[0,1],W:[-1,0]},F='NESW';
let ix=0,ev=0,viewOffset=0,drag=null;
function cell(l,x,y){return l.rows[y]?.[x]??'#'}
function guardPose(g,t){let r=g.route,n=r.length;
 if(g.kind==='static')return {p:r[0],f:g.facing};
 if(g.kind==='rotating')return {p:r[0],f:F[(F.indexOf(g.facing)+(g.clockwise?t:-t)%4+1000)%4]};
 if(g.kind==='pingPong'){let k=(t+(g.offset??0))%(2*n-2),i=k<n?k:2*n-2-k,p=r[i];if(t===0)return {p,f:g.facing};let prevk=(t-1+(g.offset??0))%(2*n-2),j=prevk<n?prevk:2*n-2-prevk,q=r[j];return {p,f:Object.keys(D).find(f=>D[f][0]*(p[0]-q[0])+D[f][1]*(p[1]-q[1])>0)??g.facing}}
 let j=(t+(g.offset??0))%n,p=r[j],q=r[(j+n-1)%n];return {p,f:t===0?g.facing:Object.keys(D).find(f=>D[f][0]*(p[0]-q[0])+D[f][1]*(p[1]-q[1])>0)??g.facing}
}
function cameraFace(c,t){if(c.kind==='fixed')return c.facing;if(c.kind==='rotating')return F[(F.indexOf(c.facing)+(c.clockwise?t:-t)%4+1000)%4];return F[(F.indexOf(c.facing)+((Math.floor(t/2)%2)*(c.clockwise?1:-1))+1000)%4]}
function seesGuard(g,t,n,l){let q=guardPose(g,t),d=D[q.f],fx=(n[0]-q.p[0])*d[0]+(n[1]-q.p[1])*d[1],side=(n[0]-q.p[0])*d[1]-(n[1]-q.p[1])*d[0];if(fx<1||fx>2||side!==0)return false;for(let z=1;z<fx;z++)if('#X'.includes(cell(l,q.p[0]+z*d[0],q.p[1]+z*d[1])))return false;return true}
function seesCam(c,t,n,l){let d=D[cameraFace(c,t)],p=[c.wall[0]+D[c.mountFace][0]*.5,c.wall[1]+D[c.mountFace][1]*.5];let v=[n[0]-p[0],n[1]-p[1]],fw=v[0]*d[0]+v[1]*d[1],side=Math.abs(v[0]*d[1]-v[1]*d[0]);if(fw<=0||fw>3.001||side>fw*Math.tan(35*Math.PI/180))return false;
for(let i=1,steps=Math.ceil(Math.max(Math.abs(v[0]),Math.abs(v[1]))*25);i<steps;i++){let x=p[0]+v[0]*i/steps,y=p[1]+v[1]*i/steps,rx=Math.floor(x+.5),ry=Math.floor(y+.5);if((rx===Math.round(p[0])&&ry===Math.round(p[1]))||(rx===n[0]&&ry===n[1]))continue;if('#X'.includes(cell(l,rx,ry)))return false}return true}
const svgEl=(tag,attributes={})=>`<${tag} ${Object.entries(attributes).map(([k,v])=>`${k}="${v}"`).join(' ')} />`;
function interp(l,events){let t=0,pos=l.start,dead=new Set(),key=false,open=false,smoke=null,smokeT=0,emp=null,empT=0;for(let j=0;j<ev;j++){let e=events[j];if(!e)break;if(e[0]==='SMOKE'){smoke=e[1];smokeT=2}else if(e[0]==='EMP'){emp=e[1];empT=2}else if(e[0]==='MOVE'){pos=e[1];t++;if(e[2]?.startsWith('KO '))dead.add(e[2].slice(3));if(l.key&&pos[0]===l.key[0]&&pos[1]===l.key[1])key=true;let dd=l.doors.find(d=>{let a=d.a,b=d.b;return (a[0]===pos[0]&&a[1]===pos[1])||(b[0]===pos[0]&&b[1]===pos[1])});if(dd&&key)open=true;smokeT=Math.max(0,smokeT-1);empT=Math.max(0,empT-1)}}return{pos,t,dead,key,open,smoke:smokeT?smoke:null,emp:empT?emp:null}}
function render(){const l=LEVELS[ix],s=SOLS[`${38+ix}`],st=interp(l,s.events),W=l.rows[0].length,H=l.rows.length,u=51,fullw=W*u,fullh=H*u;let vw=Math.min(fullw,Math.round(fullh*.73)),maxoff=Math.max(0,fullw-vw);viewOffset=Math.max(0,Math.min(maxoff,viewOffset));el('pan').max=maxoff;el('pan').value=viewOffset;el('board').setAttribute('viewBox',`${viewOffset} 0 ${vw} ${fullh}`);
let nodes=[],out=`<rect x="0" y="0" width="${fullw}" height="${fullh}" fill="#e3d2b5"/>`;let P=(x,y)=>[(x+.5)*u,(y+.5)*u];
for(let y=0;y<H;y++)for(let x=0;x<W;x++){let c=l.rows[y][x];out+=`<rect x="${x*u+1}" y="${y*u+1}" width="${u-2}" height="${u-2}" fill="${(x+y)%2?'#ebdec4':'#e6d4b9'}"/>`;if(c==='.'){nodes.push([x,y])}else if(c==='X'){out+=`<rect x="${x*u+9}" y="${y*u+14}" width="${u-18}" height="${u-21}" rx="4" fill="#b69a71"/><circle cx="${(x+.5)*u}" cy="${(y+.40)*u}" r="17" fill="#2b9d4a"/><circle cx="${(x+.38)*u}" cy="${(y+.31)*u}" r="10" fill="#78cc46"/>`}else if(c==='#')out+=`<rect x="${x*u+7}" y="${y*u+10}" width="${u-14}" height="${u-18}" rx="3" fill="#b1a299" stroke="#7a6b65" stroke-width="2"/>`}
for(const [x,y] of nodes){for(const [dx,dy] of [[1,0],[0,1]]){let nx=x+2*dx,ny=y+2*dy;if(cell(l,nx,ny)!=='.')continue;let mid=cell(l,x+dx,y+dy);if(!(dx?('-+'.includes(mid)):('|+'.includes(mid))))continue;let [a,b]=P(x,y),[c,d]=P(nx,ny);out+=`<line x1="${a}" y1="${b}" x2="${c}" y2="${d}" stroke="#aa9374" stroke-width="4" stroke-linecap="round"/>`}}
for(const [x,y] of nodes){let watch=l.cameras.some(c=>st.emp!==c.id && seesCam(c,st.t,[x,y],l)),seen=l.guards.some(g=>!st.dead.has(g.id)&&seesGuard(g,st.t,[x,y],l));let [cx,cy]=P(x,y);if(watch)out+=`<circle cx="${cx}" cy="${cy}" r="18" fill="#f8b83f" fill-opacity=".37" stroke="#d99726" stroke-width="2"/>`;if(seen)out+=`<circle cx="${cx}" cy="${cy}" r="15" fill="#e45343" fill-opacity=".33" stroke="#cf5745" stroke-width="2"/>`;out+=`<circle cx="${cx}" cy="${cy}" r="5.7" fill="#fffaf0" stroke="#ad9981" stroke-width="1.8"/>`}
for(const d of l.doors){let a=P(...d.a),b=P(...d.b),x=(a[0]+b[0])/2,y=(a[1]+b[1])/2;out+=`<rect x="${x-11}" y="${y-13}" width="22" height="26" rx="3" fill="${st.open?'#5d997c':'#daa346'}" stroke="#fff1cf" stroke-width="2"/><text x="${x}" y="${y+5}" text-anchor="middle" font-size="15" fill="white">${st.open?'✓':'◈'}</text>`}
if(l.key&&!st.key){let [x,y]=P(...l.key);out+=`<circle cx="${x}" cy="${y}" r="13" fill="#f3bd40" stroke="#a9781b" stroke-width="2"/><text x="${x}" y="${y+5}" text-anchor="middle" fill="#714c15" font-weight="bold" font-size="15">K</text>`}
for(const c of l.cameras){let [x,y]=P(...c.wall),d=D[cameraFace(c,st.t)];out+=`<circle cx="${x}" cy="${y}" r="14" fill="${st.emp===c.id?'#3d9baf':'#445979'}" stroke="#f4bb46" stroke-width="2"/><line x1="${x}" y1="${y}" x2="${x+d[0]*18}" y2="${y+d[1]*18}" stroke="#ffce68" stroke-width="4"/><text x="${x}" y="${y-16}" font-size="10" fill="#38424c" text-anchor="middle">${c.id}</text>`}
for(const g of l.guards){if(st.dead.has(g.id))continue;let {p,f}=guardPose(g,st.t),[x,y]=P(...p),d=D[f];out+=`<circle cx="${x}" cy="${y}" r="16" fill="#2d77b3" stroke="white" stroke-width="2"/><line x1="${x}" y1="${y}" x2="${x+d[0]*16}" y2="${y+d[1]*16}" stroke="#fef7e9" stroke-width="4"/><text x="${x}" y="${y+26}" text-anchor="middle" font-size="12" fill="#29455b">${g.id}</text>`}
if(st.smoke){let [x,y]=P(...st.smoke);out+=`<circle cx="${x}" cy="${y}" r="25" fill="#d4dfeb" fill-opacity=".65" stroke="#abb9cc" stroke-width="3"/>`}
for(const [p,tag,fill] of [[l.start,'S','#243e53'],[l.exit,'E','#3b9c81']]){let [x,y]=P(...p);out+=`<circle cx="${x}" cy="${y}" r="15" fill="${fill}" stroke="white" stroke-width="2"/><text x="${x}" y="${y+5}" fill="white" font-weight="bold" font-size="15" text-anchor="middle">${tag}</text>`}
let [x,y]=P(...st.pos);out+=`<circle cx="${x}" cy="${y}" r="20" fill="#172e42" stroke="#77d5ec" stroke-width="4"/><circle cx="${x}" cy="${y-5}" r="7" fill="#efc5a6"/><path d="M${x-9} ${y+3}h18l-3 9h-12z" fill="#f0f2ee"/>`;
el('board').innerHTML=out;let final=ev>=s.events.length;
el('turn').textContent=`Move ${st.t} • Step ${ev}/${s.events.length}${final?' • ESCAPED':''}`;
el('title').textContent=`Level ${38+ix}: ${l.title}`;el('subtitle').textContent=l.intro;
el('kpis').innerHTML=[`${W}×${H} board`,`${nodes.length} nodes`,`${l.guards.length} guards`,`${l.cameras.length} cameras`,`${s.actions} provisional moves`,l.key?'Key + door':'No key',l.gadgets.smoke?`${l.gadgets.smoke} Smoke`:'',l.gadgets.emp?`${l.gadgets.emp} EMP`:'' ].filter(Boolean).map(s=>`<span>${s}</span>`).join('');
let action=s.events.slice(0,ev).map(e=>e[0]==='MOVE'?`Move → (${e[1].join(',')}) ${e[2]??''}`:`${e[0]} ${Array.isArray(e[1])?`at (${e[1].join(',')})`:e[1]}`);el('path').textContent=(action.length?action.join(' · '):'At start. Select Next step to follow the reference solution.')+(final?' ✓':'');
el('back').disabled=ev===0;el('forward').disabled=final;
}
function sel(){ix=+el('pick').value;ev=0;viewOffset=0;render()}
LEVELS.forEach((l,j)=>{let o=document.createElement('option');o.value=j;o.textContent=`Level ${38+j}: ${l.title}`;el('pick').append(o)});
el('pick').addEventListener('change',sel);el('back').onclick=()=>{ev=Math.max(0,ev-1);render()};el('forward').onclick=()=>{ev=Math.min(SOLS[`${38+ix}`].events.length,ev+1);let data=interp(LEVELS[ix],SOLS[`${38+ix}`].events),pt=data.pos;let vw=+el('board').getAttribute('viewBox').split(' ')[2],cx=(pt[0]+.5)*51;viewOffset=Math.max(0,cx-vw*.6);render()};el('restart').onclick=()=>{ev=0;viewOffset=0;render()};el('pan').oninput=e=>{viewOffset=+e.target.value;render()};
const view=el('view');view.addEventListener('pointerdown',e=>{drag={x:e.clientX,offset:viewOffset};view.setPointerCapture(e.pointerId)});view.addEventListener('pointermove',e=>{if(!drag)return;let vw=+el('board').getAttribute('viewBox').split(' ')[2];viewOffset=drag.offset-(e.clientX-drag.x)*vw/view.clientWidth;render()});for(let evt of ['pointerup','pointercancel'])view.addEventListener(evt,()=>drag=null);render();
</script></body></html>'''
html=html.replace('__LEVELS__',levels_js).replace('__SOLS__',sols_js)
(ROOT/'ShadowSteps_38_50_Interactive_Review.html').write_text(html)

# keep byte-identical previous 36/37 in combined ref
assert (old[0]['title'],old[1]['title'])==('Garden Gambit','The Locked Flank')
print('PUBLISHED',len(report),'stages',[(v['level'],v['provisionalShortest'],v['guards'],v['cameras'])for v in report])
