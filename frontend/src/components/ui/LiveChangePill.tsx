import { ChangePill } from "@/components/ui/ScorePill";
import { useLiveQuote } from "@/context/LiveMarketContext";

interface LiveChangePillProps {
  symbol: string;
  fallback: number;
  className?: string;
}

export function LiveChangePill({ symbol, fallback, className }: LiveChangePillProps) {
  const live = useLiveQuote(symbol);
  return <ChangePill value={live?.changePercent ?? fallback} className={className} />;
}
