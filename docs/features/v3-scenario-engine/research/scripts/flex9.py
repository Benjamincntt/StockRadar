"""Vòng 9: luật bán riêng để lãi chạy cho lệnh mua lúc hoảng loạn sâu. Lệnh thường giữ luật MA hiện tại.
Mọi luật giá realtime trên giá khớp; hết hạn bán ATC. Bán một nửa: mô phỏng gộp, tiền nửa đầu coi như về lúc nửa sau bán (thận trọng)."""
import sys
exec(open(sys.argv[1] + '/flex6.py', encoding='utf-8').read().split("row({}, ")[0])

lech = MKT / MKT_MA50 - 1
DEEPS = {'chi so < MA50 -10%': lech < -0.10, 'chi so < MA50 -7%': lech < -0.07, 'do rong < 0,15': BREADTH50 < 0.15}
for nm, s in DEEPS.items():
    s = s[dates >= '2023-01-01']; ep = (s & ~s.shift(1, fill_value=False))
    print(f"{nm}: {int(s.sum())} phien, {int(ep.sum())} lan bat dau | " + ', '.join(d.strftime('%m/%Y') for d in s[ep].index))

ORDER = ('KB1', 'KB2', 'KB4', 'N1')
def ma_trades(nm, sig):
    _, en, _, _, kw = SC[nm]; t = simulate_real2(sig, entry=en, **kw); return t[t.date >= '2023-01-01']
RULES = {
    'giu 20 phien': dict(hold=20), 'giu 30 phien': dict(hold=30), 'giu 40 phien': dict(hold=40),
    'chot +15%, giu 40': dict(hold=40, tp=0.15), 'chot +20%, giu 40': dict(hold=40, tp=0.20),
    'lai>=10% truot 10%, giu 40': dict(hold=40, act=0.10, trail=0.10),
    'lai>=10% truot 7%, giu 60': dict(hold=60, act=0.10, trail=0.07),
}
HALF = {'nua MA + nua giu 30': dict(hold=30), 'nua MA + nua lai>=10% truot 10%, giu 60': dict(hold=60, act=0.10, trail=0.10)}

def deep_trades(nm, sig, rule):
    _, en, _, exo, _ = SC[nm]
    if rule == 'MA':
        return ma_trades(nm, sig)
    if rule in RULES:
        return sim_x(sig, en, exit_open_rule=exo, **RULES[rule])
    a = ma_trades(nm, sig).set_index(['date', 'sym']); b = sim_x(sig, en, **HALF[rule]).set_index(['date', 'sym'])
    j = a.join(b, lsuffix='_a', rsuffix='_b', how='inner')
    return pd.DataFrame({'date': j.index.get_level_values(0), 'sym': j.index.get_level_values(1), 'entry': j.entry_a.values,
                         'exit': np.nan, 'days': np.maximum(j.days_a, j.days_b).values, 'ret': (0.5 * j.ret_a + 0.5 * j.ret_b).values})

KBN = {'KB1': ('KB1', 'open'), 'KB2': ('KB2', 'open'), 'KB4': ('KB4t', 'atc'), 'N1': ('N1t', 'atc')}
def parts_for(deep, rule):
    out = []
    for nm in ORDER:
        sig = SC[nm][0]; D = bc(deep).astype(bool)
        t = pd.concat([ma_trades(nm, sig & ~D), deep_trades(nm, sig & D, rule)]).sort_values('date')
        kb, mode = KBN[nm]; out.append((kb, tag(t, kb, mode)))
    return out + [N3F]

def seg(E, a, b):
    s = E[(E.index >= a) & (E.index <= b)]; p = E[E.index < a]
    return (s.iloc[-1] / (p.iloc[-1] if len(p) else 1.0) - 1) * 100

all_rules = ['MA'] + list(RULES) + list(HALF)
for dn, deep in DEEPS.items():
    print(f'\n### Hoang loan sau = {dn}')
    for rule in all_rules:
        # thống kê lệnh hoảng loạn sâu (mọi tín hiệu, chưa qua danh mục)
        dt = pd.concat([deep_trades(nm, SC[nm][0] & bc(deep).astype(bool), rule) for nm in ORDER])
        E, T = run_pf(parts_for(deep, rule), 5, size_fn=mult, park_r=D45, park_cost=(0, 0))
        a = E[E.index <= IS[1]]
        print(f"  {rule:40s} lenh sau n{len(dt):3d} thang {(dt.ret>0.0005).mean()*100:3.0f}% TB {dt.ret.mean()*100:+5.2f}% te {dt.ret.min()*100:6.1f}% | "
              f"danh muc tong {(E.iloc[-1]-1)*100:+4.0f}% sut {(E/E.cummax()-1).min()*100:5.1f}% | trong {(a.iloc[-1]-1)*100:+4.0f}% ngoai {(E.iloc[-1]/a.iloc[-1]-1)*100:+4.0f}% | "
              f"10-12/2023 {seg(E,'2023-10-01','2023-12-31'):+5.1f}% 04-07/2025 {seg(E,'2025-04-01','2025-07-31'):+5.1f}% | nam " + ' '.join(f"{seg(E,f'{y}-01-01',f'{y}-12-31'):+.0f}" for y in (2023, 2024, 2025, 2026)))
