import sys
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])
out = pd.concat([p.assign(kb=n) for n, p in PARTS], ignore_index=True)
out = out[out.date >= '2023-01-01']
out['ngay_e'] = [dates[int(e)] if int(e) < len(dates) else pd.NaT for e in out.e]
out.to_csv(sys.argv[1] + '/v3_sig.csv', index=False)
print(out.groupby('kb').size(), out.sym.nunique(), out.date.min(), out.date.max())
print(out.columns.tolist())
