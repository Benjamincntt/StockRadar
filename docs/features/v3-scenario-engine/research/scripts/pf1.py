import sys; exec(open(sys.argv[1]+'/pfeng.py',encoding='utf-8').read())
EW=MKT[MKT.index>='2023-01-01']; EW=EW/EW.iloc[0]
metrics(EW,pd.DataFrame({'ret':[0.0]}),'Chi so binh quan deu (mua-giu)')
R0=metrics(*run_pf(BASE,5),'P1 goc, 5 vi the')
for s in (3,8,10): metrics(*run_pf(BASE,s),f'P1, {s} vi the')
import pickle; pickle.dump(R0,open(sys.argv[1]+'/p1_base.pkl','wb'))
T=R0['T']; print('\nLenh lo nang nhat cua P1 goc:'); print(T.nsmallest(12,'ret')[['date','sym','kb','entry','days','ret']].to_string(index=False))
print('\nTheo kich ban:'); print(T.groupby('kb').ret.agg(n='count',win=lambda s:(s>0.0005).mean()*100,avg=lambda s:s.mean()*100,worst=lambda s:s.min()*100).round(1))
