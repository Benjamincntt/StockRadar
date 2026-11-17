"""V4 vòng 1: quét 9 họ tín hiệu × 7 kiểu thoát, ghi bảng lệnh của từng cấu hình ra quet.pkl."""
import sys, time, pickle
exec(open(sys.argv[1] + '/v4core.py', encoding='utf-8').read())
HI55 = C.rolling(55).max(); HI20 = H.rolling(20).max().shift(1)
atrp = ATR / C; SQ = atrp.rolling(120, min_periods=60).rank(pct=True)
PHA_B = bc(PHA)
HO = {
    'F1 Pha dinh 55 phien + KL': (C >= HI55) & (V > 1.5 * VMA20) & (C > MA[50]),
    'F2 Nhip chinh trong xu huong (RSI2<10, tren MA200)': (RSI2 < 10) & (C > MA[200]) & (MA[50] > MA[200]),
    'F3 Qua ban sau (RSI2<5, duoi MA50)': (RSI2 < 5) & (C < MA[50]),
    'F4 Hoang loan: ma manh bi ban (RS63>=0.7, RSI2<15)': (PHA_B == 0) & (RS63 >= 0.7) & (RSI2 < 15),
    'F5 Dong luc: RS126 top 10% cat len MA20': (RS126 >= 0.9) & (C > MA[20]) & (C.shift() <= MA[20].shift()),
    'F6 Nen chat roi pha dinh 20': (SQ.shift(1) <= 0.2) & (C > HI20),
    'F7 Phien bung no (>=4%, KL x2.5, dong cua gan dinh)': (R >= 0.04) & (V > 2.5 * VMA20) & (C >= H - 0.2 * (H - L)),
    'F8 Tro lai xu huong: cat len MA20, MA20>MA50 tang': (C > MA[20]) & (C.shift() <= MA[20].shift()) & (MA[20] > MA[50]) & (MA[50] > MA[50].shift(5)),
    'F9 Sat dinh 1 nam (>=97% dinh 250 phien), KL tang': (C >= 0.97 * H.rolling(250, min_periods=200).max()) & (V > 1.2 * VMA20) & (R > 0),
}
duoi10 = C < MA[10]; tren5 = C > MA[5]
THOAT = {
    'T1 giu 10 phien': dict(hold=10),
    'T2 cat 2ATR / chot 3ATR / 15p': dict(hold=15, sl=2, tp=3),
    'T3 cat 1.5ATR / chot 2ATR / 10p': dict(hold=10, sl=1.5, tp=2),
    'T4 truot 3ATR / 30p': dict(hold=30, trail=3),
    'T5 truot 2ATR / 20p': dict(hold=20, trail=2),
    'T6 dong cua duoi MA10 / cat 2.5ATR / 30p': dict(hold=30, sl=2.5, exit_flag=duoi10),
    'T7 dong cua tren MA5 / cat 3ATR / 10p': dict(hold=10, sl=3, exit_flag=tren5),
}
KQ = {}
for hn, sig in HO.items():
    for tn, kw in THOAT.items():
        t0 = time.time(); t = mo_phong(sig, **kw); KQ[(hn, tn)] = t
        a, b = chia(t); sa, sb = tom_tat(a), tom_tat(b)
        print(f"{hn[:2]} {tn[:2]} | IS n{sa['n']:5d} thang {sa['thang']*100:4.0f}% TB {sa['tb']*100:+5.2f}% pf {sa['pf']:4.2f} /ngay {sa['tbngay']*100:+.3f}% "
              f"| OOS n{sb['n']:5d} thang {sb['thang']*100:4.0f}% TB {sb['tb']*100:+5.2f}% pf {sb['pf']:4.2f} /ngay {sb['tbngay']*100:+.3f}% | {time.time()-t0:4.0f}s", flush=True)
pickle.dump(dict(KQ=KQ, ho=list(HO), thoat=list(THOAT)), open(sys.argv[1] + '/quet.pkl', 'wb'))
print('xong')
