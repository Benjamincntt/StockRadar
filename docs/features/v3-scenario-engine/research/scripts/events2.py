import sys; sys.path.insert(0,sys.argv[1])
exec(open(sys.argv[1]+'/events.py').read().split("EV={")[0])
# phân rã lợi nhuận qua đêm / trong phiên của chỉ số bình quân đều
on=(O/C.shift()-1).where(UNI.shift()).clip(-.16,.16).mean(axis=1); intra=(C/O-1).where(UNI.shift()).clip(-.16,.16).mean(axis=1)
z=pd.DataFrame({'qua_dem':on,'trong_phien':intra})[dates>='2023-01-01']; print('TB/ngay (%) qua dem vs trong phien:'); print((z.groupby(z.index.year).mean()*100).round(3).to_string())
FWC={h:(C.shift(-h)/C-1-COST).where(~BAD.rolling(h+1).max().shift(-h).fillna(1).astype(bool)) for h in (5,10,20)}  # mua ATC phiên tín hiệu
mktR=bc(mret); dn=mktR<-0.015
PP=(R>0)&(V>(V.where(R<0)).rolling(10,min_periods=1).max().shift())&(C>MA[50])&(abs(C/MA[10]-1)<0.03)
NR7=((H-L)==(H-L).rolling(7).min())&(C>MA[20])&(MA[20]>MA[50])
TOP30=(TV.rank(axis=1,ascending=False)<=30)
EV={
 'Chong chiu: TT -1.5%, ma +1% vol cao':topk(dn&(R>0.01)&(V>1.5*VMA20)&(C>MA[50])&(TV>20),R,5),
 'Chong chiu 2: TT -1.5%, ma xanh, tren MA20':topk(dn&(R>0)&(C>MA[20])&(TV>50),RS63,5),
 'Pocket pivot':topk(PP&(TV>20)&bc(MKT>MKT_MA50),V/VMA20,5),
 'NR7 trong xu huong':topk(NR7&(TV>20)&(RS63>0.7),RS63,5),
 'Xoay vong top30 TV, RS63 top, dau tuan':topk(TOP30&bc(pd.Series(dates.weekday==0,index=dates))&bc(MKT>MKT_MA50),RS63,5),
 'TT giam 1.5%, bluechip giam manh nhat':topk(dn&(TV>100)&(C>MA[200]),-R,5),
 'Ma giam 3 phien lien, TT tang':topk((R<0)&(R.shift()<0)&(R.shift(2)<0)&(C/C.shift(3)-1<-0.05)&(C>MA[100])&(TV>50)&bc(MKT>MKT_MA50),-(C/C.shift(3)),5),
 'N3 (TOM RS63)':topk((LEFT==5)&(TV>30),RS63,5),
 'TT giam 1.5%, cuoi thang':topk(dn&(LEFT<=5)&(TV>50)&(C>MA[50]),RS63,5),
 'Gap down >3% dong cua xanh, tren MA50':topk((O<C.shift()*0.97)&(C>O)&(C>MA[50])&(TV>20),TV,5),
}
def scan(FWD,label):
    rows=[]
    for k,sg in EV.items():
        s=(sg&UNI&NORMAL).fillna(False); r=dict(ev=k)
        for h in (5,10,20):
            x=FWD[h].where(s).stack().dropna(); x=x[x.index.get_level_values(0)>='2023-01-01']; dt=x.index.get_level_values(0)
            isx=x[dt<='2025-06-30']; oo=x[dt>='2025-07-01']
            r.update({f'w{h}IS':round((isx>0).mean()*100,1),f'w{h}OOS':round((oo>0).mean()*100,1),f'a{h}IS':round(isx.mean()*100,2),f'a{h}OOS':round(oo.mean()*100,2)})
            if h==10: r['n']=len(x); r['ngay']=len(set(dt)); r['minY10']=min(((x[dt.year==y]>0).mean()*100 for y in (2023,2024,2025,2026) if (dt.year==y).sum()>=10),default=np.nan)
        rows.append(r)
    print(f'\n##### {label}'); print(pd.DataFrame(rows).set_index('ev')[['ngay','n','w5IS','w5OOS','a5IS','a5OOS','w10IS','w10OOS','a10IS','a10OOS','minY10','w20IS','w20OOS','a20OOS']].round(1).to_string())
scan(FW,'Mua mo cua phien sau')
scan(FWC,'Mua ATC ngay phien tin hieu')
