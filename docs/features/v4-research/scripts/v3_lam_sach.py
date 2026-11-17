"""Bộ dữ liệu cho V3 trong bộ đo V4: nến từ 2015 (v4/bars.pkl), sửa đơn vị giá như v4core.py, bỏ mã dữ liệu hỏng."""
import pandas as pd, numpy as np
df = pd.read_pickle('../v4/bars.pkl'); meta = pd.read_pickle('../v4/meta.pkl')
med = df[df.c < 500].groupby('sym').c.median()
sai_nho = df.sym.map(med).fillna(999) < 100
n = 0
for k in 'ohlc':
    m = (df[k] >= 500) & sai_nho; n += int(m.sum()); df.loc[m, k] = df.loc[m, k] / 1000
rg = df.groupby('sym').c.agg(lambda x: x.max() / x.min())
loai = rg[rg > 200].index
df = df[~df.sym.isin(loai)]; meta = meta[~meta.sym.isin(loai)]
df.to_pickle('bars.pkl'); meta.to_pickle('meta.pkl')
print('sua', n, 'gia; loai', len(loai), 'ma; con', df.sym.nunique(), 'ma,', len(df), 'nen', df.date.min().date(), '->', df.date.max().date())
