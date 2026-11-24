#!/usr/bin/env python3
"""Generate the FilterMachine item sprite.

The item's footprint comes from the image size: GameItem.SetSpriteAndShapeFromMod
divides it by UISettings.current.gridSize, which the game hardcodes to 16 px.
So a 3x3 item needs a 48x48 image, and every pixel must be opaque.
"""

import math
import zlib
from pathlib import Path

CELLS = 3
PIXELS_PER_CELL = 16
SIZE = CELLS * PIXELS_PER_CELL

OUT = Path(__file__).resolve().parent.parent / "Textures" / "Items" / "filter_machine.png"

TRANSPARENT = (0, 0, 0, 0)
BORDER = (26, 31, 38, 255)
BODY = (58, 68, 82, 255)
TOP = (78, 91, 107, 255)
WATER = (86, 196, 232, 255)
WATER_DARK = (52, 150, 190, 255)
FILTER = (168, 178, 193, 255)
FILTER_OLD = (120, 108, 96, 255)


def at(fraction):
    return int(round(fraction * SIZE))


def blank():
    return [[TRANSPARENT for _ in range(SIZE)] for _ in range(SIZE)]


def rect(px, x0, y0, x1, y1, color):
    for y in range(max(0, y0), min(SIZE, y1 + 1)):
        for x in range(max(0, x0), min(SIZE, x1 + 1)):
            px[y][x] = color


def disc(px, cx, cy, r, color):
    for y in range(SIZE):
        for x in range(SIZE):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                px[y][x] = color


def draw():
    px = blank()

    # Chassis and a one pixel frame.
    rect(px, 1, 1, SIZE - 2, SIZE - 2, BODY)
    rect(px, 0, 0, SIZE - 1, 0, BORDER)
    rect(px, 0, SIZE - 1, SIZE - 1, SIZE - 1, BORDER)
    rect(px, 0, 0, 0, SIZE - 1, BORDER)
    rect(px, SIZE - 1, 0, SIZE - 1, SIZE - 1, BORDER)

    # Vent strip along the top.
    vent_bottom = at(0.145)
    rect(px, 3, 3, SIZE - 4, vent_bottom, TOP)
    for x in range(4, SIZE - 4, 3):
        px[vent_bottom][x] = BORDER

    # Water side: a drop with a darker core.
    drop_x = at(0.30)
    drop_y = at(0.60)
    radius = max(2, at(0.17))
    tip_top = at(0.28)
    disc(px, drop_x, drop_y, radius, WATER)
    span = drop_y - tip_top
    for y in range(tip_top, drop_y + 1):
        half = int(math.sqrt(max(0.0, float(radius * radius) * (y - tip_top) / max(1, span))))
        rect(px, drop_x - half, y, drop_x + half, y, WATER)
    disc(px, drop_x, drop_y + 1, max(1, radius // 3), WATER_DARK)

    # Filter matrix: three by three cartridges, the bottom row already spent.
    cell = max(2, at(0.105))
    gap = max(1, at(0.042))
    origin_x = at(0.56)
    origin_y = at(0.40)
    for row in range(3):
        for col in range(3):
            x0 = origin_x + col * (cell + gap)
            y0 = origin_y + row * (cell + gap)
            rect(px, x0, y0, x0 + cell - 1, y0 + cell - 1, FILTER if row < 2 else FILTER_OLD)

    return px


def encode(px):
    raw = bytearray()
    for row in px:
        raw.append(0)  # PNG filter type 0: no prediction
        for r, g, b, a in row:
            raw.extend((r, g, b, a))

    def chunk(tag, data):
        body = tag + data
        return len(data).to_bytes(4, "big") + body + (zlib.crc32(body) & 0xFFFFFFFF).to_bytes(4, "big")

    header = SIZE.to_bytes(4, "big") + SIZE.to_bytes(4, "big") + bytes((8, 6, 0, 0, 0))
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", header)
        + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + chunk(b"IEND", b"")
    )


def main():
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_bytes(encode(draw()))
    print(f"wrote {OUT} ({SIZE}x{SIZE} = {CELLS}x{CELLS} cells, {OUT.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
