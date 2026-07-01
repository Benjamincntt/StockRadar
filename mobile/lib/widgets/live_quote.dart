import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/services/market_hub_service.dart';
import '../core/theme/app_theme.dart';
import 'score_pill.dart';

class LiveQuoteColumn extends StatelessWidget {
  const LiveQuoteColumn({
    super.key,
    required this.symbol,
    required this.fallbackPrice,
    required this.fallbackChange,
  });

  final String symbol;
  final double fallbackPrice;
  final double fallbackChange;

  @override
  Widget build(BuildContext context) {
    final hub = context.watch<MarketHubService>();
    final live = hub.quote(symbol);
    final price = live?.price ?? fallbackPrice;
    final change = live?.changePercent ?? fallbackChange;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(formatPrice(price), style: dataFont(context, weight: FontWeight.w700)),
        ChangePill(change),
      ],
    );
  }
}

class LivePriceText extends StatelessWidget {
  const LivePriceText({
    super.key,
    required this.symbol,
    required this.fallbackPrice,
    this.style,
  });

  final String symbol;
  final double fallbackPrice;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    final hub = context.watch<MarketHubService>();
    final price = hub.quote(symbol)?.price ?? fallbackPrice;
    return Text(
      formatPrice(price),
      style: style ?? dataFont(context, size: 28, weight: FontWeight.w700),
    );
  }
}

class LiveChangePill extends StatelessWidget {
  const LiveChangePill({super.key, required this.symbol, required this.fallback});

  final String symbol;
  final double fallback;

  @override
  Widget build(BuildContext context) {
    final hub = context.watch<MarketHubService>();
    final change = hub.quote(symbol)?.changePercent ?? fallback;
    return ChangePill(change);
  }
}

class LiveStatusBadge extends StatelessWidget {
  const LiveStatusBadge({super.key});

  @override
  Widget build(BuildContext context) {
    final hub = context.watch<MarketHubService>();
    final scheme = Theme.of(context).colorScheme;
    final (label, color) = switch (hub.connectionState) {
      LiveConnectionState.connected => ('Live', scheme.primary),
      LiveConnectionState.connecting => ('…', scheme.onSurfaceVariant),
      LiveConnectionState.reconnecting => ('↻', scheme.onSurfaceVariant),
      LiveConnectionState.disconnected => ('Off', scheme.onSurfaceVariant),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(width: 6, height: 6, decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
          const SizedBox(width: 4),
          Text(label, style: TextStyle(fontSize: 10, fontWeight: FontWeight.w600, color: color)),
        ],
      ),
    );
  }
}
