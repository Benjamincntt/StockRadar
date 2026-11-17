"""Q4-8 kiểm thực tế: lõi ít biến động + vệ tinh cổ tăng sốc, khớp ATO phiên sau và lô 100 (vốn 40 triệu).
Quyết ở đóng cửa d → lệnh khớp ở giá mở cửa d+1: bán trước, mua sau. Mua vệ tinh bị bỏ nếu mở cửa d+1 sát trần
(≥ đóng cửa d × (1 + biên độ − 0,5%)) — ATO kịch trần coi như không khớp. Định giá theo chuỗi ADJ (không giá thô).
Lô 100: số cổ = làm tròn tới 100 gần nhất của (giá trị chỗ ÷ giá), ít nhất 1 lô, không vượt tiền mặt. Phí + trượt 0,3%/chiều."""
import sys, warnings, numpy as np, pandas as pd
warnings.filterwarnings('ignore')
exec(open(sys.argv[1] + '/v4soc.py', encoding='utf-8').read().split("PHI = 0.003")[0])
_t = open(sys.argv[1] + '/v4soc.py', encoding='utf-8').read(); exec(_t[_t.index('def bao'):_t.index('E, a, b = chay()')])
PHI = 0.003; cv = C.ffill().values; ov = O.values; limv = LIM.values; DUOI50v = DUOI50


def tin(r=0.25, w=20, kl=2.0): return ((C / C.shift(w) - 1 >= r) & (C > HI60) & (V > kl * VMA50) & TOP100).values


def chay(tinh=None, S=0, truot=0.20, thoat50=True, ato=True, von=None, chan_tran=True, ky=21, so=5, lot=100):
    i0 = dates.get_loc(dates[dates >= '2017-01-01'][0])
    v0 = float(von) if von else 1.0; cash = v0; pos = {}; eq = []; lan = -10 ** 9
    dem = dict(sat=0, tran=0, thieu=0)
    def V_(d): return cash + sum(p['u'] * ADJ[d, j] for j, p in pos.items())
    def gia(j, d, dk):            # giá khớp (đơn vị thô) và hệ số đổi sang đơn vị ADJ
        g = ov[dk, j] if dk != d else cv[d, j]
        if not np.isfinite(g) or g <= 0 or not np.isfinite(cv[d, j]): return None
        return g
    def ban(j, d, dk):
        nonlocal cash
        g = gia(j, d, dk)
        if g is None: return False
        f = g / cv[d, j]; cash += pos.pop(j)['u'] * ADJ[d, j] * f * (1 - PHI); return True
    def mua(j, d, dk, gt, kind):
        nonlocal cash
        g = gia(j, d, dk)
        if g is None: return False
        f = g / cv[d, j]
        if von:
            sh = max(int(np.floor(gt / g / lot + 0.5)) * lot, lot)
            while sh > 0 and sh * g * (1 + PHI) > cash: sh -= lot
            if sh <= 0: dem['thieu'] += 1; return False
            tien = sh * g
        else:
            tien = min(gt, cash / (1 + PHI))
            if tien <= 1e-9: return False
        cash -= tien * (1 + PHI)
        u = tien / (ADJ[d, j] * f)
        pos[j] = dict(u=u, vao=ADJ[d, j] * f, dinh=ADJ[d, j] * f, kind=kind); return True
    for d in range(i0, N - 1):
        dk = d + 1 if ato else d
        # 1) thoát vệ tinh / 'chay' theo đóng cửa d
        for j in [j for j, p in pos.items() if p['kind'] != 'loi']:
            p = pos[j]; p['dinh'] = max(p['dinh'], ADJ[d, j])
            if ADJ[d, j] <= p['dinh'] * (1 - truot) or (thoat50 and DUOI50v[d, j]): ban(j, d, dk)
        # 2) vệ tinh mới
        if tinh is not None and S > 0:
            cand = np.where(tinh[d])[0]
            if len(cand):
                for j in cand[np.argsort(-RS126.values[d, cand])]:
                    if sum(p['kind'] == 'sat' for p in pos.values()) >= S: break
                    if j in pos: continue
                    if ato and chan_tran and np.isfinite(ov[dk, j]) and ov[dk, j] >= cv[d, j] * (1 + limv[j] - 0.005):
                        dem['tran'] += 1; continue
                    slot = V_(d) / so
                    if cash < slot * 0.999:
                        loi = [k for k, p in pos.items() if p['kind'] == 'loi']
                        if loi: ban(max(loi, key=lambda k: VBv[d, k] if np.isfinite(VBv[d, k]) else 9), d, dk)
                    if mua(j, d, dk, slot, 'sat'): dem['sat'] += 1
        # 3) cân lại lõi
        if d - lan >= ky:
            lan = d
            idx = np.where(DU[d])[0]; vv = VBv[d, idx]; ok = ~np.isnan(vv); ds = list(idx[ok][np.argsort(vv[ok])])
            khac = [j for j, p in pos.items() if p['kind'] != 'loi']
            dich = [j for j in ds if j not in khac][:max(so - len(khac), 0)]
            for j in [j for j, p in pos.items() if p['kind'] == 'loi']:
                if j not in dich: ban(j, d, dk)
            slot = V_(d) / so
            for j in dich:
                if j not in pos: mua(j, d, dk, slot, 'loi')
        eq.append(V_(d + 1) if ato else V_(d))
    idx_ = dates[i0 + 1:N] if ato else dates[i0:N - 1]
    return pd.Series(eq, index=idx_) / v0, dem


for ten, kw in (
    ('LOI rieng | ATC, ty trong le', dict(ato=False)),
    ('LOI rieng | ATO, lo 100, 40 trieu', dict(von=40_000)),
    ('LOI+S2 tang soc 25%/20p | ATC, ty trong le', dict(tinh=tin(), S=2, ato=False)),
    ('LOI+S2 tang soc 25%/20p | ATO, ty trong le, khong chan tran', dict(tinh=tin(), S=2, chan_tran=False)),
    ('LOI+S2 tang soc 25%/20p | ATO, ty trong le, chan tran', dict(tinh=tin(), S=2)),
    ('LOI+S2 tang soc 25%/20p | ATO, lo 100, 40 trieu', dict(tinh=tin(), S=2, von=40_000)),
    ('LOI+S2 tang soc 20%/20p | ATO, lo 100, 40 trieu', dict(tinh=tin(0.20), S=2, von=40_000)),
    ('LOI+S2 tang soc 30%/20p | ATO, lo 100, 40 trieu', dict(tinh=tin(0.30), S=2, von=40_000)),
    ('LOI+S2 tang soc 15%/40p | ATO, lo 100, 40 trieu', dict(tinh=tin(0.15, 40), S=2, von=40_000)),
    ('LOI+S1 tang soc 25%/20p | ATO, lo 100, 40 trieu', dict(tinh=tin(), S=1, von=40_000)),
    ('LOI+S3 tang soc 25%/20p | ATO, lo 100, 40 trieu', dict(tinh=tin(), S=3, von=40_000)),
):
    E, dem = chay(**kw); bao(E, ten, f"ve tinh {dem['sat']} lenh, bo vi tran {dem['tran']}, thieu tien {dem['thieu']}")
