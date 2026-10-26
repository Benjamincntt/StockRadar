import sys; sys.path.insert(0,sys.argv[1]); from bt import *
pd.set_option('display.width',250)
def run(name,**kw):
    t=simulate(**kw,name=name); r=report(t)
    print(f"\n=== {name}"); print(r[['n','days','win','avg','pf','exc','hold']].T.to_string()); return t
EX={'ma10':dict(hold=15,exit_rule=(C>MA[10])),'ma5':dict(hold=10,exit_rule=(C>MA[5])),'tp5sl8':dict(hold=15,tp=0.05,sl=0.08)}
mk5=bc(MKT/MKT.shift(5)-1); md50=bc(MKT/MKT_MA50-1); br=bc(BREADTH50)
weak=-RS126
# 2 nhịp chỉnh vừa (không tới mức hoảng loạn)
for e in ('ma10','ma5'):
    run(f'2 mktDrop4 weakRS | {e}',sig=topk((mk5<-0.04)&(TV>30),weak,5),**EX[e])
# 3 sau hoảng loạn, chỉ số lấy lại MA20
recent_panic=PANIC.rolling(20).max().fillna(0).astype(bool)
ftd=bc((MKT>MKT_MA20)&(MKT.shift()<=MKT_MA20.shift()))&recent_panic
for e in ('ma10','tp5sl8'):
    run(f'3 FTD strongBounce | {e}',sig=topk(ftd&(TV>30),C/C.rolling(10).min(),5),**EX[e])
    run(f'3 FTD hold15 RS63',sig=topk(ftd&(TV>30),RS63,5),hold=15,sl=0.08)
# 4 bán tháo riêng lẻ khi thị trường ổn
idio=(RET20<-0.12)&(TV>30)&(br>0.5)
for e in ('ma10','ma5'): run(f'4 idio oversold mkt ok | {e}',sig=topk(idio,-RET20,5),**EX[e])
# 5 chạm sàn với vol lớn
floor=(R<=-(LIM-0.005))&(V>2*VMA20)&(TV>20)
for e in ('ma5','ma10','tp5sl8'): run(f'5 floor washout | {e}',sig=topk(floor,TV,5),**EX[e])
floor2=floor&(C>MA[200])
run('5b floor washout >MA200 | ma5',sig=topk(floor2,TV,5),**EX['ma5'])
# 6 RSI14<35 trên MA200
s6=(RSI14<35)&(C>MA[200])&(TV>20)
for e in ('ma10','ma5'): run(f'6 rsi35 >ma200 | {e}',sig=topk(s6,-RSI14,5),**EX[e])
# 7 cả ngành bị bán quá mức
sec=meta.sector.reindex(syms).fillna('')
secret=RET20.where(UNI).T.groupby(sec).transform('median').T
s7=(secret<-0.10)&(RET20<secret)&(TV>20)
for e in ('ma10','ma5'): run(f'7 sector oversold | {e}',sig=topk(s7,-RET20,5),**EX[e])
