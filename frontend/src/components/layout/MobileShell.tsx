import { useState } from "react";
import { NavLink } from "react-router-dom";
import { Bell, ChevronRight, Home, LineChart, Star, Target, Wrench, X } from "lucide-react";
import { TopBar } from "./TopBar";
import { BottomNav } from "./BottomNav";
import { ThemeToggle } from "@/components/ui/ThemeToggle";
import { useThemeTokens } from "@/context/ThemeContext";

interface MobileShellProps {
  children: React.ReactNode;
}

const menuLinks = [
  {
    to: "/",
    label: "Trang chủ",
    desc: "VNINDEX · Top cơ hội · Tín hiệu",
    icon: Home,
    end: true,
  },
  {
    to: "/alerts",
    label: "Lệnh realtime",
    desc: "Khối ngoại · tự doanh · thỏa thuận · lệnh treo",
    icon: Bell,
  },
  {
    to: "/watchlist",
    label: "Watchlist",
    desc: "Mã bạn đang theo dõi",
    icon: Star,
  },
  {
    to: "/criteria",
    label: "Phân tích chỉ báo",
    desc: "Top 10 TA · độ khớp T-1 · trọng số",
    icon: LineChart,
  },
  {
    to: "/performance",
    label: "Hiệu quả Top",
    desc: "T+2.5 · Master alerts · review tuần",
    icon: Target,
  },
  {
    to: "/jobs",
    label: "Jobs",
    desc: "Job 1 — cập nhật universe",
    icon: Wrench,
  },
] as const;

export function MobileShell({ children }: MobileShellProps) {
  const theme = useThemeTokens();
  const [menuOpen, setMenuOpen] = useState(false);
  const closeMenu = () => setMenuOpen(false);

  return (
    <div className="flex min-h-screen justify-center bg-background">
      <div
        className="relative flex min-h-screen w-full flex-col bg-background"
        style={{ maxWidth: theme.maxWidth }}
      >
        <TopBar onMenuClick={() => setMenuOpen(true)} />

        {menuOpen && (
          <div className="fixed inset-0 z-50 flex justify-center">
            <button
              type="button"
              className="absolute inset-0 bg-black/50 backdrop-blur-[2px]"
              aria-label="Đóng menu"
              onClick={closeMenu}
            />
            <aside
              className="relative flex h-full w-full flex-col border-l border-outline-variant bg-surface-low shadow-2xl"
              style={{ maxWidth: theme.maxWidth }}
            >
              <div className="flex items-start justify-between gap-3 border-b border-outline-variant px-5 pb-4 pt-5">
                <div>
                  <p className="label-caps text-on-surface-variant">StockRadar</p>
                  <h2 className="mt-0.5 text-lg font-bold text-on-surface">Điều hướng</h2>
                  <p className="mt-1 text-xs text-on-surface-variant">Chọn mục để chuyển trang</p>
                </div>
                <button
                  type="button"
                  onClick={closeMenu}
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-surface-high text-on-surface-variant"
                  aria-label="Đóng"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              <nav className="flex-1 space-y-2 overflow-y-auto p-4">
                {menuLinks.map((link) => (
                  <NavLink
                    key={link.to}
                    to={link.to}
                    end={"end" in link ? link.end : false}
                    onClick={closeMenu}
                    className={({ isActive }) =>
                      [
                        "flex items-center gap-3 rounded-2xl border px-3.5 py-3 transition-colors",
                        isActive
                          ? "border-primary/30 bg-positive-dim"
                          : "border-transparent bg-surface-high/50 hover:bg-surface-high",
                      ].join(" ")
                    }
                  >
                    {({ isActive }) => {
                      const Icon = link.icon;
                      return (
                        <>
                          <span
                            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl"
                            style={{
                              backgroundColor: isActive ? theme.greenBg : theme.surfaceElevated,
                              color: isActive ? theme.primary : theme.textMuted,
                            }}
                          >
                            <Icon className="h-5 w-5" strokeWidth={isActive ? 2.5 : 2} />
                          </span>
                          <span className="min-w-0 flex-1">
                            <span
                              className="block text-sm font-semibold"
                              style={{ color: isActive ? theme.primary : theme.text }}
                            >
                              {link.label}
                            </span>
                            <span className="mt-0.5 block text-xs text-on-surface-variant">
                              {link.desc}
                            </span>
                          </span>
                          <ChevronRight
                            className="h-4 w-4 shrink-0"
                            style={{ color: isActive ? theme.primary : theme.textSubtle }}
                          />
                        </>
                      );
                    }}
                  </NavLink>
                ))}
              </nav>

              <div className="border-t border-outline-variant px-5 py-4">
                <div className="flex items-center justify-between">
                  <span className="text-[10px] text-on-surface-variant">Obsidian Financial Pro</span>
                  <ThemeToggle compact />
                </div>
              </div>
            </aside>
          </div>
        )}

        <main className="flex-1 overflow-y-auto px-4 py-4 pb-24">{children}</main>
        <BottomNav />
      </div>
    </div>
  );
}
