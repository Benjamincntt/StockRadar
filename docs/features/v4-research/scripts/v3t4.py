"""Quý 4 (01/10 → 31/12) 2017–2025 của V3 (luật production, phí 0,6%) và từng kịch bản V3 (cấp lệnh, tín hiệu trong quý 4)."""
import sys, warnings
warnings.filterwarnings('ignore')
src = open(sys.argv[1] + '/v3vi_sao.py', encoding='utf-8').read()
exec(src[:src.index("E, T, sk = run_kb4(P2")])
TET = ['2018-02-16', '2019-02-05', '2020-01-25', '2021-02-12', '2022-02-01', '2023-01-22', '2024-02-10', '2025-01-29', '2026-02-17']
def cua(E, CUA):
    out = {}
    for t in TET:
        t = pd.Timestamp(t); truoc = E[E.index < t]; sau = E[E.index >= t]
        if CUA == '15/12->Tet':
            a = E[E.index < pd.Timestamp(f'{t.year - 1}-12-15')]; out[t.year] = truoc.iloc[-1] / a.iloc[-1] - 1
        elif CUA == 'Tet-20': out[t.year] = truoc.iloc[-1] / truoc.iloc[-21] - 1
        else: out[t.year] = sau.iloc[19] / truoc.iloc[-1] - 1
    return pd.Series(out)
E, T, _ = run_kb4(P2, 40_000, 'gan', 1.0)
out = {}
for y in range(2017, 2026):
    a = E[E.index <= f'{y}-11-16']; b = E[E.index <= f'{y+1}-03-16']; out[y + 1] = (b.iloc[-1] / a.iloc[-1] - 1) * 100
v = pd.Series(out)
print('V3 16/11 -> 16/03:', ' '.join(f'{y%100}:{x:+.1f}' for y, x in v.items()), f'| TB {v.mean():+.1f} trung vi {v.median():+.1f} nam duong {(v>0).sum()}/{len(v)} te {v.min():+.1f}')
