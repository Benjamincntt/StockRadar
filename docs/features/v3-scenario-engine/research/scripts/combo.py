import sys,pandas as pd,numpy as np
S=sys.argv[1]; pd.set_option('display.width',250)
d=pd.read_pickle(S+'/feat10.pkl')
def show(name,m):
    x=d[m]; r=[]
    for p,g in [('IS',x[x.per=='IS']),('OOS',x[x.per=='OOS'])]+[(str(y),x[x.date.dt.year==y]) for y in (2023,2024,2025,2026)]+[('Q4',x[x.date.dt.quarter==4])]:
        r.append(f"{p}:{len(g)}/{g.date.nunique()}d w{(g.y>0).mean()*100:.0f} a{g.y.mean()*100:+.1f}" if len(g) else f"{p}:0")
    print(f"{name:38s}"," | ".join(r))
show('ALL',d.y.notna())
print('--- thị trường khoẻ')
show('breadth>0.75',d.breadth>0.75)
show('breadth>0.75 & d_hi250>-0.05',(d.breadth>0.75)&(d.d_hi250>-0.05))
show('breadth>0.75 & rs63>0.8',(d.breadth>0.75)&(d.rs63>0.8))
show('mkt_d50>0.05',d.mkt_d50>0.05)
print('--- cổ phiếu quá bán, thị trường bình thường')
show('ret20<-0.12 & tv>30',(d.ret20<-0.12)&(d.tv>30))
show('d_ma50<-0.1 & ma50_slope>0',(d.d_ma50<-0.1)&(d.ma50_slope>0))
show('rsi14<35 & d_ma200>0 & tv>20',(d.rsi14<35)&(d.d_ma200>0)&(d.tv>20))
show('atrp<0.02 & d_hi250>-0.1',(d.atrp<0.02)&(d.d_hi250>-0.1))
print('--- theo tháng (thị trường) win/avg 10 phiên')
g=d.groupby([d.date.dt.year,d.date.dt.month]).y.agg(lambda s:(s>0).mean()*100).unstack().round(0); print(g)
