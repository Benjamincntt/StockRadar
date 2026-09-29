import type { LucideIcon } from "lucide-react";
import { Bell, Home, Star, TrendingUp, Wrench } from "lucide-react";

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
    to: "/performance",
    label: "Hiệu quả",
    desc: "Đo lường outcome kịch bản",
    icon: TrendingUp,
  },
  {
    to: "/jobs",
    label: "Tác vụ",
    desc: "Job 1 — cập nhật universe",
    icon: Wrench,
  },
];

export const bottomNavLinks = mainNavLinks.filter((l) =>
  ["/", "/alerts", "/watchlist", "/performance", "/jobs"].includes(l.to),
);
