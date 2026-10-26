import sys; sys.path.insert(0,sys.argv[1])
exec(open(sys.argv[1]+'/newkb.py').read().split("print('### N1")[0])
print('### N3 + chot loi realtime (van ban het phien 2 thang sau neu chua cham)')
s3=(LEFT==5)&(TV>30)
for tp in (None,0.03,0.04,0.05):
    run(f'N3 top5 RS63 tp={tp}',topk(s3,RS63,5),hold=12,exit_rule=(TDM==1),tp=tp)
for nm,cond in (('RS63 + ATR thap (<3.5%)',ATR/C<0.035),('RS63 + tren MA20',C>MA[20]),('RS63 + vol 5p < TB20',ma(V,5)<VMA20)):
    run(f'N3 {nm} tp=0.04',topk(s3&cond,RS63,5),hold=12,exit_rule=(TDM==1),tp=0.04)
print('\n### N1 do nhay')
for sr in (-0.10,-0.12,-0.15):
    for lf in (4,6,8):
        s=(FLOOR20>=1)&~FLOOR&(SECR20<=sr)&(TV>10)&(LEFT<=lf)
        run(f'N1 nganh<={sr} con<={lf} phien | A',topk(s,-SECR20,5),**A10())
s=(FLOOR20>=1)&~FLOOR&(SECR20<=-0.12)&(TV>10)
run('N1 nganh<=-12% KHONG loc cuoi thang | A',topk(s,-SECR20,5),**A10())
