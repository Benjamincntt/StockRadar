import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "@/lib/api";
import { Card, SectionTitle } from "@/components/ui/Card";
import { ScorePill } from "@/components/ui/ScorePill";
import { theme } from "@/theme/tokens";
import type { CriteriaSummary, CriterionAccuracy, CriterionGroupAccuracy } from "@/types";
import { ChevronLeft, TrendingUp } from "lucide-react";

const INDICATOR_MAX_RANK = 10;
const BUNDLE_MAX_RANK = 16;

export function CriteriaSummaryPage() {
  const [data, setData] = useState<CriteriaSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getCriteriaSummary()
      .then(setData)
      .catch(() => setError("Không tải được dữ liệu chấm điểm."));
  }, []);

  if (error) {
    return <p className="text-sm text-red-600">{error}</p>;
  }

  if (!data) {
    return <p className="text-center text-sm text-gray-500">Đang tải phân tích tiêu chí...</p>;
  }

  const indicators = data.criteria
    .filter((c) => c.rank <= INDICATOR_MAX_RANK)
    .sort((a, b) => a.rank - b.rank);
  const bundles = data.criteria
    .filter((c) => c.rank > INDICATOR_MAX_RANK && c.rank <= BUNDLE_MAX_RANK)
    .sort((a, b) => a.rank - b.rank);
  const smartMoney = data.criteria
    .filter((c) => c.group === "Top cơ hội")
    .sort((a, b) => a.rank - b.rank);
  const removeCandidates = data.weeklyReview.filter((w) => w.recommendedAction === "Remove");

  return (
    <div className="space-y-4">
      <Link
        to="/"
        className="inline-flex items-center gap-1 text-sm font-medium text-gray-600"
      >
        <ChevronLeft className="h-4 w-4" />
        Trang chủ
      </Link>

      <Card padding="lg">
        <div className="flex items-start gap-3">
          <span
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl"
            style={{ backgroundColor: theme.greenBg, color: theme.green }}
          >
            <TrendingUp className="h-5 w-5" />
          </span>
          <div>
            <h1 className="text-lg font-bold text-gray-900">Phân tích chỉ báo</h1>
            <p className="mt-1 text-xs text-gray-500">{data.statusMessage}</p>
            {data.weekStartDate && (
              <p className="mt-1 text-xs font-medium" style={{ color: theme.green }}>
                Tuần review: {formatDate(data.weekStartDate)}
                {data.asOfDate ? ` · T-1 ${formatDate(data.asOfDate)}` : ""}
              </p>
            )}
          </div>
        </div>
      </Card>

      {data.criteria.length === 0 ? (
        <Card>
          <p className="text-sm text-gray-500">
            Chưa có dữ liệu. Chạy Job 2 + phân tích sau phiên giao dịch để lưu điểm thực tế vào DB.
          </p>
        </Card>
      ) : (
        <>
          {removeCandidates.length > 0 && (
            <Card>
              <SectionTitle
                title="Đề xuất loại bỏ (tuần này)"
                subtitle="Độ tin cậy 7 ngày &lt; 42% · cân nhắc tắt tiêu chí"
              />
              <ul className="space-y-1.5">
                {removeCandidates.map((w) => (
                  <li key={w.id} className="text-sm text-red-700">
                    ✕ {w.label} ({w.group}) — {w.accuracy7d.toFixed(1)}% · {w.hitCount7d}/{w.totalCount7d}
                  </li>
                ))}
              </ul>
            </Card>
          )}

          <GroupReliabilityCard groups={data.groups} />

          <CriterionGroup
            title="Top 10 chỉ báo đơn"
            subtitle="% khớp · điểm TB · trọng số 7d/30d"
            items={indicators}
            showRank
          />
          <CriterionGroup
            title="Bộ chỉ báo kết hợp"
            subtitle="Mới → Smart Money"
            items={bundles}
            showRank
            rankOffset={10}
          />
          <CriterionGroup
            title="Top cơ hội — SmartMoney"
            subtitle="Logic chấm điểm cốt lõi"
            items={smartMoney}
          />

          {data.topStocks.length > 0 && (
            <Card>
              <SectionTitle title="CP điểm tổng cao (T-1)" subtitle="Lưu trong StockCriterionScores" />
              <ul className="space-y-2">
                {data.topStocks.map((s) => (
                  <li key={s.symbol}>
                    <Link
                      to={`/stocks/${s.symbol}`}
                      className="flex items-center justify-between rounded-xl border px-3 py-2.5"
                      style={{ borderColor: theme.border, backgroundColor: theme.surfaceMuted }}
                    >
                      <div>
                        <span className="font-semibold text-gray-900">{s.symbol}</span>
                        <p className="mt-0.5 text-xs text-gray-500">
                          {s.topCriteria.map((c) => `${c.label} ${c.score}`).join(" · ")}
                        </p>
                      </div>
                      <ScorePill score={s.compositeScore} className="!px-2 !py-1 !text-xs" />
                    </Link>
                  </li>
                ))}
              </ul>
            </Card>
          )}
        </>
      )}
    </div>
  );
}

function GroupReliabilityCard({ groups }: { groups: CriterionGroupAccuracy[] }) {
  if (groups.length === 0) return null;

  return (
    <Card>
      <SectionTitle title="Độ tin cậy theo nhóm" subtitle="Review tuần — Keep / Watch / Remove" />
      <ul className="space-y-2">
        {groups.map((g) => (
          <li
            key={g.groupId}
            className="rounded-xl border px-3 py-2.5"
            style={{ borderColor: theme.border }}
          >
            <div className="flex items-center justify-between gap-2">
              <span className="text-sm font-semibold text-gray-900">{g.groupId}</span>
              <div className="flex items-center gap-2">
                <ActionBadge action={g.recommendedAction} />
                <span className="text-sm font-bold tabular-nums" style={{ color: barColor(g.accuracyPercent) }}>
                  {g.accuracyPercent.toFixed(1)}%
                </span>
              </div>
            </div>
            <p className="mt-1 text-xs text-gray-500">
              Khớp {g.hitCount}/{g.totalCount} · Điểm TB {g.avgScore.toFixed(0)} ·{" "}
              <span className="text-green-700">{g.keepCount} giữ</span>
              {" · "}
              <span className="text-amber-700">{g.watchCount} theo dõi</span>
              {" · "}
              <span className="text-red-700">{g.removeCount} loại</span>
            </p>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function CriterionGroup({
  title,
  subtitle,
  items,
  showRank,
  rankOffset = 0,
}: {
  title: string;
  subtitle: string;
  items: CriterionAccuracy[];
  showRank?: boolean;
  rankOffset?: number;
}) {
  if (items.length === 0) return null;

  return (
    <Card>
      <SectionTitle title={title} subtitle={subtitle} />
      <ul className="space-y-2">
        {items.map((c) => (
          <li
            key={c.id}
            className="rounded-xl border px-3 py-2.5"
            style={{
              borderColor: theme.border,
              opacity: c.isActive ? 1 : 0.55,
            }}
          >
            <div className="flex items-center gap-2">
              {showRank && (
                <span
                  className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg text-xs font-bold"
                  style={{
                    backgroundColor: rankOffset > 0 ? "#eef2ff" : theme.greenBg,
                    color: rankOffset > 0 ? "#4f46e5" : theme.green,
                  }}
                >
                  {rankOffset > 0 ? c.rank - rankOffset : c.rank}
                </span>
              )}
              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-sm font-semibold text-gray-900">{c.label}</span>
                  <div className="flex items-center gap-2">
                    <ActionBadge action={c.recommendedAction} />
                    <AccuracyBadge percent={c.accuracyPercent} />
                  </div>
                </div>
                <p className="text-[11px] text-gray-400">
                  {c.group === "Bộ chỉ báo" ? bundleComponents(c.id) : c.group}
                </p>
              </div>
            </div>
            <div className="mt-2 flex flex-wrap items-center justify-between gap-1 text-xs text-gray-500">
              <span>
                Khớp {c.hitCount}/{c.totalCount} · Điểm TB {c.avgScore.toFixed(0)}
              </span>
              <span>
                W {c.weight.toFixed(2)}× · 7d {c.accuracy7d.toFixed(1)}% · 30d {c.accuracy30d.toFixed(1)}%
              </span>
            </div>
            <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-gray-100">
              <div
                className="h-full rounded-full transition-all"
                style={{
                  width: `${Math.min(100, c.accuracyPercent)}%`,
                  backgroundColor: barColor(c.accuracyPercent),
                }}
              />
            </div>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function ActionBadge({ action }: { action: "Keep" | "Watch" | "Remove" }) {
  const styles = {
    Keep: { bg: theme.greenBg, color: theme.green, label: "Giữ" },
    Watch: { bg: "#fffbeb", color: "#b45309", label: "Theo dõi" },
    Remove: { bg: "#fef2f2", color: theme.red, label: "Loại" },
  }[action];
  return (
    <span
      className="rounded-full px-2 py-0.5 text-[10px] font-semibold"
      style={{ backgroundColor: styles.bg, color: styles.color }}
    >
      {styles.label}
    </span>
  );
}

function AccuracyBadge({ percent }: { percent: number }) {
  return (
    <span className="text-sm font-bold tabular-nums" style={{ color: barColor(percent) }}>
      {percent.toFixed(1)}%
    </span>
  );
}

function barColor(percent: number) {
  if (percent >= 55) return theme.green;
  if (percent >= 45) return "#94a3b8";
  return theme.red;
}

function formatDate(iso: string) {
  const [y, m, d] = iso.split("-");
  return `${d}/${m}/${y}`;
}

const BUNDLE_COMPONENTS: Record<string, string> = {
  BundleBeginner: "EMA + RSI + Volume",
  BundleIntermediate: "EMA + Volume + ATR",
  BundleAdvanced: "VWAP + EMA + Volume + ATR",
  BundleProfessional: "Wyckoff + VSA",
  BundleInstitutional: "Volume Profile + VWAP + Delta",
  BundleSmartMoneyConcept: "SMC + Volume + VWAP",
};

function bundleComponents(id: string) {
  return BUNDLE_COMPONENTS[id] ?? "";
}
