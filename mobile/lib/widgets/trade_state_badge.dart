import 'package:flutter/material.dart';

import '../core/labels/trade_state_labels.dart';

class TradeStateBadge extends StatelessWidget {
  const TradeStateBadge({
    super.key,
    required this.trade,
    this.showReason = false,
  });

  final ResolvedTradeState trade;
  final bool showReason;

  @override
  Widget build(BuildContext context) {
    final style = tradeStateStyle(context, trade.state);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
          decoration: BoxDecoration(
            color: style.pillBg,
            borderRadius: BorderRadius.circular(6),
          ),
          child: Text(
            trade.label,
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w700,
              color: style.pillColor,
            ),
          ),
        ),
        if (showReason && trade.reason.isNotEmpty) ...[
          const SizedBox(height: 2),
          Text(
            trade.reason,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 9,
              height: 1.3,
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ],
    );
  }
}
