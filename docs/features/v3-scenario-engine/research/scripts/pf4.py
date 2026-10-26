import sys; exec(open(sys.argv[1]+'/pfeng.py',encoding='utf-8').read())
exec(open(sys.argv[1]+'/pf3.py',encoding='utf-8').read().split("split(*P(BASE[4][1]),'P1 goc')")[0].split("P=lambda")[0].split("md=bc(")[1].join(["md=bc(",""]) if False else "")
md=bc(MKT/MKT_MA50-1); base3=(LEFT==5)&(TV>30)
t=simulate_real2(topk(base3&~((md>-0.015)&(md<0.015)),RS63,5),hold=12,exit_open_rule=(TDM==2),entry='atc'); n3f=tag(t[t.date>='2023-01-01'],'N3t')
PARTS=BASE[:4]+[('N3t',n3f)]
ATRP=(ATR/C)
def split(E,T,label):
    a=E[E.index<=IS[1]]; b=E[E.index>IS[1]]
    print(f"{label:44s} trong mau {(a.iloc[-1]-1)*100:+5.0f}% (sut {(a/a.cummax()-1).min()*100:5.1f}%) | ngoai mau {(b.iloc[-1]/a.iloc[-1]-1)*100:+5.0f}% (sut {(b/b.cummax()-1).min()*100:5.1f}%) | thang {(T.ret>0.0005).mean()*100:.0f}% | lenh to nhat lam mat {(-T.ret.min())*100/5:.1f}% von")
split(*run_pf(PARTS,5),'P1 + loc N3 (5 vi the)')
for s_ in (4,6): split(*run_pf(PARTS,s_),f'P1 + loc N3 ({s_} vi the)')
for tgt in (0.025,0.03,0.035):
    fn=lambda x,tgt=tgt: float(np.clip(tgt/ATRP.values[dates.get_loc(x.date),syms.get_loc(x.sym)],0.5,1.0))
    split(*run_pf(PARTS,5,size_fn=fn),f'chia von theo bien dong (muc tieu ATR {tgt*100:.1f}%)')
for cap in (2,3): split(*run_pf(PARTS,5,kb_cap={'N3t':cap}),f'N3 toi da {cap} cho')
