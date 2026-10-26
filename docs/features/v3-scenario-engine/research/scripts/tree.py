import sys,pandas as pd,numpy as np
from sklearn.tree import DecisionTreeClassifier
S=sys.argv[1]; pd.set_option('display.width',250)
d=pd.read_pickle(S+'/feat2.pkl'); d=d[d.date>='2023-01-01']
feats=[c for c in d.columns if c not in ('date','sym','y5','y10','month')]
def mine(sub,label,target='y10',depth=4,leaf=0.01):
    tr=sub[sub.date<='2025-06-30'].dropna(subset=feats+[target]); te=sub[sub.date>='2025-07-01'].dropna(subset=feats+[target])
    m=DecisionTreeClassifier(max_depth=depth,min_samples_leaf=max(500,int(len(tr)*leaf)),random_state=0).fit(tr[feats],tr[target]>0)
    tr=tr.assign(leaf=m.apply(tr[feats])); te=te.assign(leaf=m.apply(te[feats]))
    def agg(x): return pd.Series({'n':len(x),'days':x.date.nunique(),'win':(x[target]>0).mean()*100,'avg':x[target].mean()*100,
        'minYear':min((x[x.date.dt.year==y][target]>0).mean()*100 for y in sorted(x.date.dt.year.unique()) if (x.date.dt.year==y).sum()>50) if len(x) else np.nan})
    a=tr.groupby('leaf').apply(agg); b=te.groupby('leaf').apply(agg)
    t=a.join(b,lsuffix='_IS',rsuffix='_OOS').sort_values('win_IS',ascending=False).round(1)
    # mô tả luật của mỗi lá
    tree=m.tree_; paths={}
    def walk(node,conds):
        if tree.children_left[node]==-1: paths[node]=' & '.join(conds); return
        f=feats[tree.feature[node]]; th=tree.threshold[node]
        walk(tree.children_left[node],conds+[f'{f}<={th:.3g}']); walk(tree.children_right[node],conds+[f'{f}>{th:.3g}'])
    walk(0,[])
    print(f'\n##### {label} target={target}'); 
    for lf,r in t.head(6).iterrows(): print(f"[{lf}] IS n{int(r.n_IS)} d{int(r.days_IS)} w{r.win_IS} a{r.avg_IS} minY{r.minYear_IS} | OOS n{r.n_OOS} d{r.days_OOS} w{r.win_OOS} a{r.avg_OOS}  :: {paths[lf]}")
mine(d[d.breadth>=0.35],'Thi truong binh thuong','y10')
mine(d[d.breadth>=0.35],'Thi truong binh thuong','y5')
nm=[f for f in feats if not f.startswith(('mkt_','breadth'))]
feats=nm
mine(d[d.breadth>=0.35],'Binh thuong, bo dac trung thi truong','y10')
mine(d,'Moi trang thai, bo dac trung thi truong','y10')
