"""V4 vòng 3b: cá nhân hoá theo từng mã, cuốn chiếu theo năm.
Đầu mỗi năm Y, với mỗi mã và mỗi cấu hình (họ × thoát), ước lượng lãi TB từ các lệnh ĐÃ ĐÓNG trước Y,
co về trung bình của cấu hình trong cùng giai đoạn (k lệnh giả). Trong năm Y, chỉ nhận lệnh của cấu hình
có ước lượng tốt nhất của mã đó (và > ngưỡng). So với: mọi lệnh / chỉ lọc theo giai đoạn."""
import sys, pickle, numpy as np, pandas as pd
D = sys.argv[1]
exec(open(D + '/v4core.py', encoding='utf-8').read())
Q = pickle.load(open(D + '/quet.pkl', 'rb'))['KQ']
T = []
for n, ((hn, tn), t) in enumerate(Q.items()):
    t = t.dropna(subset=['pha']).copy(); t['cfg'] = n; t['ten'] = hn[:2] + tn[:2]; T.append(t)
T = pd.concat(T, ignore_index=True)
T['ngay_dong'] = dates[T.k.values]; T['nam'] = T.date.dt.year
K = 10
out = []
for Y in range(2019, 2027):
    cu = T[T.ngay_dong < f'{Y}-01-01']
    if len(cu) == 0: continue
    # trung bình của cấu hình theo giai đoạn (mốc để co về)
    mc = cu.groupby(['cfg', 'pha']).ret.mean().rename('mc')
    ms = cu.groupby(['sym', 'cfg', 'pha']).ret.agg(['mean', 'count']).reset_index()
    ms = ms.join(mc, on=['cfg', 'pha'])
    ms['uoc'] = (ms['mean'] * ms['count'] + ms.mc * K) / (ms['count'] + K)
    moi = T[T.nam == Y].merge(ms[['sym', 'cfg', 'pha', 'uoc', 'count']], on=['sym', 'cfg', 'pha'], how='left')
    moi = moi.merge(mc.reset_index(), on=['cfg', 'pha'], how='left')
    moi['uoc'] = moi.uoc.fillna(moi.mc)
    # cấu hình tốt nhất của mã trong giai đoạn đó
    best = moi.groupby(['sym', 'pha']).uoc.transform('max')
    for nguong in (0.0, 0.005, 0.01):
        a = moi[(moi.uoc == best) & (moi.uoc > nguong)]
        out.append(dict(nam=Y, cach=f'ca nhan hoa, uoc>{nguong*100:.1f}%', n=len(a), tb=a.ret.mean()))
    b = moi[moi.mc > 0.005]
    out.append(dict(nam=Y, cach='chi theo cau hinh+giai doan (mc>0.5%)', n=len(b), tb=b.ret.mean()))
    out.append(dict(nam=Y, cach='moi lenh', n=len(moi), tb=moi.ret.mean()))
R_ = pd.DataFrame(out)
P = R_.pivot(index='cach', columns='nam', values='tb') * 100
Nn = R_.pivot(index='cach', columns='nam', values='n')
pd.set_option('display.width', 220)
print('TB lai/lenh (%) theo nam — moi nam chi dung du lieu cac nam truoc de chon:'); print(P.round(2).to_string())
print('\nSo lenh:'); print(Nn.to_string())
print('\nTB 2019-2022 / 2023-2026:'); print(pd.DataFrame({'19-22': P[[c for c in P.columns if c <= 2022]].mean(axis=1), '23-26': P[[c for c in P.columns if c >= 2023]].mean(axis=1)}).round(2).to_string())
