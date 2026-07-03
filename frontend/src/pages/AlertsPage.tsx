import { useState } from "react";
import { FilterChips } from "@/components/ui/ScorePill";
import { TradePrintList } from "@/components/trades/TradePrintList";
import { IntradayMonitorStatusLine } from "@/components/alerts/IntradayMonitorStatusLine";
import { useLiveTrades } from "@/hooks/useLiveTrades";
import { TRADE_FILTER_OPTIONS, type TradeLabelFilter } from "@/lib/tradeLabels";

export function AlertsPage() {
  const [label, setLabel] = useState<TradeLabelFilter>("All");
  const { trades, loading } = useLiveTrades(label);

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-bold text-on-surface">Khớp lệnh</h1>
        <p className="mt-1 text-xs text-on-surface-variant">
          Lô lớn + nhãn VSA + dòng tiền NN/Tự doanh — không suy đoán mua/bán chủ động
        </p>
        <IntradayMonitorStatusLine className="mt-2" />
      </div>

      <FilterChips value={label} options={TRADE_FILTER_OPTIONS} onChange={setLabel} />

      <TradePrintList trades={trades} loading={loading} />
    </div>
  );
}
