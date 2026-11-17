"""V4 vòng 9: lõi + vệ tinh. Lõi = giỏ ít biến động (top100 TK, đang trong xu hướng), cân lại mỗi 21 phiên.
Vệ tinh = lệnh Minervini/Livermore, tối đa S chỗ; khi có tín hiệu thì bán mã lõi yếu nhất để lấy tiền.
Tổng 5 chỗ, mỗi chỗ 1/5 giá trị danh mục. Lẻ cổ phiếu (chưa làm tròn lô). Phí + trượt 0,3% mỗi chiều."""
import sys
exec(open(sys.argv[1] + '/v4mm.py', encoding='utf-8').read().split("CH = {")[0])
Rl = np.log1p(R.where(~BAD)); VB = Rl.rolling(120, min_periods=72).std()
TOP100 = TV20.where(UNI).rank(axis=1, ascending=False) <= 100
xh = (C > MA[200]) & (MA[50] > MA[200])
DU = (TOP100 & xh).values; VBv = VB.values; col = {s: j for j, s in enumerate(syms)}
# Lõi định giá theo chuỗi đã điều chỉnh (phiên vượt biên độ do quyền tính lãi 0); vệ tinh theo giá thô điền tiếp (lệnh có phiên quyền đã bị bỏ)
ADJ = (1 + R.where(~BAD, 0.0).fillna(0.0).clip(-.15, .15)).cumprod().values
cc = C.ffill().values


def ghep(sat, S=2, ky=21, ne=None, so=5):
    sat = sat[sat.date >= '2017-01-01']
    sat = sat.assign(prio=-RSm.values[sat.i.values, sat.sym.map(col).values]).sort_values(['e', 'prio'])
    by_e = {e: g for e, g in sat.groupby('e')}
    i0 = dates.get_loc(dates[dates >= '2017-01-01'][0])
    cash = 1.0; core = {}; sats = []; eq = []; lan = -10 ** 9; nsat = 0
    def gia(j, d):
        x = cc[d, j]; return x if not np.isnan(x) else np.nan
    def gtri(d):
        v = cash
        for j, sh in core.items():
            v += sh * ADJ[d, j]
        for p in sats:
            x = gia(p['j'], d); v += p['sh'] * (x if not np.isnan(x) else p['ent'])
        return v
    for d in range(i0, N):
        # vệ tinh đến hạn bán
        keep = []
        for p in sats:
            if p['k'] == d: cash += p['cost'] * (1 + p['ret'])
            else: keep.append(p)
        sats = keep
        V_ = gtri(d); slot = V_ / so
        # vệ tinh mới (mua ở giá vào của lệnh)
        if d in by_e:
            for _, x in by_e[d].iterrows():
                if len(sats) >= S: break
                j = col[x.sym]
                if any(p['j'] == j for p in sats): continue
                if j in core:   # mã đang ở lõi: chuyển sang vệ tinh
                    cash += core.pop(j) * ADJ[d, j] * (1 - 0.003)
                if cash < slot * 0.999 and core:
                    yeu = max(core, key=lambda jj: VBv[d, jj] if not np.isnan(VBv[d, jj]) else 9)
                    cash += core.pop(yeu) * ADJ[d, yeu] * (1 - 0.003)
                cost = min(slot, cash)
                if cost <= 0: break
                cash -= cost; nsat += 1
                sats.append(dict(j=j, k=int(x.k), cost=cost, sh=cost / x.entry, ent=x.entry, ret=x.ret))
        # lõi: cân lại định kỳ
        if d - lan >= ky:
            lan = d; n_loi = so - len(sats)
            idx = np.where(DU[d])[0]; v = VBv[d, idx]; ok = ~np.isnan(v); ds = list(idx[ok][np.argsort(v[ok])])
            ds = [j for j in ds if all(p['j'] != j for p in sats)][:n_loi]
            if ne is not None and PHA.iloc[d] in ne: ds = []
            for j in list(core):
                if j not in ds: cash += core.pop(j) * ADJ[d, j] * (1 - 0.003)
            V_ = gtri(d); slot = V_ / so
            for j in ds:
                cur = core.get(j, 0) * ADJ[d, j]; need = slot - cur
                if need > 0:
                    need = min(need, cash / (1 + 0.003))
                    core[j] = core.get(j, 0) + need / ADJ[d, j]; cash -= need * (1 + 0.003)
                elif need < 0:
                    core[j] += need / ADJ[d, j]; cash += -need * (1 - 0.003)
        eq.append(gtri(d))
    return pd.Series(eq, index=dates[i0:]), nsat


M2 = TT & VCP & PIVOT
VE = {
    'M2 VCP, tt tang, cat10/thung day': lenh(M2 & THI_TRUONG_TANG, hold=250, sl_pct=0.10, exit_flag=THUNG_DAY),
    'M2 VCP, cat7/truot15': lenh(M2, hold=250, sl_pct=0.07, trail_pct=0.15),
    'M1 TT, tt tang, cat10/thung day': lenh(TT & PIVOT & THI_TRUONG_TANG, hold=250, sl_pct=0.10, exit_flag=THUNG_DAY),
    'L1 Livermore, cat7/MA50': lenh(LIV, hold=250, sl_pct=0.07, exit_flag=duoi50),
}
_m = MKT.loc['2017-01-01':]; bao(_m / _m.iloc[0], 'MOC chi so deu trong so')
bao(ghep(VE['M2 VCP, cat7/truot15'].iloc[0:0], S=0)[0], 'LOI rieng: 5 ma it bien dong')
for vn, t in VE.items():
    for S in (1, 2, 3):
        E, ns = ghep(t, S=S); bao(E, f'loi + {S} cho ve tinh {vn}', ns)
