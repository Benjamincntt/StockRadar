import { Link } from "react-router-dom";
import { useLiveQuote } from "@/context/LiveMarketContext";
import { ChangePill } from "@/components/ui/ScorePill";
import { MiniSparkline } from "@/components/ui/MiniSparkline";
import { formatPrice } from "@/lib/utils";
import { laiLoPhanTram, nhanGiaChotLoi, nhanSoPhienConLai } from "@/lib/viTheDangGiu";
import type { SparklineSeries, ViTheV3 } from "@/types";

interface ViTheDangGiuListProps {
  viThe: ViTheV3[];
  sparklines: Record<string, SparklineSeries>;
}

/** Danh sách mã V3 đang giữ: giá mua, giá hiện tại (trực tiếp), % lãi/lỗ so giá mua, giá chốt lời, số phiên còn lại. */
export function ViTheDangGiuList({ viThe, sparklines }: ViTheDangGiuListProps) {
  if (viThe.length === 0) {
    return <p className="text-sm text-on-surface-variant">Chưa giữ mã nào.</p>;
  }
  return (
    <div className="space-y-2">
      {viThe.map((v) => (
        <ViTheDangGiuRow key={v.id} viThe={v} series={sparklines[v.symbol]} />
      ))}
    </div>
  );
}

function ViTheDangGiuRow({ viThe, series }: { viThe: ViTheV3; series?: SparklineSeries }) {
  const live = useLiveQuote(viThe.symbol);
  const giaHienTai = live?.price ?? null;
  const laiLo = laiLoPhanTram(viThe.giaVao, giaHienTai);
  const dongPhu = [
    viThe.giaVao != null ? `Mua ${formatPrice(viThe.giaVao)}` : null,
    nhanGiaChotLoi(viThe),
    nhanSoPhienConLai(viThe),
  ]
    .filter((x): x is string => x !== null)
    .join(" · ");

  return (
    <Link
      to={`/stocks/${viThe.symbol}`}
      className="flex items-center gap-2 rounded-2xl bg-surface-low px-2 py-2.5 transition-colors hover:bg-surface-high lg:gap-4 lg:px-3 lg:py-3"
    >
      <div className="min-w-0 flex-1">
        <div className="flex items-baseline gap-2">
          <span className="font-bold text-on-surface">{viThe.symbol}</span>
          <span className="text-xs font-bold text-on-surface-variant">{viThe.loai}</span>
        </div>
        {dongPhu && <p className="mt-1 text-[11px] leading-snug text-on-surface-variant">{dongPhu}</p>}
      </div>
      {series && series.prices.length >= 2 && (
        <MiniSparkline
          prices={series.prices}
          reference={series.reference}
          positive={(laiLo ?? 0) >= 0}
        />
      )}
      <div className="flex shrink-0 flex-col items-end gap-1">
        <span className="font-data text-sm font-bold text-on-surface">
          {giaHienTai != null ? formatPrice(giaHienTai) : "—"}
        </span>
        {laiLo != null && <ChangePill value={laiLo} />}
      </div>
    </Link>
  );
}
