import sys; exec(open(sys.argv[1]+'/pfeng.py',encoding='utf-8').read())
t=BASE[2][1].copy()   # KB4: mua ATC, bán ATO sau khi đóng cửa > MA5, giữ tối đa 10
ii=dates.get_indexer(t.date); jj=np.array([syms.get_loc(s) for s in t.sym])
A=lambda df: df.values[ii,jj]
secmed=lambda x: x.where(UNI).T.groupby(sec).transform('median').T
secN=UNI.astype(int).T.groupby(sec).transform('sum').T
F={'r5':C/C.shift(5)-1,'r20':RET20,'rel_sec20':RET20-SECR20,'sec20':SECR20,'sec5':secmed(C/C.shift(5)-1),'d_ma200':C/MA[200]-1,'d_ma50':C/MA[50]-1,
   'rsi14':RSI14,'atrp':ATR/C,'vratio':V/VMA20,'tv':TV,'secN':secN,'r1':R,'clv':((C-L)/(H-L)).clip(0,1),'d_hi250':C/HI250-1,'floor20':FLOOR20}
for k,v in F.items(): t[k]=A(v)
t['br']=BREADTH50.values[ii]; t['brc']=(BREADTH50-BREADTH50.shift(5)).values[ii]; t['mk5']=(MKT/MKT.shift(5)-1).values[ii]
t['per']=np.where(t.date<=IS[1],'IS','OOS'); t['bad']=t.ret<-0.08
print('KB4: lenh',len(t),' lo nang (<-8%)',t.bad.sum(),' ty le',round(t.bad.mean()*100,1),'%')
print('Ly do thoat cua lenh lo nang: giu toi het han?',(t[t.bad].days>=10).mean().round(2))
rows=[]
for k in list(F)+['br','brc','mk5']:
    q=pd.qcut(t[k].rank(method='first'),4,labels=False)
    g=t.groupby([q,'per']).agg(bad=('bad','mean'),avg=('ret','mean')).unstack()
    rows.append((k,)+tuple((g['bad'].values*100).round(0).flatten())+tuple((g['avg'].values*100).round(1).flatten())+tuple(t.groupby(q)[k].min().round(3)))
cols=['feat']+[f'bad_q{q}_{p}' for q in range(4) for p in ('IS','OOS')]+[f'avg_q{q}_{p}' for q in range(4) for p in ('IS','OOS')]+[f'lo_q{q}' for q in range(4)]
D=pd.DataFrame(rows,columns=cols).set_index('feat'); pd.set_option('display.width',300)
print(D[[c for c in cols[1:] if c.startswith('bad')]+['lo_q1','lo_q2','lo_q3']].to_string())
print(D[[c for c in cols[1:] if c.startswith('avg')]].to_string())
# diễn biến: lệnh lỗ nặng chạm đáy khi nào
bad=t[t.bad]; print('\nLenh lo nang, so phien giu TB',bad.days.mean().round(1))
t.to_pickle(sys.argv[1]+'/kb4_feat.pkl')
