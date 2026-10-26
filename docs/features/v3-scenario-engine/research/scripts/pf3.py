import sys; exec(open(sys.argv[1]+'/pfeng.py',encoding='utf-8').read())
md=bc(MKT/MKT_MA50-1); brc=bc(BREADTH50-BREADTH50.shift(5)); base3=(LEFT==5)&(TV>30)
def n3(cond):
    t=simulate_real2(topk(base3&cond,RS63,5),hold=12,exit_open_rule=(TDM==2),entry='atc'); return tag(t[t.date>='2023-01-01'],'N3t')
def split(E,T,label):
    # tách hiệu quả trong mẫu / ngoài mẫu trên cùng một đường giá trị
    a=E[E.index<=IS[1]]; b=E[E.index>IS[1]]
    ddA=(a/a.cummax()-1).min(); ddB=(b/b.cummax()-1).min()
    print(f"{label:44s} trong mau {(a.iloc[-1]-1)*100:+6.0f}% (sut {ddA*100:5.1f}%) | ngoai mau {(b.iloc[-1]/a.iloc[-1]-1)*100:+6.0f}% (sut {ddB*100:5.1f}%) | thang {(T.ret>0.0005).mean()*100:.0f}%")
P=lambda n3t,parts=None,**kw: run_pf((parts or BASE[:4])+[('N3t',n3t)],**kw)
split(*P(BASE[4][1]),'P1 goc')
import sys as _s
if 0:
  print()
  for w in (): split(*P(n3(~((md>-w)&(md<w)))),f'bo vung |d50|<{w*100:.1f}%')
  split(*P(n3(~((md>-0.015)&(md<0.015))&(brc>-0.09))),'bo vung 1.5% + do rong khong sut manh')
print('-- luat ban tung kich ban (N3 da loc 1.5%)')
n3f=n3(~((md>-0.015)&(md<0.015)))
sig1,sig2,sig4=SIG['KB1'][0],SIG['KB2'][0],SIG['KB4'][0]
n1=topk((FLOOR20>=1)&~FLOOR&(SECR20<=-0.12)&(TV>10)&(LEFT<=6),-SECR20,5)
def T_(sig,kb,mode,**kw):
    t=simulate_real2(sig,entry=mode,**kw); return tag(t[t.date>='2023-01-01'],kb,mode)
LV[20]=ma_touch_level(20)
F=lambda n,hd=15: dict(hold=hd,exit_level=LV[n],exit_above_entry=True)
alts={
 'KB1,KB2 ban F MA20, giu 20':[('KB1',T_(sig1,'KB1','open',**F(20,20))),('KB2',T_(sig2,'KB2','open',**F(20,20))),BASE[2],BASE[3]],
 'KB4 dong cua>MA10 -> ATO':[BASE[0],BASE[1],('KB4t',T_(sig4,'KB4t','atc',hold=15,exit_open_rule=nxt(C>MA[10]))),BASE[3]],
 'N1 dong cua>MA20 -> ATO':[BASE[0],BASE[1],BASE[2],('N1t',T_(n1,'N1t','atc',hold=20,exit_open_rule=nxt(C>MA[20])))],
 'uu tien N1 truoc KB4':[BASE[0],BASE[1],BASE[3],BASE[2]],
}
for k,parts in alts.items(): split(*P(n3f,parts),k)
print('-- lenh lo nang cua KB4')
t4=BASE[2][1]; ii=dates.get_indexer(t4.date); jj=[syms.get_loc(s) for s in t4.sym]
t4=t4.assign(fl5=FLOOR.rolling(5).sum().values[ii,jj],r5=(C/C.shift(5)-1).values[ii,jj],sec5=R.where(UNI).T.groupby(sec).transform('median').T.rolling(5).sum().values[ii,jj],br=BREADTH50.values[ii])
print(t4.nsmallest(8,'ret')[['date','sym','ret','fl5','r5','br']].round(3).to_string(index=False))
for nm,c in (('co phien san 5 ngay',t4.fl5>0),('khong san',t4.fl5==0)): x=t4[c]; print(nm,len(x),'win',round((x.ret>0.0005).mean()*100),'avg',round(x.ret.mean()*100,2))
