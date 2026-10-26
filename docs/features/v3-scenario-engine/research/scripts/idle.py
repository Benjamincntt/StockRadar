import sys; exec(open(sys.argv[1]+'/kb4fix.py',encoding='utf-8').read().split("split(*P(BASE[2]),'P1+ (KB4 goc)')")[0])
PARTS=[BASE[0],BASE[1],BASE[2],BASE[3],N3F]
def split2(E,T,label):
    a=E[E.index<=IS[1]]; b=E[E.index>IS[1]]
    ye=E.groupby(E.index.year).last(); yr=(ye/ye.shift(1).fillna(1.0)-1)*100
    print(f"{label:48s} {' '.join(f'{y}:{v:+.0f}%' for y,v in yr.items())} | tong {(E.iloc[-1]-1)*100:+.0f}% | sut max {(E/E.cummax()-1).min()*100:.1f}% | trong mau {(a.iloc[-1]-1)*100:+.0f}% / ngoai mau {(b.iloc[-1]/a.iloc[-1]-1)*100:+.0f}%")
z=pd.Series(0.0,index=dates)
dep=lambda rate: pd.Series((1+rate)**(1/250)-1,index=dates)
# rổ: lợi nhuận ngày bình quân đều, chọn lại thành phần mỗi đầu tháng theo dữ liệu tới cuối tháng trước
mo=pd.Series(dates.to_period('M'),index=dates); first=(mo!=mo.shift())
def basket(score,n=10,cond=None):
    sel=pd.DataFrame(False,index=dates,columns=syms); cur=None
    for d in dates:
        if first[d]:
            i=dates.get_loc(d); s=score.iloc[i-1].where(UNI.iloc[i-1] & (cond.iloc[i-1] if cond is not None else True))
            cur=s.nlargest(n).index
        if cur is not None: sel.loc[d,cur]=True
    r=R.where(sel).clip(-.16,.16).mean(axis=1).fillna(0)
    turn=0.004*first.astype(float)*0.5      # phí đổi thành phần mỗi tháng (ước 50% thay mới)
    return r-turn
blue=basket(TV,10); lowv=basket(-(ATR/C),10,TV>100); mom=basket(RS63,10,TV>50)
trend=(MKT>MKT_MA50).shift(1).fillna(False)
def with_trend(r,rate=0.045):
    # trên MA50 thì giữ rổ, dưới MA50 thì về tiền gửi; mỗi lần đổi trạng thái tốn 0.4%
    flip=(trend!=trend.shift(1)).astype(float)*0.004
    return pd.Series(np.where(trend,r,dep(rate)),index=dates)-flip
split2(*run_pf(PARTS,5),'P1+ (tien nhan roi lai 0%)')
for rate in (0.03,0.045): split2(*run_pf(PARTS,5,park_r=dep(rate),park_cost=(0,0)),f'+ gui tien {rate*100:.1f}%/nam (gia dinh)')
for nm,r in (('chi so binh quan deu',mret),('10 ma thanh khoan nhat',blue),('10 ma it bien dong (TV>100)',lowv),('10 ma manh nhat RS63',mom)):
    split2(*run_pf(PARTS,5,park_r=r),f'+ do vao {nm}')
    split2(*run_pf(PARTS,5,park_r=with_trend(r)),f'+ {nm}, chi khi chi so > MA50')
for nm,r in (('10 ma thanh khoan nhat',blue),('10 ma manh nhat RS63',mom)):
    e=(1+r[dates>='2023-01-01']).cumprod(); print(f'   (tham chieu) ro {nm} mua-giu: tong {(e.iloc[-1]-1)*100:+.0f}%, sut max {(e/e.cummax()-1).min()*100:.1f}%')
