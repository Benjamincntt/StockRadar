"""Vòng 6: ghép luật bán không-MA vào danh mục P1++ (5 chỗ, KB1/KB2/N1 x1.5, gửi tiền 4.5%)."""
import sys, pickle
exec(open(sys.argv[1] + '/flex5.py', encoding='utf-8').read().split("RES = {}")[0])

KBN = {'KB1': ('KB1', 'open'), 'KB2': ('KB2', 'open'), 'KB4': ('KB4t', 'atc'), 'N1': ('N1t', 'atc')}
def part(nm, exn):
    sig, en, hd, exo, _ = SC[nm]
    t = sim_x(sig, en, exit_open_rule=exo, **EX(hd)[exn])
    kb, mode = KBN[nm]; return (kb, tag(t, kb, mode))
MA_PARTS = {'KB1': BASE[0], 'KB2': BASE[1], 'KB4': k4, 'N1': n1r}

def row(cfg, label):
    parts = [MA_PARTS[nm] if cfg.get(nm) is None else part(nm, cfg[nm]) for nm in ('KB1', 'KB2', 'KB4', 'N1')] + [N3F]
    E, T = run_pf(parts, 5, size_fn=mult, park_r=D45, park_cost=(0, 0))
    m = E.resample('ME').last(); mo = (m / m.shift(1).fillna(1.0) - 1)
    split2(E, T, label)
    print(f"      thang {(T.ret > 0.0005).mean()*100:.0f}% | lenh {len(T)} | thang te {mo.min()*100:.1f}% | lenh te {T.ret.min()*100:.1f}% | theo KB: "
          + ', '.join(f"{k} {len(g)}/{(g.ret>0.0005).mean()*100:.0f}%/{g.ret.mean()*100:+.1f}%" for k, g in T.groupby('kb')))

row({}, 'P1++ hien tai (MA)')
for exn in ('chi giu het han', 'chot +3%', 'chot +5%', 'chot +7%', 'chot +10%', 'lai>=5% thi truot 3%', 'lai>=5% truot 3%, giu x2', 'lai>=3% thi truot 2%'):
    row({k: exn for k in KBN}, f'Ca 4 KB: {exn}')
row({'KB1': 'chot +3%', 'KB2': 'lai>=5% truot 3%, giu x2', 'KB4': 'chot +5%', 'N1': 'chot +10%'}, 'Chon tot nhat tung KB (de khop qua khu)')
row({'KB1': 'chot +5%', 'KB2': 'chot +5%', 'KB4': 'chot +5%', 'N1': 'chot +5%'}, 'Dong loat chot +5% (tham chieu)')
row({'KB1': 'chot +7%', 'KB2': 'chot +7%', 'KB4': None, 'N1': 'chot +7%'}, 'KB1/KB2/N1 chot +7%, KB4 giu MA')
