import 'dart:io';

import 'package:image/image.dart' as img;

/// Tạo bản icon launcher có chừa lề để logo hiển thị nhỏ hơn trên home điện thoại.
/// Thu logo về [scale] của khung, phần còn lại đổ nền đen (khớp adaptive background).
///
/// Chạy: `dart run tool/pad_launcher_icon.dart` (từ thư mục mobile),
/// sau đó `dart run flutter_launcher_icons` để sinh lại icon Android/iOS.
void main(List<String> args) {
  const input = 'assets/juice-launcher-icon.png';
  const output = 'assets/juice-launcher-icon-padded.png';
  // logo chiếm 82% khung → 18% còn lại là lề (chỉnh số này nếu muốn nhỏ/to hơn).
  final scale = args.isNotEmpty ? double.tryParse(args.first) ?? 0.82 : 0.82;

  final src = img.decodePng(File(input).readAsBytesSync());
  if (src == null) {
    stderr.writeln('Không đọc được $input');
    exit(1);
  }

  final size = src.width >= src.height ? src.width : src.height;
  final canvas = img.Image(width: size, height: size, numChannels: 4);
  img.fill(canvas, color: img.ColorRgba8(0, 0, 0, 255));

  final target = (size * scale).round();
  final resized = img.copyResize(
    src,
    width: target,
    height: target,
    interpolation: img.Interpolation.cubic,
  );
  final dx = ((size - resized.width) / 2).round();
  final dy = ((size - resized.height) / 2).round();
  img.compositeImage(canvas, resized, dstX: dx, dstY: dy);

  File(output).writeAsBytesSync(img.encodePng(canvas));
  stdout.writeln('Đã tạo $output (${size}x$size, logo ${(scale * 100).round()}%).');
}
