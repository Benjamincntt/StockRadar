"""Quý 4 cấp lệnh: 63 cấu hình (9 họ × 7 kiểu thoát, quet.pkl), chỉ lệnh có tín hiệu tháng 10–12, 2017–2025.
Xếp theo lãi TB/lệnh; kèm số năm có TB dương và năm tệ nhất. So với cùng cấu hình ở tháng 1–9."""
import sys, pickle, numpy as np, pandas as pd
Q = pickle.load(open(sys.argv[1] + '/quet.pkl', 'rb'))['KQ']
rows = []
for (hn, tn), t in Q.items():
    t = t[(t.date.dt.year >= 2017) & (t.date.dt.year <= 2025)]
    q = t[t.date.dt.month >= 10]; k = t[t.date.dt.month < 10]
    g = q.groupby(q.date.dt.year).ret.mean()
    rows.append(dict(cau_hinh=hn[:40] + ' | ' + tn[:30], n=len(q), thang=(q.ret > .0005).mean() * 100, tb=q.ret.mean() * 100,
                     trung_vi=q.ret.median() * 100, nam_duong=f'{(g > 0).sum()}/{len(g)}', te_nhat=g.min() * 100, tb_thang1_9=k.ret.mean() * 100))
A = pd.DataFrame(rows).sort_values('tb', ascending=False)
pd.set_option('display.width', 230)
print(A.head(15).round(2).to_string(index=False)); print('...'); print(A.tail(5).round(2).to_string(index=False))
