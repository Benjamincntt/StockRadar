export type MarketTrend = "Uptrend" | "Sideway" | "Downtrend";

export type SignalType =
  | "Breakout"
  | "DarvasBreakout"
  | "VolumeSpike"
  | "Accumulation"
  | "Shakeout"
  | "Distribution"
  | "RelativeStrength"
  | "BullishDivergence";

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

export type StockTradeState = "Avoid" | "Watchlist" | "AwaitingTrigger" | "Actionable";

export type BuyRecommendation = "Avoid" | "Watch" | "StrongBuy";

export interface BuyScoreComponent {
  id: string;
  label: string;
  points: number;
  maxPoints: number;
  detail: string;
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
  tradeState?: StockTradeState | null;
  tradeStateLabelVi?: string | null;
  tradeStateReason?: string | null;
  predictedHitPercent?: number;
  predictedSampleCount?: number;
  setupDna?: string | null;
  topExplainLines?: string[] | null;
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
  tradeState?: StockTradeState | null;
  tradeStateLabelVi?: string | null;
  tradeStateReason?: string | null;
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

export interface EngineTrust {
  winRate7d?: number | null;
  measuredCount7d: number;
  goodCount7d: number;
  calibrationGlobalFactor: number;
  calibrationSamples: number;
  dataAsOfDate?: string | null;
  shadowModeEnabled: boolean;
}

export type OpportunityAnalysisStatus =
  | "not_run"
  | "zero_matches"
  | "has_results"
  | "reference_list";

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
  analysisStatus?: OpportunityAnalysisStatus | null;
  lastAnalysisAt?: string | null;
  targetTradingDate?: string | null;
  lastAnalysisStocksScored?: number | null;
  lastAnalysisOpportunitiesSaved?: number | null;
  statusBullets?: string[] | null;
  /** Số mã bị loại theo từng gate (nhãn gate tiếng Việt → count). Null nếu lần chạy chưa lưu stats. */
  gateStats?: Record<string, number> | null;
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

export interface RightsEvent {
  symbol: string;
  exDate: string;
  cash: number;
  dilution: number;
  oldShares?: number;
  newShares?: number;
  issuePrice?: number;
}

export interface StockDetail {

  symbol: string;
  name: string;
  sector: string;
  price: number;
  changePercent: number;
  score: number;
  sectorWave: string;
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
  flatBox?: FlatBox | null;
  patternScores: CriterionScore[];
  opportunityCompositeScore: number;
  entryPoint: EntryPoint;
  buyDecision: BuyDecision;
}

export interface FlatBox {
  boxLow: number;
  boxHigh: number;
  sessionDays: number;
  refBoxPeriod: string;
  isBreakoutConfirmed: boolean;
  priceGainPercent?: number | null;
  volumeMultiplier?: number | null;
  suggestedStopLoss: number;
  gainFromBoxTopPercent: number;
  exceedsRunupFilter: boolean;
  filterBoxTop: number;
  filterGainFromBoxTopPercent: number;
  eventLabel?: string;
  periods: BasePricePeriod[];
}

/** @deprecated Chart zones — same shape as flat box period */
export interface BasePricePeriod {
  fromDate: string;
  toDate: string;
  sessionDays: number;
  low: number;
  high: number;
}

/** @deprecated Replaced by flatBox */
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

/** Kỳ lọc hiệu quả kịch bản. */
export type HieuQuaPeriod = "week" | "month" | "quarter" | "all";

/** Kết quả đo hiển thị (có dấu) của một lệnh. */
export type KetQuaHienThi = "Thắng" | "Thua" | "Ngang" | "Chờ đo";

export interface LoaiKichBanStats {
  tenKichBan: string;
  tong: number;
  thang: number;
  thua: number;
  ngang: number;
  tyLeThang: number;
  tbRR: number;
}

export interface HieuQuaTomTat {
  tongKichHoat: number;
  thang: number;
  thua: number;
  ngang: number;
  choDo: number;
  tyLeThang: number;
  tbRR: number;
  tbPhanTram: number;
  theoLoaiKichBan: LoaiKichBanStats[];
}

export interface LichSuLenh {
  id: number;
  symbol: string;
  loaiKichBan: string;
  ketQua: string;
  giaVao: number;
  giaThoat: number | null;
  phanTram: number | null;
  rrThucTe: number | null;
  ngayKichHoat: string;
  ngayThoat: string | null;
}

export interface LichSuResponse {
  items: LichSuLenh[];
  totalCount: number;
  page: number;
  pageSize: number;
}
