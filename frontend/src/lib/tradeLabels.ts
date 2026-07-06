/** Mã nhãn backend → hiển thị tiếng Việt */
export const TRADE_LABEL_VI: Record<string, string> = {
  GomIm: "Gom im",
  DayGia: "Đẩy giá",
  Xa: "Xả hàng",
  TrungTinh: "Trung tính",
};

export type TradeLabelFilter =
  | "All"
  | "GomIm"
  | "DayGia"
  | "Xa"
  | "TrungTinh"
  | "ForeignStrong";

export const TRADE_FILTER_OPTIONS: { key: TradeLabelFilter; label: string }[] = [
  { key: "All", label: "Tất cả" },
  { key: "GomIm", label: "Gom im" },
  { key: "DayGia", label: "Đẩy giá" },
  { key: "Xa", label: "Xả hàng" },
  { key: "ForeignStrong", label: "Khối ngoại mạnh" },
];

export function tradeLabelVi(label: string): string {
  return TRADE_LABEL_VI[label] ?? label;
}

/** Ngưỡng % tham chiếu — cổ trần/sàn HOSE ~±7%. */
export const VN_CEILING_CHANGE_PCT = 6.9;
export const VN_FLOOR_CHANGE_PCT = -6.9;

export function isBuyTradeLabel(label: string): boolean {
  return label === "GomIm" || label === "DayGia";
}

export function labelAccentColor(label: string): "green" | "red" | "neutral" {
  if (isBuyTradeLabel(label)) return "green";
  switch (label) {
    case "Xa":
      return "red";
    default:
      return "neutral";
  }
}
