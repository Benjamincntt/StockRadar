import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { IntradayMonitorStatus } from "@/types";

const POLL_MS = 15_000;

export function useIntradayMonitorStatus() {
  const [status, setStatus] = useState<IntradayMonitorStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const load = useCallback(async () => {
    try {
      const data = await api.getIntradayMonitorStatus();
      setStatus(data);
      setError(false);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
    const timer = setInterval(() => void load(), POLL_MS);
    return () => clearInterval(timer);
  }, [load]);

  return { status, loading, error, refresh: load };
}
