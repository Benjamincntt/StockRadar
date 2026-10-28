"""Vòng 4: ghép ứng viên vào danh mục P1++ realtime (5 chỗ, KB1/KB2/N1 x1.5, gửi tiền 4.5%)."""
import sys, pickle
exec(open(sys.argv[1] + '/flex3.py', encoding='utf-8').read().split("SEL = {")[0])
beta = R.rolling(60).cov(bc(mret)) / bc(mret.rolling(60).var())

def mk(sig, kb, en, **kw):
    t = simulate_real2(sig, entry=en, **kw); return (kb, tag(t[t.date >= '2023-01-01'], kb, en))

a2 = CANDS['A2 TT giam >=1.5% (N4 cu)']
X = {
    'A2 mo cua, chot +5% / het 10 ATO': mk(a2, 'A2', 'open', hold=10, tp=0.05, expire_open=True),
    'A2 mo cua, ATO sau 8 phien': mk(a2, 'A2', 'open', hold=8, expire_open=True),
    'A2 mo cua, F MA10 giu 15': mk(a2, 'A2', 'open', **FX(10, 15)),
    'W  T2 ATC -> T5 ATO, beta cao': mk(topk((WD == 0) & (TV > 50), beta, 5), 'W', 'atc', hold=3, expire_open=True),
    'C2 ATC, F MA10 giu 15': mk(CANDS['C2 nganh giam >=4%/5p khi TT tren MA50, ma tren MA50'], 'C2', 'atc', **FX(10, 15)),
}
B = [BASE[0], BASE[1], k4, n1r]
def row(parts, label):
    E, T = run_pf(parts, 5, size_fn=mult, park_r=D45, park_cost=(0, 0))
    m = E.resample('ME').last(); mo = (m / m.shift(1).fillna(1.0) - 1)
    act = T.groupby(T.date.dt.to_period('M')).size().reindex(pd.period_range('2023-01', '2026-10', freq='M'), fill_value=0)
    split2(E, T, label)
    print(f"      thang {(T.ret > 0.0005).mean()*100:.0f}% | lenh {len(T)} | thang khong co lenh moi: {(act == 0).sum()}/{len(act)} | thang te {mo.min()*100:.1f}% | theo KB: "
          + ', '.join(f"{k} {len(g)}/{(g.ret>0.0005).mean()*100:.0f}%/{g.ret.mean()*100:+.1f}%" for k, g in T.groupby('kb')))

row(B + [N3F], 'P1++ hien tai (KB1 KB2 KB4 N1 N3)')
row(B, 'Bo N3')
for nm, p in X.items():
    row(B + [p], f'Thay N3 bang {nm}')
    row(B + [N3F, p], f'Giu N3, them {nm} (uu tien thap nhat)')
row(B + [X['A2 mo cua, chot +5% / het 10 ATO'], N3F], 'A2 (chot +5%) uu tien truoc N3')
