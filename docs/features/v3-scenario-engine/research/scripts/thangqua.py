"""Bộ kịch bản V3 (luật production P1++ realtime + PH-21) trong một tháng gần nhất, KỂ CẢ lệnh còn đang mở
(đánh giá theo giá đóng cửa phiên cuối). Tín hiệu từ TU tới hết dữ liệu. Danh mục 40 triệu bắt đầu trống tại TU."""
import sys
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])

TU = sys.argv[2] if len(sys.argv) > 2 else '2026-10-01'
n_ = len(dates); CUOI = dates[-1]
print(f'\nDu lieu toi {CUOI.date()} | tin hieu tu {TU}')


def sim_mo(sig, entry, normal, deep_rule, deep_mask, ten):
    """Như sim_mix nhưng giữ lệnh chưa thoát (trạng thái 'dang mo', giá = đóng cửa phiên cuối)."""
    sg = (sig & UNI).fillna(False).values; dm = deep_mask.reindex(dates).fillna(False).values
    o, h, l, c = O.values, H.values, L.values, C.values; bad = BAD.values; lim = LIM.values
    lvls = {n: LV[n].values for n in (5, 10, 20)}
    i_tu = dates.get_indexer([dates[dates >= TU][0]])[0]
    out = []
    for j in range(len(syms)):
        busy = -1
        for i in np.nonzero(sg[:, j])[0]:
            if i <= busy or i < 1: continue
            p = deep_rule if dm[i] else normal
            if entry == 'atc':
                e = i; ent = c[i, j]
                if np.isnan(ent) or c[i, j] >= c[i - 1, j] * (1 + lim[j] - 0.005): continue
            else:
                e = i + 1
                if e >= n_:
                    if i >= i_tu: out.append((dates[i], ten, syms[j], np.nan, np.nan, 0, np.nan, 'cho mua phien sau', bool(dm[i])))
                    continue
                if np.isnan(o[e, j]) or o[e, j] >= c[i, j] * (1 + lim[j] - 0.005): continue
                ent = o[e, j]
            t2 = e + 2; last = i + p['hold']
            px = None; k = e; ly = None; hong = False
            while k < n_:
                if bad[k, j]: hong = True; break
                if k >= t2 and not np.isnan(c[k, j]) and not floor_locked(k, j, o, h, l, c, lim):
                    lv = None
                    if p.get('ma') and not np.isnan(lvls[p['ma']][k, j]): lv = max(lvls[p['ma']][k, j], ent * (1 + COST))
                    if p.get('tp') is not None: lv = ent * (1 + p['tp'])
                    if k == t2:
                        if lv is not None and c[k, j] >= lv: px, ly = c[k, j], 'cham nguong'; break
                        if k >= last: px, ly = c[k, j], 'het han'; break
                    else:
                        if lv is not None and h[k, j] >= lv: px, ly = max(o[k, j], lv), 'cham nguong'; break
                        if k >= last: px, ly = c[k, j], 'het han'; break
                k += 1
            if hong:
                busy = k; continue  # như sim_mix: phiên dữ liệu xấu thì bỏ lệnh
            if px is None:
                k = n_ - 1; px = c[k, j]; ly = 'dang mo'
            if i >= i_tu:
                out.append((dates[i], ten, syms[j], ent, px, k - e + 1, px / ent - 1 - COST, ly, bool(dm[i])))
            busy = k
    return pd.DataFrame(out, columns=['date', 'kb', 'sym', 'entry', 'exit', 'days', 'ret', 'ly', 'deep'])


def sim_n3(sig):
    """N3: mua ATC ngày tín hiệu, bán ATO phiên TDM==2 tháng sau (tối đa 12 phiên); chưa tới thì 'dang mo'."""
    sg = (sig & UNI).fillna(False).values; o, c = O.values, C.values; tdm = TDM.values
    i_tu = dates.get_indexer([dates[dates >= TU][0]])[0]; out = []
    for j in range(len(syms)):
        for i in np.nonzero(sg[:, j])[0]:
            if i < i_tu or np.isnan(c[i, j]): continue
            ent = c[i, j]; px = None
            for k in range(i + 1, min(n_, i + 13)):
                tk = tdm[k, j] if np.ndim(tdm) == 2 else tdm[k]
                if tk == 2 and not np.isnan(o[k, j]): px, ly = o[k, j], 'ATO TDM=2'; break
            if px is None: k = n_ - 1; px = c[k, j]; ly = 'dang mo'
            out.append((dates[i], 'N3', syms[j], ent, px, k - i + 1, px / ent - 1 - COST, ly, False))
    return pd.DataFrame(out, columns=['date', 'kb', 'sym', 'entry', 'exit', 'days', 'ret', 'ly', 'deep'])


parts = []
for nm in ('KB1', 'KB2', 'KB4', 'N1'):
    kb, mode = KBN[nm]
    parts.append(sim_mo(SC[nm][0], mode, NORMAL[nm], rule, deep, nm))
md_ = bc(MKT / MKT_MA50 - 1)
n3sig = topk(((LEFT == 5) & (TV > 30)) & ~((md_ > -0.015) & (md_ < 0.015)), RS63, 5)
parts.append(sim_n3(n3sig))
T = pd.concat([p for p in parts if len(p)], ignore_index=True)
T['date'] = pd.to_datetime(T['date']); T = T.sort_values(['date', 'kb', 'sym'])

pd.set_option('display.width', 200)
show = T.assign(ret=(T.ret * 100).round(2), date=T.date.dt.strftime('%d/%m'))
print('\n### Moi tin hieu trong ky (truoc khi qua danh muc 5 cho)')
print(show[['date', 'kb', 'sym', 'entry', 'exit', 'days', 'ret', 'ly', 'deep']].to_string(index=False))

print('\n### Tom tat theo kich ban (moi tin hieu)')
for kb, g in T.dropna(subset=['entry']).groupby('kb'):
    dong = g[g.ly != 'dang mo']; mo = g[g.ly == 'dang mo']
    print(f"  {kb:4s} {len(g):3d} lenh | da dong {len(dong):3d}: thang {(dong.ret>0.0005).mean()*100 if len(dong) else 0:3.0f}% TB {dong.ret.mean()*100 if len(dong) else 0:+5.2f}% "
          f"| dang mo {len(mo):3d}: TB tam tinh {mo.ret.mean()*100 if len(mo) else 0:+5.2f}%")

# Danh mục 5 chỗ, 40 triệu, bắt đầu trống tại TU (lệnh đang mở: không bao giờ thoát, định giá theo đóng cửa)
ten_kb = {'KB1': 'KB1', 'KB2': 'KB2', 'KB4': 'KB4t', 'N1': 'N1t', 'N3': 'N3t'}
pp = []
for kb in ('KB1', 'KB2', 'KB4', 'N1', 'N3'):
    g = T[(T.kb == kb)].dropna(subset=['entry']).copy()
    if not len(g): continue
    mode = 'atc' if kb in ('KB4', 'N1', 'N3') else 'open'
    g = tag(g.drop(columns=['kb']), ten_kb[kb], mode)
    g.loc[g.ly == 'dang mo', 'k'] = n_ + 10
    pp.append((ten_kb[kb], g))
E, TT, sk = run_kb4(pp, 40_000, 'gan', 1.0)
E = E[E.index >= TU]; E = E / E.iloc[0]
print(f"\n### Danh muc 40 trieu bat dau trong tai {TU}: gia tri cuoi {(E.iloc[-1]-1)*100:+.2f}% | sut toi da {(E/E.cummax()-1).min()*100:.2f}% | so lenh vao {len(TT)} (bo vi het cho/tien {sk})")
print(TT.assign(date=TT.date.dt.strftime('%d/%m'), ret=(TT.ret*100).round(2), w=(TT.w*100).round(1))[['date', 'kb', 'ret', 'w']].to_string(index=False))
ew = MKT[MKT.index >= TU]; print(f"Chi so binh quan deu cung ky: {(ew.iloc[-1]/ew.iloc[0]-1)*100:+.2f}%")
