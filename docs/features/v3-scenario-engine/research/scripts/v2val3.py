import sys; sys.path.insert(0,sys.argv[1]); from v2sim import *
f=pd.read_csv(sys.argv[1]+'/prod_forming.txt',sep='|',header=None,names=['sym','kb','d','tt','bc','ht'])
f['d']=pd.to_datetime(f.d); f=f[(f.d<='2026-10-22')&(f.d>='2026-10-01')]
# Giả thuyết: lúc Pha 1 chạy, lịch sử có thêm nến hôm nay giữ chỗ (O=H=L=C=giá tham chiếu, KL=0)
Cp=C.shift(1); 
E20p=E20.shift(1)*(1-k20)+k20*Cp; E50p=E50.shift(1)*(1-k50)+k50*Cp
dd=C.diff().shift(1)  # thay đổi tới hôm qua
gain=dd.clip(lower=0).rolling(13).sum(); loss=(-dd.clip(upper=0)).rolling(13).sum()
RSIp=100-100/(1+gain/loss)          # 13 thay đổi cũ + 1 thay đổi bằng 0
V3p=(V.shift(1)+V.shift(2))/3; V20p=V.shift(1).rolling(19).sum()/20
FH=((E20p>E50p)&((Cp-E20p).abs()/E20p<=0.03)&(RSIp>=40)&(RSIp<=50)&(V3p/V20p<0.8)&SOTUYEN.shift(1,fill_value=False))
real=f[(f.kb==2)&(f.ht==1)]; realset=set(zip(real.sym,real.d))
for nm,Fm in (('nen toi hom qua',F_HHT),('them nen giu cho',FH)):
    sim=Fm[(dates>='2026-10-01')&(dates<='2026-10-22')]
    simset={(s,d) for d,row in sim.iterrows() for s in row.index[row.values]}
    print(f"HoiHoTro {nm}: that {len(realset)}, mo phong {len(simset)}, trung {len(realset&simset)}")
