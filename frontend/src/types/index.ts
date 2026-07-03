export type MarketTrend = "Uptrend" | "Sideway" | "Downtrend";

export type SignalType =
  | "Breakout"
  | "VolumeSpike"
  | "Accumulation"
  | "Shakeout"
  | "Distribution"
  | "RelativeStrength";

export type AlertCategory = "Buy" | "Sell" | "All";

export type AlertFeedScope = "opportunity" | "universe";

export interface MarketOverview {
  indexSymbol: string;
  indexPrice: number;
  indexChangePercent: number;
  marketScore: number;
  trend: MarketTrend;
}

export interface TradeEvent {
  symbol: string;
  label: string;
  price: number;
  volume: number;
  valueVnd: number;
  spreadPct: number;
  bookImbalance: number;
  foreignNetDelta: number;
  sessionForeignNet: number;
  sessionPropNet: number;
  sessionPressure: number;
  at: string;
  isAggregated: boolean;
}

/** @deprecated Use TradeEvent */
export interface TradePrint {
  symbol: string;
  side: "Buy" | "Sell";
  price: number;
  volume: number;
  at: string;
}

export interface IntradayMonitorStatus {
  enabled: boolean;
  marketOpen: boolean;
  intervalSeconds: number;
  lastScanAt: string | null;
  lastSymbolsScanned: number;
  lastAlertsSent: number;
  status: string;
  isStale: boolean;
}

export interface QuoteTick {
  symbol: string;
  price: number;
  changePercent: number;
  volume: number;
  updatedAt: string;
}

export interface IndexTick {
  symbol: string;
  price: number;
  changePercent: number;
  marketScore: number;
  trend: MarketTrend;
  updatedAt: string;
}

export interface Sector {
  name: string;
  score: number;
  changePercent: number;
}

export type BuyRecommendation = "Avoid" | "Watch" | "StrongBuy";

export interface BuyScoreComponent {
  id: string;
  label: string;
  points: number;
  maxPoints: number;
  detail: string;
}

export interface SwingDecision {
  verdict: "Go" | "Wait" | "NoGo" | string;
  headline: string;
  detail: string;
  adjustedHitPercent: number;
  rawHitPercent: number;
  suggestedSizePercent: number;
  riskRewardRatio: number;
  regimeSizeFactor: number;
  requiresMasterConfirm: boolean;
  regimeNotes: string[];
  reasons: string[];
  personalCalibrationFactor: number;
  winRate7d?: number | null;
  measuredCount7d: number;
}

export interface BuyDecision {
  buyScore: number;
  actionScore: number;
  recommendation: BuyRecommendation;
  passesTopFilter: boolean;
  gateFailure?: string | null;
  reasons: string[];
  breakdown: BuyScoreComponent[];
  entryPoint: EntryPoint;
  predictedHitPercent?: number;
  predictedSampleCount?: number;
  setupDna?: string | null;
  topExplainLines?: string[] | null;
  swingDecision?: SwingDecision | null;
}

export interface TradeJournalEntry {
  id: string;
  symbol: string;
  tradeDate: string;
  action: "Entered" | "Skipped" | "Vetoed" | string;
  sizePercent?: number | null;
  engineVerdict?: string | null;
  note?: string | null;
  buyScore?: number | null;
  predictedHit?: number | null;
  setupDna?: string | null;
  createdAt: string;
}

export interface PersonalCalibration {
  factor: number;
  sampleCount: number;
  updatedAt: string;
}

export interface Opportunity {
  symbol: string;
  name: string;
  score: number;
  price: number;
  changePercent: number;
  volumeRatio: number;
  sector: string;
  generatedAt?: string | null;
  entryPoint?: EntryPoint | null;
  recommendation?: BuyRecommendation | null;
  predictedHitPercent?: number;
  predictedSampleCount?: number;
  setupDna?: string | null;
  topExplainLines?: string[] | null;
}

export type EntryPointStatus = "Ready" | "Watch" | "Late" | "Invalid";
export type EntryPointType = "None" | "Breakout" | "Shakeout";

export interface EntryPointCheck {
  id: string;
  label: string;
  passed: boolean;
  detail: string;
}

export interface EntryPoint {
  status: EntryPointStatus;
  type: EntryPointType;
  confidence: number;
  entryPrice: number;
  stopLoss: number;
  triggerPrice: number;
  targetPrice: number;
  baseLow: number;
  baseHigh: number;
  gainFromBasePercent: number;
  riskRewardRatio: number;
  isActionable: boolean;
  headline: string;
  action: string;
  checklist: EntryPointCheck[];
}

export interface ShadowVariantStatus {
  minPassScore: number;
  measuredCount: number;
  successRatePercent: number;
  isProduction: boolean;
  isLeader: boolean;
}

export interface ShadowWeightVariantStatus {
  weightMultiplier: number;
  measuredCount: number;
  successRatePercent: number;
  isProduction: boolean;
  isLeader: boolean;
}

export interface EntryTimingSummary {
  topOnlySuccessRate: number;
  confirmSuccessRate: number;
  topOnlySamples: number;
  confirmSamples: number;
  preferMasterConfirm: boolean;
}

export interface EngineTrust {
  winRate7d?: number | null;
  measuredCount7d: number;
  goodCount7d: number;
  calibrationGlobalFactor: number;
  calibrationSamples: number;
  dataAsOfDate?: string | null;
  shadowModeEnabled: boolean;
  shadowLeaderMinPassScore?: number | null;
  shadowStatusMessage?: string | null;
  shadowVariants?: ShadowVariantStatus[] | null;
}

export interface OpportunitiesList {
  items: Opportunity[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasFreshData: boolean;
  statusMessage?: string | null;
  forTradingDate?: string | null;
  generatedAt?: string | null;
  needsAnalysis: boolean;
  canRunAnalysis: boolean;
  analysisAvailableAt?: string | null;
  engineTrust?: EngineTrust | null;
}

export interface DailyAnalysisResult {
  forTradingDate: string;
  stocksScored: number;
  opportunitiesSaved: number;
  completedAt: string;
  patternAlertsPublished: number;
}

export interface Job1Status {
  isRunning: boolean;
  currentSymbol?: string | null;
  processed: number;
  total: number;
  percentComplete: number;
  startedAt?: string | null;
}

export interface Job1Result {
  symbolsTotal: number;
  symbolsScreened: number;
  symbolsInUniverse: number;
  symbolsSucceeded: number;
  symbolsFailed: number;
  symbolsExcluded: number;
  barsWritten: number;
  failedSymbols: string[];
  completedAt: string;
}

export interface Signal {
  symbol: string;
  type: SignalType;
  title: string;
  description: string;
  createdAt: string;
}

export interface ScoreBreakdown {
  marketTrend: number;
  sectorStrength: number;
  relativeStrength: number;
  accumulation: number;
  breakout: number;
  volumeExpansion: number;
}

export interface OhlcvBar {
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export type ChartInterval = "1D" | "1H" | "30m" | "15m" | "5m" | "1m";

export interface ChartBar {
  time: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface StockChart {
  symbol: string;
  interval: ChartInterval;
  bars: ChartBar[];
}

export const CHART_INTERVALS: { value: ChartInterval; label: string; short: string }[] = [
  { value: "1D", label: "Ngày", short: "D" },
  { value: "1H", label: "Giờ", short: "H" },
  { value: "30m", label: "30 phút", short: "30" },
  { value: "15m", label: "15 phút", short: "15" },
  { value: "5m", label: "5 phút", short: "5" },
  { value: "1m", label: "1 phút", short: "1" },
];

export interface StockDetail {
  symbol: string;
  name: string;
  sector: string;
  price: number;
  changePercent: number;
  score: number;
  sectorRank: number;
  passesSmartMoneyFilter: boolean;
  scoreReasons: string[];
  summary: string;
  activeSignals: string[];
  buyZone: number;
  stopLoss: number;
  resistance: number;
  target: number;
  relativeStrength: number;
  volumeRatio: number;
  history: OhlcvBar[];
  basePrice?: BasePrice | null;
  patternScores: CriterionScore[];
  patternCompositeScore: number;
  bundleCompositeScore: number;
  opportunityCompositeScore: number;
  entryPoint: EntryPoint;
  buyDecision: BuyDecision;
}

export interface BasePricePeriod {
  fromDate: string;
  toDate: string;
  sessionDays: number;
  low: number;
  high: number;
}

export interface BasePrice {
  baseLow: number;
  baseHigh: number;
  totalSessionDays: number;
  gainFromBasePercent: number;
  baseIndex: number;
  totalBases: number;
  filterBaseHigh: number;
  filterGainFromBasePercent: number;
  exceedsRunupFilter: boolean;
  qualityScore: number;
  quality?: BaseQualityComponents | null;
  periods: BasePricePeriod[];
}

export interface BaseQualityComponents {
  priorTrendScore: number;
  atrContractionScore: number;
  compressionScore: number;
  volumeDryScore: number;
  contractionPatternScore: number;
  distributionScore: number;
  durationScore: number;
  totalScore: number;
}

export interface RadarItem {
  symbol: string;
  name: string;
  sector: string;
  score: number;
  price: number;
  changePercent: number;
  volumeRatio: number;
  relativeStrength: number;
  signals: SignalType[];
}

export interface Alert {
  id: string;
  symbol: string;
  type: SignalType;
  title: string;
  message: string;
  createdAt: string;
  category: AlertCategory;
  volumeRatio?: number;
  relativeStrength?: number;
  sectorRank?: string;
  inOpportunity?: boolean;
  inWatchlist?: boolean;
}

export interface WatchlistItem {
  symbol: string;
  name: string;
  sector: string;
  score: number;
  changePercent: number;
  sectorLocked: boolean;
}

export interface SectorCatalogItem {
  name: string;
}

export interface RadarFilters {
  breakout: boolean;
  accumulation: boolean;
  relativeStrength: boolean;
  volumeSpike: boolean;
  shakeout: boolean;
  distribution: boolean;
  sector?: string;
}

export type RadarLiveDirection = "All" | "Up" | "Down";

export interface RadarLiveItem {
  symbol: string;
  name: string;
  sector: string;
  price: number;
  changePercent: number;
  sessionVolume: number;
  volumeRatio: number;
  relativeStrength: number;
  signals: SignalType[];
  scannedAt: string;
}

export interface RadarLiveSnapshot {
  exchange: string;
  sessionDate: string;
  scannedAt: string;
  matchCount: number;
  items: RadarLiveItem[];
}

export interface RadarLiveQuery {
  minSessionVolume?: number;
  minAbsChangePercent?: number;
  direction?: RadarLiveDirection;
}

export interface SparklineSeries {
  symbol: string;
  prices: number[];
  reference: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CriterionScore {
  id: string;
  label: string;
  group: string;
  rank: number;
  score: number;
  bias: "Neutral" | "Bullish" | "Bearish";
  summary: string;
}

export interface CriterionBucket {
  bucketId: string;
  hitCount: number;
  totalCount: number;
  accuracyPercent: number;
}

export interface CriterionPhaseStat {
  phase: string;
  hitCount: number;
  totalCount: number;
  accuracyPercent: number;
}

export interface CriterionAccuracy {
  id: string;
  label: string;
  group: string;
  rank: number;
  hitCount: number;
  totalCount: number;
  accuracyPercent: number;
  avgScore: number;
  weight: number;
  accuracy7d: number;
  accuracy30d: number;
  recommendedAction: "Keep" | "Watch" | "Remove";
  isActive: boolean;
  reliabilityScore?: number;
  edgePercent?: number;
  avgMfePercent?: number;
  invalidationRatePercent?: number;
  baselinePercent?: number;
  buckets?: CriterionBucket[];
  phases?: CriterionPhaseStat[];
}

export interface CriterionGroupAccuracy {
  groupId: string;
  hitCount: number;
  totalCount: number;
  accuracyPercent: number;
  avgScore: number;
  criterionCount: number;
  recommendedAction: "Keep" | "Watch" | "Remove";
  keepCount: number;
  watchCount: number;
  removeCount: number;
  reliabilityScore?: number;
  edgePercent?: number;
}

export interface WeeklyCriterionReview {
  id: string;
  label: string;
  group: string;
  rank: number;
  hitCount7d: number;
  totalCount7d: number;
  accuracy7d: number;
  avgScore7d: number;
  weight: number;
  recommendedAction: "Keep" | "Watch" | "Remove";
  isActive: boolean;
  reliability7d?: number;
  edge7d?: number;
  avgMfe7d?: number;
  invalidationRate7d?: number;
  buckets?: CriterionBucket[];
  phases?: CriterionPhaseStat[];
}

export interface CriterionStockRank {
  symbol: string;
  compositeScore: number;
  topCriteria: CriterionScore[];
}

export interface CriteriaSummary {
  asOfDate?: string | null;
  weekStartDate?: string | null;
  generatedAt?: string | null;
  criteria: CriterionAccuracy[];
  groups: CriterionGroupAccuracy[];
  weeklyReview: WeeklyCriterionReview[];
  topStocks: CriterionStockRank[];
  statusMessage?: string | null;
}

export interface SetupTrackOutcome {
  id: string;
  symbol: string;
  sourceType: string;
  sourceLabel: string;
  entryDate: string;
  entryPrice: number;
  opportunityRank?: number | null;
  opportunityScore?: number | null;
  sessionChangePercent?: number | null;
  forwardReturnPercent?: number | null;
  outcomeBucket?: string | null;
  measuredAt?: string | null;
  predictedHitPercent?: number | null;
  setupDna?: string | null;
  forwardReturnT5?: number | null;
  forwardReturnT10?: number | null;
  outcomeBucketT5?: string | null;
  outcomeBucketT10?: string | null;
  maxFavorableExcursionPercent?: number | null;
  maxAdverseExcursionPercent?: number | null;
  hadMasterConfirm?: boolean | null;
}

export interface HitCalibrationBucket {
  bucketId: string;
  sampleCount: number;
  predictedMidPercent: number;
  actualHitRatePercent: number;
  calibrationFactor: number;
}

export interface FalsePositiveCriterion {
  componentId: string;
  label: string;
  falsePositiveHits: number;
  falsePositiveAvgNorm: number;
  goodAvgNorm: number;
  deceptionScore: number;
  weightPenalty: number;
}

export interface FalsePositiveMiningSummary {
  falsePositiveSetups: number;
  goodSetups: number;
  flaggedCriteria: FalsePositiveCriterion[];
}

export interface HitCalibrationSummary {
  globalFactor: number;
  totalSamples: number;
  predictionBiasPercent: number;
  updatedAt?: string | null;
  buckets: HitCalibrationBucket[];
}

export interface WeeklyOpportunityReview {
  weekStartDate: string;
  totalTracked: number;
  measuredCount: number;
  goodCount: number;
  flatCount: number;
  failedCount: number;
  successRatePercent: number;
  failedRatePercent: number;
  opportunityCount: number;
  buyPoint1Count: number;
  buyPoint2Count: number;
  cutLoss1Count: number;
  cutAllCount: number;
  opportunitySuccessRate: number;
  buyPoint1SuccessRate: number;
  buyPoint2SuccessRate: number;
  recommendedAction: string;
  summary: string;
  generatedAt: string;
}

export interface OpportunityPerformanceSummary {
  weekStartDate?: string | null;
  generatedAt?: string | null;
  weeklyReview?: WeeklyOpportunityReview | null;
  recentOutcomes: SetupTrackOutcome[];
  statusMessage?: string | null;
  calibration?: HitCalibrationSummary | null;
  falsePositiveMining?: FalsePositiveMiningSummary | null;
  shadowVariants?: ShadowVariantStatus[] | null;
  shadowStatusMessage?: string | null;
  shadowWeightVariants?: ShadowWeightVariantStatus[] | null;
  entryTiming?: EntryTimingSummary | null;
}

export type SmartMoneyBacktestMode = "strict" | "relaxed" | "strict-then-relaxed";

export interface SmartMoneyBacktestSummary {
  fromDate: string;
  toDate: string;
  tradingDaysScanned: number;
  daysWithPicks: number;
  totalTrades: number;
  winCount: number;
  lossCount: number;
  flatCount: number;
  winRatePercent: number;
  avgReturnPercent: number;
  medianReturnPercent: number;
  maxDrawdownPercent: number;
  successThresholdPercent: number;
  universeSize: number;
  relaxedFallbackEnabled: boolean;
}

export interface SmartMoneyBacktestTrade {
  symbol: string;
  entryDate: string;
  entryPrice: number;
  exitPrice: number;
  returnPercent: number;
  buyScore: number;
  outcome: string;
  usedRelaxedFallback: boolean;
}

export interface SmartMoneyBacktestResult {
  summary: SmartMoneyBacktestSummary;
  trades: SmartMoneyBacktestTrade[];
}
