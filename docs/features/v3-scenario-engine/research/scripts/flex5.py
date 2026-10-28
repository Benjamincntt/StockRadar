"""Vòng 5: luật bán KHÔNG dùng MA — chốt lời cố định, trượt theo đỉnh, chốt lời động, chỉ giữ theo thời gian.
Áp cho 5 kịch bản P1++ và các ứng viên vòng trước. Mọi luật giá đều realtime trên giá khớp."""
import sys, pickle
exec(open(sys.argv[1] + '/flex4.py', encoding='utf-8').read().split("B = [BASE[0]")[0])


def sim_x(sig, entry, hold, tp=None, trail=None, act=None, exit_open_rule=None, expire_open=False):
    """Mua 'open' (mở cửa phiên sau) hoặc 'atc' (đóng cửa phiên tín hiệu). Bán realtime khi:
    tp: giá khớp ≥ giá vào × (1+tp); trail: giá khớp ≤ đỉnh từ lúc mua × (1−trail);
    act: chỉ bật trượt khi đỉnh đã ≥ giá vào × (1+act), và mức trượt không thấp hơn giá vào + phí.
    Hết hạn: ATC phiên giữ cuối (expire_open=True thì ATO). Phiên T+2 chỉ bán buổi chiều (xét giá đóng cửa).
    Sàn trắng không bán được. Trong cùng một nến, mức trượt được xét trước chốt lời (thận trọng)."""
    sig = (sig & UNI).fillna(False).values
    o, h, l, c = O.values, H.values, L.values, C.values; bad = BAD.values; lim = LIM.values
    exo = exit_open_rule.fillna(False).values if exit_open_rule is not None else None
    out = []; n = len(dates)
    for j in range(len(syms)):
        busy = -1
        for i in np.nonzero(sig[:, j])[0]:
            if i <= busy or i < 1: continue
            if entry == 'atc':
                e = i; ent = c[i, j]
                if np.isnan(ent) or c[i, j] >= c[i - 1, j] * (1 + lim[j] - 0.005): continue
            else:
                e = i + 1
                if e >= n or np.isnan(o[e, j]) or o[e, j] >= c[i, j] * (1 + lim[j] - 0.005): continue
                ent = o[e, j]
            t2 = e + 2; last = i + hold
            if last >= n or t2 >= n: continue
            peak = ent; px = None; k = e; skipped = False
            while k < n:
                if bad[k, j]: skipped = True; break
                if k >= t2 and not np.isnan(c[k, j]) and not floor_locked(k, j, o, h, l, c, lim):
                    lv = ent * (1 + tp) if tp is not None else None
                    ts = None
                    if trail is not None and (act is None or peak >= ent * (1 + act)):
                        ts = peak * (1 - trail)
                        if act is not None: ts = max(ts, ent * (1 + COST))
                    if k == t2:
                        if ts is not None and c[k, j] <= ts: px = c[k, j]; break
                        if lv is not None and c[k, j] >= lv: px = c[k, j]; break
                        if k >= last and not expire_open: px = c[k, j]; break
                    else:
                        if exo is not None and exo[k, j]: px = o[k, j]; break
                        if expire_open and k >= last: px = o[k, j]; break
                        if ts is not None and l[k, j] <= ts: px = min(o[k, j], ts); break
                        if lv is not None and h[k, j] >= lv: px = max(o[k, j], lv); break
                        if k >= last: px = c[k, j]; break
                if not np.isnan(h[k, j]) and (k > e or entry == 'open'): peak = max(peak, h[k, j])
                k += 1
            if skipped: busy = k; continue
            if px is None: break
            out.append((dates[i], syms[j], ent, px, k - e + 1, px / ent - 1 - COST))
            busy = k
    t = pd.DataFrame(out, columns=['date', 'sym', 'entry', 'exit', 'days', 'ret'])
    return t[t.date >= '2023-01-01']


n3sig = topk(base3 & ~((md > -0.015) & (md < 0.015)), RS63, 5)
SC = {  # tên: (tín hiệu, cách mua, giữ tối đa, luật thoát lịch riêng, luật MA hiện tại)
    'KB1': (SIG['KB1'][0], 'open', 15, None, FX(10, 15)),
    'KB2': (SIG['KB2'][0], 'open', 15, None, FX(10, 15)),
    'KB4': (sig4, 'atc', 10, None, FX(10, 10)),
    'N1': (n1, 'atc', 15, None, FX(20, 15)),
    'N3': (n3sig, 'atc', 12, TDM == 2, None),
    'A2': (CANDS['A2 TT giam >=1.5% (N4 cu)'], 'open', 10, None, FX(10, 15)),
    'C2': (CANDS['C2 nganh giam >=4%/5p khi TT tren MA50, ma tren MA50'], 'atc', 15, None, FX(10, 15)),
    'W': (topk((WD == 0) & (TV > 50), beta, 5), 'atc', 3, None, None),
}
def EX(hd):
    return {
        'chi giu het han': dict(hold=hd),
        'chot +3%': dict(hold=hd, tp=0.03), 'chot +5%': dict(hold=hd, tp=0.05),
        'chot +7%': dict(hold=hd, tp=0.07), 'chot +10%': dict(hold=hd, tp=0.10),
        'truot 5% tu dinh': dict(hold=hd, trail=0.05), 'truot 8% tu dinh': dict(hold=hd, trail=0.08),
        'truot 8%, giu x2': dict(hold=hd * 2, trail=0.08),
        'lai>=3% thi truot 2%': dict(hold=hd, act=0.03, trail=0.02),
        'lai>=5% thi truot 3%': dict(hold=hd, act=0.05, trail=0.03),
        'lai>=5% thi truot 5%': dict(hold=hd, act=0.05, trail=0.05),
        'lai>=5% truot 3%, giu x2': dict(hold=hd * 2, act=0.05, trail=0.03),
        'chot +5% hoac lai>=3% truot 2%': dict(hold=hd, tp=0.05, act=0.03, trail=0.02),
    }

RES = {}
for nm, (sig, en, hd, exo, ma_kw) in SC.items():
    print(f'\n### {nm} (mua {en}, giu toi da {hd})')
    rows = {}
    if ma_kw is not None:
        rows['MA hien tai (F)'] = simulate_real2(sig, entry=en, **ma_kw); rows['MA hien tai (F)'] = rows['MA hien tai (F)'][rows['MA hien tai (F)'].date >= '2023-01-01']
    if exo is not None:
        rows['ATO TDM=2 hien tai'] = sim_x(sig, en, hd, exit_open_rule=exo)
    for exn, kw in EX(hd).items():
        rows[exn] = sim_x(sig, en, exit_open_rule=exo, **kw)
    for exn, t in rows.items():
        s = summarize(t); RES[(nm, exn)] = t
        print(f"  {exn:32s} n{s['n']:4d} | thang {s['winIS']:5}/{s['winOOS']:5} Q4 {s['winQ4']:5} min {s['winMinNam']:5} | TB {s['avg']:+5.2f} lo {s['avgLoss']:6} pf {s['pf']:5} te {s['worst']:6} giu {s['hold']}")
pickle.dump(RES, open(sys.argv[1] + '/flex5.pkl', 'wb'))
