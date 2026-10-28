"""Vòng 10: như vòng 9 nhưng mô phỏng MỘT lần — mỗi lệnh chọn luật bán theo trạng thái ngày tín hiệu
(lệnh thường: chạm MA như hiện tại; lệnh hoảng loạn sâu: luật để lãi chạy). Tránh lệch do tách hai bộ tín hiệu."""
import sys
exec(open(sys.argv[1] + '/flex6.py', encoding='utf-8').read().split("row({}, ")[0])


def sim_mix(sig, entry, normal, deep, deep_mask):
    """normal / deep: dict(hold, ma=n hoặc None, tp=None). ma=n: bán khi giá khớp ≥ max(chạm MAn, giá vào + phí).
    Lệnh có ngày tín hiệu thuộc deep_mask dùng bộ 'deep'. Còn lại như sim_x (T+2.5, sàn trắng, hết hạn ATC)."""
    sg = (sig & UNI).fillna(False).values; dm = deep_mask.reindex(dates).fillna(False).values
    o, h, l, c = O.values, H.values, L.values, C.values; bad = BAD.values; lim = LIM.values
    lvls = {n: LV[n].values for n in (5, 10, 20)}
    out = []; n_ = len(dates)
    for j in range(len(syms)):
        busy = -1
        for i in np.nonzero(sg[:, j])[0]:
            if i <= busy or i < 1: continue
            p = deep if dm[i] else normal
            if entry == 'atc':
                e = i; ent = c[i, j]
                if np.isnan(ent) or c[i, j] >= c[i - 1, j] * (1 + lim[j] - 0.005): continue
            else:
                e = i + 1
                if e >= n_ or np.isnan(o[e, j]) or o[e, j] >= c[i, j] * (1 + lim[j] - 0.005): continue
                ent = o[e, j]
            t2 = e + 2; last = i + p['hold']
            if last >= n_ or t2 >= n_: continue
            px = None; k = e; skipped = False
            while k < n_:
                if bad[k, j]: skipped = True; break
                if k >= t2 and not np.isnan(c[k, j]) and not floor_locked(k, j, o, h, l, c, lim):
                    lv = None
                    if p.get('ma') and not np.isnan(lvls[p['ma']][k, j]): lv = max(lvls[p['ma']][k, j], ent * (1 + COST))
                    if p.get('tp') is not None: lv = ent * (1 + p['tp'])
                    if k == t2:
                        if lv is not None and c[k, j] >= lv: px = c[k, j]; break
                        if k >= last: px = c[k, j]; break
                    else:
                        if lv is not None and h[k, j] >= lv: px = max(o[k, j], lv); break
                        if k >= last: px = c[k, j]; break
                k += 1
            if skipped: busy = k; continue
            if px is None: break
            out.append((dates[i], syms[j], ent, px, k - e + 1, px / ent - 1 - COST, bool(dm[i])))
            busy = k
    t = pd.DataFrame(out, columns=['date', 'sym', 'entry', 'exit', 'days', 'ret', 'deep'])
    return t[t.date >= '2023-01-01']

NORMAL = {'KB1': dict(hold=15, ma=10), 'KB2': dict(hold=15, ma=10), 'KB4': dict(hold=10, ma=10), 'N1': dict(hold=15, ma=20)}
KBN = {'KB1': ('KB1', 'open'), 'KB2': ('KB2', 'open'), 'KB4': ('KB4t', 'atc'), 'N1': ('N1t', 'atc')}
lech = MKT / MKT_MA50 - 1
DEEPS = {'chi so < MA50 -10%': lech < -0.10, 'chi so < MA50 -7%': lech < -0.07, 'do rong < 0,15': BREADTH50 < 0.15}
RULES = {'MA (nhu hien tai)': None, 'giu 30': dict(hold=30), 'chot +10%, giu 30': dict(hold=30, tp=0.10),
         'chot +15%, giu 30': dict(hold=30, tp=0.15), 'chot +15%, giu 40': dict(hold=40, tp=0.15),
         'chot +20%, giu 40': dict(hold=40, tp=0.20), 'chot +15%, giu 60': dict(hold=60, tp=0.15)}

# kiểm: deep rỗng phải ra đúng như simulate_real2 hiện tại
for nm in ('KB1', 'KB4'):
    a = sim_mix(SC[nm][0], KBN[nm][1], NORMAL[nm], NORMAL[nm], pd.Series(False, index=dates)); b = MA_PARTS[nm][1]
    print(f'kiem {nm}: sim_mix n{len(a)} TB {a.ret.mean()*100:.3f}% | ban hien tai n{len(b)} TB {b.ret.mean()*100:.3f}%')

def seg(E, a, b):
    s = E[(E.index >= a) & (E.index <= b)]; p = E[E.index < a]
    return (s.iloc[-1] / (p.iloc[-1] if len(p) else 1.0) - 1) * 100

KEEP = {}
for dn, deep in DEEPS.items():
    print(f'\n### Hoang loan sau = {dn}')
    for rn, rule in RULES.items():
        parts, dts = [], []
        for nm in ('KB1', 'KB2', 'KB4', 'N1'):
            d_ = NORMAL[nm] if rule is None else rule
            t = sim_mix(SC[nm][0], KBN[nm][1], NORMAL[nm], d_, deep)
            kb, mode = KBN[nm]; parts.append((kb, tag(t, kb, mode))); dts.append(t[t.deep].assign(kb=nm))
        dt = pd.concat(dts)
        E, T = run_pf(parts + [N3F], 5, size_fn=mult, park_r=D45, park_cost=(0, 0))
        a = E[E.index <= IS[1]]; KEEP[(dn, rn)] = (E, T, dt)
        print(f"  {rn:20s} lenh sau n{len(dt):3d} thang {(dt.ret>0.0005).mean()*100:3.0f}% TB {dt.ret.mean()*100:+5.2f}% te {dt.ret.min()*100:6.1f}% giu {dt.days.mean():4.1f} | "
              f"tong {(E.iloc[-1]-1)*100:+4.0f}% sut {(E/E.cummax()-1).min()*100:5.1f}% | trong {(a.iloc[-1]-1)*100:+4.0f}% ngoai {(E.iloc[-1]/a.iloc[-1]-1)*100:+4.0f}% | nam "
              + ' '.join(f"{seg(E,f'{y}-01-01',f'{y}-12-31'):+.0f}" for y in (2023, 2024, 2025, 2026)))

# từng đợt: lệnh hoảng loạn sâu theo tháng, MA vs chốt +15% giữ 40 (mọi tín hiệu)
print('\n### Tung dot (chi so < MA50 -7%): TB lai rong lenh hoang loan sau, MA vs chot +15% giu 40')
m0 = KEEP[('chi so < MA50 -7%', 'MA (nhu hien tai)')][2]; m1 = KEEP[('chi so < MA50 -7%', 'chot +15%, giu 40')][2]
g = lambda t: t.groupby(t.date.dt.to_period('M')).ret.agg(['size', 'mean'])
x = g(m0).join(g(m1), lsuffix='_MA', rsuffix='_chot15'); x[['mean_MA', 'mean_chot15']] *= 100
print(x.round(2).to_string())
import pickle; pickle.dump({k: (v[0], v[1]) for k, v in KEEP.items()}, open(sys.argv[1] + '/flex10.pkl', 'wb'))
