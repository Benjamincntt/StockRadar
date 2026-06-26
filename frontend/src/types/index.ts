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

export interface Opportunity {
  symbol: string;
  name: string;
  score: number;
  price: number;
  changePercent: number;
  volumeRatio: number;
  sector: string;
  generatedAt?: string | null;
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
  periods: BasePricePeriod[];
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
