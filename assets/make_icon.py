"""Draws assets/app.ico (the program icon). Run: python3 assets/make_icon.py

An original design: a warm orange rounded square with a white usage gauge.
Each size is drawn separately (4x supersampled) so small sizes stay crisp.
"""
import math
from pathlib import Path
from PIL import Image, ImageDraw

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
TOP = (0xE3, 0x8B, 0x6A)     # warm orange, light
BOTTOM = (0xC9, 0x62, 0x42)  # warm orange, dark
SS = 4                       # supersampling


def draw(size: int) -> Image.Image:
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    # Vertical gradient clipped to a rounded square.
    grad = Image.new("RGBA", (s, s))
    gd = ImageDraw.Draw(grad)
    for y in range(s):
        t = y / (s - 1)
        gd.line([(0, y), (s, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)) + (255,))
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, s - 1, s - 1], radius=s * 0.22, fill=255)
    img.paste(grad, (0, 0), mask)

    # Gauge: faint full track + white arc (about 70%) with round ends.
    # Drawn on its own layer and composited, so the faint track blends with the orange.
    layer = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    small = size <= 24
    stroke = s * (0.15 if small else 0.11)
    inset = s * (0.20 if small else 0.22)
    box = [inset, inset, s - inset, s - inset]
    d.ellipse(box, outline=(255, 255, 255, 80), width=round(stroke))
    start, end = -90, -90 + 252
    d.arc(box, start, end, fill=(255, 255, 255, 255), width=round(stroke))

    # Round caps on the arc.
    cx = cy = s / 2
    r = (s - 2 * inset) / 2 - stroke / 2
    for ang in (start, end):
        a = math.radians(ang)
        x, y = cx + r * math.cos(a), cy + r * math.sin(a)
        d.ellipse([x - stroke / 2, y - stroke / 2, x + stroke / 2, y + stroke / 2], fill=(255, 255, 255, 255))

    # Center dot on larger sizes.
    if size >= 48:
        dot = s * 0.07
        d.ellipse([cx - dot, cy - dot, cx + dot, cy + dot], fill=(255, 255, 255, 255))

    img = Image.alpha_composite(img, layer)
    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    here = Path(__file__).parent
    images = [draw(n) for n in SIZES]
    images[-1].save(here / "app.ico", sizes=[(n, n) for n in SIZES], append_images=images[:-1])
    print("wrote", here / "app.ico")


if __name__ == "__main__":
    main()
