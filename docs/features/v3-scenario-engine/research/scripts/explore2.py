import sys; sys.path.insert(0,sys.argv[1]); from bt import *
pd.set_option('display.width',250)
def run(name,**kw):
    t=simulate(**kw,name=name); r=report(t)
    print(f"\n=== {name}"); print(r.to_string()); return t
liq=TV
# KB-A hoảng loạn: chọn mã thanh khoản lớn nhất
for ex_name,ex_kw in [('hold10',dict(hold=10)),('ma10',dict(hold=15,exit_rule=(C>MA[10]))),('tp6sl8',dict(hold=15,tp=0.06,sl=0.08)),('tp4sl8',dict(hold=15,tp=0.04,sl=0.08))]:
    run(f'A panic top5 TV | {ex_name}',sig=topk(PANIC,TV,5),**ex_kw)
    run(f'A panic top5 weakRS | {ex_name}',sig=topk(PANIC&(TV>30),-RS126,5),**ex_kw)
# KB-B mã thanh khoản giảm sâu 20 phiên
B=(RET20<-0.12)&(TV>30)
for ex_name,ex_kw in [('hold10',dict(hold=10)),('ma10',dict(hold=15,exit_rule=(C>MA[10]))),('tp6sl8',dict(hold=15,tp=0.06,sl=0.08))]:
    run(f'B liqOversold top5 | {ex_name}',sig=topk(B,-RET20,5),**ex_kw)
    run(f'B +green | {ex_name}',sig=topk(B&(C>O)&(C>C.shift()),-RET20,5),**ex_kw)
