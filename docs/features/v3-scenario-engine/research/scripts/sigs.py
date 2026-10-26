from real import *
md50=bc(MKT/MKT_MA50-1); br=bc(BREADTH50); SD20=C.rolling(20).std()
sec=meta.sector.reindex(syms).fillna('')
secret=RET20.where(UNI).T.groupby(sec).transform('median').T
SIG={  # tín hiệu, MA dùng để bán, số phiên giữ tối đa mặc định
 'KB1':(topk(PANIC&(TV>30),-RS126,5),10,15),
 'KB2':(topk((md50<-0.05)&(TV>100),-RSI14,5),10,15),
 'KB3':(topk((RSI14<35)&(C>MA[200])&(TV>20)&(br<0.35),-RSI14,5),10,15),
 'KB4':(topk((secret<-0.10)&(RET20<secret)&(TV>20)&(br<0.35),-RET20,5),5,10),
 'KB5':(topk((C<MA[20]-2*SD20)&(TV>100)&(md50<-0.03),-(C/MA[20]),5),10,15),
}
LV={n:ma_touch_level(n) for n in (5,10)}
def summarize(t):
    t=t[t.date>='2023-01-01']; isp=t[t.date<=IS[1]]; oos=t[t.date>=OOS[0]]
    w=lambda x:round((x.ret>0.0005).mean()*100,1) if len(x) else np.nan
    g=t.ret[t.ret>0].sum(); b=-t.ret[t.ret<0].sum()
    yrs=[w(t[t.date.dt.year==y]) for y in (2023,2024,2025,2026)]
    return dict(n=len(t),winIS=w(isp),winOOS=w(oos),winQ4=w(t[t.date.dt.quarter==4]),winMinNam=min(yrs),
        hoavon=round((t.ret.abs()<=0.0005).mean()*100,1),avg=round(t.ret.mean()*100,2),
        avgLoss=round(t.ret[t.ret<-0.0005].mean()*100,2),pf=round(g/b,2) if b>0 else np.inf,
        worst=round(t.ret.min()*100,1),p10=round(t.ret.quantile(.1)*100,1),hold=round(t.days.mean(),1))
def portfolio(trades,slots=5):
    allt=trades.sort_values(['date','kb']); held=[]; taken=[]
    for _,x in allt.iterrows():
        held=[h for h in held if h[1]>=x.date]
        if len(held)>=slots or any(h[0]==x.sym for h in held): continue
        i1=min(dates.get_loc(x.date)+x.days,len(dates)-1); held.append((x.sym,dates[i1])); taken.append(x)
    P=pd.DataFrame(taken); P['pnl']=P.ret/slots
    return {y:round(P[P.date.dt.year==y].pnl.sum()*100,1) for y in (2023,2024,2025,2026)}
