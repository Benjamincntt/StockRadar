"""Mô phỏng cách đánh "mua thử gần hỗ trợ, chốt ở kháng cự, cắt khi thủng hỗ trợ" của một đội tư vấn (owner gửi 03/11).
Quy đổi thành luật máy (giả định của AI, vì hỗ trợ/kháng cự của đội là vẽ tay):
- Hỗ trợ = thấp nhất N phiên (gồm hôm nay); kháng cự = cao nhất N phiên trước hôm nay.
- Vào lệnh ATC khi: khoảng cách tới hỗ trợ 2–7% giá, kháng cự cao hơn giá ≥ 5%, R:R = (KC − giá)/(giá − HT) ≥ RR_MIN,
  thanh khoản 20 phiên ≥ TV_MIN tỷ, không phải phiên trần. Mỗi ngày lấy tối đa TOP mã có R:R cao nhất.
- Thoát (T+2.5: phiên e+2 chỉ xét theo giá đóng cửa): chạm kháng cự → bán ở kháng cự (mở cửa cao hơn thì giá mở cửa);
  thủng hỗ trợ → bán (kiểu 'trong phien': ở hỗ trợ, mở cửa thấp hơn thì giá mở cửa; kiểu 'dong cua': giá đóng cửa < HT);
  cùng phiên chạm cả hai thì tính cắt lỗ trước (bi quan). Hết GIU phiên → bán giá đóng cửa. Phí khứ hồi 0,4%.
- Danh mục: tiền 40 triệu, 5 chỗ × 20%, lô 100, làm tròn lô gần nhất (dùng run_kb4 của kb4size.py)."""
import sys
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])

TU = '2023-11-01'
o_, h_, l_, c_ = O.values, H.values, L.values, C.values
tv = TV.values; bad = BAD.values; lim = LIM.values


def mo_phong(N=20, RR_MIN=2.0, TV_MIN=20.0, TOP=3, GIU=20, kieu='trong phien', DEM=0.0, THI_TRUONG=False):
    sup = L.rolling(N).min().values
    mkt_len = (MKT > MKT_MA50).values
    res = H.shift(1).rolling(N).max().values
    n_ = len(dates); i0 = dates.get_indexer([dates[dates >= TU][0]])[0]
    ung = []
    for i in range(max(i0, N + 1), n_ - 3):
        ci = c_[i]; s = sup[i]; r = res[i]
        with np.errstate(invalid='ignore', divide='ignore'):
            kc = (ci - s) / ci
            rr = (r - ci) / (ci - s)
            ok = (kc >= 0.02) & (kc <= 0.07) & (r >= ci * 1.05) & (rr >= RR_MIN) & (tv[i] >= TV_MIN) \
                 & (ci < c_[i - 1] * (1 + lim - 0.005)) & UNI.values[i]
        js = np.nonzero(np.nan_to_num(ok, nan=0).astype(bool))[0]
        if len(js) == 0 or (THI_TRUONG and not mkt_len[i]): continue
        js = js[np.argsort(-rr[js])][:TOP]
        for j in js: ung.append((i, j, rr[j]))
    out = []; busy = {}
    for i, j, rr in ung:
        if busy.get(j, -1) >= i: continue
        ent = c_[i, j]; s = sup[i, j] * (1 - DEM); r = res[i, j]; t2 = i + 2; last = i + GIU
        if last >= n_: continue
        px = None; ly = None; k = i + 1
        while k < n_:
            if bad[k, j]: break
            if k >= t2 and not np.isnan(c_[k, j]) and not floor_locked(k, j, o_, h_, l_, c_, lim):
                if k == t2:
                    if c_[k, j] < s: px, ly = c_[k, j], 'cat'; break
                    if c_[k, j] >= r: px, ly = c_[k, j], 'chot'; break
                else:
                    if kieu == 'trong phien' and l_[k, j] < s:
                        px, ly = min(o_[k, j], s), 'cat'; break
                    if kieu == 'dong cua' and c_[k, j] < s:
                        px, ly = c_[k, j], 'cat'; break
                    if h_[k, j] >= r:
                        px, ly = max(o_[k, j], r), 'chot'; break
                if k >= last: px, ly = c_[k, j], 'het han'; break
            k += 1
        if px is None: continue
        out.append((dates[i], syms[j], ent, px, k - i + 1, px / ent - 1 - COST, False, ly, rr))
        busy[j] = k
    return pd.DataFrame(out, columns=['date', 'sym', 'entry', 'exit', 'days', 'ret', 'deep', 'ly', 'rr'])


def tom(lab, t):
    w = t.ret[t.ret > 0.0005]; ls = t.ret[t.ret <= 0.0005]
    nam = ' '.join(f"{y}:{t[t.date.dt.year==y].ret.mean()*100:+.2f}%" for y in (2024, 2025, 2026))
    print(f"  {lab:38s} n{len(t):5d} | thang {(t.ret>0.0005).mean()*100:3.0f}% | TB {t.ret.mean()*100:+5.2f}% | lai TB {w.mean()*100:+5.1f}% "
          f"lo TB {ls.mean()*100:+5.1f}% | tong lai/lo {w.sum()/-ls.sum():4.2f} | chot {(t.ly=='chot').mean()*100:3.0f}% cat {(t.ly=='cat').mean()*100:3.0f}% "
          f"het han {(t.ly=='het han').mean()*100:3.0f}% | R:R TB luc vao {t.rr.clip(upper=20).mean():.1f} | giu {t.days.mean():4.1f} | TB/lenh theo nam {nam}")


def dm(lab, t):
    E, T, sk = run_kb4([('DOI', tag(t, 'DOI', 'atc'))], 40_000, 'gan', 1.0)
    E = E[E.index >= TU]; E = E / E.iloc[0]
    yr = ' '.join(f"{y}:{(E[E.index.year==y].iloc[-1]/(E[E.index.year<y].iloc[-1] if (E.index.year<y).any() else 1)-1)*100:+.0f}%" for y in (2023, 2024, 2025, 2026))
    print(f"  DANH MUC {lab:29s} tong {(E.iloc[-1]-1)*100:+4.0f}% | sut {(E/E.cummax()-1).min()*100:5.1f}% | {yr}")


# V3 cùng kỳ để so
E0, _, _ = run_kb4(PARTS, 40_000, 'gan', 1.0); E0 = E0[E0.index >= TU]; E0 = E0 / E0.iloc[0]
print(f"\nV3 (luat production) cung ky tu {TU}: tong {(E0.iloc[-1]-1)*100:+.0f}% | sut {(E0/E0.cummax()-1).min()*100:.1f}%")
ew = MKT[MKT.index >= TU]; ew = ew / ew.iloc[0]
print(f"Chi so binh quan deu cung ky: tong {(ew.iloc[-1]-1)*100:+.0f}% | sut {(ew/ew.cummax()-1).min()*100:.1f}%")

print('\n### Luat chuan (N=20, R:R>=2, thanh khoan>=20 ty, top 3/ngay, giu toi da 20 phien)')
for kieu in ('trong phien', 'dong cua'):
    t = mo_phong(kieu=kieu); tom(f'cat {kieu}', t); dm(f'cat {kieu}', t)
print('\n### Do ben (cat trong phien)')
for kw in (dict(DEM=0.02), dict(THI_TRUONG=True), dict(DEM=0.02, THI_TRUONG=True), dict(N=10), dict(N=40), dict(RR_MIN=1.0), dict(RR_MIN=3.0), dict(TV_MIN=100.0), dict(GIU=10), dict(GIU=40)):
    t = mo_phong(**kw); lab = ', '.join(f'{k}={v}' for k, v in kw.items()); tom(lab, t); dm(lab, t)
