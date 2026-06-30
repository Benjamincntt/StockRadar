import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ArrowRight, ShieldCheck } from "lucide-react";
import { api } from "@/lib/api";
import { saveAuth } from "@/lib/auth";
import { ThemeToggle } from "@/components/ui/ThemeToggle";
import { AppLogo } from "@/components/ui/AppLogo";
import { cn } from "@/lib/utils";

export function LoginPage() {
  const navigate = useNavigate();
  const [authMode, setAuthMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async (e?: React.FormEvent) => {
    e?.preventDefault();
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
    } catch (err) {
      setError(err instanceof Error ? err.message : "Đăng nhập thất bại");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="relative flex min-h-screen flex-col overflow-hidden bg-background wave-bg">
      <div className="pointer-events-none absolute inset-0 overflow-hidden">
        <div className="absolute -right-[10%] -top-[10%] h-[50vh] w-[50vh] rounded-full bg-primary/5 blur-[120px]" />
        <div className="absolute -bottom-[10%] -left-[5%] h-[40vh] w-[40vh] rounded-full bg-secondary/10 blur-[100px]" />
      </div>

      <header className="app-header relative z-10 flex h-14 items-center justify-between border-b border-outline-variant/30 px-4 lg:px-10">
        <Link to="/" className="text-sm font-medium text-primary hover:opacity-80">
          ← Trang chủ
        </Link>
        <AppLogo variant="mark" size="sm" />
        <ThemeToggle compact />
      </header>

      <div className="relative z-10 flex flex-1 flex-col items-center justify-center px-4 py-10">
        <div className="glass-card w-full max-w-md rounded-2xl p-8 lg:p-10">
          <header className="mb-8 text-center">
            <AppLogo variant="full" className="mx-auto max-w-[140px]" />
            <h1 className="mt-6 text-2xl font-semibold tracking-tight text-on-surface">
              {authMode === "login" ? "Welcome back" : "Tạo tài khoản"}
            </h1>
            <p className="mt-2 flex items-center justify-center gap-2 text-sm text-on-surface-variant">
              <ShieldCheck className="h-4 w-4 text-primary" />
              Đăng nhập để quản lý watchlist cá nhân
            </p>
          </header>

          <div className="mb-6 flex gap-1 rounded-xl bg-surface-low p-1">
            {(["login", "register"] as const).map((tab) => (
              <button
                key={tab}
                type="button"
                onClick={() => setAuthMode(tab)}
                className={cn(
                  "flex-1 rounded-lg py-2.5 text-sm font-semibold transition-all",
                  authMode === tab
                    ? "bg-surface-lowest text-primary shadow-sm"
                    : "text-on-surface-variant hover:text-on-surface",
                )}
              >
                {tab === "login" ? "Đăng nhập" : "Đăng ký"}
              </button>
            ))}
          </div>

          <form className="space-y-4" onSubmit={submit}>
            {authMode === "register" && (
              <div className="space-y-1.5">
                <label className="label-caps text-on-surface-variant">Tên hiển thị</label>
                <input
                  value={displayName}
                  onChange={(e) => setDisplayName(e.target.value)}
                  placeholder="Tên của bạn"
                  className="input-obsidian w-full rounded-xl px-4 py-3.5 text-sm text-on-surface"
                />
              </div>
            )}
            <div className="space-y-1.5">
              <label className="label-caps text-on-surface-variant">Email</label>
              <input
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="email@example.com"
                type="email"
                required
                className="input-obsidian w-full rounded-xl px-4 py-3.5 text-sm text-on-surface"
              />
            </div>
            <div className="space-y-1.5">
              <label className="label-caps text-on-surface-variant">Mật khẩu</label>
              <input
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                type="password"
                required
                className="input-obsidian w-full rounded-xl px-4 py-3.5 text-sm text-on-surface"
              />
            </div>

            {error && <p className="text-sm text-negative">{error}</p>}

            <button
              type="submit"
              disabled={loading}
              className="mt-2 flex w-full items-center justify-center gap-2 rounded-xl bg-primary-container py-4 text-sm font-bold text-on-primary shadow-[0_4px_12px_rgba(0,192,118,0.2)] transition-all hover:opacity-90 active:scale-[0.98] disabled:opacity-60"
            >
              {loading ? "Đang xử lý..." : authMode === "login" ? "Sign In" : "Tạo tài khoản"}
              {!loading && <ArrowRight className="h-4 w-4" />}
            </button>
          </form>
        </div>

        <p className="mt-8 text-center text-xs text-on-surface-variant/70">
          © {new Date().getFullYear()} JUICE · Smart Money Monitor
        </p>
      </div>
    </div>
  );
}
