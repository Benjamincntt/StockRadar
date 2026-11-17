"""Độ bền 'mua 16/11 giữ tới 16/03': beta cao / gần đỉnh 1 năm / ít biến động với 5, 10, 20 mã; ngày mua 01/11, 16/11, 01/12."""
import sys, warnings, numpy as np, pandas as pd
warnings.filterwarnings('ignore')
exec(open(sys.argv[1] + '/v4core.py', encoding='utf-8').read())
ADJ = (1 + R.where(~BAD, 0.0).fillna(0.0).clip(-.15, .15)).cumprod().values
TOP = (TV20.where(UNI).rank(axis=1, ascending=False) <= 100).values
beta = R.where(~BAD).rolling(250, min_periods=150).cov(mret).div(mret.rolling(250, min_periods=150).var(), axis=0).values
gd = (C / H.rolling(250, min_periods=200).max()).values
vb = np.log1p(R.where(~BAD)).rolling(120, min_periods=72).std().values
def ngay(y, md): x = dates[dates <= pd.Timestamp(f'{y}-{md}')]; return dates.get_loc(x[-1])
print(f"{'cach chon':28s} {'mua':6s} " + ' '.join(f'{y%100:>5d}' for y in range(2018, 2027)) + '    TB  trung vi  duong  te')
for ten, sc, thap in (('beta cao', beta, False), ('gan dinh 1 nam', gd, False), ('it bien dong', vb, True)):
    for so in (5, 10, 20):
        for md in ('11-01', '11-16', '12-01'):
            v = []
            for y in range(2017, 2026):
                d0, d1 = ngay(y, md), ngay(y + 1, '03-16'); idx = np.where(TOP[d0] & np.isfinite(sc[d0]))[0]
                ch = idx[np.argsort(sc[d0, idx] if thap else -sc[d0, idx])][:so]
                v.append((np.nanmean(ADJ[d1, ch] / ADJ[d0, ch]) - 1 - 0.006) * 100)
            v = np.array(v)
            print(f'{ten + f" ({so} ma)":28s} {md:6s} ' + ' '.join(f'{x:+5.0f}' for x in v) + f'  {v.mean():+5.1f}  {np.median(v):+6.1f}  {(v>0).sum()}/9 {v.min():+5.0f}')
