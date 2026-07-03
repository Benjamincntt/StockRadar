import type { LucideIcon } from "lucide-react";
import { Bell, Home, LineChart, Star, Target, Wrench } from "lucide-react";

export interface NavLinkItem {
  to: string;
  label: string;
  desc: string;
  icon: LucideIcon;
  end?: boolean;
  ariaLabel?: string;
  filledWhenActive?: boolean;
}

export const mainNavLinks: NavLinkItem[] = [
  {
    to: "/",
    label: "Trang chủ",
    desc: "VNINDEX · Top cơ hội · Tín hiệu",
    icon: Home,
    end: true,
  },
  {
    to: "/alerts",
    label: "Khớp lệnh",
    desc: "Lô lớn · VSA · dòng tiền",
    icon: Bell,
  },
  {
    to: "/watchlist",
    label: "Watchlist",
    desc: "Mã bạn đang theo dõi",
    icon: Star,
    filledWhenActive: true,
  },
  {
    to: "/criteria",
    label: "Phân tích chỉ báo",
    desc: "Top 10 TA · độ khớp T-1",
    icon: LineChart,
    ariaLabel: "Phân tích chỉ báo",
  },
  {
    to: "/performance",
    label: "Hiệu quả Top",
    desc: "T+2.5 · Master · review tuần",
    icon: Target,
  },
  {
    to: "/jobs",
    label: "Jobs",
    desc: "Job 1 — cập nhật universe",
    icon: Wrench,
  },
];

export const bottomNavLinks = mainNavLinks.filter((l) =>
  ["/", "/alerts", "/watchlist", "/criteria"].includes(l.to),
);
