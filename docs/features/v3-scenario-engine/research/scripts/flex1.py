"""Dò kịch bản tín hiệu dày (nhiều ngày/tháng) cho thị trường bình thường/tăng, luật bán realtime F."""
import sys, pickle
exec(open(sys.argv[1] + '/final3.py', encoding='utf-8').read().split("from sigs import summarize")[0])
from sigs import summarize
pd.set_option('display.width', 250)

LV[5] = ma_touch_level(5)
RET5 = C / C.shift(5) - 1
SECR5 = RET5.where(UNI).T.groupby(sec).transform('median').T
mret3 = mret.rolling(3).sum()
MRSI2 = rsi(MKT, 2)
NORM = (br >= 0.35) & (md50 > -0.05)          # không trùng vùng hoảng loạn của KB1/KB2
months = dates[dates >= '2023-01-01'].to_period('M').nunique()

CANDS = {
    'A1 TT giam >=1% 1 phien, bluechip giam manh nhat': topk(bc(mret <= -0.01) & NORM & (TV > 50) & (C > MA[200]), -R, 5),
    'A2 TT giam >=1.5% (N4 cu)': topk(bc(mret <= -0.015) & NORM & (TV > 50) & (C > MA[200]), -R, 5),
    'A3 TT giam >=2.5% 3 phien, ma tren MA50 RSI2 thap': topk(bc(mret3 <= -0.025) & NORM & (TV > 30) & (C > MA[50]), -RSI2, 5),
    'B1 RSI2 chi so <15, ma RS126>0.6 RSI2 thap': topk(bc(MRSI2 < 15) & NORM & (TV > 30) & (RS126 > 0.6), -RSI2, 5),
    'B2 RSI2 chi so <15, ma RSI2<20 RS126 cao': topk(bc(MRSI2 < 15) & NORM & (TV > 30) & (RSI2 < 20), RS126, 5),
    'C1 nganh giam >=5%/5p, ma yeu hon nganh': topk((SECR5 <= -0.05) & (br >= 0.35) & (RET5 < SECR5) & (TV > 20), -RET5, 5),
    'C2 nganh giam >=4%/5p khi TT tren MA50, ma tren MA50': topk((SECR5 <= -0.04) & (md50 > 0) & (C > MA[50]) & (TV > 20), -RET5, 5),
    'D1 ma manh RS126>0.7 RSI2<10 (moi ngay)': topk((RSI2 < 10) & (RS126 > 0.7) & (TV > 30) & NORM, -RSI2, 5),
}
EXITS = {'F MA5 giu10': FX(5, 10), 'F MA10 giu15': FX(10, 15)}

res = {}
print(f"{'kich ban':58s} {'vao':4s} {'ban':12s}  lenh ngay/thang | thang IS/OOS  Q4  namMin | TB    loTB  pf   te   giu")
for nm, sig in CANDS.items():
    for en in ('atc', 'open'):
        for exn, kw in EXITS.items():
            t = simulate_real2(sig, entry=en, **kw); t = t[t.date >= '2023-01-01']
            if len(t) == 0: continue
            s = summarize(t); dpm = t.date.nunique() / months
            res[(nm, en, exn)] = t
            print(f"{nm:58s} {en:4s} {exn:12s} {s['n']:5d} {dpm:5.1f}     | {s['winIS']:5}/{s['winOOS']:5} {s['winQ4']:5} {s['winMinNam']:5} | {s['avg']:+5.2f} {s['avgLoss']:6} {s['pf']:4} {s['worst']:6} {s['hold']}")
pickle.dump(res, open(sys.argv[1] + '/flex1.pkl', 'wb'))
