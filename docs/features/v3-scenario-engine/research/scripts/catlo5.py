"""PH-32 — họ I6 (không hồi sau n phiên → cắt chặt): phần bớt sụt đến từ kịch bản nào; đợt sụt tối đa đổi ra sao.
Chạy: python catlo5.py <thư mục làm việc>"""
import sys
exec(open(sys.argv[1] + '/catlo2.py', encoding='utf-8').read().split("if __name__")[0])
rule = no_bounce(5, 0.03, 0.08)
for lab, sc in (('tat ca', ('KB1', 'KB2', 'KB4t', 'N1t', 'N3t')), ('chi N3', ('N3t',)), ('chi KB4', ('KB4t',)),
                ('KB1/KB2/N1', ('KB1', 'KB2', 'N1t')), ('tru N3', ('KB1', 'KB2', 'KB4t', 'N1t'))):
    p = apply(rule, scope=sc); E, T = run_pf(p); d = E / E.cummax() - 1
    bot = d.idxmin(); top = E[:bot].idxmax()
    r = summarize(lab, p)
    print(f"  I6 5p +3% -> 8% {lab:12s} DM {r['tot']*100:+4.0f}% sut {r['mdd']*100:5.1f}% ({top.date()} -> {bot.date()}) | IS {r['IS']*100:+4.0f}% OOS {r['OOS']*100:+4.0f}% | 26 {r['y26']*100:+.0f}%")
