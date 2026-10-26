import sys; sys.path.insert(0,sys.argv[1]); from bt import *
ent=O.shift(-1)
sec=meta.sector.reindex(syms).fillna('')
def secmed(x): return x.where(UNI).T.groupby(sec).transform('median').T
R5=C/C.shift(5)-1; R20=RET20
F={
 'ret1':R,'ret3':C/C.shift(3)-1,'ret5':R5,'ret20':R20,'ret60':C/C.shift(60)-1,
 'd_ma10':C/MA[10]-1,'d_ma20':C/MA[20]-1,'d_ma50':C/MA[50]-1,'d_ma200':C/MA[200]-1,
 'ma20_slope':MA[20]/MA[20].shift(5)-1,'ma50_slope':MA[50]/MA[50].shift(10)-1,
 'rsi2':RSI2,'rsi14':RSI14,'vratio':V/VMA20,'v5_20':ma(V,5)/VMA20,'atrp':ATR/C,
 'pos20':(C-LO20)/(H.rolling(20).max()-LO20),'d_hi250':C/HI250-1,'d_hi60':C/HI60-1,
 'clv':((C-L)/(H-L)).clip(0,1),'gap':O/C.shift()-1,'body':(C-O)/O,
 'downdays':(R<0).astype(int).rolling(5).sum(),'ceil20':(R>=LIM-0.005).astype(int).rolling(20).sum(),
 'floor20':(R<=-(LIM-0.005)).astype(int).rolling(20).sum(),
 'range10':H.rolling(10).max()/L.rolling(10).min()-1,
 'rs63':RS63,'rs126':RS126,'logtv':np.log(TV),
 'sec_ret5':secmed(R5),'sec_ret20':secmed(R20),'rel_sec5':R5-secmed(R5),'rel_sec20':R20-secmed(R20),
 'mkt_ret5':bc(MKT/MKT.shift(5)-1),'mkt_ret20':bc(MKT/MKT.shift(20)-1),'mkt_d50':bc(MKT/MKT_MA50-1),'breadth':bc(BREADTH50),
 'breadth_chg5':bc(BREADTH50-BREADTH50.shift(5)),
}
dom=pd.Series(dates.day,index=dates); 
mo=pd.Series(dates.to_period('M'),index=dates); tdm=mo.groupby(mo).cumcount(); tleft=mo.groupby(mo).cumcount(ascending=False)
F['tday_month']=bc(tdm.astype(float)); F['tdays_left']=bc(tleft.astype(float)); F['weekday']=bc(pd.Series(dates.weekday,index=dates).astype(float))
F['month']=bc(pd.Series(dates.month,index=dates).astype(float))
out={}
for HZ in (5,10):
    fwd=C.shift(-HZ)/ent-1-COST
    badf=BAD.rolling(HZ+1).max().shift(-HZ).fillna(1).astype(bool)
    ceil_open=(ent>=C*(1+LIM-0.005))
    out[HZ]=(fwd.where(UNI&~badf&~ceil_open))
mask=out[10].notna()&(dates>='2022-06-01')[:,None]
d=pd.DataFrame({'y10':out[10].where(mask).stack()})
d['y5']=out[5].stack().reindex(d.index)
for k,v in F.items(): d[k]=v.stack().reindex(d.index).astype('float32')
d=d.reset_index(); d.columns=['date','sym']+list(d.columns[2:])
d.to_pickle(sys.argv[1]+'/feat2.pkl'); print(d.shape, d.date.min(), d.date.max())
