"""V4 vòng 3c: lọc lệnh theo đặc tính đã chứng minh cùng dấu (RS63 cao, ngành mạnh, thanh khoản), theo từng giai đoạn."""
import sys, pickle, numpy as np, pandas as pd
D = sys.argv[1]
exec(open(D + '/v4core.py', encoding='utf-8').read())
Q = pickle.load(open(D + '/quet.pkl', 'rb'))['KQ']; DTN = pickle.load(open(D + '/dt.pkl', 'rb'))
col = {s: j for j, s in enumerate(syms)}
PT = {0: 'HoangLoan', 1: 'TangManh', 2: 'HoiPhuc', 3: 'DieuChinh', 4: 'GiamGia'}
LOC = {
    'khong loc': lambda d: np.ones(len(d['rs63']), bool),
    'RS cao': lambda d: d['rs63'] == 2,
    'RS cao + nganh >= vua': lambda d: (d['rs63'] == 2) & (d['nganh'] >= 1),
    'RS cao + nganh + TK >= vua': lambda d: (d['rs63'] == 2) & (d['nganh'] >= 1) & (d['thanhkhoan'] >= 1),
    'RS >= vua + TK cao': lambda d: (d['rs63'] >= 1) & (d['thanhkhoan'] == 2),
}
rows = []
for (hn, tn), t in Q.items():
    t = t[t.date >= '2017-01-01'].dropna(subset=['pha'])
    jj = t.sym.map(col).values; ii = t.i.values
    d = {k: v.values[ii, jj] for k, v in DTN.items()}
    for ln, f in LOC.items():
        m = f(d)
        x = t[m]
        for (p, o), y in x.groupby([x.pha, x.date >= '2023-01-01']):
            yr = y.groupby(y.date.dt.year).ret.mean()
            rows.append(dict(ho=hn[:2], thoat=tn[:2], loc=ln, pha=PT[p], oos=o, n=len(y), tb=y.ret.mean(), thang=(y.ret > .0005).mean(), ngay=y.days.mean(), nd=(yr > 0).mean()))
A = pd.DataFrame(rows); A.to_pickle(D + '/loc.pkl')
pd.set_option('display.width', 250)
w = lambda x: np.average(x.tb, weights=x.n)
print('== TB/lenh (%) gop moi cau hinh, theo bo loc x giai doan (IS / OOS) ==')
print((A.groupby(['pha', 'loc', 'oos']).apply(w).unstack() * 100).round(2).to_string())
IS = A[~A.oos].set_index(['ho', 'thoat', 'loc', 'pha']); OS = A[A.oos].set_index(['ho', 'thoat', 'loc', 'pha'])
J = IS.join(OS, rsuffix='_o'); J = J[(J.n >= 60) & (J.n_o >= 30)]
J['diem'] = J.tb * np.sqrt(J.n)
print('\n== Moi giai doan: 5 cau hinh + bo loc tot nhat theo TRONG MAU, kem NGOAI MAU ==')
for p, x in J[J.nd >= 0.6].groupby(level='pha'):
    for idx, r in x.sort_values('diem', ascending=False).head(5).iterrows():
        print(f"{p:9s} {idx[0]} {idx[1]} {idx[2]:26s} | IS n{r.n:5.0f} TB {r.tb*100:+5.2f}% thang {r.thang*100:3.0f}% nd {r.nd*100:3.0f}% ngay {r.ngay:4.1f} | OOS n{r.n_o:5.0f} TB {r.tb_o*100:+5.2f}% thang {r.thang_o*100:3.0f}% nd {r.nd_o*100:3.0f}%")
