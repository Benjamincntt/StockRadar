"""Kịch bản A (gần đỉnh 1 năm, mua 16/11 giữ tới 16/03): độ bền + lớp cắt lỗ."""
import sys, warnings
warnings.filterwarnings('ignore')
s = open(sys.argv[1] + '/v4mua.py', encoding='utf-8').read(); exec(s[:s.index('KQ = {')])
def mua_cat(so=10, md='11-16', cat_ma=None, cat_gio=None, sc=GD):
    """cat_ma: bán riêng mã khi giá đóng cửa <= giá mua × (1 − cat_ma) (bán ở giá đóng cửa đó, giữ tiền mặt).
    cat_gio: bán cả giỏ khi giá trị giỏ <= vốn × (1 − cat_gio)."""
    out = {}
    for y in range(2016, 2026):
        a, b = i_le(f'{y}-{md}'), i_le(f'{y+1}-03-16'); ch = chon(a, sc, so)
        w = np.full(len(ch), 1.0 / len(ch)); con = np.ones(len(ch), bool); tien = 0.0
        for d in range(a + 1, b + 1):
            r = ADJ[d, ch] / ADJ[a, ch]; r = np.where(np.isfinite(r), r, 1.0)
            if cat_ma:
                ban = con & (r <= 1 - cat_ma)
                tien += (w[ban] * r[ban]).sum(); con &= ~ban
            gt = tien + (w[con] * r[con]).sum()
            if cat_gio and gt <= 1 - cat_gio: tien = gt; con[:] = False; break
        gt = tien + (w[con] * (ADJ[b, ch[con]] / ADJ[a, ch[con]])).sum() if con.any() else tien
        out[y + 1] = gt * (1 - PHI) - 1
    return pd.Series(out)
def tom(s): return f"TB {s.mean()*100:+5.1f} trung vi {s.median()*100:+5.1f} duong {(s>0).sum()}/10 te {s.min()*100:+5.1f} bo2021 {s.drop(2021).mean()*100:+5.1f} | " + ' '.join(f"{y%100}:{v*100:+.0f}" for y, v in s.items())
print('--- do ben: so ma x ngay mua ---')
for so in (5, 10, 15, 20, 30):
    for md in ('11-01', '11-16', '12-01'):
        print(f'{so:2d} ma, mua {md}: ' + tom(mua_cat(so, md)))
print('--- lop cat lo (10 ma, mua 16/11) ---')
for cm in (None, 0.10, 0.15, 0.20):
    for cg in (None, 0.10, 0.15):
        print(f'cat ma {cm}, cat gio {cg}: ' + tom(mua_cat(10, '11-16', cm, cg)))
