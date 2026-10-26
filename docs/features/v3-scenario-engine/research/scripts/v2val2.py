import sys; sys.path.insert(0,sys.argv[1]); from v2sim import *
f=pd.read_csv(sys.argv[1]+'/prod_forming.txt',sep='|',header=None,names=['sym','kb','d','tt','bc','ht'])
f['d']=pd.to_datetime(f.d); f=f[(f.d<='2026-10-22')&(f.d>='2026-10-01')]
FM={1:F_NHL,2:F_HHT,3:F_QTK}; names={1:'NoHuongLen',2:'HoiHoTro',3:'QuetThanhKhoan'}
for kb in (1,2,3):
    real=f[(f.kb==kb)&(f.ht==1)][['sym','d']].drop_duplicates()
    realset=set(zip(real.sym,real.d))
    Fm=FM[kb]; sim=Fm[(dates>='2026-10-01')&(dates<='2026-10-22')]
    simset={(s,d) for d,row in sim.iterrows() for s in row.index[row.values]}
    inter=realset&simset
    print(f"{names[kb]}: FORMING that {len(realset)}, mo phong {len(simset)}, trung {len(inter)} ({len(inter)/max(1,len(realset))*100:.0f}% cua that)")
    # bối cảnh
    rb=set(zip(f[(f.kb==kb)&(f.bc==1)].sym,f[(f.kb==kb)&(f.bc==1)].d))
    print('   (boi canh that',len(rb),')')
    miss=list(realset-simset)[:8]; print('   that co, mo phong khong:',miss)
    ext=list(simset-realset)[:8]; print('   mo phong co, that khong:',ext)
