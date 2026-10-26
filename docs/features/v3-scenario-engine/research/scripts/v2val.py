import sys; sys.path.insert(0,sys.argv[1]); from v2sim import *
real=pd.read_csv(sys.argv[1]+'/prod_kq.txt',sep='|',skiprows=[1])
real=real[real.LoaiKichBan.isin([1,2,3])].copy()
real['d']=(pd.to_datetime(real.TG)+pd.Timedelta(hours=7)).dt.normalize()
real['kb']=real.LoaiKichBan.map({1:'NoHuongLen',2:'HoiHoTro',3:'QuetThanhKhoan'})
tr=triggers('mid')
T=pd.DataFrame([(dates[i],syms[j],nm,p) for (i,j,nm,p,*_) in tr],columns=['d','sym','kb','p'])
win=(T.d>='2026-09-30')&(T.d<='2026-10-22')
Tw=T[win]
rw=real[real.d<='2026-10-22']
m=rw.merge(Tw,left_on=['Symbol','d'],right_on=['sym','d'],how='left',suffixes=('','_sim'))
print('Lenh that (30/09-22/10):',len(rw),' mo phong bat trung (cung ma, cung ngay):',m.p.notna().sum())
print(m[['Symbol','d','kb','GiaVaoThucTe','p','kb_sim']].to_string(index=False))
print('\nMo phong ban ra trong cung giai doan:',len(Tw),'lenh; theo loai:',Tw.kb.value_counts().to_dict())
fs=(F_HHT|F_NHL|F_QTK); print('So FORMING/ngay TB (mo phong) thang 10:',round(F_HHT[(dates>='2026-10-01')].sum(axis=1).mean(),1),'HHT,',round(F_NHL[(dates>='2026-10-01')].sum(axis=1).mean(),2),'NHL,',round(F_QTK[(dates>='2026-10-01')].sum(axis=1).mean(),2),'QTK')
extra=Tw.merge(rw,left_on=['sym','d'],right_on=['Symbol','d'],how='left'); extra=extra[extra.Symbol.isna()]
print('Mo phong co ma he thong that khong co:',len(extra)); print(extra[['d','sym','kb_x','p']].head(25).to_string(index=False))
