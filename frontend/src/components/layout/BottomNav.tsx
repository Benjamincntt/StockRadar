import { NavLink, useLocation } from "react-router-dom";
import { Bell, Home, Star } from "lucide-react";
import { theme } from "@/theme/tokens";

const tabs = [
  { to: "/", label: "Trang chủ", icon: Home, end: true },
  { to: "/alerts", label: "Lệnh RT", icon: Bell },
  { to: "/watchlist", label: "Watchlist", icon: Star },
];

export function BottomNav() {
  const { pathname } = useLocation();
  const onDetail = pathname.startsWith("/stocks/");

  return (
    <nav
      className="sticky bottom-0 z-30 border-t bg-white px-2 pb-[env(safe-area-inset-bottom)] pt-1"
      style={{ borderColor: theme.border }}
    >
      <div className="grid grid-cols-3">
        {tabs.map(({ to, label, icon: Icon, end }) => {
          const active = !onDetail && (end ? pathname === to : pathname.startsWith(to));
          return (
            <NavLink
              key={to}
              to={to}
              end={end}
              className="flex flex-col items-center gap-0.5 rounded-xl py-2 text-[10px] font-medium"
              style={{ color: active ? theme.green : theme.textMuted }}
            >
              <Icon className="h-5 w-5" strokeWidth={active ? 2.5 : 2} />
              <span>{label}</span>
            </NavLink>
          );
        })}
      </div>
    </nav>
  );
}
