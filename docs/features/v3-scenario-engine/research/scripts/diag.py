import sys; sys.path.insert(0,sys.argv[1])
exec(open(sys.argv[1]+'/compare.py').read().split('rows=[]')[0])
for kb in ('KB1','KB3','KB4'):
    sig,n,hold=SIG[kb]
    t=simulate(sig=sig,hold=hold,exit_level=LV[n],sl=0.07); t=t[t.date>='2023-01-01']
    deep=t[t.ret< -0.07-COST-0.01]
    print(kb,'SL7: lenh lo vuot -8.4%:',len(deep),'/',len(t),'; trong do thoat dung phien dau duoc ban (T+2.5):',int((deep.days==3).sum()))
    b=simulate(sig=sig,hold=hold,exit_level=LV[n]); b=b[b.date>='2023-01-01']
    a=simulate(sig=sig,hold=hold,exit_rule=(C>MA[n])); a=a[a.date>='2023-01-01']
    lossB=b[b.ret<=0]; idx=[dates.get_loc(d) for d in lossB.date]
    print(f'   realtime: {len(lossB)} lenh lo; ban do cham MA (khong phai het han): {(lossB.days<hold).sum()} ; trong do gia ban < gia mua: {(lossB.exit<lossB.entry).sum()}')
    m=a.merge(b,on=['date','sym'],suffixes=('_A','_B'))
    print(f'   cung lenh: dong cua thang nhung realtime thua: {((m.ret_A>0)&(m.ret_B<=0)).sum()} ; nguoc lai: {((m.ret_A<=0)&(m.ret_B>0)).sum()}')
