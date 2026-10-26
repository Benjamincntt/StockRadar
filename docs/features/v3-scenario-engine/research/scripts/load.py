import json, sys, pandas as pd
S=sys.argv[1]
rows=[];meta=[]
with open(S+'/prod_stocks.txt',encoding='utf-8',errors='replace') as f:
    for line in f:
        line=line.strip()
        if not line.startswith('[') and '|' in line and line.count('|')>=4:
            sym,ex,sec,act,js=line.split('|',4)
            try: h=json.loads(js)
            except Exception as e: print('bad',sym,len(js)); continue
            meta.append((sym,ex,sec,int(act),len(h)))
            for b in h:
                if b['date']>='2021-06-01': rows.append((sym,b['date'],b['open'],b['high'],b['low'],b['close'],b['volume']))
df=pd.DataFrame(rows,columns=['sym','date','o','h','l','c','v']); df['date']=pd.to_datetime(df['date'])
df=df[df.date<'2026-10-23']
m=pd.DataFrame(meta,columns=['sym','ex','sector','active','n'])
df.to_pickle(S+'/bars.pkl'); m.to_pickle(S+'/meta.pkl')
print(len(m), len(df), df.date.min(), df.date.max()); print(m.groupby(['ex','active']).size())
print(df.groupby(df.date.dt.year).sym.nunique())
