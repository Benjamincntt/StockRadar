"""Kịch bản mùa 16/11 → 16/03 (qua Tết), 10 mùa 2016/17 → 2025/26. Mua / đổi giỏ theo luật cố định, lãi theo chuỗi ADJ,
phí + trượt 0,6% mỗi lần đổi toàn bộ giỏ. Top 100 thanh khoản tại ngày chọn."""
import sys, warnings, numpy as np, pandas as pd
warnings.filterwarnings('ignore')
exec(open(sys.argv[1] + '/v4core.py', encoding='utf-8').read())
ADJ = (1 + R.where(~BAD, 0.0).fillna(0.0).clip(-.15, .15)).cumprod().values
TOP = (TV20.where(UNI).rank(axis=1, ascending=False) <= 100).values
Rc = R.where(~BAD)
VB = np.log1p(Rc).rolling(120, min_periods=72).std().values
BETA = Rc.rolling(250, min_periods=150).cov(mret).div(mret.rolling(250, min_periods=150).var(), axis=0).values
GD = (C / H.rolling(250, min_periods=200).max()).values
MOM = (C.shift(21) / C.shift(126) - 1).values
RS = RS63.values
TREN200 = (C > MA[200]).values
PHAv = PHA.values
TET = {2017: '2017-01-28', 2018: '2018-02-16', 2019: '2019-02-05', 2020: '2020-01-25', 2021: '2021-02-12', 2022: '2022-02-01',
       2023: '2023-01-22', 2024: '2024-02-10', 2025: '2025-01-29', 2026: '2026-02-17'}
def i_le(ts): x = dates[dates <= pd.Timestamp(ts)]; return dates.get_loc(x[-1])
def i_truoc(ts): x = dates[dates < pd.Timestamp(ts)]; return dates.get_loc(x[-1])
def chon(d, sc, so, thap=False, them=None):
    m = TOP[d] & np.isfinite(sc[d])
    if them is not None: m &= them[d]
    idx = np.where(m)[0]
    return idx[np.argsort(sc[d, idx] if thap else -sc[d, idx])][:so]
def giu(ch, a, b): return np.nanmean(ADJ[b, ch] / ADJ[a, ch]) - 1 if len(ch) else 0.0
PHI = 0.006
def mua_giu(sc, so=10, thap=False, them=None, md='11-16'):
    out = {}
    for y in range(2016, 2026):
        a, b = i_le(f'{y}-{md}'), i_le(f'{y+1}-03-16'); out[y + 1] = (1 + giu(chon(a, sc, so, thap, them), a, b)) * (1 - PHI) - 1
    return pd.Series(out)
def lam_moi(sc, so=10, thap=False, them=None, ky=21):
    """Mua 16/11, cân lại mỗi ky phiên trong mùa (đổi giỏ theo điểm mới, phí theo phần đổi)."""
    out = {}
    for y in range(2016, 2026):
        a, b = i_le(f'{y}-11-16'), i_le(f'{y+1}-03-16'); v = 1 - PHI / 2; cu = set()
        for d in range(a, b, ky):
            ch = chon(d, sc, so, thap, them); doi = len(set(ch) - cu) / max(len(ch), 1)
            v *= (1 - PHI * doi if cu else 1); v *= 1 + giu(ch, d, min(d + ky, b)); cu = set(ch)
        out[y + 1] = v * (1 - PHI / 2) - 1
    return pd.Series(out)
def xoay(chang):
    """chang: [(mốc bắt đầu theo năm y -> chỉ số phiên, hàm chọn)] — đổi toàn bộ giỏ ở mỗi mốc."""
    out = {}
    for y in range(2016, 2026):
        moc = [f(y) for f, _ in chang] + [i_le(f'{y+1}-03-16')]; v = 1.0
        for k, (_, g) in enumerate(chang):
            a, b = moc[k], moc[k + 1]
            if b <= a: continue
            v *= (1 - PHI) * (1 + (giu(g(a), a, b) if g is not None else 0.0))
        out[y + 1] = v - 1
    return pd.Series(out)
def nua_cho(sc, so=10):
    """Giữ 50% tiền mặt; mua nửa đầu 16/11, nửa sau khi gặp Hoảng loạn hoặc ngày Tết (cái nào trước)."""
    out = {}
    for y in range(2016, 2026):
        a, b, t = i_le(f'{y}-11-16'), i_le(f'{y+1}-03-16'), i_le(TET[y + 1])
        hl = [d for d in range(a, t) if PHAv[d] == 0]; d2 = hl[0] if hl else t
        r1 = giu(chon(a, sc, so), a, b); r2 = giu(chon(d2, sc, so), d2, b)
        out[y + 1] = 0.5 * ((1 + r1) * (1 - PHI) - 1) + 0.5 * ((1 + r2) * (1 - PHI) - 1)
    return pd.Series(out)
KQ = {
    'A gan dinh 1 nam 10 ma, mua giu': mua_giu(GD),
    'B gan dinh 1 nam + RS63 top30% + tren MA200 (ma khoe khi TT yeu)': mua_giu(GD, them=(RS >= 0.7) & TREN200),
    'C it bien dong 10 ma, mua giu': mua_giu(VB, thap=True),
    'D beta cao 10 ma, mua giu': mua_giu(BETA),
    'E 5 gan dinh + 5 it bien dong': None,
    'F gan dinh 10 ma, lam moi moi 21 phien': lam_moi(GD),
    'G xoay: gan dinh (16/11) -> it bien dong (15/12) -> beta cao (Tet)': xoay([(lambda y: i_le(f'{y}-11-16'), lambda d: chon(d, GD, 10)),
                                                                                  (lambda y: i_le(f'{y}-12-15'), lambda d: chon(d, VB, 10, True)),
                                                                                  (lambda y: i_le(TET[y + 1]), lambda d: chon(d, BETA, 10))]),
    'H gan dinh, nghi 20 phien truoc Tet roi mua lai ngay Tet': xoay([(lambda y: i_le(f'{y}-11-16'), lambda d: chon(d, GD, 10)),
                                                                      (lambda y: i_truoc(TET[y + 1]) - 20, None),
                                                                      (lambda y: i_le(TET[y + 1]), lambda d: chon(d, GD, 10))]),
    'I 50% mua 16/11, 50% cho Hoang loan hoac Tet (gan dinh)': nua_cho(GD),
    'J dong luc 6-1 thang 10 ma, mua giu': mua_giu(MOM),
    'K ngau nhien 10 ma top100 (TB 20 lan)': None,
    'L chi so deu trong so 300 ma': None,
}
a5 = mua_giu(GD, 5); b5 = mua_giu(VB, 5, True); KQ['E 5 gan dinh + 5 it bien dong'] = (a5 + b5) / 2
rng = np.random.default_rng(0); rr = []
for k in range(20):
    out = {}
    for y in range(2016, 2026):
        a, b = i_le(f'{y}-11-16'), i_le(f'{y+1}-03-16'); idx = np.where(TOP[a])[0]
        out[y + 1] = (1 + giu(rng.choice(idx, 10, replace=False), a, b)) * (1 - PHI) - 1
    rr.append(pd.Series(out))
KQ['K ngau nhien 10 ma top100 (TB 20 lan)'] = pd.concat(rr, axis=1).mean(axis=1)
KQ['L chi so deu trong so 300 ma'] = pd.Series({y + 1: MKT.iloc[i_le(f'{y+1}-03-16')] / MKT.iloc[i_le(f'{y}-11-16')] - 1 for y in range(2016, 2026)})
T = pd.DataFrame(KQ).T * 100; cols = list(range(2017, 2027))
T['TB'] = T[cols].mean(axis=1); T['trung vi'] = T[cols].median(axis=1); T['duong'] = (T[cols] > 0).sum(axis=1); T['te'] = T[cols].min(axis=1)
T['TB bo 2021'] = T[[c for c in cols if c != 2021]].mean(axis=1)
G = [2019, 2020, 2025]   # mùa bắt đầu ở giai đoạn Giảm giá (16/11/2018, 2019, 2024)
T['TB mua GiamGia'] = T[G].mean(axis=1)
pd.set_option('display.width', 280)
print('Mua 16/11 -> 16/03 (cot = nam ket thuc mua); phi 0,6% moi lan doi gio')
print(T.sort_values('trung vi', ascending=False).round(1).to_string())
