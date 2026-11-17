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
for CUA in ('15/12->Tet', 'Tet-20', 'Tet+20'):
    v = cua(E, CUA) * 100
    print(f'V3 {CUA:10s}:', ' '.join(f'{y%100}:{x:+.1f}' for y, x in v.items()), f'| TB {v.mean():+.1f} trung vi {v.median():+.1f} nam duong {(v>0).sum()}/{len(v)} te {v.min():+.1f}')
print()
print('Tung kich ban V3, lenh co tin hieu tu 15/12 toi truoc Tet (cap lenh, phi 0,6%):')
tet = [pd.Timestamp(t) for t in TET]
for kb, t in P2:
    m = np.zeros(len(t), bool)
    for x in tet: m |= ((t.date >= pd.Timestamp(f'{x.year-1}-12-15')) & (t.date < x)).values
    u = t[m]; g = u.groupby(u.date.dt.year).ret.mean() * 100
    print(f'{kb:5s} n{len(u):4d} thang {(u.ret>.0005).mean()*100:3.0f}% TB {u.ret.mean()*100:+5.2f}% | nam duong {(g>0).sum()}/{len(g)}')
