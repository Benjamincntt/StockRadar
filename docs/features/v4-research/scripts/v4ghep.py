"""V4 vòng 7: danh mục đổi giỏ theo giai đoạn. Cân lại mỗi 21 phiên hoặc khi giai đoạn đổi."""
import sys
exec(open(sys.argv[1] + '/v4gd.py', encoding='utf-8').read().split("fw = (1 + Rd)")[0])
BETA = DIEM['beta cao']; VB = VW[120]
q_it_xh = Quy(TOP100 & xh, -VB); q_it = Quy(TOP100, -VB); q_beta = Quy(TOP100, BETA); q_beta_xh = Quy(TOP100 & xh, BETA)


def chay_gd(bang, ky=21, so=5):
    """bang: {giai đoạn: Quy hoặc None (tiền mặt)}."""
    i0 = dates.get_loc(dates[dates >= '2017-01-01'][0]); rd = Rd.values; col = {s: j for j, s in enumerate(syms)}
    wv = np.zeros(len(syms)); v = 1.0; eq = [1.0]; lan = 0; p_cu = None
    for d in range(i0, N - 1):
        p = PHA.iloc[d]
        if (d - lan) >= ky or p != p_cu:
            q = bang.get(p); nw = np.zeros(len(syms))
            if q is not None:
                for s in q(d)[:so]: nw[col[s]] = 1.0 / so
            v *= 1 - 0.003 * np.abs(nw - wv).sum(); wv = nw; lan = d; p_cu = p
        r = (wv * rd[d + 1]).sum(); v *= 1 + r
        wv = wv * (1 + rd[d + 1]); t = wv.sum()
        if t > 0: wv = wv / t * min(1.0, t)
        eq.append(v)
    return pd.Series(eq, index=dates[i0:])


_m = MKT.loc['2017-01-01':]; bao(_m / _m.iloc[0], 'MOC chi so deu trong so')
bao(chay(q_it_xh), 'Q4 it bien dong + xu huong (khong theo giai doan)')
TM, HP, DC, HL, GG = 1, 2, 3, 0, 4
for ten, bang in (
    ('V4a: TM/DC/GG it bien dong, HL+HP beta cao', {TM: q_it_xh, DC: q_it, GG: q_it, HL: q_beta, HP: q_beta_xh}),
    ('V4b: nhu V4a nhung GG tien mat', {TM: q_it_xh, DC: q_it, GG: None, HL: q_beta, HP: q_beta_xh}),
    ('V4c: moi giai doan it bien dong, HL beta cao', {TM: q_it_xh, DC: q_it, GG: q_it, HL: q_beta, HP: q_it_xh}),
    ('V4d: it bien dong, GG+HL tien mat', {TM: q_it_xh, DC: q_it, GG: None, HL: None, HP: q_it_xh}),
    ('V4e: it bien dong moi giai doan (doi giai doan van can lai)', {TM: q_it_xh, DC: q_it_xh, GG: q_it_xh, HL: q_it_xh, HP: q_it_xh}),
):
    bao(chay_gd(bang), ten)
    bao(chay_gd(bang, so=10), ten + ' [10 ma]')
