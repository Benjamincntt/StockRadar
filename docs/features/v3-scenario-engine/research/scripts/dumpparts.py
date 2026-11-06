"""Dựng bảng lệnh gốc V3 (luật production P1++ realtime + PH-21) và lưu ra parts.pkl cho catlo*.py."""
import sys, time, pickle
t0 = time.time()
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])
print('dung xong sau', round(time.time() - t0), 'giay; ngay cuoi', dates[-1].date())
for kb, t in PARTS:
    print(kb, len(t), 'TB %+.2f%%' % (t.ret.mean() * 100), 'cot', list(t.columns))
E, T, sk = run_kb4(PARTS, 40_000, 'gan', 1.0)
print('nen: tong %+.0f%% sut %.1f%%' % ((E.iloc[-1] - 1) * 100, (E / E.cummax() - 1).min() * 100))
pickle.dump(PARTS, open(sys.argv[1] + '/parts.pkl', 'wb'))
