import sys; sys.path.insert(0,sys.argv[1]); import v2sim as m
f=m.pd.read_csv(sys.argv[1]+'/prod_forming.txt',sep='|',header=None,names=['sym','kb','d','tt','bc','ht'])
f['d']=m.pd.to_datetime(f.d); f=f[(f.d<='2026-10-22')&(f.d>='2026-10-01')]
FH,FN,FQ=m.pha1_placeholder()
D=m.dates
for kb,a,b,nm in ((1,m.F_NHL,FN,'NoHuongLen'),(2,m.F_HHT,FH,'HoiHoTro'),(3,m.F_QTK,FQ,'QuetThanhKhoan')):
    real=f[(f.kb==kb)&(f.ht==1)]; rs=set(zip(real.sym,real.d))
    for lab,Fm in (('toi hom qua',a),('giu cho',b)):
        sim=Fm[(D>='2026-10-01')&(D<='2026-10-22')]; ss={(s,d) for d,row in sim.iterrows() for s in row.index[row.values]}
        print(f"{nm:15s} {lab:12s} that {len(rs):3d} mo phong {len(ss):3d} trung {len(rs&ss):3d}")
