import sys; sys.path.insert(0,sys.argv[1]); from bt import *
pd.set_option('display.width',250)
HZ=int(sys.argv[2]) if len(sys.argv)>2 else 10
ent=O.shift(-1); fwd=C.shift(-HZ)/ent-1-COST
mfwd=bc(MKT.shift(-HZ)/MKT-1)
badf=BAD.rolling(HZ+1).max().shift(-HZ).astype(bool)
F={
 'ret1':R,'ret5':C/C.shift(5)-1,'ret20':C/C.shift(20)-1,'ret60':C/C.shift(60)-1,
 'd_ma20':C/MA[20]-1,'d_ma50':C/MA[50]-1,'d_ma200':C/MA[200]-1,
 'rsi2':RSI2,'rsi14':RSI14,'vratio':V/VMA20,'atrp':ATR/C,
 'pos20':(C-LO20)/(H.rolling(20).max()-LO20),'d_hi250':C/HI250-1,'clv':(C-L)/(H-L),
 'rs63':RS63,'rs126':RS126,'tv':TV,
 'mkt_ret5':bc(MKT/MKT.shift(5)-1),'mkt_d50':bc(MKT/MKT_MA50-1),'breadth':bc(BREADTH50),
 'ma50_slope':MA[50]/MA[50].shift(10)-1,
}
mask=UNI&fwd.notna()&~badf
def stack(x): return x.where(mask).stack()
y=stack(fwd); ex=stack(fwd-mfwd)
d=pd.DataFrame({'y':y,'ex':ex}); 
for k,v in F.items(): d[k]=stack(v).reindex(d.index)
d=d.reset_index(); d.columns=['date','sym']+list(d.columns[2:])
d['per']=np.where(d.date<=IS[1],'IS','OOS'); d=d[d.date>='2023-01-01']
d.to_pickle(S+f'/feat{HZ}.pkl')
print('rows',len(d),'base win IS/OOS',d.groupby('per').y.apply(lambda s:(s>0).mean()).round(3).to_dict())
for k in F:
    q=pd.qcut(d[k].rank(method='first'),10,labels=False)
    g=d.groupby([q,'per']).agg(win=('y',lambda s:(s>0).mean()*100),avg=('y','mean'),exc=('ex','mean')).unstack()
    lo=d.groupby(q)[k].min()
    print(f'\n--- {k}  (H={HZ})'); 
    out=pd.DataFrame({'lo':lo.round(3),'winIS':g['win']['IS'].round(1),'winOOS':g['win']['OOS'].round(1),'avgIS%':(g['avg']['IS']*100).round(2),'avgOOS%':(g['avg']['OOS']*100).round(2),'excIS%':(g['exc']['IS']*100).round(2),'excOOS%':(g['exc']['OOS']*100).round(2)})
    print(out.T.to_string())
