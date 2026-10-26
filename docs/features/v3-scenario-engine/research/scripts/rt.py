import sys; exec(open(sys.argv[1]+'/size.py',encoding='utf-8').read().split("split2(*run_pf(PARTS,5,park_r=D45")[0])
sig4=SIG['KB4'][0]; n1=topk((FLOOR20>=1)&~FLOOR&(SECR20<=-0.12)&(TV>10)&(LEFT<=6),-SECR20,5)
LV[20]=ma_touch_level(20)
def T_(sig,kb,mode,**kw):
    x=simulate_real2(sig,entry=mode,**kw); return (kb,tag(x[x.date>='2023-01-01'],kb,mode))
FX=lambda n,hd: dict(hold=hd,exit_level=LV[n],exit_above_entry=True)
BX=lambda n,hd: dict(hold=hd,exit_level=LV[n])
mult=lambda x: 1.5 if x.kb in ('KB1','KB2','N1t') else 1.0
def row(parts,label):
    E,T=run_pf(parts,5,park_r=D45,park_cost=(0,0),size_fn=mult); split2(E,T,label)
    for kb in ('KB4t','N1t'):
        x=T[T.kb==kb]; print(f"      {kb}: n{len(x)} thang {(x.ret>0.0005).mean()*100:.0f}% TB {x.ret.mean()*100:+.2f}% te nhat {x.ret.min()*100:.1f}%")
row(PARTS,'P1++ (KB4/N1 dong cua -> ATO)')
for e4 in ('atc','open'):
    for e1 in ('atc','open'):
        row([BASE[0],BASE[1],T_(sig4,'KB4t',e4,**FX(5,10)),T_(n1,'N1t',e1,**FX(10,15)),N3F],f'realtime F: KB4 mua {e4}, N1 mua {e1}')
row([BASE[0],BASE[1],T_(sig4,'KB4t','atc',**FX(10,10)),T_(n1,'N1t','atc',**FX(20,15)),N3F],'realtime F: KB4 cham MA10, N1 cham MA20 (atc)')
row([BASE[0],BASE[1],T_(sig4,'KB4t','atc',**BX(5,10)),T_(n1,'N1t','atc',**BX(10,15)),N3F],'realtime cham MA (khong can > gia vao), atc')
row([BASE[0],BASE[1],N3F],'Bo KB4 va N1')
