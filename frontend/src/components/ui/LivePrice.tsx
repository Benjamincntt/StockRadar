import { cn, formatPrice } from "@/lib/utils";
import { useLiveQuote, usePriceFlash } from "@/context/LiveMarketContext";

interface LivePriceProps {
  symbol: string;
  fallbackPrice: number;
  className?: string;
}

export function LivePrice({ symbol, fallbackPrice, className }: LivePriceProps) {
  const live = useLiveQuote(symbol);
  const price = live?.price ?? fallbackPrice;
  const flash = usePriceFlash(price);

  return (
    <span
      className={cn(
        "inline-block rounded-md px-0.5 transition-colors duration-500",
        flash === "up" && "live-flash-up",
        flash === "down" && "live-flash-down",
        className,
      )}
    >
      {formatPrice(price)}
    </span>
  );
}
