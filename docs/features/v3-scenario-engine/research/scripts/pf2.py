import sys; exec(open(sys.argv[1]+'/pfeng.py',encoding='utf-8').read())
md=bc(MKT/MKT_MA50-1); m20=bc(MKT/MKT_MA20-1); slope20=bc(MKT_MA20/MKT_MA20.shift(5)-1); brc=bc(BREADTH50-BREADTH50.shift(5))
base3=(LEFT==5)&(TV>30)
def n3(cond=True,**kw):
    s=topk(base3&cond,RS63,5) if cond is not True else topk(base3,RS63,5)
    t=simulate_real2(s,hold=12,exit_open_rule=(TDM==2),entry='atc',**kw); return tag(t[t.date>='2023-01-01'],'N3t')
def P(n3t,**kw): return run_pf(BASE[:4]+[('N3t',n3t)],**kw)
metrics(*P(BASE[4][1]),'P1 goc')
print('-- Loc thi truong cho N3')
V={'bo vung sat MA50 (|d50|<1.5%)':~((md>-0.015)&(md<0.015)),'chi so tren MA50':md>0,'chi so tren MA20':m20>0,'MA20 chi so dang len':slope20>0,
   'do rong khong sut manh (chg5>-9%)':brc>-0.09,'tren MA20 HOAC duoi MA50 >5%':(m20>0)|(md<-0.05)}
N3V={}
for k,c in V.items(): N3V[k]=n3(c); metrics(*P(N3V[k]),'N3 '+k)
print('-- Cat lo realtime cho N3')
for sl in (0.05,0.07,0.10): metrics(*P(n3(sl=sl)),f'N3 cat lo {int(sl*100)}%')
print('-- Gioi han & phanh danh muc')
metrics(*P(BASE[4][1],sector_cap=2),'gioi han 2 ma/nganh')
metrics(*P(BASE[4][1],max_new_day=3),'toi da 3 lenh moi/ngay')
for b in (0.08,0.12): metrics(*P(BASE[4][1],dd_brake=b),f'phanh: ngung mua khi sut >{int(b*100)}%')
import pickle; pickle.dump(N3V,open(sys.argv[1]+'/n3v.pkl','wb'))
