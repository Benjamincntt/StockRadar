import { useNavigate } from "react-router-dom";
import { LogOut } from "lucide-react";
import { clearAuth, getUser, isLoggedIn } from "@/lib/auth";
import { LiveStatusBadge } from "@/components/layout/LiveStatusBadge";
import { ThemeToggle } from "@/components/ui/ThemeToggle";
import { AppLogo } from "@/components/ui/AppLogo";
import { useTheme } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

export function TopBar() {
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
        "app-header sticky top-0 z-30 flex h-16 shrink-0 items-center justify-between border-b border-outline-variant/40 px-4 lg:px-10",
        isLight ? "" : "shadow-lg",
      )}
    >
      <div className="flex min-w-0 flex-1 items-center gap-2">
        <AppLogo variant="mark" size="xs" />
        <span className="text-lg font-black tracking-tight text-primary">JUICE</span>
      </div>

      <div className="flex items-center gap-2 lg:gap-4">
        <LiveStatusBadge inline />
        <ThemeToggle compact />
        {loggedIn ? (
          <>
            <span className="hidden max-w-[96px] truncate text-xs text-on-surface-variant sm:inline">
              {user?.displayName}
            </span>
            <button
              type="button"
              onClick={logout}
              className="flex h-9 w-9 items-center justify-center rounded-full bg-surface-low text-on-surface-variant hover:text-primary"
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
              "rounded-full px-4 py-1.5 text-xs font-bold transition-all active:scale-95",
              isLight
                ? "bg-primary/10 text-primary hover:bg-primary hover:text-on-primary"
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
