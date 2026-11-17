"""Q4-9 — hệ V4 hoàn chỉnh: lõi ít biến động + 2 chỗ vệ tinh cổ tăng sốc + lớp ra tiền mặt theo giai đoạn.

Nạp phần đầu của v4soc3.py (không sửa v4soc3.py) rồi định nghĩa chay_he() = chay() + tham số ra_tien:
  - ra_tien=None : như chay() hiện tại.
  - 'loi'        : đóng cửa phiên d có PHA ∈ pha_ra → bán mọi mã LÕI (khớp ATO d+1), không mua lõi mới.
                   Vệ tinh vẫn theo luật thoát riêng và vẫn được mua mới.
  - 'ca'         : như 'loi', thêm bán mọi vệ tinh và không mua vệ tinh mới.
Rời pha xấu → cân lại lõi NGAY phiên đó (khớp ATO phiên sau), không chờ đủ 21 phiên; nếu chờ thì danh mục
kẹt tiền mặt tới 20 phiên sau khi thị trường đã hồi — lỗi của bộ đo, không phải của luật.
Quyết theo PHA tại đóng cửa d, khớp mở cửa d+1 (không dùng PHA của d+1: nhìn trước). Định giá theo chuỗi ADJ.
Mọi dòng: ATO, lô 100, vốn 40 triệu, phí + trượt 0,3%/chiều, 5 chỗ, cân lại 21 phiên.
"""
import sys, warnings, numpy as np, pandas as pd
warnings.filterwarnings('ignore')
D = sys.argv[1]
_t = open(D + '/v4soc3.py', encoding='utf-8').read()
exec(_t.split("\nfor ten, kw in (")[0])
PHAv = PHA.values


def bao2(E, ten, extra=''):
    """Như bao() của v4soc.py, thêm sụt riêng giai đoạn 2023–26."""
    nam = E.groupby(E.index.year).last() / E.groupby(E.index.year).first() - 1
    a = E[E.index <= '2022-12-31']; b = E[E.index >= '2023-01-01']
    cagr = E.iloc[-1] ** (252 / len(E)) - 1; sut = (E / E.cummax() - 1).min()
    sutb = (b / b.cummax() - 1).min()
    bo = lambda ys: (nam.drop(ys).add(1).prod() - 1) * 100
    print(f"{ten:42s} | tong {(E.iloc[-1]-1)*100:+5.0f}% nam {cagr*100:+5.1f}% sut {sut*100:5.1f}% l/s {cagr/-sut:4.2f} | "
          f"17-22 {(a.iloc[-1]/a.iloc[0]-1)*100:+4.0f}% 23-26 {(b.iloc[-1]/b.iloc[0]-1)*100:+4.0f}% (sut {sutb*100:5.1f}%) | "
          f"bo21 {bo([2021]):+4.0f}% bo21+25 {bo([2021, 2025]):+4.0f}%", flush=True)
    print(f"{'':42s} | {extra}", flush=True)
    print(f"{'':42s} | nam " + ' '.join(f"{y%100}:{v*100:+3.0f}" for y, v in nam.items()), flush=True)


def chay_he(tinh=None, S=0, truot=0.20, thoat50=True, ato=True, von=None, chan_tran=True,
            ky=21, so=5, lot=100, ra_tien=None, pha_ra=(0, 4)):
    """chay() của v4soc3.py + lớp ra tiền mặt. Trả (E chuẩn hoá về 1, bộ đếm)."""
    i0 = dates.get_loc(dates[dates >= '2017-01-01'][0])
    v0 = float(von) if von else 1.0
    cash = v0; pos = {}; eq = []; ty = []; lan = -10 ** 9
    dem = dict(sat=0, tran=0, thieu=0, ra=0, vao=0, phien_ngoai=0, ban_loi=0, ban_sat=0, tien_mat=0.0)
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
    ngoai = False
    for d in range(i0, N - 1):
        dk = d + 1 if ato else d
        ph = PHAv[d]
        xau = ra_tien is not None and np.isfinite(ph) and int(ph) in pha_ra
        if xau:
            if not ngoai:
                ngoai = True; dem['ra'] += 1; lan = -10 ** 9   # rời pha xấu: cân lại lõi ngay
            dem['phien_ngoai'] += 1
        elif ngoai:
            ngoai = False; dem['vao'] += 1; lan = -10 ** 9
        # 1) thoát vệ tinh / 'chay' theo đóng cửa d
        if ra_tien == 'ca' and xau:
            for j in [j for j, p in pos.items() if p['kind'] != 'loi']:
                if ban(j, d, dk): dem['ban_sat'] += 1
        else:
            for j in [j for j, p in pos.items() if p['kind'] != 'loi']:
                p = pos[j]; p['dinh'] = max(p['dinh'], ADJ[d, j])
                if ADJ[d, j] <= p['dinh'] * (1 - truot) or (thoat50 and DUOI50v[d, j]): ban(j, d, dk)
        # 2) vệ tinh mới
        if tinh is not None and S > 0 and not (ra_tien == 'ca' and xau):
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
        # 3) lõi
        if ra_tien is not None and xau:
            for j in [j for j, p in pos.items() if p['kind'] == 'loi']:
                if ban(j, d, dk): dem['ban_loi'] += 1
        elif d - lan >= ky:
            lan = d
            idx = np.where(DU[d])[0]; vv = VBv[d, idx]; ok = ~np.isnan(vv); ds = list(idx[ok][np.argsort(vv[ok])])
            khac = [j for j, p in pos.items() if p['kind'] != 'loi']
            dich = [j for j in ds if j not in khac][:max(so - len(khac), 0)]
            for j in [j for j, p in pos.items() if p['kind'] == 'loi']:
                if j not in dich: ban(j, d, dk)
            slot = V_(d) / so
            for j in dich:
                if j not in pos: mua(j, d, dk, slot, 'loi')
        v = V_(d + 1) if ato else V_(d)
        eq.append(v); ty.append(cash / v if v > 0 else 1.0)
    idx_ = dates[i0 + 1:N] if ato else dates[i0:N - 1]
    dem['tien_mat'] = float(np.mean(ty))
    return pd.Series(eq, index=idx_) / v0, dem


TIN2 = tin(0.25, 20, 2.0)
print('ngay cuoi', dates[-1].date(), '| ATO, lo 100, von 40 trieu, phi+truot 0,3%/chieu, 5 cho, can lai 21 phien')
print('ve tinh: tang >=25%/20 phien, dinh 60 phien, KL x2 TB50, top100 | thoat truot 20% hoac duoi MA50 | S=2')
for ten, kw in (
    ('A loi rieng, khong lop tien mat', dict()),
    ('B loi rieng, ra tien khi HoangLoan+GiamGia', dict(ra_tien='loi')),
    ('C loi + S2, khong lop tien mat', dict(tinh=TIN2, S=2)),
    ('D loi + S2, ra tien LOI khi HoangLoan+GiamGia', dict(tinh=TIN2, S=2, ra_tien='loi')),
    ('E loi + S2, ra tien CA khi HoangLoan+GiamGia', dict(tinh=TIN2, S=2, ra_tien='ca')),
    ('D4 loi + S2, ra tien LOI khi chi GiamGia', dict(tinh=TIN2, S=2, ra_tien='loi', pha_ra=(4,))),
    ('E4 loi + S2, ra tien CA khi chi GiamGia', dict(tinh=TIN2, S=2, ra_tien='ca', pha_ra=(4,))),
):
    E, dem = chay_he(von=40_000, **kw)
    bao2(E, ten, f"ve tinh {dem['sat']} lenh, bo vi tran {dem['tran']}, thieu tien {dem['thieu']} | "
                  f"ra tien {dem['ra']} lan / vao lai {dem['vao']}, phien ngoai {dem['phien_ngoai']}, "
                  f"ban loi {dem['ban_loi']} ban ve tinh {dem['ban_sat']} | tien mat TB {dem['tien_mat']*100:4.1f}%")
