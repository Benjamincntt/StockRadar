"""Q4-4 — giỏ ít biến động khi đặt lệnh thật.

Lõi như P4-08: top 100 thanh khoản, giá > MA200 và MA50 > MA200, 5 mã biến động 120 phiên thấp nhất,
cân lại 21 phiên. Ba cách đặt vốn × hai cách khớp:
  - 'le'      : tỷ trọng lẻ, không làm tròn lô (bản v4ghep2.py, dùng làm mốc).
  - 40_000    : lô 100 cổ phiếu, vốn 40 triệu (đơn vị nghìn đồng như dữ liệu).
  - 100_000   : lô 100 cổ phiếu, vốn 100 triệu.
  - 'atc' : quyết ở đóng cửa t, khớp đóng cửa t.
  - 'ato' : quyết ở đóng cửa t, khớp mở cửa t+1; mã không có giá mở cửa t+1 thì không mua, xét mã kế.
Định giá vị thế giữa hai lần cân lại theo chuỗi ADJ (như v4ghep2.py) — KHÔNG dùng giá thô C,
để phiên chia tách / quyền không tạo sụt giả. Số cổ chỉ dùng lúc mua / bán.
Lô 100: số cổ cho CẢ VỊ THẾ = làm tròn tới 100 gần nhất của (giá trị chỗ ÷ giá), ít nhất 1 lô,
không vượt tiền mặt; không đủ 1 lô thì bỏ mã, xét mã kế. Giao dịch là phần chênh với số cổ đang giữ.
Thứ tự mỗi lần cân lại: bán hết mã rời giỏ TRƯỚC, rồi mới mua — nếu mua trước thì tiền bị khoá,
danh mục kẹt tiền mặt và lãi tụt (bản nháp đầu bị đúng lỗi này: +125% thay vì +217%).
Phí + trượt 0,3% mỗi chiều. Tiền mặt thừa lãi 0%.
"""
import sys, numpy as np, pandas as pd
D = sys.argv[1]
exec(open(D + '/v4core.py', encoding='utf-8').read())

Rl = np.log1p(R.where(~BAD)); VB = Rl.rolling(120, min_periods=72).std()
TOP100 = TV20.where(UNI).rank(axis=1, ascending=False) <= 100
xh = (C > MA[200]) & (MA[50] > MA[200])
# Chuỗi giá đã điều chỉnh (phiên vượt biên độ do quyền tính lãi 0) — dùng để định giá vị thế.
ADJ = (1 + R.where(~BAD, 0.0).fillna(0.0).clip(-.15, .15)).cumprod().values
cv = C.ffill().values
ov = O.values
DU = (TOP100 & xh).values
VBv = VB.values
NS = len(syms)


def chay(kieu='atc', von=None, so=5, ky=21, lot=100, phi=0.003, du_phong=15):
    """kieu: 'atc' (quyết + khớp đóng cửa t) hoặc 'ato' (quyết đóng cửa t, khớp mở cửa t+1).
    von=None → tỷ trọng lẻ; von=số tiền → lô 100. Trả E (chuẩn hoá về 1), tỉ lệ tiền mặt theo ngày, các bộ đếm."""
    lo_mode = von is not None
    i0 = dates.get_loc(dates[dates >= '2017-01-01'][0])
    v0 = 1.0 if not lo_mode else float(von)
    cash = v0
    units = np.zeros(NS)
    eq = []; tyc = []; gaps = []; gapsb = []
    lan = -10 ** 9
    pend = None
    n_cl = n_cl_bo = n_bo_lan = n_bo_tien = n_khong_gia = n_qua_cho = 0

    def khop(d_dec, d_khop, pool, V):
        """Bán trước, mua sau. pool xếp theo biến động thấp trước; 5 mã đầu là đích, phần sau để bù chỗ trống."""
        nonlocal cash, n_cl_bo, n_bo_lan, n_bo_tien, n_khong_gia, n_qua_cho, gaps, gapsb
        ato = d_khop != d_dec
        f = (ov[d_khop] / cv[d_dec]) if ato else np.ones(NS)

        def gia(j):
            g = ov[d_khop, j] if ato else cv[d_dec, j]
            return g if (np.isfinite(g) and g > 0 and np.isfinite(f[j]) and f[j] > 0) else np.nan

        def doi(j, so_co, px):
            """so_co: số cổ mua (+)/bán (−). Quy về đơn vị điều chỉnh theo giá đóng cửa ngày quyết định."""
            nonlocal cash, gaps, gapsb
            units[j] += so_co * cv[d_dec, j] / ADJ[d_dec, j]
            if ato:
                g_ = px / cv[d_dec, j] - 1   # chênh mở cửa t+1 so với đóng cửa t
                (gaps if so_co > 0 else gapsb).append(g_)
            cash += (-so_co * px * (1 - phi)) if so_co < 0 else (-so_co * px * (1 + phi))

        def so_co_dang_giu(j):
            return units[j] * ADJ[d_dec, j] / cv[d_dec, j]

        target = V / so
        giu = set(); bo_lan = False
        # 1. bán hết mã không còn trong 5 mã đích
        want = set(pool[:so])
        for j in np.nonzero(units > 1e-12)[0]:
            if j in want:
                continue
            px = gia(j)
            if np.isnan(px):
                continue
            doi(j, -so_co_dang_giu(j), px)
        # 2. điền 5 chỗ đích
        for j in pool[:so]:
            px = gia(j)
            if np.isnan(px):
                if units[j] > 1e-12:
                    giu.add(j)
                else:
                    n_khong_gia += 1
                continue
            if not lo_mode:
                gt = ADJ[d_dec, j] * f[j]
                need = target - units[j] * gt
                if need > 0:
                    need = min(need, cash / (1 + phi))
                    if need > 1e-12:
                        units[j] += need / gt; cash -= need * (1 + phi)
                        if ato:
                            gaps.append(px / cv[d_dec, j] - 1)
                elif need < 0:
                    units[j] += need / gt; cash += -need * (1 - phi)
                giu.add(j)
                continue
            sh_giu = int(np.floor(so_co_dang_giu(j) / lot + 0.5)) * lot
            sh_dich = max(int(np.floor(target / px / lot + 0.5)) * lot, lot)
            delta = sh_dich - sh_giu
            while delta > 0 and delta * px > cash:
                delta -= lot
            if delta > 0:
                doi(j, delta, px)
            elif delta < 0:
                doi(j, delta, px)
            elif sh_giu <= 0:
                n_bo_tien += 1; bo_lan = True   # không đủ tiền mặt cho 1 lô → bỏ mã, xét mã kế
                continue
            if (sh_giu + max(delta, 0)) * px > target * 1.02:
                n_qua_cho += 1                  # 1 lô vượt giá trị 1 chỗ nhưng vẫn mua theo luật "ít nhất 1 lô"
            giu.add(j)
        # 3. bù chỗ trống bằng mã kế tiếp
        for j in pool[so:]:
            if len(giu) >= so:
                break
            px = gia(j)
            if np.isnan(px):
                n_khong_gia += 1
                continue
            if lo_mode:
                sh_dich = max(int(np.floor(target / px / lot + 0.5)) * lot, lot)
                if sh_dich * px > cash:
                    n_bo_tien += 1; bo_lan = True
                    continue
                doi(j, sh_dich, px)
                if sh_dich * px > target * 1.02:
                    n_qua_cho += 1
            else:
                need = min(target, cash / (1 + phi))
                if need <= 1e-12:
                    continue
                doi(j, need / px, px)
            giu.add(j)
        # 4. mã còn giữ mà không nằm trong 'giu' thì bán nốt
        for j in np.nonzero(units > 1e-12)[0]:
            if j in giu:
                continue
            px = gia(j)
            if np.isnan(px):
                continue
            doi(j, -so_co_dang_giu(j), px)
        if bo_lan:
            n_cl_bo += 1

    for d in range(i0, N):
        if pend is not None and pend[0] == d:
            _, d_dec, pool, v0x = pend
            pend = None
            khop(d_dec, d, pool, v0x)
        v = cash + float(units @ ADJ[d])
        eq.append(v); tyc.append(cash / v if v > 0 else 1.0)
        if d - lan >= ky:
            lan = d; n_cl += 1
            idx = np.where(DU[d])[0]
            vv = VBv[d, idx]; ok = ~np.isnan(vv)
            pool = [int(x) for x in idx[ok][np.argsort(vv[ok])][:so + du_phong]]
            if kieu == 'atc':
                khop(d, d, pool, v)
                eq[-1] = cash + float(units @ ADJ[d])
                tyc[-1] = cash / eq[-1] if eq[-1] > 0 else 1.0
            elif d + 1 < N:
                pend = (d + 1, d, pool, v)
    E = pd.Series(eq, index=dates[i0:]) / v0
    return E, tyc, n_cl, n_cl_bo, n_bo_tien, n_qua_cho, n_khong_gia, gaps, gapsb


def bao(E, ten, tyc, n_cl, n_bo_lan, n_bo_tien, n_qua_cho, n_kg, gaps, gapsb):
    nam = E.groupby(E.index.year).last() / E.groupby(E.index.year).first() - 1
    a = E[E.index <= '2022-12-31']; b = E[E.index >= '2023-01-01']
    cagr = E.iloc[-1] ** (252 / len(E)) - 1; sut = (E / E.cummax() - 1).min()
    print(f'{ten:26s} | tong {(E.iloc[-1]-1)*100:+6.0f}% nam {cagr*100:+5.1f}% sut {sut*100:6.1f}% l/s {cagr/-sut:4.2f} '
          f'| 17-22 {(a.iloc[-1]/a.iloc[0]-1)*100:+5.0f}% 23-26 {(b.iloc[-1]/b.iloc[0]-1)*100:+5.0f}% (sut {((b/b.cummax()-1).min())*100:5.1f}%) '
          f'| tien mat TB {np.mean(tyc)*100:4.1f}% | can lai {n_cl}, bo vi thieu tien 1 lo {n_bo_lan} ({n_bo_lan/n_cl*100:4.1f}%) '
          f'[mua vuot 1 cho {n_qua_cho}], thieu gia mo cua {n_kg}', flush=True)
    if gaps and gapsb:
        print(f'{"":26s} | chenh mo cua t+1 vs dong cua t: mua TB {np.mean(gaps)*100:+.3f}% ({len(gaps)} lenh), '
              f'ban TB {np.mean(gapsb)*100:+.3f}% ({len(gapsb)} lenh)', flush=True)
    print(f'{"":26s} | nam ' + ' '.join(f'{y%100}:{v*100:+4.0f}' for y, v in nam.items()), flush=True)


print('ngay cuoi', dates[-1].date(), '| vu tru', len(syms), '| phi 0,3% moi chieu, can lai 21 phien, 5 ma')
for kieu, kl in (('atc', 'ATC cung phien'), ('ato', 'ATO phien sau')):
    for von, vl in ((None, 'ty trong le'), (40_000, 'lo 100, von 40 trieu'), (100_000, 'lo 100, von 100 trieu')):
        E, tyc, n_cl, n_bo_lan, n_bo_tien, n_qua_cho, n_kg, gaps, gapsb = chay(kieu, von)
        bao(E, f'{kl} | {vl}', tyc, n_cl, n_bo_lan, n_bo_tien, n_qua_cho, n_kg, gaps, gapsb)
