import { NavLink, useLocation } from "react-router-dom";
import { Bell, Home, LineChart, Star } from "lucide-react";
import { useTheme, useThemeTokens } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

const tabs = [
  { to: "/", label: "Trang chủ", icon: Home, end: true },
  { to: "/alerts", label: "Lệnh RT", icon: Bell },
  { to: "/watchlist", label: "Watchlist", icon: Star, filledWhenActive: true },
  { to: "/criteria", label: "Chỉ báo", icon: LineChart, ariaLabel: "Phân tích chỉ báo" },
];

export function BottomNav() {
  const { pathname } = useLocation();
  const { mode } = useTheme();
  const theme = useThemeTokens();
  const onDetail = pathname.startsWith("/stocks/");
  const isLight = mode === "light";

  return (
    <nav
      className={cn(
        "ios-safe-bottom fixed bottom-0 left-0 right-0 z-30 border-t border-outline-variant/20 bg-surface",
        isLight
          ? "shadow-[0_-4px_6px_-1px_rgba(15,23,42,0.05)]"
          : "shadow-[0_-4px_20px_rgba(0,0,0,0.4)]",
      )}
    >
      <div className="mx-auto grid h-16 grid-cols-4 px-1" style={{ maxWidth: theme.maxWidth }}>
        {tabs.map(({ to, label, icon: Icon, end, filledWhenActive, ariaLabel }) => {
          const active = !onDetail && (end ? pathname === to : pathname.startsWith(to));
          return (
            <NavLink
              key={to}
              to={to}
              end={end}
              aria-label={ariaLabel ?? label}
              className="flex flex-col items-center justify-center gap-0.5 px-0.5 text-[9px] font-medium transition-colors sm:text-[10px]"
              style={{ color: active ? theme.primary : theme.textMuted }}
            >
              <Icon
                className="h-5 w-5"
                strokeWidth={active ? 2.5 : 2}
                fill={active && filledWhenActive ? "currentColor" : "none"}
              />
              <span className={active ? "font-semibold" : ""}>{label}</span>
            </NavLink>
          );
        })}
      </div>
    </nav>
  );
}
