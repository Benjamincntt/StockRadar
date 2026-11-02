"""Tỉ trọng KB4 (Q4 — rủi ro đuôi): danh mục luật production (P1++ realtime + PH-21), tiền thật, lô 100 cổ phiếu.
So KB4 ×1 / ×0,75 / ×0,5 / bỏ hẳn, với ba cách làm tròn lô. Đo thêm: lệnh tệ nhất mất bao nhiêu % danh mục,
tỉ trọng thực lớn nhất của một lệnh KB4 sau làm tròn. Giá nghìn đồng → vốn 40 triệu = 40.000. Tiền mặt lãi 0%."""
import sys
exec(open(sys.argv[1] + '/lot1.py', encoding='utf-8').read().split("for v0 in (40_000")[0])


def run_kb4(parts, v0, mode, kb4_mult, slots=5, lot=100):
    """Như run_lot của lot1.py, thêm hệ số riêng cho KB4 và ghi tỉ trọng thực + phần mất trên danh mục của từng lệnh."""
    def he_so(x):
        return kb4_mult if x.kb == 'KB4t' else (1.5 if x.kb in ('KB1', 'KB2', 'N1t') else 1.0)
    cand = pd.concat([p.assign(prio=n) for n, (_, p) in enumerate(parts)], ignore_index=True)
    cand = cand[cand.date >= '2023-01-01'].sort_values(['e', 'prio']).reset_index(drop=True)
    by_e = {e: g for e, g in cand.groupby('e')}
    i0 = dates.get_loc(dates[dates >= '2023-01-01'][0])
    cash = v0; pos = []; eq = []; taken = []; skipped = 0; prev = v0
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
                hs = he_so(x)
                if hs <= 0: continue
                want = min(cash, prev / slots * hs)
                px = x.entry
                if mode == 'gan':
                    sh = max(np.floor(want / px / lot + 0.5), 1) * lot
                    while sh > 0 and sh * px > cash: sh -= lot
                    if sh == 0: skipped += 1; continue
                else:
                    sh = np.floor(want / px / lot) * lot
                    if sh == 0:
                        if mode == 'tron' and cash >= lot * px: sh = lot
                        else: skipped += 1; continue
                cost = sh * px
                if cost <= 1e-9: break
                cash -= cost
                pos.append(dict(sym=x.sym, k=int(x.k), cost=cost, ent=px, ret=x.ret, kb=x.kb, sh=sh))
                taken.append(dict(date=x.date, kb=x.kb, ret=x.ret, w=cost / prev, mat=cost * x.ret / prev))
        v = cash + sum(p['sh'] * (cc[d, col[p['sym']]] if not np.isnan(cc[d, col[p['sym']]]) else p['ent']) for p in pos)
        eq.append(v); prev = v
    E = pd.Series(eq, index=dates[i0:]) / v0
    return E, pd.DataFrame(taken), skipped


def in_dong(lab, E, T, sk):
    a = E[E.index <= IS[1]]
    m = E.resample('ME').last(); m = (m / m.shift(1).fillna(1) - 1).min() * 100
    k4 = T[T.kb == 'KB4t']
    print(f"  {lab:30s} tong {(E.iloc[-1]-1)*100:+4.0f}% | sut {(E/E.cummax()-1).min()*100:5.1f}% | thang te {m:5.1f}% "
          f"| trong {(a.iloc[-1]-1)*100:+4.0f}% ngoai {(E.iloc[-1]/a.iloc[-1]-1)*100:+4.0f}% "
          f"| lenh te nhat mat {T.mat.min()*100:4.1f}% DM ({T.loc[T.mat.idxmin(),'kb']}) "
          f"| KB4: {len(k4):3d} lenh, mat te nhat {k4.mat.min()*100 if len(k4) else 0:4.1f}%, ti trong max {k4.w.max()*100 if len(k4) else 0:4.1f}% "
          f"| bo {sk}")


for v0 in (40_000, 100_000):
    print(f'\n### Von {v0/1000:.0f} trieu')
    for mode, ten in (('gan', 'tron lo gan nhat (dang dung)'), ('tron', 'tron xuong, it nhat 1 lo'), ('bo', 'tron xuong, thieu lo thi bo')):
        print(f'  -- {ten}')
        for hs, lab in ((1.0, 'KB4 x1 (20%)'), (0.75, 'KB4 x0,75 (15%)'), (0.5, 'KB4 x0,5 (10%)'), (0.0, 'bo KB4')):
            in_dong(lab, *run_kb4(PARTS, v0, mode, hs))
