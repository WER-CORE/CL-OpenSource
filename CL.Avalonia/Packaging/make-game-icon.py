#!/usr/bin/env python3
"""Складає іконку для вікна гри: блок Minecraft із бейджем CL у кутку.

Основу беремо з уже завантажених ассетів гри (icons/icon_256x256.png),
логотип - з ресурсів лаунчера. У репозиторії лежить лише логотип CL,
іконка Mojang не копіюється.

    make-game-icon.py <icon_256x256.png> <cl-logo.png> <вихідний.png>
"""
import sys
from PIL import Image, ImageDraw

BG_THRESHOLD = 150          # усе світліше за тло логотипа вважаємо гліфом
LOGO_BG = (23, 23, 23, 255) # тло самого логотипа CL (#171717)


def extract_glyphs(logo_path):
    """Прибирає тло логотипа, лишаючи самі літери C та L у рідних кольорах."""
    logo = Image.open(logo_path).convert('RGB')
    w, h = logo.size
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    src, dst = logo.load(), out.load()

    for y in range(h):
        for x in range(w):
            r, g, b = src[x, y]
            if r + g + b > BG_THRESHOLD:
                dst[x, y] = (r, g, b, 255)

    return out.crop(out.getchannel('A').getbbox())


def build(base_path, logo_path, out_path):
    base = Image.open(base_path).convert('RGBA')
    size = base.size[0]

    badge = int(size * 0.40)
    pad = int(badge * 0.20)
    inner = badge - pad * 2

    glyphs = extract_glyphs(logo_path)
    scale = min(inner / glyphs.size[0], inner / glyphs.size[1])
    glyphs = glyphs.resize(
        (max(1, int(glyphs.size[0] * scale)), max(1, int(glyphs.size[1] * scale))),
        Image.LANCZOS)

    layer = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)

    # Правий нижній кут: не перекриває впізнавану зелену верхівку блоку.
    x = y = size - badge - int(size * 0.015)
    box = (x, y, x + badge - 1, y + badge - 1)

    draw.ellipse(box, fill=LOGO_BG)

    layer.alpha_composite(glyphs, (x + (badge - glyphs.size[0]) // 2,
                                   y + (badge - glyphs.size[1]) // 2))

    Image.alpha_composite(base, layer).save(out_path)


if __name__ == '__main__':
    if len(sys.argv) != 4:
        print(__doc__, file=sys.stderr)
        sys.exit(2)
    build(*sys.argv[1:])
