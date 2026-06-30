import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "@/lib/api";
import { formatPercent, formatShortDate } from "@/lib/utils";
import type { OpportunityPerformanceSummary } from "@/types";
import { Card, SectionTitle } from "@/components/ui/Card";
import { useThemeTokens } from "@/context/ThemeContext";

export function PerformancePage() {
  const theme = useThemeTokens();
  const [data, setData] = useState<OpportunityPerformanceSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getPerformanceSummary()
      .then(setData)
      .catch(() => setError("Không tải được dữ liệu hiệu quả."));
  }, []);

  if (error) {
    return <p className="text-sm text-negative">{error}</p>;
  }

  if (!data) {
    return <p className="text-center text-sm text-on-surface-variant">Đang tải...</p>;
  }

  const review = data.weeklyReview;

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-bold text-on-surface">Hiệu quả Top cơ hội</h1>
        <p className="mt-1 text-xs text-on-surface-variant">
          Review tự động hàng tuần · đo T+2.5 phiên VN sau điểm vào / thông báo Master
        </p>
      </div>

      {data.statusMessage && !review && (
        <Card>
          <p className="text-sm text-on-surface-variant">{data.statusMessage}</p>
        </Card>
      )}

      {data.calibration && data.calibration.totalSamples > 0 && (
        <Card>
          <SectionTitle
            title="Calibration P(thành công)"
            subtitle={
              data.calibration.updatedAt
                ? `Cập nhật ${new Date(data.calibration.updatedAt).toLocaleString("vi-VN")}`
                : "Hiệu chỉnh từ setup đã đo T+2.5"
            }
          />
          <div className="mt-2 grid grid-cols-2 gap-2">
            <MetricPill label="Hệ số global" value={`×${data.calibration.globalFactor.toFixed(3)}`} accent />
            <MetricPill
              label="Lệch dự báo"
              value={`${data.calibration.predictionBiasPercent >= 0 ? "+" : ""}${data.calibration.predictionBiasPercent}%`}
              danger={Math.abs(data.calibration.predictionBiasPercent) > 10}
            />
          </div>
          <p className="mt-2 text-xs text-on-surface-variant">
            {data.calibration.totalSamples} setup có P(hit) lúc vào Top · dương = dự báo lạc quan hơn thực tế
          </p>
          {data.calibration.buckets.length > 0 && (
            <ul className="mt-3 space-y-1.5">
              {data.calibration.buckets.map((b) => (
                <li
                  key={b.bucketId}
                  className="flex items-center justify-between rounded-lg bg-surface-low px-2.5 py-2 text-xs"
                >
                  <span className="text-on-surface-variant">
                    P {b.bucketId} · n={b.sampleCount}
                  </span>
                  <span className="font-data tabular-nums text-on-surface">
                    dự {b.predictedMidPercent}% → thực {b.actualHitRatePercent}% (×{b.calibrationFactor.toFixed(2)})
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Card>
      )}

      {data.falsePositiveMining && data.falsePositiveMining.flaggedCriteria.length > 0 && (
        <Card>
          <SectionTitle
            title="False positive mining"
            subtitle={`${data.falsePositiveMining.falsePositiveSetups} setup xịt dù P cao · so với ${data.falsePositiveMining.goodSetups} tốt`}
          />
          <p className="mt-1 text-xs text-on-surface-variant">
            Tiêu chí cho điểm cao trên mã xịt — engine đã tự giảm weight
          </p>
          <ul className="mt-3 space-y-1.5">
            {data.falsePositiveMining.flaggedCriteria.map((c) => (
              <li
                key={c.componentId}
                className="rounded-lg border border-outline-variant bg-surface-low px-2.5 py-2 text-xs"
              >
                <p className="font-semibold text-on-surface">{c.label}</p>
                <p className="mt-0.5 text-on-surface-variant">
                  Xịt TB {(c.falsePositiveAvgNorm * 100).toFixed(0)}% điểm vs tốt{" "}
                  {(c.goodAvgNorm * 100).toFixed(0)}% · phạt weight −{(c.weightPenalty * 100).toFixed(0)}%
                </p>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {data.shadowVariants && data.shadowVariants.length > 0 && (
        <Card>
          <SectionTitle
            title="Shadow mode (MinPassScore)"
            subtitle={
              data.shadowStatusMessage ??
              "Chạy song song 58/60/62 — không đổi Top hiển thị"
            }
          />
          <ul className="mt-3 space-y-1.5">
            {data.shadowVariants.map((v) => (
              <li
                key={v.minPassScore}
                className="flex items-center justify-between rounded-lg bg-surface-low px-2.5 py-2 text-xs"
              >
                <span className="text-on-surface">
                  MinPassScore {v.minPassScore}
                  {v.isProduction && " · prod"}
                  {v.isLeader && " · leader"}
                </span>
                <span className="font-data tabular-nums text-on-surface">
                  {v.measuredCount > 0
                    ? `${v.successRatePercent}% (n=${v.measuredCount})`
                    : "chờ đo"}
                </span>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {data.entryTiming && (
        <Card>
          <SectionTitle
            title="Entry timing (học từ lịch sử)"
            subtitle={
              data.entryTiming.preferMasterConfirm
                ? "Nên chờ Mua điểm 1 sau Top"
                : "Top-only và confirm tương đương"
            }
          />
          <div className="mt-2 grid grid-cols-2 gap-2 text-xs">
            <MetricPill
              label={`Top-only (n=${data.entryTiming.topOnlySamples})`}
              value={`${data.entryTiming.topOnlySuccessRate}%`}
            />
            <MetricPill
              label={`Top + Mua điểm 1 (n=${data.entryTiming.confirmSamples})`}
              value={`${data.entryTiming.confirmSuccessRate}%`}
              accent
            />
          </div>
        </Card>
      )}

      {data.shadowWeightVariants && data.shadowWeightVariants.length > 0 && (
        <Card>
          <SectionTitle title="Shadow weights" subtitle="×0.9 / ×1.0 / ×1.1 criterion weights" />
          <ul className="mt-3 space-y-1.5">
            {data.shadowWeightVariants.map((v) => (
              <li
                key={v.weightMultiplier}
                className="flex items-center justify-between rounded-lg bg-surface-low px-2.5 py-2 text-xs"
              >
                <span>
                  ×{v.weightMultiplier.toFixed(1)}
                  {v.isProduction && " · prod"}
                  {v.isLeader && " · leader"}
                </span>
                <span className="font-data tabular-nums">
                  {v.measuredCount > 0
                    ? `${v.successRatePercent}% (n=${v.measuredCount})`
                    : "chờ đo"}
                </span>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {review && (
        <>
          <Card padding="lg">
            <SectionTitle
              title={`Tuần ${formatShortDate(review.weekStartDate)}`}
              subtitle={
                data.generatedAt
                  ? `Cập nhật ${new Date(data.generatedAt).toLocaleString("vi-VN")}`
                  : undefined
              }
            />
            <p className="mt-2 text-sm leading-relaxed text-on-surface-variant">{review.summary}</p>

            <div className="mt-4 grid grid-cols-3 gap-2 text-center">
              <StatBox label="Tăng tốt" value={review.goodCount} color={theme.primary} />
              <StatBox label="Đi ngang" value={review.flatCount} color={theme.text} />
              <StatBox label="Xịt" value={review.failedCount} color={theme.red} />
            </div>

            <div className="mt-3 grid grid-cols-2 gap-2">
              <MetricPill label="Tỷ lệ thành công" value={`${review.successRatePercent}%`} accent />
              <MetricPill label="Tỷ lệ hỏng" value={`${review.failedRatePercent}%`} danger={review.failedRatePercent >= 45} />
            </div>

            <ActionBanner action={review.recommendedAction} failedRate={review.failedRatePercent} />
          </Card>

          <Card>
            <SectionTitle title="Theo nguồn tín hiệu" />
            <div className="space-y-2">
              <SourceRow label="Top cơ hội" count={review.opportunityCount} rate={review.opportunitySuccessRate} />
              <SourceRow label="Mua điểm 1" count={review.buyPoint1Count} rate={review.buyPoint1SuccessRate} />
              <SourceRow label="Mua điểm 2" count={review.buyPoint2Count} rate={review.buyPoint2SuccessRate} />
              <SourceRow label="Cắt lỗ điểm 1" count={review.cutLoss1Count} />
              <SourceRow label="Cắt hết" count={review.cutAllCount} />
            </div>
          </Card>
        </>
      )}

      {data.recentOutcomes.length > 0 && (
        <Card>
          <SectionTitle title="Kết quả gần đây (T+2.5)" subtitle="Giá TB phiên T+2 và T+3" />
          <ul className="space-y-2">
            {data.recentOutcomes.map((item) => (
              <li
                key={item.id}
                className="flex items-center justify-between gap-2 rounded-xl border border-outline-variant bg-surface-low px-3 py-2.5"
              >
                <div className="min-w-0">
                  <Link to={`/stocks/${item.symbol}`} className="font-bold text-primary">
                    {item.symbol}
                  </Link>
                  <p className="text-[11px] text-on-surface-variant">
                    {item.sourceLabel} · {formatShortDate(item.entryDate)}
                    {item.predictedHitPercent != null && item.predictedHitPercent > 0 && (
                      <> · P dự {item.predictedHitPercent.toFixed(0)}%</>
                    )}
                    {item.forwardReturnT5 != null && (
                      <> · T+5 {item.forwardReturnT5 >= 0 ? "+" : ""}{item.forwardReturnT5.toFixed(1)}%</>
                    )}
                    {item.maxFavorableExcursionPercent != null && (
                      <> · MFE {item.maxFavorableExcursionPercent.toFixed(1)}%</>
                    )}
                  </p>
                </div>
                <OutcomeBadge bucket={item.outcomeBucket} returnPercent={item.forwardReturnPercent} />
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  );
}

function StatBox({ label, value, color }: { label: string; value: number; color: string }) {
  return (
    <div className="rounded-xl bg-surface-low py-2.5">
      <p className="label-caps text-on-surface-variant">{label}</p>
      <p className="font-data mt-1 text-xl font-bold tabular-nums" style={{ color }}>
        {value}
      </p>
    </div>
  );
}

function MetricPill({ label, value, accent, danger }: { label: string; value: string; accent?: boolean; danger?: boolean }) {
  const theme = useThemeTokens();
  const color = danger ? theme.red : accent ? theme.primary : theme.text;
  return (
    <div className="rounded-xl border border-outline-variant px-3 py-2 text-center">
      <p className="text-[10px] text-on-surface-variant">{label}</p>
      <p className="font-data text-lg font-bold" style={{ color }}>{value}</p>
    </div>
  );
}

function ActionBanner({ action, failedRate }: { action: string; failedRate: number }) {
  const theme = useThemeTokens();
  if (action === "Keep") return null;

  const isOverhaul = action === "Overhaul";
  return (
    <div
      className="mt-4 rounded-xl border px-3 py-2.5 text-sm"
      style={{
        borderColor: isOverhaul ? theme.red : theme.amber,
        backgroundColor: isOverhaul ? theme.redBg : theme.amberBg,
        color: isOverhaul ? theme.red : theme.amber,
      }}
    >
      {isOverhaul
        ? `Tỷ lệ hỏng ${failedRate}% vượt ngưỡng — cần đánh giá lại bộ lọc Top cơ hội.`
        : `Tỷ lệ hỏng ${failedRate}% cao — theo dõi thêm, cân nhắc tinh chỉnh tiêu chí.`}
    </div>
  );
}

function SourceRow({ label, count, rate }: { label: string; count: number; rate?: number }) {
  return (
    <div className="flex items-center justify-between rounded-lg bg-surface-low px-3 py-2 text-sm">
      <span className="text-on-surface">{label}</span>
      <span className="font-data text-on-surface-variant">
        {count} mã{rate != null ? ` · ${rate}% OK` : ""}
      </span>
    </div>
  );
}

function OutcomeBadge({ bucket, returnPercent }: { bucket?: string | null; returnPercent?: number | null }) {
  const theme = useThemeTokens();
  const map = {
    Good: { label: "Tốt", bg: theme.greenBg, color: theme.primary },
    Flat: { label: "Ngang", bg: theme.neutralBg, color: theme.textMuted },
    Failed: { label: "Xịt", bg: theme.redBg, color: theme.red },
  } as const;
  const style = map[bucket as keyof typeof map] ?? map.Flat;
  return (
    <span
      className="shrink-0 rounded-full px-2.5 py-1 text-xs font-bold"
      style={{ backgroundColor: style.bg, color: style.color }}
    >
      {style.label}
      {returnPercent != null && ` ${formatPercent(returnPercent)}`}
    </span>
  );
}
