"""V4 — bộ đo nền. Nến ngày production từ 2015-09 (v4load.py). Chỉ dùng thông tin tới đóng cửa phiên tín hiệu.
Ràng buộc thật của thị trường (thận trọng):
- Mua: 'open' = giá mở cửa phiên sau (ATO); 'close' = giá đóng cửa phiên tín hiệu (ATC). Bỏ nếu giá mua sát trần.
- T+2.5: phiên e+2 chỉ bán theo giá đóng cửa (cổ phiếu về chiều); từ e+3 bán cả phiên.
- Sàn trắng (cao = thấp = giá sàn) không bán được, dời sang phiên sau.
- Cùng một phiên chạm cả cắt lỗ lẫn chốt lời: coi cắt lỗ chạm trước. Mở cửa vượt mức: khớp ở giá mở cửa.
- Phí + thuế khứ hồi 0,4% + trượt giá 0,1% mỗi chiều (tổng 0,6%).
- Phiên có biến động vượt biên độ (giá chưa điều chỉnh quyền) trong lúc giữ: bỏ lệnh.
"""
import os, sys
import numpy as np, pandas as pd

D = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))
PHI = 0.004
TRUOT = 0.001
TRONG_MAU = ('2017-01-01', '2022-12-31')
NGOAI_MAU = ('2023-01-01', '2026-12-31')

df = pd.read_pickle(D + '/bars.pkl'); meta = pd.read_pickle(D + '/meta.pkl').set_index('sym')
P = {k: df.pivot(index='date', columns='sym', values=k) for k in 'ohlcv'}
O, H, L, C, V = P['o'], P['h'], P['l'], P['c'], P['v']
# Làm sạch đơn vị: mã giá < 1.000đ có nến lưu lẫn đồng (970) với nghìn đồng (0,97).
# Mã có trung vị giá đóng cửa < 100 (nghìn đồng) mà một giá ≥ 500 thì giá đó đang ở đơn vị đồng → chia 1000.
_trung_vi = C.where(C < 500).median()
SO_GIA_SUA = 0
for _x in (O, H, L, C):
    _sai = (_x >= 500) & (_trung_vi < 100)
    SO_GIA_SUA += int(_sai.sum().sum())
    _x[_sai] = _x[_sai] / 1000
syms = C.columns; dates = C.index; N = len(dates)
LIM = pd.Series({s: {'HOSE': .07, 'HNX': .10, 'UPCOM': .15}.get(meta.ex.get(s, ''), .07) for s in syms})
R = C.pct_change(fill_method=None)
BAD = R.abs() > (LIM + 0.015)
TV20 = (C * V).rolling(20).mean() / 1e6            # tỉ đồng / phiên (giá nghìn đồng)
TUOI = C.notna().cumsum()


def bc0(s):
    return pd.DataFrame(np.repeat(s.values[None, :], len(C.index), 0), index=C.index, columns=C.columns)
# Mã có giá cao nhất gấp > 200 lần thấp nhất: dữ liệu lẫn đơn vị không sửa được (vd PXM) → loại.
LOAI = (C.max() / C.min() > 200)
UNI = (TV20.where((C >= 3) & (TUOI >= 250) & (TV20 >= 1) & ~bc0(LOAI)).rank(axis=1, ascending=False) <= 300)


def ma(x, n): return x.rolling(n).mean()


def rsi(x, n):
    d = x.diff(); up = d.clip(lower=0).ewm(alpha=1 / n, adjust=False).mean(); dn = (-d.clip(upper=0)).ewm(alpha=1 / n, adjust=False).mean()
    return 100 - 100 / (1 + up / dn)


MA = {n: ma(C, n) for n in (5, 10, 20, 50, 100, 200)}
VMA20 = ma(V, 20)
TR = pd.concat([H - L, (H - C.shift()).abs(), (L - C.shift()).abs()]).groupby(level=0).max()
ATR = TR.ewm(alpha=1 / 14, adjust=False).mean()
RSI2 = rsi(C, 2)
BIEN = R.where(~BAD).rolling(120, min_periods=60).std()          # độ biến động ngày, 120 phiên


def hang(x):
    return x.where(UNI).rank(axis=1, pct=True)


RS63 = hang(C / C.shift(63)); RS126 = hang(C / C.shift(126))
# Nhóm biến động: 0 = thấp, 1 = vừa, 2 = cao (chia ba theo thứ hạng trong vũ trụ hôm đó)
NHOM = np.floor(hang(BIEN).clip(upper=0.9999) * 3)

# Chỉ số đều trọng số trên vũ trụ thanh khoản (thay VNINDEX: DB chỉ có VNINDEX từ 06/2026)
mret = R.where(UNI.shift(1) & ~BAD).clip(-.15, .15).mean(axis=1).fillna(0)
MKT = (1 + mret).cumprod()
M50 = MKT.rolling(50).mean(); M200 = MKT.rolling(200).mean()
RONG = ((C > MA[50]) & UNI).sum(axis=1) / UNI.sum(axis=1)
SUT20 = MKT / MKT.rolling(20).max() - 1
# Giai đoạn thị trường tại đóng cửa phiên t
PHA_TEN = {0: 'HoangLoan', 1: 'TangManh', 2: 'HoiPhuc', 3: 'DieuChinh', 4: 'GiamGia'}
PHA = pd.Series(np.select(
    [(SUT20 <= -0.08) | (RONG < 0.15),
     (MKT > M50) & (M50 > M200),
     (MKT > M50) & (M50 <= M200),
     (MKT <= M50) & (MKT > M200)],
    [0, 1, 2, 3], default=4), index=dates).where(M200.notna())


def bc(s):
    return pd.DataFrame(np.repeat(s.values[:, None], len(syms), 1), index=dates, columns=syms)


def mo_phong(sig, entry='open', hold=10, sl=None, tp=None, trail=None, exit_flag=None, chi_ngay=None, sl_pct=None, tp_pct=None, trail_pct=None):
    """sig: DataFrame bool (tín hiệu cuối phiên). sl/tp/trail: bội số ATR tại phiên tín hiệu.
    exit_flag: DataFrame bool — đúng ở đóng cửa phiên k thì bán ATO phiên k+1. Hết 'hold' phiên: bán ATC.
    Trả bảng lệnh không chồng lệnh trên cùng một mã."""
    sg = (sig & UNI).fillna(False).values
    if chi_ngay is not None: sg &= chi_ngay.values[:, None]
    o, h, l, c = O.values, H.values, L.values, C.values; bad = BAD.values; lim = LIM.values; atr = ATR.values
    ex = exit_flag.fillna(False).values if exit_flag is not None else None
    pha = PHA.values; nhom = NHOM.values
    out = []
    for j in range(len(syms)):
        busy = -1
        for i in np.nonzero(sg[:, j])[0]:
            if i <= busy or i < 1: continue
            if entry == 'open':
                e = i + 1
                if e >= N or np.isnan(o[e, j]) or o[e, j] >= c[i, j] * (1 + lim[j] - 0.005) or bad[e, j]: continue
                ent = o[e, j]
            else:
                e = i
                if np.isnan(c[i, j]) or c[i, j] >= c[i - 1, j] * (1 + lim[j] - 0.005): continue
                ent = c[i, j]
            a = atr[i, j]
            if np.isnan(a) or a <= 0: continue
            stop = ent - sl * a if sl else None
            if sl_pct: stop = max(stop or 0, ent * (1 - sl_pct))
            tgt = ent + tp * a if tp else None
            if tp_pct: tgt = ent * (1 + tp_pct)
            last = e + hold; t2 = e + 2
            if last >= N: continue
            hc = ent; px = None; pend = False; k = e
            while k < N:
                if k > e and bad[k, j]: px = np.nan; break
                if np.isnan(c[k, j]): k += 1; continue
                san = h[k, j] == l[k, j] and c[k, j] <= c[k - 1, j] * (1 - lim[j] + 0.005)
                if k >= t2 and not san:
                    if k == t2:
                        if stop is not None and c[k, j] <= stop: px = c[k, j]; break
                        if tgt is not None and c[k, j] >= tgt: px = c[k, j]; break
                        if pend: px = c[k, j]; break
                        if k >= last: px = c[k, j]; break
                    else:
                        if pend: px = o[k, j]; break
                        if stop is not None and o[k, j] <= stop: px = o[k, j]; break
                        if tgt is not None and o[k, j] >= tgt: px = o[k, j]; break
                        if stop is not None and l[k, j] <= stop: px = stop; break
                        if tgt is not None and h[k, j] >= tgt: px = tgt; break
                        if k >= last: px = c[k, j]; break
                if ex is not None and ex[k, j] and k >= e + 1: pend = True
                hc = max(hc, c[k, j])
                if trail and k >= e:
                    ns = hc - trail * atr[k, j]
                    stop = ns if stop is None else max(stop, ns)
                if trail_pct and k >= e:
                    ns = hc * (1 - trail_pct)
                    stop = ns if stop is None else max(stop, ns)
                k += 1
            if px is None: break
            if np.isnan(px): busy = k; continue
            ret = px * (1 - TRUOT) / (ent * (1 + TRUOT)) - 1 - PHI
            out.append((dates[i], syms[j], i, e, k, ret, k - e + 1, pha[i], nhom[i, j]))
            busy = k
    t = pd.DataFrame(out, columns=['date', 'sym', 'i', 'e', 'k', 'ret', 'days', 'pha', 'nhom'])
    return t


def tom_tat(t):
    if len(t) == 0: return dict(n=0, thang=np.nan, tb=np.nan, pf=np.nan, ngay=np.nan, tbngay=np.nan)
    w = t.ret > 0.0005
    pf = t.ret[t.ret > 0].sum() / max(-t.ret[t.ret < 0].sum(), 1e-9)
    return dict(n=len(t), thang=w.mean(), tb=t.ret.mean(), pf=pf, ngay=t.days.mean(), tbngay=t.ret.mean() / t.days.mean())


def chia(t):
    a = t[(t.date >= TRONG_MAU[0]) & (t.date <= TRONG_MAU[1])]
    b = t[(t.date >= NGOAI_MAU[0]) & (t.date <= NGOAI_MAU[1])]
    return a, b
