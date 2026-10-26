import sys; sys.path.insert(0,sys.argv[1])
exec(open(sys.argv[1]+'/step3.py').read().split("print('### N3")[0])
def T(sig,**kw):
    t=simulate_real2(sig,**kw); return t[t.date>='2023-01-01']
n1=topk((FLOOR20>=1)&~FLOOR&(SECR20<=-0.12)&(TV>10)&(LEFT<=6),-SECR20,5)
n3=topk((LEFT==5)&(TV>30),RS63,5)
n4=topk(bc(mret<-0.015)&bc(BREADTH50>=0.35)&(TV>50)&(C>MA[200]),-R,5)
F10=dict(hold=15,exit_level=LV[10],exit_above_entry=True)
TR={
 'KB1':T(SIG['KB1'][0],**F10),'KB2':T(SIG['KB2'][0],**F10),
 'KB4':T(SIG['KB4'][0],hold=10,exit_rule=(C>MA[5])),'KB4t':T(SIG['KB4'][0],hold=10,exit_open_rule=nxt(C>MA[5]),entry='atc'),
 'N1':T(n1,hold=15,exit_rule=(C>MA[10])),'N1t':T(n1,hold=15,exit_open_rule=nxt(C>MA[10]),entry='atc'),
 'N3':T(n3,hold=12,exit_rule=(TDM==1)),'N3t':T(n3,hold=12,exit_open_rule=(TDM==2),entry='atc'),
 'N4':T(n4,hold=15,exit_open_rule=nxt(C>MA[10])),
}
D={k:set(t.date) for k,t in TR.items()}
print('N4 trung ngay voi:',{k:round(len(D['N4']&D[k])/len(D['N4'])*100) for k in ('KB1','KB2','KB4','N1','N3')})
alld=dates[dates>='2023-01-01']
def pf(keys,label,slots=5):
    allt=pd.concat([TR[k].assign(kb=f'{i}') for i,k in enumerate(keys)])
    held=[];taken=[];exp=pd.Series(0.0,index=dates)
    for _,x in allt.sort_values(['date','kb']).iterrows():
        held=[h for h in held if h[1]>=x.date]
        if len(held)>=slots or any(h[0]==x.sym for h in held): continue
        i0=dates.get_loc(x.date); i1=min(i0+x.days,len(dates)-1); held.append((x.sym,dates[i1])); taken.append(x); exp.iloc[i0+1:i1+1]+=1/slots
    P=pd.DataFrame(taken); P['pnl']=P.ret/slots
    yrs=' | '.join(f"{y}: {P[P.date.dt.year==y].pnl.sum()*100:+.1f}% w{(P[P.date.dt.year==y].ret>0.0005).mean()*100:.0f}%" for y in (2023,2024,2025,2026))
    q4=P[P.date.dt.quarter==4]
    print(f"{label:44s} {yrs} | Q4 {q4.pnl.sum()*100:+.1f}% w{(q4.ret>0.0005).mean()*100:.0f}% | lenh {len(P)} | von dung {exp[alld].mean()*100:.0f}% | worst {P.ret.min()*100:.1f}%")
print('\nDanh muc 5 vi the x 20% von (cong don):')
pf(['KB1','KB2','KB4','N1','N3'],'P0 bo de xuat lan truoc')
pf(['KB1','KB2','KB4t','N1t','N3t'],'P1 = P0 + khop ATC/ATO cho KB4 N1 N3')
pf(['KB1','KB2','KB4t','N1t','N3t','N4'],'P2 = P1 + N4')
pf(['KB1','KB2','KB4t','N1t','N4'],'P3 = P1 nhung N4 thay N3')
pf(['KB1','KB2','KB4t','N1t','N4','N3t'],'P4 = uu tien N4 truoc N3')
yd=lambda y: dates[dates.year==y]
print('Chi so binh quan deu:',' | '.join(f"{y}: {(MKT[yd(y)[-1]]/MKT[yd(y)[0]]-1)*100:+.1f}%" for y in (2023,2024,2025,2026)))
