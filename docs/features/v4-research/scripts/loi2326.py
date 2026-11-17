import sys, warnings; warnings.filterwarnings('ignore')
exec(open(sys.argv[1] + '/v4xv.py', encoding='utf-8').read().split("mom = (C.shift(21)")[0])
Rl = np.log1p(R.where(~BAD)); VB = Rl.rolling(120, min_periods=72).std(); xh = (C > MA[200]) & (MA[50] > MA[200])
E = chay(Quy(TOP100 & xh, -VB))
b = E[E.index >= '2023-01-01']; b = b / b.iloc[0]
print('loi 23-26: tong %+.0f%% sut %.1f%%' % ((b.iloc[-1] - 1) * 100, (b / b.cummax() - 1).min() * 100))
a = E[E.index <= '2022-12-31']; print('loi 17-22: tong %+.0f%% sut %.1f%%' % ((a.iloc[-1] / a.iloc[0] - 1) * 100, (a / a.cummax() - 1).min() * 100))
