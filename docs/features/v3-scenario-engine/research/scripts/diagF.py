import sys; sys.path.insert(0,sys.argv[1])
exec(open(sys.argv[1]+'/compare.py').read().split('rows=[]')[0])
for kb,(sig,n,hold) in SIG.items():
    t=simulate(sig=sig,hold=hold,exit_level=LV[n],exit_above_entry=True); t=t[t.date>='2023-01-01']
    be=(t.ret.abs()<0.0005).sum(); small=((t.ret>0.0005)&(t.ret<0.01)).sum(); exp=(t.days>=hold-1).sum()
    w=t[t.ret>0.0005]
    print(f"{kb}: n{len(t)} | thang that (>0.05%) {len(w)/len(t)*100:.1f}% | hoa von (~0) {be} | lai 0-1% {small} | lai TB lenh thang {w.ret.mean()*100:.2f}% | het han giu {exp} lenh, TB {t[t.days>=hold-1].ret.mean()*100:.1f}%")
