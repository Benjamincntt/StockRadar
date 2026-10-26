import sys; sys.path.insert(0,sys.argv[1]); from sigs import *
pd.set_option('display.width',250)
mo=pd.Series(dates.to_period('M'),index=dates); TDM=bc(mo.groupby(mo).cumcount().astype(float)); LEFT=bc(mo.groupby(mo).cumcount(ascending=False).astype(float))
FLOOR=(R<=-(LIM-0.005)); FLOOR20=FLOOR.astype(int).rolling(20).sum()
SECR20=secret; SECR5=R.where(UNI).T.groupby(sec).transform('median').T  # placeholder
bdrop=bc(BREADTH50-BREADTH50.shift(5)); md50=bc(MKT/MKT_MA50-1)
def run(name,sig,**kw):
    t=simulate_real(sig,t2='pm_close',**kw); s=summarize(t)
    print(f"{name:52s} n{s['n']:4d} wIS{s['winIS']:5} wOOS{s['winOOS']:5} wQ4{s['winQ4']:5} minY{s['winMinNam']:5} hv{s['hoavon']:5} avg{s['avg']:+6.2f} loss{s['avgLoss']:6} pf{s['pf']:5} worst{s['worst']:6} hold{s['hold']}")
    return t
A10=lambda hd=15: dict(hold=hd,exit_rule=(C>MA[10])); F10=lambda hd=15: dict(hold=hd,exit_level=LV[10],exit_above_entry=True)
print('### N1 Giai chap can kiet')
base=(FLOOR20>=1)&~FLOOR&(SECR20<=-0.065)&(TV>10)
for nm,s in [('floor20>=1 & nganh<=-6.5%',base),('+ nganh<=-12%',base&(SECR20<=-0.12)),('+ con <=6 phien toi cuoi thang',base&(LEFT<=6)),
             ('+ nganh<=-12% & cuoi thang',base&(SECR20<=-0.12)&(LEFT<=6)),('+ floor20>=2',base&(FLOOR20>=2))]:
    for en,ek in (('A',A10()),('F',F10())): run(f'{nm} | {en}',topk(s,-SECR20,5),**ek)
print('\n### N2 Rung lac cuoi thang trong xu huong tang')
s2=(md50>0.017)&(bdrop<-0.09)&(LEFT<=5)&(TV>30)
toexit=(TDM==1)
for nm,sc in (('RS63 cao',RS63),('thanh khoan lon',TV),('giam 5 phien manh nhat',-(C/C.shift(5)))):
    run(f'{nm} | ban phien 2 thang sau',topk(s2,sc,5),hold=12,exit_rule=toexit)
    run(f'{nm} | F MA10',topk(s2,sc,5),**F10())
print('\n### N3 Don dong tien dau thang (mua 5 phien cuoi, ban het phien 2 thang sau)')
s3=(LEFT==5)
for nm,cond,sc in (('top5 thanh khoan',TV>0,TV),('top5 thanh khoan, tren MA50',C>MA[50],TV),('top5 RS63',TV>30,RS63),('top5 RS63 tren MA50',(C>MA[50])&(TV>30),RS63),
                   ('top10 thanh khoan',TV>0,TV),('top5 giam 20 phien manh nhat',TV>30,-RET20)):
    k=10 if 'top10' in nm else 5
    run(f'{nm}',topk(s3&cond,sc,k),hold=12,exit_rule=toexit)
run('top5 thanh khoan + thi truong tren MA50',topk(s3&(md50>0),TV,5),hold=12,exit_rule=toexit)
run('top5 thanh khoan + thi truong duoi MA50',topk(s3&(md50<=0),TV,5),hold=12,exit_rule=toexit)
