import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Card, SectionTitle } from "@/components/ui/Card";

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
      <div>
        <h1 className="text-xl font-bold text-on-surface">Jobs</h1>
        <p className="mt-1 text-xs text-on-surface-variant">Quản lý đồng bộ dữ liệu</p>
      </div>

      <Card>
        <SectionTitle
          title="Job 1 — Universe & Backfill"
          subtitle="Lọc HOSE+HNX+UPCOM · TB KL ≥100k/30 phiên · loại IPO 1 năm · không hạn chế GD"
        />

        {status && (
          <div className="mb-4 rounded-2xl border border-outline-variant bg-surface-low px-3 py-3 text-sm">
            <p className="font-medium text-on-surface">
              {status.isRunning ? "Đang chạy…" : "Sẵn sàng"}
              {status.isRunning && status.currentSymbol ? ` · ${status.currentSymbol}` : ""}
            </p>
            {status.isRunning && (
              <p className="mt-1 text-on-surface-variant">
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
            className="flex-1 rounded-xl bg-primary px-4 py-3 text-sm font-semibold text-on-primary disabled:opacity-60"
          >
            {running === "fast" ? "Đang chạy (nhanh)…" : "Chạy Job 1 — nhanh"}
          </button>
          <button
            type="button"
            disabled={!!running || status?.isRunning}
            onClick={() => runJob1("night")}
            className="flex-1 rounded-xl border border-outline-variant bg-surface-high px-4 py-3 text-sm font-semibold text-on-surface disabled:opacity-60"
          >
            {running === "night" ? "Đang chạy (đêm)…" : "Chạy Job 1 — ban đêm"}
          </button>
        </div>

        <p className="mt-3 text-xs text-on-surface-variant">
          Chế độ đêm dùng delay lớn hơn ({">"}1s/mã), phù hợp chạy lúc ít tải. Job 1 không dùng cache KBS.
          Sau Job 1, chạy Job 2 + phân tích hàng ngày như bình thường.
        </p>

        {lastResult && <p className="mt-2 text-sm text-primary">{lastResult}</p>}
        {error && <p className="mt-2 text-sm text-negative">{error}</p>}
      </Card>
    </div>
  );
}
