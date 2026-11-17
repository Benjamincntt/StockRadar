"""Tìm kịch bản mùa lãi cao hơn: điểm chọn mã khác, giới hạn ngành, độ dài mùa, số mã — mọi bản có cắt cả giỏ 10%."""
import sys, warnings
warnings.filterwarnings('ignore')
s = open(sys.argv[1] + '/v4mua2.py', encoding='utf-8').read(); exec(s[:s.index("print('--- do ben")])
def hang(x): return pd.DataFrame(x, index=dates, columns=syms).where(pd.DataFrame(TOP, index=dates, columns=syms)).rank(axis=1, pct=True).values
hGD, hVB, hRS, hBE = hang(GD), hang(-VB), hang(RS126.values), hang(BETA)
r20 = (C / C.shift(20) - 1).values
DIEM = {
    'gan dinh (goc)': GD,
    'gan dinh + it bien dong (hang TB)': (hGD + hVB) / 2,
    'gan dinh + RS126 (hang TB)': (hGD + hRS) / 2,
    'gan dinh + beta cao (hang TB)': (hGD + hBE) / 2,
    'gan dinh, chi ma 20 phien dang tang': np.where(r20 > 0, GD, np.nan),
    'gan dinh / bien dong (dinh dieu chinh rui ro)': GD / VB,
}
sec = meta.sector.reindex(syms).fillna('?').values
def chon_nganh(d, sc, so, toi_da=2):
    idx = np.where(TOP[d] & np.isfinite(sc[d]))[0]; idx = idx[np.argsort(-sc[d, idx])]; out = []; dem = {}
    for j in idx:
        if dem.get(sec[j], 0) >= toi_da: continue
        out.append(j); dem[sec[j]] = dem.get(sec[j], 0) + 1
        if len(out) == so: break
    return np.array(out)
def mua(so=10, sc=GD, het='03-16', cat_gio=0.10, nganh=None):
    out = {}
    for y in range(2016, 2026):
        a, b = i_le(f'{y}-11-16'), i_le(f'{y+1}-{het}')
        ch = chon_nganh(a, sc, so, nganh) if nganh else chon(a, sc, so)
        con = True; gt = 1.0
        for d in range(a + 1, b + 1):
            r = ADJ[d, ch] / ADJ[a, ch]; gt = np.nanmean(np.where(np.isfinite(r), r, 1.0))
            if cat_gio and gt <= 1 - cat_gio: break
        out[y + 1] = gt * (1 - PHI) - 1
    return pd.Series(out)
print('--- diem chon ma (10 ma, 16/11 -> 16/03, cat gio 10%) ---')
for t, sc in DIEM.items(): print(f'{t:46s} ' + tom(mua(10, sc)))
print('--- so ma (diem goc) ---')
for so in (3, 5, 7, 10): print(f'{so} ma{"":40s} ' + tom(mua(so)))
print('--- gioi han nganh (10 ma) ---')
for k in (1, 2, 3): print(f'toi da {k} ma/nganh{"":31s} ' + tom(mua(10, nganh=k)))
print('--- do dai mua (10 ma) ---')
for het in ('02-16', '03-16', '03-31', '04-15', '05-15'): print(f'het mua {het}{"":36s} ' + tom(mua(10, het=het)))
print('--- 5 ma + diem ket hop ---')
for t in ('gan dinh + it bien dong (hang TB)', 'gan dinh + RS126 (hang TB)', 'gan dinh / bien dong (dinh dieu chinh rui ro)'):
    print(f'5 ma {t:41s} ' + tom(mua(5, DIEM[t])))
