import { useState } from "react";
import { FilterChips } from "@/components/ui/ScorePill";
import { TradePrintList } from "@/components/trades/TradePrintList";
import { IntradayMonitorStatusLine } from "@/components/alerts/IntradayMonitorStatusLine";
import { useLiveTrades } from "@/hooks/useLiveTrades";

const filters = [
  { key: "All" as const, label: "Tất cả" },
  { key: "Buy" as const, label: "Mua" },
  { key: "Sell" as const, label: "Bán" },
];

export function AlertsPage() {
  const [side, setSide] = useState<"All" | "Buy" | "Sell">("All");
  const { trades, loading } = useLiveTrades(side);

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-bold text-on-surface">Khớp lệnh</h1>
        <p className="mt-1 text-xs text-on-surface-variant">
          Mua / bán · khối lượng · giá — từ bảng giá KBS trong phiên
        </p>
        <IntradayMonitorStatusLine className="mt-2" />
      </div>

      <FilterChips value={side} options={filters} onChange={setSide} />

      <TradePrintList trades={trades} loading={loading} />
    </div>
  );
}
