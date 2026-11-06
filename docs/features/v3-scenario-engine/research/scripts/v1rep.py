"""PH-31: mô phỏng mua bán cho Top V1 phát lại 2023–2026 (CSV từ V1TopReplayTests), so V3 P1 và chọn ngẫu nhiên.
Hai cách vào lệnh: 'atc' = mua ATC ngay phiên tính Top (giống Top làm mới trong phiên),
'open' = mua mở cửa phiên sau (Top tính sau đóng cửa). Bán theo luật KB1/KB2.
Chạy: python v1rep.py <thư mục dữ liệu> <v1_replay_*.csv> [nhãn]"""
import sys
exec(open(sys.argv[1] + '/pfeng.py', encoding='utf-8').read().replace('sys.argv[1]', repr(sys.argv[1])))
import real2

rep = pd.read_csv(sys.argv[2])
rep['d'] = pd.to_datetime(rep['asof'])
rep = rep[(rep.d >= '2023-01-01') & rep.d.isin(dates) & rep.symbol.isin(syms)]
rep['sig_i'] = dates.get_indexer(rep.d)
nhan = sys.argv[3] if len(sys.argv) > 3 else sys.argv[2]
print(f'== {nhan}: {len(rep)} dòng Top, {rep.d.nunique()} phiên, {rep.d.min().date()} → {rep.d.max().date()}')
print('Pha thị trường:', rep.drop_duplicates('d').phase.value_counts().to_dict(), '| TB ứng viên/phiên', round(rep.drop_duplicates('d').candidates.mean(), 1))
ALL_TRUE = pd.DataFrame(True, index=dates, columns=syms)


def sig_of(rows):
    """Ma trận tín hiệu từ các dòng (sig_i, symbol)."""
    m = np.zeros((len(dates), len(syms)), bool)
    m[rows.sig_i.values, syms.get_indexer(rows.symbol)] = True
    return pd.DataFrame(m, index=dates, columns=syms)


def chay(rows, entry):
    """Mô phỏng từng lệnh, không lọc lại vũ trụ (đo đúng mã đã chọn)."""
    real2.UNI = ALL_TRUE
    t = real2.simulate_real2(sig_of(rows), **dict(F10, entry=entry))
    real2.UNI = UNI
    return t


def tom(t, label):
    if len(t) == 0: print(f'{label:40s} không lệnh'); return
    g = t.ret[t.ret > 0].sum(); b = -t.ret[t.ret < 0].sum()
    yrs = ' | '.join(f"{y}: {t[t.date.dt.year == y].ret.mean() * 100:+.2f}% ({(t.date.dt.year == y).sum()})" for y in (2023, 2024, 2025, 2026))
    print(f'{label:40s} lệnh {len(t):4d} | thắng {(t.ret > 0.0005).mean() * 100:3.0f}% | TB {t.ret.mean() * 100:+5.2f}% | PF {g / b:4.2f} | {yrs}')


rng = np.random.default_rng(7)
pool = (UNI & (TV >= 20)).values
days = np.sort(rep.sig_i.unique())
for entry in ('atc', 'open'):
    print(f'\n--- Vào lệnh {entry}, bán chạm MA10 ≥ giá vào + phí, tối đa 15 phiên, phí 0,4% ---')
    t = chay(rep, entry)
    tom(t, 'V1 Top phát lại')
    tom(chay(rep[rep['rank'] == 1], entry), 'V1 chỉ hạng 1')
    for lab, g in rep.groupby('legacy_rec'): tom(chay(g, entry), f'V1 nhãn {lab}')
    rr = []
    for r in range(10):
        rows = [(i, syms[j]) for i in days for j in rng.choice(np.nonzero(pool[i])[0], 5, replace=False)]
        rt = chay(pd.DataFrame(rows, columns=['sig_i', 'symbol']), entry)
        rr.append([rt.ret.mean() * 100] + [rt[rt.date.dt.year == y].ret.mean() * 100 for y in (2023, 2024, 2025, 2026)])
    rr = np.array(rr)
    print(f"{'Ngẫu nhiên 5 mã/phiên TV20≥20 tỷ (10 lần)':40s} TB {rr[:, 0].mean():+5.2f}% (từ {rr[:, 0].min():+.2f} tới {rr[:, 0].max():+.2f}) | "
          + ' | '.join(f"{y}: {rr[:, k + 1].mean():+.2f}%" for k, y in enumerate((2023, 2024, 2025, 2026))))
    rk = rep.set_index(['sig_i', 'symbol'])['rank']
    tt = tag(t, 'V1', entry)
    tt['rank'] = [rk.get((i, s), 9) for i, s in zip(dates.get_indexer(tt.date), tt.sym)]
    tt = tt.sort_values(['e', 'rank'])
    E1, T1 = run_pf([('V1', tt)], start='2023-01-01')
    metrics(E1, T1, f'Danh mục V1 ({entry})')
E3, T3 = run_pf(BASE, start='2023-01-01')
metrics(E3, T3, 'Danh mục V3 P1')
yd = lambda y: dates[dates.year == y]
print('Chỉ số bình quân đều:', ' | '.join(f"{y}: {(MKT[yd(y)[-1]] / MKT[yd(y)[0]] - 1) * 100:+.1f}%" for y in (2023, 2024, 2025, 2026)))
