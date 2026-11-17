"""V4 vòng 6: kịch bản theo giai đoạn — mỗi phiên, giỏ 5 mã chọn theo từng đặc tính (top100 TK), lãi 21 phiên sau; gom theo giai đoạn."""
import sys
exec(open(sys.argv[1] + '/v4ben.py', encoding='utf-8').read().split("print(f\"{'cau hinh'")[0])
DIEM = {'it bien dong': -VW[120], 'bien dong cao': VW[120], 'dong luc 6-1': mom, 'beta cao': None, 'TK cao nhat': TV20,
        'giam manh 1 thang': -(C / C.shift(21)), 'gan dinh nam': gan_dinh, 'ngau nhien': None}
Rl0 = R.where(~BAD)
DIEM['beta cao'] = Rl0.rolling(250, min_periods=150).cov(mret).div(mret.rolling(250, min_periods=150).var(), axis=0)
fw = (1 + Rd).rolling(21).apply(np.prod, raw=True).shift(-21) - 1            # lãi 21 phiên tới, từ phiên t+1
fwv = fw.values; du = (TOP100 & xh).values; du2 = TOP100.values
rng = np.random.default_rng(0); rows = []
i0 = dates.get_loc(dates[dates >= '2017-01-01'][0])
for d in range(i0, N - 22, 5):
    p = PHA.iloc[d]
    for tn, m in (('trong xu huong', du), ('moi ma', du2)):
        idx = np.where(m[d])[0]
        if len(idx) < 10: continue
        for k, s in DIEM.items():
            if s is None: ch = rng.choice(idx, 5, replace=False)
            else:
                v = s.values[d, idx]; ok = ~np.isnan(v); ch = idx[ok][np.argsort(-v[ok])[:5]]
            r = np.nanmean(fwv[d, ch]) - 0.006
            rows.append(dict(d=dates[d], pha=PHA_TEN.get(p, '?'), tap=tn, dt=k, r=r))
A = pd.DataFrame(rows); A['oos'] = A.d >= '2023-01-01'
pd.set_option('display.width', 220)
for tn in ('trong xu huong', 'moi ma'):
    x = A[A.tap == tn]
    T = (x.groupby(['pha', 'dt', 'oos']).r.mean().unstack() * 100).round(2); T.columns = ['IS 17-22', 'OOS 23-26']
    print(f'\n== Lai TB 21 phien cua gio 5 ma (%, da tru 0,6%), tap ma: {tn} =='); print(T.unstack('dt').swaplevel(axis=1).sort_index(axis=1).to_string())
print('\nso diem quan sat moi giai doan (IS/OOS):'); print(A[A.dt == 'ngau nhien'].groupby(['pha', 'oos']).size().unstack().to_string())
