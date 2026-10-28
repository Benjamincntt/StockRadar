"""Danh mục V3 (luật production: P1++ realtime + PH-21) với tiền thật và lô 100 cổ phiếu.
Giá trong dữ liệu là nghìn đồng → vốn 40 triệu = 40.000. Không gửi tiền (app để tiền mặt 0%)."""
import sys
exec(open(sys.argv[1] + '/flex10.py', encoding='utf-8').read().split("# kiểm: deep rỗng")[0])

deep = DEEPS['do rong < 0,15']; rule = RULES['chot +15%, giu 40']
PARTS = []
for nm in ('KB1', 'KB2', 'KB4', 'N1'):
    t = sim_mix(SC[nm][0], KBN[nm][1], NORMAL[nm], rule, deep)
    kb, mode = KBN[nm]; PARTS.append((kb, tag(t, kb, mode)))
PARTS.append(N3F)


def run_lot(parts, v0, mode, slots=5, lot=100):
    """mode: 'le' = mua lẻ cổ phiếu (như backtest cũ); 'bo' = không đủ 1 lô thì bỏ, xét mã kế;
    'tron' = không đủ 1 lô mà tiền mặt đủ 1 lô thì mua 1 lô. Đủ lô thì mua số lô chẵn (làm tròn xuống)."""
    cand = pd.concat([p.assign(prio=n) for n, (_, p) in enumerate(parts)], ignore_index=True)
    cand = cand[cand.date >= '2023-01-01'].sort_values(['e', 'prio']).reset_index(drop=True)
    by_e = {e: g for e, g in cand.groupby('e')}
    i0 = dates.get_loc(dates[dates >= '2023-01-01'][0])
    cash = v0; pos = []; eq = []; taken = []; skipped = 0; over = 0; prev = v0
    cc = C.values; col = {s: j for j, s in enumerate(syms)}
    for d in range(i0, len(dates)):
        keep = []
        for p in pos:
            if p['k'] == d: cash += p['cost'] * (1 + p['ret'])
            else: keep.append(p)
        pos = keep
        if d in by_e:
            for _, x in by_e[d].iterrows():
                if len(pos) >= slots: break
                if any(p['sym'] == x.sym for p in pos): continue
                want = min(cash, prev / slots * mult(x))
                px = x.entry
                if mode == 'le':
                    sh = want / px
                elif mode == 'gan':
                    sh = max(np.floor(want / px / lot + 0.5), 1) * lot
                    while sh > 0 and sh * px > cash: sh -= lot
                    if sh == 0: skipped += 1; continue
                    if sh * px > want * 1.0001: over += 1
                else:
                    sh = np.floor(want / px / lot) * lot
                    if sh == 0:
                        if mode == 'tron' and cash >= lot * px: sh = lot; over += 1
                        else: skipped += 1; continue
                cost = sh * px
                if cost <= 1e-9: break
                cash -= cost
                pos.append(dict(sym=x.sym, k=int(x.k), cost=cost, ent=px, ret=x.ret, kb=x.kb, sh=sh))
                taken.append(x)
        v = cash + sum(p['sh'] * (cc[d, col[p['sym']]] if not np.isnan(cc[d, col[p['sym']]]) else p['ent']) for p in pos)
        eq.append(v); prev = v
    E = pd.Series(eq, index=dates[i0:]) / v0
    return E, pd.DataFrame(taken), skipped, over


for v0 in (40_000, 100_000):
    print(f'\n### Von {v0/1000:.0f} trieu')
    for mode, lab in (('le', 'mua le (backtest cu)'), ('bo', 'khong du lo -> bo, xet ma ke'), ('tron', 'khong du lo -> mua 1 lo neu du tien'), ('gan', 'lam tron lo gan nhat (it nhat 1 lo)')):
        E, T, sk, ov = run_lot(PARTS, v0, mode)
        a = E[E.index <= IS[1]]
        yr = ' '.join(f"{y}:{(E[E.index.year==y].iloc[-1]/(E[E.index.year<y].iloc[-1] if (E.index.year<y).any() else 1)-1)*100:+.0f}%" for y in (2023, 2024, 2025, 2026))
        print(f"  {lab:38s} tong {(E.iloc[-1]-1)*100:+4.0f}% sut {(E/E.cummax()-1).min()*100:5.1f}% | trong {(a.iloc[-1]-1)*100:+4.0f}% ngoai {(E.iloc[-1]/a.iloc[-1]-1)*100:+4.0f}% | {yr} | lenh {len(T)} thang {(T.ret>0.0005).mean()*100:.0f}% | bo {sk} | tron len {ov}")
