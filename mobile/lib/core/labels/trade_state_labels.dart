import 'package:flutter/material.dart';

import '../models/models.dart';
import '../theme/app_colors.dart';

const tradeStateLabelsVi = {
  'Avoid': 'Tránh',
  'Watchlist': 'Theo dõi',
  'AwaitingTrigger': 'Chờ kích hoạt',
  'Actionable': 'Vào ngay',
};

class ResolvedTradeState {
  const ResolvedTradeState({
    required this.state,
    required this.label,
    required this.reason,
  });

  final String state;
  final String label;
  final String reason;
}

ResolvedTradeState resolveBuyDecisionTradeState(BuyDecision decision) {
  final tradeState = decision.tradeState;
  if (tradeState != null && tradeState.isNotEmpty) {
    return ResolvedTradeState(
      state: tradeState,
      label: decision.tradeStateLabelVi ?? tradeStateLabelsVi[tradeState] ?? tradeState,
      reason: decision.tradeStateReason ?? '',
    );
  }
  return _inferLegacy(
    recommendation: decision.recommendation,
    entryStatus: decision.entryPoint.status,
    headline: decision.gateFailure ?? decision.entryPoint.headline,
    score: decision.buyScore ?? 0,
    onList: false,
  );
}

ResolvedTradeState resolveOpportunityTradeState(Opportunity item) {
  final tradeState = item.tradeState;
  if (tradeState != null && tradeState.isNotEmpty) {
    return ResolvedTradeState(
      state: tradeState,
      label: item.tradeStateLabelVi ?? tradeStateLabelsVi[tradeState] ?? tradeState,
      reason: item.tradeStateReason ?? '',
    );
  }
  return _inferLegacy(
    recommendation: item.recommendation,
    entryStatus: item.entryPointStatus,
    headline: item.entryPointHeadline,
    score: item.score,
    onList: true,
  );
}

ResolvedTradeState _inferLegacy({
  required String? recommendation,
  required String? entryStatus,
  required String? headline,
  required double score,
  required bool onList,
}) {
  final rec = recommendation ?? 'Avoid';
  if (rec == 'Avoid' && !onList) {
    return ResolvedTradeState(
      state: 'Avoid',
      label: tradeStateLabelsVi['Avoid']!,
      reason: headline ?? 'Không đạt tiêu chí tối thiểu',
    );
  }

  if (entryStatus == 'Ready' && rec != 'Avoid') {
    return ResolvedTradeState(
      state: 'Actionable',
      label: tradeStateLabelsVi['Actionable']!,
      reason: score >= 80 ? 'Mua mạnh — đạt chuẩn SmartMoney' : 'Đạt chuẩn SmartMoney',
    );
  }

  final text = headline ?? '';
  if (text.contains('MA stack') || text.contains('xu hướng dài hạn')) {
    return ResolvedTradeState(
      state: 'AwaitingTrigger',
      label: tradeStateLabelsVi['AwaitingTrigger']!,
      reason: text,
    );
  }

  if (onList || rec != 'Avoid') {
    return ResolvedTradeState(
      state: 'Watchlist',
      label: tradeStateLabelsVi['Watchlist']!,
      reason: text.isNotEmpty ? text : 'Chưa phá vỡ nền / Chờ phiên kích hoạt',
    );
  }

  return ResolvedTradeState(
    state: 'Avoid',
    label: tradeStateLabelsVi['Avoid']!,
    reason: text.isNotEmpty ? text : 'Không đạt tiêu chí tối thiểu',
  );
}

class TradeStateStyle {
  const TradeStateStyle({
    required this.bg,
    required this.border,
    required this.accent,
    required this.pillBg,
    required this.pillColor,
  });

  final Color bg;
  final Color border;
  final Color accent;
  final Color pillBg;
  final Color pillColor;
}

TradeStateStyle tradeStateStyle(BuildContext context, String state) {
  final scheme = Theme.of(context).colorScheme;
  final isDark = Theme.of(context).brightness == Brightness.dark;

  switch (state) {
    case 'Actionable':
      return TradeStateStyle(
        bg: AppColors.greenBg(context),
        border: scheme.primary,
        accent: scheme.primary,
        pillBg: scheme.primary,
        pillColor: isDark ? const Color(0xFF002022) : Colors.white,
      );
    case 'AwaitingTrigger':
      final amber = isDark ? AppColors.darkWarning : AppColors.lightWarning;
      return TradeStateStyle(
        bg: AppColors.amberBg(context),
        border: amber,
        accent: amber,
        pillBg: AppColors.amberBg(context),
        pillColor: amber,
      );
    case 'Watchlist':
      return TradeStateStyle(
        bg: scheme.secondaryContainer.withValues(alpha: 0.35),
        border: scheme.secondary,
        accent: scheme.secondary,
        pillBg: scheme.secondaryContainer.withValues(alpha: 0.5),
        pillColor: scheme.secondary,
      );
    default:
      return TradeStateStyle(
        bg: AppColors.neutralBg(context),
        border: scheme.outlineVariant,
        accent: scheme.onSurfaceVariant,
        pillBg: AppColors.redBg(context),
        pillColor: scheme.error,
      );
  }
}
