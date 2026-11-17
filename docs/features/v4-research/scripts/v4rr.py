"""V4 vòng 4b: lớp rủi ro — thoát khi thị trường chuyển Giảm giá, cắt lỗ cứng; mốc chỉ số có né giai đoạn."""
import sys
exec(open(sys.argv[1] + '/v4dm.py', encoding='utf-8').read().split("bao(MKT.loc")[0])
# Mốc: giữ chỉ số đều trọng số, ra tiền mặt khi giai đoạn bị loại (quyết ở đóng cửa, áp phiên sau, phí 0,6% mỗi lần đổi)
def moc_ne(loai):
    vao = (~PHA.isin(loai)).shift(1).fillna(False)
    r = mret.where(vao, 0.0) - (vao != vao.shift(1)).astype(float) * 0.003
    E = (1 + r.loc['2017-01-01':]).cumprod(); return E
bao(MKT.loc['2017-01-01':] / MKT.loc['2017-01-01':].iloc[0], 'MOC: mua giu chi so')
bao(moc_ne([4]), 'MOC2: chi so, ra tien khi GiamGia')
bao(moc_ne([4, 0]), 'MOC3: chi so, ra tien khi GiamGia hoac HoangLoan')
bao(moc_ne([4, 3]), 'MOC4: chi so, ra tien khi GiamGia hoac DieuChinh')
xau = PHA_B == 4
for ten, sig, kw in (
    ('A+thoat khi GiamGia', F2 & RSc & PHA_B.isin([0, 2]), dict(hold=30, trail=3, exit_flag=xau)),
    ('A+thoat khi GiamGia+cat 2ATR', F2 & RSc & PHA_B.isin([0, 2]), dict(hold=30, trail=3, sl=2, exit_flag=xau)),
    ('F2 chi HoiPhuc+TangManh, thoat GiamGia, cat 2ATR', F2 & PHA_B.isin([1, 2]), dict(hold=30, trail=3, sl=2, exit_flag=xau)),
    ('F1 pha dinh, chi TangManh, thoat GiamGia, truot 3ATR', (C >= C.rolling(55).max()) & (V > 1.5 * VMA20) & (PHA_B == 1), dict(hold=40, trail=3, exit_flag=xau | (PHA_B == 0))),
):
    t = lenh(sig, **kw)
    for ut in ('rs', 'ngau'):
        E, nl = danh_muc(t, uu_tien=ut); bao(E, f'{ten} [{ut}]', nl)
