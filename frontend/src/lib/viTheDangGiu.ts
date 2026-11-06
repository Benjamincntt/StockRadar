import { formatPrice } from "@/lib/utils";
import type { ViTheV3 } from "@/types";

/** Lãi/lỗ theo % so với giá mua; null khi thiếu giá mua hoặc giá hiện tại. */
export function laiLoPhanTram(giaVao: number | null | undefined, giaHienTai: number | null | undefined): number | null {
  if (giaVao == null || giaVao <= 0 || giaHienTai == null || giaHienTai <= 0) return null;
  return ((giaHienTai - giaVao) / giaVao) * 100;
}

/** Nhãn giá chốt lời: N3 bán ATO theo lịch nên không có giá; server cũ chưa trả thì không hiện. */
export function nhanGiaChotLoi(viThe: ViTheV3): string | null {
  if (viThe.loai === "N3") return "Bán ATO theo lịch";
  return viThe.giaChotLoi != null ? `Chốt lời ${formatPrice(viThe.giaChotLoi)}` : null;
}

/** Nhãn số phiên còn lại tới hạn bán; server cũ chưa trả thì không hiện. */
export function nhanSoPhienConLai(viThe: ViTheV3): string | null {
  return viThe.soPhienConLai != null ? `Còn ${viThe.soPhienConLai} phiên` : null;
}
