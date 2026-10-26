import sys; sys.path.insert(0,sys.argv[1]); from bt import *
pd.set_option('display.width',250)
md50=bc(MKT/MKT_MA50-1); br=bc(BREADTH50); SD20=C.rolling(20).std()
sec=meta.sector.reindex(syms).fillna('')
secret=RET20.where(UNI).T.groupby(sec).transform('median').T
E10=dict(hold=15,exit_rule=(C>MA[10])); E5=dict(hold=10,exit_rule=(C>MA[5]))
KB={
 'KB1 Hoang loan thi truong': dict(sig=topk(PANIC&(TV>30),-RS126,5),**E10),
 'KB2 Chi so thung MA50 5%, bluechip': dict(sig=topk((md50<-0.05)&(TV>100),-RSI14,5),**E10),
 'KB3 Ma khoe bi keo theo': dict(sig=topk((RSI14<35)&(C>MA[200])&(TV>20)&(br<0.35),-RSI14,5),**E10),
 'KB4 Ca nganh bi ban qua muc': dict(sig=topk((secret<-0.10)&(RET20<secret)&(TV>20)&(br<0.35),-RET20,5),**E5),
 'KB5 Bluechip thung Bollinger': dict(sig=topk((C<MA[20]-2*SD20)&(TV>100)&(md50<-0.03),-(C/MA[20]),5),**E10),
}
T={}
rows=[]
for k,v in KB.items():
    t=simulate(**v,name=k); t=t[t.date>='2023-01-01']; T[k]=t
    r=report(t); w=t[t.ret>0].ret; l=t[t.ret<=0].ret
    rows.append(dict(kb=k,n=len(t),episode_days=t.date.nunique(),winIS=r.loc['IS 23-25H1','win'],winOOS=r.loc['OOS 25H2-26','win'],winQ4=r.loc['Q4 23-25','win'],
      **{f'w{y}':r.loc[str(y),'win'] for y in (2023,2024,2025,2026)},avg=round(t.ret.mean()*100,2),avgWin=round(w.mean()*100,2),avgLoss=round(l.mean()*100,2),
      worst=round(t.ret.min()*100,1),p10=round(t.ret.quantile(.1)*100,1),pf=round(w.sum()/-l.sum(),2),hold=round(t.days.mean(),1),exc=round(t.excess.mean()*100,2)))
SUM=pd.DataFrame(rows).set_index('kb'); print(SUM.T.to_string())
SUM.to_csv(S+'/final_summary.csv')
# trùng lặp: tỷ lệ ngày tín hiệu của KB i cũng là ngày tín hiệu KB j
print('\nTrung lap ngay tin hieu (% ngay cua dong co trong cot):')
D={k:set(t.date) for k,t in T.items()}
print(pd.DataFrame({j:{i:round(len(D[i]&D[j])/len(D[i])*100) for i in D} for j in D}).to_string())
allt=pd.concat([t.assign(kb=k) for k,t in T.items()])
print('\nTong ngay co it nhat 1 tin hieu:',allt.date.nunique(),' / ',len(dates[dates>='2023-01-01']))
print('Ngay tin hieu theo quy:'); print(allt.groupby(allt.date.dt.to_period('Q')).date.nunique().to_string())
# danh mục: 10 slot x 10% vốn, không lãi kép, ưu tiên KB1..KB5, không trùng mã
allt=allt.sort_values(['date','kb']); held=[]; taken=[]
for _,x in allt.iterrows():
    held=[h for h in held if h[1]>=x.date]
    if len(held)>=10 or any(h[0]==x.sym for h in held): continue
    exit_date=dates[min(dates.get_loc(x.date)+x.days,len(dates)-1)]
    held.append((x.sym,exit_date)); taken.append(x)
P=pd.DataFrame(taken); P['pnl']=0.10*P.ret
TOP30=TV.rank(axis=1,ascending=False)<=30
w30=(TV.where(TOP30).shift()); r30=(R.clip(-.16,.16)*w30).sum(axis=1)/w30.sum(axis=1)
BM30=(1+r30.fillna(0)).cumprod()
print('\nDanh muc 10 slot (moi lenh 10% von, cong don khong lai kep) vs mua-giu:')
for y in (2023,2024,2025,2026):
    p=P[P.date.dt.year==y]; yd=dates[dates.year==y]
    print(y,f"lenh {len(p)}, win {(p.ret>0).mean()*100:.0f}%, loi nhuan danh muc {p.pnl.sum()*100:+.1f}% | EW thi truong {(MKT[yd[-1]]/MKT[yd[0]]-1)*100:+.1f}% | top30 thanh khoan (proxy VN30) {(BM30[yd[-1]]/BM30[yd[0]]-1)*100:+.1f}%")
q=P[P.date.dt.quarter==4]; print('Rieng Q4 2023-2025: lenh',len(q),'win',round((q.ret>0).mean()*100),'% ; loi nhuan',round(q.pnl.sum()*100,1),'%')
allt.to_csv(S+'/final_trades.csv',index=False)
for slots in (5,10):
    held=[]; taken=[]; exp=pd.Series(0.0,index=dates)
    for _,x in allt.iterrows():
        held=[h for h in held if h[1]>=x.date]
        if len(held)>=slots or any(h[0]==x.sym for h in held): continue
        i0=dates.get_loc(x.date)+1; i1=min(i0+x.days-1,len(dates)-1)
        held.append((x.sym,dates[i1])); taken.append(x); exp.iloc[i0:i1+1]+=1/slots
    P=pd.DataFrame(taken); P['pnl']=P.ret/slots
    print(f'\n--- {slots} slot, moi lenh {100//slots}% von')
    for y in (2023,2024,2025,2026):
        p=P[P.date.dt.year==y]; yd=dates[dates.year==y]
        print(y,f"lenh {len(p)}, win {(p.ret>0).mean()*100:.0f}%, LN danh muc {p.pnl.sum()*100:+.1f}%, von dung TB {exp[yd].mean()*100:.0f}% | EW thi truong {(MKT[yd[-1]]/MKT[yd[0]]-1)*100:+.1f}% | top30 TV (proxy) {(BM30[yd[-1]]/BM30[yd[0]]-1)*100:+.1f}%")
