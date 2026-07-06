"""Tạo icon launcher (chỉ Android) — cân bằng kích thước và độ nét."""
from __future__ import annotations

import re
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets"
SRC = ASSETS / "juice-launcher-mark.png"
OUT = ASSETS / "juice-launcher-icon.png"
LAUNCHER_XML = (
    ROOT / "android" / "app" / "src" / "main" / "res" / "mipmap-anydpi-v26" / "ic_launcher.xml"
)

CANVAS = 1024
BRIGHT_THRESHOLD = 28
# Mark ~84% khung — không phóng to quá ảnh gốc (tránh mờ)
FILL_RATIO = 0.84
ADAPTIVE_INSET = "8%"


def content_bbox(img: Image.Image) -> tuple[int, int, int, int]:
    rgba = img.convert("RGBA")
    pixels = rgba.load()
    w, h = rgba.size
    min_x, min_y = w, h
    max_x, max_y = 0, 0
    found = False
    for y in range(h):
        for x in range(w):
            r, g, b, _ = pixels[x, y]
            if r > BRIGHT_THRESHOLD or g > BRIGHT_THRESHOLD or b > BRIGHT_THRESHOLD:
                found = True
                min_x = min(min_x, x)
                max_x = max(max_x, x)
                min_y = min(min_y, y)
                max_y = max(max_y, y)
    if not found:
        return 0, 0, w, h
    return min_x, min_y, max_x + 1, max_y + 1


def build_launcher_icon() -> Image.Image:
    src = Image.open(SRC).convert("RGB")
    cropped = src.crop(content_bbox(src))

    max_side = int(CANVAS * FILL_RATIO)
    scale = min(max_side / cropped.width, max_side / cropped.height, 1.0)

    if scale < 1.0:
        target = (max(1, int(cropped.width * scale)), max(1, int(cropped.height * scale)))
        mark = cropped.resize(target, Image.Resampling.LANCZOS)
    else:
        mark = cropped

    canvas = Image.new("RGB", (CANVAS, CANVAS), (0, 0, 0))
    ox = (CANVAS - mark.width) // 2
    oy = (CANVAS - mark.height) // 2
    canvas.paste(mark, (ox, oy))
    return canvas


def patch_adaptive_inset() -> None:
    if not LAUNCHER_XML.exists():
        return

    inset_block = f"""  <foreground>
      <inset
          android:drawable="@drawable/ic_launcher_foreground"
          android:inset="{ADAPTIVE_INSET}" />
  </foreground>"""

    text = LAUNCHER_XML.read_text(encoding="utf-8")
    if "<inset" in text:
        text = re.sub(r'android:inset="[^"]*"', f'android:inset="{ADAPTIVE_INSET}"', text)
    else:
        text = text.replace(
            '  <foreground android:drawable="@drawable/ic_launcher_foreground"/>',
            inset_block,
        )
    LAUNCHER_XML.write_text(text, encoding="utf-8")


def main() -> None:
    if not SRC.exists():
        raise SystemExit(f"Thiếu {SRC.name}")
    icon = build_launcher_icon()
    icon.save(OUT)
    print(f"Wrote {OUT.name} (fill={FILL_RATIO:.0%}, inset={ADAPTIVE_INSET})")


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--patch-xml":
        patch_adaptive_inset()
        print(f"Patched adaptive icon inset -> {ADAPTIVE_INSET}")
    else:
        main()
