import sys,json,glob,os,pandas as pd
S=sys.argv[1]; rows=[]
for f in glob.glob(S+'/kbs60/*.json'):
    sym=os.path.basename(f)[:-5]
    try: d=json.load(open(f,encoding='utf-8'))
    except Exception: continue
    for b in d.get('data_60P',[]):
        rows.append((sym,b['t'],b['o'],b['h'],b['l'],b['c'],b.get('v',0)))
h=pd.DataFrame(rows,columns=['sym','t','o','h','l','c','v']); h['t']=pd.to_datetime(h['t']); h['date']=h.t.dt.normalize()
pm=h[h.t.dt.hour>=13].groupby(['sym','date']).agg(o=('o','first'),h=('h','max'),l=('l','min'),c=('c','last')).reset_index()
need=pd.read_csv(S+'/need_pm.csv',parse_dates=['date'])
cov=need.merge(pm,on=['sym','date'],how='left')
print('phu nen gio buoi chieu:',cov.o.notna().sum(),'/',len(need))
print('so ngay co nen gio moi ma (min/median):',h.groupby('sym').date.nunique().min(),h.groupby('sym').date.nunique().median())
pm.to_pickle(S+'/pm.pkl')
