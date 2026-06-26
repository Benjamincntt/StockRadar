import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Card, SectionTitle } from "@/components/ui/Card";
import { theme } from "@/theme/tokens";

export function JobsPage() {
  const [status, setStatus] = useState<Awaited<ReturnType<typeof api.getJob1Status>> | null>(null);
  const [running, setRunning] = useState<"fast" | "night" | null>(null);
  const [lastResult, setLastResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refreshStatus = useCallback(async () => {
    try {
      const s = await api.getJob1Status();
      setStatus(s);
      if (!s.isRunning) setRunning(null);
    } catch {
      setStatus(null);
    }
  }, []);

  useEffect(() => {
    refreshStatus();
    const id = setInterval(refreshStatus, status?.isRunning ? 3000 : 15000);
    return () => clearInterval(id);
  }, [refreshStatus, status?.isRunning]);

  const runJob1 = async (mode: "fast" | "night") => {
    setRunning(mode);
    setError(null);
    setLastResult(null);
    try {
      const result = mode === "fast" ? await api.runJob1Fast() : await api.runJob1Night();
      setLastResult(
        `Universe: ${result.symbolsInUniverse}/${result.symbolsTotal} mã · ${result.symbolsExcluded} loại · ${result.barsWritten} nến`,
      );
      await refreshStatus();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không chạy được Job 1. Kiểm tra API và VITE_SYNC_API_KEY.");
    } finally {
      setRunning(null);
    }
  };

  return (
    <div className="space-y-4">
      <Card>
        <SectionTitle
          title="Job 1 — Universe & Backfill"
          subtitle="Lọc HOSE+HNX+UPCOM · TB KL ≥100k/30 phiên · loại IPO 1 năm · không hạn chế GD"
        />

        {status && (
          <div
            className="mb-4 rounded-2xl border px-3 py-3 text-sm"
            style={{ borderColor: theme.border, backgroundColor: theme.surfaceMuted }}
          >
            <p className="font-medium text-gray-900">
              {status.isRunning ? "Đang chạy…" : "Sẵn sàng"}
              {status.isRunning && status.currentSymbol
                ? ` · ${status.currentSymbol}`
                : ""}
            </p>
            {status.isRunning && (
              <p className="mt-1 text-gray-600">
                Tiến độ: {status.processed}/{status.total} ({status.percentComplete}%)
              </p>
            )}
          </div>
        )}

        <div className="flex flex-col gap-2 sm:flex-row">
          <button
            type="button"
            disabled={!!running || status?.isRunning}
            onClick={() => runJob1("fast")}
            className="flex-1 rounded-xl px-4 py-3 text-sm font-semibold text-white disabled:opacity-60"
            style={{ backgroundColor: theme.green }}
          >
            {running === "fast" ? "Đang chạy (nhanh)…" : "Chạy Job 1 — nhanh"}
          </button>
          <button
            type="button"
            disabled={!!running || status?.isRunning}
            onClick={() => runJob1("night")}
            className="flex-1 rounded-xl px-4 py-3 text-sm font-semibold text-gray-900 disabled:opacity-60"
            style={{ backgroundColor: theme.surfaceMuted, border: `1px solid ${theme.border}` }}
          >
            {running === "night" ? "Đang chạy (đêm)…" : "Chạy Job 1 — ban đêm"}
          </button>
        </div>

        <p className="mt-3 text-xs text-gray-500">
          Chế độ đêm dùng delay lớn hơn ({">"}1s/mã), phù hợp chạy lúc ít tải. Job 1 không dùng cache KBS.
          Sau Job 1, chạy Job 2 + phân tích hàng ngày như bình thường.
        </p>

        {lastResult && <p className="mt-2 text-sm text-green-700">{lastResult}</p>}
        {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
      </Card>
    </div>
  );
}
