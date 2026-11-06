"""PH-29: lọc tin xấu CafeF trước khi vào lệnh V3. Tin lấy từ khung tin theo mã (taitin.py).
Giờ ra quyết định: KB1/KB2 08:15 ngày mua ATO; KB4/N1/N3 14:15 ngày mua ATC. Tin chỉ có ngày (00:00) chỉ tính khi ngày < ngày mua.
Cờ = có ít nhất một tiêu đề khớp nhóm trong cửa sổ W ngày lịch trước giờ quyết định. So lệnh bị gắn cờ với lệnh còn lại,
rồi chạy danh mục 40 triệu (run_kb4) khi bỏ các lệnh bị gắn cờ."""
import sys, re, json, glob, os, unicodedata
exec(open(sys.argv[1] + '/kb4size.py', encoding='utf-8').read().split("\n\nfor v0 in (40_000, 100_000):")[0])

NHOM = {
    'phap_ly': r'khởi tố|bị bắt|bắt giam|bắt tạm giam|tạm giam|truy tố|cơ quan điều tra|cơ quan cảnh sát điều tra|bộ công an|c03|bị điều tra|cưỡng chế|buôn lậu|gian lận|lừa đảo|hàng giả|giám định giả|kiểm định giả',
    'giai_chap': r'giải chấp|call margin|bán ép',
    'san_gd': r'hủy niêm yết|huỷ niêm yết|đình chỉ giao dịch|tạm ngừng giao dịch|ngừng giao dịch|diện cảnh báo|diện kiểm soát|bị cảnh báo|hạn chế giao dịch|chậm nộp báo cáo|chậm công bố',
    'kiem_toan': r'ngoại trừ|từ chối đưa ra ý kiến|không đưa ra ý kiến|không thể đưa ra ý kiến|hoạt động liên tục',
    'no': r'chậm trả|chậm thanh toán|vỡ nợ|mất khả năng thanh toán|phá sản|nợ quá hạn trái phiếu|chậm trả lãi',
    'lanh_dao': r'xin lỗi|bỏ trốn|(chủ tịch|tổng giám đốc|lãnh đạo|ceo|người nhà|con gái|con trai|phu nhân).{0,60}(từ nhiệm|bị bắt|khởi tố|bị bán)',
    'lo': r'báo lỗ|lỗ thêm|thua lỗ|lỗ ròng|lỗ kỷ lục|lỗ quý|lỗ nặng|lỗ lớn|lỗ hơn|lỗ gần|lỗ hàng',
}
HUY = r'thoát khỏi diện|ra khỏi diện|đưa ra khỏi|dỡ bỏ|gỡ bỏ|thoát lỗ|giảm lỗ|hết lỗ|lãi trở lại|có lãi trở lại|chuyển lỗ thành lãi|không bị|thoát cảnh báo|bác bỏ|niêm yết.{0,15}trái phiếu|bác yêu cầu'

def chuan(s):
    return unicodedata.normalize('NFC', s).lower()

tin = []
for f in glob.glob(sys.argv[1] + '/tin/*.json'):
    s = os.path.basename(f)[:-5]
    for x in json.load(open(f, encoding='utf-8')):
        tin.append((s, x['t'], chuan(x['title'])))
TIN = pd.DataFrame(tin, columns=['sym', 't', 'title'])
TIN['ts'] = pd.to_datetime(TIN.t, format='%d/%m/%Y %H:%M')
TIN['co_gio'] = ~TIN.t.str.endswith('00:00')
TIN = TIN.drop_duplicates(['sym', 'ts', 'title'])
huy = TIN.title.str.contains(HUY, regex=True)
for k, p in NHOM.items():
    TIN[k] = TIN.title.str.contains(p, regex=True) & ~huy
print(f'Tin: {len(TIN)} tieu de, {TIN.sym.nunique()} ma, tu {TIN.ts.min().date()} | co gio {TIN.co_gio.mean()*100:.0f}%')
print('Khop moi nhom (toan bo tin):', {k: int(TIN[k].sum()) for k in NHOM})
TIN = TIN[TIN[list(NHOM)].any(axis=1)].copy()
by_sym = {s: g for s, g in TIN.groupby('sym')}

ATC = {'KB4t', 'N1t', 'N3t'}

def gan_co(df, kb, W, nhom, cung_ngay=False):
    """Trả mảng bool: lệnh nào có tin thuộc nhom trong W ngày trước giờ quyết định."""
    co = []; ly = []
    for _, x in df.iterrows():
        d = dates[int(x.e)] if int(x.e) < len(dates) else x.date
        qd = d + pd.Timedelta(hours=14, minutes=15) if kb in ATC else d + pd.Timedelta(hours=8, minutes=15)
        g = by_sym.get(x.sym)
        if g is None: co.append(False); ly.append(''); continue
        g = g[g[nhom].any(axis=1)]
        truoc = (g.co_gio & (g.ts < qd)) | (~g.co_gio & ((g.ts <= d) if cung_ngay else (g.ts < d)))
        g = g[truoc & (g.ts >= qd - pd.Timedelta(days=W))]
        co.append(len(g) > 0); ly.append(g.title.iloc[0][:70] if len(g) else '')
    return np.array(co), ly

def tom(lab, r):
    if not len(r): return f'{lab}: 0 lenh'
    return f'{lab}: {len(r):4d} lenh, thang {(r>0.0005).mean()*100:3.0f}%, TB {r.mean()*100:+5.2f}%, trung vi {r.median()*100:+5.2f}%, te nhat {r.min()*100:+6.1f}%'

def danh_muc(parts):
    E, T, sk = run_kb4(parts, 40_000, 'gan', 1.0)
    return (E.iloc[-1] - 1) * 100, (E / E.cummax() - 1).min() * 100, len(T)

g0 = danh_muc(PARTS)
print(f'\nDanh muc goc (40tr, tu 2023): tong {g0[0]:+.0f}% | sut {g0[1]:.1f}% | {g0[2]} lenh vao')
BO = {'rat_xau': ['phap_ly', 'giai_chap', 'san_gd', 'kiem_toan', 'no', 'lanh_dao'], 'rat_xau+lo': list(NHOM)}
BO = {'san+no+kt': ['san_gd','no','kiem_toan'], 'san+no+kt+lo': ['san_gd','no','kiem_toan','lo'], 'rat_xau': BO['rat_xau'], 'rat_xau+lo': BO['rat_xau+lo']}
for W in (7, 14, 30):
    print(f'\n######## Cua so {W} ngay')
    for ten, nh in BO.items():
        parts2 = []; co_all = []; ret_co = []; ret_khong = []
        for kb, p in PARTS:
            c, ly = gan_co(p, kb, W, nh)
            parts2.append((kb, p[~c])); ret_co.append(p.ret[c]); ret_khong.append(p.ret[~c])
            if ten == 'san+no+kt' and W == 30:
                for (_, x), cc, l in zip(p.iterrows(), c, ly):
                    if cc: co_all.append((x.date.strftime('%Y-%m-%d'), kb, x.sym, round(x.ret * 100, 2), l))
        rc = pd.concat(ret_co); rk = pd.concat(ret_khong); dm = danh_muc(parts2)
        print(f'  [{ten:11s}] ' + tom('gan co', rc) + ' || ' + tom('con lai', rk) + f' || DM bo co: tong {dm[0]:+.0f}% sut {dm[1]:.1f}% ({dm[2]} lenh)')
        if co_all:
            print('    -- Lenh bi gan co (rat_xau, 30 ngay):')
            for z in sorted(co_all): print('     ', z)

