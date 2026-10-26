import sys; exec(open(sys.argv[1]+'/pfeng.py',encoding='utf-8').read())
t=tag(TR['N3t'],'N3t'); t=t[t.date>='2023-01-01'].copy()
ii=dates.get_indexer(t.date); jj=[syms.get_loc(s) for s in t.sym]
def at(df): return df.values[ii,jj]
def atm(sr): return sr.values[ii]
t['mkt_d50']=atm(MKT/MKT_MA50-1); t['mkt_r5']=atm(MKT/MKT.shift(5)-1); t['br']=atm(BREADTH50); t['br_chg']=atm(BREADTH50-BREADTH50.shift(5))
t['atrp']=at(ATR/C); t['r20']=at(C/C.shift(20)-1); t['d_ma20']=at(C/MA[20]-1); t['rsi14']=at(RSI14); t['tv']=at(TV)
t['per']=np.where(t.date<=IS[1],'IS','OOS')
for k in ('mkt_d50','mkt_r5','br','br_chg','atrp','r20','d_ma20','rsi14','tv'):
    q=pd.qcut(t[k].rank(method='first'),4,labels=False)
    g=t.groupby([q,'per']).ret.agg(lambda s:s.mean()*100).unstack().round(2); w=t.groupby([q,'per']).ret.agg(lambda s:(s>0.0005).mean()*100).unstack().round(0)
    lo=t.groupby(q)[k].min().round(3)
    print(f'--- {k}: nguong duoi | avg IS | avg OOS | win IS | win OOS'); print(pd.concat([lo,g,w],axis=1).to_string(header=False))
