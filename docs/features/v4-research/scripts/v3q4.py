"""Quý 4 (01/10 → 31/12) 2017–2025 của V3 (luật production, phí 0,6%) và từng kịch bản V3 (cấp lệnh, tín hiệu trong quý 4)."""
import sys, warnings
warnings.filterwarnings('ignore')
src = open(sys.argv[1] + '/v3vi_sao.py', encoding='utf-8').read()
exec(src[:src.index("E, T, sk = run_kb4(P2")])
NAM = range(2017, 2026)
def q4(E):
    out = {}
    for y in NAM:
        a = E[E.index <= f'{y}-09-30']; b = E[E.index <= f'{y}-12-31']
        if len(a) and a.index[-1].year == y: out[y] = b.iloc[-1] / a.iloc[-1] - 1
    return pd.Series(out)
E, T, _ = run_kb4(P2, 40_000, 'gan', 1.0)
s = q4(E) * 100
print('V3 danh muc, quy 4 tung nam (%):', ' '.join(f'{y%100}:{v:+.1f}' for y, v in s.items()), f'| TB {s.mean():+.1f} trung vi {s.median():+.1f} nam duong {(s>0).sum()}/{len(s)} te {s.min():+.1f}')
M = (PHA4 == 4)
P3 = [(kb, t[~M.reindex(pd.DatetimeIndex(t.date)).fillna(False).values]) for kb, t in P2]
E3, _, _ = run_kb4(P3, 40_000, 'gan', 1.0); s3 = q4(E3) * 100
print('V3 + khong vao khi Giam gia:', ' '.join(f'{y%100}:{v:+.1f}' for y, v in s3.items()), f'| TB {s3.mean():+.1f} trung vi {s3.median():+.1f} nam duong {(s3>0).sum()}/{len(s3)} te {s3.min():+.1f}')
print('\nTung kich ban V3, lenh co tin hieu trong thang 10-12 (cap lenh, phi 0,6%):')
for kb, t in P2:
    t = t[(t.date.dt.month >= 10) & (t.date.dt.year.isin(list(NAM)))]
    g = t.groupby(t.date.dt.year).ret.mean() * 100
    print(f'{kb:5s} n{len(t):4d} thang {(t.ret>.0005).mean()*100:3.0f}% TB {t.ret.mean()*100:+5.2f}% | theo nam: ' + ' '.join(f'{y%100}:{v:+.1f}({(t.date.dt.year==y).sum()})' for y, v in g.items())
          + f' | nam duong {(g>0).sum()}/{len(g)}')
