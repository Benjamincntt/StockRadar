"""V4 vòng 10: kiểm kỹ Minervini VCP — lưới điều kiện lân cận, lãi theo từng năm, và danh mục khi bỏ năm 2021."""
import sys
exec(open(sys.argv[1] + '/v4ghep2.py', encoding='utf-8').read().split("M2 = TT & VCP & PIVOT")[0])
ATRr = ATR10 / ATR50; VDRY = ma(V, 10) < VMA50
TT_LONG = (C > MA[50]) & (MA[50] > MA[200]) & (C >= 0.75 * HI250)       # Trend Template nới: chỉ 3 điều kiện chính
KW = dict(hold=250, sl_pct=0.10, exit_flag=THUNG_DAY)
rows = []
for tt_ten, tt in (('TT day du', TT), ('TT noi', TT_LONG)):
    for rg in (0.12, 0.15, 0.20, 0.25):
        for ar in (0.65, 0.75, 0.85):
            for kho in (True, False):
                for pw in (20, 30):
                    vcp = ((RANGE20 <= rg) & (ATRr <= ar) & (VDRY if kho else True)).shift(1).fillna(False)
                    piv = (C > H.rolling(pw).max().shift(1)) & (V > 1.5 * VMA50)
                    t = lenh(tt & vcp & piv & THI_TRUONG_TANG, **KW); t = t[t.date >= '2017-01-01']
                    a, b = chia(t); yr = t.groupby(t.date.dt.year).ret.mean()
                    rows.append(dict(tt=tt_ten, bien=rg, atr=ar, kho=kho, dinh=pw, n_is=len(a), tb_is=a.ret.mean(), n_oos=len(b), tb_oos=b.ret.mean(),
                                     thang_oos=(b.ret > .0005).mean(), nam_duong=(yr > 0).mean(), bo2021=t[t.date.dt.year != 2021].ret.mean()))
A = pd.DataFrame(rows); A.to_pickle(sys.argv[1] + '/vcp_luoi.pkl')
pd.set_option('display.width', 220)
print('== Luoi 96 o (thi truong tang, cat 10%/thung day): ==')
print('ty le o co TB lenh OOS > 0:', round((A.tb_oos > 0).mean() * 100), '% | IS > 0:', round((A.tb_is > 0).mean() * 100), '% | bo 2021 > 0:', round((A.bo2021 > 0).mean() * 100), '%')
print(A.assign(tb_is=A.tb_is * 100, tb_oos=A.tb_oos * 100, bo2021=A.bo2021 * 100, thang_oos=A.thang_oos * 100, nam_duong=A.nam_duong * 100)
      .round(1).sort_values(['tt', 'bien', 'atr', 'kho', 'dinh']).to_string(index=False))
# lãi theo năm của bản gốc M2
t = lenh(TT & VCP & PIVOT & THI_TRUONG_TANG, **KW); t = t[t.date >= '2017-01-01']
print('\n== Ban goc M2: lenh theo nam ==')
print(t.groupby(t.date.dt.year).ret.agg(['count', 'mean', 'median', 'min', 'max']).mul([1, 100, 100, 100, 100]).round(1).to_string())
print('5 lenh lai nhat:', t.nlargest(5, 'ret')[['date', 'sym', 'ret', 'days']].assign(ret=lambda x: (x.ret * 100).round(0)).to_string(index=False))
# danh mục: lợi nhuận năm khi bỏ 2021 (nhân các năm còn lại)
loi = ghep(t.iloc[0:0], S=0)[0]; vt3 = ghep(t, S=3)[0]
def bo(E, y):
    nam = E.groupby(E.index.year).last() / E.groupby(E.index.year).first(); return (nam.drop(y).prod() - 1) * 100
print(f"\nDanh muc bo nam 2021: loi rieng {bo(loi, 2021):+.0f}% | loi + 3 cho VCP {bo(vt3, 2021):+.0f}%")
print(f"Danh muc bo nam 2021 va 2025: loi rieng {bo(loi, [2021, 2025]):+.0f}% | loi + 3 cho VCP {bo(vt3, [2021, 2025]):+.0f}%")
