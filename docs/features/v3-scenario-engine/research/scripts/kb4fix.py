import sys; exec(open(sys.argv[1]+'/pfeng.py',encoding='utf-8').read())
md=bc(MKT/MKT_MA50-1); base3=(LEFT==5)&(TV>30)
t=simulate_real2(topk(base3&~((md>-0.015)&(md<0.015)),RS63,5),hold=12,exit_open_rule=(TDM==2),entry='atc'); N3F=('N3t',tag(t[t.date>='2023-01-01'],'N3t'))
def split(E,T,label):
    a=E[E.index<=IS[1]]; b=E[E.index>IS[1]]; k4=T[T.kb=='KB4t']
    print(f"{label:44s} trong mau {(a.iloc[-1]-1)*100:+5.0f}% (sut {(a/a.cummax()-1).min()*100:5.1f}%) | ngoai mau {(b.iloc[-1]/a.iloc[-1]-1)*100:+5.0f}% (sut {(b/b.cummax()-1).min()*100:5.1f}%) | KB4: n{len(k4)} thang {(k4.ret>0.0005).mean()*100:.0f}% TB {k4.ret.mean()*100:+.2f}% te nhat {k4.ret.min()*100:.1f}%")
sig4=SIG['KB4'][0]; AT=ATR.values
def kb4(**kw):
    x=simulate_real2(sig4,hold=10,exit_open_rule=nxt(C>MA[5]),entry='atc',atr_ref=AT,**kw); return ('KB4t',tag(x[x.date>='2023-01-01'],'KB4t'))
def P(k4,**kw): return run_pf([BASE[0],BASE[1],k4,BASE[3],N3F],5,**kw)
split(*P(BASE[2]),'P1+ (KB4 goc)')
for m in (2,2.5,3,4): split(*P(kb4(stop_close_atr=m)),f'KB4 cat lo dong cua < gia vao - {m} ATR -> ATO')
for p in (0.08,0.10,0.12): split(*P(kb4(stop_close_pct=p)),f'KB4 cat lo dong cua < -{int(p*100)}% -> ATO')
ATRP=ATR/C
for tgt in (0.04,0.05):
    fn=lambda x,tgt=tgt: float(np.clip(tgt/ATRP.values[dates.get_loc(x.date),syms.get_loc(x.sym)],0.5,1.0)) if x.kb=='KB4t' else 1.0
    split(*P(BASE[2],size_fn=fn),f'KB4 giam ti trong ma bien dong (ATR muc tieu {int(tgt*100)}%)')
