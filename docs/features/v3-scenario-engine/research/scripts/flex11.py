"""PH-21 không gửi tiền (app để tiền mặt 0%) để ghi vào spec."""
import sys
exec(open(sys.argv[1] + '/flex10.py', encoding='utf-8').read().split("# kiểm: deep rỗng")[0])
deep = DEEPS['do rong < 0,15']
for rn in ('MA (nhu hien tai)', 'chot +15%, giu 40'):
    rule = RULES[rn]; parts = []
    for nm in ('KB1', 'KB2', 'KB4', 'N1'):
        t = sim_mix(SC[nm][0], KBN[nm][1], NORMAL[nm], NORMAL[nm] if rule is None else rule, deep)
        kb, mode = KBN[nm]; parts.append((kb, tag(t, kb, mode)))
    for lab, kw in (('khong gui', {}), ('gui 4.5%', dict(park_r=D45, park_cost=(0, 0)))):
        E, T = run_pf(parts + [N3F], 5, size_fn=mult, **kw)
        print(f"{rn:20s} {lab:9s} tong {(E.iloc[-1]-1)*100:+.0f}% sut {(E/E.cummax()-1).min()*100:.1f}%")
