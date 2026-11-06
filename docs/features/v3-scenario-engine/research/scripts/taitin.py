"""Tải tiêu đề tin CafeF theo mã (khung tin trên trang mã), lùi tới trước 11/2022. Cache mỗi mã một file json.
Lịch sự: 0,7 giây giữa hai lần gọi, thử lại 3 lần khi lỗi."""
import sys, re, json, os, time, urllib.request
import pandas as pd
R = sys.argv[1]; OUT = R + '/tin'
UA = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130 Safari/537.36'}
pat = re.compile(r'timeTitle">([^<]+)</span>.*?href="([^"]+)"\s+title="([^"]+)"', re.S)
def get(sym, page):
    u = f'https://cafef.vn/du-lieu/Ajax/Events_RelatedNews_New.aspx?symbol={sym}&floorID=0&configID=0&PageIndex={page}&PageSize=30&Type=2'
    for t in range(3):
        try: return urllib.request.urlopen(urllib.request.Request(u, headers=UA), timeout=30).read().decode('utf-8', 'ignore')
        except Exception as e: print('loi', sym, page, e, flush=True); time.sleep(5 * (t + 1))
    return ''
syms = sorted(pd.read_csv(R + '/v3_sig.csv').sym.unique())
for n, s in enumerate(syms):
    f = f'{OUT}/{s}.json'
    if os.path.exists(f): continue
    rows = []; p = 1
    while p <= 80:
        m = pat.findall(get(s, p)); time.sleep(0.7)
        if not m: break
        rows += [dict(t=a.strip(), url=b, title=c) for a, b, c in m]
        if pd.to_datetime(m[-1][0].strip(), format='%d/%m/%Y %H:%M') < pd.Timestamp('2022-11-01'): break
        p += 1
    json.dump(rows, open(f, 'w', encoding='utf-8'), ensure_ascii=False)
    print(n, s, len(rows), rows[-1]['t'] if rows else '-', flush=True)
print('XONG')
