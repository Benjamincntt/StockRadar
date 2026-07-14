import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';

import '../core/models/models.dart';
import 'score_pill.dart';

class ChartColors {
  const ChartColors({
    required this.bg,
    required this.grid,
    required this.text,
    required this.muted,
    required this.green,
    required this.red,
    required this.crosshair,
    required this.overlay,
    required this.ma10,
    required this.ma50,
    required this.volLabel,
    required this.priceRef,
  });

  final Color bg;
  final Color grid;
  final Color text;
  final Color muted;
  final Color green;
  final Color red;
  final Color crosshair;
  final Color overlay;
  final Color ma10;
  final Color ma50;
  final Color volLabel;
  final Color priceRef;

  /// Palette gần FireAnt: xanh #26A69A, đỏ #EF5350, MA10 cam, MA50 cyan.
  factory ChartColors.of(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    if (isDark) {
      return const ChartColors(
        bg: Color(0xFF111318),
        grid: Color(0xFF1E232B),
        text: Color(0xFFE8EAED),
        muted: Color(0xFF8B939E),
        green: Color(0xFF26A69A),
        red: Color(0xFFEF5350),
        crosshair: Color(0x44FFFFFF),
        overlay: Color(0xE0111318),
        ma10: Color(0xFFFF9800),
        ma50: Color(0xFF26C6DA),
        volLabel: Color(0xFFCE93D8),
        priceRef: Color(0xFF64B5F6),
      );
    }
    return const ChartColors(
      bg: Color(0xFFFFFFFF),
      grid: Color(0xFFEEF1F4),
      text: Color(0xFF212121),
      muted: Color(0xFF78909C),
      green: Color(0xFF26A69A),
      red: Color(0xFFEF5350),
      crosshair: Color(0x33000000),
      overlay: Color(0xEBFFFFFF),
      ma10: Color(0xFFFF9800),
      ma50: Color(0xFF00ACC1),
      volLabel: Color(0xFF8E24AA),
      priceRef: Color(0xFF1E88E5),
    );
  }
}

class ChartIndicatorSeries {
  ChartIndicatorSeries._({
    required this.ma10,
    required this.ma50,
    required this.volMa5,
    required this.volMa10,
  });

  final List<double?> ma10;
  final List<double?> ma50;
  final List<double?> volMa5;
  final List<double?> volMa10;

  factory ChartIndicatorSeries.fromBars(List<ChartBar> bars) {
    final closes = bars.map((b) => b.close).toList();
    final volumes = bars.map((b) => b.volume).toList();
    return ChartIndicatorSeries._(
      ma10: _sma(closes, 10),
      ma50: _sma(closes, 50),
      volMa5: _sma(volumes, 5),
      volMa10: _sma(volumes, 10),
    );
  }

  static List<double?> _sma(List<double> values, int period) {
    final out = List<double?>.filled(values.length, null);
    if (values.length < period) return out;
    for (var i = period - 1; i < values.length; i++) {
      var sum = 0.0;
      for (var j = i - period + 1; j <= i; j++) {
        sum += values[j];
      }
      out[i] = sum / period;
    }
    return out;
  }
}

class SparklineMini extends StatelessWidget {
  const SparklineMini({
    super.key,
    required this.closes,
    this.fallbackChange = 0,
    this.width = 56,
    this.height = 28,
  });

  final List<double> closes;
  final double fallbackChange;
  final double width;
  final double height;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (closes.length < 2) {
      return SizedBox(
        width: width,
        height: height,
        child: Center(
          child: Text(
            '${fallbackChange >= 0 ? '+' : ''}${fallbackChange.toStringAsFixed(1)}%',
            style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
          ),
        ),
      );
    }
    final minY = closes.reduce((a, b) => a < b ? a : b);
    final maxY = closes.reduce((a, b) => a > b ? a : b);
    final spots = closes.asMap().entries.map((e) => FlSpot(e.key.toDouble(), e.value)).toList();
    final up = closes.last >= closes.first;
    return SizedBox(
      width: width,
      height: height,
      child: LineChart(
        LineChartData(
          minY: minY,
          maxY: maxY,
          gridData: const FlGridData(show: false),
          titlesData: const FlTitlesData(show: false),
          borderData: FlBorderData(show: false),
          lineTouchData: const LineTouchData(enabled: false),
          lineBarsData: [
            LineChartBarData(
              spots: spots,
              isCurved: true,
              color: up ? scheme.primary : scheme.error,
              barWidth: 1.5,
              dotData: const FlDotData(show: false),
            ),
          ],
        ),
      ),
    );
  }
}

class ChartTimeframeBar extends StatelessWidget {
  const ChartTimeframeBar({super.key, required this.value, required this.onChanged});

  static const intervals = ['1m', '5m', '15m', '1H', '1D'];
  static const labels = {'1D': '1D', '1H': '1h', '15m': '15m', '5m': '5m', '1m': '1m'};

  final String value;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    const activeBlue = Color(0xFF1976D2);
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: intervals.map((iv) {
          final active = value == iv;
          return GestureDetector(
            onTap: () => onChanged(iv),
            child: Container(
              margin: const EdgeInsets.only(right: 4),
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                border: Border(
                  bottom: BorderSide(
                    color: active ? activeBlue : Colors.transparent,
                    width: 2,
                  ),
                ),
              ),
              child: Text(
                labels[iv] ?? iv,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: active ? FontWeight.w700 : FontWeight.w500,
                  color: active ? activeBlue : const Color(0xFF78909C),
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}

class PriceVolumeChart extends StatefulWidget {
  const PriceVolumeChart({
    super.key,
    required this.bars,
    required this.interval,
    required this.symbol,
    required this.name,
    this.loading = false,
    this.livePrice,
    this.liveChangePercent,
  });

  final List<ChartBar> bars;
  final String interval;
  final String symbol;
  final String name;
  final bool loading;
  final double? livePrice;
  final double? liveChangePercent;

  @override
  State<PriceVolumeChart> createState() => _PriceVolumeChartState();
}

class _PriceVolumeChartState extends State<PriceVolumeChart> {
  int? _hoverIndex;
  final ScrollController _scrollController = ScrollController();

  static const _intervalLabels = {
    '1D': 'Ngày',
    '1H': '1 giờ',
    '30m': '30 phút',
    '15m': '15 phút',
    '5m': '5 phút',
    '1m': '1 phút',
  };

  @override
  void dispose() {
    _scrollController.dispose();
    super.dispose();
  }

  @override
  void didUpdateWidget(covariant PriceVolumeChart oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.interval != widget.interval || oldWidget.bars.length != widget.bars.length) {
      _hoverIndex = null;
      WidgetsBinding.instance.addPostFrameCallback((_) => _scrollToEnd());
    }
  }

  void _scrollToEnd() {
    if (!_scrollController.hasClients) return;
    _scrollController.jumpTo(_scrollController.position.maxScrollExtent);
  }

  List<ChartBar> get _data {
    final bars = List<ChartBar>.from(widget.bars);
    if (bars.isNotEmpty && widget.livePrice != null) {
      final last = bars.last;
      final live = widget.livePrice!;
      bars[bars.length - 1] = ChartBar(
        time: last.time,
        close: live,
        volume: last.volume,
        open: last.open,
        high: last.highVal > live ? last.highVal : live,
        low: last.lowVal < live ? last.lowVal : live,
      );
    }
    return sliceChartBarsForInterval(bars, widget.interval);
  }

  @override
  Widget build(BuildContext context) {
    final chartColors = ChartColors.of(context);
    final data = _data;
    final isIntraday = widget.interval != '1D';

    if (!widget.loading && data.isEmpty) {
      return SizedBox(
        height: 300,
        child: Center(
          child: Text(
            'Không có dữ liệu khung ${_intervalLabels[widget.interval] ?? widget.interval}',
            style: TextStyle(fontSize: 13, color: chartColors.muted),
          ),
        ),
      );
    }

    final activeIndex = _hoverIndex ?? (data.isNotEmpty ? data.length - 1 : null);
    final activeBar = activeIndex != null && activeIndex < data.length ? data[activeIndex] : null;

    double change = 0;
    double changePct = 0;
    if (activeBar != null && activeIndex != null) {
      if (activeIndex > 0) {
        change = activeBar.close - data[activeIndex - 1].close;
      } else {
        change = activeBar.close - activeBar.openVal;
      }
      if (widget.liveChangePercent != null && activeIndex == data.length - 1) {
        changePct = widget.liveChangePercent!;
      } else if (activeBar.openVal > 0) {
        changePct = ((activeBar.close - activeBar.openVal) / activeBar.openVal) * 100;
      }
    }
    final bullish = change >= 0;
    final accent = bullish ? chartColors.green : chartColors.red;
    final indicators = ChartIndicatorSeries.fromBars(data);
    final idx = activeIndex ?? (data.isEmpty ? 0 : data.length - 1);
    final ma10Val = idx < indicators.ma10.length ? indicators.ma10[idx] : null;
    final ma50Val = idx < indicators.ma50.length ? indicators.ma50[idx] : null;
    final volMa5Val = idx < indicators.volMa5.length ? indicators.volMa5[idx] : null;
    final volMa10Val = idx < indicators.volMa10.length ? indicators.volMa10[idx] : null;
    final lastClose = activeBar?.close ?? widget.livePrice;

    return Container(
      decoration: BoxDecoration(
        color: chartColors.bg,
        borderRadius: BorderRadius.circular(10),
      ),
      clipBehavior: Clip.antiAlias,
      child: Stack(
        children: [
          Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(12, 10, 12, 4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Wrap(
                      spacing: 14,
                      runSpacing: 4,
                      children: [
                        if (ma10Val != null)
                          _indicatorChip('MA10', formatPrice(ma10Val), chartColors.ma10),
                        if (ma50Val != null)
                          _indicatorChip('MA50', formatPrice(ma50Val), chartColors.ma50),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Wrap(
                      spacing: 14,
                      runSpacing: 4,
                      children: [
                        if (activeBar != null)
                          _indicatorChip('VOL', formatVolumeFull(activeBar.volume), chartColors.volLabel),
                        if (volMa5Val != null)
                          _indicatorChip('MA5', formatVolumeFull(volMa5Val), chartColors.ma10),
                        if (volMa10Val != null)
                          _indicatorChip('MA10', formatVolumeFull(volMa10Val), chartColors.ma50),
                      ],
                    ),
                  ],
                ),
              ),
              if (activeBar != null && _hoverIndex != null)
                Padding(
                  padding: const EdgeInsets.fromLTRB(12, 0, 12, 6),
                  child: Text.rich(
                    TextSpan(
                      style: TextStyle(fontSize: 11, color: chartColors.text, height: 1.25),
                      children: [
                        TextSpan(text: 'O ', style: TextStyle(color: chartColors.muted)),
                        TextSpan(text: formatPrice(activeBar.openVal), style: const TextStyle(fontWeight: FontWeight.w600)),
                        TextSpan(text: '  H ', style: TextStyle(color: chartColors.muted)),
                        TextSpan(text: formatPrice(activeBar.highVal), style: TextStyle(fontWeight: FontWeight.w600, color: chartColors.green)),
                        TextSpan(text: '  L ', style: TextStyle(color: chartColors.muted)),
                        TextSpan(text: formatPrice(activeBar.lowVal), style: TextStyle(fontWeight: FontWeight.w600, color: chartColors.red)),
                        TextSpan(text: '  C ', style: TextStyle(color: chartColors.muted)),
                        TextSpan(text: formatPrice(activeBar.close), style: TextStyle(fontWeight: FontWeight.w700, color: accent)),
                        TextSpan(
                          text: '  ${change >= 0 ? '+' : ''}${formatPrice(change)} (${changePct >= 0 ? '+' : ''}${changePct.toStringAsFixed(2)}%)',
                          style: TextStyle(fontWeight: FontWeight.w600, color: accent),
                        ),
                      ],
                    ),
                  ),
                ),
              SizedBox(
                height: 320,
                child: LayoutBuilder(
                  builder: (context, constraints) {
                    const padL = 2.0;
                    const padR = 44.0;
                    const slotWIntraday = 8.0;
                    const slotWDaily = 7.0;
                    final viewportW = constraints.maxWidth;
                    final useFixedSlot = isIntraday || widget.interval == '1D';
                    final slotOverride = isIntraday ? slotWIntraday : (widget.interval == '1D' ? slotWDaily : null);
                    final plotW = slotOverride != null
                        ? (data.length * slotOverride).clamp(viewportW - padL - padR, double.infinity)
                        : viewportW - padL - padR;

                    Widget chartBody(double width) {
                      return GestureDetector(
                        behavior: HitTestBehavior.opaque,
                        onPanUpdate: (d) => _updateHover(d.localPosition.dx, width, padL, padR, data.length, slotOverride),
                        onPanEnd: (_) => setState(() => _hoverIndex = null),
                        onTapDown: (d) => _updateHover(d.localPosition.dx, width, padL, padR, data.length, slotOverride),
                        onTapUp: (_) => setState(() => _hoverIndex = null),
                        child: CustomPaint(
                          size: Size(width, 320),
                          painter: _CandlestickPainter(
                            bars: data,
                            colors: chartColors,
                            indicators: indicators,
                            activeIndex: activeIndex,
                            interval: widget.interval,
                            padL: padL,
                            padR: padR,
                            plotW: plotW,
                            slotWOverride: slotOverride,
                            lastClose: lastClose,
                            showCrosshair: _hoverIndex != null,
                          ),
                        ),
                      );
                    }

                    if (!useFixedSlot || plotW <= viewportW - padL - padR) {
                      return chartBody(viewportW);
                    }

                    final contentW = padL + plotW + padR;
                    return Scrollbar(
                      controller: _scrollController,
                      thumbVisibility: data.length > 40,
                      child: SingleChildScrollView(
                        controller: _scrollController,
                        scrollDirection: Axis.horizontal,
                        child: chartBody(contentW),
                      ),
                    );
                  },
                ),
              ),
              if (widget.interval == '1D' && data.length >= 2)
                _PerformanceStrip(bars: data, colors: chartColors),
              if (data.isNotEmpty) ...[
                Builder(
                  builder: (context) {
                    final sessionBar = data.last;
                    return _SessionStatsRow(
                      reference: _sessionReference(data, sessionBar.close, widget.liveChangePercent),
                      open: sessionBar.openVal,
                      high: sessionBar.highVal,
                      low: sessionBar.lowVal,
                      colors: chartColors,
                    );
                  },
                ),
              ],
            ],
          ),
          if (widget.loading)
            Positioned.fill(
              child: Container(
                color: chartColors.overlay,
                child: Center(
                  child: Text(
                    'Đang tải ${_intervalLabels[widget.interval] ?? widget.interval}...',
                    style: TextStyle(fontSize: 12, color: chartColors.muted),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }

  void _updateHover(double x, double width, double padL, double padR, int count, double? slotOverride) {
    if (count == 0) return;
    final plotW = slotOverride != null ? count * slotOverride : width - padL - padR;
    final slotW = slotOverride ?? plotW / count;
    final rel = x - padL;
    final idx = (rel / slotW).floor().clamp(0, count - 1);
    setState(() => _hoverIndex = idx);
  }

  double? _sessionReference(List<ChartBar> data, double? lastClose, double? liveChangePercent) {
    if (lastClose != null && lastClose > 0 && liveChangePercent != null) {
      return lastClose / (1 + liveChangePercent / 100);
    }
    if (data.length >= 2) return data[data.length - 2].close;
    return null;
  }

  Widget _indicatorChip(String label, String value, Color color) {
    return RichText(
      text: TextSpan(
        style: const TextStyle(fontSize: 11, height: 1.2),
        children: [
          TextSpan(text: '$label ', style: TextStyle(color: color, fontWeight: FontWeight.w600)),
          TextSpan(text: value, style: TextStyle(color: color, fontWeight: FontWeight.w700)),
        ],
      ),
    );
  }
}

class _CandlestickPainter extends CustomPainter {
  _CandlestickPainter({
    required this.bars,
    required this.colors,
    required this.indicators,
    required this.activeIndex,
    required this.interval,
    required this.padL,
    required this.padR,
    required this.plotW,
    this.slotWOverride,
    this.lastClose,
    this.showCrosshair = false,
  });

  final List<ChartBar> bars;
  final ChartColors colors;
  final ChartIndicatorSeries indicators;
  final int? activeIndex;
  final String interval;
  final double padL;
  final double padR;
  final double plotW;
  final double? slotWOverride;
  final double? lastClose;
  final bool showCrosshair;

  @override
  void paint(Canvas canvas, Size size) {
    if (bars.isEmpty) return;

    const padTop = 8.0;
    const padBottom = 20.0;
    const gap = 6.0;
    final plotH = size.height - padTop - padBottom;
    final volumeH = plotH * 0.24;
    final priceH = plotH - volumeH - gap;
    final priceTop = padTop;
    final volumeTop = padTop + priceH + gap;

    final lows = bars.map((b) => b.lowVal);
    final highs = bars.map((b) => b.highVal);
    var minP = lows.reduce((a, b) => a < b ? a : b);
    var maxP = highs.reduce((a, b) => a > b ? a : b);
    final pad = (maxP - minP) * 0.05;
    minP -= pad > 0 ? pad : maxP * 0.02;
    maxP += pad > 0 ? pad : maxP * 0.02;
    final maxV = bars.map((b) => b.volume).reduce((a, b) => a > b ? a : b);
    final slotW = slotWOverride ?? (plotW / bars.length);
    final bodyW = (slotW * 0.72).clamp(2.5, 9.0);

    double priceY(double price) {
      final range = maxP - minP;
      return priceTop + priceH - ((price - minP) / (range == 0 ? 1 : range)) * priceH;
    }

    double volY(double volume) => volumeTop + volumeH - (volume / (maxV == 0 ? 1 : maxV)) * volumeH;

    final gridPaint = Paint()
      ..color = colors.grid
      ..strokeWidth = 0.8;
    for (var i = 0; i <= 3; i++) {
      final tick = minP + (maxP - minP) * i / 3;
      final y = priceY(tick);
      canvas.drawLine(Offset(padL, y), Offset(padL + plotW, y), gridPaint);
      _drawText(canvas, formatPrice(tick), Offset(padL + plotW + 3, y), colors.muted, 9, TextAlign.left);
    }

    if (lastClose != null) {
      _drawHLine(canvas, padL, plotW, priceY(lastClose!), colors.priceRef, formatPrice(lastClose!), dashed: true);
    }

    canvas.drawLine(
      Offset(padL, volumeTop - 0.5),
      Offset(padL + plotW, volumeTop - 0.5),
      Paint()
        ..color = colors.grid
        ..strokeWidth = 0.8,
    );

    for (var i = 0; i < bars.length; i++) {
      final bar = bars[i];
      final cx = padL + (i + 0.5) * slotW;
      final up = bar.close >= bar.openVal;
      final color = up ? colors.green : colors.red;
      _drawCandle(canvas, cx, bar, color, bodyW, priceY);

      final vh = volY(bar.volume);
      final volRect = Rect.fromLTWH(cx - bodyW / 2, vh, bodyW, volumeTop + volumeH - vh);
      canvas.drawRect(volRect, Paint()..color = color.withValues(alpha: 0.55));
    }

    _drawMaLine(canvas, indicators.ma10, slotW, priceY, colors.ma10, padL, stroke: 1.6);
    _drawMaLine(canvas, indicators.ma50, slotW, priceY, colors.ma50, padL, stroke: 1.4);
    _drawMaLine(canvas, indicators.volMa5, slotW, volY, colors.ma10.withValues(alpha: 0.75), padL, stroke: 1.0);
    _drawMaLine(canvas, indicators.volMa10, slotW, volY, colors.ma50.withValues(alpha: 0.75), padL, stroke: 1.0);

    if (showCrosshair && activeIndex != null && activeIndex! >= 0 && activeIndex! < bars.length) {
      final cx = padL + (activeIndex! + 0.5) * slotW;
      final dashPaint = Paint()
        ..color = colors.crosshair
        ..strokeWidth = 1;
      _drawDashedLine(canvas, Offset(cx, priceTop), Offset(cx, volumeTop + volumeH), dashPaint);
    }

    final tickIndices = interval == '1D' ? _axisTickIndices(bars.length) : _intradayTickIndices(bars.length);
    for (final i in tickIndices) {
      if (i < 0 || i >= bars.length) continue;
      final cx = padL + (i + 0.5) * slotW;
      final prev = i > 0 ? bars[i - 1].time : null;
      _drawText(
        canvas,
        _formatAxisTime(bars[i].time, prev),
        Offset(cx, size.height - 4),
        colors.muted,
        9,
        TextAlign.center,
      );
    }
  }

  void _drawHLine(Canvas canvas, double padL, double plotW, double y, Color color, String label, {bool dashed = false}) {
    final paint = Paint()
      ..color = color.withValues(alpha: dashed ? 0.85 : 0.45)
      ..strokeWidth = dashed ? 1.0 : 0.9;
    if (dashed) {
      _drawDashedHLine(canvas, Offset(padL, y), Offset(padL + plotW, y), paint);
    } else {
      canvas.drawLine(Offset(padL, y), Offset(padL + plotW, y), paint..style = PaintingStyle.stroke);
    }
    _drawText(canvas, label, Offset(padL + plotW + 3, y), color, 9, TextAlign.left);
  }

  void _drawMaLine(
    Canvas canvas,
    List<double?> series,
    double slotW,
    double Function(double) yMap,
    Color color,
    double padL, {
    double stroke = 1.4,
  }) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round
      ..style = PaintingStyle.stroke;
    Offset? prev;
    for (var i = 0; i < series.length; i++) {
      final v = series[i];
      if (v == null) {
        prev = null;
        continue;
      }
      final pt = Offset(padL + (i + 0.5) * slotW, yMap(v));
      if (prev != null) {
        canvas.drawLine(prev, pt, paint);
      }
      prev = pt;
    }
  }

  void _drawDashedHLine(Canvas canvas, Offset start, Offset end, Paint paint) {
    const dash = 4.0;
    const gapLen = 3.5;
    var x = start.dx;
    while (x < end.dx) {
      final x2 = (x + dash).clamp(start.dx, end.dx);
      canvas.drawLine(Offset(x, start.dy), Offset(x2, start.dy), paint);
      x += dash + gapLen;
    }
  }

  void _drawCandle(Canvas canvas, double cx, ChartBar bar, Color color, double bodyW, double Function(double) priceY) {
    final highY = priceY(bar.highVal);
    final lowY = priceY(bar.lowVal);
    final half = bodyW / 2;
    final bodyTop = priceY(bar.openVal > bar.close ? bar.openVal : bar.close);
    final bodyBottom = priceY(bar.openVal < bar.close ? bar.openVal : bar.close);
    final bodyH = (bodyBottom - bodyTop).clamp(1.5, double.infinity);

    final wick = Paint()
      ..color = color
      ..strokeWidth = (bodyW * 0.18).clamp(1.0, 1.8)
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(Offset(cx, highY), Offset(cx, lowY), wick);

    final bodyRect = RRect.fromRectAndRadius(
      Rect.fromLTWH(cx - half, bodyTop, bodyW, bodyH),
      const Radius.circular(0.5),
    );
    canvas.drawRRect(bodyRect, Paint()..color = color);
  }

  void _drawDashedLine(Canvas canvas, Offset start, Offset end, Paint paint) {
    const dash = 4.0;
    const gapLen = 3.0;
    var y = start.dy;
    while (y < end.dy) {
      final y2 = (y + dash).clamp(start.dy, end.dy);
      canvas.drawLine(Offset(start.dx, y), Offset(start.dx, y2), paint);
      y += dash + gapLen;
    }
  }

  void _drawText(Canvas canvas, String text, Offset offset, Color color, double size, TextAlign align) {
    final tp = TextPainter(
      text: TextSpan(text: text, style: TextStyle(color: color, fontSize: size, fontWeight: FontWeight.w500)),
      textAlign: align,
      textDirection: TextDirection.ltr,
    )..layout();
    var dx = offset.dx;
    if (align == TextAlign.right) dx -= tp.width;
    if (align == TextAlign.center) dx -= tp.width / 2;
    tp.paint(canvas, Offset(dx, offset.dy - tp.height / 2));
  }

  Set<int> _axisTickIndices(int count) {
    if (count <= 0) return {};
    if (count == 1) return {0};
    final indices = <int>{0, count - 1};
    const maxTicks = 5;
    final inner = maxTicks - 2;
    if (inner > 0) {
      final step = (count - 1) / (inner + 1);
      for (var k = 1; k <= inner; k++) {
        indices.add((k * step).round());
      }
    }
    return indices;
  }

  Set<int> _intradayTickIndices(int count) {
    if (count <= 0) return {};
    if (count <= 5) return {for (var i = 0; i < count; i++) i};
    final indices = <int>{0, count - 1};
    final step = (count / 5).ceil().clamp(1, count);
    for (var i = step; i < count - 1; i += step) {
      indices.add(i);
    }
    return indices;
  }

  String _formatAxisTime(String iso, String? prevIso) {
    try {
      final d = _parseChartTime(iso);
      if (interval == '1D') {
        final dd = d.day.toString().padLeft(2, '0');
        final mm = d.month.toString().padLeft(2, '0');
        // FireAnt-style: chỉ hiện năm ở tick biên / đổi năm
        var showYear = true;
        if (prevIso != null) {
          try {
            final prev = _parseChartTime(prevIso);
            showYear = prev.year != d.year;
          } catch (_) {}
        }
        return showYear ? '$dd/$mm/${d.year}' : '$dd/$mm';
      }
      if (interval == '1H') {
        return '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')} '
            '${d.hour.toString().padLeft(2, '0')}h';
      }
      return '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
    } catch (_) {
      return iso;
    }
  }

  DateTime _parseChartTime(String iso) {
    final normalized = iso.replaceFirstMapped(
      RegExp(r'\.(\d{3})\d+'),
      (m) => '.${m[1]}',
    );
    return DateTime.parse(normalized).toLocal();
  }

  @override
  bool shouldRepaint(covariant _CandlestickPainter old) =>
      old.bars != bars ||
      old.activeIndex != activeIndex ||
      old.interval != interval ||
      old.plotW != plotW ||
      old.lastClose != lastClose ||
      old.showCrosshair != showCrosshair;
}

class _PerformanceStrip extends StatelessWidget {
  const _PerformanceStrip({required this.bars, required this.colors});

  final List<ChartBar> bars;
  final ChartColors colors;

  @override
  Widget build(BuildContext context) {
    final items = <(String, int)>[
      ('1D', 1),
      ('1W', 5),
      ('1M', 22),
      ('3M', 66),
      ('6M', 132),
      ('1Y', 252),
    ];
    return Container(
      padding: const EdgeInsets.fromLTRB(8, 6, 8, 8),
      decoration: BoxDecoration(
        border: Border(top: BorderSide(color: colors.grid)),
      ),
      child: Row(
        children: items.map((item) {
          final pct = _changePercent(bars, item.$2);
          final color = pct == null
              ? colors.muted
              : pct >= 0
                  ? colors.green
                  : colors.red;
          return Expanded(
            child: Column(
              children: [
                Text(item.$1, style: TextStyle(fontSize: 9, color: colors.muted)),
                const SizedBox(height: 2),
                Text(
                  pct == null ? '—' : '${pct >= 0 ? '' : ''}${pct.toStringAsFixed(2)}%',
                  style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: color),
                ),
              ],
            ),
          );
        }).toList(),
      ),
    );
  }

  double? _changePercent(List<ChartBar> bars, int sessions) {
    if (bars.length <= sessions) return null;
    final prev = bars[bars.length - 1 - sessions].close;
    if (prev == 0) return null;
    return ((bars.last.close - prev) / prev) * 100;
  }
}

class _SessionStatsRow extends StatelessWidget {
  const _SessionStatsRow({
    required this.reference,
    required this.open,
    required this.high,
    required this.low,
    required this.colors,
  });

  final double? reference;
  final double open;
  final double high;
  final double low;
  final ChartColors colors;

  static const _refOrange = Color(0xFFFF9800);

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(10, 4, 10, 10),
      decoration: BoxDecoration(
        border: Border(top: BorderSide(color: colors.grid)),
      ),
      child: Row(
        children: [
          _cell('Tham chiếu', reference != null ? formatPrice(reference!) : '—', _refOrange),
          _cell('Mở cửa', formatPrice(open), _refOrange),
          _cell('Thấp nhất', formatPrice(low), colors.red),
          _cell('Cao nhất', formatPrice(high), colors.green),
        ],
      ),
    );
  }

  Widget _cell(String label, String value, Color color) {
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: TextStyle(fontSize: 9, color: colors.muted)),
          const SizedBox(height: 2),
          Text(value, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: color)),
        ],
      ),
    );
  }
}

String formatVolumeFull(double volume) {
  final n = volume.round();
  final s = n.abs().toString();
  final buf = StringBuffer();
  for (var i = 0; i < s.length; i++) {
    if (i > 0 && (s.length - i) % 3 == 0) buf.write(',');
    buf.write(s[i]);
  }
  final formatted = buf.toString();
  if (volume >= 1e9) return '${formatted} (${(volume / 1e9).toStringAsFixed(2)}B)';
  return formatted;
}

String formatVolume(double volume) {
  if (volume >= 1e9) return '${(volume / 1e9).toStringAsFixed(1)}B';
  if (volume >= 1e6) return '${(volume / 1e6).toStringAsFixed(1)}M';
  if (volume >= 1e3) return '${(volume / 1e3).toStringAsFixed(1)}K';
  return volume.toStringAsFixed(0);
}
