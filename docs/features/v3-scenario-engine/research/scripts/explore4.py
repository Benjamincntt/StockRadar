import sys; sys.path.insert(0,sys.argv[1]); from bt import *
pd.set_option('display.width',250)
def run(name,**kw):
    t=simulate(**kw,name=name); r=report(t)
    print(f"\n=== {name}"); print(r[['n','days','win','avg','pf','exc','hold']].T.to_string()); return t
EX={'ma10':dict(hold=15,exit_rule=(C>MA[10])),'ma5':dict(hold=10,exit_rule=(C>MA[5])),'tp3sl10':dict(hold=20,tp=0.03,sl=0.10)}
mk5=bc(MKT/MKT.shift(5)-1); md50=bc(MKT/MKT_MA50-1); br=bc(BREADTH50); weak=-RS126
sec=meta.sector.reindex(syms).fillna('')
secret=RET20.where(UNI).T.groupby(sec).transform('median').T
for b in (0.35,0.45):
    s=(RSI14<35)&(C>MA[200])&(TV>20)&(br<b)
    for e in ('ma10','ma5'): run(f'6 rsi35>ma200 breadth<{b} | {e}',sig=topk(s,-RSI14,5),**EX[e])
for b in (0.35,0.45):
    s=(secret<-0.10)&(RET20<secret)&(TV>20)&(br<b)
    for e in ('ma10','ma5'): run(f'7 sector oversold breadth<{b} | {e}',sig=topk(s,-RET20,5),**EX[e])
s=(secret<-0.10)&(RET20<secret)&(RET20>secret-0.08)&(TV>20)
run('7c sector-driven (not idio) | ma5',sig=topk(s,-RET20,5),**EX['ma5'])
s=(mk5<-0.04)&(br<0.35)&(TV>30)
for e in ('ma10','ma5'): run(f'2 mktDrop4&breadth<.35 | {e}',sig=topk(s,weak,5),**EX[e])
# dip trong xu hướng thị trường tăng dài hạn: chỉ số > MA100 chỉ số, giảm 3 phiên liên tiếp, mua bluechip
MKT_MA100=MKT.rolling(100).mean(); dn3=(mret<0)&(mret.shift()<0)&(mret.shift(2)<0)
s=bc((MKT>MKT_MA100)&dn3)&(TV>50)&(C>MA[50])
for e in ('ma5','ma10'): run(f'8 mkt-uptrend 3-down dip bluechip | {e}',sig=topk(s,TV,5),**EX[e])
# panic biến thể
for e in ('tp3sl10',): run(f'1 panic weakRS | {e}',sig=topk(PANIC&(TV>30),weak,5),**EX[e])
run('1 panic bluechip TV>100 weakRS | ma10',sig=topk(PANIC&(TV>100),weak,5),**EX['ma10'])
run('1 panic weakRS top10 | ma10',sig=topk(PANIC&(TV>30),weak,10),**EX['ma10'])
