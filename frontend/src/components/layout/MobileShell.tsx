import { useState } from "react";
import { NavLink } from "react-router-dom";
import { Bell, ChevronRight, Home, LineChart, Star, Wrench, X } from "lucide-react";
import { TopBar } from "./TopBar";
import { BottomNav } from "./BottomNav";
import { theme } from "@/theme/tokens";

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
    desc: "Khối ngoại · tự doanh · lệnh treo",
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
    to: "/jobs",
    label: "Jobs",
    desc: "Job 1 — cập nhật universe",
    icon: Wrench,
  },
] as const;

export function MobileShell({ children }: MobileShellProps) {
  const [menuOpen, setMenuOpen] = useState(false);

  const closeMenu = () => setMenuOpen(false);

  return (
    <div className="flex min-h-screen justify-center bg-[#e8ecf1]">
      <div
        className="relative flex min-h-screen w-full flex-col bg-[#f4f6f8]"
        style={{ maxWidth: theme.maxWidth }}
      >
        <TopBar onMenuClick={() => setMenuOpen(true)} />

        {menuOpen && (
          <div className="fixed inset-0 z-50 flex justify-center">
            <button
              type="button"
              className="absolute inset-0 bg-black/40 backdrop-blur-[2px]"
              aria-label="Đóng menu"
              onClick={closeMenu}
            />
            <aside
              className="relative flex h-full w-full flex-col bg-white shadow-2xl"
              style={{ maxWidth: theme.maxWidth }}
            >
              <div
                className="flex items-start justify-between gap-3 border-b px-5 pb-4 pt-5"
                style={{ borderColor: theme.border }}
              >
                <div>
                  <p className="text-[10px] font-semibold uppercase tracking-wider text-gray-400">
                    StockRadar
                  </p>
                  <h2 className="mt-0.5 text-lg font-bold text-gray-900">Điều hướng</h2>
                  <p className="mt-1 text-xs text-gray-500">Chọn mục để chuyển trang</p>
                </div>
                <button
                  type="button"
                  onClick={closeMenu}
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full"
                  style={{ backgroundColor: theme.surfaceMuted }}
                  aria-label="Đóng"
                >
                  <X className="h-5 w-5 text-gray-600" />
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
                          ? "border-green-200 bg-green-50"
                          : "border-transparent bg-gray-50 hover:bg-gray-100",
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
                            backgroundColor: isActive ? theme.greenBg : "white",
                            color: isActive ? theme.green : theme.textMuted,
                          }}
                        >
                          <Icon className="h-5 w-5" strokeWidth={isActive ? 2.5 : 2} />
                        </span>
                        <span className="min-w-0 flex-1">
                          <span
                            className="block text-sm font-semibold"
                            style={{ color: isActive ? theme.green : theme.text }}
                          >
                            {link.label}
                          </span>
                          <span className="mt-0.5 block text-xs text-gray-500">{link.desc}</span>
                        </span>
                        <ChevronRight
                          className="h-4 w-4 shrink-0"
                          style={{ color: isActive ? theme.green : theme.textSubtle }}
                        />
                      </>
                      );
                    }}
                  </NavLink>
                ))}
              </nav>

              <div
                className="border-t px-5 py-4 text-center text-[10px] text-gray-400"
                style={{ borderColor: theme.border }}
              >
                AI Stock Flow Monitor
              </div>
            </aside>
          </div>
        )}

        <main className="flex-1 overflow-y-auto px-4 py-4">{children}</main>
        <BottomNav />
      </div>
    </div>
  );
}
