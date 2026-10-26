import sys; sys.path.insert(0,sys.argv[1]); from sigs import *
pd.set_option('display.width',250)
pm=pd.read_pickle(sys.argv[1]+'/pm.pkl')
for x in pm.itertuples():
    if x.date in dates and x.sym in C.columns and not np.isnan(C.at[x.date,x.sym]) and x.c>0:
        r=C.at[x.date,x.sym]/x.c
        PM[(x.sym,x.date)]=(x.o*r,x.h*r,x.l*r,x.c*r)
print('nen chieu dung duoc:',len(PM))
def V(n,kind,hd,sl=None):
    if kind=='A': return dict(hold=hd,exit_rule=(C>MA[n]),sl=sl)
    if kind=='F': return dict(hold=hd,exit_level=LV[n],exit_above_entry=True,sl=sl)
rows=[]
print('\n### 1) Anh huong cua luat T+2 chi ban buoi chieu (giu toi da mac dinh)')
for kb,(sig,n,hd) in SIG.items():
    for kind in ('A','F'):
        for t2 in ('full','pm_close','skip'):
            r=summarize(simulate_real(sig,t2=t2,**V(n,kind,hd))); rows.append(dict(kb=kb,ban=kind,t2=t2,**r))
D=pd.DataFrame(rows); print(D[['kb','ban','t2','n','winIS','winOOS','winMinNam','hoavon','avg','avgLoss','pf','worst']].to_string(index=False))
print('\n### 2) Kiem cach xap xi bang nen gio that (lenh co T+2 tu 28/04/2026)')
for kind in ('A','F'):
    diffs=[];same=0;tot=0
    for kb,(sig,n,hd) in SIG.items():
        a=simulate_real(sig,t2='pm_close',**V(n,kind,hd)); b=simulate_real(sig,t2='pm_bars',**V(n,kind,hd))
        m=a.merge(b,on=['date','sym'],suffixes=('_x','_b'))
        m=m[[ (s,dates[dates.get_loc(d)+3]) in PM for s,d in zip(m.sym,m.date)]]
        tot+=len(m); same+=(abs(m.ret_x-m.ret_b)<1e-9).sum(); diffs+=list(m.ret_b-m.ret_x)
    d=np.array(diffs)
    print(f'{kind}: {tot} lenh co nen chieu that | ket qua giong het {same} | chenh TB (nen gio - xap xi) {d.mean()*100:+.2f}%/lenh, lech lon nhat {np.abs(d).max()*100:.1f}%' if tot else f'{kind}: khong co lenh')
print('\n### 3) Giu toi da bao nhieu phien (T+2 chi ban theo gia dong cua)')
rows=[]; TR={}
for kb,(sig,n,hd0) in SIG.items():
    for kind in ('A','F'):
        for hd in ((5,7,10) if n==5 else (5,7,10,15)):
            t=simulate_real(sig,t2='pm_close',**V(n,kind,hd)); TR[(kb,kind,hd)]=t
            rows.append(dict(kb=kb,ban=kind,giu=hd,**summarize(t)))
D3=pd.DataFrame(rows); print(D3[['kb','ban','giu','n','winIS','winOOS','winMinNam','hoavon','avg','avgLoss','pf','worst','p10','hold']].to_string(index=False))
D.to_csv(sys.argv[1]+'/real_t2.csv',index=False); D3.to_csv(sys.argv[1]+'/real_hold.csv',index=False)
import pickle; pickle.dump(TR,open(sys.argv[1]+'/real_trades.pkl','wb'))
