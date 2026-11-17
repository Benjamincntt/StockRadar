"""Quý 4 (01/10 → 31/12) từng năm 2016–2025: danh mục nào lãi nhiều và đều nhất cuối năm.
Lãi quý 4 = giá trị danh mục ngày giao dịch cuối cùng của 12 chia ngày giao dịch cuối cùng của 9 (đường vốn chạy liên tục,
nên vị thế mang sang từ tháng 9 vẫn tính). Năm 2026 chưa hết quý 4 → không tính."""
import sys, warnings, numpy as np, pandas as pd
warnings.filterwarnings('ignore')
D = sys.argv[1]
src = open(D + '/v4he.py', encoding='utf-8').read(); exec(src[:src.rindex("for ten, kw in (")])
NAM = range(2016, 2026)
def q4(E):
    out = {}
    for y in NAM:
        a = E[E.index <= f'{y}-09-30']; b = E[E.index <= f'{y}-12-31']
        if len(a) and len(b) and a.index[-1].year == y: out[y] = b.iloc[-1] / a.iloc[-1] - 1
    return pd.Series(out)
DONG = {}
_m = MKT / MKT.iloc[0]; DONG['Chi so deu trong so 300 ma (moc)'] = q4(_m)
for ten, kw in (('V4 loi it bien dong (ATO, lo 100)', dict()),
                ('V4 loi + 2 tang soc (ATO, lo 100)', dict(tinh=TIN2, S=2)),
                ('V4 hoan chinh D4 (+ tien mat khi Giam gia)', dict(tinh=TIN2, S=2, ra_tien='loi', pha_ra=(4,)))):
    E, _ = chay_he(von=40_000, **kw); DONG[ten] = q4(E)
# Các giỏ 5 mã (bộ đo v4xv: tỷ trọng lẻ, ATC, cân lại 21 phiên)
s2 = open(D + '/v4xv.py', encoding='utf-8').read()
exec(s2[s2.index('Rd = R.where'):s2.index('_m = MKT')])
chay_gio = chay
for ten, q in QUY.items():
    DONG['Gio ' + ten[:40]] = q4(chay_gio(q))
rs = [q4(chay_gio(QUY['Q3 it bien dong nhat, top100 TK'], seed=s)) for s in range(8)]
DONG['Gio ngau nhien top100 (TB 8 lan)'] = pd.concat(rs, axis=1).mean(axis=1)
T = pd.DataFrame(DONG).T * 100
T['TB'] = T[list(NAM)].mean(axis=1); T['trung vi'] = T[list(NAM)].median(axis=1)
T['so nam duong'] = (T[list(NAM)] > 0).sum(axis=1); T['te nhat'] = T[list(NAM)].min(axis=1)
pd.set_option('display.width', 260)
print(T.sort_values('TB', ascending=False).round(1).to_string())
T.to_pickle(D + '/q4_dm.pkl')
