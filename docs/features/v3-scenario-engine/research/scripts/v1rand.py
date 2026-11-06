"""Mốc ngẫu nhiên cho v1top.py: cùng ngày Top, chọn ngẫu nhiên 5 mã trong vũ trụ TV20>=20 tỷ, cùng luật bán."""
import sys
exec(open(sys.argv[1] + '/v1top.py', encoding='utf-8').read().split("d0 = top.ngay.min()")[0])
rng = np.random.default_rng(7); days = sorted(set(top.sig_i)); pool = (UNI & (TV >= 20)).values
res = []
for r in range(30):
    rows = []
    for i in days:
        c = np.nonzero(pool[i])[0]
        for j in rng.choice(c, 5, replace=False): rows.append((i, syms[j]))
    t = chay(pd.DataFrame(rows, columns=['sig_i', 'sym']))
    res.append((t.ret.mean() * 100, (t.ret > 0.0005).mean() * 100, len(t)))
a = np.array(res)
print(f'Ngẫu nhiên 30 lần: TB/lệnh {a[:,0].mean():+.2f}% (từ {a[:,0].min():+.2f} tới {a[:,0].max():+.2f}) | thắng {a[:,1].mean():.0f}% | lệnh ~{a[:,2].mean():.0f}')
print('Số dòng Top V1 có TV20>=20 tỷ:', sum(TV.iat[i, syms.get_loc(s)] >= 20 for i, s in zip(top.sig_i, top.sym)), '/', len(top))
