"""KB4 lỗ sớm: lệnh KB4 (luật production) đang lỗ ≥ X% sau 1–2 phiên thì kết thúc thế nào.
Dùng mọi tín hiệu KB4 (trước khi qua danh mục). Giá nghìn đồng."""
import sys
exec(open(sys.argv[1] + '/lot1.py', encoding='utf-8').read().split("def run_lot")[0])

kb4 = [p for kb, p in PARTS if kb == 'KB4t'][0].copy(); kb4 = kb4[~kb4.deep.astype(bool)]  # bo lenh hoang loan sau (giu 40 phien)
print('cot:', list(kb4.columns)[:20], 'so lenh', len(kb4))
cc = C.values; col = {s: j for j, s in enumerate(syms)}


def gia(i, s):
    v = cc[i, col[s]] if 0 <= i < len(dates) else np.nan
    return v


rows = []
for _, x in kb4.iterrows():
    e = int(x.e); k = int(x.k); j = col[x.sym]
    path = [cc[i, j] / x.entry - 1 for i in range(e, k + 1) if not np.isnan(cc[i, j])]
    r1 = cc[e + 1, j] / x.entry - 1 if e + 1 < len(dates) else np.nan
    r2 = cc[e + 2, j] / x.entry - 1 if e + 2 < len(dates) else np.nan
    rows.append(dict(date=x.date, sym=x.sym, ret=x.ret, r1=r1, r2=r2, mae=min(path) if path else np.nan,
                     hold=k - e))
D = pd.DataFrame(rows)



lech = (MKT / MKT_MA50 - 1)
D['lech0'] = [lech.iloc[int(x.e)] for _, x in kb4.iterrows()]
D['lech2'] = [lech.iloc[int(x.e)+2] if int(x.e)+2 < len(dates) else np.nan for _, x in kb4.iterrows()]
D['br0'] = [BREADTH50.iloc[int(x.e)] for _, x in kb4.iterrows()]
D['br2'] = [BREADTH50.iloc[int(x.e)+2] if int(x.e)+2 < len(dates) else np.nan for _, x in kb4.iterrows()]
def tom(lab, d):
    if len(d)==0: print(f'  {lab:58s} 0 lenh'); return
    print(f"  {lab:58s} {len(d):3d} lenh | thang {(d.ret>0.0005).mean()*100:3.0f}% | TB {d.ret.mean()*100:+6.2f}% | lo>=10% {(d.ret<=-0.10).mean()*100:3.0f}% | te nhat {d.ret.min()*100:+5.1f}% | ban o phien 2 TB {d.r2.mean()*100:+5.1f}%")
x = D[D.r2 <= -0.045]
tom('lo >= 4,5% sau phien 2 (moi hoan canh)', x)
tom('  + chi so phien 2 thap hon phien vao (thi truong xau them)', x[x.lech2 < x.lech0])
tom('  + chi so phien 2 cao hon/bang phien vao', x[x.lech2 >= x.lech0])
tom('  + do rong phien 2 < do rong phien vao', x[x.br2 < x.br0])
tom('  + chi so duoi MA50 >= 5% o phien 2', x[x.lech2 <= -0.05])
y = x[(x.lech2 < x.lech0) & (x.br2 < x.br0)]
tom('  + ca chi so lan do rong cung xau them', y)
print(y[['date','sym','r2','ret','lech0','lech2','br0','br2']].assign(r2=lambda d:(d.r2*100).round(1), ret=lambda d:(d.ret*100).round(1), lech0=lambda d:(d.lech0*100).round(1), lech2=lambda d:(d.lech2*100).round(1), br0=lambda d:d.br0.round(2), br2=lambda d:d.br2.round(2)).to_string(index=False))
