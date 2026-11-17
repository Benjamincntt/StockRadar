"""V4 vòng 5: chọn mã theo giỏ, cân lại định kỳ (giữ 5 mã bằng tỷ trọng). Đo ở cấp danh mục, so với chọn ngẫu nhiên trong cùng tập mã.
Quyết ở đóng cửa phiên t, khớp ATC phiên t (xấp xỉ), lãi tính từ phiên t+1. Phí + trượt 0,3% mỗi chiều trên phần đổi.
Phiên vượt biên độ (giá chưa điều chỉnh quyền) tính lãi 0. Tuỳ chọn: ra tiền mặt khi giai đoạn Giảm giá/Hoảng loạn."""
import sys, pickle, numpy as np, pandas as pd
D = sys.argv[1]
exec(open(D + '/v4core.py', encoding='utf-8').read())
DTN = pickle.load(open(D + '/dt.pkl', 'rb'))
Rd = R.where(~BAD, 0.0).fillna(0.0).clip(-.15, .15)
TOP100 = TV20.where(UNI).rank(axis=1, ascending=False) <= 100
sec = meta.sector.reindex(syms).fillna('?')


def chay(chon, ky=21, ne=None, so=5, seed=None, bat_dau='2017-01-01'):
    i0 = dates.get_loc(dates[dates >= bat_dau][0]); w = pd.Series(0.0, index=syms); eq = [1.0]; rng = np.random.default_rng(seed)
    rd = Rd.values; col = {s: j for j, s in enumerate(syms)}; wv = np.zeros(len(syms)); v = 1.0
    for d in range(i0, N - 1):
        if (d - i0) % ky == 0:
            ds = chon(d)
            if seed is not None:
                pool = chon.pool(d); ds = list(rng.choice(pool, size=min(so, len(pool)), replace=False)) if len(pool) else []
            nw = np.zeros(len(syms))
            for s in ds[:so]: nw[col[s]] = 1.0 / so
            if ne is not None and PHA.iloc[d] in ne: nw[:] = 0
            v *= 1 - 0.003 * np.abs(nw - wv).sum(); wv = nw
        elif ne is not None and PHA.iloc[d] in ne and wv.sum() > 0:
            v *= 1 - 0.003 * wv.sum(); wv = np.zeros(len(syms))
        r = (wv * rd[d + 1]).sum(); v *= 1 + r
        wv = wv * (1 + rd[d + 1]); tot = wv.sum()
        if tot > 0: wv = wv / tot * min(1.0, tot)   # giữ tỷ trọng trôi theo giá (không cân lại giữa kỳ)
        eq.append(v)
    return pd.Series(eq, index=dates[i0:])


def bao(E, ten):
    nam = E.groupby(E.index.year).last() / E.groupby(E.index.year).first() - 1
    a = E[E.index <= '2022-12-31']; b = E[E.index >= '2023-01-01']
    cagr = E.iloc[-1] ** (252 / len(E)) - 1; sut = (E / E.cummax() - 1).min()
    print(f"{ten:58s} | tong {(E.iloc[-1]-1)*100:+6.0f}% nam {cagr*100:+5.1f}% sut {sut*100:6.1f}% l/s {cagr/-sut:5.2f} | 17-22 {(a.iloc[-1]/a.iloc[0]-1)*100:+5.0f}% 23-26 {(b.iloc[-1]/b.iloc[0]-1)*100:+5.0f}% | "
          + ' '.join(f"{y%100}:{v*100:+4.0f}" for y, v in nam.items()), flush=True)


class Quy:
    """Quy tắc chọn: pool(d) = tập mã đủ điều kiện; __call__(d) = 5 mã chọn theo điểm (cao trước)."""
    def __init__(self, du, diem): self.du, self.diem = du, diem
    def pool(self, d): return list(syms[self.du.values[d]])
    def __call__(self, d):
        m = self.du.values[d]; s = self.diem.values[d]; idx = np.where(m & ~np.isnan(s))[0]
        return list(syms[idx[np.argsort(-s[idx])]])


mom = (C.shift(21) / C.shift(126) - 1)                       # động lượng 6 tháng bỏ tháng gần nhất
vol = BIEN
xh = (C > MA[200]) & (MA[50] > MA[200])
r63 = C / C.shift(63) - 1
NG = pd.DataFrame(np.nan, index=dates, columns=syms)
for s_, cols in sec.groupby(sec).groups.items():
    cols = list(cols); NG[cols] = np.repeat(r63[cols].where(TOP100[cols]).mean(axis=1).values[:, None], len(cols), 1)
gan_dinh = C / H.rolling(250, min_periods=200).max()
QUY = {
    'Q1 dong luc 6-1 thang, top100 TK': Quy(TOP100, mom),
    'Q2 dong luc 6-1, chi ma trong xu huong': Quy(TOP100 & xh, mom),
    'Q3 it bien dong nhat, top100 TK': Quy(TOP100, -vol),
    'Q4 it bien dong, trong xu huong': Quy(TOP100 & xh, -vol),
    'Q5 nganh manh nhat 63p, ma TK cao nhat trong nganh': Quy(TOP100, NG * 1000 + TV20.rank(axis=1, pct=True)),
    'Q6 gan dinh 1 nam nhat': Quy(TOP100, gan_dinh),
    'Q7 thanh khoan cao nhat (giong VN30)': Quy(TOP100, TV20),
    'Q8 dong luc / bien dong (dong luc dieu chinh rui ro)': Quy(TOP100, mom / vol),
    'Q9 dao chieu: giam nhieu nhat 1 thang, trong xu huong': Quy(TOP100 & xh, -(C / C.shift(21))),
}
_m = MKT.loc['2017-01-01':]; bao(_m / _m.iloc[0], 'MOC chi so deu trong so 300 ma')
for ten, q in QUY.items():
    for ne, nn in ((None, ''), ((0, 4), ' +ne GG/HL')):
        E = chay(q, ne=ne); bao(E, ten[:44] + nn)
    rs = [chay(q, seed=s) for s in range(8)]
    print(f"{'   ngau nhien cung tap ma (8 lan)':58s} | tong TB {np.mean([e.iloc[-1]-1 for e in rs])*100:+6.0f}% sut TB {np.mean([(e/e.cummax()-1).min() for e in rs])*100:.1f}%", flush=True)
