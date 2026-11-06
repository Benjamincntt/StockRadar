"""PH-32 — vòng 2 sau lưới catlo2: (1) đợt sụt tối đa của BASE gồm những lệnh nào (vì sao cắt lỗ không bớt sụt);
(2) lệnh bị cắt rồi giá đi đâu (cắt đúng hay cắt hụt); (3) chỉ áp cho KB4 các họ cho KB4 tốt lên ở lưới
(không hồi, giảm riêng) — chọn SAU khi xem lưới nên phải xem ngoài mẫu; (4) độ bền họ I6 theo năm.
Chạy: python catlo4.py <thư mục làm việc>"""
import sys
exec(open(sys.argv[1] + '/catlo2.py', encoding='utf-8').read().split("if __name__")[0])

# (1) đợt sụt tối đa của BASE
base = [(kb, d.assign(cut=False)) for kb, d in PARTS]
E, T = run_pf(base)
dds = E / E.cummax() - 1
bot = dds.idxmin(); top = E[:bot].idxmax()
print(f'### (1) Sut toi da BASE {dds.min()*100:.1f}%: dinh {top.date()} -> day {bot.date()}')
mk = MKT[top:bot]; print(f'  chi so binh quan deu cung khoang: {(mk.iloc[-1]/mk.iloc[0]-1)*100:+.1f}%')
allt = pd.concat([d for _, d in PARTS])
ia, ib = dates.get_loc(top), dates.get_loc(bot)
ov = allt[(allt.e <= ib) & (allt.k >= ia)]
for _, t in ov.sort_values('e').iterrows():
    j = COL[t.sym]
    print(f'  {t.kb:5s} {t.sym:5s} vao {dates[int(t.e)].date()} ban {dates[int(t.k)].date()} ket qua {t.ret*100:+6.1f}% '
          f'| gia tai day dot sut so gia vao {C_[min(ib, int(t.k)), j]/t.entry*100-100:+6.1f}%')
# 5 đợt sụt sâu nhất (tách đợt theo đỉnh mới)
print('  5 dot sut sau nhat cua BASE:')
peak = E.cummax(); grp = (E == peak).cumsum(); g = dds.groupby(grp).min().sort_values()[:5]
for gi, v in g.items():
    s = dds[grp == gi]; print(f'    {s.index[0].date()} -> day {s.idxmin().date()} : {v*100:.1f}%')

# (2) lệnh bị cắt: giá sau đó đi đâu
print('\n### (2) Lenh bi cat 10% co dinh / I6 (5 phien chua +3% -> 8%): so voi giu theo luat goc')
for lab, rule in (('co dinh 10%', fixed(0.10)), ('I6 5p +3% 8%', no_bounce(5, 0.03, 0.08)), ('thoi gian e+7 -3%', time_stop(7, 0.03))):
    p = apply(rule)
    c = pd.concat([d.assign(goc=b.ret) for (kb, d), (_, b) in zip(p, PARTS)])
    c = c[c.cut]
    print(f'  {lab:18s} {len(c)} lenh bi cat | TB sau cat {c.ret.mean()*100:+.2f}% neu giu {c.goc.mean()*100:+.2f}% '
          f'| cat dung (giu con te hon) {(c.goc < c.ret).mean()*100:.0f}% | giu ve >= 0 {(c.goc > 0.0005).mean()*100:.0f}% '
          f'| giu lo >= 10% {(c.goc <= -0.10).mean()*100:.0f}% | tong chenh {(c.ret - c.goc).sum()*100:+.0f} diem %')

# (3) chỉ KB4
print('\n### (3) Chi ap cho KB4 (chon sau khi xem luoi)')
b = summarize('BASE', base); print(fmt(b))
for lab, rule in (('KB4: 5 phien chua +3% -> 8%', no_bounce(5, 0.03, 0.08)), ('KB4: 5 phien chua +2% -> 5%', no_bounce(5, 0.02, 0.05)),
                  ('KB4: 7 phien chua +3% -> 8%', no_bounce(7, 0.03, 0.08)), ('KB4: 3 phien chua +3% -> 8%', no_bounce(3, 0.03, 0.08)),
                  ('KB4: 5% neu kem chi so 5%', idio(0.05, 0.05)), ('KB4: 10% neu kem nganh 3%', sec_idio(0.10, 0.03)),
                  ('KB4: san rieng (tt > -2%)', floor_hold(-0.02))):
    for mode in ('tt', 'xau'):
        r = summarize(f'{lab} [{mode}]', apply(rule, scope=('KB4t',), mode=mode)); print(fmt(r))
        print(f'      lenh ngoai mau TB {r["avgOOS"]*100:+.2f}% (BASE {b["avgOOS"]*100:+.2f}%) te {r["worstOOS"]*100:.1f}% (BASE {b["worstOOS"]*100:.1f}%)')

# (4) I6 độ bền: lưới rộng hơn trên toàn V3
print('\n### (4) I6 khong hoi, luoi rong (toan V3, tt)')
for n in (4, 5, 6, 7, 8):
    for g in (0.02, 0.03, 0.04):
        for x in (0.05, 0.08, 0.10):
            r = summarize(f'I6 {n}p +{g*100:.0f}% -> {x*100:.0f}%', apply(no_bounce(n, g, x)))
            print(f"  {r['label']:22s} DM {r['tot']*100:+4.0f}% sut {r['mdd']*100:5.1f}% | IS {r['IS']*100:+4.0f}% OOS {r['OOS']*100:+4.0f}% "
                  f"| 23 {r['y23']*100:+3.0f} 24 {r['y24']*100:+3.0f} 25 {r['y25']*100:+3.0f} 26 {r['y26']*100:+3.0f} | thang-te {r['mmon']*100:.1f}%")
