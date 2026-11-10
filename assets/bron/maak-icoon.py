#!/usr/bin/env python3
"""Maakt het app-icoon van BTW-Boeker uit het aangeleverde ontwerp (RGB, witte hoeken).

Gebruik: python3 assets/bron/maak-icoon.py <ontwerp.png>
Schrijft assets/bron/btw-boeker-icoon-1024.png (transparante hoeken) en
src/BtwBoeker.App/Assets/BtwBoeker.ico (16-256 px; 16 en 24 px zonder tekst "BTW").
"""
import io
import struct
import sys
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

wortel = Path(__file__).resolve().parents[2]
bron = Image.open(sys.argv[1]).convert("RGB")
breedte, hoogte = bron.size


def zet_hoeken_transparant(afb: Image.Image) -> Image.Image:
    """Vult vanuit de vier hoeken het (bijna) witte gebied; de rest is de tegel."""
    px = afb.load()
    buiten = Image.new("L", afb.size, 0)
    bp = buiten.load()
    wachtrij = deque([(0, 0), (afb.width - 1, 0), (0, afb.height - 1), (afb.width - 1, afb.height - 1)])
    while wachtrij:
        x, y = wachtrij.popleft()
        if not (0 <= x < afb.width and 0 <= y < afb.height) or bp[x, y]:
            continue
        r, g, b = px[x, y]
        if min(r, g, b) < 225:
            continue
        bp[x, y] = 255
        wachtrij.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    tegel = buiten.point(lambda v: 255 - v)
    tegel = tegel.filter(ImageFilter.MinFilter(5)).filter(ImageFilter.GaussianBlur(1.2))  # geen witte rand
    rgba = afb.convert("RGBA")
    rgba.putalpha(tegel)
    return rgba


def zonder_tekst(afb: Image.Image) -> Image.Image:
    """Haalt de letters BTW weg en zet er een derde streepje voor in de plaats (voor 16 en 24 px)."""
    kopie = afb.copy()
    px = kopie.load()
    s = breedte / 1254
    x0, x1, y0, y1 = int(340 * s), int(930 * s), int(560 * s), int(790 * s)
    for y in range(y0, y1):
        kleur = px[int(310 * s), y]
        for x in range(x0, x1):
            px[x, y] = kleur
    tek = ImageDraw.Draw(kopie)
    balkkleur = px[int(400 * s), int(838 * s)]
    tek.rounded_rectangle((int(375 * s), int(640 * s), int(880 * s), int(690 * s)), radius=int(24 * s), fill=balkkleur)
    return kopie


volledig = zet_hoeken_transparant(bron).resize((1024, 1024), Image.LANCZOS)
volledig.save(wortel / "assets" / "bron" / "btw-boeker-icoon-1024.png")
klein = zet_hoeken_transparant(zonder_tekst(bron)).resize((1024, 1024), Image.LANCZOS)

maten = [16, 24, 32, 48, 64, 128, 256]
png = []
for m in maten:
    afb = (klein if m <= 24 else volledig).resize((m, m), Image.LANCZOS)
    buf = io.BytesIO()
    afb.save(buf, "PNG")
    png.append(buf.getvalue())

kop = struct.pack("<HHH", 0, 1, len(maten))
offset = 6 + 16 * len(maten)
tabel = b""
for m, data in zip(maten, png):
    tabel += struct.pack("<BBBBHHII", 0 if m == 256 else m, 0 if m == 256 else m, 0, 0, 1, 32, len(data), offset)
    offset += len(data)
(wortel / "src" / "BtwBoeker.App" / "Assets" / "BtwBoeker.ico").write_bytes(kop + tabel + b"".join(png))
