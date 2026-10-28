"""Vòng 2: (a) hiệu ứng lịch dày hơn N3 (thứ trong tuần, vị trí trong tháng); (b) luật bán thời gian / chốt lời cho ứng viên vòng 1."""
import sys, pickle
exec(open(sys.argv[1] + '/flex1.py', encoding='utf-8').read().split("res = {}")[0])

d0 = dates >= '2023-01-01'
m = mret[d0]
wd = pd.Series(dates[d0].weekday, index=dates[d0])
mo_ = pd.Series(dates.to_period('M'), index=dates)
tdm = mo_.groupby(mo_).cumcount()[d0]; left = mo_.groupby(mo_).cumcount(ascending=False)[d0]
print('Chi so binh quan deu: TB loi nhuan ngay (%) theo thu, tung nam')
print((m.groupby([wd, m.index.year]).mean().unstack() * 100).round(3))
print('\nTheo vi tri trong thang (LEFT = so phien con lai, TDM = thu tu phien)')
print((m.groupby(left).mean() * 100).round(3).head(10).to_string())
print((m.groupby(tdm).mean() * 100).round(3).head(8).to_string())
# nửa tháng: phiên giữa tháng có hiệu ứng tương tự đầu tháng không?
mid = (tdm >= 8) & (tdm <= 12)
print(f"\nGiua thang (TDM 8-12): {m[mid].mean()*100:.3f}%  | ngoai cua so dau/cuoi thang: {m[(left > 4) & (tdm > 1)].mean()*100:.3f}%")

print('\n### Luat ban thoi gian / chot loi cho ung vien vong 1')
TIME = {
    'ATO sau 3 phien': dict(hold=3, expire_open=True),
    'ATO sau 5 phien': dict(hold=5, expire_open=True),
    'ATO sau 8 phien': dict(hold=8, expire_open=True),
    'chot +3% realtime, het 8 phien ATO': dict(hold=8, tp=0.03, expire_open=True),
    'chot +5% realtime, het 10 phien ATO': dict(hold=10, tp=0.05, expire_open=True),
}
for nm in ('A1 TT giam >=1% 1 phien, bluechip giam manh nhat', 'A2 TT giam >=1.5% (N4 cu)',
           'C2 nganh giam >=4%/5p khi TT tren MA50, ma tren MA50', 'D1 ma manh RS126>0.7 RSI2<10 (moi ngay)'):
    for en in ('atc', 'open'):
        for exn, kw in TIME.items():
            t = simulate_real2(CANDS[nm], entry=en, **kw); t = t[t.date >= '2023-01-01']
            s = summarize(t); dpm = t.date.nunique() / months
            print(f"{nm[:40]:40s} {en:4s} {exn:36s} {s['n']:4d} {dpm:4.1f} | {s['winIS']:5}/{s['winOOS']:5} Q4 {s['winQ4']:5} min {s['winMinNam']:5} | TB {s['avg']:+5.2f} lo {s['avgLoss']:6} pf {s['pf']:4} te {s['worst']:6}")
