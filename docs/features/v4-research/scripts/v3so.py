"""V3 (luật production P1++ realtime + PH-21, KB1/KB2/N1 ×1,5, 5 chỗ, 40 triệu, lô 100) chạy từ 2017 trên dữ liệu sạch, phí 0,4% và 0,6%."""
import sys, warnings
warnings.filterwarnings('ignore')
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])
print('ngay cuoi', dates[-1].date(), '| lenh moi kich ban:', {kb: len(t) for kb, t in PARTS})
def bao(E, ten):
    nam = E.groupby(E.index.year).last() / E.groupby(E.index.year).first() - 1
    a = E[E.index <= '2022-12-31']; b = E[E.index >= '2023-01-01']
    cagr = E.iloc[-1] ** (252 / len(E)) - 1; sut = (E / E.cummax() - 1).min()
    sb = (b / b.cummax() - 1).min()
    print(f"{ten:34s} | tong {(E.iloc[-1]-1)*100:+6.0f}% nam {cagr*100:+5.1f}% sut {sut*100:6.1f}% l/s {cagr/-sut:5.2f} | 17-22 {(a.iloc[-1]/a.iloc[0]-1)*100:+5.0f}% 23-26 {(b.iloc[-1]/b.iloc[0]-1)*100:+5.0f}% (sut {sb*100:.1f}%) | "
          + ' '.join(f"{y%100}:{v*100:+4.0f}" for y, v in nam.items()), flush=True)
for them, ten in ((0.0, 'V3 phi 0,4% (nhu so V3)'), (0.002, 'V3 phi 0,6% (nhu bo do V4)')):
    P2 = [(kb, t.assign(ret=t.ret - them)) for kb, t in PARTS]
    E, T, sk = run_kb4(P2, 40_000, 'gan', 1.0)
    bao(E, ten)
    print(f"{'':34s}   lenh {len(T)}, thang {(T.ret > .0005).mean()*100:.0f}%, TB {T.ret.mean()*100:+.2f}%; theo nam so lenh: {T.groupby(T.date.dt.year).size().to_dict()}")
