from real import *

def simulate_real2(sig, hold=10, sl=None, tp=None, exit_rule=None, exit_level=None, exit_above_entry=False,
                   entry='open', exit_open_rule=None, expire_open=False, stop_close_atr=None, stop_close_pct=None, atr_ref=None):
    """Như simulate_real, thêm:
    entry='open' mua giá mở cửa phiên sau; 'atc' mua giá đóng cửa ngay phiên tín hiệu (bỏ nếu đóng cửa kịch trần).
    exit_open_rule: bán ATO (giá mở cửa) ở phiên mà điều kiện đúng. expire_open: hết hạn giữ thì bán ATO thay vì ATC.
    Phiên T+2 chỉ bán buổi chiều (xét theo giá đóng cửa); từ T+3 bán được cả phiên. Sàn trắng không bán được."""
    sig=(sig&UNI).fillna(False).values
    o,h,l,c=O.values,H.values,L.values,C.values; bad=BAD.values; lim=LIM.values
    ex=exit_rule.fillna(False).values if exit_rule is not None else None
    exo=exit_open_rule.fillna(False).values if exit_open_rule is not None else None
    lvl=exit_level.values if exit_level is not None else None
    out=[]; n=len(dates)
    for j in range(len(syms)):
        busy=-1
        for i in np.nonzero(sig[:,j])[0]:
            if i<=busy or i<1: continue
            if entry=='atc':
                e=i; ent=c[i,j]
                if np.isnan(ent) or c[i,j]>=c[i-1,j]*(1+lim[j]-0.005): continue
            else:
                e=i+1
                if e>=n or np.isnan(o[e,j]) or o[e,j]>=c[i,j]*(1+lim[j]-0.005): continue
                ent=o[e,j]
            t2=e+2; last=i+hold
            if last>=n or t2>=n: continue
            stop=ent*(1-sl) if sl else None
            # cắt lỗ theo giá đóng cửa: đóng cửa dưới mức -> bán ATO phiên sau
            cstop=None
            if stop_close_atr is not None and atr_ref is not None: cstop=ent-stop_close_atr*atr_ref[i,j]
            if stop_close_pct is not None: cstop=ent*(1-stop_close_pct) if cstop is None else max(cstop,ent*(1-stop_close_pct))
            pend=False
            px=None; k=e; skipped=False
            while k<n:
                if bad[k,j]: skipped=True; break
                if k>=t2 and not np.isnan(c[k,j]) and not floor_locked(k,j,o,h,l,c,lim):
                    lv=ent*(1+tp) if tp is not None else None
                    if lvl is not None and not np.isnan(lvl[k,j]):
                        lv=max(lvl[k,j],ent*(1+COST)) if exit_above_entry else lvl[k,j]
                    if k==t2:   # chỉ bán buổi chiều -> xét theo giá đóng cửa
                        if stop is not None and c[k,j]<=stop: px=c[k,j]; break
                        if lv is not None and c[k,j]>=lv: px=c[k,j]; break
                        if ex is not None and ex[k,j]: px=c[k,j]; break
                        if k>=last and not expire_open: px=c[k,j]; break
                    else:
                        if pend: px=o[k,j]; break
                        if exo is not None and exo[k,j]: px=o[k,j]; break
                        if expire_open and k>=last: px=o[k,j]; break
                        if stop is not None and l[k,j]<=stop: px=min(o[k,j],stop); break
                        if lv is not None and h[k,j]>=lv: px=max(o[k,j],lv); break
                        if ex is not None and ex[k,j]: px=c[k,j]; break
                        if k>=last: px=c[k,j]; break
                if cstop is not None and not np.isnan(c[k,j]) and c[k,j]<cstop: pend=True
                k+=1
            if skipped: busy=k; continue
            if px is None: break
            out.append((dates[i],syms[j],ent,px,k-e+1,px/ent-1-COST))
            busy=k
    return pd.DataFrame(out,columns=['date','sym','entry','exit','days','ret'])
