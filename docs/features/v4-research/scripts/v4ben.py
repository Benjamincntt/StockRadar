"""V4 vòng 5b: độ bền của chọn mã ít biến động — lưới tham số lân cận, giá mã được chọn (lô 100)."""
import sys
exec(open(sys.argv[1] + '/v4xv.py', encoding='utf-8').read().split("bao(MKT.loc")[0].split("_m = MKT")[0])
Rl = np.log1p(R.where(~BAD))
VW = {n: Rl.rolling(n, min_periods=int(n * .6)).std() for n in (60, 120, 250)}
TOPN = {n: TV20.where(UNI).rank(axis=1, ascending=False) <= n for n in (50, 100, 200)}
print(f"{'cau hinh':58s}")
for vn in (60, 120, 250):
    for tn in (50, 100, 200):
        for so in (5, 10):
            for ky in (10, 21, 63):
                for xu in (False, True):
                    du = TOPN[tn] & (xh if xu else True)
                    E = chay(Quy(du, -VW[vn]), ky=ky, so=so)
                    bao(E, f"bien {vn}p top{tn} {so}ma ky{ky} {'xu huong' if xu else 'moi ma'}")
# giá các mã được chọn ở cấu hình chính (Q4: 120p, top100, 5 mã, 21p)
q = Quy(TOP100 & xh, -VW[120]); gia = []
i0 = dates.get_loc(dates[dates >= '2017-01-01'][0])
for d in range(i0, N - 1, 21):
    for s in q(d)[:5]: gia.append(C.iloc[d][s])
g = pd.Series(gia); print('\ngia ma duoc chon (nghin dong): trung vi', round(g.median(), 1), 'p90', round(g.quantile(.9), 1), 'max', round(g.max(), 1),
                          '| ty le > 80 (1 lo > 8 trieu = 1/5 von 40tr):', round((g > 80).mean() * 100, 1), '%')
