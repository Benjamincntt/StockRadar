import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { SparklineSeries } from "@/types";

export function useSparklines(symbols: string[]) {
  const key = symbols.join(",");
  const [map, setMap] = useState<Record<string, SparklineSeries>>({});

  useEffect(() => {
    if (!symbols.length) {
      setMap({});
      return;
    }

    let cancelled = false;
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

    return () => {
      cancelled = true;
    };
  }, [key]);

  return map;
}
