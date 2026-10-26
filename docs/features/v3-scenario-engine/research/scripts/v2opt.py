import sys; sys.path.insert(0,sys.argv[1]); import v2sim as m
from sigs import summarize
FH,FN,FQ=m.pha1_placeholder(); m.F_HHT=FH; m.F_NHL=FN
tr=m.triggers('mid')
for opt in (False,True):
    for d2 in (True,False):
        t=m.sell_v2(tr,use_diem2=d2,optimistic=opt); s=summarize(t)
        print(f"lac quan={opt!s:5} diem2={d2!s:5} n{s['n']} wIS{s['winIS']} wOOS{s['winOOS']} wQ4{s['winQ4']} minY{s['winMinNam']} avg{s['avg']:+.2f} pf{s['pf']} worst{s['worst']}")
