import sys; sys.path.insert(0,sys.argv[1])
exec(open(sys.argv[1]+'/newkb.py').read().split("print('### N1")[0])
pd.set_option('display.width',260)
ent=O.shift(-1); ceil_open=(ent>=C*(1+LIM-0.005))
FW={h:(C.shift(-h)/ent-1-COST).where(~ceil_open&~BAD.rolling(h+1).max().shift(-h).fillna(1).astype(bool)) for h in (5,10,20)}
NORMAL=bc(BREADTH50>=0.35)
# ngày nghỉ dài (>=3 ngày lịch ngoài cuối tuần): khoảng cách tới phiên kế > 3 ngày
gap_next=pd.Series(dates[1:].append(pd.DatetimeIndex([dates[-1]+pd.Timedelta(days=1)])) - dates,index=dates).dt.days
PRE_HOL=pd.Series(False,index=dates)
for i in range(len(dates)):
    if gap_next.iloc[i]>=4: PRE_HOL.iloc[max(0,i-3):i+1]=True   # 4 phiên trước kỳ nghỉ dài
R5=C/C.shift(5)-1
secR5=R5.where(UNI).T.groupby(sec).transform('median').T
secBr=(C>MA[20]).where(UNI).astype(float).T.groupby(sec).transform('mean').T
secBr5=secBr-secBr.shift(5)
OBV=(np.sign(R).fillna(0)*V).rolling(20).sum()/V.rolling(20).sum()
mo_m=bc(pd.Series(dates.month,index=dates).astype(float)); dom=bc(pd.Series(dates.day,index=dates).astype(float))
ER=(mo_m.isin([1,4,7,10]))&(dom>=10)   # mùa báo cáo quý (ước lượng)
EV={
 'N3 goc (RS63 top, phien -6)':topk((LEFT==5)&(TV>30),RS63,5),
 'TOM vao som (phien -8) RS63':topk((LEFT==7)&(TV>30),RS63,5),
 'TOM RS20 top':topk((LEFT==5)&(TV>30),C/C.shift(20),5),
 'TOM RS126 top':topk((LEFT==5)&(TV>30),RS126,5),
 'Truoc nghi le dai, RS63 top':topk(bc(PRE_HOL&~PRE_HOL.shift(1,fill_value=False))&(TV>30),RS63,5),
 'Truoc nghi le, bluechip':topk(bc(PRE_HOL&~PRE_HOL.shift(1,fill_value=False))&(TV>100),TV,5),
 'Nganh bung no, ma tut hau':topk((secBr5>0.3)&(R5<secR5-0.03)&(TV>20)&(C>MA[50]),-(R5-secR5),5),
 'Nganh bung no, ma dau tau':topk((secBr5>0.3)&(TV>20),R5,5),
 'Gap+vol mua bao cao (PEAD)':topk(ER&((O/C.shift()-1)>0.02)&(V>3*VMA20)&(C>O)&(TV>10),V/VMA20,5),
 'Tran + vol, TT tren MA50':topk((R>=LIM-0.005)&(V>2*VMA20)&bc(MKT>MKT_MA50)&(TV>10),TV,5),
 'Hammer dao chieu trong xu huong':topk((O<C.shift()*0.98)&(C>=H-(H-L)*0.2)&(C>C.shift())&(V>1.5*VMA20)&(C>MA[50])&(TV>20),TV,5),
 'Tich luy OBV duong, bien hep':topk((OBV>0.3)&(H.rolling(20).max()/L.rolling(20).min()<1.12)&(C>MA[50])&(TV>10),OBV,5),
 'Bluechip giam tuan, TT tang':topk((R5<-0.05)&(TV>100)&bc(MKT>MKT_MA50)&(C>MA[200]),-R5,5),
 'Breadth thrust 40->60, RS top':topk(bc((BREADTH50>0.6)&(BREADTH50.rolling(10).min()<0.4))&(TV>30),RS63,5),
 'Gan dinh 60p, vol can, TT tang':topk((C>=HI60.shift()*0.97)&(ma(V,5)<0.8*VMA20)&bc(MKT>MKT_MA50)&(TV>20),RS63,5),
 'Low-vol bluechip tren MA50':topk((ATR/C<0.02)&(C>MA[50])&(TV>50)&(TDM==0),-(ATR/C),5),
}
rows=[]
for k,sg in EV.items():
    s=(sg&UNI&NORMAL).fillna(False)
    r=dict(ev=k)
    for h in (5,10,20):
        x=FW[h].where(s).stack().dropna(); x=x[x.index.get_level_values(0)>='2023-01-01']
        dt=x.index.get_level_values(0)
        isx=x[dt<='2025-06-30']; oo=x[dt>='2025-07-01']
        r[f'n{h}']=len(x); r[f'w{h}IS']=round((isx>0).mean()*100,1); r[f'w{h}OOS']=round((oo>0).mean()*100,1); r[f'a{h}IS']=round(isx.mean()*100,2); r[f'a{h}OOS']=round(oo.mean()*100,2)
        if h==10: r["minY10"]=min((round((x[dt.year==y]>0).mean()*100) for y in (2023,2024,2025,2026) if (dt.year==y).sum()>=10), default=np.nan); r['ngay']=len(set(dt))
    rows.append(r)
D=pd.DataFrame(rows).set_index('ev'); print(D[['ngay','n10','w5IS','w5OOS','a5IS','a5OOS','w10IS','w10OOS','a10IS','a10OOS','minY10','w20IS','w20OOS','a20IS','a20OOS']].to_string())
base=FW[10].where(UNI&NORMAL).stack().dropna(); base=base[base.index.get_level_values(0)>='2023-01-01']
print('\nNen: moi ma, thi truong binh thuong, 10 phien: win',round((base>0).mean()*100,1),'avg',round(base.mean()*100,2))
