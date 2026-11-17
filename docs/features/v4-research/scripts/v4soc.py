"""Q4-8 — kịch bản cổ tăng sốc trên lõi giỏ ít biến động (P4-08).
Cách 1 'giu': khi cân lại, mã lõi sắp bị loại mà đã lãi >= X% từ giá vào thì GIỮ ('chay'), thoát bằng trượt Y% từ đỉnh
          hoặc đóng cửa < MA50; chiếm chỗ cho tới khi thoát.
Cách 2 've tinh': tín hiệu cổ tăng sốc → mua (tối đa S chỗ, lấy tiền bằng bán mã lõi biến động cao nhất), thoát như trên.
Bộ đo: tỷ trọng lẻ, quyết + khớp ATC (đóng cửa), định giá theo chuỗi ADJ, phí + trượt 0,3% mỗi chiều, 5 chỗ.
So với lõi riêng cùng bộ đo (mốc: +216%, sụt −31,9% ở v4lo.py 'ATC, tỷ trọng lẻ')."""
import sys, warnings, numpy as np, pandas as pd
warnings.filterwarnings('ignore')
D = sys.argv[1]
exec(open(D + '/v4core.py', encoding='utf-8').read())
Rl = np.log1p(R.where(~BAD)); VB = Rl.rolling(120, min_periods=72).std()
TOP100 = TV20.where(UNI).rank(axis=1, ascending=False) <= 100
xh = (C > MA[200]) & (MA[50] > MA[200])
ADJ = (1 + R.where(~BAD, 0.0).fillna(0.0).clip(-.15, .15)).cumprod().values
DU = (TOP100 & xh).values; VBv = VB.values; DUOI50 = (C < MA[50]).values
VMA50 = ma(V, 50); HI60 = C.rolling(60).max().shift(1); HI250 = H.rolling(250, min_periods=200).max().shift(1)
RANGE30 = ((H.rolling(30).max() - L.rolling(30).min()) / H.rolling(30).max()).shift(1)
TIN = {
    'tang soc 20p >=25%, dinh 60p, KL x2': ((C / C.shift(20) - 1 >= 0.25) & (C > HI60) & (V > 2 * VMA50) & TOP100).values,
    'Livermore dinh nam sau tich luy': ((C > HI250) & (RANGE30 <= 0.20) & (V > 1.5 * VMA50) & TOP100).values,
    'dinh nam + KL x2 (top100)': ((C > HI250) & (V > 2 * VMA50) & TOP100).values,
}
PHI = 0.003


def chay(giu_x=None, truot=0.20, thoat50=False, tin=None, S=0, ky=21, so=5):
    i0 = dates.get_loc(dates[dates >= '2017-01-01'][0])
    cash = 1.0; pos = {}; eq = []; lan = -10 ** 9; n_chay = n_sat = 0
    def V_(d): return cash + sum(p['u'] * ADJ[d, j] for j, p in pos.items())
    def ban(j, d):
        nonlocal cash
        cash += pos.pop(j)['u'] * ADJ[d, j] * (1 - PHI)
    def mua(j, d, gt, kind):
        nonlocal cash
        gt = min(gt, cash / (1 + PHI))
        if gt <= 1e-9: return
        pos[j] = dict(u=gt / ADJ[d, j], vao=ADJ[d, j], dinh=ADJ[d, j], kind=kind); cash -= gt * (1 + PHI)
    for d in range(i0, N):
        # thoát vị thế 'chay' / 've tinh' (đóng cửa)
        for j in [j for j, p in pos.items() if p['kind'] != 'loi']:
            p = pos[j]; p['dinh'] = max(p['dinh'], ADJ[d, j])
            if ADJ[d, j] <= p['dinh'] * (1 - truot) or (thoat50 and DUOI50[d, j]): ban(j, d)
        # vệ tinh mới
        if tin is not None and S > 0:
            cand = np.where(tin[d])[0]
            if len(cand):
                cand = cand[np.argsort(-RS126.values[d, cand])]
                for j in cand:
                    if sum(p['kind'] == 'sat' for p in pos.values()) >= S: break
                    if j in pos: continue
                    slot = V_(d) / so
                    if cash < slot * 0.999:
                        loi = [k for k, p in pos.items() if p['kind'] == 'loi']
                        if not loi and len(pos) >= so: break
                        if loi: ban(max(loi, key=lambda k: VBv[d, k] if np.isfinite(VBv[d, k]) else 9), d)
                    mua(j, d, slot, 'sat'); n_sat += 1
        # cân lại lõi
        if d - lan >= ky:
            lan = d
            idx = np.where(DU[d])[0]; vv = VBv[d, idx]; ok = ~np.isnan(vv); ds = list(idx[ok][np.argsort(vv[ok])])
            khac = [j for j, p in pos.items() if p['kind'] != 'loi']
            n_loi = max(so - len(khac), 0); dich = [j for j in ds if j not in khac][:n_loi]
            for j in [j for j, p in pos.items() if p['kind'] == 'loi']:
                if j in dich: continue
                if giu_x is not None and ADJ[d, j] / pos[j]['vao'] - 1 >= giu_x:
                    pos[j]['kind'] = 'chay'; pos[j]['dinh'] = ADJ[d, j]; n_chay += 1
                else: ban(j, d)
            khac = [j for j, p in pos.items() if p['kind'] != 'loi']
            dich = dich[:max(so - len(khac), 0)]
            slot = V_(d) / so
            for j in dich:
                if j in pos:
                    cur = pos[j]['u'] * ADJ[d, j]; need = slot - cur
                    if need > 0:
                        need = min(need, cash / (1 + PHI)); pos[j]['u'] += need / ADJ[d, j]; cash -= need * (1 + PHI)
                    else:
                        pos[j]['u'] += need / ADJ[d, j]; cash += -need * (1 - PHI)
                else: mua(j, d, slot, 'loi')
        eq.append(V_(d))
    return pd.Series(eq, index=dates[i0:]), n_chay, n_sat


def bao(E, ten, extra=''):
    nam = E.groupby(E.index.year).last() / E.groupby(E.index.year).first() - 1
    a = E[E.index <= '2022-12-31']; b = E[E.index >= '2023-01-01']
    cagr = E.iloc[-1] ** (252 / len(E)) - 1; sut = (E / E.cummax() - 1).min()
    bo = lambda ys: (nam.drop(ys).add(1).prod() - 1) * 100
    print(f"{ten:52s} | tong {(E.iloc[-1]-1)*100:+5.0f}% nam {cagr*100:+5.1f}% sut {sut*100:5.1f}% l/s {cagr/-sut:4.2f} | 17-22 {(a.iloc[-1]/a.iloc[0]-1)*100:+4.0f}% 23-26 {(b.iloc[-1]/b.iloc[0]-1)*100:+4.0f}% "
          f"| bo21 {bo([2021]):+4.0f}% bo21+25 {bo([2021, 2025]):+4.0f}% | {extra} | " + ' '.join(f"{y%100}:{v*100:+3.0f}" for y, v in nam.items()), flush=True)


E, a, b = chay(); bao(E, 'LOI rieng (moc)')
print('--- Cach 1: giu ma loi dang tang soc ---')
for x in (0.10, 0.20, 0.30):
    for tn, kw in (('truot 15%', dict(truot=0.15)), ('truot 25%', dict(truot=0.25)), ('truot 25% hoac thung MA50', dict(truot=0.25, thoat50=True))):
        E, nc, _ = chay(giu_x=x, **kw); bao(E, f'giu khi lai >= {x*100:.0f}%, {tn}', f'giu {nc} lan')
print('--- Cach 2: ve tinh mua co tang soc ---')
for tn_, sig in TIN.items():
    for S in (1, 2):
        for tn, kw in (('truot 20%', dict(truot=0.20)), ('truot 20% hoac thung MA50', dict(truot=0.20, thoat50=True))):
            E, _, ns = chay(tin=sig, S=S, **kw); bao(E, f'{tn_[:26]} S{S} {tn}', f'{ns} lenh')
