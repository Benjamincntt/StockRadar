"""Q4-8 độ bền: vệ tinh cổ tăng sốc — lưới ngưỡng tăng × cửa sổ × khối lượng × trượt × số chỗ; lệnh đóng góp lớn nhất."""
import sys
exec(open(sys.argv[1] + '/v4soc.py', encoding='utf-8').read().split("E, a, b = chay(); bao(E, 'LOI rieng (moc)')")[0])
def tin(r=0.25, w=20, kl=2.0): return ((C / C.shift(w) - 1 >= r) & (C > HI60) & (V > kl * VMA50) & TOP100).values
E, _, _ = chay(); bao(E, 'LOI rieng (moc)')
print('--- nguong tang x cua so (KL x2, S2, truot 20% hoac MA50) ---')
for w in (10, 20, 40):
    for r in (0.15, 0.20, 0.25, 0.30, 0.40):
        E, _, ns = chay(tin=tin(r, w), S=2, truot=0.20, thoat50=True); bao(E, f'tang >= {r*100:.0f}% / {w}p', f'{ns} lenh')
print('--- khoi luong (25%/20p, S2, truot 20% hoac MA50) ---')
for kl in (1.0, 1.5, 3.0):
    E, _, ns = chay(tin=tin(kl=kl), S=2, truot=0.20, thoat50=True); bao(E, f'KL x{kl}', f'{ns} lenh')
print('--- thoat (25%/20p KL x2, S2) ---')
for tr in (0.10, 0.15, 0.25, 0.30):
    for t50 in (False, True):
        E, _, ns = chay(tin=tin(), S=2, truot=tr, thoat50=t50); bao(E, f'truot {tr*100:.0f}%{" hoac MA50" if t50 else ""}', f'{ns} lenh')
print('--- so cho ve tinh (25%/20p KL x2, truot 20% hoac MA50) ---')
for S in (1, 2, 3, 4, 5):
    E, _, ns = chay(tin=tin(), S=S, truot=0.20, thoat50=True); bao(E, f'S{S}', f'{ns} lenh')
# lệnh vệ tinh lớn nhất: mô phỏng từng lệnh độc lập cùng luật thoát
t = mo_phong(pd.DataFrame(tin(), index=dates, columns=syms), entry='close', hold=250, trail_pct=0.20, exit_flag=(C < MA[50]))
t = t[t.date >= '2017-01-01']
print(f'\nlenh doc lap: {len(t)}, thang {(t.ret>.0005).mean()*100:.0f}%, TB {t.ret.mean()*100:+.2f}%, trung vi {t.ret.median()*100:+.2f}%, ngay giu TB {t.days.mean():.0f}')
print('theo nam (so lenh / TB %):', {y: (len(g), round(g.ret.mean()*100, 1)) for y, g in t.groupby(t.date.dt.year)})
print('10 lenh lai nhat:'); print(t.nlargest(10, 'ret')[['date', 'sym', 'ret', 'days']].assign(ret=lambda x: (x.ret*100).round(0)).to_string(index=False))
print(f'TB khi bo 10 lenh lai nhat: {t.drop(t.nlargest(10, "ret").index).ret.mean()*100:+.2f}%')
