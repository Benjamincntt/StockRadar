"""4 tháng 16/11 → 16/03 năm sau (qua Tết âm lịch), 2017/18 → 2025/26.
(a) Danh mục chạy liên tục (cắt lãi theo cửa sổ). (b) Mua một lần ngày 16/11, giữ nguyên tới 16/03 (không cân lại), phí 0,6% một lần.
Lãi tính theo chuỗi giá đã điều chỉnh (ADJ); mã ngừng giao dịch giữa chừng giữ giá cuối."""
import sys, warnings, numpy as np, pandas as pd
warnings.filterwarnings('ignore')
D = sys.argv[1]
s = open(D + '/v4tet.py', encoding='utf-8').read()
exec(s[:s.index("pd.set_option('display.width', 260)\nfor CUA")])
NAMS = range(2017, 2026)
def ngay(y, md):
    x = dates[dates <= pd.Timestamp(f'{y}-{md}')]; return dates.get_loc(x[-1])
def cs(E):
    out = {}
    for y in NAMS:
        a = E[E.index <= f'{y}-11-16']; b = E[E.index <= f'{y+1}-03-16']
        if len(a) and a.index[-1].year == y and b.index[-1] > a.index[-1]: out[y + 1] = b.iloc[-1] / a.iloc[-1] - 1
    return pd.Series(out)
rows = {}
for ten, E in DONG.items():
    rows['[chay lien tuc] ' + ten] = pd.concat([cs(e) for e in E], axis=1).mean(axis=1) if isinstance(E, list) else cs(E)
# (b) mua một lần
AD = ADJ; Rl = np.log1p(R.where(~BAD)); VB120 = Rl.rolling(120, min_periods=72).std().values
mom = (C.shift(21) / C.shift(126) - 1).values; tv = TV20.values; top = TOP100.values; xhv = xh.values
beta = R.where(~BAD).rolling(250, min_periods=150).cov(mret).div(mret.rolling(250, min_periods=150).var(), axis=0).values
gd = (C / H.rolling(250, min_periods=200).max()).values
CHON = {
    'it bien dong, trong xu huong (5 ma)': lambda d: (top[d] & xhv[d], VB120[d], True),
    'it bien dong (5 ma)': lambda d: (top[d], VB120[d], True),
    'it bien dong (10 ma)': lambda d: (top[d], VB120[d], True),
    'thanh khoan cao nhat (5 ma, giong VN30)': lambda d: (top[d], tv[d], False),
    'beta cao (5 ma)': lambda d: (top[d], beta[d], False),
    'dong luc 6-1 thang (5 ma)': lambda d: (top[d], mom[d], False),
    'gan dinh 1 nam (5 ma)': lambda d: (top[d], gd[d], False),
    'ca top100 deu nhau': lambda d: (top[d], np.zeros(len(syms)), True),
}
for ten, f in CHON.items():
    out = {}
    so = 10 if '10 ma' in ten else (100 if 'ca top100' in ten else 5)
    for y in NAMS:
        d0, d1 = ngay(y, '11-16'), ngay(y + 1, '03-16')
        m, sc, thap = f(d0); idx = np.where(m & np.isfinite(sc))[0]
        ch = idx[np.argsort(sc[idx] if thap else -sc[idx])][:so]
        r = np.nanmean(AD[d1, ch] / AD[d0, ch]) - 1 - 0.006
        out[y + 1] = r
    rows['[mua 16/11 giu toi 16/03] ' + ten] = pd.Series(out)
rng = np.random.default_rng(1); rr = []
for k in range(20):
    out = {}
    for y in NAMS:
        d0, d1 = ngay(y, '11-16'), ngay(y + 1, '03-16'); idx = np.where(top[d0])[0]; ch = rng.choice(idx, 5, replace=False)
        out[y + 1] = np.nanmean(AD[d1, ch] / AD[d0, ch]) - 1 - 0.006
    rr.append(pd.Series(out))
rows['[mua 16/11 giu toi 16/03] ngau nhien 5 ma top100 (TB 20 lan)'] = pd.concat(rr, axis=1).mean(axis=1)
T = pd.DataFrame(rows).T * 100; cols = [c for c in range(2018, 2027) if c in T.columns]
T['TB'] = T[cols].mean(axis=1); T['trung vi'] = T[cols].median(axis=1); T['nam duong'] = (T[cols] > 0).sum(axis=1); T['te nhat'] = T[cols].min(axis=1)
pd.set_option('display.width', 270)
print('Cua so 16/11 nam truoc -> 16/03 (cot = nam ket thuc)'); print(T.sort_values('TB', ascending=False).round(1).to_string())
