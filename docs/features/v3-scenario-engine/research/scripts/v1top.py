"""Đo Top 5 thật của V1 (bảng DailyOpportunities trên production) với cùng luật mua/bán của V3 KB1/KB2,
rồi so với V3 cùng kỳ. Top tạo lúc ~07:00 trước phiên -> mua giá mở cửa ngày ForTradingDate (không nhìn trước).
Chạy: python v1top.py <thư mục dữ liệu> <file v1top.txt>"""
import sys
exec(open(sys.argv[1] + '/pfeng.py', encoding='utf-8').read().replace('sys.argv[1]', repr(sys.argv[1])))

top = pd.read_csv(sys.argv[2], sep='|', header=None,
                  names=['ngay', 'sym', 'rank', 'rec', 'state', 'score', 'gen', 'price'])
top['ngay'] = pd.to_datetime(top.ngay)
top['rec'] = top.rec.fillna('').replace('', '(trống)')
print('Dữ liệu nến tới', dates[-1].date(), '| Top từ', top.ngay.min().date(), 'tới', top.ngay.max().date(), '|', len(top), 'dòng')

# tín hiệu đặt ở phiên liền trước ForTradingDate, simulate_real2 mua mở cửa phiên sau
ATC = len(sys.argv) > 3 and sys.argv[3] == 'atc'   # Top làm mới ~14:00 VN trong phiên ForTradingDate -> mua ATC cùng phiên
F10 = dict(F10, entry='atc') if ATC else F10
pos = dates.searchsorted(top.ngay) - (0 if ATC else 1)
top['sig_i'] = pos
top = top[(pos >= 0) & (top.ngay <= dates[-1])]
khong_nen = top[~top.sym.isin(syms)]
if len(khong_nen): print('Mã không có nến:', sorted(set(khong_nen.sym)))
top = top[top.sym.isin(syms)]
ngoai_uni = sum(not UNI.iat[i, syms.get_loc(s)] for i, s in zip(top.sig_i, top.sym))
print('Dòng Top ngoài vũ trụ thanh khoản của nghiên cứu (TV20<5 tỷ hoặc giá<5):', ngoai_uni)

UNI_GOC = UNI.copy()


def sig_of(rows):
    """Ma trận tín hiệu từ các dòng Top (bỏ lọc vũ trụ để không mất mã V1 đã chọn)."""
    m = pd.DataFrame(False, index=dates, columns=syms)
    for i, s in zip(rows.sig_i, rows.sym): m.iat[i, syms.get_loc(s)] = True
    return m


def chay(rows):
    global UNI
    import real2
    real2.UNI = pd.DataFrame(True, index=dates, columns=syms)   # không lọc lại: đo đúng mã V1 đã đưa lên
    t = real2.simulate_real2(sig_of(rows), **F10)
    real2.UNI = UNI_GOC
    return t


def tom(t, label):
    if len(t) == 0: print(f'{label:34s} không lệnh'); return
    w = (t.ret > 0.0005).mean() * 100
    g = t.ret[t.ret > 0].sum(); b = -t.ret[t.ret < 0].sum()
    print(f'{label:34s} lệnh {len(t):3d} | thắng {w:4.0f}% | TB {t.ret.mean()*100:+5.2f}% | trung vị {t.ret.median()*100:+5.2f}% '
          f'| PF {g/b if b > 0 else float("inf"):4.2f} | tệ {t.ret.min()*100:+6.1f}% | giữ {t.days.mean():4.1f} phiên')


d0 = top.ngay.min()
print('\n=== Từng lệnh, luật bán giống KB1/KB2 (chạm MA10 và ≥ giá vào+phí, tối đa 15 phiên), phí 0,4% ===')
tom(chay(top), 'V1 Top: tất cả dòng')
for rec, g in top.groupby('rec'): tom(chay(g), f'V1 Top: {rec}')
tom(chay(top[top['rank'] == 1]), 'V1 Top: chỉ hạng 1')
v3 = pd.read_csv(sys.argv[1] + '/v3_sig.csv', parse_dates=['date'])
v3k = v3[v3.date >= d0 - pd.Timedelta(days=1)]
tom(v3k, 'V3 tất cả tín hiệu cùng kỳ')
for kb, g in v3k.groupby('kb'): tom(g, f'V3 {kb}')
chua_xong = top[top.sig_i + 15 >= len(dates)]
print(f'(Dòng Top quá gần cuối dữ liệu, chưa đủ 15 phiên nên không tính: {len(chua_xong)})')

print('\n=== Danh mục 5 vị thế x 20% vốn, lãi kép, từ', d0.date(), '===')
import real2
real2.UNI = pd.DataFrame(True, index=dates, columns=syms)
v1t = tag(simulate_real2(sig_of(top), **F10), 'V1', 'atc' if ATC else 'open')
v1t = v1t.merge(top[['ngay', 'sym', 'rank']].assign(date=lambda x: dates[x.ngay.map(lambda n: dates.searchsorted(n) - (0 if ATC else 1))]),
                on=['date', 'sym'], how='left').sort_values(['e', 'rank'])
real2.UNI = UNI_GOC
st = str(dates[dates.searchsorted(d0) - 1].date())
E1, T1 = run_pf([('V1', v1t)], start=st)
metrics(E1, T1, 'V1 Top (ưu tiên theo hạng)')
E3, T3 = run_pf(BASE, start=st)
metrics(E3, T3, 'V3 P1 (KB1>KB2>KB4>N1>N3)')
Eb = MKT[MKT.index >= st] / MKT[MKT.index >= st].iloc[0]
print(f"Chỉ số bình quân đều cùng kỳ: {(Eb.iloc[-1]-1)*100:+.1f}% | sụt max {(Eb/Eb.cummax()-1).min()*100:.1f}%")
for lab, E in (('V1', E1), ('V3', E3)):
    print(lab, 'theo tháng:', ' | '.join(f"{i.strftime('%m/%y')} {r*100:+.1f}%" for i, r in
                                       E.resample('ME').last().pct_change().fillna(E.resample('ME').last().iloc[0] - 1).items()))

