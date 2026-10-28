"""Vòng 7: phí + thuế ăn bao nhiêu phần lãi; độ nhạy khi phí khứ hồi 0,4% / 0,6% / 0,8%."""
import sys
exec(open(sys.argv[1] + '/flex6.py', encoding='utf-8').read().split("row({}, ")[0])

def build(exn):
    if exn is None:
        return [MA_PARTS[nm] for nm in ('KB1', 'KB2', 'KB4', 'N1')] + [N3F]
    return [part(nm, exn) for nm in ('KB1', 'KB2', 'KB4', 'N1')] + [N3F]

for label, exn in (('Cham MA (hien tai)', None), ('Chot +3%', 'chot +3%'), ('Chot +5%', 'chot +5%')):
    base = build(exn)
    for cost in (0.004, 0.006, 0.008):
        parts = [(kb, t.assign(ret=t.ret + COST - cost)) for kb, t in base]
        E, T = run_pf(parts, 5, size_fn=mult, park_r=D45, park_cost=(0, 0))
        gross = T.ret + cost
        fee_share = cost * len(T) / gross[gross > 0].sum() * 100
        win = (T.ret > 0.0005).mean() * 100
        wins = T.ret[T.ret > 0.0005]
        print(f"{label:20s} phi {cost*100:.1f}% | tong {(E.iloc[-1]-1)*100:+5.0f}% | sut {(E/E.cummax()-1).min()*100:5.1f}% | thang {win:3.0f}% | "
              f"lai rong TB lenh thang {wins.mean()*100:+.2f}% | lenh thang <1% rong: {(wins < 0.01).mean()*100:3.0f}% | phi / tong lai gop {fee_share:4.1f}% | lenh {len(T)}")
