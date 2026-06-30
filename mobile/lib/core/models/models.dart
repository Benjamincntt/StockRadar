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
    this.entryPointStatus,
  });

  final String symbol;
  final String name;
  final double score;
  final double price;
  final double changePercent;
  final String sector;
  final String? recommendation;
  final double? predictedHitPercent;
  final String? entryPointStatus;

  factory Opportunity.fromJson(Map<String, dynamic> json) => Opportunity(
        symbol: json['symbol'] as String? ?? '',
        name: json['name'] as String? ?? '',
        score: (json['score'] as num?)?.toDouble() ?? 0,
        price: (json['price'] as num?)?.toDouble() ?? 0,
        changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
        sector: json['sector'] as String? ?? '',
        recommendation: json['recommendation'] as String?,
        predictedHitPercent: (json['predictedHitPercent'] as num?)?.toDouble(),
        entryPointStatus: json['entryPoint']?['status'] as String?,
      );
}

class OpportunitiesList {
  const OpportunitiesList({
    required this.items,
    this.generatedAt,
    this.statusMessage,
    this.hasFreshData = false,
  });

  final List<Opportunity> items;
  final String? generatedAt;
  final String? statusMessage;
  final bool hasFreshData;

  factory OpportunitiesList.fromJson(Map<String, dynamic> json) => OpportunitiesList(
        items: (json['items'] as List<dynamic>? ?? [])
            .map((e) => Opportunity.fromJson(e as Map<String, dynamic>))
            .toList(),
        generatedAt: json['generatedAt'] as String?,
        statusMessage: json['statusMessage'] as String?,
        hasFreshData: json['hasFreshData'] as bool? ?? false,
      );
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

class WatchlistItem {
  const WatchlistItem({
    required this.symbol,
    required this.name,
    required this.sector,
    required this.score,
    required this.changePercent,
  });

  final String symbol;
  final String name;
  final String sector;
  final double score;
  final double changePercent;

  factory WatchlistItem.fromJson(Map<String, dynamic> json) => WatchlistItem(
        symbol: json['symbol'] as String? ?? '',
        name: json['name'] as String? ?? '',
        sector: json['sector'] as String? ?? '',
        score: (json['score'] as num?)?.toDouble() ?? 0,
        changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
      );
}

class CriterionAccuracy {
  const CriterionAccuracy({
    required this.id,
    required this.label,
    required this.successRatePercent,
    required this.sampleCount,
  });

  final String id;
  final String label;
  final double successRatePercent;
  final int sampleCount;

  factory CriterionAccuracy.fromJson(Map<String, dynamic> json) => CriterionAccuracy(
        id: json['id'] as String? ?? '',
        label: json['label'] as String? ?? json['name'] as String? ?? '',
        successRatePercent: (json['successRatePercent'] as num?)?.toDouble() ?? 0,
        sampleCount: (json['sampleCount'] as num?)?.toInt() ?? 0,
      );
}

class CriteriaSummary {
  const CriteriaSummary({
    required this.criteria,
    this.statusMessage,
    this.generatedAt,
  });

  final List<CriterionAccuracy> criteria;
  final String? statusMessage;
  final String? generatedAt;

  factory CriteriaSummary.fromJson(Map<String, dynamic> json) => CriteriaSummary(
        criteria: (json['criteria'] as List<dynamic>? ?? [])
            .map((e) => CriterionAccuracy.fromJson(e as Map<String, dynamic>))
            .toList(),
        statusMessage: json['statusMessage'] as String?,
        generatedAt: json['generatedAt'] as String?,
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
  });

  final String time;
  final double close;
  final double volume;

  factory ChartBar.fromJson(Map<String, dynamic> json) => ChartBar(
        time: json['time'] as String? ?? json['date'] as String? ?? '',
        close: (json['close'] as num?)?.toDouble() ?? 0,
        volume: (json['volume'] as num?)?.toDouble() ?? 0,
      );
}

class StockChart {
  const StockChart({required this.symbol, required this.bars});

  final String symbol;
  final List<ChartBar> bars;

  factory StockChart.fromJson(Map<String, dynamic> json) => StockChart(
        symbol: json['symbol'] as String? ?? '',
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
    this.buyScore,
    this.recommendation,
    this.predictedHitPercent,
  });

  final String symbol;
  final String name;
  final String sector;
  final double price;
  final double changePercent;
  final double score;
  final String summary;
  final double? buyScore;
  final String? recommendation;
  final double? predictedHitPercent;

  factory StockDetail.fromJson(Map<String, dynamic> json) {
    final buyDecision = json['buyDecision'] as Map<String, dynamic>?;
    return StockDetail(
      symbol: json['symbol'] as String? ?? '',
      name: json['name'] as String? ?? '',
      sector: json['sector'] as String? ?? '',
      price: (json['price'] as num?)?.toDouble() ?? 0,
      changePercent: (json['changePercent'] as num?)?.toDouble() ?? 0,
      score: (json['score'] as num?)?.toDouble() ?? 0,
      summary: json['summary'] as String? ?? '',
      buyScore: (buyDecision?['buyScore'] as num?)?.toDouble(),
      recommendation: buyDecision?['recommendation'] as String?,
      predictedHitPercent: (buyDecision?['predictedHitPercent'] as num?)?.toDouble(),
    );
  }
}

class PagedResult<T> {
  const PagedResult({required this.items});

  final List<T> items;
}
