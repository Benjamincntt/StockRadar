import sys; sys.path.insert(0,sys.argv[1]); from sigs import *; from real2 import simulate_real2
sig,n,hd=SIG['KB1']
a=simulate_real(sig,hold=hd,exit_level=LV[n],exit_above_entry=True,t2='pm_close'); b=simulate_real2(sig,hold=hd,exit_level=LV[n],exit_above_entry=True)
print('KB1 F cu',len(a),round(a.ret.mean()*100,3),' moi',len(b),round(b.ret.mean()*100,3))
sig,n,hd=SIG['KB4']
a=simulate_real(sig,hold=10,exit_rule=(C>MA[5]),t2='pm_close'); b=simulate_real2(sig,hold=10,exit_rule=(C>MA[5]))
print('KB4 A cu',len(a),round(a.ret.mean()*100,3),' moi',len(b),round(b.ret.mean()*100,3))
