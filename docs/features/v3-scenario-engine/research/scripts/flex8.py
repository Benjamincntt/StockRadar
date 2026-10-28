"""Vòng 8: đợt sập 2025 — lệnh bắt đáy bán theo từng luật được bao nhiêu, bỏ lỡ bao nhiêu so với đỉnh sau đó."""
import sys
exec(open(sys.argv[1] + '/flex6.py', encoding='utf-8').read().split("row({}, ")[0])

w = (dates >= '2025-02-15') & (dates <= '2025-06-30')
print('Chi so binh quan deu, do rong, lech MA50 (cac phien dang chu y):')
x = pd.DataFrame({'MKT': MKT, 'ret%': mret * 100, 'BREADTH50': BREADTH50, 'lechMA50%': (MKT / MKT_MA50 - 1) * 100})[w]
print(x[(x['ret%'].abs() > 2) | (x.BREADTH50 < 0.25)].round(3).to_string())

RULES = {'MA (hien tai)': None, 'chot +3%': 'chot +3%', 'chot +5%': 'chot +5%', 'chot +10%': 'chot +10%', 'chi giu het han': 'chi giu het han'}
cc = C.values; hh = H.values
for nm in ('KB1', 'KB2', 'KB4', 'N1'):
    out = []
    for rn, exn in RULES.items():
        t = (MA_PARTS[nm][1] if exn is None else part(nm, exn)[1])
        t = t[(t.date >= '2025-03-01') & (t.date <= '2025-05-31')]
        out.append(t.assign(rule=rn))
    a = pd.concat(out)
    if a.empty: continue
    piv = a.pivot_table(index=['date', 'sym'], columns='rule', values='ret', aggfunc='first') * 100
    # đỉnh cao nhất trong 60 phiên sau ngày mua, so với giá vào
    ent = a[a.rule == 'chi giu het han'].set_index(['date', 'sym']).entry
    mx = []
    for (d, s), e_ in ent.items():
        i = dates.get_loc(d); j = syms.get_loc(s)
        mx.append((np.nanmax(hh[i + 1:i + 61, j]) / e_ - 1) * 100 if not np.isnan(e_) else np.nan)
    piv['dinh 60 phien sau'] = pd.Series(mx, index=ent.index)
    print(f'\n### {nm}: lai rong tung lenh (%) theo luat ban, tin hieu 03-05/2025 (moi tin hieu, chua qua danh muc)')
    print(piv.round(1).to_string())
    print('  TB:', piv.mean().round(2).to_dict())

print('\n### Danh muc trong 2025 theo luat ban')
for rn, exn in RULES.items():
    parts = [MA_PARTS[nm] if exn is None else part(nm, exn) for nm in ('KB1', 'KB2', 'KB4', 'N1')] + [N3F]
    E, T = run_pf(parts, 5, size_fn=mult, park_r=D45, park_cost=(0, 0))
    def seg(a, b): s = E[(E.index >= a) & (E.index <= b)]; p = E[E.index < a].iloc[-1]; return (s.iloc[-1] / p - 1) * 100
    print(f"{rn:18s} 03-06/2025 {seg('2025-03-01','2025-06-30'):+6.1f}% | ca 2025 {seg('2025-01-01','2025-12-31'):+6.1f}% | tong {(E.iloc[-1]-1)*100:+.0f}%")
