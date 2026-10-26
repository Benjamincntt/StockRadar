import sys; sys.path.insert(0,sys.argv[1])
exec(open(sys.argv[1]+'/newkb.py').read().split("print('### N1")[0])
def T(sig,**kw):
    t=simulate_real(sig,t2='pm_close',**kw); return t[t.date>='2023-01-01']
n1=topk((FLOOR20>=1)&~FLOOR&(SECR20<=-0.12)&(TV>10)&(LEFT<=6),-SECR20,5)
n3=topk((LEFT==5)&(TV>30),RS63,5)
TR={
 'KB1 F':T(SIG['KB1'][0],**F10()),'KB2 F':T(SIG['KB2'][0],**F10()),
 'KB3 A':T(SIG['KB3'][0],**A10()),'KB4 A':T(SIG['KB4'][0],hold=10,exit_rule=(C>MA[5])),'KB5 A':T(SIG['KB5'][0],**A10()),
 'N1 A':T(n1,**A10()),'N3':T(n3,hold=12,exit_rule=(TDM==1)),'N3 tp4':T(n3,hold=12,exit_rule=(TDM==1),tp=0.04),
}
D={k:set(t.date) for k,t in TR.items()}
print('Trung ngay tin hieu N1/N3 voi bo cu (% ngay cua N co trong KB):')
for nk in ('N1 A','N3'):
    print(nk,{k:round(len(D[nk]&D[k])/max(1,len(D[nk]))*100) for k in ('KB1 F','KB2 F','KB3 A','KB4 A','KB5 A')})
alld=dates[dates>='2023-01-01']
def pf(keys,slots=5,label=''):
    allt=pd.concat([TR[k].assign(kb=f'{i}') for i,k in enumerate(keys)])
    held=[];taken=[];exp=pd.Series(0.0,index=dates)
    for _,x in allt.sort_values(['date','kb']).iterrows():
        held=[h for h in held if h[1]>=x.date]
        if len(held)>=slots or any(h[0]==x.sym for h in held): continue
        i0=dates.get_loc(x.date)+1; i1=min(i0+x.days-1,len(dates)-1); held.append((x.sym,dates[i1])); taken.append(x); exp.iloc[i0:i1+1]+=1/slots
    P=pd.DataFrame(taken); P['pnl']=P.ret/slots
    yrs=' | '.join(f"{y}: LN{P[P.date.dt.year==y].pnl.sum()*100:+.1f}% w{(P[P.date.dt.year==y].ret>0.0005).mean()*100:.0f}%" for y in (2023,2024,2025,2026))
    q4=P[P.date.dt.quarter==4]
    print(f"{label:40s} {yrs} | Q4: LN{q4.pnl.sum()*100:+.1f}% w{(q4.ret>0.0005).mean()*100:.0f}% | von dung TB {exp[alld].mean()*100:.0f}% | worst {P.ret.min()*100:.1f}%")
print('\nDanh muc 5 vi the x 20% von (cong don, khong lai kep), T+2 chi ban buoi chieu, san trang khong ban duoc:')
pf(['KB1 F','KB2 F','KB3 A','KB4 A','KB5 A'],label='Bo cu (KB1-2 F, KB3-5 A)')
pf(['KB1 F','KB2 F','KB4 A','N1 A'],label='KB1 KB2 KB4 + N1')
pf(['KB1 F','KB2 F','KB4 A','N1 A','N3'],label='KB1 KB2 KB4 N1 + N3')
pf(['KB1 F','KB2 F','KB4 A','N1 A','N3 tp4'],label='KB1 KB2 KB4 N1 + N3 tp4')
pf(['N3'],label='Chi N3')
yd=lambda y: dates[dates.year==y]
print('Chi so binh quan deu (mua-giu):',' | '.join(f"{y}: {(MKT[yd(y)[-1]]/MKT[yd(y)[0]]-1)*100:+.1f}%" for y in (2023,2024,2025,2026)))
