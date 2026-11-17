"""V4: nạp nến ngày từ prod_stocks.txt (bản xuất production) từ năm 2012, bỏ nến từ ngày cắt trở đi."""
import json, sys, pandas as pd
src, out, cut = sys.argv[1], sys.argv[2], sys.argv[3]
rows = []; meta = []
with open(src, encoding='utf-8', errors='replace') as f:
    for line in f:
        line = line.strip()
        if line.startswith('[') or '|' not in line or line.count('|') < 4: continue
        sym, ex, sec, act, js = line.split('|', 4)
        try: h = json.loads(js)
        except Exception: print('bad', sym); continue
        meta.append((sym, ex, sec, int(act), len(h), h[0]['date'] if h else None))
        for b in h:
            if '2012-01-01' <= b['date'] < cut:
                rows.append((sym, b['date'], b['open'], b['high'], b['low'], b['close'], b['volume']))
df = pd.DataFrame(rows, columns=['sym', 'date', 'o', 'h', 'l', 'c', 'v']); df['date'] = pd.to_datetime(df['date'])
m = pd.DataFrame(meta, columns=['sym', 'ex', 'sector', 'active', 'n', 'first'])
df.to_pickle(out + '/bars.pkl'); m.to_pickle(out + '/meta.pkl')
print(len(m), 'ma', len(df), 'nen', df.date.min().date(), '->', df.date.max().date())
print(df.groupby(df.date.dt.year).sym.nunique().to_string())
