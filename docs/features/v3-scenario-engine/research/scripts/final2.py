import sys,pickle; exec(open(sys.argv[1]+'/size.py',encoding='utf-8').read().split("split2(*run_pf(PARTS,5,park_r=D45")[0])
def worst_hit(E,T,sizes):
    # lệnh lỗ nặng nhất tính theo % tổng vốn lúc vào lệnh
    return min(r.ret*sizes(r)/5 for _,r in T.iterrows())*100
def mo(E): m=E.resample('ME').last(); return (m/m.shift(1).fillna(1.0)-1).min()*100
V={'P1+ (lần trước)':dict(park_r=None,size_fn=None),
   'P1+ + gửi tiền 4.5%':dict(park_r=D45,park_cost=(0,0),size_fn=None),
   'P1++ : gửi tiền + KB1/KB2/N1 x1.5':dict(park_r=D45,park_cost=(0,0),size_fn=lambda x: 1.5 if x.kb in ('KB1','KB2','N1t') else 1.0),
   'P1++ an toàn: thêm KB4 x0.5':dict(park_r=D45,park_cost=(0,0),size_fn=lambda x: 1.5 if x.kb in ('KB1','KB2','N1t') else (0.5 if x.kb=='KB4t' else 1.0))}
OUT={}
for k,kw in V.items():
    E,T=run_pf(PARTS,5,**kw); OUT[k]=(E,T)
    sf=kw.get('size_fn') or (lambda x:1.0)
    a=E[E.index<=IS[1]]; b=E[E.index>IS[1]]
    ye=E.groupby(E.index.year).last(); yr=(ye/ye.shift(1).fillna(1.0)-1)*100
    q4=[E[(E.index.year==y)&(E.index.quarter==4)] for y in (2023,2024,2025)]
    q4r=np.mean([(q.iloc[-1]/E[E.index<q.index[0]].iloc[-1]-1)*100 for q in q4])
    print(f"{k:36s} {' '.join(f'{y}:{v:+.0f}%' for y,v in yr.items())} | tong {(E.iloc[-1]-1)*100:+.0f}% | sut {(E/E.cummax()-1).min()*100:.1f}% | thang te {mo(E):.1f}% | Q4 TB {q4r:+.1f}% | IS {(a.iloc[-1]-1)*100:+.0f}% OOS {(b.iloc[-1]/a.iloc[-1]-1)*100:+.0f}% | lenh te nhat mat {worst_hit(E,T,sf):.1f}% von")
pickle.dump(OUT,open(sys.argv[1]+'/p1pp.pkl','wb'))
