import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "@/lib/api";
import { Card, SectionTitle } from "@/components/ui/Card";
import { ScorePill } from "@/components/ui/ScorePill";
import { useThemeTokens } from "@/context/ThemeContext";
import type { buildThemeTokens } from "@/theme/tokens";
import type { CriteriaSummary, CriterionAccuracy, CriterionGroupAccuracy } from "@/types";
import { ChevronLeft, TrendingUp } from "lucide-react";

const INDICATOR_MAX_RANK = 10;
const BUNDLE_MAX_RANK = 16;

function criterionPercent(c: CriterionAccuracy) {
  return c.reliabilityScore ?? c.accuracyPercent;
}

function sortByPercentDesc(items: CriterionAccuracy[]) {
  return [...items].sort((a, b) => criterionPercent(b) - criterionPercent(a));
}

export function CriteriaSummaryPage() {
  const theme = useThemeTokens();
  const [data, setData] = useState<CriteriaSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getCriteriaSummary()
      .then(setData)
      .catch(() => setError("Không tải được dữ liệu chấm điểm."));
  }, []);

  if (error) {
    return <p className="text-sm text-negative">{error}</p>;
  }

  if (!data) {
    return <p className="text-center text-sm text-on-surface-variant">Đang tải phân tích tiêu chí...</p>;
  }

  const indicators = sortByPercentDesc(
    data.criteria.filter((c) => c.rank <= INDICATOR_MAX_RANK),
  );
  const bundles = sortByPercentDesc(
    data.criteria.filter((c) => c.rank > INDICATOR_MAX_RANK && c.rank <= BUNDLE_MAX_RANK),
  );
  const smartMoney = sortByPercentDesc(
    data.criteria.filter((c) => c.group === "Top cơ hội"),
  );
  const removeCandidates = data.weeklyReview
    .filter((w) => w.recommendedAction === "Remove" && w.totalCount7d >= 30)
    .sort(
      (a, b) =>
        (a.reliability7d ?? a.accuracy7d) - (b.reliability7d ?? b.accuracy7d),
    )
    .slice(0, 5);

  return (
    <div className="space-y-4">
      <Link
        to="/"
        className="inline-flex items-center gap-1 text-sm font-medium text-on-surface-variant hover:text-primary"
      >
        <ChevronLeft className="h-4 w-4" />
        Trang chủ
      </Link>

      <Card padding="lg">
        <div className="flex items-start gap-3">
          <span
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl"
            style={{ backgroundColor: theme.greenBg, color: theme.primary }}
          >
            <TrendingUp className="h-5 w-5" />
          </span>
          <div>
            <h1 className="text-lg font-bold text-on-surface">Phân tích chỉ báo</h1>
            <p className="mt-1 text-xs text-on-surface-variant">{data.statusMessage}</p>
            {data.weekStartDate && (
              <p className="mt-1 text-xs font-medium text-primary">
                Tuần review: {formatDate(data.weekStartDate)}
                {data.asOfDate ? ` · T-1 ${formatDate(data.asOfDate)}` : ""}
              </p>
            )}
          </div>
        </div>
      </Card>

      {data.criteria.length === 0 ? (
        <Card>
          <p className="text-sm text-on-surface-variant">
            Chưa có dữ liệu. Chạy Job 2 + phân tích sau phiên giao dịch để lưu điểm thực tế vào DB.
          </p>
        </Card>
      ) : (
        <>
          {removeCandidates.length > 0 && (
            <Card>
              <SectionTitle
                title={`Cần xem lại (${removeCandidates.length})`}
                subtitle="7 ngày gần nhất · R &lt;42% và edge &lt;3% · tối đa 5 tiêu chí yếu nhất"
              />
              <ul className="space-y-2">
                {removeCandidates.map((w) => {
                  const r = w.reliability7d ?? w.accuracy7d;
                  const edge = w.edge7d ?? 0;
                  return (
                    <li
                      key={w.id}
                      className="flex items-center justify-between gap-2 rounded-xl border border-outline-variant px-3 py-2"
                    >
                      <div className="min-w-0">
                        <p className="truncate text-sm font-semibold text-on-surface">
                          {w.label}
                        </p>
                        <p className="text-[11px] text-on-surface-variant">{w.group}</p>
                      </div>
                      <div className="shrink-0 text-right text-xs tabular-nums">
                        <p className="font-semibold text-negative">R {r.toFixed(0)}%</p>
                        <p className="text-on-surface-variant">
                          edge {edge >= 0 ? "+" : ""}
                          {edge.toFixed(1)}% · {w.hitCount7d}/{w.totalCount7d}
                        </p>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </Card>
          )}

          <GroupReliabilityCard groups={[...data.groups].sort((a, b) => {
            const pa = a.reliabilityScore ?? a.accuracyPercent;
            const pb = b.reliabilityScore ?? b.accuracyPercent;
            return pb - pa;
          })} />

          <CriterionGroup
            title="Top 10 chỉ báo đơn"
            subtitle="Sắp xếp theo reliability / độ khớp giảm dần"
            items={indicators}
            showRank
          />
          <CriterionGroup
            title="Bộ chỉ báo kết hợp"
            subtitle="Sắp xếp theo reliability / độ khớp giảm dần"
            items={bundles}
            showRank
          />
          <CriterionGroup
            title="Top cơ hội — SmartMoney"
            subtitle="Sắp xếp theo reliability / độ khớp giảm dần"
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
                      className="flex items-center justify-between rounded-xl border border-outline-variant bg-surface-low px-3 py-2.5"
                    >
                      <div>
                        <span className="font-semibold text-on-surface">{s.symbol}</span>
                        <p className="mt-0.5 text-xs text-on-surface-variant">
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
  const theme = useThemeTokens();
  if (groups.length === 0) return null;

  return (
    <Card>
      <SectionTitle title="Độ tin cậy theo nhóm" subtitle="Setup trend · reliability + edge · Keep / Watch / Remove" />
      <ul className="space-y-2">
        {groups.map((g) => (
          <li
            key={g.groupId}
            className="rounded-xl border border-outline-variant px-3 py-2.5"
          >
            <div className="flex items-center justify-between gap-2">
              <span className="text-sm font-semibold text-on-surface">{g.groupId}</span>
              <div className="flex items-center gap-2">
                <ActionBadge action={g.recommendedAction} />
                <span className="text-sm font-bold tabular-nums" style={{ color: scoreBarColor(g.accuracyPercent, theme) }}>
                  {g.accuracyPercent.toFixed(1)}%
                </span>
              </div>
            </div>
            <p className="mt-1 text-xs text-on-surface-variant">
              Khớp {g.hitCount}/{g.totalCount} · Điểm TB {g.avgScore.toFixed(0)}
              {g.edgePercent != null ? ` · Edge +${g.edgePercent.toFixed(1)}%` : ""}
              {g.reliabilityScore != null ? ` · R ${g.reliabilityScore.toFixed(0)}` : ""} ·{" "}
              <span className="text-primary">{g.keepCount} giữ</span>
              {" · "}
              <span className="text-warning">{g.watchCount} theo dõi</span>
              {" · "}
              <span className="text-negative">{g.removeCount} loại</span>
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
}: {
  title: string;
  subtitle: string;
  items: CriterionAccuracy[];
  showRank?: boolean;
}) {
  const theme = useThemeTokens();
  if (items.length === 0) return null;

  return (
    <Card>
      <SectionTitle title={title} subtitle={subtitle} />
      <ul className="space-y-2">
        {items.map((c, index) => (
          <li
            key={c.id}
            className="rounded-xl border border-outline-variant px-3 py-2.5"
            style={{
              opacity: c.isActive ? 1 : 0.55,
            }}
          >
            <div className="flex items-center gap-2">
              {showRank && (
                <span
                  className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg text-xs font-bold"
                  style={{
                    backgroundColor: theme.greenBg,
                    color: theme.primary,
                  }}
                >
                  {index + 1}
                </span>
              )}
              <div className="min-w-0 flex-1">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-sm font-semibold text-on-surface">{c.label}</span>
                  <div className="flex items-center gap-2">
                    <ActionBadge action={c.recommendedAction} />
                    <AccuracyBadge percent={c.reliabilityScore ?? c.accuracyPercent} />
                  </div>
                </div>
                <p className="text-[11px] text-on-surface-variant">
                  {c.group === "Bộ chỉ báo" ? bundleComponents(c.id) : c.group}
                </p>
              </div>
            </div>
            <div className="mt-2 flex flex-wrap items-center justify-between gap-1 text-xs text-on-surface-variant">
              <span>
                Khớp {c.hitCount}/{c.totalCount} · Điểm TB {c.avgScore.toFixed(0)}
                {c.edgePercent != null ? ` · Edge +${c.edgePercent.toFixed(1)}%` : ""}
                {c.avgMfePercent != null ? ` · MFE ${c.avgMfePercent.toFixed(1)}%` : ""}
                {c.invalidationRatePercent != null ? ` · Rũ nền ${c.invalidationRatePercent.toFixed(0)}%` : ""}
              </span>
              <span>
                W {c.weight.toFixed(2)}× · 7d {c.accuracy7d.toFixed(1)}% · baseline{" "}
                {(c.baselinePercent ?? 0).toFixed(1)}%
              </span>
            </div>
            {c.buckets && c.buckets.length > 0 && (
              <p className="mt-1 text-[10px] text-on-surface-variant">
                Bucket: {c.buckets.map((b) => `${b.bucketId} ${b.accuracyPercent.toFixed(0)}%`).join(" · ")}
              </p>
            )}
            {c.phases && c.phases.length > 0 && (
              <p className="mt-0.5 text-[10px] text-on-surface-variant">
                Pha TT: {c.phases.map((p) => `${phaseLabel(p.phase)} ${p.accuracyPercent.toFixed(0)}%`).join(" · ")}
              </p>
            )}
            <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-surface-high">
              <div
                className="h-full rounded-full transition-all"
                style={{
                  width: `${Math.min(100, c.reliabilityScore ?? c.accuracyPercent)}%`,
                  backgroundColor: scoreBarColor(c.reliabilityScore ?? c.accuracyPercent, theme),
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
  const theme = useThemeTokens();
  const styles = {
    Keep: { bg: theme.greenBg, color: theme.primary, label: "Giữ" },
    Watch: { bg: theme.amberBg, color: theme.amber, label: "Theo dõi" },
    Remove: { bg: theme.redBg, color: theme.red, label: "Loại" },
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
  const theme = useThemeTokens();
  return (
    <span className="text-sm font-bold tabular-nums" style={{ color: scoreBarColor(percent, theme) }}>
      {percent.toFixed(1)}%
    </span>
  );
}

function scoreBarColor(
  percent: number,
  theme: ReturnType<typeof buildThemeTokens>,
) {
  if (percent >= 55) return theme.primary;
  if (percent >= 45) return theme.textMuted;
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

function phaseLabel(phase: string) {
  return (
    {
      Favorable: "Thuận",
      Neutral: "TB",
      Unfavorable: "Xấu",
    }[phase] ?? phase
  );
}
