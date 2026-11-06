"""PH-29b: thay vì bỏ lệnh dính tin xấu, vào lệnh với hệ số tỉ trọng m (0 = bỏ, 0,5 = nửa, 1 = gốc).
Gắn cờ y hệt loctin2.py (giờ quyết định, cửa sổ W ngày, nhóm từ khoá, danh sách huỷ cờ)."""
import sys
exec(open(sys.argv[1] + '/loctin2.py', encoding='utf-8').read().split("g0 = danh_muc(PARTS)")[0])
src = open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read()
src = src.split('def run_kb4(')[1].split('\n\n\ndef in_dong')[0]
exec('def run_kb4m(' + src.replace('hs = he_so(x)', 'hs = he_so(x) * x.m'))

def chay(parts):
    E, T, sk = run_kb4m(parts, 40_000, 'gan', 1.0)
    m = E.resample('ME').last(); m = (m / m.shift(1).fillna(1) - 1).min() * 100
    return f'tong {(E.iloc[-1]-1)*100:+5.0f}% | sut {(E/E.cummax()-1).min()*100:6.1f}% | thang te {m:5.1f}% | lenh te nhat mat {T.mat.min()*100:4.1f}% DM | {len(T)} lenh'

print('\nGOC (m=1 moi lenh):', chay([(kb, p.assign(m=1.0)) for kb, p in PARTS]))
NH = {'giai_chap+lanh_dao': ['giai_chap', 'lanh_dao'],
      'rat_xau': ['phap_ly', 'giai_chap', 'san_gd', 'kiem_toan', 'no', 'lanh_dao'],
      'rat_xau+lo': list(NHOM),
      'san+no+kt': ['san_gd', 'no', 'kiem_toan']}
for W in (7, 14, 30):
    print(f'\n######## Cua so {W} ngay')
    for ten, nh in NH.items():
        co = {kb: gan_co(p, kb, W, nh)[0] for kb, p in PARTS}
        n = sum(int(c.sum()) for c in co.values())
        for m in (0.0, 0.5, 0.75):
            parts = [(kb, p.assign(m=np.where(co[kb], m, 1.0))) for kb, p in PARTS]
            print(f'  [{ten:18s}] {n:3d} lenh dinh co, he so {m:4.2f}: ' + chay(parts))
