import { useCallback, useEffect, useMemo, useState } from "react";
import { api } from "@/lib/api";
import { useSymbolSubscriptions } from "@/context/LiveMarketContext";
import { useSparklines } from "@/hooks/useSparklines";
import { ViTheDangGiuList } from "@/components/home/ViTheDangGiuList";
import { Card, SectionTitle } from "@/components/ui/Card";
import { useThemeTokens } from "@/context/ThemeContext";
import type { ViTheV3 } from "@/types";

/** Làm mới danh sách đang giữ để số phiên còn lại và ngưỡng không đứng im khi tab mở lâu. */
const HOLDINGS_REFRESH_MS = 120_000;

export function HomePage() {
  const theme = useThemeTokens();
  const [viThe, setViThe] = useState<ViTheV3[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const symbols = useMemo(() => viThe.map((v) => v.symbol), [viThe]);

  const loadViThe = useCallback(async () => {
    setViThe(await api.getViTheV3DangGiu());
  }, []);

  useEffect(() => {
    loadViThe()
      .catch(() => setError("Không thể tải danh sách đang giữ."))
      .finally(() => setLoading(false));
  }, [loadViThe]);

  // Lỗi khi làm mới nền thì giữ dữ liệu đang hiển thị, không lật sang banner lỗi.
  useEffect(() => {
    const id = setInterval(() => {
      loadViThe().catch(() => undefined);
    }, HOLDINGS_REFRESH_MS);
    return () => clearInterval(id);
  }, [loadViThe]);

  useSymbolSubscriptions(symbols);
  const sparklines = useSparklines(symbols, !loading && symbols.length > 0);

  return (
    <div className="grid gap-6 lg:grid-cols-2 lg:items-start">
      <Card wave>
        <SectionTitle title={viThe.length > 0 ? `Mã V3 đang giữ (${viThe.length})` : "Mã V3 đang giữ"} />
        {loading ? (
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="h-16 animate-pulse rounded-2xl bg-surface-low" />
            ))}
          </div>
        ) : error && viThe.length === 0 ? (
          <div
            className="rounded-2xl border border-outline-variant p-4 text-sm text-negative"
            style={{ backgroundColor: theme.redSoft }}
          >
            {error}
          </div>
        ) : (
          <ViTheDangGiuList viThe={viThe} sparklines={sparklines} />
        )}
      </Card>
    </div>
  );
}
