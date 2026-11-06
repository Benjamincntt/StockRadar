"""Chờ dừng rơi mới vào: giữ nguyên tín hiệu KB1/KB2/KB4/N1 (luật production), nhưng chỉ mua ATC ở phiên q đầu tiên
trong W phiên sau tín hiệu có dấu hiệu ngừng rơi; không có thì bỏ lệnh. Luật bán, giữ tối đa, T+2.5, sàn trắng,
lệnh hoảng loạn sâu (cờ theo NGÀY TÍN HIỆU) giữ y như sim_mix; thời gian giữ tính từ phiên mua q. N3 không đổi.
Dấu hiệu xét theo giá đóng cửa phiên q (mua ATC phiên q — gần đúng: thực tế quyết định lúc ~14:25)."""
import sys
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])

MA5 = C.rolling(5).mean().values
DAU_HIEU = {
    'dong cua tang': lambda c, q, i, j: c[q] > c[q - 1],
    'dong cua > MA5': lambda c, q, i, j: c[q] > MA5[q, j],
    'dong cua > gia phien tin hieu': lambda c, q, i, j: c[q] > c[i],
    'tang 2 phien lien tiep': lambda c, q, i, j: c[q] > c[q - 1] > c[q - 2],
}


def sim_cho(sig, normal, deep, deep_mask, dau_hieu, W):
    """Như sim_mix nhưng điểm mua = ATC phiên q đầu tiên (i < q ≤ i+W) thoả dau_hieu; trả bảng lệnh (date = phiên mua)."""
    sg = (sig & UNI).fillna(False).values; dm = deep_mask.reindex(dates).fillna(False).values
    o, h, l, c = O.values, H.values, L.values, C.values; bad = BAD.values; lim = LIM.values
    lvls = {n: LV[n].values for n in (5, 10, 20)}
    out = []; n_ = len(dates); bo = 0
    for j in range(len(syms)):
        busy = -1
        cj = c[:, j]
        for i in np.nonzero(sg[:, j])[0]:
            if i <= busy or i < 2: continue
            p = deep if dm[i] else normal
            q = None
            for qq in range(i + 1, min(i + W, n_ - 1) + 1):
                if np.isnan(cj[qq]) or np.isnan(cj[qq - 1]) or bad[qq, j]: continue
                if dau_hieu(cj, qq, i, j):
                    q = qq; break
            if q is None: bo += 1; continue
            ent = cj[q]
            if ent >= cj[q - 1] * (1 + lim[j] - 0.005): bo += 1; continue    # trần: không mua được
            e = q; t2 = e + 2; last = q + p['hold']
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
            out.append((dates[q], syms[j], ent, px, k - e + 1, px / ent - 1 - COST, bool(dm[i])))
            busy = k
    t = pd.DataFrame(out, columns=['date', 'sym', 'entry', 'exit', 'days', 'ret', 'deep'])
    return t[t.date >= '2023-01-01'], bo


def tom(t):
    if len(t) == 0: return 'n0'
    l = t.ret[t.ret < 0]
    return (f"n{len(t):4d} thang {(t.ret>0.0005).mean()*100:3.0f}% TB {t.ret.mean()*100:+5.2f}% lo TB {l.mean()*100 if len(l) else 0:+5.1f}% "
            f"te nhat {t.ret.min()*100:+5.1f}% lo>=10% {(t.ret<=-0.10).mean()*100:3.0f}%")


def dm_dong(lab, E, T, sk):
    a = E[E.index <= IS[1]]
    m = E.resample('ME').last(); m = (m / m.shift(1).fillna(1) - 1).min() * 100
    yr = ' '.join(f"{y}:{(E[E.index.year==y].iloc[-1]/(E[E.index.year<y].iloc[-1] if (E.index.year<y).any() else 1)-1)*100:+.0f}%" for y in (2023, 2024, 2025, 2026))
    print(f"  DANH MUC {lab:34s} tong {(E.iloc[-1]-1)*100:+4.0f}% | sut {(E/E.cummax()-1).min()*100:5.1f}% | thang te {m:5.1f}% "
          f"| trong {(a.iloc[-1]-1)*100:+4.0f}% ngoai {(E.iloc[-1]/a.iloc[-1]-1)*100:+4.0f}% | lenh te nhat mat {T.mat.min()*100:4.1f}% DM | {yr}")


TEN = ('KB1', 'KB2', 'KB4', 'N1')
print('\n### Goc (mua ngay theo luat hien tai)')
for nm, (kb, part) in zip(TEN, PARTS[:4]):
    print(f'  {nm:4s} ' + tom(part))
dm_dong('goc', *run_kb4(PARTS, 40_000, 'gan', 1.0)[:3])

W = int(sys.argv[2]) if len(sys.argv) > 2 else 5
for ten_dh, dh in DAU_HIEU.items():
    print(f'\n### Cho "{ten_dh}" (toi da {W} phien sau tin hieu)')
    parts = []
    for nm in TEN:
        kb, _ = KBN[nm]
        t, bo = sim_cho(SC[nm][0], NORMAL[nm], rule, deep, dh, W)
        print(f'  {nm:4s} ' + tom(t) + f' | bo {bo}')
        parts.append((kb, tag(t, kb, 'atc')))
    parts.append(PARTS[4])
    dm_dong(ten_dh, *run_kb4(parts, 40_000, 'gan', 1.0)[:3])
