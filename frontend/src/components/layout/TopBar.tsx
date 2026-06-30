import { useNavigate } from "react-router-dom";
import { Menu, LogOut } from "lucide-react";
import { clearAuth, getUser, isLoggedIn } from "@/lib/auth";
import { LiveStatusBadge } from "@/components/layout/LiveStatusBadge";
import { ThemeToggle } from "@/components/ui/ThemeToggle";
import { AppLogo } from "@/components/ui/AppLogo";
import { useTheme } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

interface TopBarProps {
  onMenuClick?: () => void;
}

export function TopBar({ onMenuClick }: TopBarProps) {
  const navigate = useNavigate();
  const { mode } = useTheme();
  const loggedIn = isLoggedIn();
  const user = getUser();
  const isLight = mode === "light";

  const logout = () => {
    clearAuth();
    navigate("/");
    window.location.reload();
  };

  return (
    <header
      className={cn(
        "app-header sticky top-0 z-30 flex h-14 shrink-0 items-center justify-between border-b border-outline-variant/30 px-4 shadow-sm lg:pl-6",
        isLight ? "shadow-[0_1px_3px_rgba(15,23,42,0.05)]" : "shadow-lg",
      )}
    >
      <button
        type="button"
        onClick={onMenuClick}
        className="flex h-9 w-9 items-center justify-center rounded-full bg-surface-low text-on-surface lg:hidden"
        aria-label="Menu"
      >
        <Menu className="h-5 w-5" />
      </button>

      {/* Desktop: sidebar đã có logo — header chỉ spacer */}
      <div className="hidden min-w-0 flex-1 lg:block" />

      {/* Mobile: icon JUICE giữa */}
      <div className="absolute left-1/2 flex -translate-x-1/2 items-center lg:hidden">
        <AppLogo variant="mark" size="sm" />
      </div>

      <div className="flex items-center gap-2">
        <LiveStatusBadge inline className="lg:hidden" />
        <ThemeToggle compact />
        {loggedIn ? (
          <>
            <span className="hidden max-w-[72px] truncate text-[10px] text-on-surface-variant sm:inline">
              {user?.displayName}
            </span>
            <button
              type="button"
              onClick={logout}
              className="flex h-9 w-9 items-center justify-center rounded-full bg-surface-low text-on-surface-variant"
              aria-label="Đăng xuất"
            >
              <LogOut className="h-4 w-4" />
            </button>
          </>
        ) : (
          <button
            type="button"
            onClick={() => navigate("/login")}
            className={cn(
              "rounded-full px-3.5 py-1.5 text-xs font-semibold transition-colors",
              isLight
                ? "bg-primary text-on-primary hover:opacity-90"
                : "border border-outline-variant bg-surface-low text-on-surface hover:bg-surface-high",
            )}
          >
            Sign In
          </button>
        )}
      </div>
    </header>
  );
}
