import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { saveAuth } from "@/lib/auth";
import { ThemeToggle } from "@/components/ui/ThemeToggle";
import { useTheme, useThemeTokens } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

export function LoginPage() {
  const navigate = useNavigate();
  const { mode } = useTheme();
  const theme = useThemeTokens();
  const isLight = mode === "light";
  const [authMode, setAuthMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      const result =
        authMode === "login"
          ? await api.login(email, password)
          : await api.register(email, password, displayName || email.split("@")[0]);

      saveAuth({
        userId: result.userId,
        email: result.email,
        displayName: result.displayName,
        token: result.token,
      });
      navigate("/");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Đăng nhập thất bại");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen flex-col bg-background">
      <header className="app-header flex h-14 items-center justify-between border-b border-outline-variant/30 px-4">
        <Link to="/" className="text-sm font-medium text-primary hover:opacity-80">
          ← Trang chủ
        </Link>
        <span className="text-sm font-bold text-primary">Obsidian Financial</span>
        <ThemeToggle compact />
      </header>

      <div className="flex flex-1 flex-col items-center justify-center px-4 py-10">
        <div
          className={cn(
            "w-full max-w-md rounded-xl p-6 lg:p-8",
            isLight ? "glass-card" : "glass-card rounded-2xl",
          )}
        >
          <p className="label-caps text-center text-on-surface-variant">StockRadar</p>
          <h1 className="mt-1 text-center text-2xl font-bold text-on-surface">Sign In</h1>
          <p className="mt-2 text-center text-sm text-on-surface-variant">
            Đăng nhập để xem và quản lý danh sách watchlist cá nhân của bạn.
          </p>

          <div className="mt-6 flex gap-1 rounded-lg bg-surface-low p-1">
            {(["login", "register"] as const).map((tab) => {
              const active = authMode === tab;
              return (
                <button
                  key={tab}
                  type="button"
                  onClick={() => setAuthMode(tab)}
                  className={cn(
                    "flex-1 rounded-md py-2 text-sm font-semibold transition-all",
                    active
                      ? isLight
                        ? "bg-surface-lowest text-primary shadow-sm"
                        : "text-primary"
                      : "text-on-surface-variant hover:text-on-surface",
                  )}
                  style={
                    active && !isLight
                      ? { backgroundColor: theme.greenBg, color: theme.primary }
                      : undefined
                  }
                >
                  {tab === "login" ? "Đăng nhập" : "Đăng ký"}
                </button>
              );
            })}
          </div>

          <div className="mt-4 space-y-3">
            {authMode === "register" && (
              <input
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                placeholder="Tên hiển thị"
                className="input-obsidian w-full rounded-lg px-4 py-3 text-sm text-on-surface"
              />
            )}
            <input
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="Email"
              type="email"
              className="input-obsidian w-full rounded-lg px-4 py-3 text-sm text-on-surface"
            />
            <input
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Mật khẩu"
              type="password"
              className="input-obsidian w-full rounded-lg px-4 py-3 text-sm text-on-surface"
            />
          </div>

          {error && <p className="mt-3 text-sm text-negative">{error}</p>}

          <button
            type="button"
            disabled={loading}
            onClick={submit}
            className="mt-5 w-full rounded-lg bg-primary py-3.5 text-sm font-bold text-on-primary shadow-md transition-opacity hover:opacity-90 disabled:opacity-60"
          >
            {loading ? "Đang xử lý..." : authMode === "login" ? "Đăng nhập" : "Tạo tài khoản"}
          </button>
        </div>
      </div>
    </div>
  );
}
