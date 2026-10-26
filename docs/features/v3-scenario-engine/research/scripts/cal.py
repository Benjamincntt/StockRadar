import sys; sys.path.insert(0,sys.argv[1]); from bt import *
x=pd.DataFrame({'r':mret},index=dates); x=x[x.index>='2023-01-01']
mo=x.index.to_period('M'); x['tdm']=x.groupby(mo).cumcount(); x['left']=x.groupby(mo).cumcount(ascending=False); x['y']=x.index.year
print('Loi nhuan TB/ngay (%) cua chi so binh quan deu theo so phien con lai den cuoi thang:')
print((x.groupby(['left','y']).r.mean().unstack()*100).round(2).head(8).to_string())
print('\ntheo phien thu may trong thang:'); print((x.groupby(['tdm','y']).r.mean().unstack()*100).round(2).head(6).to_string())
# chiến lược thị trường: giữ từ 5 phiên cuối tháng tới hết phiên 2 tháng sau
w=(x.left<=4)|(x.tdm<=1); print('\nCua so 5 phien cuoi + 2 phien dau thang: TB/ngay',round(x[w].r.mean()*100,3),'% ; ngoai cua so',round(x[~w].r.mean()*100,3),'%')
for y in (2023,2024,2025,2026): z=x[x.y==y]; ww=w[x.y==y]; print(y,'trong',round(z[ww].r.mean()*100,3),'ngoai',round(z[~ww].r.mean()*100,3),' ty le ngay tang trong cua so',round((z[ww].r>0).mean()*100))
