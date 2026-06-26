import { useNavigate } from "react-router-dom";
import { Menu, LogOut } from "lucide-react";
import { clearAuth, getUser, isLoggedIn } from "@/lib/auth";
import { LiveStatusBadge } from "@/components/layout/LiveStatusBadge";
import { theme } from "@/theme/tokens";

interface TopBarProps {
  onMenuClick?: () => void;
}

export function TopBar({ onMenuClick }: TopBarProps) {
  const navigate = useNavigate();
  const loggedIn = isLoggedIn();
  const user = getUser();

  const logout = () => {
    clearAuth();
    navigate("/");
    window.location.reload();
  };

  return (
    <header
      className="sticky top-0 z-30 flex h-14 shrink-0 items-center justify-between border-b bg-white px-4"
      style={{ borderColor: theme.border }}
    >
      <button
        type="button"
        onClick={onMenuClick}
        className="flex h-9 w-9 items-center justify-center rounded-full"
        style={{ backgroundColor: theme.surfaceMuted }}
        aria-label="Menu"
      >
        <Menu className="h-5 w-5 text-gray-700" />
      </button>

      <h1 className="absolute left-1/2 max-w-[52%] -translate-x-1/2 truncate text-sm font-bold text-gray-900">
        AI Stock Flow Monitor
      </h1>

      <div className="flex items-center gap-2">
        <LiveStatusBadge />
        {loggedIn ? (
          <>
            <span className="hidden max-w-[80px] truncate text-[10px] text-gray-500 sm:inline">
              {user?.displayName}
            </span>
            <button
              type="button"
              onClick={logout}
              className="flex h-9 w-9 items-center justify-center rounded-full"
              style={{ backgroundColor: theme.surfaceMuted }}
              aria-label="Đăng xuất"
            >
              <LogOut className="h-4 w-4 text-gray-600" />
            </button>
          </>
        ) : (
          <button
            type="button"
            onClick={() => navigate("/login")}
            className="rounded-full px-3.5 py-1.5 text-xs font-semibold text-gray-800"
            style={{ backgroundColor: theme.surfaceMuted }}
          >
            Sign In
          </button>
        )}
      </div>
    </header>
  );
}
