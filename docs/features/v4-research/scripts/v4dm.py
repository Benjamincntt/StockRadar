"""V4 vòng 4: danh mục 5 chỗ, vốn 40 triệu, lô 100, tiền mặt 0%. So với mốc mua-giữ chỉ số đều trọng số và chọn mã ngẫu nhiên."""
import sys, pickle, numpy as np, pandas as pd
D = sys.argv[1]
exec(open(D + '/v4core.py', encoding='utf-8').read())
DTN = pickle.load(open(D + '/dt.pkl', 'rb'))
PHA_B = bc(PHA)
F2 = (RSI2 < 10) & (C > MA[200]) & (MA[50] > MA[200])
RSc = DTN['rs63'] == 2; NGv = DTN['nganh'] >= 1


def danh_muc(t, v0=40_000, slots=5, lot=100, uu_tien='rs', seed=0):
    t = t[t.date >= '2017-01-01'].copy()
    rng = np.random.default_rng(seed)
    t['prio'] = -RS63.values[t.i.values, t.sym.map({s: j for j, s in enumerate(syms)}).values] if uu_tien == 'rs' else rng.random(len(t))
    by_e = {e: g.sort_values('prio') for e, g in t.groupby('e')}
    i0 = dates.get_loc(dates[dates >= '2017-01-01'][0]); cc = C.values; col = {s: j for j, s in enumerate(syms)}
    cash = v0; pos = []; eq = []; prev = v0; nl = 0
    for d in range(i0, N):
        keep = []
        for p in pos:
            if p['k'] == d: cash += p['cost'] * (1 + p['ret'])
            else: keep.append(p)
        pos = keep
        if d in by_e:
            for _, x in by_e[d].iterrows():
                if len(pos) >= slots: break
                if any(p['sym'] == x.sym for p in pos): continue
                px = x.entry if 'entry' in x else cc[x.e, col[x.sym]]
                want = min(cash, prev / slots)
                sh = max(np.floor(want / px / lot + 0.5), 1) * lot
                while sh > 0 and sh * px > cash: sh -= lot
                if sh == 0: continue
                cash -= sh * px; nl += 1
                pos.append(dict(sym=x.sym, k=int(x.k), cost=sh * px, sh=sh, ent=px, ret=x.ret))
        v = cash + sum(p['sh'] * (cc[d, col[p['sym']]] if not np.isnan(cc[d, col[p['sym']]]) else p['ent']) for p in pos)
        eq.append(v); prev = v
    return pd.Series(eq, index=dates[i0:]) / v0, nl


def bao(E, ten, nl=None):
    nam = E.groupby(E.index.year).last() / E.groupby(E.index.year).first() - 1
    a = E[E.index <= '2022-12-31']; b = E[E.index >= '2023-01-01']
    cagr = E.iloc[-1] ** (252 / len(E)) - 1; sut = (E / E.cummax() - 1).min()
    print(f"{ten:52s} | tong {(E.iloc[-1]-1)*100:+6.0f}% nam {cagr*100:+5.1f}% sut {sut*100:6.1f}% lai/sut {cagr/-sut:4.2f} | 17-22 {(a.iloc[-1]/a.iloc[0]-1)*100:+5.0f}% 23-26 {(b.iloc[-1]/b.iloc[0]-1)*100:+5.0f}% | "
          + ' '.join(f"{y%100}:{v*100:+4.0f}" for y, v in nam.items()) + (f" | lenh {nl}" if nl is not None else ''), flush=True)


# ghi giá vào lệnh (entry) để danh mục dùng đúng giá mua
def lenh(sig, **kw):
    t = mo_phong(sig, **kw)
    t['entry'] = np.where(np.isin(t.e, t.i), np.nan, np.nan)
    t['entry'] = O.values[t.e.values, t.sym.map({s: j for j, s in enumerate(syms)}).values] * (1 + TRUOT)
    return t


bao(MKT.loc['2017-01-01':] / MKT.loc['2017-01-01':].iloc[0], 'MOC: mua giu chi so deu trong so 300 ma')
for ten, sig, kw in (
    ('A: F2 HoangLoan+HoiPhuc, RS cao, truot 3ATR/30p', F2 & RSc & PHA_B.isin([0, 2]), dict(hold=30, trail=3)),
    ('B: F2 HoangLoan+HoiPhuc, RS cao, truot 2ATR/20p', F2 & RSc & PHA_B.isin([0, 2]), dict(hold=20, trail=2)),
    ('C: nhu A, HoiPhuc them nganh>=vua', F2 & RSc & ((PHA_B == 0) | ((PHA_B == 2) & NGv)), dict(hold=30, trail=3)),
    ('D: F2 moi giai doan tru GiamGia, RS cao, truot 3ATR', F2 & RSc & (PHA_B != 4), dict(hold=30, trail=3)),
    ('E: F2 moi giai doan, RS cao, truot 3ATR (khong ne pha)', F2 & RSc, dict(hold=30, trail=3)),
):
    t = lenh(sig, **kw)
    E, nl = danh_muc(t); bao(E, ten, nl)
    if ten.startswith('A'):
        rs = [danh_muc(t, uu_tien='ngau', seed=s)[0] for s in range(10)]
        tong = [e.iloc[-1] - 1 for e in rs]; sut = [(e / e.cummax() - 1).min() for e in rs]
        print(f"{'   A nhung chon ma ngau nhien (10 lan)':52s} | tong TB {np.mean(tong)*100:+6.0f}% (min {min(tong)*100:+.0f}% max {max(tong)*100:+.0f}%) sut TB {np.mean(sut)*100:.1f}%", flush=True)
