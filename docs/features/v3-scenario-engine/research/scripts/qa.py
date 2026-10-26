import sys,pandas as pd,numpy as np
S=sys.argv[1]; df=pd.read_pickle(S+'/bars.pkl'); m=pd.read_pickle(S+'/meta.pkl')
last=df.groupby('sym').date.max(); print('last date dist'); print(last.dt.to_period('M').value_counts().sort_index().tail(8))
C=df.pivot(index='date',columns='sym',values='c'); r=C.pct_change(fill_method=None)
ex=m.set_index('sym').ex
for e,lim in [('HOSE',.075),('HNX',.105),('UPCOM',.16)]:
    cols=[s for s in C.columns if ex.get(s)==e]; x=r[cols]
    print(e,'bars beyond limit',int((x.abs()>lim).sum().sum()),'of',int(x.notna().sum().sum()))
V=df.pivot(index='date',columns='sym',values='v'); tv=(C*V*1000/1e9).rolling(20).mean()
print('liquid (>=5bn/day) count by year-end'); print((tv>=5).sum(axis=1).resample('YE').last())
print('days per year',C.resample('YE').size())
