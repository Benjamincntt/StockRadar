"""PH-32 — kiểm ngoài mẫu.
(1) Với mỗi họ trong catlo2_<mode>.csv: chọn tham số tốt nhất TRONG MẪU (01/2023–06/2025, theo lợi nhuận danh mục),
    rồi xem đoạn NGOÀI MẪU (07/2025–02/11/2026) của đúng tham số đó so với BASE. Làm cả chiều ngược (chọn ngoài, kiểm trong).
(2) Họ I8 mới — mức cắt học từ MAE (mức sụt sâu nhất trong lúc giữ) của lệnh THẮNG trong mẫu, riêng từng kịch bản:
    đặt mức cắt ngay dưới phân vị q của lệnh thắng, rồi áp nguyên mức đó cho ngoài mẫu.
Chạy: python catlo3.py <thư mục làm việc>"""
import sys
exec(open(sys.argv[1] + '/catlo2.py', encoding='utf-8').read().split("if __name__")[0])

print('### (1) Chon tham so trong mau -> kiem ngoai mau (va nguoc lai)')
for mode in ('tt', 'xau'):
    df = pd.read_csv(f'{WD}/catlo2_{mode}.csv')
    b = df.iloc[0]
    print(f'\n-- mode {mode}: BASE trong {b.IS*100:+.0f}% (sut {b.ISdd*100:.1f}) | ngoai {b.OOS*100:+.0f}% (sut {b.OOSdd*100:.1f})')
    for fam, g in df.iloc[1:].groupby('fam'):
        a = g.loc[g.IS.idxmax()]; z = g.loc[g.OOS.idxmax()]
        rule, kw = [(r_, k_) for f_, l_, r_, k_ in FAM if f_ == fam and l_ == a.label][0]
        s = summarize(a.label, apply(rule, mode=mode, **kw))
        print(f'  {fam:28s} chon trong mau: {a.label[:40]:40s} trong {a.IS*100:+4.0f}% -> ngoai {a.OOS*100:+4.0f}% (sut {a.OOSdd*100:5.1f}, BASE {b.OOS*100:+.0f}%)'
              f' lenh ngoai mau TB {s["avgOOS"]*100:+.2f}% thang {s["winOOS"]*100:.0f}% te {s["worstOOS"]*100:.1f}%'
              f' | chon ngoai mau: {z.label[:34]:34s} ngoai {z.OOS*100:+4.0f}% -> trong {z.IS*100:+4.0f}% (BASE {b.IS*100:+.0f}%)')
    s0 = summarize('BASE', [(kb, d.assign(cut=False)) for kb, d in PARTS])
    print(f'  BASE lenh ngoai mau TB {s0["avgOOS"]*100:+.2f}% thang {s0["winOOS"]*100:.0f}% te {s0["worstOOS"]*100:.1f}%')

# (2) MAE
def mae_of(t):
    j = COL[t.sym]; e, k = int(t.e), int(t.k)
    lows = L_[e + 2:k + 1, j]
    return np.nanmin(lows) / t.entry - 1 if len(lows) else 0.0

print('\n### (2) I8 muc cat hoc tu MAE cua lenh thang trong mau')
XKB = {}
for q in (0.80, 0.90, 0.95, 1.00):
    xs = {}
    for kb, d in PARTS:
        dd_ = d[(d.date <= IS[1]) & (d.ret > 0.0005)]
        if 'deep' in dd_: dd_ = dd_[~dd_.deep.astype(bool)]
        m = -dd_.apply(mae_of, axis=1)
        xs[kb] = float(np.quantile(m, q)) + 0.005          # ngay dưới phân vị q (thêm 0,5%)
    XKB[q] = xs
    print(f'  q={q:.2f}: ' + ' '.join(f'{kb} {x*100:.1f}%' for kb, x in xs.items()))

def mae_rule(xs):
    return lambda i, st: {'lv': st['ent'] * (1 - xs[st['kb']])}

rows = []
for mode in ('tt', 'xau', 'tot'):
    b = summarize('BASE', [(kb, d.assign(cut=False)) for kb, d in PARTS])
    print(f'\n-- mode {mode}'); print(fmt(b))
    for q, xs in XKB.items():
        r = summarize(f'MAE thang q{q:.2f}', apply(mae_rule(xs), mode=mode)); print(fmt(r))
        print(f'      lenh trong mau TB {r["avgIS"]*100:+.2f}% (BASE {b["avgIS"]*100:+.2f}%) | ngoai mau TB {r["avgOOS"]*100:+.2f}% '
              f'(BASE {b["avgOOS"]*100:+.2f}%) thang {r["winOOS"]*100:.0f}% (BASE {b["winOOS"]*100:.0f}%) te {r["worstOOS"]*100:.1f}% (BASE {b["worstOOS"]*100:.1f}%)')
