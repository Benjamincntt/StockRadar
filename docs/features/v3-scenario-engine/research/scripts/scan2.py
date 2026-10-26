import sys,pandas as pd,numpy as np
S=sys.argv[1]; pd.set_option('display.width',250)
d=pd.read_pickle(S+'/feat2.pkl'); d=d[d.date>='2023-01-01']
d['per']=np.where(d.date<='2025-06-30','IS','OOS')
N=d[d.breadth>=0.35]   # thị trường bình thường/khoẻ (KB1-5 gần như không báo)
print('Thi truong binh thuong: rows',len(N),'ngay',N.date.nunique(),' base win10 IS/OOS',N.groupby('per').y10.apply(lambda s:round((s>0).mean()*100,1)).to_dict(),
      ' avg',N.groupby('per').y10.apply(lambda s:round(s.mean()*100,2)).to_dict())
def tab(k,q=10,col='y10'):
    x=N[[k,col,'per','date']].dropna()
    b=pd.qcut(x[k].rank(method='first'),q,labels=False) if x[k].nunique()>q else x[k]
    g=x.groupby([b,'per'])[col].agg(win=lambda s:(s>0).mean()*100,avg=lambda s:s.mean()*100).unstack()
    lo=x.groupby(b)[k].min()
    o=pd.DataFrame({'lo':lo.round(3),'wIS':g['win']['IS'].round(1),'wOOS':g['win']['OOS'].round(1),'aIS':g['avg']['IS'].round(2),'aOOS':g['avg']['OOS'].round(2)})
    spread=(o.aIS.max()-o.aIS.min())
    return o,spread
res=[]
for k in [c for c in d.columns if c not in ('date','sym','y5','y10','per')]:
    o,s=tab(k); 
    # đo tính nhất quán: tương quan thứ hạng giữa IS và OOS theo bucket
    cons=np.corrcoef(o.aIS.fillna(0),o.aOOS.fillna(0))[0,1]
    res.append((k,round(s,2),round(cons,2)))
r=pd.DataFrame(res,columns=['feat','spreadIS','corr_IS_OOS']).sort_values('corr_IS_OOS',ascending=False); print(r.to_string(index=False))
for k in r.head(8).feat: print('\n---',k); print(tab(k)[0].T.to_string())
for k in ('tdays_left','tday_month','weekday','month'): print('\n---',k); print(tab(k)[0].T.to_string())
