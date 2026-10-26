import sys; sys.path.insert(0,sys.argv[1]); import v2sim as m
from sigs import summarize
pd=m.pd; np=m.np
FH,FN,FQ=m.pha1_placeholder(); m.F_HHT=FH; m.F_NHL=FN      # QTK giữ bản "tới hôm qua" (khớp thật 7/10)
real=pd.read_csv(sys.argv[1]+'/prod_kq.txt',sep='|',skiprows=[1]); real=real[real.LoaiKichBan.isin([1,2,3])]
real['d']=(pd.to_datetime(real.TG)+pd.Timedelta(hours=7)).dt.normalize(); rw=real[real.d<='2026-10-22']
rs=set(zip(rw.Symbol,rw.d))
OUT={}
for mode in ('mid','close'):
    tr=m.triggers(mode)
    T=pd.DataFrame([(m.dates[i],m.syms[j],nm) for (i,j,nm,*_) in tr],columns=['d','sym','kb'])
    w=T[(T.d>='2026-09-30')&(T.d<='2026-10-22')]; ss=set(zip(w.sym,w.d))
    near=sum(1 for (s,d) in rs if any((s,d+pd.Timedelta(days=x)) in ss for x in (-3,-2,-1,0,1,2,3)))
    print(f"[{mode}] cò mô phỏng 30/09-22/10: {len(ss)} ; lệnh thật {len(rs)} ; trùng đúng ngày {len(rs&ss)} ; trùng ±3 ngày {near}")
    for d2 in (True,False):
        t=m.sell_v2(tr,use_diem2=d2); OUT[(mode,d2)]=t
        print(f"\n== Cò '{mode}', mua điểm 2={d2}")
        for kb in ('HoiHoTro','NoHuongLen','QuetThanhKhoan','TAT CA'):
            x=t if kb=='TAT CA' else t[t.kb==kb]
            if len(x[x.date>='2023-01-01'])==0: print(kb,'0 lệnh'); continue
            s=summarize(x)
            print(f"{kb:15s} n{s['n']:4d} wIS{s['winIS']:5} wOOS{s['winOOS']:5} wQ4{s['winQ4']:5} minY{s['winMinNam']:5} avg{s['avg']:+6.2f} loss{s['avgLoss']:7} pf{s['pf']:5} worst{s['worst']:6} hold{s['hold']}")
        x=t[t.date>='2023-01-01']; print('   lý do thoát:',(x.why.value_counts(normalize=True)*100).round(0).to_dict())
import pickle; pickle.dump(OUT,open(sys.argv[1]+'/v2_trades.pkl','wb'))
