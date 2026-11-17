"""V4 vòng 8: phương pháp Minervini (Trend Template, VCP, cắt lỗ 7%) và Livermore (điểm xoay, đi theo xu hướng thị trường, cắt khi thủng đáy xoay).
Đo từng lệnh (IS 2017–22 / OOS 2023–26) và danh mục 5 chỗ, 40 triệu, lô 100 (ưu tiên RS, và chọn ngẫu nhiên)."""
import sys
exec(open(sys.argv[1] + '/v4dm.py', encoding='utf-8').read().split("bao(MKT.loc")[0])
MA150 = ma(C, 150); HI250 = H.rolling(250, min_periods=200).max(); LO250 = L.rolling(250, min_periods=200).min()
RSm = (RS63.fillna(0) + 2 * RS126.fillna(0)) / 3            # gần "RS rating": nặng 6 tháng
TT = (C > MA150) & (C > MA[200]) & (MA150 > MA[200]) & (MA[200] > MA[200].shift(22)) & (MA[50] > MA150) & (C > MA[50]) \
     & (C >= 1.3 * LO250) & (C >= 0.75 * HI250) & (RSm >= 0.7)
VMA50 = ma(V, 50)
PIVOT = (C > H.rolling(20).max().shift(1)) & (V > 1.5 * VMA50)
ATR10 = TR.rolling(10).mean(); ATR50 = TR.rolling(50).mean()
RANGE20 = (H.rolling(20).max() - L.rolling(20).min()) / H.rolling(20).max()
VCP = ((RANGE20 <= 0.15) & (ATR10 / ATR50 <= 0.75) & (ma(V, 10) < VMA50)).shift(1).fillna(False)
THI_TRUONG_TANG = bc((MKT > M50) & (M50 > M50.shift(10)))
PHA_TOT = PHA_B.isin([1, 2])
# Livermore: điểm xoay = đóng cửa vượt đỉnh 250 phiên sau giai đoạn đi ngang 30 phiên (biên ≤ 20%), KL lớn
RANGE30 = ((H.rolling(30).max() - L.rolling(30).min()) / H.rolling(30).max()).shift(1)
LIV = (C > HI250.shift(1)) & (RANGE30 <= 0.20) & (V > 1.5 * VMA50)
THUNG_DAY = C < L.rolling(20).min().shift(1)
duoi50 = C < MA[50]

CH = {
    'M1 Trend Template + pha diem xoay': TT & PIVOT,
    'M2 Trend Template + VCP + pha diem xoay': TT & VCP & PIVOT,
    'L1 Livermore diem xoay (dinh nam sau tich luy)': LIV,
}
LOC = {'': None, ' | thi truong tang': THI_TRUONG_TANG, ' | giai doan TangManh/HoiPhuc': PHA_TOT}
TH = {
    'cat 7%, ban khi dong cua < MA50': dict(hold=250, sl_pct=0.07, exit_flag=duoi50),
    'cat 7%, chot +20%': dict(hold=60, sl_pct=0.07, tp_pct=0.20),
    'cat 7%, truot 15% tu dinh': dict(hold=250, sl_pct=0.07, trail_pct=0.15),
    'cat 10%, ban khi thung day 20 phien (Livermore)': dict(hold=250, sl_pct=0.10, exit_flag=THUNG_DAY),
}
for cn, sig in CH.items():
    for ln, f in LOC.items():
        s = sig if f is None else sig & f
        for tn, kw in TH.items():
            t = lenh(s, **kw); a, b = chia(t); sa, sb = tom_tat(a), tom_tat(b)
            E, nl = danh_muc(t)
            rr = [danh_muc(t, uu_tien='ngau', seed=k)[0].iloc[-1] - 1 for k in range(4)]
            print(f"{cn[:2]}{ln:28s} {tn[:34]:34s} | lenh IS n{sa['n']:5d} TB {sa['tb']*100:+5.2f}% thang {sa['thang']*100:3.0f}% | OOS n{sb['n']:4d} TB {sb['tb']*100:+5.2f}% thang {sb['thang']*100:3.0f}% ngay {sb['ngay']:5.1f}", flush=True)
            bao(E, f"     danh muc (RS uu tien) | ngau nhien TB {np.mean(rr)*100:+.0f}%", nl)
