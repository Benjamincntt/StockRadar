"""Q4-3 — V3 lỗ 2017–2022 vì đâu.

Dựng trên phần đầu của v3so.py: cùng PARTS, cùng run_kb4, cùng phí 0,6% (trừ thêm 0,002 vào ret).
Tách bốn góc nhìn:
  A. kịch bản × năm: số lệnh, tỉ lệ thắng (lãi ròng > 0,05%), lãi TB, đóng góp vào danh mục (điểm %).
  B. kịch bản × giai đoạn thị trường tại ngày tín hiệu × giai đoạn kiểm (2017–22 / 2023–26).
  C. tần suất tín hiệu mỗi năm của từng kịch bản (tín hiệu thô và số lệnh vào được).
  D. phép thử chẩn đoán: bỏ tín hiệu khi giai đoạn là Giảm giá / Hoảng loạn (không phải đề xuất luật).
Giai đoạn thị trường đọc từ bộ đo V4 (v4core.py của PHA) — chỉ đọc nhãn, không đổi định nghĩa.
Đóng góp vào danh mục = run_kb4 ghi `mat` = cost × ret / giá trị danh mục hôm trước.
"""
import sys, warnings
warnings.filterwarnings('ignore')
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])

V4 = sys.argv[2] if len(sys.argv) > 2 else 'D:/sr-research/v4'
_argv = sys.argv[:]
sys.argv = [sys.argv[0], V4]
_ns = {'__name__': '__main__'}
exec(open(V4 + '/v4core.py', encoding='utf-8').read(), _ns)
sys.argv = _argv
PHA4 = _ns['PHA']
TENPHA = {0: 'HoangLoan', 1: 'TangManh', 2: 'HoiPhuc', 3: 'DieuChinh', 4: 'GiamGia'}
KB = ('KB1', 'KB2', 'KB4t', 'N1t', 'N3t')
P2 = [(kb, t.assign(ret=t.ret - 0.002)) for kb, t in PARTS]


def pha_cua(t):
    return PHA4.reindex(pd.DatetimeIndex(t.date)).values


def tach(g):
    """n | thắng % | lãi TB/lệnh % | đóng góp điểm % vào danh mục."""
    n = len(g)
    if n == 0:
        return 0, np.nan, np.nan, 0.0
    return n, (g.ret > 0.0005).mean() * 100, g.ret.mean() * 100, g.mat.sum() * 100


def baoE(E, ten):
    a = E[E.index <= '2022-12-31']; b = E[E.index >= '2023-01-01']
    cagr = E.iloc[-1] ** (252 / len(E)) - 1; sut = (E / E.cummax() - 1).min()
    print(f'{ten:32s} | tong {(E.iloc[-1]-1)*100:+6.0f}% nam {cagr*100:+5.1f}% sut {sut*100:6.1f}% l/s {cagr/-sut:5.2f} '
          f'| 17-22 {(a.iloc[-1]/a.iloc[0]-1)*100:+5.0f}% 23-26 {(b.iloc[-1]/b.iloc[0]-1)*100:+5.0f}% (sut {((b/b.cummax()-1).min())*100:5.1f}%)')


E, T, sk = run_kb4(P2, 40_000, 'gan', 1.0)
T['ph'] = pha_cua(T)
print('ngay cuoi', dates[-1].date(), '| so tin hieu:', {kb: len(t) for kb, t in P2})
print('lenh vao duoc', len(T), '| bo vi thieu lo', sk, '| lenh thieu nhan giai doan', int(T.ph.isna().sum()))

print('\n### A. Kich ban x nam   (n | thang % | lai TB/lenh % | dong gop diem %)')
print(f"{'kb':6s}{'nam':6s}{'n':>5s}{'thang':>7s}{'TB':>8s}{'gop':>8s}")
for kb in KB:
    for y in sorted(T.date.dt.year.unique()):
        g = T[(T.kb == kb) & (T.date.dt.year == y)]
        if not len(g):
            continue
        n, w, a, c = tach(g)
        print(f'{kb:6s}{y:<6d}{n:5d}{w:7.0f}{a:+8.2f}{c:+8.1f}')
    n, w, a, c = tach(T[T.kb == kb])
    print(f'{kb:6s}{"TAT":<6s}{n:5d}{w:7.0f}{a:+8.2f}{c:+8.1f}')
n, w, a, c = tach(T)
print(f'{"TAT":6s}{"":<6s}{n:5d}{w:7.0f}{a:+8.2f}{c:+8.1f}')

print('\n### B. Kich ban x giai doan (n | thang % | lai TB/lenh % | dong gop diem %)')
for lo, hi, lab in ((2017, 2022, 'TRONG MAU 2017-22'), (2023, 2026, 'NGOAI MAU 2023-26')):
    print(f'\n-- {lab}')
    s = T[(T.date.dt.year >= lo) & (T.date.dt.year <= hi)]
    for kb in KB + ('TAT',):
        g0 = s if kb == 'TAT' else s[s.kb == kb]
        for ph in (0, 1, 2, 3, 4):
            g = g0[g0.ph == ph]
            if not len(g):
                continue
            n, w, a, c = tach(g)
            print(f'{kb:6s}{TENPHA[ph]:11s}n{n:4d} thang {w:5.0f}% TB {a:+6.2f}% gop {c:+7.1f}')

print('\n### C. Tan suat tin hieu moi nam (tin hieu thô / lenh vao duoc)')
for kb, t in P2:
    s = t[t.date >= '2017-01-01']
    tn = s.groupby(s.date.dt.year).size()
    lv = T[T.kb == kb].groupby(T[T.kb == kb].date.dt.year).size()
    print(f'{kb:6s}', ' '.join(f'{y}:{tn.get(y, 0):3d}/{lv.get(y, 0):3d}' for y in range(2017, 2027)))

print('\n### D. Chan doan: bo tin hieu theo giai doan (khong phai de xuat luat)')
for bo, ten in ((set(), 'V3 goc (khong bo)'), ({4}, 'bo khi Giam gia'), ({0}, 'bo khi Hoang loan'), ({0, 4}, 'bo khi Giam gia + Hoang loan')):
    P3 = [(kb, t[~pd.Series(pha_cua(t), index=t.index).isin(bo)]) for kb, t in P2]
    E2, T2, _ = run_kb4(P3, 40_000, 'gan', 1.0)
    baoE(E2, ten)
    print(f'{"":32s}   lenh {len(T2)}, thang {(T2.ret > 0.0005).mean()*100:.0f}%, TB {T2.ret.mean()*100:+.2f}%')

print('\n### E. Lai TB/lenh theo kich ban: trong mau vs ngoai mau (dau doi chieu)')
for kb in KB:
    a = T[(T.kb == kb) & (T.date.dt.year <= 2022)]; b = T[(T.kb == kb) & (T.date.dt.year >= 2023)]
    print(f'{kb:6s} trong n{len(a):3d} TB {a.ret.mean()*100:+6.2f}% thang {(a.ret > .0005).mean()*100:4.0f}% '
          f'| ngoai n{len(b):3d} TB {b.ret.mean()*100:+6.2f}% thang {(b.ret > .0005).mean()*100:4.0f}%')

print('\n### F. Tong dong gop theo kich ban va theo nam (diem %)')
print(f"{'kb':6s}{'17-22':>9s}{'23-26':>9s}{'ca ky':>9s}")
for kb in KB:
    g = T[T.kb == kb]
    print(f'{kb:6s}{g[g.date.dt.year <= 2022].mat.sum()*100:+9.1f}{g[g.date.dt.year >= 2023].mat.sum()*100:+9.1f}{g.mat.sum()*100:+9.1f}')
print(f"{'TAT':6s}{T[T.date.dt.year <= 2022].mat.sum()*100:+9.1f}{T[T.date.dt.year >= 2023].mat.sum()*100:+9.1f}{T.mat.sum()*100:+9.1f}")
print(f"{'nam':6s}{'n':>5s}{'gop':>9s}")
for y in sorted(T.date.dt.year.unique()):
    g = T[T.date.dt.year == y]
    print(f'{y:<6d}{len(g):5d}{g.mat.sum()*100:+9.1f}')

