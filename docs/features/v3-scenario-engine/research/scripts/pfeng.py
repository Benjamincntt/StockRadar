"""Bộ mô phỏng danh mục tính giá trị hằng ngày (lãi kép), dùng cho P1 và các biến thể."""
import sys
sys.path.insert(0, sys.argv[1])
exec(open(sys.argv[1] + '/mix2.py', encoding='utf-8').read().split("print('\\nDanh muc")[0])

MODE = {'KB1': 'open', 'KB2': 'open', 'KB4t': 'atc', 'N1t': 'atc', 'N3t': 'atc', 'N4': 'open'}
SECTOR = meta.sector.reindex(syms).fillna('')


def tag(t, kb, mode=None):
    """Gắn chỉ số phiên mua (e) và phiên bán (k) cho danh sách lệnh của một kịch bản."""
    mode = mode or MODE.get(kb, 'open')
    t = t.copy()
    i = dates.get_indexer(t.date)
    t['e'] = i if mode == 'atc' else i + 1
    t['k'] = t.e + t.days - 1
    t['kb'] = kb
    return t


def run_pf(parts, slots=5, sector_cap=None, max_new_day=None, start='2023-01-01', dd_brake=None, size_fn=None, kb_cap=None, brake_kbs=None, park_r=None, park_cost=(0.0015, 0.0025)):
    """parts: list (tên, bảng lệnh đã tag) theo thứ tự ưu tiên.
    Mỗi lệnh dùng min(tiền mặt, giá trị danh mục / slots). Trả (chuỗi giá trị, bảng lệnh đã vào)."""
    cand = pd.concat([p.assign(prio=n) for n, (_, p) in enumerate(parts)], ignore_index=True)
    cand = cand[cand.date >= start].sort_values(['e', 'prio']).reset_index(drop=True)
    by_e = {e: g for e, g in cand.groupby('e')}
    i0 = dates.get_loc(dates[dates >= start][0])
    cash = 1.0; pos = []; eq = []; taken = []; peak = 1.0; prev_eq = 1.0
    cc = C.values; colidx = {s: j for j, s in enumerate(syms)}
    for d in range(i0, len(dates)):
        if park_r is not None:
            cash *= 1 + park_r.iloc[d]           # tiền nhàn rỗi đỗ vào rổ/tiền gửi
        keep = []
        for p_ in pos:
            if p_['k'] == d:
                back = p_['alloc'] * (1 + p_['ret'])
                cash += back * (1 - (park_cost[0] if park_r is not None else 0))
            else:
                keep.append(p_)
        pos = keep
        if d in by_e:
            braking = dd_brake is not None and prev_eq < peak * (1 - dd_brake)
            new_today = 0
            for _, x in by_e[d].iterrows():
                if braking and (brake_kbs is None or x.kb in brake_kbs): continue
                if len(pos) >= slots: break
                if max_new_day and new_today >= max_new_day: break
                if any(p_['sym'] == x.sym for p_ in pos): continue
                if sector_cap and sum(SECTOR[p_['sym']] == SECTOR[x.sym] for p_ in pos) >= sector_cap: continue
                if kb_cap and x.kb in kb_cap and sum(p_.get('kb') == x.kb for p_ in pos) >= kb_cap[x.kb]: continue
                alloc = min(cash, prev_eq / slots * (size_fn(x) if size_fn else 1.0))
                if alloc <= 1e-9: break
                cash -= alloc
                if park_r is not None: cash -= alloc * park_cost[1]      # bán rổ để lấy tiền mua
                pos.append(dict(sym=x.sym, k=int(x.k), alloc=alloc, ent=x.entry, ret=x.ret, kb=x.kb))
                taken.append(x); new_today += 1
        v = cash
        for p_ in pos:
            px = cc[d, colidx[p_['sym']]]
            v += p_['alloc'] * (px / p_['ent'] if not np.isnan(px) else 1)
        eq.append(v); prev_eq = v; peak = max(peak, v)
    E = pd.Series(eq, index=dates[i0:])
    return E, pd.DataFrame(taken)


def metrics(E, T, label):
    """In lợi nhuận từng năm, sụt giảm tối đa, tháng tệ nhất, tỉ lệ thắng, lệnh tệ nhất."""
    ye = E.groupby(E.index.year).last()
    yr = ye / ye.shift(1).fillna(1.0) - 1
    dd = (E / E.cummax() - 1).min()
    mo = E.resample('ME').last().pct_change().fillna(E.resample('ME').last().iloc[0] - 1)
    q4 = [E[(E.index.year == y) & (E.index.quarter == 4)] for y in (2023, 2024, 2025)]
    q4r = [(q.iloc[-1] / E[E.index < q.index[0]].iloc[-1] - 1) * 100 for q in q4 if len(q)]
    w = (T.ret > 0.0005).mean() * 100
    s = ' | '.join(f"{y}: {r * 100:+.1f}%" for y, r in yr.items())
    print(f"{label:46s} {s} | tổng {(E.iloc[-1] - 1) * 100:+.0f}% | sụt max {dd * 100:.1f}% | tháng tệ {mo.min() * 100:.1f}% "
          f"| Q4 TB {np.mean(q4r):+.1f}% | thắng {w:.0f}% | lệnh {len(T)} | lệnh tệ {T.ret.min() * 100:.1f}%")
    return dict(E=E, T=T)


BASE = [('KB1', tag(TR['KB1'], 'KB1')), ('KB2', tag(TR['KB2'], 'KB2')), ('KB4t', tag(TR['KB4t'], 'KB4t')),
        ('N1t', tag(TR['N1t'], 'N1t')), ('N3t', tag(TR['N3t'], 'N3t'))]
