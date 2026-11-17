import sys; exec(open(sys.argv[1] + '/v4core.py', encoding='utf-8').read())
print('so ma trong vu tru theo nam:'); print(UNI.sum(axis=1).groupby(dates.year).mean().round(0).to_string())
p = PHA.dropna().map(PHA_TEN)
print('\nty le phien theo giai doan, tung nam (%):')
print((pd.crosstab(p.index.year, p, normalize='index') * 100).round(0).to_string())
print('\nchi so deu trong so theo nam (%):'); print(((MKT.groupby(dates.year).last() / MKT.groupby(dates.year).first() - 1) * 100).round(0).to_string())
# lợi nhuận chỉ số 20 phiên sau, theo giai đoạn (kiểm nhãn có ý nghĩa không)
f20 = MKT.shift(-20) / MKT - 1
print('\nchi so 20 phien sau theo giai doan: TB % / so phien'); g = f20.groupby(p).agg(['mean', 'count']); g['mean'] *= 100; print(g.round(2).to_string())
# các đợt liên tục
chg = (p != p.shift()).cumsum(); seg = p.groupby(chg).agg(['first', 'size'])
print('\ndo dai trung binh moi dot (phien):'); print(seg.groupby('first')['size'].agg(['mean', 'count']).round(1).to_string())
