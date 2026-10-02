# Декорации Walk of Industry (low-poly, тот же стиль, что wi_models.py).
# Запуск: python wi_decor.py "<проект>"   — модели в Resources/Models/Decor, иконки в Resources/Decor/Icons,
# манифест частей/огней в Resources/Decor/decor_manifest.json, новые материалы палитры в Resources/Models/Materials.
# Координаты Unity: x вправо, y вверх, z вперёд. Метры, клетка = 1 м, pivot = центр footprint, y=0 земля.
import io, json, math, os, re, sys, uuid
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from wi_lib import Model, MATS, write_mtl, render
import wi_palette

# Новые цвета палитры (у emissive второй элемент 1 — светятся и в иконке не затеняются).
NEW_MATS = {
    'wi_leaf':     ((0.27, 0.54, 0.21), 0, 0.00),
    'wi_leaf2':    ((0.16, 0.40, 0.17), 0, 0.00),
    'wi_wood':     ((0.52, 0.34, 0.18), 0, 0.00),
    'wi_gold':     ((0.98, 0.76, 0.22), 0, 0.55),
    'wi_snow':     ((0.94, 0.96, 0.98), 0, 0.10),
    'wi_orange':   ((0.96, 0.46, 0.10), 0, 0.00),
    'wi_flower':   ((0.92, 0.38, 0.58), 0, 0.00),
    'wi_redpaint': ((0.74, 0.17, 0.13), 0, 0.10),
    'wi_asphalt':  ((0.17, 0.17, 0.18), 0, 0.00),
    'wi_paving':   ((0.64, 0.61, 0.56), 0, 0.00),
    'wi_grass':    ((0.33, 0.57, 0.23), 0, 0.00),
    'wi_paint':    ((0.16, 0.42, 0.50), 0, 0.15),   # перекрашиваемая часть (E по декорации)
    'wi_warm':     ((1.00, 0.84, 0.52), 1, 0.00),   # тёплые лампы
    'wi_neon':     ((1.00, 0.30, 0.72), 1, 0.00),
    'wi_holo':     ((0.30, 0.90, 1.00), 1, 0.00),
}
MATS.update(NEW_MATS)

DECOR = {}     # id -> функция, возвращает Model
PARTS = {}     # id -> [(объект, pivot)]
LIGHTS = {}    # id -> [(pos, (r,g,b), range)]
EXTRA = {}     # id -> dict (text anchor и т.п.)
ICON_YAW = {}  # id -> ракурс иконки


def reg(name, parts=(), lights=(), yaw=145, **extra):
    def deco(fn):
        DECOR[name] = fn
        PARTS[name] = list(parts)
        LIGHTS[name] = list(lights)
        EXTRA[name] = extra
        ICON_YAW[name] = yaw
        return fn
    return deco

# ---------------- помощники ----------------

def ball(m, obj, c, r, mat, sides=8, rings=4, sy=1.0, sx=1.0):
    """Низкополигональный шар (кроны, камни, снег)."""
    cx, cy, cz = c
    pts = []
    for i in range(rings + 1):
        phi = math.pi * i / rings
        y = math.cos(phi) * r * sy
        rr = math.sin(phi) * r
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides + (math.pi / sides if i % 2 else 0)
            ring.append((cx + rr * math.cos(a) * sx, cy + y, cz + rr * math.sin(a)))
        pts.append(ring)
    for i in range(rings):
        for k in range(sides):
            k2 = (k + 1) % sides
            a, b = pts[i][k], pts[i][k2]
            c2, d = pts[i + 1][k2], pts[i + 1][k]
            if i == 0:
                m.face(obj, [a, c2, d], mat, c)
            elif i == rings - 1:
                m.face(obj, [a, b, d], mat, c)
            else:
                m.face(obj, [a, b, c2, d], mat, c)


def post(m, obj, x, z, h, r, mat, y0=0.0, sides=6):
    m.cyl(obj, (x, y0, z), r, h, mat, sides)


def stripes_v(m, obj, c, h, w, d, n, a='wi_yellow', b='wi_dark'):
    step = h / n
    for i in range(n):
        m.box(obj, (c[0], c[1] + step * (i + 0.5), c[2]), (w, step, d), a if i % 2 == 0 else b)


def stripes_x(m, obj, c, L, h, d, n, a='wi_redpaint', b='wi_white'):
    step = L / n
    for i in range(n):
        m.box(obj, (c[0] - L / 2 + step * (i + 0.5), c[1], c[2]), (step, h, d), a if i % 2 == 0 else b)


def lattice(m, obj, legs, y0, y1, mat, w=0.05, braces=5, top_scale=1.0):
    """Решётчатая мачта: legs — список (x,z) у основания; сверху сжаты к оси в top_scale."""
    tops = [(x * top_scale, z * top_scale) for x, z in legs]
    for (x0, z0), (x1, z1) in zip(legs, tops):
        m.beam(obj, (x0, y0, z0), (x1, y1, z1), w, w, mat)
    n = len(legs)
    for k in range(braces + 1):
        t = k / braces
        y = y0 + (y1 - y0) * t
        ring = [(x0 + (x1 - x0) * t, z0 + (z1 - z0) * t) for (x0, z0), (x1, z1) in zip(legs, tops)]
        for i in range(n):
            a, b = ring[i], ring[(i + 1) % n]
            m.beam(obj, (a[0], y, a[1]), (b[0], y, b[1]), w * 0.6, w * 0.6, mat)
        if k < braces:
            t2 = (k + 1) / braces
            y2 = y0 + (y1 - y0) * t2
            ring2 = [(x0 + (x1 - x0) * t2, z0 + (z1 - z0) * t2) for (x0, z0), (x1, z1) in zip(legs, tops)]
            for i in range(n):
                a = ring[i]
                b = ring2[(i + 1) % n]
                m.beam(obj, (a[0], y, a[1]), (b[0], y2, b[1]), w * 0.45, w * 0.45, mat)


def bulb(m, obj, c, s, mat):
    m.box(obj, c, (s, s, s), mat)

# ================= ОСВЕЩЕНИЕ =================

@reg('decor_street_lamp', lights=[((0.36, 2.22, 0), (1.0, 0.86, 0.6), 7.0)])
def street_lamp():
    m = Model()
    m.cyl('Body', (0, 0, 0), 0.16, 0.12, 'wi_dark', 8)
    m.cyl('Body', (0, 0.12, 0), 0.09, 0.22, 'wi_paint', 8)
    m.cyl('Body', (0, 0.34, 0), 0.05, 2.0, 'wi_dark', 8)
    m.beam('Body', (0, 2.25, 0), (0.42, 2.38, 0), 0.05, 0.05, 'wi_dark')
    m.beam('Body', (0, 2.0, 0), (0.22, 2.32, 0), 0.03, 0.03, 'wi_dark')
    m.box('Body', (0.40, 2.34, 0), (0.30, 0.08, 0.20), 'wi_paint')
    m.box('Body', (0.40, 2.40, 0), (0.18, 0.05, 0.12), 'wi_dark')
    m.box('Lamp', (0.40, 2.285, 0), (0.24, 0.03, 0.15), 'wi_warm')
    m.cyl('Body', (0, 2.33, 0), 0.06, 0.06, 'wi_gold', 8)
    return m


@reg('decor_floodlight', lights=[((0, 3.05, 0.35), (1.0, 0.95, 0.85), 11.0)])
def floodlight():
    m = Model()
    m.box('Body', (0, 0.05, 0), (0.7, 0.1, 0.7), 'wi_stone')
    lattice(m, 'Body', [(-0.26, -0.26), (0.26, -0.26), (0.26, 0.26), (-0.26, 0.26)], 0.1, 2.9, 'wi_steel', 0.05, 6, 0.45)
    m.box('Body', (0, 2.92, 0), (0.6, 0.06, 0.6), 'wi_dark')
    for sx in (-1, 1):
        m.box('Body', (sx * 0.17, 3.1, 0.12), (0.28, 0.22, 0.16), 'wi_dark', rx=-25)
        m.box('Lamp', (sx * 0.17, 3.07, 0.205), (0.24, 0.18, 0.02), 'wi_warm', rx=-25)
    m.box('Body', (0, 3.25, -0.1), (0.04, 0.3, 0.04), 'wi_steel')
    m.box('Body', (0.25, 0.5, 0.27), (0.18, 0.24, 0.1), 'wi_yellow')
    return m


@reg('decor_beacon', parts=[('Head', (0, 0.72, 0))], lights=[((0, 0.82, 0), (1.0, 0.55, 0.15), 4.5)], blink=0.6)
def beacon():
    m = Model()
    m.box('Body', (0, 0.06, 0), (0.42, 0.12, 0.42), 'wi_dark')
    stripes_v(m, 'Body', (0, 0.12, 0), 0.52, 0.16, 0.16, 4)
    m.cyl('Body', (0, 0.64, 0), 0.16, 0.08, 'wi_dark', 10)
    m.cyl('Head', (0, 0.72, 0), 0.13, 0.2, 'wi_fire', 10, r2=0.1)
    m.box('Head', (0, 0.82, 0.04), (0.04, 0.16, 0.12), 'wi_steel')
    m.cyl('Body', (0, 0.92, 0), 0.15, 0.03, 'wi_dark', 10)
    return m


@reg('decor_fire_barrel', parts=[('Flame', (0, 0.74, 0))], lights=[((0, 1.0, 0), (1.0, 0.55, 0.2), 5.0)], fire=(0, 0.85, 0))
def fire_barrel():
    m = Model()
    m.cyl('Body', (0, 0, 0), 0.26, 0.72, 'wi_dark', 10)
    for y in (0.12, 0.38, 0.64):
        m.cyl('Body', (0, y, 0), 0.272, 0.05, 'wi_copper', 10)
    for k in range(6):
        a = 2 * math.pi * k / 6
        m.box('Body', (0.262 * math.cos(a), 0.25, 0.262 * math.sin(a)), (0.03, 0.08, 0.06), 'wi_fire', yaw=-math.degrees(a))
    m.cyl('Body', (0, 0.66, 0), 0.23, 0.04, 'wi_base', 10)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        m.beam('Body', (0.15 * math.cos(a), 0.7, 0.15 * math.sin(a)), (-0.12 * math.cos(a), 0.78, -0.12 * math.sin(a)), 0.05, 0.05, 'wi_wood')
    m.cyl('Flame', (0, 0.72, 0), 0.17, 0.42, 'wi_fire', 6, r2=0.0)
    m.cyl('Flame', (0.07, 0.72, 0.04), 0.09, 0.3, 'wi_yellow', 6, r2=0.0)
    m.cyl('Flame', (-0.06, 0.72, -0.05), 0.08, 0.26, 'wi_warm', 6, r2=0.0)
    return m


@reg('decor_garland', parts=[('BulbsA', (0, 0, 0)), ('BulbsB', (0, 0, 0))],
     lights=[((0, 1.55, 0), (1.0, 0.8, 0.5), 6.0)], blink=0.5)
def garland():
    m = Model()
    for sx in (-1, 1):
        m.box('Body', (sx * 0.85, 0.05, 0), (0.24, 0.1, 0.24), 'wi_stone')
        m.cyl('Body', (sx * 0.85, 0.1, 0), 0.04, 1.75, 'wi_wood', 6)
        m.cyl('Body', (sx * 0.85, 1.85, 0), 0.06, 0.06, 'wi_paint', 6)
    n = 9
    prev = None
    cols = ['wi_red', 'wi_lamp', 'wi_blue', 'wi_warm']
    for i in range(n + 1):
        t = i / n
        x = -0.85 + 1.7 * t
        y = 1.8 - 0.32 * math.sin(math.pi * t)
        p = (x, y, 0)
        if prev:
            m.beam('Body', prev, p, 0.015, 0.015, 'wi_dark')
        if 0 < i < n:
            part = 'BulbsA' if i % 2 else 'BulbsB'
            m.box(part, (x, y - 0.06, 0), (0.06, 0.09, 0.06), cols[i % 4])
        prev = p
    return m


@reg('decor_neon_sign', lights=[((0, 1.55, 0.35), (1.0, 0.35, 0.75), 6.0)],
     text=dict(pos=(0, 1.55, 0.07), size=0.055, color=(1.0, 0.55, 0.85), kind='world'))
def neon_sign():
    m = Model()
    for sx in (-1, 1):
        m.box('Body', (sx * 0.7, 0.05, 0), (0.2, 0.1, 0.2), 'wi_stone')
        m.box('Body', (sx * 0.7, 0.6, 0), (0.07, 1.1, 0.07), 'wi_dark')
    m.box('Body', (0, 1.55, 0), (1.86, 0.78, 0.1), 'wi_dark')
    m.box('Body', (0, 1.55, -0.06), (1.9, 0.82, 0.03), 'wi_paint')
    for y in (1.18, 1.92):
        m.box('Neon', (0, y, 0.06), (1.76, 0.035, 0.03), 'wi_neon')
    for x in (-0.88, 0.88):
        m.box('Neon', (x, 1.55, 0.06), (0.035, 0.74, 0.03), 'wi_neon')
    m.box('Neon', (-0.6, 1.08, 0.06), (0.4, 0.03, 0.03), 'wi_holo')
    m.box('Neon', (0.6, 1.08, 0.06), (0.4, 0.03, 0.03), 'wi_holo')
    return m

# ================= ПРИРОДА =================

@reg('decor_bush')
def bush():
    m = Model()
    ball(m, 'Body', (0, 0.28, 0), 0.34, 'wi_leaf', 8, 4, 0.8)
    ball(m, 'Body', (0.18, 0.22, 0.12), 0.24, 'wi_leaf2', 7, 4, 0.85)
    ball(m, 'Body', (-0.16, 0.2, -0.14), 0.22, 'wi_leaf', 7, 4, 0.85)
    ball(m, 'Body', (0.05, 0.46, -0.05), 0.2, 'wi_leaf2', 7, 3)
    for (x, z) in ((0.12, 0.28), (-0.25, 0.08), (0.28, -0.1)):
        m.box('Body', (x, 0.36, z), (0.05, 0.05, 0.05), 'wi_flower')
    return m


@reg('decor_potted_tree')
def potted_tree():
    m = Model()
    m.cyl('Body', (0, 0, 0), 0.24, 0.42, 'wi_paint', 8, r2=0.3)
    m.cyl('Body', (0, 0.4, 0), 0.31, 0.05, 'wi_dark', 8)
    m.cyl('Body', (0, 0.42, 0), 0.27, 0.02, 'wi_base', 8)
    m.tube('Body', (0, 0.42, 0), (0.03, 1.2, 0.0), 0.05, 'wi_wood', 6)
    ball(m, 'Body', (0.03, 1.35, 0), 0.32, 'wi_leaf', 8, 4)
    ball(m, 'Body', (0.2, 1.15, 0.1), 0.2, 'wi_leaf2', 7, 3)
    ball(m, 'Body', (-0.16, 1.2, -0.08), 0.2, 'wi_leaf2', 7, 3)
    return m


@reg('decor_flowerbed')
def flowerbed():
    m = Model()
    m.box('Body', (0, 0.1, 0.42), (1.9, 0.2, 0.1), 'wi_stone')
    m.box('Body', (0, 0.1, -0.42), (1.9, 0.2, 0.1), 'wi_stone')
    m.box('Body', (0.9, 0.1, 0), (0.1, 0.2, 0.74), 'wi_stone')
    m.box('Body', (-0.9, 0.1, 0), (0.1, 0.2, 0.74), 'wi_stone')
    m.box('Body', (0, 0.08, 0), (1.72, 0.16, 0.76), 'wi_base')
    cols = ['wi_flower', 'wi_yellow', 'wi_white', 'wi_redpaint']
    rng = np.random.default_rng(3)
    k = 0
    for ix in range(7):
        for iz in range(3):
            x = -0.72 + ix * 0.24 + rng.uniform(-0.04, 0.04)
            z = -0.24 + iz * 0.24 + rng.uniform(-0.04, 0.04)
            h = 0.22 + rng.uniform(0, 0.12)
            m.box('Body', (x, 0.16 + h / 2, z), (0.025, h, 0.025), 'wi_leaf2')
            m.box('Body', (x + 0.05, 0.2, z), (0.08, 0.03, 0.04), 'wi_leaf', yaw=30)
            m.box('Body', (x, 0.17 + h, z), (0.09, 0.05, 0.09), cols[k % 4], yaw=45)
            k += 1
    return m


@reg('decor_big_tree', parts=[('Crown', (0, 1.5, 0))], yaw=150)
def big_tree():
    m = Model()
    m.cyl('Body', (0, 0, 0), 0.62, 0.05, 'wi_grass', 10)
    m.cyl('Body', (0, 0, 0), 0.2, 1.6, 'wi_wood', 7, r2=0.13)
    for a, l in ((20, 0.5), (140, 0.45), (260, 0.5)):
        t = math.radians(a)
        m.tube('Body', (0, 0.0, 0), (0.42 * math.cos(t), 0.05, 0.42 * math.sin(t)), 0.08, 'wi_wood', 5, r2=0.03)
        m.tube('Body', (0, 1.25, 0), (l * math.cos(t), 1.75, l * math.sin(t)), 0.07, 'wi_wood', 5, r2=0.04)
    ball(m, 'Crown', (0, 2.25, 0), 0.85, 'wi_leaf', 9, 5, 0.85)
    ball(m, 'Crown', (0.5, 1.95, 0.2), 0.5, 'wi_leaf2', 8, 4)
    ball(m, 'Crown', (-0.45, 2.0, -0.25), 0.55, 'wi_leaf2', 8, 4)
    ball(m, 'Crown', (0.1, 2.0, -0.55), 0.45, 'wi_leaf', 8, 4)
    ball(m, 'Crown', (-0.2, 2.75, 0.2), 0.45, 'wi_leaf', 8, 4)
    return m


@reg('decor_rock_garden')
def rock_garden():
    m = Model()
    m.box('Body', (0, 0.025, 0), (1.9, 0.05, 1.9), 'wi_paving')
    for i in range(9):
        z = -0.8 + i * 0.2
        m.box('Body', (0.15, 0.055, z), (1.4, 0.012, 0.03), 'wi_white')
    ball(m, 'Body', (-0.45, 0.18, 0.35), 0.36, 'wi_stone', 7, 4, 0.6, 1.2)
    ball(m, 'Body', (0.4, 0.22, -0.35), 0.42, 'wi_stone', 7, 4, 0.75)
    ball(m, 'Body', (0.6, 0.1, 0.5), 0.2, 'wi_dark', 6, 3, 0.6)
    ball(m, 'Body', (-0.55, 0.12, -0.5), 0.22, 'wi_stone', 6, 3, 0.6)
    ball(m, 'Body', (0.05, 0.16, 0.65), 0.25, 'wi_leaf', 7, 4, 0.7)
    ball(m, 'Body', (-0.7, 0.14, -0.05), 0.2, 'wi_leaf2', 7, 4, 0.7)
    m.cyl('Body', (0.75, 0.05, -0.75), 0.08, 0.25, 'wi_stone', 6)
    m.cyl('Body', (0.75, 0.3, -0.75), 0.13, 0.05, 'wi_stone', 6, r2=0.04)
    return m


@reg('decor_pond', parts=[('Reeds', (0.55, 0.05, -0.5))])
def pond():
    m = Model()
    m.cyl('Body', (0, 0, 0), 0.92, 0.04, 'wi_grass', 12)
    for k in range(12):
        a = 2 * math.pi * k / 12
        m.box('Body', (0.82 * math.cos(a), 0.09, 0.82 * math.sin(a)), (0.28, 0.14 + 0.04 * (k % 3), 0.2), 'wi_stone', yaw=-math.degrees(a) + 90)
    m.cyl('Body', (0, 0.04, 0), 0.74, 0.04, 'wi_water', 12)
    for (x, z, r) in ((-0.25, 0.2, 0.12), (0.2, 0.3, 0.09), (-0.05, -0.3, 0.1)):
        m.cyl('Body', (x, 0.085, z), r, 0.01, 'wi_leaf', 8)
    m.box('Body', (-0.22, 0.1, 0.22), (0.05, 0.03, 0.05), 'wi_flower')
    rng = np.random.default_rng(5)
    for i in range(7):
        x = 0.55 + rng.uniform(-0.15, 0.15)
        z = -0.5 + rng.uniform(-0.15, 0.15)
        h = 0.55 + rng.uniform(0, 0.3)
        m.box('Reeds', (x, 0.05 + h / 2, z), (0.025, h, 0.025), 'wi_leaf2', rz=rng.uniform(-6, 6))
        m.box('Reeds', (x, 0.05 + h * 0.85, z), (0.05, 0.14, 0.05), 'wi_wood')
    return m


@reg('decor_fountain', parts=[('Jet', (0, 0.95, 0))], splash=(0, 1.6, 0), yaw=150)
def fountain():
    m = Model()
    m.cyl('Body', (0, 0, 0), 1.45, 0.06, 'wi_paving', 8)
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        L = 2 * 1.25 * math.sin(math.pi / 8)
        m.box('Body', (1.2 * math.cos(a), 0.25, 1.2 * math.sin(a)), (0.2, 0.4, L + 0.08), 'wi_stone', yaw=-math.degrees(a))
        m.box('Body', (1.2 * math.cos(a), 0.47, 1.2 * math.sin(a)), (0.26, 0.05, L + 0.12), 'wi_white', yaw=-math.degrees(a))
    m.cyl('Body', (0, 0.06, 0), 1.12, 0.26, 'wi_water', 8)
    m.cyl('Body', (0, 0.06, 0), 0.18, 0.8, 'wi_stone', 8)
    m.cyl('Body', (0, 0.86, 0), 0.55, 0.12, 'wi_white', 10, r2=0.62)
    m.cyl('Body', (0, 0.9, 0), 0.5, 0.06, 'wi_water', 10)
    m.cyl('Body', (0, 0.96, 0), 0.08, 0.3, 'wi_stone', 8)
    m.cyl('Body', (0, 1.26, 0), 0.24, 0.06, 'wi_white', 8, r2=0.28)
    m.cyl('Jet', (0, 0.95, 0), 0.06, 0.75, 'wi_water', 6, r2=0.02)
    for k in range(6):
        a = 2 * math.pi * k / 6
        m.beam('Jet', (0, 1.55, 0), (0.38 * math.cos(a), 1.0, 0.38 * math.sin(a)), 0.035, 0.035, 'wi_water')
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.3
        m.box('Body', (1.25 * math.cos(a), 0.62, 1.25 * math.sin(a)), (0.12, 0.25, 0.12), 'wi_stone', yaw=-math.degrees(a))
        m.box('Body', (1.25 * math.cos(a), 0.78, 1.25 * math.sin(a)), (0.1, 0.07, 0.1), 'wi_gold', yaw=-math.degrees(a))
    return m

# ================= ПРОМЗОНА =================

def barrel(m, obj, c, mat, ring='wi_steel', h=0.48, r=0.17):
    x, y, z = c
    m.cyl(obj, (x, y, z), r, h, mat, 10)
    for yy in (0.08, h - 0.1):
        m.cyl(obj, (x, y + yy, z), r + 0.012, 0.03, ring, 10)
    m.cyl(obj, (x, y + h, z), r - 0.02, 0.01, 'wi_dark', 10)


@reg('decor_barrels')
def barrels():
    m = Model()
    m.box('Body', (0, 0.04, 0), (0.86, 0.08, 0.86), 'wi_wood')
    barrel(m, 'Body', (-0.2, 0.08, -0.18), 'wi_paint')
    barrel(m, 'Body', (0.2, 0.08, -0.18), 'wi_dark', 'wi_yellow')
    barrel(m, 'Body', (0.0, 0.08, 0.2), 'wi_redpaint')
    barrel(m, 'Body', (0.0, 0.56, -0.18), 'wi_yellow', 'wi_dark')
    m.box('Body', (0.0, 0.42, 0.372), (0.12, 0.12, 0.01), 'wi_white')
    return m


@reg('decor_pallets')
def pallets():
    m = Model()
    y = 0.0
    for i in range(4):
        yaw = 8 * (i % 2) - 4
        for x in (-0.33, 0, 0.33):
            m.box('Body', (x, y + 0.04, 0), (0.1, 0.08, 0.84), 'wi_crate', yaw=yaw)
        for z in (-0.36, -0.18, 0, 0.18, 0.36):
            m.box('Body', (0, y + 0.1, z), (0.86, 0.025, 0.12), 'wi_wood', yaw=yaw)
        y += 0.115
    m.box('Body', (0.1, y + 0.17, 0.05), (0.5, 0.34, 0.45), 'wi_crate')
    m.box('Body', (0.1, y + 0.17, 0.05), (0.52, 0.06, 0.47), 'wi_wood')
    m.box('Body', (0.1, y + 0.2, 0.28), (0.14, 0.1, 0.01), 'wi_dark')
    return m


@reg('decor_cable_reel', yaw=120)
def cable_reel():
    m = Model()
    for x in (-0.3, 0.3):
        m.tube('Body', (x - 0.03, 0.42, 0), (x + 0.03, 0.42, 0), 0.4, 'wi_wood', 12)
        m.tube('Body', (x - 0.035, 0.42, 0), (x + 0.035, 0.42, 0), 0.1, 'wi_dark', 8)
    m.tube('Body', (-0.27, 0.42, 0), (0.27, 0.42, 0), 0.33, 'wi_copper', 12)
    for x in (-0.18, 0.0, 0.18):
        m.tube('Body', (x - 0.03, 0.42, 0), (x + 0.03, 0.42, 0), 0.335, 'wi_rubber', 12)
    m.beam('Body', (0.2, 0.75, 0.0), (0.38, 0.02, 0.38), 0.05, 0.05, 'wi_copper')
    m.box('Body', (0.45, 0.02, 0.42), (0.22, 0.04, 0.08), 'wi_copper')
    for x in (-0.4, 0.4):
        m.box('Body', (x, 0.02, 0.24), (0.08, 0.04, 0.12), 'wi_wood')
        m.box('Body', (x, 0.02, -0.24), (0.08, 0.04, 0.12), 'wi_wood')
    return m


@reg('decor_toolbox')
def toolbox():
    m = Model()
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl('Body', (sx * 0.28, 0, sz * 0.18), 0.05, 0.08, 'wi_dark', 6)
    m.box('Body', (0, 0.42, 0), (0.7, 0.68, 0.46), 'wi_redpaint')
    for i in range(4):
        y = 0.18 + i * 0.15
        m.box('Body', (0, y, 0.232), (0.62, 0.012, 0.01), 'wi_dark')
        m.box('Body', (0, y + 0.07, 0.24), (0.2, 0.025, 0.025), 'wi_steel')
    m.box('Body', (0, 0.79, -0.02), (0.72, 0.06, 0.48), 'wi_dark')
    m.box('Body', (0, 0.86, -0.05), (0.5, 0.08, 0.3), 'wi_redpaint')
    m.box('Body', (0, 0.92, -0.05), (0.2, 0.03, 0.03), 'wi_steel')
    m.beam('Body', (0.18, 0.83, 0.12), (0.34, 0.83, 0.02), 0.03, 0.015, 'wi_steel')
    m.box('Body', (0.36, 0.83, 0.01), (0.06, 0.02, 0.06), 'wi_steel')
    m.box('Body', (-0.38, 0.6, 0), (0.04, 0.04, 0.3), 'wi_steel')
    return m


@reg('decor_pipe_pile')
def pipe_pile():
    m = Model()
    for x in (-0.6, 0.0, 0.6):
        m.box('Body', (x, 0.04, 0), (0.1, 0.08, 0.8), 'wi_wood')
    r = 0.12
    rows = [(3, 0.08 + r), (2, 0.08 + r + 2 * r * 0.866), (1, 0.08 + r + 4 * r * 0.866)]
    k = 0
    for n, y in rows:
        for i in range(n):
            z = (i - (n - 1) / 2) * 2 * r
            mat = 'wi_steel' if k % 3 else 'wi_copper'
            m.tube('Body', (-0.88, y, z), (0.88, y, z), r, mat, 10)
            m.tube('Body', (0.875, y, z), (0.885, y, z), r * 0.6, 'wi_dark', 10)
            m.tube('Body', (-0.885, y, z), (-0.875, y, z), r * 0.6, 'wi_dark', 10)
            k += 1
    for sz in (-1, 1):
        m.box('Body', (0.75, 0.2, sz * 0.4), (0.06, 0.3, 0.06), 'wi_yellow')
        m.box('Body', (-0.75, 0.2, sz * 0.4), (0.06, 0.3, 0.06), 'wi_yellow')
    return m


@reg('decor_scaffold')
def scaffold():
    m = Model()
    xs, zs, H = (-0.85, 0.85), (-0.35, 0.35), 2.4
    for x in xs:
        for z in zs:
            m.box('Body', (x, 0.02, z), (0.14, 0.04, 0.14), 'wi_dark')
            m.tube('Body', (x, 0.04, z), (x, H, z), 0.03, 'wi_steel', 6)
    for y in (0.4, 1.2, 2.0, H - 0.05):
        for z in zs:
            m.tube('Body', (-0.85, y, z), (0.85, y, z), 0.025, 'wi_steel', 6)
        for x in xs:
            m.tube('Body', (x, y, -0.35), (x, y, 0.35), 0.025, 'wi_steel', 6)
    for z in zs:
        m.tube('Body', (-0.85, 0.4, z), (0.85, 1.2, z), 0.02, 'wi_steel', 5)
    for y in (1.22, 2.02):
        for i in range(4):
            m.box('Body', (0, y + 0.03, -0.27 + i * 0.18), (1.75, 0.04, 0.16), 'wi_wood')
        m.box('Body', (0, y + 0.08, 0.37), (1.7, 0.1, 0.02), 'wi_yellow')
    for z in (-0.12, 0.12):
        m.tube('Body', (0.98, 0.0, z), (0.98, 2.1, z), 0.02, 'wi_steel', 5)
    for i in range(10):
        m.box('Body', (0.98, 0.2 + i * 0.2, 0), (0.03, 0.025, 0.24), 'wi_steel')
    m.box('Body', (-0.5, 1.4, 0.0), (0.4, 0.3, 0.3), 'wi_crate')
    return m


@reg('decor_tower_crane', parts=[('Jib', (0, 4.9, 0))], lights=[((0, 6.1, 0), (1.0, 0.25, 0.2), 4.0)], blink=1.2, yaw=135)
def tower_crane():
    m = Model()
    m.box('Body', (0, 0.2, 0), (1.4, 0.4, 1.4), 'wi_stone')
    m.box('Body', (0, 0.42, 0), (0.8, 0.06, 0.8), 'wi_dark')
    lattice(m, 'Body', [(-0.25, -0.25), (0.25, -0.25), (0.25, 0.25), (-0.25, 0.25)], 0.45, 4.6, 'wi_yellow', 0.06, 8)
    m.box('Body', (0, 4.7, 0), (0.6, 0.2, 0.6), 'wi_dark')
    # поворотная часть: кабина, стрела вперёд (+Z), противовес назад
    m.cyl('Jib', (0, 4.8, 0), 0.32, 0.12, 'wi_dark', 10)
    m.box('Jib', (0.32, 5.1, 0.15), (0.38, 0.4, 0.42), 'wi_yellow')
    m.box('Jib', (0.32, 5.15, 0.37), (0.3, 0.2, 0.02), 'wi_glass')
    for x in (-0.15, 0.15):
        m.beam('Jib', (x, 4.95, -1.4), (x, 4.95, 3.8), 0.06, 0.06, 'wi_yellow')
    m.beam('Jib', (0, 5.25, 0.2), (0, 5.25, 3.6), 0.05, 0.05, 'wi_yellow')
    for i in range(12):
        z = 0.2 + i * 0.3
        for x in (-0.15, 0.15):
            m.beam('Jib', (x, 4.95, z), (0, 5.25, z + 0.15), 0.025, 0.025, 'wi_yellow')
    m.box('Jib', (0, 5.0, -1.25), (0.5, 0.45, 0.5), 'wi_stone')
    m.box('Jib', (0, 5.0, -0.8), (0.42, 0.35, 0.3), 'wi_stone')
    m.tube('Jib', (0, 4.9, 0), (0, 5.9, 0), 0.06, 'wi_yellow', 6)
    m.beam('Jib', (0, 5.9, 0), (0, 5.27, 3.4), 0.015, 0.015, 'wi_steel')
    m.beam('Jib', (0, 5.9, 0), (0, 5.0, -1.2), 0.015, 0.015, 'wi_steel')
    m.box('Jib', (0, 4.86, 2.6), (0.24, 0.12, 0.2), 'wi_dark')
    m.tube('Jib', (0, 4.8, 2.6), (0, 3.2, 2.6), 0.01, 'wi_steel', 4)
    m.box('Jib', (0, 3.15, 2.6), (0.12, 0.1, 0.12), 'wi_redpaint')
    m.box('Jib', (0, 3.0, 2.6), (0.4, 0.2, 0.4), 'wi_crate')
    m.box('Lamp', (0, 5.95, 0), (0.1, 0.1, 0.1), 'wi_red')
    return m

# ================= ДОРОГИ И ОГРАЖДЕНИЯ (линии — вдоль +Z) =================

@reg('decor_fence', line=True)
def fence():
    m = Model()
    for z in (-0.47, 0.47):
        m.box('Body', (0, 0.45, z), (0.08, 0.9, 0.06), 'wi_wood')
        m.box('Body', (0, 0.92, z), (0.1, 0.04, 0.08), 'wi_paint')
    for y in (0.28, 0.7):
        m.box('Body', (0, y, 0), (0.05, 0.08, 1.0), 'wi_paint')
    for i in range(5):
        z = -0.32 + i * 0.16
        m.box('Body', (0.035, 0.42, z), (0.03, 0.72, 0.1), 'wi_wood')
        m.box('Body', (0.035, 0.81, z), (0.03, 0.06, 0.07), 'wi_wood')
    return m


@reg('decor_wire_fence', line=True)
def wire_fence():
    m = Model()
    for z in (-0.48, 0.48):
        m.tube('Body', (0, 0, z), (0, 1.25, z), 0.035, 'wi_steel', 6)
        m.beam('Body', (0, 1.2, z), (0.16, 1.38, z), 0.025, 0.025, 'wi_steel')
    m.tube('Body', (0, 1.0, -0.5), (0, 1.0, 0.5), 0.015, 'wi_steel', 4)
    m.tube('Body', (0, 0.08, -0.5), (0, 0.08, 0.5), 0.015, 'wi_steel', 4)
    n = 5
    for i in range(n):
        z0 = -0.46 + i * 0.92 / n
        z1 = z0 + 0.92 / n
        m.beam('Body', (0, 0.1, z0), (0, 0.98, z1), 0.008, 0.008, 'wi_steel')
        m.beam('Body', (0, 0.1, z1), (0, 0.98, z0), 0.008, 0.008, 'wi_steel')
    for y, x in ((1.24, 0.05), (1.31, 0.11)):
        m.tube('Body', (x, y, -0.5), (x, y, 0.5), 0.008, 'wi_dark', 4)
        for i in range(6):
            z = -0.42 + i * 0.17
            m.box('Body', (x, y, z), (0.05, 0.05, 0.008), 'wi_dark', rx=45)
    m.box('Body', (0.02, 0.6, 0), (0.01, 0.16, 0.24), 'wi_yellow')
    m.box('Body', (0.026, 0.6, 0), (0.01, 0.06, 0.06), 'wi_dark', rx=45)
    return m


@reg('decor_cone')
def cone():
    m = Model()
    for (x, z) in ((-0.22, 0.2), (0.25, 0.25)):
        m.box('Body', (x, 0.02, z), (0.3, 0.04, 0.3), 'wi_dark')
        m.cyl('Body', (x, 0.04, z), 0.12, 0.5, 'wi_orange', 8, r2=0.025)
        m.cyl('Body', (x, 0.22, z), 0.088, 0.08, 'wi_white', 8, r2=0.075)
    for sx in (-1, 1):
        m.beam('Body', (sx * 0.38, 0.0, -0.32), (sx * 0.3, 0.55, -0.2), 0.04, 0.04, 'wi_dark')
        m.beam('Body', (sx * 0.38, 0.0, -0.08), (sx * 0.3, 0.55, -0.2), 0.04, 0.04, 'wi_dark')
    stripes_x(m, 'Body', (0, 0.5, -0.2), 0.84, 0.16, 0.04, 6)
    m.box('Lamp', (0.32, 0.62, -0.2), (0.07, 0.07, 0.07), 'wi_fire')
    return m


@reg('decor_sign_arrow', yaw=120)
def sign_arrow():
    m = Model()
    m.box('Body', (0, 0.04, 0), (0.24, 0.08, 0.24), 'wi_stone')
    m.cyl('Body', (0, 0.08, 0), 0.04, 1.35, 'wi_steel', 6)
    y = 1.25
    # стрелка смотрит вперёд (+Z): табличка в плоскости YZ
    m.box('Body', (0.05, y, -0.08), (0.03, 0.3, 0.55), 'wi_yellow')
    m.face('Body', [(0.035, y + 0.24, 0.19), (0.035, y - 0.24, 0.19), (0.035, y, 0.45)], 'wi_yellow', (0, y, 0.2))
    m.face('Body', [(0.065, y + 0.24, 0.19), (0.065, y - 0.24, 0.19), (0.065, y, 0.45)], 'wi_yellow', (1, y, 0.2))
    m.box('Body', (0.05, y + 0.24, 0.32), (0.03, 0.02, 0.3), 'wi_yellow', rx=-43)
    m.chevron('Body', (0.07, y, 0.02), 0, 'wi_dark', size=0.2, w=0.05)
    m.box('Body', (0.068, y, -0.18), (0.01, 0.06, 0.22), 'wi_dark')
    return m


@reg('decor_paving', floor=True, yaw=145)
def paving():
    m = Model()
    m.box('Body', (0, 0.012, 0), (1.0, 0.024, 1.0), 'wi_dark')
    for ix in range(2):
        for iz in range(2):
            x = -0.25 + ix * 0.5
            z = -0.25 + iz * 0.5
            m.box('Body', (x, 0.026, z), (0.47, 0.04, 0.47), 'wi_paving')
    m.box('Body', (0.25, 0.047, -0.25), (0.47, 0.002, 0.47), 'wi_stone')
    return m


@reg('decor_asphalt', floor=True, line=True)
def asphalt():
    m = Model()
    m.box('Body', (0, 0.018, 0), (1.0, 0.036, 1.0), 'wi_asphalt')
    m.box('Body', (0, 0.037, 0), (0.07, 0.003, 0.45), 'wi_white')
    for x in (-0.47, 0.47):
        m.box('Body', (x, 0.037, 0), (0.025, 0.003, 1.0), 'wi_yellow')
    return m


@reg('decor_barrier_gate', parts=[('Arm', (-0.72, 0.92, 0))])
def barrier_gate():
    m = Model()
    m.box('Body', (-0.72, 0.04, 0), (0.4, 0.08, 0.4), 'wi_stone')
    m.box('Body', (-0.72, 0.5, 0), (0.28, 0.84, 0.28), 'wi_yellow')
    stripes_v(m, 'Body', (-0.72, 0.1, 0.142), 0.6, 0.28, 0.01, 5, 'wi_yellow', 'wi_dark')
    m.box('Body', (-0.72, 0.96, 0), (0.32, 0.06, 0.32), 'wi_dark')
    m.box('Lamp', (-0.72, 1.02, 0), (0.08, 0.06, 0.08), 'wi_red')
    m.tube('Arm', (-0.72, 0.92, 0.17), (0.92, 0.92, 0.17), 0.04, 'wi_white', 8)
    for i in range(5):
        x0 = -0.5 + i * 0.32
        m.tube('Arm', (x0, 0.92, 0.17), (x0 + 0.16, 0.92, 0.17), 0.043, 'wi_redpaint', 8)
    m.box('Arm', (-0.72, 0.92, 0.12), (0.12, 0.12, 0.1), 'wi_dark')
    m.box('Body', (0.85, 0.35, 0.17), (0.06, 0.7, 0.06), 'wi_dark')
    m.box('Body', (0.85, 0.72, 0.17), (0.14, 0.05, 0.1), 'wi_yellow')
    m.box('Body', (0.85, 0.03, 0.17), (0.2, 0.06, 0.2), 'wi_stone')
    return m


@reg('decor_factory_gate', yaw=150,
     text=dict(pos=(0, 2.55, 0.095), size=0.05, color=(1.0, 0.93, 0.7), kind='world'))
def factory_gate():
    m = Model()
    for sx in (-1, 1):
        m.box('Body', (sx * 1.32, 1.2, 0), (0.32, 2.4, 0.36), 'wi_brick')
        m.box('Body', (sx * 1.32, 2.44, 0), (0.4, 0.08, 0.44), 'wi_stone')
        m.box('Body', (sx * 1.32, 0.06, 0), (0.4, 0.12, 0.44), 'wi_stone')
        m.cyl('Body', (sx * 1.32, 2.48, 0), 0.08, 0.14, 'wi_dark', 6)
        m.box('Lamp', (sx * 1.32, 2.68, 0), (0.12, 0.12, 0.12), 'wi_warm')
        m.cyl('Body', (sx * 1.32, 2.74, 0), 0.1, 0.06, 'wi_dark', 6, r2=0.02)
        # открытая створка вдоль столба (внутрь участка, −Z)
        x0 = sx * 1.12
        for i in range(6):
            z = -0.08 - i * 0.16
            m.box('Body', (x0, 0.95, z), (0.03, 1.7, 0.03), 'wi_dark')
        for y in (0.2, 1.75):
            m.box('Body', (x0, y, -0.48), (0.04, 0.05, 0.86), 'wi_dark')
        m.box('Body', (x0, 1.0, -0.48), (0.03, 0.06, 0.86), 'wi_gold')
    m.box('Body', (0, 2.55, 0), (2.4, 0.42, 0.14), 'wi_paint')
    m.box('Body', (0, 2.55, 0.07), (2.3, 0.34, 0.01), 'wi_dark')
    m.box('Body', (0, 2.8, 0), (2.5, 0.06, 0.18), 'wi_copper')
    m.box('Body', (0, 2.3, 0), (2.5, 0.06, 0.18), 'wi_copper')
    stripes_x(m, 'Body', (0, 0.01, 0.3), 2.3, 0.01, 0.12, 8, 'wi_yellow', 'wi_dark')
    return m

# ================= ЗОНА ОТДЫХА =================

@reg('decor_vending', lights=[((0, 1.1, 0.5), (0.6, 0.85, 1.0), 3.5)], yaw=150)
def vending():
    m = Model()
    m.box('Body', (0, 0.04, -0.02), (0.74, 0.08, 0.62), 'wi_dark')
    m.box('Body', (0, 0.95, -0.02), (0.72, 1.78, 0.58), 'wi_redpaint')
    m.box('Body', (-0.1, 1.15, 0.272), (0.44, 1.1, 0.02), 'wi_glass')
    cols = ['wi_yellow', 'wi_blue', 'wi_lamp', 'wi_white', 'wi_orange']
    for r in range(5):
        y = 0.72 + r * 0.21
        m.box('Body', (-0.1, y - 0.06, 0.25), (0.42, 0.015, 0.06), 'wi_steel')
        for c in range(4):
            m.box('Body', (-0.27 + c * 0.11, y, 0.25), (0.06, 0.1, 0.05), cols[(r + c) % 5])
    m.box('Body', (0.24, 1.25, 0.272), (0.14, 0.22, 0.02), 'wi_dark')
    for i in range(3):
        m.box('Body', (0.24, 1.0 - i * 0.08, 0.275), (0.08, 0.04, 0.02), 'wi_steel')
    m.box('Body', (-0.05, 0.38, 0.27), (0.5, 0.16, 0.04), 'wi_dark')
    m.box('Lamp', (0, 1.88, 0.27), (0.66, 0.1, 0.03), 'wi_warm')
    m.box('Body', (0, 1.86, -0.02), (0.76, 0.04, 0.62), 'wi_dark')
    return m


@reg('decor_cooler')
def cooler():
    m = Model()
    m.box('Body', (0, 0.5, 0), (0.36, 1.0, 0.34), 'wi_white')
    m.box('Body', (0, 0.62, 0.172), (0.26, 0.24, 0.01), 'wi_dark')
    m.box('Body', (-0.06, 0.66, 0.18), (0.05, 0.06, 0.04), 'wi_blue')
    m.box('Body', (0.06, 0.66, 0.18), (0.05, 0.06, 0.04), 'wi_red')
    m.box('Body', (0, 0.52, 0.19), (0.2, 0.02, 0.06), 'wi_steel')
    m.cyl('Body', (0, 1.0, 0), 0.15, 0.04, 'wi_steel', 10)
    m.cyl('Body', (0, 1.04, 0), 0.17, 0.42, 'wi_water', 10)
    m.cyl('Body', (0, 1.46, 0), 0.17, 0.08, 'wi_water', 10, r2=0.06)
    m.cyl('Body', (0, 1.54, 0), 0.06, 0.04, 'wi_blue', 8)
    m.cyl('Body', (0.28, 0, 0.1), 0.06, 0.55, 'wi_steel', 6)
    for i in range(4):
        m.cyl('Body', (0.28, 0.55 + i * 0.07, 0.1), 0.06, 0.07, 'wi_white', 6, r2=0.05)
    return m


@reg('decor_bbq', parts=[('Coals', (0, 0.62, 0))], lights=[((0, 0.9, 0), (1.0, 0.5, 0.2), 3.0)], smoke=(0, 0.95, 0))
def bbq():
    m = Model()
    for sx in (-1, 1):
        m.beam('Body', (sx * 0.28, 0, -0.18), (sx * 0.24, 0.55, -0.1), 0.04, 0.04, 'wi_dark')
        m.beam('Body', (sx * 0.28, 0, 0.18), (sx * 0.24, 0.55, 0.1), 0.04, 0.04, 'wi_dark')
    m.box('Body', (0, 0.3, 0), (0.48, 0.03, 0.3), 'wi_dark')
    m.box('Body', (0, 0.62, 0), (0.64, 0.16, 0.36), 'wi_dark')
    m.box('Body', (0, 0.555, 0), (0.6, 0.02, 0.32), 'wi_steel')
    m.box('Coals', (0, 0.66, 0), (0.56, 0.04, 0.28), 'wi_fire')
    for i in range(7):
        m.box('Body', (-0.27 + i * 0.09, 0.705, 0), (0.015, 0.015, 0.34), 'wi_steel')
    m.box('Body', (-0.1, 0.725, 0.04), (0.16, 0.03, 0.06), 'wi_brick')
    m.box('Body', (0.12, 0.725, -0.05), (0.14, 0.03, 0.06), 'wi_brick')
    m.box('Body', (0, 0.9, -0.2), (0.64, 0.4, 0.03), 'wi_dark', rx=-15)
    m.box('Body', (0, 1.08, -0.16), (0.2, 0.03, 0.05), 'wi_wood')
    m.box('Body', (0.42, 0.6, 0), (0.18, 0.02, 0.28), 'wi_wood')
    return m


@reg('decor_bench')
def bench():
    m = Model()
    for x in (-0.72, 0.72):
        m.box('Body', (x, 0.22, 0.0), (0.06, 0.44, 0.06), 'wi_paint')
        m.box('Body', (x, 0.22, 0.18), (0.06, 0.44, 0.06), 'wi_paint')
        m.box('Body', (x, 0.44, 0.06), (0.07, 0.04, 0.46), 'wi_paint')
        m.beam('Body', (x, 0.44, -0.15), (x, 0.92, -0.22), 0.06, 0.06, 'wi_paint')
        m.box('Body', (x, 0.6, 0.18), (0.06, 0.04, 0.3), 'wi_paint')
    for i in range(4):
        m.box('Body', (0, 0.475, -0.11 + i * 0.11), (1.62, 0.035, 0.09), 'wi_wood')
    for i in range(3):
        y = 0.6 + i * 0.12
        z = -0.17 - (y - 0.6) * 0.15
        m.box('Body', (0, y, z), (1.62, 0.09, 0.03), 'wi_wood', rx=-8)
    return m


@reg('decor_picnic', yaw=140)
def picnic():
    m = Model()
    for x in (-0.62, 0.62):
        m.beam('Body', (x, 0, -0.55), (x, 0.72, 0.0), 0.06, 0.06, 'wi_wood')
        m.beam('Body', (x, 0, 0.55), (x, 0.72, 0.0), 0.06, 0.06, 'wi_wood')
        m.box('Body', (x, 0.42, 0), (0.06, 0.05, 1.3), 'wi_wood')
    for i in range(4):
        m.box('Body', (0, 0.75, -0.24 + i * 0.16), (1.6, 0.04, 0.14), 'wi_wood')
    for sz in (-1, 1):
        for i in range(2):
            m.box('Body', (0, 0.45, sz * (0.48 + i * 0.12)), (1.6, 0.04, 0.11), 'wi_wood')
    m.tube('Body', (0, 0.0, 0), (0, 2.0, 0), 0.025, 'wi_steel', 6)
    m.cyl('Body', (0, 1.75, 0), 0.95, 0.32, 'wi_paint', 8, r2=0.05)
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        m.box('Body', (0.93 * math.cos(a), 1.72, 0.93 * math.sin(a)), (0.08, 0.08, 0.3), 'wi_white', yaw=-math.degrees(a))
    m.box('Body', (0.3, 0.8, 0.05), (0.18, 0.06, 0.14), 'wi_crate')
    m.cyl('Body', (-0.35, 0.77, -0.05), 0.04, 0.12, 'wi_redpaint', 6)
    return m


@reg('decor_gazebo', lights=[((0, 2.0, 0), (1.0, 0.85, 0.6), 5.5)], yaw=150)
def gazebo():
    m = Model()
    m.cyl('Body', (0, 0, 0), 1.42, 0.16, 'wi_stone', 8)
    m.cyl('Body', (0, 0.16, 0), 1.32, 0.04, 'wi_wood', 8)
    R = 1.18
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        x, z = R * math.cos(a), R * math.sin(a)
        m.box('Body', (x, 1.15, z), (0.1, 1.9, 0.1), 'wi_white')
        if k != 1:   # вход спереди (+Z)
            a2 = 2 * math.pi * (k + 1) / 8 + math.pi / 8
            x2, z2 = R * math.cos(a2), R * math.sin(a2)
            m.beam('Body', (x, 0.75, z), (x2, 0.75, z2), 0.06, 0.06, 'wi_white')
            mx, mz = (x + x2) / 2 * 0.86, (z + z2) / 2 * 0.86
            m.beam('Body', (x * 0.86, 0.42, z * 0.86), (x2 * 0.86, 0.42, z2 * 0.86), 0.32, 0.05, 'wi_wood')
            for t in (0.25, 0.5, 0.75):
                px, pz = x + (x2 - x) * t, z + (z2 - z) * t
                m.box('Body', (px, 0.48, pz), (0.04, 0.5, 0.04), 'wi_white')
    m.cyl('Body', (0, 2.1, 0), 1.4, 0.08, 'wi_white', 8)
    m.cyl('Body', (0, 2.18, 0), 1.55, 0.75, 'wi_paint', 8, r2=0.12)
    m.cyl('Body', (0, 2.93, 0), 0.14, 0.2, 'wi_paint', 8, r2=0.02)
    m.cyl('Body', (0, 3.1, 0), 0.05, 0.12, 'wi_gold', 6)
    m.cyl('Body', (0, 1.9, 0), 0.02, 0.2, 'wi_dark', 4)
    m.box('Lamp', (0, 1.85, 0), (0.12, 0.12, 0.12), 'wi_warm')
    m.cyl('Body', (0, 0.2, 0), 0.32, 0.55, 'wi_wood', 8)
    m.cyl('Body', (0, 0.75, 0), 0.45, 0.04, 'wi_wood', 8)
    return m

# ================= ПАМЯТНИКИ И «ЖИВЫЕ» =================

@reg('decor_flagpole', parts=[('Flag', (0.04, 2.9, 0))], tint_part='Flag', yaw=130)
def flagpole():
    m = Model()
    m.box('Body', (0, 0.08, 0), (0.5, 0.16, 0.5), 'wi_stone')
    m.box('Body', (0, 0.2, 0), (0.32, 0.08, 0.32), 'wi_stone')
    m.cyl('Body', (0, 0.24, 0), 0.04, 3.0, 'wi_steel', 8, r2=0.025)
    ball(m, 'Body', (0, 3.29, 0), 0.06, 'wi_gold', 6, 3)
    m.box('Flag', (0.5, 2.62, 0), (0.92, 0.56, 0.02), 'wi_paint')
    m.box('Flag', (0.5, 2.62, 0.012), (0.3, 0.3, 0.005), 'wi_white', rz=45)
    m.box('Flag', (0.5, 2.62, -0.012), (0.3, 0.3, 0.005), 'wi_white', rz=45)
    m.box('Flag', (0.04, 2.62, 0), (0.03, 0.6, 0.03), 'wi_white')
    return m


def clock_hands(m, side):
    s = 1 if side > 0 else -1
    z = s * 0.085
    tag = 'F' if side > 0 else 'B'
    # стрелки на 12:00, вращает код вокруг оси Z
    m.box('Hour' + tag, (0, 0.07, z), (0.035, 0.15, 0.01), 'wi_dark')
    m.box('Min' + tag, (0, 0.1, z + s * 0.008), (0.022, 0.22, 0.01), 'wi_dark')


@reg('decor_clock', parts=[('HourF', (0, 2.25, 0)), ('MinF', (0, 2.25, 0)), ('HourB', (0, 2.25, 0)), ('MinB', (0, 2.25, 0))],
     lights=[((0, 2.25, 0.3), (1.0, 0.9, 0.7), 2.5)], yaw=160)
def clock():
    m = Model()
    m.cyl('Body', (0, 0, 0), 0.2, 0.1, 'wi_dark', 8)
    m.cyl('Body', (0, 0.1, 0), 0.12, 0.3, 'wi_paint', 8, r2=0.07)
    m.cyl('Body', (0, 0.4, 0), 0.05, 1.5, 'wi_dark', 8)
    m.cyl('Body', (0, 1.85, 0), 0.08, 0.12, 'wi_paint', 8)
    m.tube('Body', (0, 2.25, -0.07), (0, 2.25, 0.07), 0.32, 'wi_paint', 12)
    m.tube('Body', (0, 2.25, -0.075), (0, 2.25, 0.075), 0.27, 'wi_warm', 12)
    for k in range(12):
        a = 2 * math.pi * k / 12
        for s in (1, -1):
            L = 0.06 if k % 3 == 0 else 0.03
            m.box('Body', (0.22 * math.sin(a), 2.25 + 0.22 * math.cos(a), s * 0.078), (0.018, L, 0.004), 'wi_dark', rz=-math.degrees(a))
    m.cyl('Body', (0, 2.57, 0), 0.06, 0.08, 'wi_gold', 8, r2=0.0)
    clock_hands(m, 1)
    clock_hands(m, -1)
    # стрелки — отдельные модели с центром на оси циферблата
    return m


@reg('decor_scoreboard', lights=[((0, 1.5, 0.4), (0.5, 1.0, 0.6), 4.0)],
     text=dict(pos=(0, 1.5, 0.085), size=0.03, color=(0.55, 1.0, 0.65), kind='stats'))
def scoreboard():
    m = Model()
    for sx in (-1, 1):
        m.box('Body', (sx * 0.78, 0.06, 0), (0.24, 0.12, 0.24), 'wi_stone')
        m.box('Body', (sx * 0.78, 0.55, 0), (0.08, 1.0, 0.08), 'wi_dark')
    m.box('Body', (0, 1.5, 0), (1.9, 1.0, 0.12), 'wi_paint')
    m.box('Body', (0, 1.5, 0.05), (1.78, 0.88, 0.03), 'wi_dark')
    m.box('Body', (0, 2.03, 0), (1.94, 0.06, 0.16), 'wi_copper')
    m.box('Body', (0, 0.97, 0), (1.94, 0.06, 0.16), 'wi_copper')
    for x in (-0.6, 0.0, 0.6):
        m.box('Lamp', (x, 2.1, 0.02), (0.1, 0.08, 0.08), 'wi_lamp')
    m.box('Body', (0, 2.25, -0.02), (0.6, 0.22, 0.05), 'wi_gold')
    return m


def gear_shape(m, obj, c, r, teeth, w, mat, hub_mat):
    cx, cy, cz = c
    for k in range(teeth * 2):
        a = 2 * math.pi * k / (teeth * 2)
        L = 2 * r * math.sin(math.pi / (teeth * 2)) + 0.02
        m.box(obj, (cx + r * math.sin(a), cy + r * math.cos(a), cz), (L, 0.16 * r, w), mat, rz=-math.degrees(a))
    for k in range(teeth):
        a = 2 * math.pi * k / teeth
        m.box(obj, (cx + (r + 0.1 * r) * math.sin(a), cy + (r + 0.1 * r) * math.cos(a), cz), (0.16 * r, 0.22 * r, w * 0.9), mat, rz=-math.degrees(a))
    for k in range(5):
        a = 2 * math.pi * k / 5
        m.beam(obj, (cx, cy, cz), (cx + r * 0.95 * math.sin(a), cy + r * 0.95 * math.cos(a), cz), 0.1 * r, w * 0.7, mat)
    m.tube(obj, (cx, cy, cz - w * 0.7), (cx, cy, cz + w * 0.7), 0.2 * r, hub_mat, 10)


@reg('decor_gear_monument', parts=[('Gear', (0, 1.75, 0))], yaw=155)
def gear_monument():
    m = Model()
    m.box('Body', (0, 0.1, 0), (1.8, 0.2, 1.4), 'wi_stone')
    m.box('Body', (0, 0.35, 0), (1.4, 0.3, 1.0), 'wi_stone')
    m.box('Body', (0, 0.62, 0), (0.6, 0.24, 0.5), 'wi_dark')
    m.box('Body', (0, 0.35, 0.505), (0.7, 0.18, 0.01), 'wi_gold')
    m.box('Body', (0, 0.75, 0), (0.18, 0.3, 0.18), 'wi_steel')
    gear_shape(m, 'Gear', (0, 1.75, 0), 0.82, 10, 0.16, 'wi_gold', 'wi_copper')
    for sx in (-1, 1):
        m.box('Body', (sx * 0.8, 0.28, 0.6), (0.1, 0.1, 0.1), 'wi_warm')
    return m


@reg('decor_rocket', lights=[((0, 0.6, 0), (1.0, 0.5, 0.2), 5.0)], yaw=150)
def rocket():
    m = Model()
    m.cyl('Body', (0, 0, 0), 1.4, 0.3, 'wi_stone', 8)
    m.cyl('Body', (0, 0.3, 0), 1.1, 0.08, 'wi_dark', 8)
    stripes_x(m, 'Body', (0, 0.39, 0.95), 1.4, 0.02, 0.2, 8, 'wi_yellow', 'wi_dark')
    for k in range(4):
        a = 2 * math.pi * k / 4 + math.pi / 4
        m.beam('Body', (0.85 * math.cos(a), 0.38, 0.85 * math.sin(a)), (0.4 * math.cos(a), 1.2, 0.4 * math.sin(a)), 0.08, 0.08, 'wi_steel')
    y0 = 0.55
    m.cyl('Body', (0, y0, 0), 0.26, 0.3, 'wi_dark', 10, r2=0.34)
    m.cyl('Lamp', (0, y0 - 0.05, 0), 0.2, 0.06, 'wi_fire', 10)
    m.cyl('Body', (0, y0 + 0.3, 0), 0.45, 3.1, 'wi_white', 12)
    for y in (y0 + 0.9, y0 + 2.3):
        m.cyl('Body', (0, y, 0), 0.46, 0.12, 'wi_redpaint', 12)
    m.cyl('Body', (0, y0 + 3.4, 0), 0.45, 1.2, 'wi_redpaint', 12, r2=0.0)
    for y in (y0 + 1.6, y0 + 2.0):
        m.tube('Body', (0, y, 0.42), (0, y, 0.47), 0.11, 'wi_steel', 10)
        m.tube('Body', (0, y, 0.43), (0, y, 0.48), 0.08, 'wi_blue', 10)
    for k in range(4):
        a = 2 * math.pi * k / 4
        d = (math.cos(a), 0, math.sin(a))
        p0 = (0.42 * d[0], y0 + 0.35, 0.42 * d[2])
        p1 = (0.42 * d[0], y0 + 1.3, 0.42 * d[2])
        p2 = (0.95 * d[0], y0 + 0.2, 0.95 * d[2])
        p3 = (0.9 * d[0], y0 + 0.65, 0.9 * d[2])
        nrm = (-d[2] * 0.03, 0, d[0] * 0.03)
        for s in (1, -1):
            q = [(p[0] + s * nrm[0], p[1], p[2] + s * nrm[2]) for p in (p0, p1, p3, p2)]
            m.face('Body', q, 'wi_redpaint', (q[0][0] - s * nrm[0] * 10, q[0][1], q[0][2] - s * nrm[2] * 10))
        m.beam('Body', (p0[0], p0[1], p0[2]), (p2[0], p2[1], p2[2]), 0.06, 0.06, 'wi_redpaint')
        m.beam('Body', (p1[0], p1[1], p1[2]), (p3[0], p3[1], p3[2]), 0.06, 0.06, 'wi_redpaint')
        m.beam('Body', (p2[0], p2[1], p2[2]), (p3[0], p3[1], p3[2]), 0.06, 0.06, 'wi_redpaint')
    m.box('Body', (0, y0 + 1.1, 0.455), (0.22, 0.5, 0.02), 'wi_dark')
    m.box('Body', (0, 0.25, 1.15), (0.5, 0.25, 0.04), 'wi_gold')
    return m


@reg('decor_robot', parts=[('Arm', (0.24, 0.98, 0))], tint_part='Body', yaw=160)
def robot():
    m = Model()
    for sx in (-1, 1):
        m.box('Body', (sx * 0.11, 0.06, 0.04), (0.14, 0.12, 0.22), 'wi_dark')
        m.box('Body', (sx * 0.11, 0.32, 0), (0.1, 0.4, 0.1), 'wi_steel')
    m.box('Body', (0, 0.58, 0), (0.36, 0.16, 0.22), 'wi_dark')
    m.box('Body', (0, 0.84, 0), (0.42, 0.42, 0.28), 'wi_paint')
    m.box('Body', (0, 0.86, 0.142), (0.18, 0.14, 0.01), 'wi_yellow')
    m.box('Body', (0, 0.86, 0.148), (0.08, 0.06, 0.01), 'wi_lamp')
    m.box('Body', (0, 1.08, 0), (0.12, 0.06, 0.12), 'wi_dark')
    m.box('Body', (0, 1.25, 0), (0.36, 0.28, 0.28), 'wi_paint')
    for sx in (-1, 1):
        m.box('Body', (sx * 0.08, 1.27, 0.142), (0.08, 0.06, 0.01), 'wi_lamp')
    m.box('Body', (0, 1.17, 0.142), (0.14, 0.02, 0.01), 'wi_dark')
    m.tube('Body', (0, 1.39, 0), (0, 1.56, 0), 0.012, 'wi_steel', 4)
    ball(m, 'Body', (0, 1.6, 0), 0.04, 'wi_red', 6, 3)
    m.box('Body', (-0.26, 0.8, 0), (0.09, 0.36, 0.09), 'wi_steel', rz=-10)
    m.box('Body', (-0.29, 0.6, 0), (0.12, 0.1, 0.12), 'wi_dark')
    # правая рука машет: вверх от плеча
    m.box('Arm', (0.3, 1.13, 0), (0.09, 0.32, 0.09), 'wi_steel', rz=-20)
    m.box('Arm', (0.36, 1.31, 0), (0.12, 0.1, 0.12), 'wi_dark')
    m.box('Arm', (0.24, 0.98, 0), (0.1, 0.1, 0.1), 'wi_dark')
    return m


@reg('decor_windmill', parts=[('Blades', (0, 3.55, 0.3))], yaw=150)
def windmill():
    m = Model()
    m.box('Body', (0, 0.1, 0), (1.0, 0.2, 1.0), 'wi_stone')
    m.cyl('Body', (0, 0.2, 0), 0.24, 3.2, 'wi_white', 10, r2=0.1)
    m.box('Body', (0, 3.45, 0.0), (0.3, 0.3, 0.6), 'wi_white')
    m.box('Body', (0, 3.45, -0.32), (0.22, 0.22, 0.06), 'wi_paint')
    m.box('Body', (0, 3.62, -0.15), (0.05, 0.12, 0.3), 'wi_paint')
    m.box('Body', (0, 0.55, 0.245), (0.2, 0.4, 0.02), 'wi_dark')
    m.tube('Blades', (0, 3.55, 0.28), (0, 3.55, 0.42), 0.09, 'wi_paint', 8, r2=0.03)
    for k in range(3):
        a = 2 * math.pi * k / 3
        tip = (1.4 * math.sin(a), 3.55 + 1.4 * math.cos(a), 0.36)
        mid = (0.25 * math.sin(a), 3.55 + 0.25 * math.cos(a), 0.36)
        m.beam('Blades', mid, tip, 0.24, 0.04, 'wi_white')
        m.beam('Blades', (0.25 * math.sin(a), 3.55 + 0.25 * math.cos(a), 0.36),
               (1.1 * math.sin(a), 3.55 + 1.1 * math.cos(a), 0.37), 0.06, 0.035, 'wi_paint')
    return m


@reg('decor_radio_tower', parts=[('Light', (0, 5.6, 0))], lights=[((0, 5.6, 0), (1.0, 0.2, 0.15), 5.0)], blink=1.0, yaw=150)
def radio_tower():
    m = Model()
    m.box('Body', (0, 0.06, 0), (1.9, 0.12, 1.9), 'wi_stone')
    lattice(m, 'Body', [(-0.55, -0.45), (0.55, -0.45), (0.0, 0.6)], 0.12, 5.2, 'wi_redpaint', 0.06, 9, 0.18)
    m.box('Body', (0.6, 0.45, 0.55), (0.55, 0.65, 0.55), 'wi_white')
    m.gable('Body', (0.6, 0.78, 0.55), 0.55, 0.55, 0.15, 'wi_paint', over=0.04)
    m.box('Body', (0.6, 0.4, 0.83), (0.18, 0.4, 0.01), 'wi_dark')
    m.tube('Body', (0, 5.2, 0), (0, 5.55, 0), 0.03, 'wi_steel', 6)
    m.box('Light', (0, 5.6, 0), (0.12, 0.12, 0.12), 'wi_red')
    m.tube('Body', (0.08, 3.6, 0.1), (0.3, 3.75, 0.3), 0.22, 'wi_white', 10, r2=0.04)
    m.beam('Body', (0, 4.4, 0.0), (0.0, 4.4, 0.35), 0.03, 0.03, 'wi_steel')
    m.box('Body', (0, 4.4, 0.4), (0.1, 0.5, 0.06), 'wi_white')
    m.box('Body', (-0.08, 2.4, -0.05), (0.08, 0.6, 0.08), 'wi_dark')
    return m


@reg('decor_hologram', parts=[('Holo', (0, 1.25, 0))], lights=[((0, 1.2, 0), (0.3, 0.85, 1.0), 5.0)], yaw=150)
def hologram():
    m = Model()
    m.cyl('Body', (0, 0, 0), 0.9, 0.12, 'wi_dark', 12)
    m.cyl('Body', (0, 0.12, 0), 0.75, 0.08, 'wi_steel', 12)
    m.cyl('Body', (0, 0.2, 0), 0.3, 0.12, 'wi_dark', 10, r2=0.2)
    m.cyl('Lamp', (0, 0.32, 0), 0.18, 0.03, 'wi_holo', 10)
    for k in range(8):
        a = 2 * math.pi * k / 8
        m.box('Lamp', (0.8 * math.cos(a), 0.13, 0.8 * math.sin(a)), (0.08, 0.04, 0.08), 'wi_holo')
    # голограмма: шестерня-эмблема и кольца-орбиты
    r = 0.45
    for k in range(16):
        a = 2 * math.pi * k / 16
        L = 2 * r * math.sin(math.pi / 16) + 0.01
        m.box('Holo', (r * math.cos(a), 1.25, r * math.sin(a)), (0.025, 0.025, L), 'wi_holo', yaw=-math.degrees(a))
        m.box('Holo', (r * 0.98 * math.cos(a), 1.25 + r * 0.98 * math.sin(a), 0), (0.025, L, 0.025), 'wi_holo', rz=math.degrees(a))
    gear_shape(m, 'Holo', (0, 1.25, 0), 0.22, 8, 0.05, 'wi_holo', 'wi_holo')
    for k in range(6):
        a = 2 * math.pi * k / 6
        m.box('Holo', (0.62 * math.cos(a), 1.25 + 0.1 * math.sin(3 * a), 0.62 * math.sin(a)), (0.06, 0.06, 0.06), 'wi_holo')
    return m


@reg('decor_golden_cup', yaw=150)
def golden_cup():
    m = Model()
    m.box('Body', (0, 0.25, 0), (0.56, 0.5, 0.56), 'wi_stone')
    m.box('Body', (0, 0.52, 0), (0.62, 0.05, 0.62), 'wi_dark')
    m.box('Body', (0, 0.3, 0.282), (0.36, 0.14, 0.01), 'wi_gold')
    m.box('Body', (0, 0.6, 0), (0.3, 0.1, 0.3), 'wi_dark')
    m.cyl('Body', (0, 0.65, 0), 0.12, 0.05, 'wi_gold', 10)
    m.cyl('Body', (0, 0.7, 0), 0.04, 0.22, 'wi_gold', 8)
    m.cyl('Body', (0, 0.9, 0), 0.07, 0.1, 'wi_gold', 10, r2=0.2)
    m.cyl('Body', (0, 1.0, 0), 0.2, 0.32, 'wi_gold', 10, r2=0.25)
    m.cyl('Body', (0, 1.32, 0), 0.255, 0.02, 'wi_gold', 10)
    for sx in (-1, 1):
        m.beam('Body', (sx * 0.2, 1.24, 0), (sx * 0.34, 1.2, 0), 0.04, 0.04, 'wi_gold')
        m.beam('Body', (sx * 0.34, 1.2, 0), (sx * 0.3, 1.02, 0), 0.04, 0.04, 'wi_gold')
        m.beam('Body', (sx * 0.3, 1.02, 0), (sx * 0.18, 1.0, 0), 0.04, 0.04, 'wi_gold')
    m.box('Body', (0, 1.12, 0.215), (0.12, 0.12, 0.01), 'wi_redpaint', rz=45)
    return m

# ================= СЕЗОННЫЕ =================

@reg('decor_snowman', yaw=160)
def snowman():
    m = Model()
    m.cyl('Body', (0, 0, 0), 0.42, 0.03, 'wi_snow', 10)
    ball(m, 'Body', (0, 0.3, 0), 0.32, 'wi_snow', 10, 5)
    ball(m, 'Body', (0, 0.76, 0), 0.23, 'wi_snow', 10, 5)
    ball(m, 'Body', (0, 1.1, 0), 0.17, 'wi_snow', 9, 5)
    m.tube('Body', (0, 1.1, 0.15), (0, 1.08, 0.36), 0.035, 'wi_orange', 6, r2=0.0)
    for sx in (-1, 1):
        m.box('Body', (sx * 0.06, 1.16, 0.15), (0.035, 0.035, 0.02), 'wi_dark')
        m.beam('Body', (sx * 0.2, 0.8, 0), (sx * 0.5, 1.02, 0.05), 0.025, 0.025, 'wi_wood')
        m.beam('Body', (sx * 0.43, 0.97, 0.04), (sx * 0.5, 1.1, 0.0), 0.018, 0.018, 'wi_wood')
    for y in (0.62, 0.74, 0.86):
        m.box('Body', (0, y, 0.225), (0.035, 0.035, 0.02), 'wi_dark')
    m.cyl('Body', (0, 0.92, 0), 0.19, 0.07, 'wi_redpaint', 10)
    m.box('Body', (0.1, 0.84, 0.15), (0.08, 0.18, 0.03), 'wi_redpaint', rz=10)
    m.cyl('Body', (0, 1.23, 0), 0.19, 0.02, 'wi_dark', 10)
    m.cyl('Body', (0, 1.25, 0), 0.13, 0.2, 'wi_dark', 10)
    m.cyl('Body', (0, 1.27, 0), 0.135, 0.03, 'wi_redpaint', 10)
    return m


@reg('decor_xmas_tree', parts=[('LightsA', (0, 0, 0)), ('LightsB', (0, 0, 0))],
     lights=[((0, 1.6, 0), (1.0, 0.8, 0.45), 6.0)], blink=0.7, yaw=150)
def xmas_tree():
    m = Model()
    m.cyl('Body', (0, 0, 0), 0.9, 0.04, 'wi_snow', 10)
    m.cyl('Body', (0, 0.04, 0), 0.32, 0.22, 'wi_redpaint', 8)
    m.cyl('Body', (0, 0.26, 0), 0.12, 0.3, 'wi_wood', 6)
    tiers = [(0.5, 0.85, 0.85), (1.05, 0.68, 0.75), (1.55, 0.5, 0.7), (2.0, 0.32, 0.6)]
    for y, r, h in tiers:
        m.cyl('Body', (0, y, 0), r, h, 'wi_leaf2', 9, r2=0.02)
    rng = np.random.default_rng(11)
    orn = ['wi_redpaint', 'wi_gold', 'wi_blue', 'wi_white']
    k = 0
    for y, r, h in tiers:
        for i in range(6):
            a = 2 * math.pi * i / 6 + y
            t = 0.3
            rr = r * (1 - t) * 0.98
            yy = y + h * t
            m.box('Body', (rr * math.cos(a), yy - 0.04, rr * math.sin(a)), (0.08, 0.08, 0.08), orn[k % 4], yaw=30)
            k += 1
        for i in range(8):
            a = 2 * math.pi * i / 8 + y * 1.7
            t = 0.12
            rr = r * (1 - t) * 1.0
            part = 'LightsA' if i % 2 else 'LightsB'
            m.box(part, (rr * math.cos(a), y + h * t, rr * math.sin(a)), (0.05, 0.05, 0.05), 'wi_warm' if i % 4 < 2 else 'wi_lamp')
    m.face('Body', [(0, 2.85, 0.015), (0.06, 2.68, 0.015), (0.16, 2.68, 0.015), (0.08, 2.6, 0.015), (0.11, 2.48, 0.015),
                    (0, 2.56, 0.015), (-0.11, 2.48, 0.015), (-0.08, 2.6, 0.015), (-0.16, 2.68, 0.015), (-0.06, 2.68, 0.015)], 'wi_gold', (0, 2.66, -1))
    m.face('Body', [(0, 2.85, -0.015), (0.06, 2.68, -0.015), (0.16, 2.68, -0.015), (0.08, 2.6, -0.015), (0.11, 2.48, -0.015),
                    (0, 2.56, -0.015), (-0.11, 2.48, -0.015), (-0.08, 2.6, -0.015), (-0.16, 2.68, -0.015), (-0.06, 2.68, -0.015)], 'wi_gold', (0, 2.66, 1))
    for (x, z, s, mat) in ((0.55, 0.45, 0.26, 'wi_redpaint'), (-0.5, 0.5, 0.22, 'wi_blue'), (0.45, -0.55, 0.2, 'wi_gold')):
        m.box('Body', (x, 0.04 + s / 2, z), (s, s, s), mat, yaw=20)
        m.box('Body', (x, 0.04 + s / 2, z), (s + 0.01, s + 0.01, 0.04), 'wi_white', yaw=20)
    return m

# ---------------- установка ----------------

SPRITE = '''fileFormatVersion: 2
guid: {GUID}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    wrapU: 1
    wrapV: 1
  maxTextureSize: 512
  spriteMode: 1
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  userData:
  assetBundleName:
  assetBundleVariant:
'''
NATIVE = 'fileFormatVersion: 2\nguid: {GUID}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
TEXT = 'fileFormatVersion: 2\nguid: {GUID}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
FOLDER = 'fileFormatVersion: 2\nguid: {GUID}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
DEFAULT = 'fileFormatVersion: 2\nguid: {GUID}\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'


def w(path, text):
    io.open(path, 'w', encoding='utf-8', newline='\n').write(text)


def ensure_meta(path, body):
    meta = path + '.meta'
    if os.path.exists(meta):
        return re.search(r'guid: (\w+)', io.open(meta, encoding='utf-8').read()).group(1)
    g = uuid.uuid4().hex
    w(meta, body.replace('{GUID}', g))
    return g


def ensure_dir(path):
    if not os.path.isdir(path):
        os.makedirs(path)
    ensure_meta(path.rstrip('\\/'), FOLDER)


def bounds(m):
    pts = np.array([p for faces in m.objects.values() for verts, _m, _u in faces for p in verts])
    return pts.min(0), pts.max(0)


def install(root, only=None):
    A = os.path.join(root, 'Assets')
    res_models = os.path.join(A, 'Resources', 'Models')
    out_models = os.path.join(res_models, 'Decor')
    mat_dir = os.path.join(res_models, 'Materials')
    decor_res = os.path.join(A, 'Resources', 'Decor')
    icons = os.path.join(decor_res, 'Icons')
    for d in (out_models, decor_res, icons):
        ensure_dir(d)

    # 1. новые материалы палитры (шаблон — wi_teal.mat; старые не трогаем)
    tpl = io.open(os.path.join(mat_dir, 'wi_teal.mat'), encoding='utf-8').read()
    for name, (col, emis, gloss) in NEW_MATS.items():
        path = os.path.join(mat_dir, name + '.mat')
        t = re.sub(r'm_Name: .*', 'm_Name: ' + name, tpl, count=1)
        t = re.sub(r'    - _Color: \{.*\}', '    - _Color: {r: %.4f, g: %.4f, b: %.4f, a: 1}' % col, t)
        t = re.sub(r'    - _Glossiness: .*', '    - _Glossiness: %.2f' % gloss, t)
        e = tuple(v * 1.6 for v in col) if emis else (0, 0, 0)
        t = re.sub(r'    - _EmissionColor: \{.*\}', '    - _EmissionColor: {r: %.4f, g: %.4f, b: %.4f, a: 1}' % e, t)
        t = re.sub(r'  m_LightmapFlags: \d+', '  m_LightmapFlags: %d' % (2 if emis else 4), t)
        if emis and '_EMISSION' not in t:
            t = t.replace('  m_ValidKeywords: []', '  m_ValidKeywords:\n  - _EMISSION')
        ensure_meta(path, NATIVE)
        w(path, t)
    wi_palette.install(mat_dir)
    mat_guids = {}
    for f in os.listdir(mat_dir):
        if f.endswith('.mat.meta'):
            mat_guids[f[:-9]] = re.search(r'guid: (\w+)', io.open(os.path.join(mat_dir, f), encoding='utf-8').read()).group(1)

    # 2. модели, части, иконки
    write_mtl(os.path.join(out_models, 'wi.mtl'))
    ensure_meta(os.path.join(out_models, 'wi.mtl'), DEFAULT)
    obj_tpl = io.open(os.path.join(res_models, 'smelter.obj.meta'), encoding='utf-8').read()
    manifest = []
    import copy
    for name, fn in DECOR.items():
        if only and name not in only:
            continue
        m = fn()
        full = copy.deepcopy(m)
        lo, hi = bounds(full)
        render([(full, (0, 0, 0), 0)], os.path.join(icons, name + '.png'), size=256, yaw=ICON_YAW[name], pitch=28)
        ensure_meta(os.path.join(icons, name + '.png'), SPRITE)
        entry = {'id': name, 'model': 'Decor/' + name, 'height': float(round(hi[1], 3)),
                 'width': float(round(hi[0] - lo[0], 3)), 'depth': float(round(hi[2] - lo[2], 3)),
                 'parts': [], 'lights': [], 'line': bool(EXTRA[name].get('line')), 'floor': bool(EXTRA[name].get('floor')),
                 'blink': float(EXTRA[name].get('blink', 0)), 'tintPart': EXTRA[name].get('tint_part', ''),
                 'fire': list(EXTRA[name].get('fire', ())), 'smoke': list(EXTRA[name].get('smoke', ())),
                 'splash': list(EXTRA[name].get('splash', ())), 'text': None}
        outputs = [(name, m)]
        for obj, pivot in PARTS[name]:
            part_name = name + '_' + obj.lower()
            outputs.append((part_name, m.split(obj, pivot)))
            entry['parts'].append({'name': obj, 'model': 'Decor/' + part_name, 'pos': list(pivot)})
        for pos, col, rng in LIGHTS[name]:
            entry['lights'].append({'pos': list(pos), 'color': list(col), 'range': rng})
        t = EXTRA[name].get('text')
        if t:
            entry['text'] = {'pos': list(t['pos']), 'size': t['size'], 'color': list(t['color']), 'kind': t['kind']}
        for out_name, model in outputs:
            p = os.path.join(out_models, out_name + '.obj')
            model.write_obj(p, keep=('Lamp',))     # Lamp — мигалка крана (Decoration.NamedRenderers)
            meta = p + '.meta'
            if os.path.exists(meta):
                mt = io.open(meta, encoding='utf-8').read()
            else:
                mt = re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, obj_tpl, count=1)
            used = sorted(set(re.findall(r'^usemtl (\S+)', io.open(p, encoding='utf-8').read(), re.M)))
            ext = '  externalObjects:\n' + ''.join(
                '  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n      name: %s\n'
                '    second: {fileID: 2100000, guid: %s, type: 2}\n' % (n, mat_guids[n]) for n in used if n in mat_guids)
            mt = re.sub(r'  externalObjects:.*?\n(?=  materials:)', ext, mt, count=1, flags=re.S)
            w(meta, mt)
        manifest.append(entry)
    if not only:
        man = os.path.join(decor_res, 'decor_manifest.json')
        w(man, json.dumps({'items': manifest}, ensure_ascii=False, indent=1))
        ensure_meta(man, TEXT)
    print('decor:', len(manifest))


if __name__ == '__main__':
    install(sys.argv[1], set(sys.argv[2:]) or None)
