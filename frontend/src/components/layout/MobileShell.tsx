import { useState } from "react";
import { NavLink } from "react-router-dom";
import { ChevronRight, X } from "lucide-react";
import { TopBar } from "./TopBar";
import { BottomNav } from "./BottomNav";
import { SidebarNav } from "./SidebarNav";
import { ThemeToggle } from "@/components/ui/ThemeToggle";
import { AppLogo } from "@/components/ui/AppLogo";
import { mainNavLinks } from "@/components/layout/navConfig";
import { useThemeTokens } from "@/context/ThemeContext";

interface MobileShellProps {
  children: React.ReactNode;
}

export function MobileShell({ children }: MobileShellProps) {
  const theme = useThemeTokens();
  const [menuOpen, setMenuOpen] = useState(false);
  const closeMenu = () => setMenuOpen(false);

  return (
    <div className="flex min-h-screen bg-background">
      <SidebarNav className="sticky top-0 hidden h-screen w-64 shrink-0 xl:w-72 lg:flex" />

      <div className="flex min-w-0 flex-1 flex-col wave-bg">
        <TopBar onMenuClick={() => setMenuOpen(true)} />

        {menuOpen && (
          <div className="fixed inset-0 z-50 lg:hidden">
            <button
              type="button"
              className="absolute inset-0 bg-black/50 backdrop-blur-[2px]"
              aria-label="Đóng menu"
              onClick={closeMenu}
            />
            <aside className="relative flex h-full w-full max-w-sm flex-col border-l border-outline-variant bg-surface-low shadow-2xl">
              <div className="flex items-start justify-between gap-3 border-b border-outline-variant px-5 pb-4 pt-5">
                <div>
                  <AppLogo variant="full" className="max-w-[120px]" />
                  <h2 className="mt-3 text-base font-semibold text-on-surface">Điều hướng</h2>
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
                {mainNavLinks.map((link) => (
                  <NavLink
                    key={link.to}
                    to={link.to}
                    end={link.end}
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
                  <span className="text-[10px] text-on-surface-variant">Giao diện</span>
                  <ThemeToggle compact />
                </div>
              </div>
            </aside>
          </div>
        )}

        <main className="flex-1 overflow-y-auto px-4 py-5 pb-24 lg:px-10 lg:py-8 lg:pb-10">
          <div className="page-container">{children}</div>
        </main>

        <BottomNav />
      </div>
    </div>
  );
}
