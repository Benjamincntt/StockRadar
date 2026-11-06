import { useThemeTokens } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";
import type { TrangThaiV3RutGon } from "@/types";

interface V3StatusBadgeProps {
  trangThai?: TrangThaiV3RutGon | null;
  className?: string;
}

/** Huy hiệu trạng thái V3 rút gọn của một mã (Watchlist). Trạng thái thiếu hoặc lạ thì không hiện gì. */
export function V3StatusBadge({ trangThai, className }: V3StatusBadgeProps) {
  const theme = useThemeTokens();
  const style =
    trangThai === "DangGiu"
      ? { label: "Đang giữ", bg: theme.greenBg, color: theme.primary }
      : trangThai === "ChoMua"
        ? { label: "Chờ mua", bg: theme.amberBg, color: theme.amber }
        : trangThai === "CoTinHieuMoi"
          ? { label: "Có tín hiệu", bg: theme.neutralBg, color: theme.textMuted }
          : null;
  if (!style) return null;

  return (
    <span
      className={cn("inline-flex rounded px-2 py-0.5 text-xs font-semibold", className)}
      style={{ backgroundColor: style.bg, color: style.color }}
    >
      {style.label}
    </span>
  );
}
