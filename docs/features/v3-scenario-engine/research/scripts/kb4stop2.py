"""Do do ben cua luoi kb4stop: o lan can (X ± 2%, ngưỡng điều kiện ± 2 điểm), tách theo năm,
và biến thể "chặt T+2.5" (phiên e+2 chỉ bán theo giá đóng cửa)."""
import sys
exec(open(sys.argv[1] + '/kb4stop.py', encoding='utf-8').read().split("# ---- luoi luu")[0])

print(f"\n### DO BEN — goc: " + tom_lenh(K0))
E0, T0, sk0 = run_kb4(PARTS, 40_000, 'gan', 1.0)
tom_dm('GOC khong dung lo', E0, T0, sk0, 0)


def do(lab, x, dk, chat=False):
    K1, n = ap_dung(K0, x, dk, chat=chat)
    parts = list(PARTS); parts[idx_kb4] = ('KB4t', K1)
    print(f'\n  [{lab}] dung {x*100:.0f}%' + (' +chat T+2.5' if chat else ''))
    print('    lenh: ' + tom_lenh(K1) + f'  (bi dung {n})')
    tom_dm('    dm', *run_kb4(parts, 40_000, 'gan', 1.0), n)


dk_lech = lambda ng: (lambda i, e: lech[i - 1] < -ng)
dk_br = lambda ng: (lambda i, e: br[i - 1] < ng)
dk_xau = lambda sl: (lambda i, e: lech[i - 1] < lech[e] - sl)
dk_ai = lambda sl: (lambda i, e: lech[i - 1] < 0 and lech[i - 1] < lech[e] - sl)

print('\n### O 1: do rong (ngưỡng 0,20 ± 0,02; X 8/10/12%)')
for ng in (0.18, 0.20, 0.22):
    for x in (0.08, 0.10, 0.12):
        do(f'BR<{ng:.2f}', x, dk_br(ng))

print('\n### O 2: chi so < MA50 (ngưỡng -3%/-5%/-7%; X 13/15/17%)')
for ng in (0.03, 0.05, 0.07):
    for x in (0.13, 0.15, 0.17):
        do(f'MA50-{ng*100:.0f}%', x, dk_lech(ng))

print('\n### O 3: chi so te hon luc vao (dung 0 / ±2 điểm; X 8/10/12%)')
for sl in (-0.02, 0.0, 0.02):
    for x in (0.08, 0.10, 0.12):
        do(f'xau+{sl*100:+.0f}diem', x, dk_xau(sl))

print('\n### O 4: DE XUAT AI (duoi MA50 va te hon luc vao; dung 0/+2/+4 diem; X 10/12%)')
for sl in (0.0, 0.02, 0.04):
    for x in (0.10, 0.12):
        do(f'AI+{sl*100:.0f}diem', x, dk_ai(sl))

print('\n### BIEN THE CHAT T+2.5 (phiên e+2 chỉ bán theo đóng cửa) — 4 ô đại diện')
for lab, x, dk in (('BR<0.20', 0.10, dk_br(0.20)), ('BR<0.20', 0.07, dk_br(0.20)),
                   ('MA50-5%', 0.15, dk_lech(0.05)), ('xau hon luc vao', 0.10, dk_xau(0.0))):
    do(lab, x, dk, chat=True)

print('\n### KIEM TRAO LUOI: 5% luon dung (so san chat/lo)')
do('luon dung', 0.05, DK_hoc)
do('luon dung', 0.05, DK_hoc, chat=True)
