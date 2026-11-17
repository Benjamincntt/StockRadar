"""V4 vòng 2: chia lệnh theo ô (giai đoạn thị trường × nhóm biến động). Chọn cấu hình CHỈ bằng trong mẫu, rồi xem ngoài mẫu."""
import sys, pickle, numpy as np, pandas as pd
Q = pickle.load(open(sys.argv[1] + '/quet.pkl', 'rb'))['KQ']
PT = {0: 'HoangLoan', 1: 'TangManh', 2: 'HoiPhuc', 3: 'DieuChinh', 4: 'GiamGia'}; NT = {0: 'it bien dong', 1: 'vua', 2: 'bien dong cao'}
rows = []
for (hn, tn), t in Q.items():
    t = t.dropna(subset=['pha', 'nhom']); t = t[t.date >= '2017-01-01']
    t['oos'] = t.date >= '2023-01-01'; t['nam'] = t.date.dt.year
    for (p, g, o), x in t.groupby(['pha', 'nhom', 'oos']):
        yr = x.groupby('nam').ret.mean()
        rows.append(dict(ho=hn[:2], thoat=tn[:2], pha=PT[p], nhom=NT[g], oos=o, n=len(x), tb=x.ret.mean(), thang=(x.ret > .0005).mean(),
                         ngay=x.days.mean(), nam_duong=(yr > 0).mean(), so_nam=len(yr)))
A = pd.DataFrame(rows)
IS = A[~A.oos].set_index(['ho', 'thoat', 'pha', 'nhom']); OS = A[A.oos].set_index(['ho', 'thoat', 'pha', 'nhom'])
J = IS.join(OS, rsuffix='_o', how='left')
J = J[(J.n >= 100)]
J['diem'] = J.tb * np.sqrt(J.n)                 # ưu tiên lãi TB cao và đủ mẫu
pd.set_option('display.width', 250)
print('== Moi o: cau hinh tot nhat theo TRONG MAU (n>=100, >=60% nam duong), xem NGOAI MAU ==')
for (p, g), x in J[J.nam_duong >= 0.6].groupby(level=['pha', 'nhom']):
    b = x.sort_values('diem', ascending=False).head(3).reset_index()
    for _, r in b.iterrows():
        print(f"{p:9s} {g:13s} {r.ho} {r.thoat} | IS n{r.n:5.0f} TB {r.tb*100:+5.2f}% thang {r.thang*100:3.0f}% namduong {r.nam_duong*100:3.0f}% ngay {r.ngay:4.1f} "
              f"| OOS n{r.n_o:5.0f} TB {r.tb_o*100:+5.2f}% thang {r.thang_o*100:3.0f}% namduong {r.nam_duong_o*100:3.0f}%")
A.to_pickle(sys.argv[1] + '/o.pkl')
# mốc: mọi lệnh của mọi cấu hình gộp theo ô (gần với "mua đại" trong ô đó)
print('\n== Moc: TB moi lenh cua moi ho+thoat trong o (IS / OOS) ==')
m = A.groupby(['pha', 'nhom', 'oos']).apply(lambda x: np.average(x.tb, weights=x.n)).unstack()
print((m * 100).round(2).to_string())
