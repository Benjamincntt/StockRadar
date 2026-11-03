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


def tom(lab, d):
    if len(d) == 0:
        print(f'  {lab:34s} 0 lenh'); return
    print(f"  {lab:34s} {len(d):4d} lenh | thang {(d.ret>0.0005).mean()*100:3.0f}% | TB {d.ret.mean()*100:+5.2f}% "
          f"| trung vi {d.ret.median()*100:+5.1f}% | te nhat {d.ret.min()*100:+5.1f}% | lo>=10% {(d.ret<=-0.10).mean()*100:3.0f}% "
          f"| lo>=20% {(d.ret<=-0.20).mean()*100:3.0f}% | MAE TB {d.mae.mean()*100:+5.1f}% | giu TB {d.hold.mean():.1f}")


print('\nKet thuc cua lenh KB4 theo muc lo sau phien 1 / phien 2 (gia dong cua so voi gia vao):')
tom('tat ca KB4', D)
for lim in (-0.03, -0.045, -0.07):
    tom(f'sau phien 2 lo >= {-lim*100:.1f}%', D[D.r2 <= lim])
for lim in (-0.03, -0.045):
    tom(f'sau phien 1 lo >= {-lim*100:.1f}%', D[D.r1 <= lim])
d = D[D.r2 <= -0.045]
print('\nPhan bo ket qua cuoi (lo >= 4,5% sau phien 2):')
print((d.ret * 100).describe(percentiles=[.1, .25, .5, .75, .9]).round(1).to_string())
print('\nTrong mau / ngoai mau (lo >= 4,5% sau phien 2):')
tom('trong mau', d[d.date <= IS[1]]); tom('ngoai mau', d[d.date > IS[1]])

