import sys; sys.path.insert(0,sys.argv[1])
exec(open(sys.argv[1]+'/newkb.py').read().split("print('### N1")[0])
from real2 import simulate_real2
def run(name,sig,**kw):
    t=simulate_real2(sig,**kw); s=summarize(t)
    print(f"{name:58s} n{s['n']:4d} wIS{s['winIS']:5} wOOS{s['winOOS']:5} wQ4{s['winQ4']:5} minY{s['winMinNam']:5} hv{s['hoavon']:5} avg{s['avg']:+6.2f} loss{s['avgLoss']:7} pf{s['pf']:5} worst{s['worst']:6} hold{s['hold']}")
    return t
nxt=lambda cond: cond.shift(1).fillna(False).astype(bool)   # điều kiện đúng lúc đóng cửa hôm trước -> bán ATO hôm nay
print('### N3 Don dong tien dau thang: thoi diem khop lenh')
n3=topk((LEFT==5)&(TV>30),RS63,5)
run('N3 mua mo cua, ban ATC phien 2 thang sau (goc)',n3,hold=12,exit_rule=(TDM==1))
run('N3 mua ATC, ban ATC phien 2',n3,hold=12,exit_rule=(TDM==1),entry='atc')
run('N3 mua ATC, ban ATO phien 2',n3,hold=12,exit_open_rule=(TDM==1),entry='atc')
run('N3 mua ATC, ban ATO phien 3',n3,hold=12,exit_open_rule=(TDM==2),entry='atc')
run('N3 mua ATC phien -7, ban ATO phien 3',topk((LEFT==6)&(TV>30),RS63,5),hold=13,exit_open_rule=(TDM==2),entry='atc')
print('\n### N4 TT giam >=1.5% (thi truong binh thuong), bluechip giam manh nhat')
dn=bc(mret<-0.015)&bc(BREADTH50>=0.35)
n4=topk(dn&(TV>100)&(C>MA[200]),-R,5)
for en in ('open','atc'):
    run(f'N4 {en} | ban khi dong cua > MA10',n4,hold=15,exit_rule=(C>MA[10]),entry=en)
    run(f'N4 {en} | dong cua > MA10 -> ATO hom sau',n4,hold=15,exit_open_rule=nxt(C>MA[10]),entry=en)
    run(f'N4 {en} | F MA10',n4,hold=15,exit_level=LV[10],exit_above_entry=True,entry=en)
    run(f'N4 {en} | giu 10 phien, ban ATO',n4,hold=10,expire_open=True,entry=en)
n4b=topk(dn&(TV>50)&(C>MA[200]),-R,5)
run('N4b TV>50 atc | F MA10',n4b,hold=15,exit_level=LV[10],exit_above_entry=True,entry='atc')
n4c=topk(dn&(TV>100)&(C>MA[50]),-R,5)
run('N4c tren MA50 atc | F MA10',n4c,hold=15,exit_level=LV[10],exit_above_entry=True,entry='atc')
print('\n### Ap ATC/ATO cho bo cu')
n1=topk((FLOOR20>=1)&~FLOOR&(SECR20<=-0.12)&(TV>10)&(LEFT<=6),-SECR20,5)
for kb,sig,ma_n,hd in (('KB1',SIG['KB1'][0],10,15),('KB2',SIG['KB2'][0],10,15),('KB4',SIG['KB4'][0],5,10),('N1',n1,10,15)):
    run(f'{kb} open | dong cua>MA{ma_n}',sig,hold=hd,exit_rule=(C>MA[ma_n]))
    run(f'{kb} atc  | dong cua>MA{ma_n} -> ATO hom sau',sig,hold=hd,exit_open_rule=nxt(C>MA[ma_n]),entry='atc')
    run(f'{kb} open | F MA{ma_n}',sig,hold=hd,exit_level=LV[ma_n],exit_above_entry=True)
    run(f'{kb} atc  | F MA{ma_n}',sig,hold=hd,exit_level=LV[ma_n],exit_above_entry=True,entry='atc')
