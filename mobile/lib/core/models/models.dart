class AuthUser {
  const AuthUser({
    required this.userId,
    required this.email,
    required this.displayName,
    required this.token,
  });

  final String userId;
  final String email;
  final String displayName;
  final String token;

  factory AuthUser.fromJson(Map<String, dynamic> json) => AuthUser(
        userId: json['userId']?.toString() ?? '',
        email: json['email'] as String? ?? '',
        displayName: json['displayName'] as String? ?? '',
        token: json['token'] as String? ?? '',
      );
}

class StockSearchHit {
  const StockSearchHit({required this.symbol, required this.name});

  final String symbol;
  final String name;

  factory StockSearchHit.fromJson(Map<String, dynamic> json) => StockSearchHit(
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        name: json['name'] as String? ?? '',
      );
}

class QuoteTick {
  const QuoteTick({
    required this.symbol,
    required this.price,
    required this.changePercent,
    this.volume = 0,
    this.updatedAt,
  });

  final String symbol;
  final double price;
  final double changePercent;
  final double volume;
  final String? updatedAt;

  factory QuoteTick.fromJson(Map<String, dynamic> json) => QuoteTick(
        symbol: (json['symbol'] ?? json['Symbol'] ?? '').toString().toUpperCase(),
        price: ((json['price'] ?? json['Price']) as num?)?.toDouble() ?? 0,
        changePercent: ((json['changePercent'] ?? json['ChangePercent']) as num?)?.toDouble() ?? 0,
        volume: ((json['volume'] ?? json['Volume']) as num?)?.toDouble() ?? 0,
        updatedAt: (json['updatedAt'] ?? json['UpdatedAt'])?.toString(),
      );
}

class SparklineSeries {
  const SparklineSeries({required this.symbol, required this.closes});

  final String symbol;
  final List<double> closes;

  factory SparklineSeries.fromJson(Map<String, dynamic> json) => SparklineSeries(
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        closes: (json['closes'] as List<dynamic>? ?? [])
            .map((e) => (e as num).toDouble())
            .toList(),
      );
}

/// Danh sách theo dõi (multi-watchlist): mặc định / ngành tự động / tùy chỉnh.
/// Khớp WatchlistDto từ API `api/v1/watchlists`.
class Watchlist {
  const Watchlist({
    required this.id,
    required this.name,
    required this.laDanhSachNganh,
    required this.laMacDinh,
    required this.maNganh,
    required this.soLuongMa,
    this.createdAt,
  });

  final int id;
  final String name;
  /// true = danh sách ngành tự động (items động theo [maNganh], không thêm/xóa tay).
  final bool laDanhSachNganh;
  /// true = danh sách mặc định của user (không xóa/đổi tên).
  final bool laMacDinh;
  final String? maNganh;
  final int soLuongMa;
  final DateTime? createdAt;

  /// Danh sách thường (mặc định hoặc tùy chỉnh) — cho phép thêm/xóa mã thủ công.
  bool get choPhepSua => !laDanhSachNganh;

  factory Watchlist.fromJson(Map<String, dynamic> json) => Watchlist(
        id: (json['id'] as num?)?.toInt() ?? 0,
        name: json['name'] as String? ?? '',
        laDanhSachNganh: json['laDanhSachNganh'] as bool? ?? false,
        laMacDinh: json['laMacDinh'] as bool? ?? false,
        maNganh: json['maNganh'] as String?,
        soLuongMa: (json['soLuongMa'] as num?)?.toInt() ?? 0,
        createdAt: DateTime.tryParse(json['createdAt'] as String? ?? ''),
      );
}

class WatchlistItem {
  const WatchlistItem({
    required this.symbol,
    required this.name,
    required this.sector,
    required this.changePercent,
    this.sectorLocked = false,
    this.trangThaiV3,
  });

  final String symbol;
  final String name;
  final String sector;
  final double changePercent;
  final bool sectorLocked;

  /// Trạng thái V3 rút gọn: DangGiu, ChoMua, CoTinHieuMoi; null = không có gì (hoặc server cũ chưa trả).
  final String? trangThaiV3;

  factory WatchlistItem.fromJson(Map<String, dynamic> json) => WatchlistItem(
        symbol: json['symbol'] as String? ?? '',
        name: json['name'] as String? ?? '',
        sector: json['sector'] as String? ?? '',
        changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
        sectorLocked: json['sectorLocked'] as bool? ?? false,
        trangThaiV3: json['trangThaiV3'] as String?,
      );
}

class ChartBar {
  const ChartBar({
    required this.time,
    required this.close,
    required this.volume,
    this.open,
    this.high,
    this.low,
  });

  final String time;
  final double close;
  final double volume;
  final double? open;
  final double? high;
  final double? low;

  factory ChartBar.fromJson(Map<String, dynamic> json) => ChartBar(
        time: json['time'] as String? ?? json['date'] as String? ?? '',
        close: (json['close'] as num?)?.toDouble() ?? 0,
        volume: (json['volume'] as num?)?.toDouble() ?? 0,
        open: (json['open'] as num?)?.toDouble(),
        high: (json['high'] as num?)?.toDouble(),
        low: (json['low'] as num?)?.toDouble(),
      );

  double get openVal => open ?? close;
  double get highVal => high ?? close;
  double get lowVal => low ?? close;
}

const chartOneYearSessions = 252;

List<ChartBar> chartBarsFromHistory(List<ChartBar> history) {
  if (history.length <= chartOneYearSessions) return history;
  return history.sublist(history.length - chartOneYearSessions);
}

/// Giới hạn số nến intraday để mỗi cây đủ rộng (FireAnt-style).
List<ChartBar> sliceChartBarsForInterval(List<ChartBar> bars, String interval) {
  if (interval == '1D') return chartBarsFromHistory(bars);
  const limits = {
    '1m': 180,
    '5m': 156,
    '15m': 128,
    '30m': 96,
    '1H': 120,
  };
  final limit = limits[interval] ?? 120;
  if (bars.length <= limit) return bars;
  return bars.sublist(bars.length - limit);
}

class StockChart {
  const StockChart({required this.symbol, required this.bars, this.interval = '1D'});

  final String symbol;
  final List<ChartBar> bars;
  final String interval;

  factory StockChart.fromJson(Map<String, dynamic> json) => StockChart(
        symbol: json['symbol'] as String? ?? '',
        interval: json['interval'] as String? ?? '1D',
        bars: (json['bars'] as List<dynamic>? ?? [])
            .map((e) => ChartBar.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class VnIndexChartSnapshot {
  const VnIndexChartSnapshot({
    required this.symbol,
    required this.price,
    required this.changePercent,
    required this.changePoints,
    required this.volume,
    required this.turnoverBillionVnd,
    required this.advancing,
    required this.unchanged,
    required this.declining,
    required this.asOfUtc,
    this.exchangeLabel = 'Sàn Chứng khoán TP.HCM',
  });

  final String symbol;
  final double price;
  final double changePercent;
  final double changePoints;
  final int volume;
  final double turnoverBillionVnd;
  final int advancing;
  final int unchanged;
  final int declining;
  final String asOfUtc;
  final String exchangeLabel;

  factory VnIndexChartSnapshot.fromJson(Map<String, dynamic> json) => VnIndexChartSnapshot(
        symbol: json['symbol'] as String? ?? 'VNINDEX',
        price: (json['price'] as num?)?.toDouble() ?? 0,
        changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
        changePoints: (json['changePoints'] as num?)?.toDouble() ?? 0,
        volume: (json['volume'] as num?)?.toInt() ?? 0,
        turnoverBillionVnd: (json['turnoverBillionVnd'] as num?)?.toDouble() ?? 0,
        advancing: (json['advancing'] as num?)?.toInt() ?? 0,
        unchanged: (json['unchanged'] as num?)?.toInt() ?? 0,
        declining: (json['declining'] as num?)?.toInt() ?? 0,
        asOfUtc: json['asOfUtc'] as String? ?? '',
        exchangeLabel: json['exchangeLabel'] as String? ?? 'Sàn Chứng khoán TP.HCM',
      );

  bool isNewerOrSameAs(VnIndexChartSnapshot? previous) {
    if (previous == null) return true;
    if (asOfUtc.isEmpty || previous.asOfUtc.isEmpty) {
      return price != previous.price ||
          advancing != previous.advancing ||
          volume != previous.volume;
    }
    final a = DateTime.tryParse(asOfUtc);
    final b = DateTime.tryParse(previous.asOfUtc);
    if (a == null || b == null) return true;
    return !a.isBefore(b);
  }
}

/// Chi tiết một mã: thông tin cơ bản, giá, lịch sử nến ngày và trạng thái universe.
class StockDetail {
  const StockDetail({
    required this.symbol,
    required this.name,
    required this.sector,
    required this.price,
    required this.changePercent,
    required this.history,
    this.isUniverseActive = true,
    this.universeStatusReason,
    this.universeUpdatedAt,
  });

  final String symbol;
  final String name;
  final String sector;
  final double price;
  final double changePercent;
  final List<ChartBar> history;
  final bool isUniverseActive;
  final String? universeStatusReason;
  final String? universeUpdatedAt;

  factory StockDetail.fromJson(Map<String, dynamic> json) => StockDetail(
        symbol: json['symbol'] as String? ?? '',
        name: json['name'] as String? ?? '',
        sector: json['sector'] as String? ?? '',
        price: (json['price'] as num?)?.toDouble() ?? 0,
        changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
        history: (json['history'] as List<dynamic>? ?? [])
            .map((e) => ChartBar.fromJson(e as Map<String, dynamic>))
            .toList(),
        isUniverseActive: json['isUniverseActive'] as bool? ?? true,
        universeStatusReason: json['universeStatusReason']?.toString(),
        universeUpdatedAt: json['universeUpdatedAt']?.toString(),
      );
}

class SuKienQuyen {
  const SuKienQuyen({
    required this.symbol,
    required this.exDate,
    required this.cash,
    required this.dilution,
    this.oldShares = 0,
    this.newShares = 0,
    this.issuePrice = 0,
  });

  final String symbol;
  final String exDate;
  final double cash;
  final double dilution;
  final int oldShares;
  final int newShares;
  final double issuePrice;

  factory SuKienQuyen.fromJson(Map<String, dynamic> json) => SuKienQuyen(
        symbol: json['symbol'] as String? ?? '',
        exDate: json['exDate']?.toString() ?? '',
        cash: (json['cash'] as num?)?.toDouble() ?? 0,
        dilution: (json['dilution'] as num?)?.toDouble() ?? 1,
        oldShares: (json['oldShares'] as num?)?.toInt() ?? 0,
        newShares: (json['newShares'] as num?)?.toInt() ?? 0,
        issuePrice: (json['issuePrice'] as num?)?.toDouble() ?? 0,
      );
}

class NhatKyLenh {
  const NhatKyLenh({
    required this.id,
    required this.symbol,
    required this.loaiLenh,
    required this.thoiDiemKhop,
    required this.giaKhop,
    required this.khoiLuong,
    this.phi,
    this.nguonTinHieu,
    this.maTinHieu,
    this.giaTinHieuChup,
    this.lyDo,
    this.ghiChu,
    this.kLieuChinh,
    this.vonDieuChinh,
    this.lechSoTinHieu,
    this.lechSoTinHieuPhanTram,
    this.ketQuaTinHieu,
    this.ketQuaTinHieuPhanTram,
  });

  final String id;
  final String symbol;
  final String loaiLenh;
  final String thoiDiemKhop;
  final double giaKhop;
  final double khoiLuong;
  final double? phi;
  final String? nguonTinHieu;
  final String? maTinHieu;
  final double? giaTinHieuChup;
  final String? lyDo;
  final String? ghiChu;
  final double? kLieuChinh;
  final double? vonDieuChinh;
  final double? lechSoTinHieu;
  final double? lechSoTinHieuPhanTram;
  final String? ketQuaTinHieu;
  final double? ketQuaTinHieuPhanTram;

  factory NhatKyLenh.fromJson(Map<String, dynamic> json) => NhatKyLenh(
        id: json['id']?.toString() ?? '',
        symbol: json['symbol'] as String? ?? '',
        loaiLenh: json['loaiLenh'] as String? ?? '',
        thoiDiemKhop: json['thoiDiemKhop']?.toString() ?? '',
        giaKhop: (json['giaKhop'] as num?)?.toDouble() ?? 0,
        khoiLuong: (json['khoiLuong'] as num?)?.toDouble() ?? 0,
        phi: (json['phi'] as num?)?.toDouble(),
        nguonTinHieu: json['nguonTinHieu'] as String?,
        maTinHieu: json['maTinHieu'] as String?,
        giaTinHieuChup: (json['giaTinHieuChup'] as num?)?.toDouble(),
        lyDo: json['lyDo'] as String?,
        ghiChu: json['ghiChu'] as String?,
        kLieuChinh: (json['kLieuChinh'] as num?)?.toDouble(),
        vonDieuChinh: (json['vonDieuChinh'] as num?)?.toDouble(),
        lechSoTinHieu: (json['lechSoTinHieu'] as num?)?.toDouble(),
        lechSoTinHieuPhanTram: (json['lechSoTinHieuPhanTram'] as num?)?.toDouble(),
        ketQuaTinHieu: json['ketQuaTinHieu'] as String?,
        ketQuaTinHieuPhanTram: (json['ketQuaTinHieuPhanTram'] as num?)?.toDouble(),
      );

  Map<String, dynamic> toRequestBody() => {
        'symbol': symbol,
        'loaiLenh': loaiLenh,
        'thoiDiemKhop': thoiDiemKhop,
        'giaKhop': loaiLenh == 'DieuChinh' ? null : giaKhop,
        'khoiLuong': loaiLenh == 'DieuChinh' ? null : khoiLuong,
        'phi': phi,
        'nguonTinHieu': nguonTinHieu,
        'maTinHieu': maTinHieu,
        'lyDo': lyDo,
        'ghiChu': ghiChu,
        'kLieuChinh': kLieuChinh,
        'vonDieuChinh': vonDieuChinh,
      };
}

class ViTheThat {
  const ViTheThat({
    required this.symbol,
    required this.khoiLuongMo,
    required this.giaVonBinhQuan,
    required this.tienVonDangKe,
    required this.ngayMo,
    required this.soLenhDaBan,
    this.daDong = false,
    this.ngayDong,
    this.returnOnDeployedPercent,
    this.loiNhuanRongVND,
  });

  final String symbol;
  final double khoiLuongMo;
  final double giaVonBinhQuan;
  final double tienVonDangKe;
  final String ngayMo;
  final int soLenhDaBan;
  final bool daDong;
  final String? ngayDong;
  final double? returnOnDeployedPercent;
  final double? loiNhuanRongVND;

  factory ViTheThat.fromJson(Map<String, dynamic> json) => ViTheThat(
        symbol: json['symbol'] as String? ?? '',
        khoiLuongMo: (json['khoiLuongMo'] as num?)?.toDouble() ?? 0,
        giaVonBinhQuan: (json['giaVonBinhQuan'] as num?)?.toDouble() ?? 0,
        tienVonDangKe: (json['tienVonDangKe'] as num?)?.toDouble() ?? 0,
        ngayMo: json['ngayMo']?.toString() ?? '',
        soLenhDaBan: (json['soLenhDaBan'] as num?)?.toInt() ?? 0,
        daDong: json['daDong'] as bool? ?? false,
        ngayDong: json['ngayDong']?.toString(),
        returnOnDeployedPercent: (json['returnOnDeployedPercent'] as num?)?.toDouble(),
        loiNhuanRongVND: (json['loiNhuanRongVND'] as num?)?.toDouble(),
      );
}

/// US3 — tổng hợp tài khoản thật theo kỳ (GET /nhat-ky/thong-ke).
class ThongKeNhatKy {
  const ThongKeNhatKy({
    required this.tang,
    required this.thua,
    required this.ngang,
    required this.choDo,
    required this.soLenh,
    required this.tongLoiNhuanRongVND,
    required this.tongDoLosuDungVND,
    required this.nguongThangPhanTram,
    this.tyLeThang,
    this.tyLeKhopTinHieu,
    this.lechTBSoTinHieuPhanTram,
    this.lechTBMuaPhanTram,
    this.lechTBBanPhanTram,
    this.tinHieuCoLenh = 0,
    this.tinHieuBoQua = 0,
  });

  final int tang;
  final int thua;
  final int ngang;
  final int choDo;
  final int soLenh;
  final double tongLoiNhuanRongVND;
  final double tongDoLosuDungVND;
  final double nguongThangPhanTram;
  final double? tyLeThang;
  final double? tyLeKhopTinHieu;
  final double? lechTBSoTinHieuPhanTram;
  final double? lechTBMuaPhanTram;
  final double? lechTBBanPhanTram;
  final int tinHieuCoLenh;
  final int tinHieuBoQua;

  factory ThongKeNhatKy.fromJson(Map<String, dynamic> json) {
    final viThe = json['viThe'] as Map<String, dynamic>? ?? {};
    return ThongKeNhatKy(
      tang: (viThe['tang'] as num?)?.toInt() ?? 0,
      thua: (viThe['thua'] as num?)?.toInt() ?? 0,
      ngang: (viThe['ngang'] as num?)?.toInt() ?? 0,
      choDo: (viThe['choDo'] as num?)?.toInt() ?? 0,
      tyLeThang: (viThe['tyLeThang'] as num?)?.toDouble(),
      soLenh: (json['soLenh'] as num?)?.toInt() ?? 0,
      tongLoiNhuanRongVND: (json['tongLoiNhuanRongVND'] as num?)?.toDouble() ?? 0,
      tongDoLosuDungVND: (json['tongDoLosuDungVND'] as num?)?.toDouble() ?? 0,
      nguongThangPhanTram: (json['nguongThangPhanTram'] as num?)?.toDouble() ?? 0,
      tyLeKhopTinHieu: (json['tyLeKhopTinHieu'] as num?)?.toDouble(),
      tinHieuCoLenh: (json['tinHieuCoLenh'] as num?)?.toInt() ?? 0,
      tinHieuBoQua: (json['tinHieuBoQua'] as num?)?.toInt() ?? 0,
      lechTBSoTinHieuPhanTram: (json['lechTBSoTinHieuPhanTram'] as num?)?.toDouble(),
      lechTBMuaPhanTram: (json['lechTBMuaPhanTram'] as num?)?.toDouble(),
      lechTBBanPhanTram: (json['lechTBBanPhanTram'] as num?)?.toDouble(),
    );
  }
}

/// Nhãn nguồn tín hiệu cho lệnh đã lưu. VIP (V1) và V2 đã ngừng — giữ lệnh cũ, không migrate.
String tinHieuNguonLabel(String? nguon) {
  final n = nguon ?? '';
  return n.toUpperCase() == 'VIP' ? 'VIP (đã ngừng)' : n;
}

/// Item picker tín hiệu US2 (V3 kịch bản P1++ đã vào lệnh) — chỉ đọc.
class TinHieuNhatKy {
  const TinHieuNhatKy({
    required this.nguon,
    required this.maTinHieu,
    required this.symbol,
    required this.ngay,
    this.loaiTinHieu,
    this.giaTinHieu,
    this.ketQuaTinHieu,
    this.ketQuaTinHieuPhanTram,
    this.daCoLenh = false,
    this.daBoQua = false,
  });

  final String nguon;
  final String maTinHieu;
  final String symbol;
  final String ngay;
  final String? loaiTinHieu;
  final double? giaTinHieu;
  final String? ketQuaTinHieu;
  final double? ketQuaTinHieuPhanTram;
  final bool daCoLenh;
  final bool daBoQua;

  factory TinHieuNhatKy.fromJson(Map<String, dynamic> json) => TinHieuNhatKy(
        nguon: json['nguon'] as String? ?? 'V3',
        maTinHieu: json['maTinHieu']?.toString() ?? '',
        symbol: json['symbol'] as String? ?? '',
        ngay: json['ngay']?.toString() ?? '',
        loaiTinHieu: json['loaiTinHieu'] as String?,
        giaTinHieu: (json['giaTinHieu'] as num?)?.toDouble(),
        ketQuaTinHieu: json['ketQuaTinHieu'] as String?,
        ketQuaTinHieuPhanTram: (json['ketQuaTinHieuPhanTram'] as num?)?.toDouble(),
        daCoLenh: json['daCoLenh'] as bool? ?? false,
        daBoQua: json['daBoQua'] as bool? ?? false,
      );
}
