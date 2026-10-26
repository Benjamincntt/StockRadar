import sys; sys.path.insert(0,sys.argv[1]); from bt import *
pd.set_option('display.width',250)
md50=bc(MKT/MKT_MA50-1); br=bc(BREADTH50); SD20=C.rolling(20).std()
sec=meta.sector.reindex(syms).fillna('')
secret=RET20.where(UNI).T.groupby(sec).transform('median').T
SIG={
 'KB1':(topk(PANIC&(TV>30),-RS126,5),10,15),
 'KB2':(topk((md50<-0.05)&(TV>100),-RSI14,5),10,15),
 'KB3':(topk((RSI14<35)&(C>MA[200])&(TV>20)&(br<0.35),-RSI14,5),10,15),
 'KB4':(topk((secret<-0.10)&(RET20<secret)&(TV>20)&(br<0.35),-RET20,5),5,10),
 'KB5':(topk((C<MA[20]-2*SD20)&(TV>100)&(md50<-0.03),-(C/MA[20]),5),10,15),
}
LV={n:ma_touch_level(n) for n in (5,10)}
def variants(n,hold):
    return {'A dong cua':dict(hold=hold,exit_rule=(C>MA[n])),
            'B realtime':dict(hold=hold,exit_level=LV[n]),
            'C realtime+SL7':dict(hold=hold,exit_level=LV[n],sl=0.07),
            'D realtime+SL10':dict(hold=hold,exit_level=LV[n],sl=0.10),
            'E dong cua+SL10':dict(hold=hold,exit_rule=(C>MA[n]),sl=0.10),
            'F rt>=giamua':dict(hold=hold,exit_level=LV[n],exit_above_entry=True),
            'G rt>=giamua+SL10':dict(hold=hold,exit_level=LV[n],exit_above_entry=True,sl=0.10)}
rows=[]; ALL={}
for kb,(sig,n,hold) in SIG.items():
    for vn,kw in variants(n,hold).items():
        t=simulate(sig=sig,**kw); t=t[t.date>='2023-01-01']; r=report(t)
        w=t.ret[t.ret>0]; l=t.ret[t.ret<-0.0005]
        rows.append(dict(kb=kb,ban=vn,n=len(t),winIS=r.loc['IS 23-25H1','win'],winOOS=r.loc['OOS 25H2-26','win'],winQ4=r.loc['Q4 23-25','win'],hoavon=round((t.ret.abs()<=0.0005).mean()*100,1),
            winMinNam=min(r.loc[str(y),'win'] for y in (2023,2024,2025,2026)),avg=round(t.ret.mean()*100,2),avgLoss=round(l.mean()*100,2),
            pf=round(w.sum()/-l.sum(),2),worst=round(t.ret.min()*100,1),p10=round(t.ret.quantile(.1)*100,1),hold=round(t.days.mean(),1)))
        ALL[(kb,vn)]=t
D=pd.DataFrame(rows); print(D.to_string(index=False)); D.to_csv(S+'/compare_summary.csv',index=False)
print('\nDanh muc 5 slot x 20% von, gop 5 KB (uu tien KB1..KB5), cong don khong lai kep:')
for vn in variants(10,15):
    allt=pd.concat([ALL[(kb,vn)].assign(kb=kb) for kb in SIG]).sort_values(['date','kb'])
    held=[]; taken=[]
    for _,x in allt.iterrows():
        held=[h for h in held if h[1]>=x.date]
        if len(held)>=5 or any(h[0]==x.sym for h in held): continue
        i1=min(dates.get_loc(x.date)+x.days,len(dates)-1); held.append((x.sym,dates[i1])); taken.append(x)
    P=pd.DataFrame(taken); P['pnl']=P.ret/5
    s=' | '.join(f"{y}: w{(P[P.date.dt.year==y].ret>0.0005).mean()*100:.0f}% LN{P[P.date.dt.year==y].pnl.sum()*100:+.1f}%" for y in (2023,2024,2025,2026))
    print(f'{vn:16s}',s,f"| worst lenh {P.ret.min()*100:.1f}%")
