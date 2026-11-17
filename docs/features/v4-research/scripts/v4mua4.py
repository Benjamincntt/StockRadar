"""Mùa 16/11 → 16/03: giỏ gần đỉnh (N mã) + tối đa 2 chỗ cổ tăng sốc (tín hiệu P4-16) trong mùa. Mỗi chỗ 1/(N+2) vốn.
Lệnh tăng sốc: mua đóng cửa phiên tín hiệu, thoát trượt 20% hoặc thủng MA50 (bộ đo mo_phong), cắt ở cuối mùa nếu chưa thoát.
Cắt cả danh mục khi giá trị <= 90% vốn."""
import sys, warnings
warnings.filterwarnings('ignore')
s = open(sys.argv[1] + '/v4mua3.py', encoding='utf-8').read(); exec(s[:s.index("print('--- diem chon")])
VMA50 = ma(V, 50); HI60 = C.rolling(60).max().shift(1); TOPdf = pd.DataFrame(TOP, index=dates, columns=syms)
SIG = (C / C.shift(20) - 1 >= 0.25) & (C > HI60) & (V > 2 * VMA50) & TOPdf
LT = mo_phong(SIG, entry='close', hold=250, trail_pct=0.20, exit_flag=(C < MA[50]))
col = {s_: j for j, s_ in enumerate(syms)}
def mua_ve_tinh(so=8, S=2, het='03-16', cat=0.10, nganh=2):
    """Mỗi chỗ vệ tinh là một 'túi tiền' riêng: lệnh mới chỉ dùng đúng tiền của túi đó (lãi kép trong túi), không tạo thêm vốn."""
    out = {}; n_vt = 0
    for y in range(2016, 2026):
        a, b = i_le(f'{y}-11-16'), i_le(f'{y+1}-{het}'); ch = chon_nganh(a, GD, so, nganh); w = 1.0 / (so + S)
        lenh = LT[(LT.e > a) & (LT.e < b) & (~LT.sym.map(col).isin(ch))].sort_values('e')
        tui = [dict(tien=w, j=None, e=None, k=None, vao=None) for _ in range(S)]
        by_e = {}
        for _, x in lenh.iterrows(): by_e.setdefault(int(x.e), []).append(col[x.sym])
        gt = 1.0
        for d in range(a + 1, b + 1):
            for t in tui:                         # thoát lệnh đến hạn (ở giá đóng cửa phiên k)
                if t['j'] is not None and d >= t['k']:
                    t['tien'] = t['tien'] * ADJ[t['k'], t['j']] / ADJ[t['e'], t['j']] * (1 - PHI / 2); t['j'] = None
            for j in by_e.get(d, []):             # vào lệnh mới bằng túi rảnh
                if any(t['j'] == j for t in tui): continue
                ranh = [t for t in tui if t['j'] is None]
                if not ranh: break
                t = ranh[0]; k = int(LT[(LT.e == d) & (LT.sym == syms[j])].k.iloc[0])
                t['tien'] *= (1 - PHI / 2); t.update(j=j, e=d, k=min(k, b)); n_vt += 1
            r = ADJ[d, ch] / ADJ[a, ch]; core = np.nanmean(np.where(np.isfinite(r), r, 1.0)) * so * w
            sat = sum(t['tien'] * (ADJ[d, t['j']] / ADJ[t['e'], t['j']] if t['j'] is not None else 1.0) for t in tui)
            gt = core + sat
            if cat and gt <= 1 - cat: break
        out[y + 1] = gt * (1 - PHI / 2) - 1 if not any(t['j'] is not None for t in tui) else gt * (1 - PHI / 2) - 1
    return pd.Series(out), n_vt
for so, S in ((10, 0), (8, 2), (7, 3), (5, 2)):
    for het in ('02-16', '03-16'):
        r, n = mua_ve_tinh(so, S, het); print(f'{so} ma gan dinh + {S} tang soc, het {het} ({n} lenh tang soc): ' + tom(r))
