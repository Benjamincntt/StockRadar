"""PH-32 — lưới mọi họ cắt lỗ trên toàn bộ V3 (KB1/KB2/KB4/N1/N3), ba cách xử lý nến ngày (tt / xau / tot).
Chạy: python catlo2.py <thư mục làm việc> [tt,xau,tot]. Ghi kết quả ra catlo2_<mode>.csv và in từng dòng."""
import sys
exec(open(sys.argv[1] + '/catlo.py', encoding='utf-8').read().split('# ==== CHAY')[0])
MODES = (sys.argv[2] if len(sys.argv) > 2 else 'tt,xau,tot').split(',')
INF = 1e12


def fixed(x):
    return lambda i, st: {'lv': st['ent'] * (1 - x)}


def atr(m):
    return lambda i, st: {'lv': st['ent'] - m * ATRV[st['sig'], st['j']]}


def lowN(n, b):
    return lambda i, st: {'lv': LO[n][st['sig'], st['j']] * (1 - b)}


def ma_rt(n, b):
    def f(i, st):
        lv = LVL[n][i, st['j']]
        return None if np.isnan(lv) else {'lv': lv * (1 - b)}
    return f


def time_stop(n, y):
    """ATC phiên e+n: giá khớp lúc ~14:25 (≈ đóng cửa) dưới giá vào × (1−y) thì bán."""
    return lambda i, st: {'atc': True} if i == st['e'] + n and C_[i, st['j']] < st['ent'] * (1 - y) else None


def trail(x):
    return lambda i, st: {'lv': st['peak'] * (1 - x)}


def breakeven(g, x=None):
    """Đỉnh từng đạt giá vào × (1+g) thì dời mức cắt lên giá vào + phí; x: mức cắt cố định trước đó (None = chưa cắt)."""
    def f(i, st):
        if st['peak'] >= st['ent'] * (1 + g): return {'lv': st['ent'] * (1 + PHI)}
        return {'lv': st['ent'] * (1 - x)} if x else None
    return f


def mkt_worse(x):
    """Cắt X chỉ khi chỉ số (đóng cửa phiên trước) xấu hơn lúc vào so với MA50 (PH-24 mở rộng cho mọi kịch bản)."""
    return lambda i, st: {'lv': st['ent'] * (1 - x)} if LECH[i - 1] < LECH[st['e']] else None


def breadth_drop(x, d):
    return lambda i, st: {'lv': st['ent'] * (1 - x)} if BRV[i - 1] < BRV[st['sig']] - d else None


def idio(x, y):
    """Mới: chỉ cắt khi mã giảm RIÊNG — lợi nhuận từ lúc vào của mã kém chỉ số bình quân đều quá y (đóng cửa phiên trước)."""
    def f(i, st):
        j, e = st['j'], st['e']
        if np.isnan(C_[i - 1, j]): return None
        rel = (C_[i - 1, j] / st['ent']) / (MKTV[i - 1] / MKTV[e]) - 1
        return {'lv': st['ent'] * (1 - x)} if rel < -y else None
    return f


def sec_idio(x, y):
    """Mới: chỉ cắt khi mã kém chỉ số NGÀNH của nó quá y từ lúc vào."""
    def f(i, st):
        j, e = st['j'], st['e']; s = SECJ[j]
        if np.isnan(C_[i - 1, j]): return None
        rel = (C_[i - 1, j] / st['ent']) / (s[i - 1] / s[e]) - 1
        return {'lv': st['ent'] * (1 - x)} if rel < -y else None
    return f


def floor_hold(mk=-0.02):
    """Mới: trong lúc giữ, mã có phiên giảm sát sàn mà thị trường hôm đó không sập (mret > mk) và mã đang lỗ → bán ngay
    phiên sau (mở cửa; phiên e+2 thì đóng cửa)."""
    def f(i, st):
        j = st['j']
        if i - 1 >= st['e'] and FLOOR[i - 1, j] and (MKTV[i - 1] / MKTV[i - 2] - 1) > mk and C_[i - 1, j] < st['ent']:
            return {'lv': INF}
        return None
    return f


def partial(x, fr):
    return lambda i, st: {'lv': st['ent'] * (1 - x), 'frac': fr}


def decay(x0, x1, dn):
    """Mới: mức cắt rộng lúc đầu (x0), siết dần tới x1 sau dn phiên — cú hồi phải tới sớm."""
    return lambda i, st: {'lv': st['ent'] * (1 - (x0 + (x1 - x0) * min(1.0, (i - st['e']) / dn)))}


def no_bounce(n, g, x):
    """Mới: sau n phiên mà đỉnh chưa từng vượt giá vào × (1+g) (không có cú hồi) thì bật mức cắt chặt x."""
    def f(i, st):
        if i - st['e'] >= n and st['peak'] < st['ent'] * (1 + g): return {'lv': st['ent'] * (1 - x)}
        return None
    return f


def combo_wide_nb(xw, n, g, x):
    """Mới: cắt thảm hoạ xw luôn bật + mức chặt x khi không có cú hồi sau n phiên."""
    nb = no_bounce(n, g, x)
    def f(i, st):
        r = nb(i, st)
        return r if r else {'lv': st['ent'] * (1 - xw)}
    return f


FAM = []
def add(fam, lab, rule, **kw): FAM.append((fam, lab, rule, kw))

for x in (0.03, 0.05, 0.07, 0.10, 0.12, 0.15, 0.20, 0.25): add('A % co dinh', f'{x*100:.0f}%', fixed(x))
for m in (1.0, 1.5, 2.0, 3.0, 4.0): add('B ATR', f'{m} ATR', atr(m))
for n in (5, 10, 20):
    for b in (0.0, 0.02, 0.05): add('C day N phien', f'day {n} phien -{b*100:.0f}%', lowN(n, b))
for n, b in ((20, 0.05), (20, 0.10), (50, 0.10), (50, 0.20)): add('D MA realtime', f'MA{n} -{b*100:.0f}%', ma_rt(n, b))
for n in (3, 5, 7):
    for y in (0.0, 0.03, 0.05): add('E thoi gian', f'ATC phien e+{n} neu < vao -{y*100:.0f}%', time_stop(n, y))
for x in (0.07, 0.10, 0.15, 0.20): add('F truot tu dinh', f'truot {x*100:.0f}%', trail(x))
for g in (0.03, 0.05, 0.08): add('F hoa von', f'dinh +{g*100:.0f}% -> hoa von', breakeven(g))
for g in (0.03, 0.05): add('F hoa von', f'cat 10% + dinh +{g*100:.0f}% -> hoa von', breakeven(g, 0.10))
for x in (0.05, 0.10, 0.15): add('G theo kich ban', f'chi KB4 {x*100:.0f}%', fixed(x), scope=('KB4t',))
for x in (0.05, 0.10, 0.15): add('G theo kich ban', f'chi N3 {x*100:.0f}%', fixed(x), scope=('N3t',))
for x in (0.10, 0.15): add('G theo kich ban', f'chi KB1/KB2/N1 {x*100:.0f}%', fixed(x), scope=('KB1', 'KB2', 'N1t'))
for x in (0.10, 0.15): add('G theo kich ban', f'{x*100:.0f}% ca lenh hoang loan sau', fixed(x), deep=True)
for x in (0.05, 0.10, 0.15): add('H thi truong', f'{x*100:.0f}% khi chi so xau hon luc vao', mkt_worse(x))
for x in (0.05, 0.10):
    for d in (0.05, 0.10): add('H thi truong', f'{x*100:.0f}% khi do rong giam {d*100:.0f} diem', breadth_drop(x, d))
for x in (0.05, 0.07, 0.10):
    for y in (0.03, 0.05, 0.08): add('I1 giam rieng (so chi so)', f'{x*100:.0f}% neu kem chi so {y*100:.0f}%', idio(x, y))
for x in (0.05, 0.07, 0.10):
    for y in (0.03, 0.05, 0.08): add('I2 giam rieng (so nganh)', f'{x*100:.0f}% neu kem nganh {y*100:.0f}%', sec_idio(x, y))
for mk in (-0.02, -0.04, -1.0): add('I3 san luc giu', f'san rieng (thi truong > {mk*100:.0f}%)', floor_hold(mk))
for x in (0.05, 0.07, 0.10, 0.15): add('I4 cat mot nua', f'ban 50% o -{x*100:.0f}%', partial(x, 0.5))
for x0, x1, dn in ((0.15, 0.05, 10), (0.20, 0.07, 10), (0.15, 0.08, 6), (0.10, 0.03, 10)):
    add('I5 siet dan', f'{x0*100:.0f}% -> {x1*100:.0f}% sau {dn} phien', decay(x0, x1, dn))
for n in (3, 5, 7):
    for g, x in ((0.02, 0.03), (0.02, 0.05), (0.03, 0.05), (0.03, 0.08)):
        add('I6 khong hoi', f'sau {n} phien chua +{g*100:.0f}% -> cat {x*100:.0f}%', no_bounce(n, g, x))
for xw in (0.15, 0.20):
    add('I7 tham hoa + khong hoi', f'{xw*100:.0f}% + (5 phien chua +2% -> 5%)', combo_wide_nb(xw, 5, 0.02, 0.05))


if __name__ == '__main__':
    for mode in MODES:
        rows = []
        b = summarize('BASE', [(kb, d.assign(cut=False)) for kb, d in PARTS]); b['fam'] = '0 BASE'; rows.append(b)
        print(f'\n######## mode {mode}'); print(fmt(b))
        for fam, lab, rule, kw in FAM:
            r = summarize(lab, apply(rule, mode=mode, **kw)); r['fam'] = fam; rows.append(r)
            print(f'[{fam}] ' + fmt(r), flush=True)
        df = pd.DataFrame(rows)
        for kb in ('KB1', 'KB2', 'KB4t', 'N1t', 'N3t'):
            df[kb + '_avg'] = df[kb].map(lambda v: v[0]); df[kb + '_worst'] = df[kb].map(lambda v: v[1]); df[kb + '_cut'] = df[kb].map(lambda v: v[2])
            del df[kb]
        df.to_csv(f'{WD}/catlo2_{mode}.csv', index=False)
