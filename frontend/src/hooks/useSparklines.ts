import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { SparklineSeries } from "@/types";

export function useSparklines(symbols: string[], enabled = true) {
  const key = symbols.join(",");
  const [map, setMap] = useState<Record<string, SparklineSeries>>({});

  useEffect(() => {
    if (!enabled || !symbols.length) {
      if (!symbols.length) setMap({});
      return;
    }

    let cancelled = false;
    const timer = window.setTimeout(() => {
      api
        .getSparklines(symbols)
        .then((items) => {
          if (cancelled) return;
          const next: Record<string, SparklineSeries> = {};
          items.forEach((item) => {
            next[item.symbol] = item;
          });
          setMap(next);
        })
        .catch(() => {
          if (!cancelled) setMap({});
        });
    }, 120);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [enabled, key]);

  return map;
}
