import sys; exec(open(sys.argv[1]+'/idle.py',encoding='utf-8').read().split("split2(*run_pf(PARTS,5),")[0])
D45=dep(0.045)
split2(*run_pf(PARTS,5,park_r=D45,park_cost=(0,0)),'P1+ + gui tien 4.5%')
for mult,kbs in ((1.5,{'KB1','KB2'}),(2.0,{'KB1','KB2'}),(1.5,{'KB1','KB2','N1t'}),(0.5,{'KB4t'}),(0.75,{'N3t'})):
    fn=lambda x,mult=mult,kbs=kbs: mult if x.kb in kbs else 1.0
    E,T=run_pf(PARTS,5,park_r=D45,park_cost=(0,0),size_fn=fn)
    split2(E,T,f"ti trong x{mult} cho {','.join(sorted(kbs))}")
# 6 vị thế nhưng KB1/KB2 gấp đôi
fn=lambda x: 2.0 if x.kb in ('KB1','KB2') else 1.0
split2(*run_pf(PARTS,6,park_r=D45,park_cost=(0,0),size_fn=fn),'6 vi the, KB1/KB2 x2')
