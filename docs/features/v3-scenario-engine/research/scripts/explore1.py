import sys; sys.path.insert(0,sys.argv[1]); from bt import *
pd.set_option('display.width',200)
up=bc(MKT_UP)
cands={
 # 1 Connors RSI2 trong xu hướng dài hạn
 'rsi2_mr': dict(sig=(C>MA[200])&(RSI2<10), hold=10, exit_rule=(C>MA[5])),
 'rsi2_mr_mkt': dict(sig=(C>MA[200])&(RSI2<10)&up, hold=10, exit_rule=(C>MA[5])),
 # 2 pullback về MA20 trong trend mạnh, RS cao
 'pb_ma20_rs': dict(sig=(MA[20]>MA[50])&(MA[50]>MA[100])&(RS126>0.8)&(L<=MA[20]*1.01)&(C>MA[20])&(C>C.shift())&(V<VMA20), hold=15, sl_atr=2, trail_atr=3),
 # 3 gần đỉnh 52 tuần + RS + thị trường tăng
 'near52_rs': dict(sig=(C>=HI250.shift()*0.97)&(RS63>0.85)&up&(V>1.3*VMA20), hold=20, sl_atr=2, trail_atr=3),
 # 4 đầu hàng toàn thị trường -> bắt hồi
 'capit': dict(sig=bc((MKT/MKT.shift(5)-1)<-0.06)&(C/C.shift(5)-1<-0.10)&(C>MA[200]*0.9), hold=10, exit_rule=(C>MA[10])),
 # 5 co hẹp biên độ rồi bứt (NR + RS)
 'vcp': dict(sig=((H.rolling(10).max()/L.rolling(10).min()-1).shift()<0.08)&(C>H.rolling(10).max().shift())&(V>1.5*VMA20)&(RS126>0.7)&(C>MA[50]), hold=20, sl_atr=1.5, trail_atr=3),
 # 6 V1/V2 kiểu breakout đơn giản để so
 'brk_simple': dict(sig=(C>HI60.shift())&(V>1.5*VMA20), hold=10, sl_atr=2),
 # 7 mua ngẫu nhiên mọi mã trong vũ trụ (đường chuẩn)
 'all_10d': dict(sig=UNI&(np.arange(len(dates))[:,None]%10==0), hold=10),
}
for k,v in cands.items():
    t=simulate(**v,name=k); print('\n=== ',k); print(report(t).to_string())
