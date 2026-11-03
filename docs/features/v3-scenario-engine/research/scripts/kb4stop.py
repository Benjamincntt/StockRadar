"""Q4 — luật dừng lỗ realtime cho KB4 thường (không áp dụng cho lệnh hoảng loạn sâu), có/không điều kiện thị trường.
Dừng: từ phiên T+2 (T+2.5), nếu giá thấp nhất phiên ≤ giá vào × (1 − X) và điều kiện thị trường (đóng cửa phiên
trước) đúng → bán ở mức dừng (mở cửa thấp hơn mức dừng thì bán ở giá mở cửa). Phiên nằm sàn trắng (thấp = cao) không bán được.
Đo từng lệnh KB4 và cả danh mục (luật production, tiền thật 40 triệu, lô 100, làm tròn lô gần nhất).

Bản đã sửa so với nháp đầu (xem báo cáo):
- `col` không có sẵn trong chuỗi exec (nó là biến cục bộ trong run_lot/run_kb4) → phải tự dựng.
- `P` bị một file sau trong chuỗi exec định nghĩa lại THÀNH HÀM, không còn là dict nến → dùng thẳng O/H/L của bt.py.
- Thêm biến thể "chat T+2.5": phiên e+2 chỉ được bán theo giá đóng cửa (đúng như sim_mix), để số liệu không đẹp giả.
"""
import sys
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])

col = {s: j for j, s in enumerate(syms)}          # bt.py: cột của O/H/L/C đúng thứ tự syms
O_, H_, L_, C_ = O.values, H.values, L.values, C.values
lech = (MKT / MKT_MA50 - 1).values
br = BREADTH50.values
idx_kb4 = [n for n, (kb, _) in enumerate(PARTS) if kb == 'KB4t'][0]
K0 = PARTS[idx_kb4][1].copy()
phi = float((K0.exit / K0.entry - 1 - K0.ret).median())
print(f'phi khu hoi suy tu du lieu: {phi*100:.2f}%  (ky vong 0,40%)')


def DK_hoc(i, e):
    return True


DK = {
    'luon dung': DK_hoc,
    'chi so < MA50 -5%': lambda i, e: lech[i - 1] < -0.05,
    'chi so xau hon luc vao': lambda i, e: lech[i - 1] < lech[e],
    'do rong < 0,20': lambda i, e: br[i - 1] < 0.20,
    # de xuat cua AI: thi truong xau di THEO CA HAI chieu (duoi MA50 va te hon ngay vao lenh)
    'DE XUAT AI: chi so < MA50 va xau hon luc vao': lambda i, e: lech[i - 1] < 0 and lech[i - 1] < lech[e],
}


def ap_dung(df, x, dk, chat=False):
    """Bảng lệnh KB4 sau khi áp luật dừng X với điều kiện dk; đổi ret/exit/k của lệnh bị dừng.
    chat=True: phiên e+2 chỉ bán theo giá đóng cửa (đúng ràng buộc T+2.5 của sim_mix)."""
    out = df.copy(); bi_dung = 0
    for r, t in df.iterrows():
        if bool(t.deep):
            continue
        e, k, j = int(t.e), int(t.k), col[t.sym]
        muc = t.entry * (1 - x)
        for i in range(e + 2, k):
            lo, hi, op, cl = L_[i, j], H_[i, j], O_[i, j], C_[i, j]
            if np.isnan(lo) or np.isnan(cl) or lo >= hi - 1e-9:
                continue                                     # không có giá / nằm sàn trắng -> xét phiên sau
            if chat and i == e + 2:                          # chưa giao cổ phiếu sáng phiên e+2: chỉ đóng cửa
                if cl <= muc and dk(i, e):
                    out.at[r, 'ret'] = cl / t.entry - 1 - phi
                    out.at[r, 'exit'] = cl; out.at[r, 'k'] = i; out.at[r, 'days'] = i - e + 1
                    bi_dung += 1; break
                continue
            if lo <= muc and dk(i, e):
                gia = min(op, muc) if not np.isnan(op) else muc
                out.at[r, 'ret'] = gia / t.entry - 1 - phi
                out.at[r, 'exit'] = gia; out.at[r, 'k'] = i; out.at[r, 'days'] = i - e + 1
                bi_dung += 1; break
    return out, bi_dung


def tom_lenh(df):
    d = df[~df.deep.astype(bool)]
    return (f"KB4 thuong {len(d)} lenh: thang {(d.ret>0.0005).mean()*100:3.0f}% TB {d.ret.mean()*100:+5.2f}% "
            f"te nhat {d.ret.min()*100:+5.1f}% lo>=10% {(d.ret<=-0.10).mean()*100:3.0f}%")


def theo_nam(E):
    out = []
    for y in (2023, 2024, 2025, 2026):
        cur = E[E.index.year == y]
        if not len(cur):
            out.append(f"{y}:  n/a"); continue
        prev = E[E.index.year < y]
        base = prev.iloc[-1] if len(prev) else 1.0
        out.append(f"{y}:{(cur.iloc[-1]/base-1)*100:+.0f}%")
    return ' '.join(out)


def tom_dm(lab, E, T, sk, n_dung):
    a = E[E.index <= IS[1]]
    m = E.resample('ME').last(); m = (m / m.shift(1).fillna(1) - 1).min() * 100
    k4 = T[T.kb == 'KB4t']
    print(f"  {lab:46s} dung {n_dung:3d} | tong {(E.iloc[-1]-1)*100:+4.0f}% | sut {(E/E.cummax()-1).min()*100:5.1f}% "
          f"| thang te {m:5.1f}% | trong {(a.iloc[-1]-1)*100:+4.0f}% ngoai {(E.iloc[-1]/a.iloc[-1]-1)*100:+4.0f}% "
          f"| lenh te nhat mat {T.mat.min()*100:4.1f}% DM | KB4 mat te nhat {k4.mat.min()*100 if len(k4) else 0:4.1f}% "
          f"| {theo_nam(E)}")


def o_cung(parts_list, chat=False):
    """Chạy một ô lưới: in số từng lệnh + số danh mục."""
    for ten, x in parts_list:
        K1, n = ap_dung(K0, x, DK[ten] if callable(DK.get(ten)) else ten, chat=chat)
        parts = list(PARTS); parts[idx_kb4] = ('KB4t', K1)
        print(f"  {x*100:.0f}%: " + tom_lenh(K1) + f"  (bi dung {n})")
        tom_dm(f"{x*100:.0f}% {ten[:28]} (DM 40tr)", *run_kb4(parts, 40_000, 'gan', 1.0), n)


# ---- luoi luu (script do ben chia tai day) ----
print('\n### Goc (luat hien tai, khong dung lo)')
print('  ' + tom_lenh(K0))
tom_dm('khong dung lo', *run_kb4(PARTS, 40_000, 'gan', 1.0), 0)

for ten, dk in DK.items():
    print(f'\n### Dieu kien: {ten}')
    for x in (0.05, 0.07, 0.10, 0.15):
        K1, n = ap_dung(K0, x, dk)
        parts = list(PARTS); parts[idx_kb4] = ('KB4t', K1)
        print(f'  dung {x*100:.0f}%: ' + tom_lenh(K1) + f'  (bi dung {n})')
        tom_dm(f'dung {x*100:.0f}% (DM 40tr)', *run_kb4(parts, 40_000, 'gan', 1.0), n)
