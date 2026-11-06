"""Bộ đo cắt lỗ cho V3 (PH-32). Lấy bảng lệnh gốc của luật production (P1++ realtime + PH-21) từ parts.pkl
(sinh bằng dumpparts.py), đi lại từng phiên của mỗi lệnh từ phiên mua tới phiên bán gốc và áp một luật cắt lỗ.
Luật cắt lỗ chỉ có thể làm lệnh kết thúc SỚM hơn (hoặc bán một phần sớm hơn); phần còn lại giữ luật bán gốc.

Ràng buộc: realtime trên giá khớp (chạm mức thì bán ở mức, mở cửa đã thấp hơn mức thì bán ở giá mở cửa);
sớm nhất phiên e+2 (T+2.5, cổ phiếu về buổi chiều); phiên sàn trắng không bán được; phí khứ hồi 0,4%.
Nến ngày không cho biết thứ tự giá trong phiên, nên mỗi lệnh có ba cách tính:
- 'tt' (trung tính, mặc định): phiên e+2 chỉ xét giá đóng cửa (như sim_mix); phiên bán gốc mà bán gốc là chạm mức
  trong phiên thì coi cắt lỗ chạm trước nếu giá thấp nhất ≤ mức cắt.
- 'xau' (xấu nhất): với mỗi lệnh lấy kết quả THẤP hơn giữa các cách xử lý chỗ mơ hồ (phiên e+2 dùng cả phiên hay
  chỉ đóng cửa; phiên bán gốc: cắt trước hay chốt trước).
- 'tot' (tốt nhất): lấy kết quả CAO hơn.
Chạy: python catlo.py <thư mục làm việc>  (import từ script khác: exec phần trên dòng mốc CHAY ở cuối file).
"""
import sys, pickle
import numpy as np, pandas as pd

WD = sys.argv[1]
exec(open(WD + '/bt.py', encoding='utf-8').read())
PARTS = pickle.load(open(WD + '/parts.pkl', 'rb'))
PHI = 0.004
COL = {s: j for j, s in enumerate(syms)}
O_, H_, L_, C_ = O.values, H.values, L.values, C.values
LIMV = LIM.values
ATRV = ATR.values
LECH = (MKT / MKT_MA50 - 1).values
MKTV = MKT.values
BRV = BREADTH50.values
FLOOR = (R <= -(LIM - 0.005)).values            # phiên giảm sát sàn
# mức chạm MAn realtime: TB (n−1) đóng cửa trước
LVL = {n: C.shift().rolling(n - 1).mean().values for n in (10, 20, 50)}
LO = {n: L.rolling(n).min().values for n in (5, 10, 20)}  # đáy n phiên (gồm phiên tính)
# chỉ số ngành bình quân đều (trung vị lợi nhuận ngày của mã UNI cùng ngành), cộng dồn
SECT = meta.sector.reindex(syms).fillna('')
_r = R.where(UNI.shift()).clip(-.16, .16)
SECIDX = {}
for s_, cols in SECT.groupby(SECT).groups.items():
    SECIDX[s_] = (1 + _r[list(cols)].median(axis=1).fillna(0)).cumprod().values
SECJ = [SECIDX[SECT.iloc[j]] for j in range(len(syms))]
MAXI = len(dates) - 1


def floor_locked(i, j):
    return i > 0 and H_[i, j] == L_[i, j] and C_[i, j] <= C_[i - 1, j] * (1 - LIMV[j] + 0.005)


def exit_kind(t, j):
    """Kiểu bán gốc ở phiên k: 'open' (ATO / mở cửa đã vượt mức), 'close' (ATC / đóng cửa e+2), 'intra' (chạm mức trong phiên)."""
    k = int(t.k)
    if abs(t.exit - O_[k, j]) < 1e-9: return 'open'
    if abs(t.exit - C_[k, j]) < 1e-9: return 'close'
    return 'intra'


def walk(t, rule, e2_full, stop_first):
    """Đi từng phiên của một lệnh với luật `rule`. Trả (ret mới, phiên bán, phần bán, đã cắt?).
    rule(i, st) trả dict có thể chứa: 'lv' mức cắt realtime (bán khi giá thấp nhất ≤ lv), 'frac' phần bán (mặc định 1),
    'atc' True = bán ATC phiên i (quyết định lúc ~14:25 bằng giá khớp ≈ đóng cửa)."""
    j = COL[t.sym]; e, k = int(t.e), int(t.k); ent = t.entry
    kind = exit_kind(t, j)
    st = dict(e=e, ent=ent, j=j, peak=ent, kb=t.kb, sig=dates.get_loc(t.date))
    for i in range(e + 1, k + 1):
        if i >= e + 2:
            if i == k and kind == 'open':
                break                                         # bán gốc ở mở cửa: cắt lỗ không đến trước được
            r = rule(i, st)
            if r and not floor_locked(i, j) and not np.isnan(L_[i, j]):
                frac = r.get('frac', 1.0)
                px = None
                if r.get('lv') is not None:
                    lv = r['lv']
                    if i == e + 2 and not e2_full:
                        if C_[i, j] <= lv: px = C_[i, j]
                    elif L_[i, j] <= lv:
                        if i == k and kind == 'intra' and not stop_first:
                            px = None                         # giả định chốt gốc chạm trước
                        else:
                            # buổi chiều e+2 không có giá mở cửa: bán ở mức cắt; mức cắt cao hơn cả giá cao nhất phiên
                            # (giá đã ở dưới mức từ trước) thì bán theo giá đóng cửa
                            if i == e + 2:
                                px = lv if lv <= H_[i, j] else C_[i, j]
                            else:
                                px = min(O_[i, j], lv)
                if px is None and r.get('atc'):
                    px = C_[i, j]
                if px is not None:
                    rs = px / ent - 1 - PHI
                    if frac >= 0.999:
                        return rs, i, 1.0, True
                    return frac * rs + (1 - frac) * t.ret, k, frac, True
        # cập nhật trạng thái sau khi phiên i đóng cửa (dùng cho phiên sau)
        if not np.isnan(H_[i, j]): st['peak'] = max(st['peak'], H_[i, j])
        st['cmax'] = max(st.get('cmax', ent), C_[i, j] if not np.isnan(C_[i, j]) else ent)
    return t.ret, k, 0.0, False


def apply(rule, scope=('KB1', 'KB2', 'KB4t', 'N1t', 'N3t'), deep=False, mode='tt'):
    """Áp luật lên mọi lệnh trong scope (deep=False: bỏ qua lệnh hoảng loạn sâu PH-21). Trả PARTS mới."""
    out = []
    for kb, df in PARTS:
        if kb not in scope:
            out.append((kb, df.assign(cut=False, frac=0.0))); continue
        d = df.copy(); cut = np.zeros(len(d), bool); fr = np.zeros(len(d))
        rets = d.ret.values.copy(); ks = d.k.values.copy()
        for n, (_, t) in enumerate(df.iterrows()):
            if not deep and 'deep' in t and bool(t.deep):
                continue
            if mode == 'tt':
                res = [walk(t, rule, False, True)]
            else:
                res = [walk(t, rule, a, b) for a in (False, True) for b in (False, True)]
            r = min(res, key=lambda z: z[0]) if mode in ('tt', 'xau') else max(res, key=lambda z: z[0])
            rets[n], ks[n], fr[n], cut[n] = r
        d['ret'] = rets; d['k'] = ks; d['cut'] = cut; d['frac'] = fr
        d['days'] = d.k - d.e + 1
        out.append((kb, d))
    return out


def run_pf(parts, v0=40_000, slots=5, lot=100):
    """Danh mục luật production: 5 chỗ, KB1/KB2/N1 ×1,5, tiền 40 triệu, lô 100 làm tròn gần nhất, tiền mặt 0%
    (chép run_kb4 của kb4size.py, hệ số KB4 ×1)."""
    def he_so(x):
        return 1.5 if x.kb in ('KB1', 'KB2', 'N1t') else 1.0
    cand = pd.concat([p.assign(prio=n) for n, (_, p) in enumerate(parts)], ignore_index=True)
    cand = cand[cand.date >= '2023-01-01'].sort_values(['e', 'prio']).reset_index(drop=True)
    by_e = {e: g for e, g in cand.groupby('e')}
    i0 = dates.get_loc(dates[dates >= '2023-01-01'][0])
    cash = v0; pos = []; eq = []; taken = []; prev = v0
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
                want = min(cash, prev / slots * he_so(x))
                px = x.entry
                sh = max(np.floor(want / px / lot + 0.5), 1) * lot
                while sh > 0 and sh * px > cash: sh -= lot
                if sh == 0: continue
                cost = sh * px; cash -= cost
                pos.append(dict(sym=x.sym, k=int(x.k), cost=cost, ent=px, ret=x.ret, kb=x.kb, sh=sh))
                taken.append(dict(date=x.date, kb=x.kb, ret=x.ret, cut=bool(x.get('cut', False)), mat=cost * x.ret / prev))
        v = cash + sum(p['sh'] * (C_[d, COL[p['sym']]] if not np.isnan(C_[d, COL[p['sym']]]) else p['ent']) for p in pos)
        eq.append(v); prev = v
    return pd.Series(eq, index=dates[i0:]) / v0, pd.DataFrame(taken)


def seg(E, a, b):
    s = E[(E.index >= a) & (E.index <= b)]; p = E[E.index < a]
    return s.iloc[-1] / (p.iloc[-1] if len(p) else 1.0) - 1


def dd(E):
    return (E / E.cummax() - 1).min()


def summarize(label, parts):
    """Một dòng số: từng lệnh (gộp mọi KB) + danh mục + theo năm + trong/ngoài mẫu."""
    allt = pd.concat([p for _, p in parts])
    allt = allt[allt.date >= '2023-01-01']
    E, T = run_pf(parts)
    Eis = E[E.index <= IS[1]]; Eos = E[E.index > IS[1]] / Eis.iloc[-1]
    m = E.resample('ME').last(); mm = (m / m.shift(1).fillna(1) - 1).min()
    row = dict(label=label, n=len(allt), cut=int(allt.get('cut', pd.Series(False)).sum()),
               win=(allt.ret > 0.0005).mean(), avg=allt.ret.mean(), worst=allt.ret.min(), l10=(allt.ret <= -0.10).mean(),
               tot=E.iloc[-1] - 1, mdd=dd(E), mmon=mm, worstDM=T.mat.min(),
               y23=seg(E, '2023-01-01', '2023-12-31'), y24=seg(E, '2024-01-01', '2024-12-31'),
               y25=seg(E, '2025-01-01', '2025-12-31'), y26=seg(E, '2026-01-01', '2026-12-31'),
               IS=Eis.iloc[-1] - 1, ISdd=dd(Eis), OOS=Eos.iloc[-1] - 1, OOSdd=dd(Eos),
               avgIS=allt[allt.date <= IS[1]].ret.mean(), avgOOS=allt[allt.date > IS[1]].ret.mean(),
               worstOOS=allt[allt.date > IS[1]].ret.min(), winOOS=(allt[allt.date > IS[1]].ret > 0.0005).mean())
    for kb, p in parts:
        p = p[p.date >= '2023-01-01']
        row[kb] = (p.ret.mean(), p.ret.min(), int(p.get('cut', pd.Series(False)).sum()))
    return row


def fmt(r):
    p = lambda x: f"{x*100:+.0f}%"
    q = lambda x: f"{x*100:+.2f}%"
    kbs = ' '.join(f"{kb}:{q(v[0])}/{v[1]*100:.0f}%/{v[2]}" for kb, v in ((k, r[k]) for k in ('KB1', 'KB2', 'KB4t', 'N1t', 'N3t')))
    return (f"{r['label'][:44]:44s} cat {r['cut']:3d} | thang {r['win']*100:3.0f}% TB {q(r['avg'])} te {r['worst']*100:5.1f}% l10 {r['l10']*100:3.1f}% "
            f"| DM {p(r['tot'])} sut {r['mdd']*100:5.1f}% thang-te {r['mmon']*100:5.1f}% lenh-te {r['worstDM']*100:4.1f}%DM "
            f"| 23 {p(r['y23'])} 24 {p(r['y24'])} 25 {p(r['y25'])} 26 {p(r['y26'])} | IS {p(r['IS'])}/{r['ISdd']*100:.1f} OOS {p(r['OOS'])}/{r['OOSdd']*100:.1f} "
            f"| {kbs}")


# ==== CHAY
if __name__ == '__main__':
    base = summarize('BASE (khong cat lo)', [(kb, d.assign(cut=False)) for kb, d in PARTS])
    print(fmt(base))
    nr = apply(lambda i, st: None)
    print(fmt(summarize('kiem: luat rong (phai = BASE)', nr)))
