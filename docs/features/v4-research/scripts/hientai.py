import sys, warnings, numpy as np, pandas as pd
warnings.filterwarnings('ignore')
exec(open(sys.argv[1] + '/v4core.py', encoding='utf-8').read())
d = dates[-1]; P = PHA.map(PHA_TEN)
print('ngay', d.date(), '| giai doan', P.iloc[-1], '| 10 phien gan nhat:', list(P.iloc[-10:]))
print(f'chi so / MA50 {MKT.iloc[-1]/M50.iloc[-1]-1:+.1%} | / MA200 {MKT.iloc[-1]/M200.iloc[-1]-1:+.1%} | MA50/MA200 {M50.iloc[-1]/M200.iloc[-1]-1:+.1%} | sut tu dinh 20p {SUT20.iloc[-1]:+.1%} | do rong {RONG.iloc[-1]:.2f}')
print(f'chi so tu dau 2026 {MKT.iloc[-1]/MKT[MKT.index<"2026-01-01"].iloc[-1]-1:+.1%} | tu dinh 1 nam {MKT.iloc[-1]/MKT.iloc[-250:].max()-1:+.1%} | so phien GiamGia trong 2026: {(P[P.index.year==2026]=="GiamGia").mean():.0%}')
f = {n: MKT.shift(-n) / MKT - 1 for n in (20, 60, 80)}
ok = P.notna() & (P.index >= '2016-06-01')
print('\nLai chi so sau N phien theo giai doan hien tai (TB / ty le duong / so phien):')
for p in ['GiamGia', 'HoangLoan', 'DieuChinh', 'HoiPhuc', 'TangManh']:
    m = ok & (P == p)
    print(p.ljust(10), ' | '.join(f'{n}p: {f[n][m].mean():+.1%} ({(f[n][m]>0).mean():.0%}, n{f[n][m].notna().sum()})' for n in (20, 60, 80)))
mua = ok & ((P.index.month >= 11) | (P.index.month <= 1))
print('\nRieng phien thang 11-01:')
for p in ['GiamGia', 'HoangLoan', 'DieuChinh', 'HoiPhuc', 'TangManh']:
    m = mua & (P == p)
    if m.sum(): print(p.ljust(10), ' | '.join(f'{n}p: {f[n][m].mean():+.1%} ({(f[n][m]>0).mean():.0%}, n{f[n][m].notna().sum()})' for n in (20, 60, 80)))
print('\nCac nam co giai doan GiamGia ngay ~16/11 va lai 16/11 -> 16/03:')
for y in range(2016, 2026):
    x = dates[dates <= f'{y}-11-16'][-1]; b = dates[dates <= f'{y+1}-03-16'][-1]
    print(y, P.loc[x], f'{MKT.loc[b]/MKT.loc[x]-1:+.1%}')
