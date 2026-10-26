import pandas as pd, numpy as np
import os
S=os.path.dirname(os.path.abspath(__file__))   # thư mục làm việc: chứa script + dữ liệu (xem research/README.md)
COST=0.004
IS=('2023-01-01','2025-06-30'); OOS=('2025-07-01','2026-12-31')
df=pd.read_pickle(S+'/bars.pkl'); meta=pd.read_pickle(S+'/meta.pkl').set_index('sym')
P={k:df.pivot(index='date',columns='sym',values=k) for k in 'ohlcv'}
O,H,L,C,V=P['o'],P['h'],P['l'],P['c'],P['v']
syms=C.columns; dates=C.index
LIM=pd.Series({s:{'HOSE':.07,'HNX':.10,'UPCOM':.15}.get(meta.ex.get(s,''),.07) for s in syms})
R=C.pct_change(fill_method=None)
TV=(C*V/1e6).rolling(20).mean()   # bn VND (price in k VND)
UNI=(TV>=5)&(C>=5)&(C.notna())
def ma(x,n): return x.rolling(n).mean()
def ema(x,n): return x.ewm(span=n,adjust=False).mean()
def rsi(x,n):
    d=x.diff(); up=d.clip(lower=0).ewm(alpha=1/n,adjust=False).mean(); dn=(-d.clip(upper=0)).ewm(alpha=1/n,adjust=False).mean()
    return 100-100/(1+up/dn)
MA={n:ma(C,n) for n in (5,10,20,50,100,200)}
VMA20=ma(V,20)
TR=pd.concat([H-L,(H-C.shift()).abs(),(L-C.shift()).abs()]).groupby(level=0).max()
ATR=TR.ewm(alpha=1/14,adjust=False).mean()
RSI14=rsi(C,14); RSI2=rsi(C,2)
HI250=H.rolling(250,min_periods=120).max(); HI60=H.rolling(60).max(); LO20=L.rolling(20).min()
def rank_pct(x): return x.where(UNI).rank(axis=1,pct=True)
RS63=rank_pct(C/C.shift(63)); RS126=rank_pct(C/C.shift(126))
# chỉ số đều trọng số trên vũ trụ thanh khoản
mret=R.where(UNI.shift()).clip(-.16,.16).mean(axis=1).fillna(0)
MKT=(1+mret).cumprod()
MKT_MA20=MKT.rolling(20).mean(); MKT_MA50=MKT.rolling(50).mean()
BREADTH50=((C>MA[50])&UNI).sum(axis=1)/UNI.sum(axis=1)
MKT_UP=(MKT>MKT_MA50)
# ngày ngoại lệ (giá chưa điều chỉnh/ quyền)
BAD=(R.abs()>(LIM+0.015))
def bc(s): return pd.DataFrame(np.repeat(s.values[:,None],len(syms),1),index=dates,columns=syms)

def simulate(sig, hold=10, tp=None, sl=None, sl_atr=None, exit_rule=None, trail_atr=None, exit_level=None, exit_above_entry=False, name=""):
    """Mô phỏng: tín hiệu cuối phiên t, mua giá mở cửa t+1, sớm nhất bán phiên t+3 (T+2.5)."""
    sig=(sig&UNI).fillna(False).values
    o,h,l,c=O.values,H.values,L.values,C.values; bad=BAD.values; lim=LIM.values
    atr=ATR.values; ex=exit_rule.fillna(False).values if exit_rule is not None else None
    lvl=exit_level.values if exit_level is not None else None
    mk=MKT.values
    out=[]; n=len(dates)
    for j in range(len(syms)):
        i=0; idx=np.nonzero(sig[:,j])[0]; busy=-1
        for i in idx:
            if i<=busy or i+3>=n: continue
            e=i+1
            if np.isnan(o[e,j]) or o[e,j]>=c[i,j]*(1+lim[j]-0.005): continue  # trần khi mở cửa, không mua được
            ent=o[e,j]; stop=None
            if sl is not None: stop=ent*(1-sl)
            if sl_atr is not None: stop=ent-sl_atr*atr[i,j]
            tgt=ent*(1+tp) if tp else None
            px=None; k=e; hi=ent; last=min(i+hold,n-1)
            if last<i+3: continue
            for k in range(e,last+1):
                if bad[k,j]: px=-1; break
                if np.isnan(c[k,j]): continue
                if k>=i+3:
                    if stop is not None and l[k,j]<=stop: px=min(o[k,j],stop); break
                    if tgt is not None and h[k,j]>=tgt: px=max(o[k,j],tgt); break
                    if lvl is not None and not np.isnan(lvl[k,j]):
                        lv=max(lvl[k,j],ent*(1+COST)) if exit_above_entry else lvl[k,j]
                        if h[k,j]>=lv: px=max(o[k,j],lv); break
                    if ex is not None and ex[k,j]: px=c[k,j]; break
                hi=max(hi,c[k,j])
                if trail_atr is not None and k>=e: 
                    ns=hi-trail_atr*atr[k,j]; stop=ns if stop is None else max(stop,ns)
            if px==-1: busy=k; continue
            if px is None:
                if i+hold>n-1: break
                px=c[k,j]
            ret=px/ent-1-COST
            mret_=mk[k]/mk[i]-1   # thị trường cùng khoảng (từ đóng cửa t tới phiên thoát)
            out.append((dates[i],syms[j],ent,px,k-e+1,ret,ret-mret_))
            busy=k
    t=pd.DataFrame(out,columns=['date','sym','entry','exit','days','ret','excess'])
    t.attrs['name']=name; return t

def stats(t,label=''):
    if len(t)==0: return dict(label=label,n=0)
    w=t.ret>0.0005; be=t.ret.abs()<=0.0005; g=t.ret[t.ret>0].sum(); b=-t.ret[t.ret<0].sum()
    return dict(label=label,n=len(t),days=t.date.nunique(),win=round(w.mean()*100,1),be=round(be.mean()*100,1),avg=round(t.ret.mean()*100,2),
                med=round(t.ret.median()*100,2),pf=round(g/b,2) if b>0 else np.inf,exc=round(t.excess.mean()*100,2),
                beat=round((t.excess>0).mean()*100,1),hold=round(t.days.mean(),1))

def report(t,show_years=True):
    rows=[stats(t[(t.date>=IS[0])&(t.date<=IS[1])],'IS 23-25H1'),stats(t[(t.date>=OOS[0])],'OOS 25H2-26')]
    if show_years:
        for y in (2023,2024,2025,2026): rows.append(stats(t[t.date.dt.year==y],str(y)))
        q4=t[(t.date.dt.quarter==4)&(t.date.dt.year>=2023)]; rows.append(stats(q4,'Q4 23-25'))
    return pd.DataFrame(rows).set_index('label')

def topk(sig,score,k=5):
    """Giữ tối đa k mã/ngày theo score giảm dần."""
    s=score.where((sig&UNI).fillna(False))
    rk=s.rank(axis=1,ascending=False,method='first')
    return (rk<=k)
def first_profit_exit(): return None
PANIC=bc((BREADTH50<0.20)|(MKT/MKT_MA50-1< -0.07))
RET20=C/C.shift(20)-1
# Mức giá realtime để "giá khớp chạm MAn": giá p thoả p >= (tổng n-1 giá đóng cửa trước + p)/n  <=> p >= TB n-1 phiên trước
def ma_touch_level(n): return C.shift().rolling(n-1).mean()
