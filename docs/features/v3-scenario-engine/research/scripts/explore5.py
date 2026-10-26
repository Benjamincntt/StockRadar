import sys; sys.path.insert(0,sys.argv[1]); from bt import *
pd.set_option('display.width',250)
def run(name,**kw):
    t=simulate(**kw,name=name); r=report(t)
    print(f"\n=== {name}"); print(r[['n','days','win','avg','pf','exc','hold']].T.to_string()); return t
EX={'ma10':dict(hold=15,exit_rule=(C>MA[10])),'ma5':dict(hold=10,exit_rule=(C>MA[5])),'ma20':dict(hold=20,exit_rule=(C>MA[20]))}
mk5=bc(MKT/MKT.shift(5)-1); md50=bc(MKT/MKT_MA50-1); br=bc(BREADTH50); weak=-RS126
SD20=C.rolling(20).std()
s=(C<MA[20]-2*SD20)&(TV>100)&(md50<-0.03)
for e in ('ma5','ma10'): run(f'5 bluechip <BB-2sd & idx<MA50-3% | {e}',sig=topk(s,-(C/MA[20]),5),**EX[e])
s=(md50<-0.05)&(TV>100)
for e in ('ma10',): run(f'4 idx<MA50-5% bluechip lowRSI | {e}',sig=topk(s,-RSI14,5),**EX[e])
s=(md50<-0.05)&(TV>30)&(C>MA[200])
run('4b idx<MA50-5% & stock>MA200 lowRSI | ma10',sig=topk(s,-RSI14,5),**EX['ma10'])
print('\n##### Độ nhạy KB1')
for b,m in [(0.15,-0.09),(0.20,-0.07),(0.25,-0.05),(0.30,-0.05)]:
    P=bc((BREADTH50<b)|(MKT/MKT_MA50-1<m))
    for e in ('ma5','ma10','ma20'):
        t=simulate(sig=topk(P&(TV>30),weak,5),**EX[e]); r=report(t,False)
        print(f'breadth<{b} or idx<{m} {e}:',' | '.join(f"{i}: n{int(x.n)} w{x.win} a{x.avg} pf{x.pf}" for i,x in r.iterrows()))
# số đợt độc lập
def episodes(sig):
    d=pd.Series(sig.any(axis=1)); d=d[d].index; 
    if len(d)==0: return 0
    gaps=pd.Series(d).diff().dt.days.fillna(99); return int((gaps>10).sum())
print('\nso dot PANIC:',episodes(PANIC&(TV>30)), ' dot breadth<.35:',episodes(br<0.35))
print('\n##### Trạng thái hiện tại')
print(pd.DataFrame({'breadth50':BREADTH50,'idx_vs_MA50%':(MKT/MKT_MA50-1)*100,'idx_5d%':(MKT/MKT.shift(5)-1)*100}).tail(12).round(2))
