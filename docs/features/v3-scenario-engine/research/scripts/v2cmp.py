import sys,pickle; sys.path.insert(0,sys.argv[1])
exec(open(sys.argv[1]+'/mix2.py',encoding='utf-8').read().split("print('\nDanh muc")[0])
OUT=pickle.load(open(sys.argv[1]+'/v2_trades.pkl','rb'))
v2=OUT[('mid',True)]; v2=v2[v2.date>='2023-01-01']
real=pd.read_csv(sys.argv[1]+'/prod_kq.txt',sep='|',skiprows=[1]); real=real[real.LoaiKichBan.isin([1,2,3])&real.PhanTramLoiNhuan.notna()&(real.KetQuaDoLuong!='Huy')]
real['d']=(pd.to_datetime(real.TG)+pd.Timedelta(hours=7)).dt.normalize()
mm=real.merge(v2,left_on=['Symbol','d'],right_on=['sym','date'])
print('So lai/lo tung lenh: that vs mo phong (lenh trung ngay, da dong):')
print(mm[['Symbol','d','GiaVaoThucTe','entry','PhanTramLoiNhuan','LyDoThoatHet','why']].assign(mo_phong=(mm.ret*100).round(2)).drop(columns=[]).to_string(index=False))
print('Lenh that da dong:',len(real),' thang',(real.PhanTramLoiNhuan>0.05).sum(),' TB',round(real.PhanTramLoiNhuan.mean(),2),'%')
TR['V2']=v2.rename(columns={'kb':'kb_'})[['date','sym','entry','days','ret']]
alld=dates[dates>='2023-01-01']
print('\nDanh muc 5 vi the x 20% von (cong don):')
pf(['V2'],'V2 hien tai (3 kich ban mua)')
pf(['KB1','KB2','KB4t','N1t','N3t'],'P1 de xuat (uu tien lai)')
pf(['KB1','KB2','KB4t','N1t','N4'],'P3 de xuat (uu tien thang)')
yd=lambda y: dates[dates.year==y]
print('Chi so binh quan deu:',' | '.join(f"{y}: {(MKT[yd(y)[-1]]/MKT[yd(y)[0]]-1)*100:+.1f}%" for y in (2023,2024,2025,2026)))
