"""V4 vòng 3a: đặc tính từng mã (chỉ dùng dữ liệu trước ngày tín hiệu) và đo đặc tính nào tách được lệnh lãi/lỗ."""
import sys, pickle
exec(open(sys.argv[1] + '/v4core.py', encoding='utf-8').read())
Rc = R.where(~BAD)
# 1. Kiểu đi: tỉ số phương sai 10 phiên (>1 bám xu hướng, <1 hay đảo chiều), 250 phiên
r10 = np.log(C / C.shift(10)).where(~BAD.rolling(10).max().astype(bool))
KIEU = r10.rolling(250, min_periods=150).var() / (10 * np.log1p(Rc).rolling(250, min_periods=150).var())
# 2. Beta theo chỉ số 250 phiên
cov = Rc.rolling(250, min_periods=150).cov(mret); BETA = cov.div(mret.rolling(250, min_periods=150).var(), axis=0)
# 3. Sức bật sau hoảng loạn: TB lãi 10 phiên sau các phiên hoảng loạn trước đây (chỉ dùng cửa sổ đã kết thúc)
f10 = (C.shift(-10) / C - 1).where(bc(PHA == 0)).clip(-.5, .5)
BAT = f10.shift(10).expanding(min_periods=1).mean().where(f10.shift(10).notna().cumsum() >= 10)
# 4. Thanh khoản
TK = TV20
# 5. Ngành: sức mạnh 63 phiên của ngành (TB đều các mã trong vũ trụ) so với thị trường
sec = meta.sector.reindex(syms).fillna('?')
r63 = (C / C.shift(63) - 1).where(UNI)
NG = pd.DataFrame(index=dates, columns=syms, dtype=float)
for s, cols in sec.groupby(sec).groups.items():
    NG[list(cols)] = bc(r63[list(cols)].mean(axis=1)).loc[:, list(cols)].values
NGANH = NG.sub(r63.mean(axis=1), axis=0)
# 6. Sức mạnh tương đối của mã (đã có RS63)
DT = {'kieu': KIEU, 'beta': BETA, 'bat': BAT, 'thanhkhoan': TK, 'nganh': NGANH, 'rs63': RS63, 'bien': BIEN}
# chuẩn hoá thành thứ hạng trong vũ trụ hôm đó → ba nhóm 0/1/2
DTN = {k: np.floor(v.where(UNI).rank(axis=1, pct=True).clip(upper=.9999) * 3) for k, v in DT.items()}
pickle.dump(DTN, open(sys.argv[1] + '/dt.pkl', 'wb'))
print('do phu (ty le phien trong UNI co gia tri):', {k: round(float(v.where(UNI).notna().sum().sum() / UNI.sum().sum()), 2) for k, v in DTN.items()})

Q = pickle.load(open(sys.argv[1] + '/quet.pkl', 'rb'))['KQ']
col = {s: j for j, s in enumerate(syms)}
PT = {0: 'HoangLoan', 1: 'TangManh', 2: 'HoiPhuc', 3: 'DieuChinh', 4: 'GiamGia'}
rows = []
for (hn, tn), t in Q.items():
    t = t[(t.date >= '2017-01-01')].dropna(subset=['pha'])
    jj = t.sym.map(col).values; ii = t.i.values
    for k, v in DTN.items():
        g = v.values[ii, jj]
        x = pd.DataFrame({'ret': t.ret.values, 'g': g, 'pha': t.pha.values, 'oos': (t.date >= '2023-01-01').values}).dropna()
        for (p, o, gg), y in x.groupby(['pha', 'oos', 'g']):
            rows.append(dict(ho=hn[:2], thoat=tn[:2], dt=k, pha=PT[p], oos=o, g=int(gg), n=len(y), tb=y.ret.mean()))
A = pd.DataFrame(rows); A.to_pickle(sys.argv[1] + '/dt_a.pkl')
# Tóm tắt: với mỗi đặc tính, chênh lãi TB nhóm cao (2) − nhóm thấp (0), gộp mọi cấu hình, theo giai đoạn
def w(x): return np.average(x.tb, weights=x.n)
S = A.groupby(['dt', 'pha', 'oos', 'g']).apply(w).unstack('g')
S['cao-thap'] = S[2] - S[0]
T = (S['cao-thap'] * 100).unstack('oos').round(2); T.columns = ['IS', 'OOS']
T['cung dau'] = np.sign(T.IS) == np.sign(T.OOS)
pd.set_option('display.width', 200)
print('\n== Chenh lai TB/lenh (diem %) giua nhom cao va nhom thap cua tung dac tinh, gop moi cau hinh ==')
print(T.to_string())
