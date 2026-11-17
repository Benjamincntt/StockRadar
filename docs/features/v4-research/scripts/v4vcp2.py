"""V4 vòng 11: VCP bản nới làm vệ tinh cho lõi ít biến động. Kèm phép bỏ năm có siêu cổ phiếu (2021, 2025)."""
import sys, warnings
warnings.filterwarnings('ignore')
exec(open(sys.argv[1] + '/v4ghep2.py', encoding='utf-8').read().split("M2 = TT & VCP & PIVOT")[0])
ATRr = ATR10 / ATR50; VDRY = ma(V, 10) < VMA50
TT_NOI = (C > MA[50]) & (MA[50] > MA[200]) & (C >= 0.75 * HI250)
KW = dict(hold=250, sl_pct=0.10, exit_flag=THUNG_DAY)
def vcp(tt, rg, ar=0.75, kho=True, pw=20):
    v = ((RANGE20 <= rg) & (ATRr <= ar) & (VDRY if kho else True)).shift(1).fillna(False)
    return tt & v & (C > H.rolling(pw).max().shift(1)) & (V > 1.5 * VMA50) & THI_TRUONG_TANG
def bo(E, ys):
    nam = E.groupby(E.index.year).last() / E.groupby(E.index.year).first(); return (nam.drop(ys).prod() - 1) * 100
loi = ghep(lenh(vcp(TT, .15), **KW).iloc[0:0], S=0)[0]
bao(loi, 'LOI rieng')
print(f"{'':58s} | bo 2021 {bo(loi, [2021]):+.0f}% | bo 2021+2025 {bo(loi, [2021, 2025]):+.0f}%")
for ten, sig in (('VCP goc (TT day du, nen 15%)', vcp(TT, .15)), ('VCP noi (TT noi, nen 15%)', vcp(TT_NOI, .15)),
                 ('VCP noi (TT noi, nen 20%)', vcp(TT_NOI, .20)), ('VCP noi rong (TT noi, nen 20%, khong can KL can)', vcp(TT_NOI, .20, .85, False))):
    t = lenh(sig, **KW); t = t[t.date >= '2017-01-01']
    print(f"\n{ten}: {len(t)} lenh, {len(t)/9.9:.0f}/nam, TB {t.ret.mean()*100:+.1f}%, trung vi {t.ret.median()*100:+.1f}%, thang {(t.ret>.0005).mean()*100:.0f}%")
    for S in (1, 2, 3):
        E, ns = ghep(t, S=S); bao(E, f'  loi + {S} cho ve tinh', ns)
        print(f"{'':58s} | bo 2021 {bo(E, [2021]):+.0f}% | bo 2021+2025 {bo(E, [2021, 2025]):+.0f}%")
