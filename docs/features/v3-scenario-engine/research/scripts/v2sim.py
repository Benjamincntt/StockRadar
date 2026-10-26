"""Mô phỏng luật thật của 3 kịch bản mua V2 (KichBanNoHuongLen/HoiHoTro/QuetThanhKhoan + luật bán Pha 2) trên nến ngày."""
from real import *
c_=C.values; h_=H.values; l_=L.values; o_=O.values; v_=V.values

def ema_np(x,n): return x.ewm(span=n,adjust=False,min_periods=n).mean()
E20,E50,E12,E26=ema_np(C,20),ema_np(C,50),ema_np(C,12),ema_np(C,26)
MACD=E12-E26; SIGL=MACD.ewm(span=9,adjust=False).mean(); HIST=MACD-SIGL
d=C.diff()
RSI_S=100-100/(1+d.clip(lower=0).rolling(14).sum()/(-d.clip(upper=0)).rolling(14).sum())   # RSI cộng dồn 14 phiên như IndicatorMath.Rsi
TRr=pd.concat([H-L,(H-C.shift()).abs(),(L-C.shift()).abs()]).groupby(level=0).max()
ATRs=TRr.rolling(14).mean()                                                                   # ATR trung bình đơn như AtrAt
up=H.diff(); dn=-L.diff()
pdm=up.where((up>dn)&(up>0),0.0); mdm=dn.where((dn>up)&(dn>0),0.0)
sp=pdm.ewm(alpha=1/14,adjust=False).mean(); sm=mdm.ewm(alpha=1/14,adjust=False).mean(); st=TRr.ewm(alpha=1/14,adjust=False).mean()
pdi=100*sp/st; mdi=100*sm/st; DX=100*(pdi-mdi).abs()/(pdi+mdi); ADX=DX.ewm(alpha=1/14,adjust=False).mean()
BBW=4*C.rolling(20).std(ddof=0)/C.rolling(20).mean()                                          # (upper-lower)/mid
VOL20=V.rolling(20).mean(); VOL5=V.rolling(5).mean(); VOL3=V.rolling(3).mean()
NBARS=C.notna().cumsum()
SOTUYEN=(TV>=10)&(NBARS>=250)
HI20=H.rolling(20).max(); LO20=L.rolling(20).min(); LO5=L.rolling(5).min()
DAYNEN=L.rolling(20).min().shift(5)

# Pha 1: đánh giá trên nến tới hôm qua -> FORMING cho hôm nay
F_HHT=((E20>E50)&((C-E20).abs()/E20<=0.03)&(RSI_S>=40)&(RSI_S<=50)&(VOL3/VOL20<0.8)&SOTUYEN).shift(1,fill_value=False)
F_NHL=((E20>E50)&(ADX>20)&(BBW<0.05)&(VOL5/VOL20<0.7)&SOTUYEN).shift(1,fill_value=False)
F_QTK=(((E20>E50)|(BBW<0.05))&(C>E50)&(L<DAYNEN)&SOTUYEN).shift(1,fill_value=False)
k12,k26,k9,k20,k50=2/13,2/27,2/10,2/21,2/51


def hist_at(i,j,p):
    """MACD histogram khi nến hôm nay (chỉ số i) đang dở dang ở giá p."""
    f=E12.values[i-1,j]*(1-k12)+k12*p; s=E26.values[i-1,j]*(1-k26)+k26*p; m=f-s
    return m-(SIGL.values[i-1,j]*(1-k9)+k9*m)


def rsi_at(i,j,p):
    """RSI cộng dồn 14 phiên khi nến hôm nay đang ở giá p."""
    ch=np.append(np.diff(c_[i-14:i,j]),p-c_[i-1,j])
    g=ch[ch>0].sum(); lo=-ch[ch<0].sum()
    return 100.0 if lo==0 else 100-100/(1+g/lo)


def triggers(mode='mid'):
    """Cò kích hoạt Pha 2 cho các mã FORMING.
    mode='close': kịch bản cần KL chỉ tính kích hoạt khi giá đóng cửa đạt mức (bi quan);
    mode='mid': giá trong phiên chạm mức là tính, giá khớp = max(mở cửa, mức).
    Trả list (i, j, loại, giá khớp, SL, TP1, TP2, mức điểm 2, băng điểm 2, khoảng cách đuổi)."""
    out=[]
    for name,F in (('HoiHoTro',F_HHT),('NoHuongLen',F_NHL),('QuetThanhKhoan',F_QTK)):
        ii,jj=np.nonzero(F.values)
        for i,j in zip(ii,jj):
            if i<30 or np.isnan(o_[i,j]) or v_[i,j]<=0: continue
            atr=ATRs.values[i-1,j]; o,hh,ll,cc=o_[i,j],h_[i,j],l_[i,j],c_[i,j]
            if name=='HoiHoTro':
                A=E20.values[i-1,j]*(1-k20)
                lo_=A/(1-k20)*1.0000001; hi_=1.005*A/(1-1.005*k20)
                if lo_<o<=hi_: p=o
                elif o>hi_ and ll<=hi_: p=hi_
                elif o<=lo_ and hh>lo_: p=min(lo_*1.001,hi_)
                else: continue
                if not (rsi_at(i,j,p)>RSI_S.values[i-1,j] and hist_at(i,j,p)>HIST.values[i-1,j]): continue
                e20p=A+k20*p; e50p=E50.values[i-1,j]*(1-k50)+k50*p
                zmin,zmax=e20p*0.995,e20p*1.005
                tp1=max(HI20.values[i-1,j],max(o,p)); sl=e50p-0.5*atr; tp2=tp1+(tp1-zmin)*0.5
                if not (tp1>zmax and sl<zmin): continue
                d2=p+atr; d2=None if d2>=tp1 else d2
                out.append((i,j,name,p,sl,tp1,tp2,d2,0.3*atr,None))
                continue
            thr=(1.5 if name=='NoHuongLen' else 2.0)
            sum19=np.nansum(v_[i-19:i,j])
            if not v_[i,j]*(1-thr/20)>thr/20*sum19: continue        # KL cả phiên > thr × TB20 (TB20 gồm cả phiên hôm nay)
            if name=='NoHuongLen':
                lvl=HI20.values[i-1,j]
            else:
                lvl=np.nanmin(l_[i-24:i-4,j])*1.01
            if not hh>lvl: continue
            if mode=='close' and not cc>lvl: continue
            p=cc if (mode=='close' or cc>lvl) else max(o,lvl*1.001)
            if name=='NoHuongLen':
                if not hist_at(i,j,p)>HIST.values[i-1,j]: continue
                box_lo=LO20.values[i-1,j]; hgt=lvl-box_lo
                sl=box_lo-0.5*atr; tp1=lvl+hgt; tp2=lvl+1.5*hgt
                zmin,zmax=p,p+0.3*atr
                d2=lvl+0.5*hgt
            else:
                tp1=max(HI20.values[i-1,j],max(o,p)); sl=min(LO5.values[i-1,j],ll)-0.3*atr
                zmin,zmax=p*0.997,p*1.003
                tp2=tp1+(tp1-zmin)*0.5; d2=p+atr
            if not (tp1>zmax and sl<zmin): continue
            d2=None if d2>=tp1 else d2
            out.append((i,j,name,p,sl,tp1,tp2,d2,0.3*atr,2.5*atr))
    return out


def sell_v2(trig, use_diem2=True, optimistic=False):
    """Luật bán Pha 2 trên nến ngày.
    SL hiệu lực = max(SL kế hoạch, giá vào điểm 1 sau CL1, đỉnh − 2.5·ATR với kịch bản có đuổi);
    CL1 bán nửa + dời SL; CL2 bán hết; ≥5 phiên mà MFE < 3% bán hết; rút mốc 4% bán nửa, 6% bán hết;
    > 20 phiên bán hết; < 3 phiên chưa bán được. Trong phiên xét chiều giảm trước (bi quan)."""
    n=len(dates); res=[]; busy={}
    for (i,j,name,p,sl,tp1,tp2,d2,band,trail) in sorted(trig,key=lambda x:(x[0],x[1])):
        if busy.get(j,-1)>=i: continue
        lots=[p]; frac=1.0; proceeds=0.0; half=False; moved=False; got2=False
        peak=p; mfe=0.0; k=i; exit_k=None; why=None
        while True:
            k+=1
            if k>=n: break
            o,hh,ll,cc=o_[k,j],h_[k,j],l_[k,j],c_[k,j]
            if np.isnan(cc): continue
            sess=k-i; units=len(lots)
            if sess>20:
                proceeds+=frac*units*o; frac=0; exit_k=k; why='HetHan'; break
            evt=None
            if sess>=3:
                cand=[sl]+([lots[0]] if moved else [])+([peak-trail] if trail is not None else [])
                s=max(cand); moc=max(peak,lots[0])
                if o<=s: evt=('all',o,'DungLo')
                elif (not half) and sess>=5 and mfe<3: evt=('all',o,'HetThoiGian')
                elif o<=moc*0.94: evt=('all',o,'RutMoc6')
                elif (not half) and o<=moc*0.96: evt=('half',o,'RutMoc4')
                elif half and not got2 and o>=tp2: evt=('all',o,'ChotLoi2')
                elif (not half) and not got2 and o>=tp1: evt=('half',o,'ChotLoi1')
                if evt is None and optimistic:
                    if half and not got2 and hh>=tp2: evt=('all',tp2,'ChotLoi2')
                    elif (not half) and not got2 and hh>=tp1: evt=('half',tp1,'ChotLoi1')
                if evt is None:
                    if ll<=s: evt=('all',s,'DungLo')
                    elif ll<=moc*0.94: evt=('all',moc*0.94,'RutMoc6')
                    elif (not half) and ll<=moc*0.96: evt=('half',moc*0.96,'RutMoc4')
                    elif half and not got2 and hh>=tp2: evt=('all',tp2,'ChotLoi2')
                    elif (not half) and not got2 and hh>=tp1: evt=('half',tp1,'ChotLoi1')
                if evt:
                    kind,px,why=evt
                    if kind=='all':
                        proceeds+=frac*units*px; frac=0; exit_k=k; break
                    proceeds+=frac*0.5*units*px; frac*=0.5; half=True
                    if why=='ChotLoi1': moved=True
            if use_diem2 and evt is None and d2 is not None and not half and not got2 and ll<=d2+band and hh>=d2:
                lots.append(o if d2<=o<=d2+band else d2); got2=True
            peak=max(peak,hh); avg=sum(lots)/len(lots); mfe=max(mfe,(peak-avg)/avg*100)
        if exit_k is None: continue
        cost=sum(lots)
        ret=proceeds/cost-1-COST
        res.append((dates[i],syms[j],name,p,exit_k-i,ret,why,got2))
        busy[j]=exit_k
    return pd.DataFrame(res,columns=['date','sym','kb','entry','days','ret','why','diem2'])

# --- Pha 1 khi lịch sử lúc 08:30 đã có nến hôm nay giữ chỗ (O=H=L=C=giá tham chiếu, KL=0) — khớp dữ liệu thật tốt hơn ---
def pha1_placeholder():
    Cp=C.shift(1)
    E20p=E20.shift(1)*(1-k20)+k20*Cp; E50p=E50.shift(1)*(1-k50)+k50*Cp
    dd=C.diff().shift(1)
    RSIp=100-100/(1+dd.clip(lower=0).rolling(13).sum()/(-dd.clip(upper=0)).rolling(13).sum())
    V3p=(V.shift(1)+V.shift(2))/3; V5p=V.shift(1).rolling(4).sum()/5; V20p=V.shift(1).rolling(19).sum()/20
    sh=lambda x: x.shift(1)
    # Bollinger 20 với nến giữ chỗ: 19 giá đóng cửa cũ + giá tham chiếu
    win=pd.concat([C.shift(1).rolling(19).sum()+Cp],axis=0)
    s1=C.shift(1).rolling(19).sum()+Cp; s2=(C.shift(1)**2).rolling(19).sum()+Cp**2
    mean=s1/20; std=np.sqrt((s2/20-mean**2).clip(lower=0)); BBWp=4*std/mean
    # ADX: nến giữ chỗ có biên độ 0 -> làm mượt thêm một bước với DM=0, TR=|C-Cprev|=0
    spp=sp.shift(1)*(1-1/14); smp=sm.shift(1)*(1-1/14); stp=st.shift(1)*(1-1/14)
    pdi_=100*spp/stp; mdi_=100*smp/stp; DXp=100*(pdi_-mdi_).abs()/(pdi_+mdi_); ADXp=ADX.shift(1)*(1-1/14)+DXp/14
    DAYNENp=L.rolling(20).min().shift(4)        # cửa sổ lùi theo nến giữ chỗ
    sot=SOTUYEN.shift(1,fill_value=False)
    FH=(E20p>E50p)&((Cp-E20p).abs()/E20p<=0.03)&(RSIp>=40)&(RSIp<=50)&(V3p/V20p<0.8)&sot
    FN=(E20p>E50p)&(ADXp>20)&(BBWp<0.05)&(V5p/V20p<0.7)&sot
    FQ=((E20p>E50p)|(BBWp<0.05))&(Cp>E50p)&(Cp<DAYNENp)&sot
    return FH.fillna(False),FN.fillna(False),FQ.fillna(False)
