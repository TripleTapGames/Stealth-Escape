import copy,sys
import tune_levels as t,build_levels as m

def candidates(L):
 main=m.solve(L,maxstates=80000);path=[tuple(p)for p in main['route']];pairs=[]
 for a,b in zip(path,path[1:]):
  if abs(a[0]-b[0])+abs(a[1]-b[1])!=2:continue
  if not any(set((a,b))==set((tuple(d['a']),tuple(d['b'])))for d in L['doors']):
   pp={a,b};used=any(any(set((u,v))==pp for u,v in zip([tuple(p)for p in g['route']],[tuple(p)for p in g['route']][1:]))for g in L['guards'])
   if not used and (a,b) not in pairs:pairs.append((a,b))
 return pairs
for L in m.LEVELS:
 n=int(L['name'].split('_')[-1]);base=m.solve(L,maxstates=120000)
 if not base['solved']:continue
 if n in (43,47,49):continue
 results=[]
 for a,b in candidates(L):
  x=copy.deepcopy(L)
  try:t.cut(x,a,b)
  except AssertionError:continue
  if m.validate(x):continue
  r=m.solve(x,maxstates=50000)
  if not r['solved']:continue
  if n in (46,50):
   noemp=m.solve(x,no_emp=True,maxstates=40000)
   if noemp['solved']:continue
  if n==50:
   nosm=m.solve(x,no_smoke=True,maxstates=40000)
   if nosm['solved']:continue
  results.append((r['actions']-base['actions'],a,b,r['actions'],r['route']))
 results.sort(key=lambda x:(-x[0],x[1]))
 print('\n',n,L['title'], 'base',base['actions'],'best', [(v[0],v[1],v[2],v[3]) for v in results[:6]],flush=True)
