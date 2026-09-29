import type { LucideIcon } from "lucide-react";
import { Home, Star, TrendingUp } from "lucide-react";

export interface NavLinkItem {
  to: string;
  label: string;
  desc: string;
  icon: LucideIcon;
  end?: boolean;
  ariaLabel?: string;
  filledWhenActive?: boolean;
}

export const bottomNavLinks: NavLinkItem[] = [
  {
    to: "/",
    label: "Trang chủ",
    desc: "VNINDEX · Top cơ hội · Tín hiệu",
    icon: Home,
    end: true,
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
];
