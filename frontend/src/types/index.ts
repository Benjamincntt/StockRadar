export type MarketTrend = "Uptrend" | "Sideway" | "Downtrend";

export interface MarketOverview {
  indexSymbol: string;
  indexPrice: number;
  indexChangePercent: number;
  marketScore: number;
  trend: MarketTrend;
}

export interface QuoteTick {
  symbol: string;
  price: number;
  changePercent: number;
  volume: number;
  updatedAt: string;
}

export interface IndexTick {
  symbol: string;
  price: number;
  changePercent: number;
  marketScore: number;
  trend: MarketTrend;
  updatedAt: string;
}

export interface OhlcvBar {
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export type ChartInterval = "1D" | "1H" | "30m" | "15m" | "5m" | "1m";

export interface ChartBar {
  time: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface StockChart {
  symbol: string;
  interval: ChartInterval;
  bars: ChartBar[];
}

export const CHART_INTERVALS: { value: ChartInterval; label: string; short: string }[] = [
  { value: "1D", label: "Ngày", short: "D" },
  { value: "1H", label: "Giờ", short: "H" },
  { value: "30m", label: "30 phút", short: "30" },
  { value: "15m", label: "15 phút", short: "15" },
  { value: "5m", label: "5 phút", short: "5" },
  { value: "1m", label: "1 phút", short: "1" },
];

export interface RightsEvent {
  symbol: string;
  exDate: string;
  cash: number;
  dilution: number;
  oldShares?: number;
  newShares?: number;
  issuePrice?: number;
}

export interface StockDetail {
  symbol: string;
  name: string;
  sector: string;
  price: number;
  changePercent: number;
  history: OhlcvBar[];
  /** false = mã đã ra khỏi universe theo dõi; giá là đóng cửa phiên cuối. */
  isUniverseActive?: boolean;
  universeStatusReason?: string | null;
  universeUpdatedAt?: string | null;
}

/** Một danh sách theo dõi — mặc định, tự tạo hoặc tự động theo ngành. */
export interface WatchlistDto {
  id: number;
  name: string;
  laDanhSachNganh: boolean;
  laMacDinh: boolean;
  maNganh: string | null;
  soLuongMa: number;
  createdAt: string;
}

export interface WatchlistItem {
  symbol: string;
  name: string;
  sector: string;
  changePercent: number;
  sectorLocked: boolean;
  /** Trạng thái V3 rút gọn; thiếu hoặc null = không có gì (hoặc server cũ chưa trả). */
  trangThaiV3?: TrangThaiV3RutGon | null;
}

/** Trạng thái V3 rút gọn của một mã trên Watchlist. */
export type TrangThaiV3RutGon = "DangGiu" | "ChoMua" | "CoTinHieuMoi";

export interface SectorCatalogItem {
  name: string;
}

export interface SparklineSeries {
  symbol: string;
  prices: number[];
  reference: number;
}

/** Mã kịch bản V3 (enum LoaiKichBanV3, backend serialize dạng chuỗi). */
export type LoaiTinHieuV3 = "KB1" | "KB2" | "KB4" | "N1" | "N3";

/** Trạng thái vị thế V3 (enum TrangThaiViTheV3). */
export type TrangThaiViTheV3 = "DanhDau" | "ChoMua" | "DangGiu" | "DaThoat" | "BiHuy";

/** Trạng thái tín hiệu V3 (enum TrangThaiTinHieuV3). */
export type TrangThaiTinHieuV3 = "DaBao" | "BiCatDanhMuc" | "DaVaoLenh" | "BiHuy";

/** Kết quả đo (ASCII, không dấu) của một vị thế V3 đã đóng. */
export type KetQuaV3 = "Thang" | "Thua" | "HoaVon";

/** Bộ lọc kỳ màn Hiệu quả (quy đổi sang mốc từ–đén phía client). */
export type HieuQuaPeriod = "week" | "month" | "quarter" | "all";

/** Tóm tắt hiệu quả một kịch bản V3 (Phụ lục H khối 1). */
export interface HieuQuaTheoKichBanV3 {
  loai: LoaiTinHieuV3;
  ten: string;
  soLenhDong: number;
  soThang: number;
  soThua: number;
  soHoaVon: number;
  tyLeThangPhanTram: number;
  laiTBPhanTram: number;
  loTBPhanTram: number;
  tongLaiTongLo: number;
  teNhatPhanTram: number;
  giuTBPhien: number;
  soDangGiu: number;
}

/** Tóm tắt danh mục V3 (Phụ lục H khối 2) — đơn vị nghìn đồng. */
export interface HieuQuaDanhMucV3 {
  giaTriHienTai: number;
  tienMat: number;
  soViTheDangGiu: number;
  sutGiamToiDaPhanTram: number;
}

/** Toàn bộ dữ liệu màn Hiệu quả V3 — GET /v3/hieu-qua/tom-tat. */
export interface HieuQuaTomTatV3 {
  theoKichBan: HieuQuaTheoKichBanV3[];
  danhMuc: HieuQuaDanhMucV3 | null;
  denNgay: string | null;
}

/** Một vị thế / lệnh V3 (Phụ lục H khối 3) — GET /v3/hieu-qua/lich-su. */
export interface ViTheV3 {
  id: number;
  symbol: string;
  loai: LoaiTinHieuV3;
  ngayTinHieu: string;
  ngayVao: string | null;
  giaVao: number | null;
  giaTriLenh: number | null;
  trangThai: TrangThaiViTheV3;
  soPhienDaGi: number;
  ngayThoat: string | null;
  giaThoat: number | null;
  lyDoThoat: string | null;
  phanTramLoiNhuan: number | null;
  ketQua: string | null;
  /** Số thật lấy từ Nhật ký giao dịch (spec 014); thiếu/false = số hệ thống giả lập. */
  doiChieuMua?: boolean;
  doiChieuBan?: boolean;
  /** Giá chốt lời của vị thế đang giữ; null với N3 (bán ATO theo lịch); thiếu khi server cũ. */
  giaChotLoi?: number | null;
  /** Số phiên giữ tối đa của vị thế; thiếu khi server cũ. */
  giuToiDa?: number | null;
  /** Số phiên còn lại tới hạn bán; thiếu khi server cũ. */
  soPhienConLai?: number | null;
}

export interface ViTheV3Paged {
  items: ViTheV3[];
  total: number;
  page: number;
  size: number;
}

/** Một tín hiệu V3 kèm snapshot chỉ báo — GET /stocks/{symbol}/v3 (màn chi tiết mã). */
export interface TinHieuV3 {
  id: number;
  symbol: string;
  loai: LoaiTinHieuV3;
  ngayTinHieu: string;
  giaTinHieu: number;
  hang: number;
  giaTriXepHang: number;
  muaMoCuaPhienSau: boolean;
  trangThai: TrangThaiTinHieuV3;
  snapshotJson: string | null;
  giaVao: number | null;
  giaThoat: number | null;
  ketQua: string | null;
  laHoangLoanSau: boolean;
}
