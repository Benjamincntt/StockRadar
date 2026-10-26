import sys; sys.path.insert(0,sys.argv[1]); from sigs import *
# kiểm tra: bản 'full' của simulate_real phải gần khớp simulate cũ (khác do luật sàn trắng)
sig,n,hold=SIG['KB1']
a=simulate(sig=sig,hold=hold,exit_level=LV[n],exit_above_entry=True); b=simulate_real(sig,hold=hold,exit_level=LV[n],exit_above_entry=True,t2='full')
print('sanity KB1 F: cu',len(a),round(a.ret.mean()*100,3),' moi(full)',len(b),round(b.ret.mean()*100,3))
need=set()
for kb,(sig,n,hold) in SIG.items():
    for kw in (dict(exit_rule=(C>MA[n])),dict(exit_level=LV[n],exit_above_entry=True)):
        for hd in (5,7,10,15):
            t=simulate_real(sig,hold=hd,t2='pm_close',**kw)
            for _,x in t.iterrows():
                d2=dates[dates.get_loc(x.date)+3] if dates.get_loc(x.date)+3<len(dates) else None
                if d2 is not None and d2>=pd.Timestamp('2026-04-28'): need.add((x.sym,str(d2.date())))
pd.DataFrame(sorted(need),columns=['sym','date']).to_csv(sys.argv[1]+'/need_pm.csv',index=False)
print('can nen gio cho',len(need),'cap (ma, ngay T+2);',len({s for s,_ in need}),'ma')
