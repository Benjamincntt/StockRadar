import { useMemo } from "react";
import { useLiveQuote } from "@/context/LiveMarketContext";
import { MiniQuoteStats, MiniSparkline } from "@/components/ui/MiniSparkline";
import type { SparklineSeries } from "@/types";

interface LiveMiniQuoteProps {
  symbol: string;
  fallbackPrice: number;
  fallbackChangePercent: number;
  className?: string;
}

export function LiveMiniQuote({ symbol, fallbackPrice, fallbackChangePercent, className }: LiveMiniQuoteProps) {
  const live = useLiveQuote(symbol);
  const price = live?.price ?? fallbackPrice;
  const changePercent = live?.changePercent ?? fallbackChangePercent;

  return <MiniQuoteStats price={price} changePercent={changePercent} className={className} />;
}

interface LiveMiniSparklineProps {
  symbol: string;
  series?: SparklineSeries;
  fallbackChangePercent: number;
  className?: string;
}

export function LiveMiniSparkline({
  symbol,
  series,
  fallbackChangePercent,
  className,
}: LiveMiniSparklineProps) {
  const live = useLiveQuote(symbol);

  const { prices, reference, positive } = useMemo(() => {
    const base = series?.prices ?? [];
    const ref = series?.reference;
    const livePrice = live?.price;
    const prices =
      livePrice != null && base.length > 0
        ? [...base.slice(0, -1), livePrice]
        : base.length > 0
          ? base
          : livePrice != null
            ? [livePrice, livePrice]
            : [];

    const changePercent = live?.changePercent ?? fallbackChangePercent;
    return {
      prices,
      reference: ref,
      positive: changePercent >= 0,
    };
  }, [series, live, fallbackChangePercent]);

  return (
    <MiniSparkline
      prices={prices}
      reference={reference}
      positive={positive}
      className={className}
    />
  );
}
