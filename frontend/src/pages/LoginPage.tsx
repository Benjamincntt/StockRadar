import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "@/lib/api";
import { saveAuth } from "@/lib/auth";
import { Card } from "@/components/ui/Card";
import { theme } from "@/theme/tokens";

export function LoginPage() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<"login" | "register">("login");
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
        mode === "login"
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
    <div className="mx-auto min-h-screen max-w-[430px] bg-[#f4f6f8] px-4 py-8">
      <Link to="/" className="text-sm text-gray-500">
        ← Trang chủ
      </Link>
      <h1 className="mt-4 text-2xl font-bold text-gray-900">Sign In</h1>
      <p className="mt-1 text-sm text-gray-500">
        Đăng nhập để có watchlist riêng. Không đăng nhập vẫn dùng được (guest).
      </p>

      <Card className="mt-6 space-y-3">
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => setMode("login")}
            className="flex-1 rounded-xl py-2 text-sm font-semibold"
            style={{
              backgroundColor: mode === "login" ? theme.greenBg : theme.surfaceMuted,
              color: mode === "login" ? theme.green : theme.textMuted,
            }}
          >
            Đăng nhập
          </button>
          <button
            type="button"
            onClick={() => setMode("register")}
            className="flex-1 rounded-xl py-2 text-sm font-semibold"
            style={{
              backgroundColor: mode === "register" ? theme.blueBg : theme.surfaceMuted,
              color: mode === "register" ? theme.blue : theme.textMuted,
            }}
          >
            Đăng ký
          </button>
        </div>

        {mode === "register" && (
          <input
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            placeholder="Tên hiển thị"
            className="w-full rounded-xl border px-4 py-3 text-sm"
            style={{ borderColor: theme.border }}
          />
        )}
        <input
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="Email"
          type="email"
          className="w-full rounded-xl border px-4 py-3 text-sm"
          style={{ borderColor: theme.border }}
        />
        <input
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="Mật khẩu"
          type="password"
          className="w-full rounded-xl border px-4 py-3 text-sm"
          style={{ borderColor: theme.border }}
        />

        {error && <p className="text-sm text-red-600">{error}</p>}

        <button
          type="button"
          disabled={loading}
          onClick={submit}
          className="w-full rounded-2xl py-3 text-sm font-semibold text-white disabled:opacity-60"
          style={{ backgroundColor: theme.green }}
        >
          {loading ? "Đang xử lý..." : mode === "login" ? "Đăng nhập" : "Tạo tài khoản"}
        </button>
      </Card>
    </div>
  );
}
