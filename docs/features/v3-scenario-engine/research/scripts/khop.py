import pandas as pd,sys
S=sys.argv[1]
real=pd.read_csv(S+'/v1top.txt',sep='|',header=None,names=['ngay','sym','rank','rec','state','score','gen','price'])
real['ngay']=pd.to_datetime(real.ngay)
rep=pd.read_csv(S+'/v1replay/'+sys.argv[2]); rep['d']=pd.to_datetime(rep['asof'])
d=sorted(rep['d'].unique()); nxt={d[i]:d[i+1] for i in range(len(d)-1)}
rep['ngay']=rep['d'].map(nxt)
rows=[]
for n,g in real.groupby('ngay'):
    r=rep[rep.ngay==n]
    if len(r)==0: continue
    a=set(g.sym); b=set(r.symbol); rows.append((n.date(),len(a),len(b),len(a&b),' '.join(sorted(a)),' '.join(sorted(b))))
t=pd.DataFrame(rows,columns=['ngay','that','phatlai','trung','ma_that','ma_phatlai'])
print('phien so sanh',len(t),'| TB trung',round(t.trung.mean(),2),'| ty le',round(t.trung.sum()/t.that.sum()*100,1),'%')
print(t.to_string(index=False))
