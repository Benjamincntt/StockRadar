"""Trần tỉ trọng khi thị trường yếu: chỉ số (đóng cửa phiên trước) dưới MA50 thì tổng giá trị cổ phiếu đang giữ
không vượt CAP × giá trị danh mục. Tuỳ chọn để KB1 được dùng phần tiền giữ lại (mục đích: còn tiền cho hoảng loạn sâu).
Danh mục luật production (P1++ realtime + PH-21), tiền thật 40 triệu, lô 100, làm tròn lô gần nhất, tiền mặt lãi 0%."""
import sys
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])

lech = (MKT / MKT_MA50 - 1).values


def run_cap(parts, v0, cap, nguong, mien_kb1, slots=5, lot=100):
    """Như run_kb4 (KB4 ×1, làm tròn lô gần nhất) thêm trần tỉ trọng khi lech[d-1] < nguong."""
    def he_so(x):
        return 1.5 if x.kb in ('KB1', 'KB2', 'N1t') else 1.0
    cand = pd.concat([p.assign(prio=n) for n, (_, p) in enumerate(parts)], ignore_index=True)
    cand = cand[cand.date >= '2023-01-01'].sort_values(['e', 'prio']).reset_index(drop=True)
    by_e = {e: g for e, g in cand.groupby('e')}
    i0 = dates.get_loc(dates[dates >= '2023-01-01'][0])
    cash = v0; pos = []; eq = []; taken = []; skipped = 0; capped = 0; prev = v0
    cc = C.values; col = {s: j for j, s in enumerate(syms)}

    def gia(p, d):
        v = cc[d, col[p['sym']]]
        return p['ent'] if np.isnan(v) else v
    for d in range(i0, len(dates)):
        keep = []
        for p in pos:
            if p['k'] == d: cash += p['cost'] * (1 + p['ret'])
            else: keep.append(p)
        pos = keep
        yeu = cap < 1.0 and lech[d - 1] < nguong
        if d in by_e:
            for _, x in by_e[d].iterrows():
                if len(pos) >= slots: break
                if any(p['sym'] == x.sym for p in pos): continue
                tien_dung = cash
                if yeu and not (mien_kb1 and x.kb == 'KB1'):
                    dang_giu = sum(p['sh'] * gia(p, d - 1) for p in pos)
                    tien_dung = min(cash, max(0.0, cap * prev - dang_giu))
                want = min(tien_dung, prev / slots * he_so(x))
                px = x.entry
                sh = max(np.floor(want / px / lot + 0.5), 1) * lot
                while sh > 0 and sh * px > tien_dung: sh -= lot
                if sh == 0:
                    if tien_dung < cash: capped += 1
                    else: skipped += 1
                    continue
                cost = sh * px
                cash -= cost
                pos.append(dict(sym=x.sym, k=int(x.k), cost=cost, ent=px, ret=x.ret, kb=x.kb, sh=sh))
                taken.append(dict(date=x.date, kb=x.kb, ret=x.ret, w=cost / prev, mat=cost * x.ret / prev))
        v = cash + sum(p['sh'] * gia(p, d) for p in pos)
        eq.append(v); prev = v
    E = pd.Series(eq, index=dates[i0:]) / v0
    return E, pd.DataFrame(taken), skipped, capped


def theo_nam(E):
    out = []
    for y in (2023, 2024, 2025, 2026):
        cur = E[E.index.year == y]; prv = E[E.index.year < y]
        out.append(f"{y}:{(cur.iloc[-1]/(prv.iloc[-1] if len(prv) else 1)-1)*100:+.0f}%")
    return ' '.join(out)


def dong(lab, E, T, sk, cp):
    a = E[E.index <= IS[1]]
    m = E.resample('ME').last(); m = (m / m.shift(1).fillna(1) - 1).min() * 100
    kb1 = T[T.kb == 'KB1']
    print(f"  {lab:34s} tong {(E.iloc[-1]-1)*100:+4.0f}% | sut {(E/E.cummax()-1).min()*100:5.1f}% | thang te {m:5.1f}% "
          f"| trong {(a.iloc[-1]-1)*100:+4.0f}% ngoai {(E.iloc[-1]/a.iloc[-1]-1)*100:+4.0f}% | lenh {len(T)} (KB1 {len(kb1)}) "
          f"| bi tran chan {cp} | {theo_nam(E)}")


V0 = int(sys.argv[2]) if len(sys.argv) > 2 else 40_000
print(f'\n### Von {V0/1000:.0f} trieu')
dong('goc (khong tran)', *run_cap(PARTS, V0, 1.0, 0.0, False))
for nguong, ten in ((0.0, 'chi so < MA50'), (-0.05, 'chi so < MA50 -5%')):
    for mien in (False, True):
        print(f'  -- khi {ten}, {"KB1 duoc dung phan giu lai" if mien else "ap cho moi kich ban"}')
        for cap in (0.5, 0.6, 0.7, 0.8):
            dong(f'tran {cap*100:.0f}%', *run_cap(PARTS, V0, cap, nguong, mien))
