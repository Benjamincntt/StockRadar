class AuthUser {
  const AuthUser({
    required this.userId,
    required this.email,
    required this.displayName,
    required this.token,
  });

  final String userId;
  final String email;
  final String displayName;
  final String token;

  factory AuthUser.fromJson(Map<String, dynamic> json) => AuthUser(
        userId: json['userId']?.toString() ?? '',
        email: json['email'] as String? ?? '',
        displayName: json['displayName'] as String? ?? '',
        token: json['token'] as String? ?? '',
      );
}

class StockSearchHit {
  const StockSearchHit({required this.symbol, required this.name});

  final String symbol;
  final String name;

  factory StockSearchHit.fromJson(Map<String, dynamic> json) => StockSearchHit(
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        name: json['name'] as String? ?? '',
      );
}

class Opportunity {
  const Opportunity({
    required this.symbol,
    required this.name,
    required this.score,
    required this.price,
    required this.changePercent,
    required this.sector,
    this.recommendation,
    this.predictedHitPercent,
    this.predictedSampleCount = 0,
    this.setupDna,
    this.entryPointStatus,
    this.topExplainLines,
  });

  final String symbol;
  final String name;
  final double score;
  final double price;
  final double changePercent;
  final String sector;
  final String? recommendation;
  final double? predictedHitPercent;
  final int predictedSampleCount;
  final String? setupDna;
  final String? entryPointStatus;
  final List<String>? topExplainLines;

  factory Opportunity.fromJson(Map<String, dynamic> json) => Opportunity(
        symbol: json['symbol'] as String? ?? '',
        name: json['name'] as String? ?? '',
        score: (json['score'] as num?)?.toDouble() ?? 0,
        price: (json['price'] as num?)?.toDouble() ?? 0,
        changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
        sector: json['sector'] as String? ?? '',
        recommendation: json['recommendation'] as String?,
        predictedHitPercent: (json['predictedHitPercent'] as num?)?.toDouble(),
        predictedSampleCount: (json['predictedSampleCount'] as num?)?.toInt() ?? 0,
        setupDna: json['setupDna'] as String?,
        entryPointStatus: json['entryPoint']?['status'] as String?,
        topExplainLines: (json['topExplainLines'] as List<dynamic>?)
            ?.map((e) => e.toString())
            .toList(),
      );
}

class EngineTrust {
  const EngineTrust({
    this.winRate7d,
    this.measuredCount7d = 0,
    this.goodCount7d = 0,
    this.calibrationGlobalFactor = 1,
    this.calibrationSamples = 0,
    this.dataAsOfDate,
    this.shadowModeEnabled = false,
    this.shadowStatusMessage,
  });

  final double? winRate7d;
  final int measuredCount7d;
  final int goodCount7d;
  final double calibrationGlobalFactor;
  final int calibrationSamples;
  final String? dataAsOfDate;
  final bool shadowModeEnabled;
  final String? shadowStatusMessage;

  factory EngineTrust.fromJson(Map<String, dynamic>? json) {
    if (json == null) return const EngineTrust();
    return EngineTrust(
      winRate7d: (json['winRate7d'] as num?)?.toDouble(),
      measuredCount7d: (json['measuredCount7d'] as num?)?.toInt() ?? 0,
      goodCount7d: (json['goodCount7d'] as num?)?.toInt() ?? 0,
      calibrationGlobalFactor: (json['calibrationGlobalFactor'] as num?)?.toDouble() ?? 1,
      calibrationSamples: (json['calibrationSamples'] as num?)?.toInt() ?? 0,
      dataAsOfDate: json['dataAsOfDate'] as String?,
      shadowModeEnabled: json['shadowModeEnabled'] as bool? ?? false,
      shadowStatusMessage: json['shadowStatusMessage'] as String?,
    );
  }
}

class OpportunitiesList {
  const OpportunitiesList({
    required this.items,
    this.generatedAt,
    this.statusMessage,
    this.hasFreshData = false,
    this.needsAnalysis = false,
    this.canRunAnalysis = true,
    this.analysisAvailableAt,
    this.engineTrust,
  });

  final List<Opportunity> items;
  final String? generatedAt;
  final String? statusMessage;
  final bool hasFreshData;
  final bool needsAnalysis;
  final bool canRunAnalysis;
  final String? analysisAvailableAt;
  final EngineTrust? engineTrust;

  factory OpportunitiesList.fromJson(Map<String, dynamic> json) => OpportunitiesList(
        items: (json['items'] as List<dynamic>? ?? [])
            .map((e) => Opportunity.fromJson(e as Map<String, dynamic>))
            .toList(),
        generatedAt: json['generatedAt'] as String?,
        statusMessage: json['statusMessage'] as String?,
        hasFreshData: json['hasFreshData'] as bool? ?? false,
        needsAnalysis: json['needsAnalysis'] as bool? ?? false,
        canRunAnalysis: json['canRunAnalysis'] as bool? ?? true,
        analysisAvailableAt: json['analysisAvailableAt'] as String?,
        engineTrust: EngineTrust.fromJson(json['engineTrust'] as Map<String, dynamic>?),
      );
}

class DailyAnalysisResult {
  const DailyAnalysisResult({
    required this.stocksScored,
    required this.opportunitiesSaved,
  });

  final int stocksScored;
  final int opportunitiesSaved;

  factory DailyAnalysisResult.fromJson(Map<String, dynamic> json) => DailyAnalysisResult(
        stocksScored: (json['stocksScored'] as num?)?.toInt() ?? 0,
        opportunitiesSaved: (json['opportunitiesSaved'] as num?)?.toInt() ?? 0,
      );
}

class QuoteTick {
  const QuoteTick({
    required this.symbol,
    required this.price,
    required this.changePercent,
    this.volume = 0,
    this.updatedAt,
  });

  final String symbol;
  final double price;
  final double changePercent;
  final double volume;
  final String? updatedAt;

  factory QuoteTick.fromJson(Map<String, dynamic> json) => QuoteTick(
        symbol: (json['symbol'] ?? json['Symbol'] ?? '').toString().toUpperCase(),
        price: ((json['price'] ?? json['Price']) as num?)?.toDouble() ?? 0,
        changePercent: ((json['changePercent'] ?? json['ChangePercent']) as num?)?.toDouble() ?? 0,
        volume: ((json['volume'] ?? json['Volume']) as num?)?.toDouble() ?? 0,
        updatedAt: (json['updatedAt'] ?? json['UpdatedAt'])?.toString(),
      );
}

class SparklineSeries {
  const SparklineSeries({required this.symbol, required this.closes});

  final String symbol;
  final List<double> closes;

  factory SparklineSeries.fromJson(Map<String, dynamic> json) => SparklineSeries(
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        closes: (json['closes'] as List<dynamic>? ?? [])
            .map((e) => (e as num).toDouble())
            .toList(),
      );
}

class Job1Status {
  const Job1Status({
    required this.isRunning,
    this.currentSymbol,
    this.processed = 0,
    this.total = 0,
    this.percentComplete = 0,
  });

  final bool isRunning;
  final String? currentSymbol;
  final int processed;
  final int total;
  final int percentComplete;

  factory Job1Status.fromJson(Map<String, dynamic> json) => Job1Status(
        isRunning: json['isRunning'] as bool? ?? false,
        currentSymbol: json['currentSymbol'] as String?,
        processed: (json['processed'] as num?)?.toInt() ?? 0,
        total: (json['total'] as num?)?.toInt() ?? 0,
        percentComplete: (json['percentComplete'] as num?)?.toInt() ?? 0,
      );
}

class Job1Result {
  const Job1Result({
    required this.symbolsInUniverse,
    required this.symbolsTotal,
    required this.symbolsExcluded,
    required this.barsWritten,
  });

  final int symbolsInUniverse;
  final int symbolsTotal;
  final int symbolsExcluded;
  final int barsWritten;

  factory Job1Result.fromJson(Map<String, dynamic> json) => Job1Result(
        symbolsInUniverse: (json['symbolsInUniverse'] as num?)?.toInt() ?? 0,
        symbolsTotal: (json['symbolsTotal'] as num?)?.toInt() ?? 0,
        symbolsExcluded: (json['symbolsExcluded'] as num?)?.toInt() ?? 0,
        barsWritten: (json['barsWritten'] as num?)?.toInt() ?? 0,
      );
}

class OpportunityPerformanceSummary {
  const OpportunityPerformanceSummary({
    this.statusMessage,
    this.weekStartDate,
    this.generatedAt,
    this.weeklyReview,
    this.calibration,
    this.shadowStatusMessage,
    this.raw = const {},
  });

  final String? statusMessage;
  final String? weekStartDate;
  final String? generatedAt;
  final Map<String, dynamic>? weeklyReview;
  final Map<String, dynamic>? calibration;
  final String? shadowStatusMessage;
  final Map<String, dynamic> raw;

  factory OpportunityPerformanceSummary.fromJson(Map<String, dynamic> json) =>
      OpportunityPerformanceSummary(
        statusMessage: json['statusMessage'] as String?,
        weekStartDate: json['weekStartDate'] as String?,
        generatedAt: json['generatedAt'] as String?,
        weeklyReview: json['weeklyReview'] as Map<String, dynamic>?,
        calibration: json['calibration'] as Map<String, dynamic>?,
        shadowStatusMessage: json['shadowStatusMessage'] as String?,
        raw: json,
      );
}

class SmartMoneyBacktestSummary {
  const SmartMoneyBacktestSummary({
    required this.fromDate,
    required this.toDate,
    required this.tradingDaysScanned,
    required this.daysWithPicks,
    required this.totalTrades,
    required this.winCount,
    required this.lossCount,
    required this.flatCount,
    required this.winRatePercent,
    required this.avgReturnPercent,
    required this.medianReturnPercent,
    required this.maxDrawdownPercent,
    required this.successThresholdPercent,
    required this.universeSize,
    required this.relaxedFallbackEnabled,
  });

  final String fromDate;
  final String toDate;
  final int tradingDaysScanned;
  final int daysWithPicks;
  final int totalTrades;
  final int winCount;
  final int lossCount;
  final int flatCount;
  final double winRatePercent;
  final double avgReturnPercent;
  final double medianReturnPercent;
  final double maxDrawdownPercent;
  final double successThresholdPercent;
  final int universeSize;
  final bool relaxedFallbackEnabled;

  factory SmartMoneyBacktestSummary.fromJson(Map<String, dynamic> json) =>
      SmartMoneyBacktestSummary(
        fromDate: json['fromDate'] as String? ?? '',
        toDate: json['toDate'] as String? ?? '',
        tradingDaysScanned: (json['tradingDaysScanned'] as num?)?.toInt() ?? 0,
        daysWithPicks: (json['daysWithPicks'] as num?)?.toInt() ?? 0,
        totalTrades: (json['totalTrades'] as num?)?.toInt() ?? 0,
        winCount: (json['winCount'] as num?)?.toInt() ?? 0,
        lossCount: (json['lossCount'] as num?)?.toInt() ?? 0,
        flatCount: (json['flatCount'] as num?)?.toInt() ?? 0,
        winRatePercent: (json['winRatePercent'] as num?)?.toDouble() ?? 0,
        avgReturnPercent: (json['avgReturnPercent'] as num?)?.toDouble() ?? 0,
        medianReturnPercent: (json['medianReturnPercent'] as num?)?.toDouble() ?? 0,
        maxDrawdownPercent: (json['maxDrawdownPercent'] as num?)?.toDouble() ?? 0,
        successThresholdPercent: (json['successThresholdPercent'] as num?)?.toDouble() ?? 3,
        universeSize: (json['universeSize'] as num?)?.toInt() ?? 0,
        relaxedFallbackEnabled: json['relaxedFallbackEnabled'] as bool? ?? false,
      );
}

class SmartMoneyBacktestTrade {
  const SmartMoneyBacktestTrade({
    required this.symbol,
    required this.entryDate,
    required this.entryPrice,
    required this.exitPrice,
    required this.returnPercent,
    required this.buyScore,
    required this.outcome,
    required this.usedRelaxedFallback,
  });

  final String symbol;
  final String entryDate;
  final double entryPrice;
  final double exitPrice;
  final double returnPercent;
  final int buyScore;
  final String outcome;
  final bool usedRelaxedFallback;

  factory SmartMoneyBacktestTrade.fromJson(Map<String, dynamic> json) =>
      SmartMoneyBacktestTrade(
        symbol: json['symbol'] as String? ?? '',
        entryDate: json['entryDate'] as String? ?? '',
        entryPrice: (json['entryPrice'] as num?)?.toDouble() ?? 0,
        exitPrice: (json['exitPrice'] as num?)?.toDouble() ?? 0,
        returnPercent: (json['returnPercent'] as num?)?.toDouble() ?? 0,
        buyScore: (json['buyScore'] as num?)?.toInt() ?? 0,
        outcome: json['outcome'] as String? ?? 'Flat',
        usedRelaxedFallback: json['usedRelaxedFallback'] as bool? ?? false,
      );
}

class SmartMoneyBacktestResult {
  const SmartMoneyBacktestResult({
    required this.summary,
    required this.trades,
  });

  final SmartMoneyBacktestSummary summary;
  final List<SmartMoneyBacktestTrade> trades;

  factory SmartMoneyBacktestResult.fromJson(Map<String, dynamic> json) {
    final tradeList = json['trades'] as List<dynamic>? ?? [];
    return SmartMoneyBacktestResult(
      summary: SmartMoneyBacktestSummary.fromJson(
        json['summary'] as Map<String, dynamic>? ?? {},
      ),
      trades: tradeList
          .map((e) => SmartMoneyBacktestTrade.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

class BuyScoreComponent {
  const BuyScoreComponent({
    required this.id,
    required this.label,
    required this.points,
    required this.maxPoints,
  });

  final String id;
  final String label;
  final double points;
  final double maxPoints;

  factory BuyScoreComponent.fromJson(Map<String, dynamic> json) => BuyScoreComponent(
        id: json['id'] as String? ?? '',
        label: json['label'] as String? ?? '',
        points: (json['points'] as num?)?.toDouble() ?? 0,
        maxPoints: (json['maxPoints'] as num?)?.toDouble() ?? 0,
      );
}

class EntryPointCheck {
  const EntryPointCheck({
    required this.id,
    required this.label,
    required this.passed,
    required this.detail,
  });

  final String id;
  final String label;
  final bool passed;
  final String detail;

  factory EntryPointCheck.fromJson(Map<String, dynamic> json) => EntryPointCheck(
        id: json['id'] as String? ?? '',
        label: json['label'] as String? ?? '',
        passed: json['passed'] as bool? ?? false,
        detail: json['detail'] as String? ?? '',
      );
}

class EntryPoint {
  const EntryPoint({
    this.status = 'Invalid',
    this.type = 'None',
    this.confidence = 0,
    this.entryPrice = 0,
    this.stopLoss = 0,
    this.triggerPrice = 0,
    this.targetPrice = 0,
    this.riskRewardRatio = 0,
    this.headline = '',
    this.action = '',
    this.checklist = const [],
  });

  final String status;
  final String type;
  final double confidence;
  final double entryPrice;
  final double stopLoss;
  final double triggerPrice;
  final double targetPrice;
  final double riskRewardRatio;
  final String headline;
  final String action;
  final List<EntryPointCheck> checklist;

  factory EntryPoint.fromJson(Map<String, dynamic>? json) {
    if (json == null) return const EntryPoint();
    return EntryPoint(
      status: json['status'] as String? ?? 'Invalid',
      type: json['type'] as String? ?? 'None',
      confidence: (json['confidence'] as num?)?.toDouble() ?? 0,
      entryPrice: (json['entryPrice'] as num?)?.toDouble() ?? 0,
      stopLoss: (json['stopLoss'] as num?)?.toDouble() ?? 0,
      triggerPrice: (json['triggerPrice'] as num?)?.toDouble() ?? 0,
      targetPrice: (json['targetPrice'] as num?)?.toDouble() ?? 0,
      riskRewardRatio: (json['riskRewardRatio'] as num?)?.toDouble() ?? 0,
      headline: json['headline'] as String? ?? '',
      action: json['action'] as String? ?? '',
      checklist: (json['checklist'] as List<dynamic>? ?? [])
          .map((e) => EntryPointCheck.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

class SwingDecision {
  const SwingDecision({
    this.verdict = '',
    this.headline = '',
    this.detail = '',
    this.adjustedHitPercent = 0,
    this.suggestedSizePercent = 0,
    this.riskRewardRatio = 0,
    this.personalCalibrationFactor = 1,
    this.requiresMasterConfirm = false,
    this.reasons = const [],
  });

  final String verdict;
  final String headline;
  final String detail;
  final double adjustedHitPercent;
  final double suggestedSizePercent;
  final double riskRewardRatio;
  final double personalCalibrationFactor;
  final bool requiresMasterConfirm;
  final List<String> reasons;

  factory SwingDecision.fromJson(Map<String, dynamic>? json) {
    if (json == null) return const SwingDecision();
    return SwingDecision(
      verdict: json['verdict'] as String? ?? '',
      headline: json['headline'] as String? ?? '',
      detail: json['detail'] as String? ?? '',
      adjustedHitPercent: (json['adjustedHitPercent'] as num?)?.toDouble() ?? 0,
      suggestedSizePercent: (json['suggestedSizePercent'] as num?)?.toDouble() ?? 0,
      riskRewardRatio: (json['riskRewardRatio'] as num?)?.toDouble() ?? 0,
      personalCalibrationFactor: (json['personalCalibrationFactor'] as num?)?.toDouble() ?? 1,
      requiresMasterConfirm: json['requiresMasterConfirm'] as bool? ?? false,
      reasons: (json['reasons'] as List<dynamic>? ?? []).map((e) => e.toString()).toList(),
    );
  }
}

class CriterionScore {
  const CriterionScore({
    required this.id,
    required this.label,
    required this.group,
    required this.rank,
    required this.score,
    required this.bias,
    required this.summary,
  });

  final String id;
  final String label;
  final String group;
  final int rank;
  final double score;
  final String bias;
  final String summary;

  factory CriterionScore.fromJson(Map<String, dynamic> json) => CriterionScore(
        id: json['id'] as String? ?? '',
        label: json['label'] as String? ?? '',
        group: json['group'] as String? ?? '',
        rank: (json['rank'] as num?)?.toInt() ?? 0,
        score: (json['score'] as num?)?.toDouble() ?? 0,
        bias: json['bias'] as String? ?? 'Neutral',
        summary: json['summary'] as String? ?? '',
      );
}

class BuyDecision {
  const BuyDecision({
    this.buyScore,
    this.actionScore,
    this.recommendation,
    this.passesTopFilter = false,
    this.reasons = const [],
    this.breakdown = const [],
    this.predictedHitPercent,
    this.predictedSampleCount = 0,
    this.setupDna,
    this.gateFailure,
    this.topExplainLines,
    this.entryPoint = const EntryPoint(),
    this.swingDecision,
  });

  final double? buyScore;
  final double? actionScore;
  final String? recommendation;
  final bool passesTopFilter;
  final List<String> reasons;
  final List<BuyScoreComponent> breakdown;
  final double? predictedHitPercent;
  final int predictedSampleCount;
  final String? setupDna;
  final String? gateFailure;
  final List<String>? topExplainLines;
  final EntryPoint entryPoint;
  final SwingDecision? swingDecision;

  factory BuyDecision.fromJson(Map<String, dynamic>? json) {
    if (json == null) return const BuyDecision();
    return BuyDecision(
      buyScore: (json['buyScore'] as num?)?.toDouble(),
      actionScore: (json['actionScore'] as num?)?.toDouble(),
      recommendation: json['recommendation'] as String?,
      passesTopFilter: json['passesTopFilter'] as bool? ?? false,
      reasons: (json['reasons'] as List<dynamic>? ?? []).map((e) => e.toString()).toList(),
      breakdown: (json['breakdown'] as List<dynamic>? ?? [])
          .map((e) => BuyScoreComponent.fromJson(e as Map<String, dynamic>))
          .toList(),
      predictedHitPercent: (json['predictedHitPercent'] as num?)?.toDouble(),
      predictedSampleCount: (json['predictedSampleCount'] as num?)?.toInt() ?? 0,
      setupDna: json['setupDna'] as String?,
      gateFailure: json['gateFailure'] as String?,
      topExplainLines: (json['topExplainLines'] as List<dynamic>?)
          ?.map((e) => e.toString())
          .toList(),
      entryPoint: EntryPoint.fromJson(json['entryPoint'] as Map<String, dynamic>?),
      swingDecision: SwingDecision.fromJson(json['swingDecision'] as Map<String, dynamic>?),
    );
  }
}

class AlertItem {
  const AlertItem({
    required this.id,
    required this.symbol,
    required this.title,
    required this.message,
    required this.category,
    required this.createdAt,
    this.sectorRank,
    this.inOpportunity,
    this.inWatchlist,
  });

  final String id;
  final String symbol;
  final String title;
  final String message;
  final String category;
  final String createdAt;
  final String? sectorRank;
  final bool? inOpportunity;
  final bool? inWatchlist;

  bool get isBuy => category == 'Buy';
  bool get isMaster => sectorRank == 'Master';

  factory AlertItem.fromJson(Map<String, dynamic> json) => AlertItem(
        id: json['id']?.toString() ?? '',
        symbol: json['symbol'] as String? ?? '',
        title: json['title'] as String? ?? '',
        message: json['message'] as String? ?? '',
        category: json['category'] as String? ?? 'All',
        createdAt: json['createdAt'] as String? ?? '',
        sectorRank: json['sectorRank'] as String?,
        inOpportunity: json['inOpportunity'] as bool?,
        inWatchlist: json['inWatchlist'] as bool?,
      );
}

class RadarLiveItem {
  const RadarLiveItem({
    required this.symbol,
    required this.name,
    required this.sector,
    required this.price,
    required this.changePercent,
    required this.sessionVolume,
    required this.volumeRatio,
    required this.relativeStrength,
    required this.signals,
    required this.scannedAt,
  });

  final String symbol;
  final String name;
  final String sector;
  final double price;
  final double changePercent;
  final int sessionVolume;
  final double volumeRatio;
  final double relativeStrength;
  final List<String> signals;
  final String scannedAt;

  factory RadarLiveItem.fromJson(Map<String, dynamic> json) => RadarLiveItem(
        symbol: json['symbol'] as String? ?? '',
        name: json['name'] as String? ?? '',
        sector: json['sector'] as String? ?? '',
        price: (json['price'] as num?)?.toDouble() ?? 0,
        changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
        sessionVolume: (json['sessionVolume'] as num?)?.toInt() ?? 0,
        volumeRatio: (json['volumeRatio'] as num?)?.toDouble() ?? 0,
        relativeStrength: (json['relativeStrength'] as num?)?.toDouble() ?? 0,
        signals: (json['signals'] as List<dynamic>?)
                ?.map((e) => e.toString())
                .toList() ??
            const [],
        scannedAt: json['scannedAt'] as String? ?? '',
      );
}

class RadarLiveSnapshot {
  const RadarLiveSnapshot({
    required this.exchange,
    required this.sessionDate,
    required this.scannedAt,
    required this.matchCount,
    required this.items,
  });

  final String exchange;
  final String sessionDate;
  final String scannedAt;
  final int matchCount;
  final List<RadarLiveItem> items;

  factory RadarLiveSnapshot.fromJson(Map<String, dynamic> json) => RadarLiveSnapshot(
        exchange: json['exchange'] as String? ?? '',
        sessionDate: json['sessionDate'] as String? ?? '',
        scannedAt: json['scannedAt'] as String? ?? '',
        matchCount: (json['matchCount'] as num?)?.toInt() ?? 0,
        items: (json['items'] as List<dynamic>?)
                ?.map((e) => RadarLiveItem.fromJson(e as Map<String, dynamic>))
                .toList() ??
            const [],
      );
}

class WatchlistItem {
  const WatchlistItem({
    required this.symbol,
    required this.name,
    required this.sector,
    required this.score,
    required this.changePercent,
    this.sectorLocked = false,
  });

  final String symbol;
  final String name;
  final String sector;
  final double score;
  final double changePercent;
  final bool sectorLocked;

  factory WatchlistItem.fromJson(Map<String, dynamic> json) => WatchlistItem(
        symbol: json['symbol'] as String? ?? '',
        name: json['name'] as String? ?? '',
        sector: json['sector'] as String? ?? '',
        score: (json['score'] as num?)?.toDouble() ?? 0,
        changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
        sectorLocked: json['sectorLocked'] as bool? ?? false,
      );
}

class CriterionBucket {
  const CriterionBucket({
    required this.bucketId,
    required this.accuracyPercent,
  });

  final String bucketId;
  final double accuracyPercent;

  factory CriterionBucket.fromJson(Map<String, dynamic> json) => CriterionBucket(
        bucketId: json['bucketId'] as String? ?? '',
        accuracyPercent: (json['accuracyPercent'] as num?)?.toDouble() ?? 0,
      );
}

class CriterionPhaseStat {
  const CriterionPhaseStat({
    required this.phase,
    required this.accuracyPercent,
  });

  final String phase;
  final double accuracyPercent;

  factory CriterionPhaseStat.fromJson(Map<String, dynamic> json) => CriterionPhaseStat(
        phase: json['phase'] as String? ?? '',
        accuracyPercent: (json['accuracyPercent'] as num?)?.toDouble() ?? 0,
      );
}

/// Accuracy của tiêu chí ở khung đo bổ sung (T+10, T+20).
class CriterionHorizonStat {
  const CriterionHorizonStat({
    required this.horizon,
    required this.accuracyPercent,
    required this.edgePercent,
    this.totalCount = 0,
  });

  final int horizon;
  final double accuracyPercent;
  final double edgePercent;
  final int totalCount;

  factory CriterionHorizonStat.fromJson(Map<String, dynamic> json) => CriterionHorizonStat(
        horizon: (json['horizon'] as num?)?.toInt() ?? 0,
        accuracyPercent: (json['accuracyPercent'] as num?)?.toDouble() ?? 0,
        edgePercent: (json['edgePercent'] as num?)?.toDouble() ?? 0,
        totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
      );
}

class CriterionAccuracy {
  const CriterionAccuracy({
    required this.id,
    required this.label,
    required this.successRatePercent,
    required this.sampleCount,
    this.group = '',
    this.rank = 0,
    this.reliabilityScore = 0,
    this.accuracyPercent = 0,
    this.hitCount = 0,
    this.totalCount = 0,
    this.avgScore = 0,
    this.weight = 1,
    this.accuracy7d = 0,
    this.recommendedAction = 'Keep',
    this.isActive = true,
    this.edgePercent,
    this.avgMfePercent,
    this.invalidationRatePercent,
    this.baselinePercent,
    this.buckets = const [],
    this.phases = const [],
    this.horizons = const [],
  });

  final String id;
  final String label;
  final double successRatePercent;
  final int sampleCount;
  final String group;
  final int rank;
  final double reliabilityScore;
  final double accuracyPercent;
  final int hitCount;
  final int totalCount;
  final double avgScore;
  final double weight;
  final double accuracy7d;
  final String recommendedAction;
  final bool isActive;
  final double? edgePercent;
  final double? avgMfePercent;
  final double? invalidationRatePercent;
  final double? baselinePercent;
  final List<CriterionBucket> buckets;
  final List<CriterionPhaseStat> phases;
  final List<CriterionHorizonStat> horizons;

  double get displayPercent =>
      reliabilityScore > 0 ? reliabilityScore : (accuracyPercent > 0 ? accuracyPercent : successRatePercent);

  factory CriterionAccuracy.fromJson(Map<String, dynamic> json) {
    final reliability = (json['reliabilityScore'] as num?)?.toDouble() ?? 0;
    final accuracy = (json['accuracyPercent'] as num?)?.toDouble() ?? 0;
    final legacy = (json['successRatePercent'] as num?)?.toDouble() ?? 0;
    final total = (json['totalCount'] as num?)?.toInt() ?? (json['sampleCount'] as num?)?.toInt() ?? 0;
    return CriterionAccuracy(
      id: json['id'] as String? ?? '',
      label: json['label'] as String? ?? json['name'] as String? ?? '',
      successRatePercent: legacy > 0 ? legacy : (reliability > 0 ? reliability : accuracy),
      sampleCount: total,
      group: json['group'] as String? ?? '',
      rank: (json['rank'] as num?)?.toInt() ?? 0,
      reliabilityScore: reliability,
      accuracyPercent: accuracy,
      hitCount: (json['hitCount'] as num?)?.toInt() ?? 0,
      totalCount: total,
      avgScore: (json['avgScore'] as num?)?.toDouble() ?? 0,
      weight: (json['weight'] as num?)?.toDouble() ?? 1,
      accuracy7d: (json['accuracy7d'] as num?)?.toDouble() ?? 0,
      recommendedAction: json['recommendedAction'] as String? ?? 'Keep',
      isActive: json['isActive'] as bool? ?? true,
      edgePercent: (json['edgePercent'] as num?)?.toDouble(),
      avgMfePercent: (json['avgMfePercent'] as num?)?.toDouble(),
      invalidationRatePercent: (json['invalidationRatePercent'] as num?)?.toDouble(),
      baselinePercent: (json['baselinePercent'] as num?)?.toDouble(),
      buckets: (json['buckets'] as List<dynamic>? ?? [])
          .map((e) => CriterionBucket.fromJson(e as Map<String, dynamic>))
          .toList(),
      phases: (json['phases'] as List<dynamic>? ?? [])
          .map((e) => CriterionPhaseStat.fromJson(e as Map<String, dynamic>))
          .toList(),
      horizons: (json['horizons'] as List<dynamic>? ?? [])
          .map((e) => CriterionHorizonStat.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

class CriterionGroupAccuracy {
  const CriterionGroupAccuracy({
    required this.groupId,
    required this.hitCount,
    required this.totalCount,
    required this.accuracyPercent,
    required this.avgScore,
    required this.recommendedAction,
    required this.keepCount,
    required this.watchCount,
    required this.removeCount,
    this.reliabilityScore,
    this.edgePercent,
  });

  final String groupId;
  final int hitCount;
  final int totalCount;
  final double accuracyPercent;
  final double avgScore;
  final String recommendedAction;
  final int keepCount;
  final int watchCount;
  final int removeCount;
  final double? reliabilityScore;
  final double? edgePercent;

  /// Khớp web: reliability khi > 0, không thì accuracyPercent.
  double get displayPercent =>
      (reliabilityScore ?? 0) > 0 ? reliabilityScore! : accuracyPercent;

  factory CriterionGroupAccuracy.fromJson(Map<String, dynamic> json) => CriterionGroupAccuracy(
        groupId: json['groupId'] as String? ?? '',
        hitCount: (json['hitCount'] as num?)?.toInt() ?? 0,
        totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
        accuracyPercent: (json['accuracyPercent'] as num?)?.toDouble() ?? 0,
        avgScore: (json['avgScore'] as num?)?.toDouble() ?? 0,
        recommendedAction: json['recommendedAction'] as String? ?? 'Keep',
        keepCount: (json['keepCount'] as num?)?.toInt() ?? 0,
        watchCount: (json['watchCount'] as num?)?.toInt() ?? 0,
        removeCount: (json['removeCount'] as num?)?.toInt() ?? 0,
        reliabilityScore: (json['reliabilityScore'] as num?)?.toDouble(),
        edgePercent: (json['edgePercent'] as num?)?.toDouble(),
      );
}

class WeeklyCriterionReview {
  const WeeklyCriterionReview({
    required this.id,
    required this.label,
    required this.group,
    required this.hitCount7d,
    required this.totalCount7d,
    required this.accuracy7d,
    required this.recommendedAction,
    this.reliability7d,
    this.edge7d,
  });

  final String id;
  final String label;
  final String group;
  final int hitCount7d;
  final int totalCount7d;
  final double accuracy7d;
  final String recommendedAction;
  final double? reliability7d;
  final double? edge7d;

  double get displayReliability7d =>
      (reliability7d ?? 0) > 0 ? reliability7d! : accuracy7d;

  factory WeeklyCriterionReview.fromJson(Map<String, dynamic> json) => WeeklyCriterionReview(
        id: json['id'] as String? ?? '',
        label: json['label'] as String? ?? '',
        group: json['group'] as String? ?? '',
        hitCount7d: (json['hitCount7d'] as num?)?.toInt() ?? 0,
        totalCount7d: (json['totalCount7d'] as num?)?.toInt() ?? 0,
        accuracy7d: (json['accuracy7d'] as num?)?.toDouble() ?? 0,
        recommendedAction: json['recommendedAction'] as String? ?? 'Remove',
        reliability7d: (json['reliability7d'] as num?)?.toDouble(),
        edge7d: (json['edge7d'] as num?)?.toDouble(),
      );
}

class CriterionStockRank {
  const CriterionStockRank({
    required this.symbol,
    required this.compositeScore,
    required this.topCriteria,
  });

  final String symbol;
  final double compositeScore;
  final List<CriterionScore> topCriteria;

  factory CriterionStockRank.fromJson(Map<String, dynamic> json) => CriterionStockRank(
        symbol: json['symbol'] as String? ?? '',
        compositeScore: (json['compositeScore'] as num?)?.toDouble() ?? 0,
        topCriteria: (json['topCriteria'] as List<dynamic>? ?? [])
            .map((e) => CriterionScore.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class CriteriaSummary {
  const CriteriaSummary({
    required this.criteria,
    this.groups = const [],
    this.weeklyReview = const [],
    this.topStocks = const [],
    this.statusMessage,
    this.generatedAt,
    this.weekStartDate,
    this.asOfDate,
  });

  final List<CriterionAccuracy> criteria;
  final List<CriterionGroupAccuracy> groups;
  final List<WeeklyCriterionReview> weeklyReview;
  final List<CriterionStockRank> topStocks;
  final String? statusMessage;
  final String? generatedAt;
  final String? weekStartDate;
  final String? asOfDate;

  factory CriteriaSummary.fromJson(Map<String, dynamic> json) => CriteriaSummary(
        criteria: (json['criteria'] as List<dynamic>? ?? [])
            .map((e) => CriterionAccuracy.fromJson(e as Map<String, dynamic>))
            .toList(),
        groups: (json['groups'] as List<dynamic>? ?? [])
            .map((e) => CriterionGroupAccuracy.fromJson(e as Map<String, dynamic>))
            .toList(),
        weeklyReview: (json['weeklyReview'] as List<dynamic>? ?? [])
            .map((e) => WeeklyCriterionReview.fromJson(e as Map<String, dynamic>))
            .toList(),
        topStocks: (json['topStocks'] as List<dynamic>? ?? [])
            .map((e) => CriterionStockRank.fromJson(e as Map<String, dynamic>))
            .toList(),
        statusMessage: json['statusMessage'] as String?,
        generatedAt: json['generatedAt'] as String?,
        weekStartDate: json['weekStartDate'] as String?,
        asOfDate: json['asOfDate'] as String?,
      );
}

class TradeEvent {
  const TradeEvent({
    required this.symbol,
    required this.label,
    required this.price,
    required this.volume,
    required this.valueVnd,
    required this.spreadPct,
    required this.bookImbalance,
    required this.foreignNetDelta,
    required this.sessionForeignNet,
    required this.sessionPropNet,
    required this.sessionPressure,
    required this.at,
    this.isAggregated = false,
  });

  final String symbol;
  final String label;
  final double price;
  final int volume;
  final double valueVnd;
  final double spreadPct;
  final int bookImbalance;
  final int foreignNetDelta;
  final int sessionForeignNet;
  final int sessionPropNet;
  final double sessionPressure;
  final String at;
  final bool isAggregated;

  factory TradeEvent.fromJson(Map<String, dynamic> json) => TradeEvent(
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        label: json['label'] as String? ?? 'TrungTinh',
        price: (json['price'] as num?)?.toDouble() ?? 0,
        volume: (json['volume'] as num?)?.toInt() ?? 0,
        valueVnd: (json['valueVnd'] as num?)?.toDouble() ??
            ((json['price'] as num?)?.toDouble() ?? 0) *
                1000 *
                ((json['volume'] as num?)?.toInt() ?? 0),
        spreadPct: (json['spreadPct'] as num?)?.toDouble() ?? 0,
        bookImbalance: (json['bookImbalance'] as num?)?.toInt() ?? 0,
        foreignNetDelta: (json['foreignNetDelta'] as num?)?.toInt() ?? 0,
        sessionForeignNet: (json['sessionForeignNet'] as num?)?.toInt() ?? 0,
        sessionPropNet: (json['sessionPropNet'] as num?)?.toInt() ?? 0,
        sessionPressure: (json['sessionPressure'] as num?)?.toDouble() ?? 0,
        at: json['at'] as String? ?? '',
        isAggregated: json['isAggregated'] as bool? ?? false,
      );
}

/// Nhãn VSA → tiếng Việt
String tradeLabelVi(String label) {
  switch (label) {
    case 'GomIm':
      return 'Gom im';
    case 'DayGia':
      return 'Đẩy giá';
    case 'Xa':
      return 'Xả hàng';
    case 'TrungTinh':
      return 'Trung tính';
    default:
      return label;
  }
}

@Deprecated('Use TradeEvent')
class TradePrint {
  const TradePrint({
    required this.symbol,
    required this.side,
    required this.price,
    required this.volume,
    required this.at,
  });

  final String symbol;
  final String side;
  final double price;
  final int volume;
  final String at;

  bool get isBuy => side == 'Buy';

  factory TradePrint.fromJson(Map<String, dynamic> json) => TradePrint(
        symbol: json['symbol'] as String? ?? '',
        side: json['side'] as String? ?? 'Buy',
        price: (json['price'] as num?)?.toDouble() ?? 0,
        volume: (json['volume'] as num?)?.toInt() ?? 0,
        at: json['at'] as String? ?? '',
      );
}

class IntradayMonitorStatus {
  const IntradayMonitorStatus({
    required this.enabled,
    required this.marketOpen,
    required this.status,
    this.lastScanAt,
    this.lastAlertsSent = 0,
    this.isStale = false,
  });

  final bool enabled;
  final bool marketOpen;
  final String status;
  final String? lastScanAt;
  final int lastAlertsSent;
  final bool isStale;

  factory IntradayMonitorStatus.fromJson(Map<String, dynamic> json) =>
      IntradayMonitorStatus(
        enabled: json['enabled'] as bool? ?? false,
        marketOpen: json['marketOpen'] as bool? ?? false,
        status: json['status'] as String? ?? '',
        lastScanAt: json['lastScanAt'] as String?,
        lastAlertsSent: (json['lastAlertsSent'] as num?)?.toInt() ?? 0,
        isStale: json['isStale'] as bool? ?? false,
      );
}

class ChartBar {
  const ChartBar({
    required this.time,
    required this.close,
    required this.volume,
    this.open,
    this.high,
    this.low,
  });

  final String time;
  final double close;
  final double volume;
  final double? open;
  final double? high;
  final double? low;

  factory ChartBar.fromJson(Map<String, dynamic> json) => ChartBar(
        time: json['time'] as String? ?? json['date'] as String? ?? '',
        close: (json['close'] as num?)?.toDouble() ?? 0,
        volume: (json['volume'] as num?)?.toDouble() ?? 0,
        open: (json['open'] as num?)?.toDouble(),
        high: (json['high'] as num?)?.toDouble(),
        low: (json['low'] as num?)?.toDouble(),
      );

  double get openVal => open ?? close;
  double get highVal => high ?? close;
  double get lowVal => low ?? close;
}

const chartOneYearSessions = 252;

List<ChartBar> chartBarsFromHistory(List<ChartBar> history) {
  if (history.length <= chartOneYearSessions) return history;
  return history.sublist(history.length - chartOneYearSessions);
}

/// Giới hạn số nến intraday để mỗi cây đủ rộng (FireAnt-style).
List<ChartBar> sliceChartBarsForInterval(List<ChartBar> bars, String interval) {
  if (interval == '1D') return chartBarsFromHistory(bars);
  const limits = {
    '1m': 180,
    '5m': 156,
    '15m': 128,
    '30m': 96,
    '1H': 120,
  };
  final limit = limits[interval] ?? 120;
  if (bars.length <= limit) return bars;
  return bars.sublist(bars.length - limit);
}

class StockChart {
  const StockChart({required this.symbol, required this.bars, this.interval = '1D'});

  final String symbol;
  final List<ChartBar> bars;
  final String interval;

  factory StockChart.fromJson(Map<String, dynamic> json) => StockChart(
        symbol: json['symbol'] as String? ?? '',
        interval: json['interval'] as String? ?? '1D',
        bars: (json['bars'] as List<dynamic>? ?? [])
            .map((e) => ChartBar.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class StockDetail {
  const StockDetail({
    required this.symbol,
    required this.name,
    required this.sector,
    required this.price,
    required this.changePercent,
    required this.score,
    required this.summary,
    this.volumeRatio = 0,
    this.relativeStrength = 0,
    this.activeSignals = const [],
    this.buyDecision = const BuyDecision(),
    this.entryPoint = const EntryPoint(),
    this.buyZone = 0,
    this.stopLoss = 0,
    this.resistance = 0,
    this.target = 0,
    this.flatBox,
    this.history = const [],
    this.patternScores = const [],
  });

  final String symbol;
  final String name;
  final String sector;
  final double price;
  final double changePercent;
  final double score;
  final String summary;
  final double volumeRatio;
  final double relativeStrength;
  final List<String> activeSignals;
  final BuyDecision buyDecision;
  final EntryPoint entryPoint;
  final double buyZone;
  final double stopLoss;
  final double resistance;
  final double target;
  final Map<String, dynamic>? flatBox;
  final List<ChartBar> history;
  final List<CriterionScore> patternScores;

  factory StockDetail.fromJson(Map<String, dynamic> json) {
    final buyDecision = BuyDecision.fromJson(json['buyDecision'] as Map<String, dynamic>?);
    final rootEntry = EntryPoint.fromJson(json['entryPoint'] as Map<String, dynamic>?);
    final entry = buyDecision.entryPoint.headline.isNotEmpty ? buyDecision.entryPoint : rootEntry;
    return StockDetail(
      symbol: json['symbol'] as String? ?? '',
      name: json['name'] as String? ?? '',
      sector: json['sector'] as String? ?? '',
      price: (json['price'] as num?)?.toDouble() ?? 0,
      changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
      score: (json['score'] as num?)?.toDouble() ?? 0,
      summary: json['summary'] as String? ?? '',
      volumeRatio: (json['volumeRatio'] as num?)?.toDouble() ?? 0,
      relativeStrength: (json['relativeStrength'] as num?)?.toDouble() ?? 0,
      activeSignals: (json['activeSignals'] as List<dynamic>? ?? []).map((e) => e.toString()).toList(),
      buyDecision: buyDecision,
      entryPoint: entry,
      buyZone: (json['buyZone'] as num?)?.toDouble() ?? 0,
      stopLoss: (json['stopLoss'] as num?)?.toDouble() ?? 0,
      resistance: (json['resistance'] as num?)?.toDouble() ?? 0,
      target: (json['target'] as num?)?.toDouble() ?? 0,
      flatBox: json['flatBox'] as Map<String, dynamic>?,
      history: (json['history'] as List<dynamic>? ?? [])
          .map((e) => ChartBar.fromJson(e as Map<String, dynamic>))
          .toList(),
      patternScores: (json['patternScores'] as List<dynamic>? ?? [])
          .map((e) => CriterionScore.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}

class PagedResult<T> {
  const PagedResult({required this.items});

  final List<T> items;
}
