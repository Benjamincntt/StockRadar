"""Trước Tết: lãi các cửa sổ quanh Tết âm lịch, 2017/18 → 2025/26. Dựa trên v4q4.py: danh mục nào lãi nhiều và đều nhất cuối năm.
Lãi quý 4 = giá trị danh mục ngày giao dịch cuối cùng của 12 chia ngày giao dịch cuối cùng của 9 (đường vốn chạy liên tục,
nên vị thế mang sang từ tháng 9 vẫn tính). Năm 2026 chưa hết quý 4 → không tính."""
import sys, warnings, numpy as np, pandas as pd
warnings.filterwarnings('ignore')
D = sys.argv[1]
src = open(D + '/v4he.py', encoding='utf-8').read(); exec(src[:src.rindex("for ten, kw in (")])
TET = ['2018-02-16', '2019-02-05', '2020-01-25', '2021-02-12', '2022-02-01', '2023-01-22', '2024-02-10', '2025-01-29', '2026-02-17']
def cua(E, CUA):
    """Lãi cửa sổ: '15/12->Tet' (15/12 năm trước → phiên cuối trước Tết), 'Tet-20' (20 phiên trước Tết), 'Tet+20' (20 phiên sau Tết)."""
    out = {}
    for t in TET:
        t = pd.Timestamp(t); truoc = E[E.index < t]; sau = E[E.index >= t]
        if CUA == '15/12->Tet':
            a = E[E.index < pd.Timestamp(f'{t.year - 1}-12-15')]
            if len(a) == 0 or a.index[0] > pd.Timestamp(f'{t.year - 1}-12-15') - pd.Timedelta(days=40): continue
            out[t.year] = truoc.iloc[-1] / a.iloc[-1] - 1
        elif CUA == 'Tet-20':
            if len(truoc) < 21: continue
            out[t.year] = truoc.iloc[-1] / truoc.iloc[-21] - 1
        else:
            if len(sau) < 20 or len(truoc) == 0: continue
            out[t.year] = sau.iloc[19] / truoc.iloc[-1] - 1
    return pd.Series(out)
NAM = [pd.Timestamp(t).year for t in TET]
q4 = lambda E: E
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
DONG['Gio ngau nhien top100 (8 lan)'] = rs
pd.set_option('display.width', 260)
for CUA in ('15/12->Tet', 'Tet-20', 'Tet+20'):
    rows = {}
    for ten, E in DONG.items():
        rows[ten] = pd.concat([cua(e, CUA) for e in E], axis=1).mean(axis=1) if isinstance(E, list) else cua(E, CUA)
    T = pd.DataFrame(rows).T * 100
    cols = [c for c in NAM if c in T.columns]
    T['TB'] = T[cols].mean(axis=1); T['trung vi'] = T[cols].median(axis=1)
    T['so nam duong'] = (T[cols] > 0).sum(axis=1); T['te nhat'] = T[cols].min(axis=1)
    print()
    print(f'=== Cua so {CUA} (nam = nam cua Tet) ===')
    print(T.sort_values('TB', ascending=False).round(1).to_string())
