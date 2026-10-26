from bt import *
PM={}   # (sym, date) -> (open, high, low, close) buổi chiều, đã quy về thang giá điều chỉnh

def floor_locked(k,j,o,h,l,c,lim):
    """Sàn trắng bên mua: cả phiên một giá, đóng ở giá sàn -> không bán được."""
    return k>0 and h[k,j]==l[k,j] and c[k,j]<=c[k-1,j]*(1-lim[j]+0.005)

def simulate_real(sig, hold=10, sl=None, exit_rule=None, exit_level=None, exit_above_entry=False, t2="pm_close", tp=None):
    """Mua giá mở cửa t+1. Phiên T+2 (t+3) chỉ bán buổi chiều: 'pm_close' = chỉ xét giá đóng cửa,
    'pm_bars' = dùng nến giờ buổi chiều nếu có, 'skip' = sớm nhất t+4, 'full' = cả phiên (bản cũ).
    Sàn trắng bên mua thì không bán được, kể cả phiên hết hạn giữ."""
    sig=(sig&UNI).fillna(False).values
    o,h,l,c=O.values,H.values,L.values,C.values; bad=BAD.values; lim=LIM.values
    ex=exit_rule.fillna(False).values if exit_rule is not None else None
    lvl=exit_level.values if exit_level is not None else None
    mk=MKT.values; out=[]; n=len(dates)
    for j in range(len(syms)):
        busy=-1
        for i in np.nonzero(sig[:,j])[0]:
            if i<=busy or i+3>=n: continue
            e=i+1
            if np.isnan(o[e,j]) or o[e,j]>=c[i,j]*(1+lim[j]-0.005): continue
            ent=o[e,j]; stop=ent*(1-sl) if sl else None
            first=i+4 if t2=='skip' else i+3
            last=i+hold
            if last>=n: continue
            px=None; k=e; skipped=False
            while k<n-1 or k==n-1:
                if bad[k,j]: skipped=True; break
                if k>=first and not np.isnan(c[k,j]) and not floor_locked(k,j,o,h,l,c,lim):
                    lv=None
                    if tp is not None: lv=ent*(1+tp)
                    if lvl is not None and not np.isnan(lvl[k,j]):
                        lv=max(lvl[k,j],ent*(1+COST)) if exit_above_entry else lvl[k,j]
                    pm_day=(k==i+3 and t2 in ('pm_close','pm_bars'))
                    bar=PM.get((syms[j],dates[k])) if (pm_day and t2=='pm_bars') else None
                    if pm_day and bar is None:
                        if stop is not None and c[k,j]<=stop: px=c[k,j]; break
                        if lv is not None and c[k,j]>=lv: px=c[k,j]; break
                    else:
                        bo,bh,bl,bc_=bar if bar is not None else (o[k,j],h[k,j],l[k,j],c[k,j])
                        if stop is not None and bl<=stop: px=min(bo,stop); break
                        if lv is not None and bh>=lv: px=max(bo,lv); break
                    if ex is not None and ex[k,j]: px=c[k,j]; break
                    if k>=last: px=c[k,j]; break
                k+=1
                if k>=n: break
            if skipped: busy=k; continue
            if px is None: break
            ret=px/ent-1-COST
            out.append((dates[i],syms[j],ent,px,k-e+1,ret,ret-(mk[k]/mk[i]-1)))
            busy=k
    return pd.DataFrame(out,columns=['date','sym','entry','exit','days','ret','excess'])
