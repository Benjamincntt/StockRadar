"""Vòng 3: kịch bản theo tuần (hiệu ứng thứ trong tuần). Mua ATC ngày X, bán theo lịch sau T+2.5."""
import sys, pickle
exec(open(sys.argv[1] + '/flex1.py', encoding='utf-8').read().split("res = {}")[0])

d0 = dates >= '2023-01-01'
WD = bc(pd.Series(dates.weekday, index=dates).astype(float))
# tách qua đêm / trong phiên theo thứ (chỉ số bình quân đều)
on = (O / C.shift() - 1).where(UNI.shift()).clip(-.16, .16).mean(axis=1)
intra = (C / O - 1).where(UNI.shift()).clip(-.16, .16).mean(axis=1)
w = pd.Series(dates.weekday, index=dates)
tb = pd.DataFrame({'qua dem': on[d0].groupby(w[d0]).mean(), 'trong phien': intra[d0].groupby(w[d0]).mean(),
                   'ca ngay': mret[d0].groupby(w[d0]).mean()}) * 100
print('TB %/ngay theo thu (0=T2):'); print(tb.round(3))
for per, msk in (('trong mau', d0 & (dates <= IS[1])), ('ngoai mau', dates >= OOS[0])):
    print(per, (mret[msk].groupby(w[msk]).mean() * 100).round(3).to_dict())

beta = R.rolling(60).cov(bc(mret)) / bc(mret.rolling(60).var())
SEL = {
    'top5 thanh khoan': (TV > 0, TV),
    'top5 beta cao (TV>50)': (TV > 50, beta),
    'top5 giam 5p manh nhat (TV>50)': (TV > 50, -RET5),
    'top5 RS63 cao (TV>30)': (TV > 30, RS63),
}
PLAN = {   # (thứ mua ATC, số phiên giữ, bán ATO?)
    'mua ATC T2, ban ATC T4': (0, 2, False),
    'mua ATC T2, ban ATO T5': (0, 3, True),
    'mua ATC T6, ban ATC T3': (4, 2, False),
    'mua ATC T3, ban ATO T6': (1, 3, True),
}
for gate_nm, gate in (('moi tuan', bc(mret == mret) ), ('khi TT khong hoang loan', NORM)):
    print(f'\n### {gate_nm}')
    for pn, (wd_, hd, ato) in PLAN.items():
        for sn, (cond, sc) in SEL.items():
            sig = topk((WD == wd_) & cond & gate, sc, 5)
            t = simulate_real2(sig, entry='atc', hold=hd, expire_open=ato); t = t[t.date >= '2023-01-01']
            s = summarize(t); yr = t.groupby(t.date.dt.year).ret.mean() * 100
            print(f"{pn:24s} {sn:32s} n{s['n']:4d} | thang {s['winIS']:5}/{s['winOOS']:5} Q4 {s['winQ4']:5} | TB {s['avg']:+5.2f} pf {s['pf']:4} te {s['worst']:6} | TB nam " + ' '.join(f'{v:+.2f}' for v in yr))
