import sys,pickle; exec(open(sys.argv[1]+'/rt.py',encoding='utf-8').read().split("row(PARTS,")[0])
k4=T_(sig4,'KB4t','atc',**FX(10,10)); n1r=T_(n1,'N1t','atc',**FX(20,15))
PARTS_RT=[BASE[0],BASE[1],k4,n1r,N3F]
from sigs import summarize
print('Tung kich ban (moi tin hieu, chua qua danh muc):')
for nm,t in PARTS_RT:
    s=summarize(t); print(f"  {nm:5s} n{s['n']:4d} thang trong mau {s['winIS']}% / ngoai mau {s['winOOS']}% | Q4 {s['winQ4']}% | nam thap nhat {s['winMinNam']}% | TB {s['avg']:+.2f}% | lo TB {s['avgLoss']}% | tong lai/tong lo {s['pf']} | te nhat {s['worst']}% | giu {s['hold']} phien")
for lab,kw in (('khong gui tien',dict()),('gui tien 3%',dict(park_r=dep(0.03),park_cost=(0,0))),('gui tien 4.5%',dict(park_r=D45,park_cost=(0,0)))):
    E,T=run_pf(PARTS_RT,5,size_fn=mult,**kw)
    m=E.resample('ME').last(); mo=(m/m.shift(1).fillna(1.0)-1).min()*100
    split2(E,T,f'P1++ realtime, {lab}'); print(f"      thang te nhat {mo:.1f}% | thang {(T.ret>0.0005).mean()*100:.0f}% | lenh {len(T)} | lenh te nhat mat {min(r.ret*mult(r)/5 for _,r in T.iterrows())*100:.1f}% von")
    if lab=='gui tien 4.5%': pickle.dump((E,T),open(sys.argv[1]+'/p1pp_rt.pkl','wb'))
