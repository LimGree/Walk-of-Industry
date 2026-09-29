# Модели зданий Walk of Industry. Запуск: python wi_models.py <Resources/Models> <icons dir> <copy dir> [batch...]
import math, os, sys, shutil
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wi_lib import Model, write_mtl, render

MODELS = {}   # имя -> функция
ICONS = {}    # id здания -> (список (модель, offset, yaw), ракурс)
BATCH = {}    # имя -> номер партии


def reg(name, batch):
    def deco(fn):
        MODELS[name] = fn
        BATCH[name] = batch
        return fn
    return deco


def plinth(m, sx, sz, h=0.08):
    m.box('Body', (0, h / 2, 0), (sx - 0.04, h, sz - 0.04), 'wi_base')
    return h


def lamp(m, c, s=0.06):
    m.box('Lamp', c, (s, s, s), 'wi_lamp')


def stripes(m, obj, c, length, axis='x', n=4, h=0.05):
    # жёлто-чёрная полоса
    step = length / n
    for i in range(n):
        off = -length / 2 + step * (i + 0.5)
        pos = (c[0] + off, c[1], c[2]) if axis == 'x' else (c[0], c[1], c[2] + off)
        size = (step, h, 0.012) if axis == 'x' else (0.012, h, step)
        m.box(obj, pos, size, 'wi_yellow' if i % 2 == 0 else 'wi_dark')

# ---------------- ПОРТЫ (ставятся кодом на сторону сокета; наружу = +Z, стена в z=0) ----------------

@reg('port_in', 1)
def port_in():
    m = Model()
    m.box('Port', (0, 0.26, -0.05), (0.34, 0.28, 0.10), 'wi_dark')
    m.box('Port', (0, 0.42, -0.03), (0.42, 0.05, 0.08), 'wi_copper')
    m.box('Port', (-0.19, 0.26, -0.03), (0.05, 0.32, 0.08), 'wi_copper')
    m.box('Port', (0.19, 0.26, -0.03), (0.05, 0.32, 0.08), 'wi_copper')
    m.box('Port', (0, 0.10, -0.03), (0.42, 0.04, 0.08), 'wi_copper')
    m.chevron('Port', (0, 0.465, -0.03), 180, 'wi_yellow', size=0.16, w=0.04)
    return m


@reg('port_out', 1)
def port_out():
    m = Model()
    m.box('Port', (0, 0.26, -0.05), (0.34, 0.28, 0.10), 'wi_dark')
    m.box('Port', (0, 0.42, -0.03), (0.42, 0.05, 0.08), 'wi_steel')
    m.box('Port', (-0.19, 0.26, -0.03), (0.05, 0.32, 0.08), 'wi_steel')
    m.box('Port', (0.19, 0.26, -0.03), (0.05, 0.32, 0.08), 'wi_steel')
    m.box('Port', (0, 0.12, -0.02), (0.34, 0.03, 0.12), 'wi_steel')
    m.chevron('Port', (0, 0.465, -0.03), 0, 'wi_lamp', size=0.16, w=0.04)
    return m


@reg('port_fluid', 1)
def port_fluid():
    m = Model()
    m.tube('Port', (0, 0.28, -0.12), (0, 0.28, 0.0), 0.10, 'wi_steel', 8)
    m.tube('Port', (0, 0.28, -0.03), (0, 0.28, 0.0), 0.14, 'wi_copper', 8)
    return m

# ---------------- ПЕЧЬ 1×1 ----------------

@reg('smelter', 1)
def smelter():
    m = Model()
    y = plinth(m, 1, 1)
    m.box('Body', (0, y + 0.26, -0.02), (0.80, 0.52, 0.74), 'wi_brick')
    for i in range(3):
        m.box('Body', (0, y + 0.08 + i * 0.17, 0.36), (0.82, 0.02, 0.02), 'wi_stone')
    m.box('Body', (0, y + 0.55, -0.02), (0.86, 0.06, 0.80), 'wi_stone')
    m.roof('Body', (0, y + 0.58, -0.02), 0.78, 0.72, 0.2, 'wi_copper', over=0.02)
    # топка с огнём сбоку (+X)
    m.box('Body', (0.405, y + 0.2, 0.0), (0.02, 0.2, 0.3), 'wi_fire')
    m.box('Body', (0.415, y + 0.33, 0.0), (0.04, 0.05, 0.38), 'wi_dark')
    m.box('Body', (0.415, y + 0.07, 0.0), (0.04, 0.05, 0.38), 'wi_dark')
    # труба
    m.cyl('Body', (-0.25, y + 0.5, -0.22), 0.1, 0.62, 'wi_steel', 8)
    m.cyl('Body', (-0.25, y + 1.12, -0.22), 0.13, 0.06, 'wi_copper', 8)
    m.cyl('Body', (-0.25, y + 1.18, -0.22), 0.08, 0.02, 'wi_dark', 8)
    m.box('Body', (0.2, y + 0.7, -0.15), (0.2, 0.12, 0.2), 'wi_dark')
    lamp(m, (0.3, y + 0.46, 0.36))
    return m

# ---------------- СБОРЩИК 1×1, ур. 1 и 2 ----------------

def assembler_body(level):
    m = Model()
    y = plinth(m, 1, 1)
    h = 0.5 if level == 1 else 0.62
    m.box('Body', (0, y + h / 2, 0), (0.82, h, 0.82), 'wi_teal')
    m.box('Body', (0, y + h + 0.03, 0), (0.86, 0.06, 0.86), 'wi_copper')
    m.box('Body', (0.412, y + h * 0.55, 0.08), (0.01, 0.2, 0.36), 'wi_glass')
    # портал с кареткой
    for sx in (-1, 1):
        m.box('Body', (sx * 0.33, y + h + 0.2, 0), (0.06, 0.34, 0.06), 'wi_dark')
    m.box('Body', (0, y + h + 0.38, 0), (0.72, 0.06, 0.08), 'wi_dark')
    m.box('Carriage', (0, y + h + 0.3, 0), (0.12, 0.12, 0.12), 'wi_copper')
    m.box('Carriage', (0, y + h + 0.17, 0), (0.03, 0.14, 0.03), 'wi_steel')
    m.box('Carriage', (0, y + h + 0.095, 0), (0.07, 0.02, 0.07), 'wi_dark')
    lamp(m, (-0.3, y + h + 0.08, 0.3))
    if level >= 2:
        stripes(m, 'Body', (0, y + 0.12, 0.415), 0.8, 'x', 6, 0.06)
        for sx in (-1, 1):
            for sz in (-1, 1):
                m.box('Body', (sx * 0.41, y + h / 2, sz * 0.41), (0.05, h, 0.05), 'wi_copper')
        m.box('Body', (0, y + h + 0.38, 0.25), (0.72, 0.06, 0.08), 'wi_dark')
        m.box('Carriage2', (0, y + h + 0.3, 0.25), (0.12, 0.12, 0.12), 'wi_copper')
        m.box('Carriage2', (0, y + h + 0.17, 0.25), (0.03, 0.14, 0.03), 'wi_steel')
        m.box('Carriage2', (0, y + h + 0.095, 0.25), (0.07, 0.02, 0.07), 'wi_dark')
        m.tube('Body', (-0.3, y + h + 0.06, -0.3), (-0.3, y + h + 0.5, -0.3), 0.015, 'wi_steel', 6)
        lamp(m, (-0.3, y + h + 0.52, -0.3), 0.05)
    return m


@reg('assembler_1', 1)
def assembler_1():
    return assembler_body(1)


@reg('assembler_2', 1)
def assembler_2():
    return assembler_body(2)

# ---------------- ЭКСТРАКТОР 1×1, ур. 1 и 2 ----------------

def extractor_body(level):
    m = Model()
    y = plinth(m, 1, 1)
    m.cyl('Body', (0, y, 0), 0.36, 0.08, 'wi_dark', 8)
    m.cyl('Drill', (0, y - 0.05, 0), 0.07, 0.95, 'wi_steel', 6)
    for k, yy in enumerate((0.1, 0.2, 0.62, 0.75)):
        m.box('Drill', (0, y + yy, 0), (0.22, 0.03, 0.05), 'wi_copper', yaw=45 * k)
    m.cyl('Drill', (0, y - 0.12, 0), 0.1, 0.1, 'wi_dark', 6, r2=0.0)
    legs = 3 if level == 1 else 4
    top = y + (0.95 if level == 1 else 1.15)
    for i in range(legs):
        a = 2 * math.pi * i / legs + math.pi / 4
        foot = (0.38 * math.cos(a), y, 0.38 * math.sin(a))
        m.beam('Body', foot, (0.06 * math.cos(a), top, 0.06 * math.sin(a)), 0.05, 0.05, 'wi_teal')
    m.box('Body', (0, top, 0), (0.28, 0.08, 0.28), 'wi_copper')
    m.box('Body', (0, y + 0.42, 0), (0.34, 0.26, 0.34), 'wi_teal')
    m.box('Body', (0, y + 0.58, 0), (0.24, 0.06, 0.24), 'wi_copper')
    m.box('Body', (0.2, y + 0.42, 0), (0.06, 0.18, 0.18), 'wi_dark')
    lamp(m, (0, top + 0.07, 0), 0.06)
    if level >= 2:
        m.cyl('Body', (0, y + 0.12, 0), 0.3, 0.05, 'wi_copper', 8)
        m.box('Body', (-0.2, y + 0.42, 0), (0.06, 0.18, 0.18), 'wi_dark')
        m.box('Body', (0, y + 0.8, 0), (0.22, 0.12, 0.22), 'wi_teal2')
        stripes(m, 'Body', (0, y + 0.3, 0.172), 0.34, 'x', 4, 0.04)
        m.tube('Body', (0.12, top + 0.04, 0.12), (0.12, top + 0.3, 0.12), 0.015, 'wi_steel', 6)
        lamp(m, (0.12, top + 0.32, 0.12), 0.045)
    return m


@reg('extractor_1', 1)
def extractor_1():
    return extractor_body(1)


@reg('extractor_2', 1)
def extractor_2():
    return extractor_body(2)

# ---------------- СКЛАД 1×1 ----------------

@reg('storage_container', 1)
def storage_container():
    m = Model()
    y = plinth(m, 1, 1)
    m.box('Body', (0, y + 0.36, 0), (0.8, 0.72, 0.8), 'wi_teal')
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.box('Body', (sx * 0.4, y + 0.36, sz * 0.4), (0.06, 0.74, 0.06), 'wi_copper')
    for yy in (0.18, 0.54):
        m.box('Body', (0, y + yy, 0), (0.82, 0.04, 0.82), 'wi_copper')
    m.box('Body', (0, y + 0.75, 0), (0.84, 0.05, 0.84), 'wi_dark')
    m.box('Body', (0, y + 0.8, 0), (0.5, 0.05, 0.5), 'wi_steel')
    m.box('Body', (0.405, y + 0.36, 0), (0.01, 0.12, 0.3), 'wi_yellow')
    m.box('Body', (-0.405, y + 0.36, 0), (0.01, 0.12, 0.3), 'wi_yellow')
    lamp(m, (0.25, y + 0.85, 0.25), 0.05)
    return m

# ---------------- ДРОНЫ (партия 0) ----------------

PAD_Y = 0.62
LOAD_PADS = [(-1.2, 0.3), (1.2, 0.3), (-1.2, 1.75), (1.2, 1.75)]
UNLOAD_PADS = [(-1.2, -1.75), (1.2, -1.75), (-1.2, -0.3), (1.2, -0.3)]


@reg('drone', 0)
def drone():
    m = Model()
    m.box('Body', (0, 0.36, 0), (0.62, 0.24, 0.82), 'wi_teal')
    m.box('Body', (0, 0.52, -0.02), (0.44, 0.08, 0.56), 'wi_copper')
    m.box('Body', (0, 0.40, 0.42), (0.34, 0.10, 0.06), 'wi_glass')
    m.box('Lamp', (0, 0.58, 0.18), (0.10, 0.05, 0.10), 'wi_lamp')
    for sx in (-1, 1):
        for sz in (-1, 1):
            yaw = 45 if sx * sz > 0 else -45
            m.box('Body', (sx * 0.32, 0.40, sz * 0.32), (0.10, 0.07, 0.72), 'wi_dark', yaw)
            m.cyl('Body', (sx * 0.56, 0.36, sz * 0.56), 0.09, 0.14, 'wi_steel', 8)
            m.cyl('Body', (sx * 0.56, 0.50, sz * 0.56), 0.035, 0.05, 'wi_dark', 6)
    for sx in (-1, 1):
        m.box('Body', (sx * 0.22, 0.13, 0.18), (0.05, 0.22, 0.05), 'wi_dark')
        m.box('Body', (sx * 0.22, 0.13, -0.18), (0.05, 0.22, 0.05), 'wi_dark')
        m.box('Body', (sx * 0.22, 0.03, 0), (0.06, 0.05, 0.72), 'wi_dark')
    m.box('Crate', (0, 0.14, 0), (0.40, 0.26, 0.46), 'wi_crate')
    m.box('Crate', (0, 0.14, 0), (0.42, 0.05, 0.48), 'wi_copper')
    m.box('Crate', (0, 0.14, 0), (0.06, 0.28, 0.48), 'wi_copper')
    return m


DRONE_PROPS = ((-0.56, 0.565, 0.56), (0.56, 0.565, 0.56), (-0.56, 0.565, -0.56), (0.56, 0.565, -0.56))


@reg('drone_prop', 0)
def drone_prop():
    # винт с центром на оси: дрон ставит 4 штуки и крутит сам (Drone.SpinProps)
    m = Model()
    m.box('Body', (0, 0, 0), (0.62, 0.018, 0.07), 'wi_dark', 20)
    m.box('Body', (0, 0, 0), (0.62, 0.018, 0.07), 'wi_dark', 110)
    m.cyl('Body', (0, -0.01, 0), 0.04, 0.03, 'wi_copper', 6)
    return m


def pad(m, cx, cz):
    m.box('Base', (cx, 0.56, cz), (1.5, 0.12, 1.5), 'wi_steel')
    m.box('Base', (cx, PAD_Y + 0.005, cz), (1.24, 0.01, 1.24), 'wi_dark')
    m.box('Base', (cx - 0.3, PAD_Y + 0.012, cz), (0.12, 0.01, 0.8), 'wi_yellow')
    m.box('Base', (cx + 0.3, PAD_Y + 0.012, cz), (0.12, 0.01, 0.8), 'wi_yellow')
    m.box('Base', (cx, PAD_Y + 0.012, cz), (0.6, 0.01, 0.12), 'wi_yellow')
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.box('PadLamps', (cx + sx * 0.66, PAD_Y + 0.04, cz + sz * 0.66), (0.08, 0.06, 0.08), 'wi_lamp')


def deck(m, z0, z1):
    zc, zs = (z0 + z1) / 2, (z1 - z0)
    m.box('Base', (0, 0.25, zc), (4.9, 0.5, zs), 'wi_steel')
    for sx in (-1, 1):
        m.box('Base', (sx * 2.42, 0.62, zc), (0.06, 0.24, zs), 'wi_yellow')


@reg('drone_load_station', 0)
def load_station():
    m = Model()
    m.box('Base', (0, 0.08, 0), (5.0, 0.16, 5.0), 'wi_base')
    m.box('Packer', (0, 1.0, -1.5), (4.8, 1.7, 1.9), 'wi_teal')
    m.roof('Packer', (0, 1.85, -1.5), 4.8, 1.9, 0.7, 'wi_copper', over=0.12)
    for i in range(5):
        x = -2.0 + i
        m.box('Packer', (x, 0.45, -2.47), (0.7, 0.5, 0.08), 'wi_dark')
        m.box('Packer', (x, 0.74, -2.52), (0.8, 0.08, 0.14), 'wi_copper')
    m.box('Packer', (1.6, 2.7, -1.6), (0.35, 0.9, 0.35), 'wi_dark')
    m.box('Packer', (1.6, 3.18, -1.6), (0.45, 0.08, 0.45), 'wi_copper')
    m.box('Packer', (-1.2, 1.3, -0.54), (1.0, 0.5, 0.04), 'wi_glass')
    m.box('Lamp', (0.0, 1.55, -0.53), (0.18, 0.18, 0.06), 'wi_lamp')
    deck(m, -0.55, 2.45)
    for cx, cz in LOAD_PADS:
        pad(m, cx, cz)
    m.box('Packer', (0, 0.72, -0.35), (3.6, 0.1, 0.4), 'wi_dark')
    for i in range(3):
        m.box('Packer', (-1.0 + i, 0.93, -0.35), (0.34, 0.32, 0.34), 'wi_crate')
    return m


@reg('drone_unload_station', 0)
def unload_station():
    m = Model()
    m.box('Base', (0, 0.08, 0), (5.0, 0.16, 5.0), 'wi_base')
    deck(m, -2.45, 0.55)
    for cx, cz in UNLOAD_PADS:
        pad(m, cx, cz)
    m.box('Store', (0, 1.0, 1.5), (4.8, 1.7, 1.9), 'wi_teal')
    m.roof('Store', (0, 1.85, 1.5), 4.8, 1.9, 0.6, 'wi_copper', over=0.12)
    for sx in (-1, 1):
        m.cyl('Store', (sx * 1.55, 1.85, 1.5), 0.55, 1.3, 'wi_steel', 8)
        m.cyl('Store', (sx * 1.55, 3.15, 1.5), 0.62, 0.45, 'wi_copper', 8, r2=0.0)
    for i in range(5):
        x = -2.0 + i
        m.box('Store', (x, 0.45, 2.47), (0.7, 0.5, 0.08), 'wi_dark')
        m.box('Store', (x, 0.74, 2.52), (0.8, 0.08, 0.14), 'wi_copper')
    m.box('Store', (0.0, 1.3, 0.54), (1.4, 0.9, 0.04), 'wi_dark')
    m.box('Lamp', (0.0, 1.95, 0.53), (0.18, 0.18, 0.06), 'wi_lamp')
    for i in range(3):
        m.box('Store', (-1.0 + i, 0.93, 0.35), (0.34, 0.32, 0.34), 'wi_crate')
    m.box('Store', (0, 0.72, 0.35), (3.6, 0.1, 0.4), 'wi_dark')
    return m


# ---------------- ЛЕНТЫ (оси ленты: выход +Z; вход «left» = −X, «right» = +X) ----------------
SY0, SY1 = 0.13, 0.18        # полотно
ARR_Y = SY1 + 0.006          # шевроны
RAIL_X = 0.44


def travel_deg(dx, dz):
    return math.degrees(math.atan2(dx, dz))


def rail_z(m, x, z0=-0.5, z1=0.5):
    zc, zs = (z0 + z1) / 2, z1 - z0
    m.box('Frame', (x, 0.155, zc), (0.07, 0.19, zs), 'wi_edge')
    m.box('Frame', (x, 0.26, zc), (0.08, 0.025, zs), 'wi_copper')


def rail_x(m, z, x0=-0.5, x1=0.5):
    xc, xs = (x0 + x1) / 2, x1 - x0
    m.box('Frame', (xc, 0.155, z), (xs, 0.19, 0.07), 'wi_edge')
    m.box('Frame', (xc, 0.26, z), (xs, 0.025, 0.08), 'wi_copper')


def feet(m, xs=(-RAIL_X, RAIL_X), zs=(-0.3, 0.3)):
    for x in xs:
        for z in zs:
            m.box('Frame', (x, 0.03, z), (0.1, 0.06, 0.12), 'wi_dark')


def surface(m, x0, x1, z0, z1):
    m.box('Belt', ((x0 + x1) / 2, (SY0 + SY1) / 2, (z0 + z1) / 2), (x1 - x0, SY1 - SY0, z1 - z0), 'wi_rubber', scroll_top=True)
    m.box('Belt', ((x0 + x1) / 2, SY0 - 0.03, (z0 + z1) / 2), (x1 - x0 - 0.04, 0.06, z1 - z0 - 0.04), 'wi_dark')


def arrows(m, pts):
    for (x, z, deg) in pts:
        m.chevron('Arrows', (x, ARR_Y, z), deg, 'wi_yellow', size=0.26, w=0.06, th=0.012)


@reg('belt_straight', 2)
def belt_straight():
    m = Model()
    surface(m, -0.405, 0.405, -0.5, 0.5)
    rail_z(m, -RAIL_X)
    rail_z(m, RAIL_X)
    for z in (-0.46, 0.46):
        m.tube('Frame', (-0.4, SY0 + 0.01, z), (0.4, SY0 + 0.01, z), 0.035, 'wi_steel', 6)
    feet(m)
    arrows(m, [(0, -0.22, 0), (0, 0.22, 0)])
    return m


def belt_corner_r():
    # вход справа (+X), движение −X, поворот на выход +Z; центр дуги (0.5, 0.5)
    m = Model()
    c = (0.5, 0.5)
    m.arc_strip('Belt', c, 0.095, 0.905, 270, 180, SY0, SY1, 'wi_rubber', seg=8, scroll=True)
    m.arc_strip('Belt', c, 0.12, 0.88, 180, 270, SY0 - 0.06, SY0, 'wi_dark', seg=8)
    m.arc_strip('Frame', c, 0.905, 0.975, 180, 270, 0.06, 0.25, 'wi_edge', seg=8)
    m.arc_strip('Frame', c, 0.9, 0.98, 180, 270, 0.25, 0.275, 'wi_copper', seg=8)
    m.box('Frame', (0.47, 0.155, 0.47), (0.06, 0.19, 0.06), 'wi_edge')
    m.box('Frame', (-0.3, 0.03, -0.3), (0.12, 0.06, 0.12), 'wi_dark')
    m.box('Frame', (0.3, 0.03, -0.35), (0.12, 0.06, 0.1), 'wi_dark')
    m.box('Frame', (-0.35, 0.03, 0.3), (0.1, 0.06, 0.12), 'wi_dark')
    pts = []
    for a in (247.5, 202.5):
        t = math.radians(a)
        x, z = c[0] + 0.5 * math.cos(t), c[1] + 0.5 * math.sin(t)
        # движение по убыванию угла (от 270° к 180°)
        dx, dz = math.sin(t), -math.cos(t)
        pts.append((x, z, travel_deg(dx, dz)))
    arrows(m, pts)
    return m


@reg('belt_corner_r', 2)
def _bcr():
    return belt_corner_r()


@reg('belt_corner_l', 2)
def _bcl():
    return belt_corner_r().mirror_x()


def belt_tee_r():
    m = Model()
    surface(m, -0.405, 0.5, -0.5, 0.5)
    rail_z(m, -RAIL_X)
    m.box('Frame', (0.47, 0.155, -0.47), (0.06, 0.19, 0.06), 'wi_edge')
    m.box('Frame', (0.47, 0.155, 0.47), (0.06, 0.19, 0.06), 'wi_edge')
    feet(m)
    arrows(m, [(-0.08, -0.24, 0), (-0.08, 0.24, 0), (0.3, 0.0, 270)])
    return m


@reg('belt_tee_r', 2)
def _btr():
    return belt_tee_r()


@reg('belt_tee_l', 2)
def _btl():
    return belt_tee_r().mirror_x()


@reg('belt_sides', 2)
def belt_sides():
    m = Model()
    surface(m, -0.5, 0.5, -0.405, 0.5)
    rail_x(m, -RAIL_X)
    for x in (-0.47, 0.47):
        m.box('Frame', (x, 0.155, 0.47), (0.06, 0.19, 0.06), 'wi_edge')
    feet(m)
    arrows(m, [(-0.28, -0.06, 90), (0.28, -0.06, 270), (0, 0.28, 0)])
    return m


@reg('belt_triple', 2)
def belt_triple():
    m = Model()
    surface(m, -0.5, 0.5, -0.5, 0.5)
    for x in (-0.47, 0.47):
        for z in (-0.47, 0.47):
            m.box('Frame', (x, 0.155, z), (0.06, 0.19, 0.06), 'wi_edge')
    feet(m)
    arrows(m, [(0, -0.3, 0), (-0.3, -0.02, 90), (0.3, -0.02, 270), (0, 0.3, 0)])
    return m

# ---------------- ТРУБЫ ----------------
PY, PR = 0.3, 0.11


def pipe_run(m, p0, p1):
    m.tube('Pipe', p0, p1, PR, 'wi_steel', 10)


def flange(m, p, axis):
    x, y, z = p
    d = 0.022
    if axis == 'z':
        m.tube('Pipe', (x, y, z - d), (x, y, z + d), PR + 0.035, 'wi_copper', 10)
    else:
        m.tube('Pipe', (x - d, y, z), (x + d, y, z), PR + 0.035, 'wi_copper', 10)


def saddle(m, x, z):
    m.box('Pipe', (x, (PY - PR) / 2, z), (0.16, PY - PR, 0.1), 'wi_dark')


def pipe_arrow(m, x, z, deg):
    m.chevron('Arrows', (x, PY + PR + 0.012, z), deg, 'wi_yellow', size=0.16, w=0.04, th=0.012)


@reg('pipe_straight', 2)
def pipe_straight():
    m = Model()
    pipe_run(m, (0, PY, -0.5), (0, PY, 0.5))
    flange(m, (0, PY, -0.46), 'z')
    flange(m, (0, PY, 0.46), 'z')
    saddle(m, 0, 0)
    pipe_arrow(m, 0, 0, 0)
    return m


def pipe_corner_r():
    m = Model()
    c = (0.5, 0.5)
    seg = 6
    prev = None
    for i in range(seg + 1):
        t = math.radians(270 - 90 * i / seg)
        p = (c[0] + 0.5 * math.cos(t), PY, c[1] + 0.5 * math.sin(t))
        if prev is not None:
            m.tube('Pipe', prev, p, PR, 'wi_steel', 10)
        prev = p
    flange(m, (0.46, PY, 0), 'x')
    flange(m, (0, PY, 0.46), 'z')
    saddle(m, 0.12, 0.12)
    t = math.radians(225)
    x, z = c[0] + 0.5 * math.cos(t), c[1] + 0.5 * math.sin(t)
    pipe_arrow(m, x, z, travel_deg(math.sin(t), -math.cos(t)))
    return m


@reg('pipe_corner_r', 2)
def _pcr():
    return pipe_corner_r()


@reg('pipe_corner_l', 2)
def _pcl():
    return pipe_corner_r().mirror_x()


def pipe_junction(ins):
    m = Model()
    m.cyl('Pipe', (0, 0.1, 0), 0.18, 0.4, 'wi_teal', 8)
    m.cyl('Pipe', (0, 0.5, 0), 0.2, 0.04, 'wi_copper', 8)
    m.cyl('Pipe', (0, 0.0, 0), 0.2, 0.1, 'wi_dark', 8)
    pipe_run(m, (0, PY, 0.15), (0, PY, 0.5))
    flange(m, (0, PY, 0.46), 'z')
    pipe_arrow(m, 0, 0.33, 0)
    for side in ins:
        if side == 'back':
            pipe_run(m, (0, PY, -0.5), (0, PY, -0.15))
            flange(m, (0, PY, -0.46), 'z')
            pipe_arrow(m, 0, -0.33, 0)
        elif side == 'left':
            pipe_run(m, (-0.5, PY, 0), (-0.15, PY, 0))
            flange(m, (-0.46, PY, 0), 'x')
            pipe_arrow(m, -0.33, 0, 90)
        elif side == 'right':
            pipe_run(m, (0.15, PY, 0), (0.5, PY, 0))
            flange(m, (0.46, PY, 0), 'x')
            pipe_arrow(m, 0.33, 0, 270)
    return m


@reg('pipe_tee_r', 2)
def _ptr():
    return pipe_junction(['back', 'right'])


@reg('pipe_tee_l', 2)
def _ptl():
    return pipe_junction(['back', 'left'])


@reg('pipe_sides', 2)
def _ps():
    return pipe_junction(['left', 'right'])


@reg('pipe_triple', 2)
def _pt():
    return pipe_junction(['back', 'left', 'right'])


# ---------------- ТРУБНЫЙ СПЛИТТЕР 1×1 (вход −Z, выходы +Z, ±X) ----------------

@reg('pipe_splitter', 5)
def pipe_splitter():
    m = Model()
    m.box('Body', (0, 0.03, 0), (0.9, 0.06, 0.9), 'wi_base')
    m.box('Body', (0, PY, 0), (0.36, 0.36, 0.36), 'wi_teal')
    m.box('Body', (0, PY + 0.19, 0), (0.4, 0.03, 0.4), 'wi_copper')
    m.box('Body', (0, PY - 0.19, 0), (0.4, 0.03, 0.4), 'wi_copper')
    m.box('Body', (0, 0.06 + (PY - 0.2) / 2, 0), (0.2, PY - 0.2, 0.2), 'wi_dark')
    # вентиль-штурвал сверху
    m.cyl('Valve', (0, PY + 0.2, 0), 0.03, 0.14, 'wi_steel', 6)
    m.tube('Valve', (0, PY + 0.34, 0), (0, PY + 0.37, 0), 0.14, 'wi_red', 10)
    m.tube('Valve', (0, PY + 0.33, 0), (0, PY + 0.38, 0), 0.09, 'wi_steel', 10)
    m.box('Valve', (0, PY + 0.355, 0), (0.26, 0.02, 0.03), 'wi_red')
    # патрубки: вход сзади, три выхода
    pipe_run(m, (0, PY, -0.5), (0, PY, -0.18))
    flange(m, (0, PY, -0.46), 'z')
    pipe_arrow(m, 0, -0.33, 0)
    pipe_run(m, (0, PY, 0.18), (0, PY, 0.5))
    flange(m, (0, PY, 0.46), 'z')
    pipe_arrow(m, 0, 0.33, 0)
    pipe_run(m, (0.18, PY, 0), (0.5, PY, 0))
    flange(m, (0.46, PY, 0), 'x')
    pipe_arrow(m, 0.33, 0, 90)
    pipe_run(m, (-0.5, PY, 0), (-0.18, PY, 0))
    flange(m, (-0.46, PY, 0), 'x')
    pipe_arrow(m, -0.33, 0, 270)
    lamp(m, (0.15, PY + 0.22, 0.15), 0.04)
    return m

# ---------------- СПЛИТТЕР 1×1 (вход −Z, выходы +Z, ±X), ниже 0.29 — груз идёт поверху ----------------

@reg('splitter', 2)
def splitter():
    m = Model()
    m.box('Body', (0, 0.04, 0), (0.94, 0.08, 0.94), 'wi_base')
    m.box('Belt', (0, (SY0 + SY1) / 2, 0), (0.86, SY1 - SY0, 0.3), 'wi_rubber')
    m.box('Belt', (0, (SY0 + SY1) / 2, 0), (0.3, SY1 - SY0, 0.86), 'wi_rubber')
    m.cyl('Hub', (0, SY1, 0), 0.2, 0.05, 'wi_teal', 8)
    m.cyl('Hub', (0, SY1 + 0.05, 0), 0.12, 0.03, 'wi_copper', 8)
    m.box('Hub', (0, SY1 + 0.055, 0), (0.34, 0.02, 0.05), 'wi_dark')
    for x in (-0.39, 0.39):
        for z in (-0.39, 0.39):
            m.box('Frame', (x, 0.17, z), (0.14, 0.18, 0.14), 'wi_teal')
            m.box('Frame', (x, 0.27, z), (0.15, 0.025, 0.15), 'wi_copper')
    arrows(m, [(0, -0.34, 0), (0, 0.34, 0), (0.34, 0, 90), (-0.34, 0, 270)])
    lamp(m, (0.39, 0.3, 0.39), 0.04)
    return m

# ---------------- ПОДЗЕМКА 1×1 ----------------

def underground(exit_side):
    # exit_side=False: вход — лента сзади ныряет вперёд; True: выход — выныривает спереди
    m = Model()
    s = 1 if not exit_side else -1        # где стоит будка: +Z у входа, −Z у выхода
    m.box('Body', (0, 0.03, 0), (0.94, 0.06, 0.94), 'wi_base')
    m.box('Body', (0, 0.3, s * 0.24), (0.9, 0.5, 0.46), 'wi_teal')
    m.gable('Body', (0, 0.55, s * 0.24), 0.9, 0.46, 0.14, 'wi_copper', over=0.03)
    m.box('Body', (0, 0.2, s * 0.005), (0.62, 0.3, 0.03), 'wi_dark')
    m.box('Body', (0, 0.37, s * 0.0), (0.72, 0.05, 0.05), 'wi_copper')
    for x in (-0.33, 0.33):
        m.box('Body', (x, 0.2, 0), (0.05, 0.36, 0.05), 'wi_copper')
    stripes(m, 'Body', (0, 0.47, -s * 0.012), 0.8, 'x', 6, 0.05)
    # рампа
    z_open, z_mouth = -s * 0.5, 0.0
    y_open, y_mouth = SY1, 0.07
    mid = ((z_open + z_mouth) / 2)
    L = math.hypot(z_mouth - z_open, y_mouth - y_open)
    ang = math.degrees(math.atan2(y_open - y_mouth, abs(z_mouth - z_open)))
    rx = ang if not exit_side else -ang
    m.box('Belt', (0, (y_open + y_mouth) / 2 - 0.02, mid), (0.62, 0.04, L), 'wi_rubber', rx=rx)
    for x in (-0.36, 0.36):
        m.box('Frame', (x, 0.14, mid), (0.07, 0.2, 0.5), 'wi_teal')
        m.box('Frame', (x, 0.25, mid), (0.08, 0.025, 0.5), 'wi_copper')
    arrows(m, [(0, -s * 0.25, 0)])
    m.chevron('Arrows', (0, 0.62, s * 0.24), 0, 'wi_yellow', size=0.3, w=0.07)
    lamp(m, (0.36, 0.62, s * 0.4), 0.05)
    return m


@reg('underground_in', 2)
def _ui():
    return underground(False)


@reg('underground_out', 2)
def _uo():
    return underground(True)

# ---------------- РУКА 1×1 (берёт с −Z, кладёт на +Z) ----------------

@reg('robotic_arm', 2)
def robotic_arm():
    m = Model()
    plinth(m, 1, 1, 0.08)
    m.cyl('Body', (0, 0.08, 0), 0.3, 0.07, 'wi_copper', 10)
    m.cyl('Turret', (0, 0.15, 0), 0.12, 0.34, 'wi_teal', 8)
    m.box('Turret', (0, 0.52, 0), (0.2, 0.14, 0.2), 'wi_dark')
    sh, el, wr = (0, 0.55, 0.02), (0, 0.78, -0.2), (0, 0.5, -0.36)
    m.beam('Turret', sh, el, 0.09, 0.09, 'wi_teal')
    m.beam('Turret', el, wr, 0.07, 0.07, 'wi_teal2')
    m.cyl('Turret', (0, 0.74, -0.2), 0.06, 0.08, 'wi_copper', 8)
    m.box('Turret', (0, wr[1] - 0.02, wr[2]), (0.14, 0.05, 0.08), 'wi_dark')
    for x in (-0.05, 0.05):
        m.box('Turret', (x, wr[1] - 0.1, wr[2]), (0.025, 0.12, 0.05), 'wi_dark')
    m.chevron('Arrows', (0.3, 0.085, 0), 0, 'wi_yellow', size=0.2, w=0.05)
    m.chevron('Arrows', (-0.3, 0.085, 0), 0, 'wi_yellow', size=0.2, w=0.05)
    m.box('Turret', (0, 0.62, 0.08), (0.05, 0.05, 0.05), 'wi_lamp')
    return m


# ---------------- КОНСТРУКТОР 2×2 (цех с пилообразной крышей) ----------------

@reg('constructor', 3)
def constructor():
    m = Model()
    y = plinth(m, 2, 2)
    H = 0.85
    m.box('Body', (0, y + H / 2, 0), (1.76, H, 1.7), 'wi_teal')
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.box('Body', (sx * 0.88, y + H / 2, sz * 0.85), (0.08, H + 0.02, 0.08), 'wi_copper')
    m.box('Body', (0, y + H, 0), (1.84, 0.05, 1.78), 'wi_copper')
    # пилообразная крыша: 3 зуба вдоль Z, стекло смотрит назад
    teeth = 3
    dz = 1.7 / teeth
    for i in range(teeth):
        z0 = -0.85 + dz * i
        L = math.hypot(dz, 0.32)
        ang = math.degrees(math.atan2(0.32, dz))
        m.box('Roof', (0, y + H + 0.16, z0 + dz / 2), (1.74, 0.04, L), 'wi_copper', rx=ang)
        m.box('Roof', (0, y + H + 0.16, z0 + 0.02), (1.7, 0.32, 0.03), 'wi_glass')
    # ворота спереди и полосы
    m.box('Body', (0, y + 0.33, 0.86), (0.8, 0.62, 0.03), 'wi_dark')
    for i in range(5):
        m.box('Body', (0, y + 0.08 + i * 0.12, 0.875), (0.78, 0.02, 0.01), 'wi_steel')
    stripes(m, 'Body', (0, y + 0.72, 0.87), 0.9, 'x', 7, 0.06)
    # кран-балка над крышей
    for sx in (-1, 1):
        m.box('Crane', (sx * 0.8, y + H + 0.45, 0.55), (0.07, 0.5, 0.07), 'wi_dark')
    m.box('Crane', (0, y + H + 0.72, 0.55), (1.7, 0.08, 0.1), 'wi_yellow')
    m.box('Trolley', (0, y + H + 0.62, 0.55), (0.14, 0.12, 0.14), 'wi_dark')
    m.tube('Trolley', (0, y + H + 0.56, 0.55), (0, y + H + 0.3, 0.55), 0.01, 'wi_steel', 4)
    m.box('Trolley', (0, y + H + 0.27, 0.55), (0.08, 0.06, 0.08), 'wi_yellow')
    # труба
    m.cyl('Body', (-0.62, y + H, -0.55), 0.1, 0.7, 'wi_steel', 8)
    m.cyl('Body', (-0.62, y + H + 0.7, -0.55), 0.13, 0.05, 'wi_copper', 8)
    lamp(m, (0.6, y + H + 0.08, 0.7))
    lamp(m, (-0.6, y + H + 0.08, 0.7))
    return m

# ---------------- ХИМЗАВОД 3×3 ----------------

@reg('chemical_plant', 3)
def chemical_plant():
    m = Model()
    y = plinth(m, 3, 3)
    m.box('Body', (0, y + 0.04, 0), (2.8, 0.08, 2.8), 'wi_stone')
    # два бака с реагентами
    for z in (-0.65, 0.55):
        m.cyl('Tanks', (-0.75, y + 0.08, z), 0.42, 1.0, 'wi_chem', 10)
        m.cyl('Tanks', (-0.75, y + 1.08, z), 0.45, 0.25, 'wi_copper', 10, r2=0.12)
        for yy in (0.35, 0.75):
            m.cyl('Tanks', (-0.75, y + yy, z), 0.44, 0.04, 'wi_steel', 10)
    # реактор
    m.cyl('Reactor', (0.55, y + 0.08, -0.45), 0.42, 1.5, 'wi_teal', 10)
    m.cyl('Reactor', (0.55, y + 1.58, -0.45), 0.45, 0.35, 'wi_copper', 10, r2=0.1)
    m.cyl('Reactor', (0.55, y + 0.5, -0.45), 0.44, 0.06, 'wi_yellow', 10)
    m.cyl('Reactor', (0.55, y + 1.2, -0.45), 0.44, 0.06, 'wi_copper', 10)
    m.box('Reactor', (0.55, y + 0.9, -0.03), (0.2, 0.2, 0.04), 'wi_glass')
    # трубы между баками и реактором
    for z in (-0.65, 0.55):
        m.tube('Pipes', (-0.33, y + 0.9, z), (0.15, y + 0.9, z), 0.06, 'wi_steel', 6)
    m.tube('Pipes', (0.15, y + 0.9, 0.55), (0.15, y + 0.9, -0.45), 0.06, 'wi_steel', 6)
    m.tube('Pipes', (0.15, y + 0.9, -0.65), (0.15, y + 0.9, -0.45), 0.06, 'wi_steel', 6)
    m.tube('Pipes', (0.15, y + 0.9, -0.45), (0.13, y + 0.9, -0.45), 0.08, 'wi_copper', 6)
    # пульт
    m.box('Control', (0.75, y + 0.3, 0.75), (0.8, 0.6, 0.7), 'wi_teal2')
    m.box('Control', (0.75, y + 0.62, 0.75), (0.86, 0.05, 0.76), 'wi_copper')
    m.box('Control', (0.75, y + 0.38, 1.105), (0.5, 0.2, 0.01), 'wi_glass')
    stripes(m, 'Control', (0.75, y + 0.1, 1.105), 0.8, 'x', 6, 0.05)
    m.tube('Control', (1.0, y + 0.65, 0.6), (1.0, y + 1.2, 0.6), 0.015, 'wi_steel', 6)
    lamp(m, (1.0, y + 1.22, 0.6), 0.05)
    m.box('Beacon', (0.55, y + 1.95, -0.45), (0.07, 0.07, 0.07), 'wi_lamp')
    return m

# ---------------- НПЗ 3×3 ----------------

@reg('refinery', 3)
def refinery():
    m = Model()
    y = plinth(m, 3, 3)
    m.box('Body', (0, y + 0.04, 0), (2.8, 0.08, 2.8), 'wi_stone')
    # ректификационные колонны
    for (x, z, h, r) in ((-0.75, -0.7, 2.4, 0.3), (-0.05, -0.8, 1.9, 0.24)):
        m.cyl('Columns', (x, y + 0.08, z), r, h, 'wi_steel', 10)
        m.cyl('Columns', (x, y + 0.08 + h, z), r + 0.03, 0.22, 'wi_copper', 10, r2=0.05)
        k = 0.45
        while k < h:
            m.cyl('Columns', (x, y + 0.08 + k, z), r + 0.03, 0.04, 'wi_copper', 10)
            k += 0.45
        m.beam('Columns', (x + r + 0.05, y + 0.1, z), (x + r + 0.05, y + h, z), 0.03, 0.03, 'wi_yellow')
    # печь-нагреватель
    m.box('Heater', (0.65, y + 0.45, 0.45), (1.1, 0.9, 1.0), 'wi_brick')
    m.roof('Heater', (0.65, y + 0.9, 0.45), 1.1, 1.0, 0.25, 'wi_copper', over=0.03)
    m.box('Heater', (1.205, y + 0.3, 0.45), (0.02, 0.22, 0.5), 'wi_fire')
    m.cyl('Heater', (0.95, y + 1.0, 0.2), 0.09, 0.8, 'wi_steel', 8)
    # резервуар
    m.cyl('Tank', (-0.75, y + 0.08, 0.75), 0.5, 0.55, 'wi_white', 12)
    m.cyl('Tank', (-0.75, y + 0.63, 0.75), 0.52, 0.12, 'wi_white', 12, r2=0.3)
    m.cyl('Tank', (-0.75, y + 0.35, 0.75), 0.51, 0.05, 'wi_yellow', 12)
    # факел
    m.tube('Flare', (1.1, y + 0.08, -1.0), (1.1, y + 2.6, -1.0), 0.05, 'wi_dark', 6)
    m.cyl('Flare', (1.1, y + 2.6, -1.0), 0.08, 0.08, 'wi_steel', 6)
    m.cyl('Flame', (1.1, y + 2.68, -1.0), 0.06, 0.14, 'wi_fire', 6, r2=0.0)
    # трубопровод
    m.tube('Pipes', (-0.75, y + 1.2, -0.7), (0.65, y + 1.2, -0.7), 0.06, 'wi_steel', 6)
    m.tube('Pipes', (0.65, y + 1.2, -0.7), (0.65, y + 1.2, -0.05), 0.06, 'wi_steel', 6)
    m.tube('Pipes', (-0.05, y + 0.6, -0.8), (-0.05, y + 0.6, 0.75), 0.05, 'wi_steel', 6)
    m.tube('Pipes', (-0.05, y + 0.6, 0.75), (-0.3, y + 0.6, 0.75), 0.05, 'wi_steel', 6)
    lamp(m, (-0.75, y + 2.72, -0.7), 0.07)
    return m

# ---------------- ЛАБОРАТОРИЯ 1×1 (входы со всех сторон) ----------------

@reg('research_lab', 3)
def research_lab():
    m = Model()
    y = plinth(m, 1, 1)
    m.box('Body', (0, y + 0.27, 0), (0.78, 0.54, 0.78), 'wi_white')
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.box('Body', (sx * 0.39, y + 0.27, sz * 0.39), (0.06, 0.56, 0.06), 'wi_teal')
    m.box('Body', (0, y + 0.56, 0), (0.84, 0.05, 0.84), 'wi_teal')
    m.cyl('Dome', (0, y + 0.58, 0), 0.3, 0.14, 'wi_copper', 10)
    m.cyl('Dome', (0, y + 0.72, 0), 0.28, 0.22, 'wi_glass', 10, r2=0.1)
    m.cyl('Dome', (0, y + 0.94, 0), 0.1, 0.03, 'wi_copper', 10)
    # антенна-тарелка
    m.tube('Dish', (0.25, y + 0.58, -0.25), (0.25, y + 0.9, -0.25), 0.02, 'wi_steel', 6)
    m.tube('Dish', (0.2, y + 0.98, -0.2), (0.27, y + 0.93, -0.27), 0.14, 'wi_steel', 10, r2=0.02)
    m.cyl('Beacon', (0, y + 0.97, 0), 0.04, 0.12, 'wi_blue', 6)
    for sx, sz in ((0, 0.395), (0, -0.395), (0.395, 0), (-0.395, 0)):
        m.box('Body', (sx, y + 0.45, sz), (0.3 if sz else 0.01, 0.06, 0.01 if sz else 0.3), 'wi_blue')
    return m


# ---------------- НЕФТЕКАЧАЛКА 2×2 ----------------

@reg('oil_extractor', 4)
def oil_extractor():
    m = Model()
    y = plinth(m, 2, 2)
    m.box('Body', (0, y + 0.04, 0), (1.7, 0.08, 1.7), 'wi_stone')
    # устье скважины сзади
    m.cyl('Body', (0, y + 0.08, -0.62), 0.14, 0.3, 'wi_steel', 8)
    m.cyl('Body', (0, y + 0.38, -0.62), 0.18, 0.05, 'wi_copper', 8)
    m.tube('Body', (0, y + 0.3, -0.62), (0.55, y + 0.3, -0.62), 0.05, 'wi_steel', 6)
    # стойка A
    top = (0, y + 1.25, -0.05)
    for sx in (-1, 1):
        m.beam('Post', (sx * 0.3, y + 0.08, 0.15), top, 0.07, 0.07, 'wi_teal')
        m.beam('Post', (sx * 0.3, y + 0.08, -0.25), top, 0.07, 0.07, 'wi_teal')
    # балансир с «головой лошади»
    head, tail = (0, y + 1.3, -0.62), (0, y + 1.2, 0.55)
    m.beam('Beam', head, tail, 0.12, 0.14, 'wi_dark')
    m.box('Beam', (0, y + 1.18, -0.68), (0.14, 0.42, 0.18), 'wi_copper', rx=-12)
    m.tube('Beam', (0, y + 0.98, -0.72), (0, y + 0.38, -0.62), 0.015, 'wi_steel', 4)
    # кривошип, противовесы, редуктор
    m.box('Body', (0, y + 0.35, 0.55), (0.45, 0.5, 0.4), 'wi_teal2')
    for sx in (-1, 1):
        m.tube('Crank', (sx * 0.26, y + 0.55, 0.62), (sx * 0.3, y + 0.55, 0.62), 0.3, 'wi_copper', 10)
        m.beam('Crank', (sx * 0.28, y + 0.55, 0.62), (sx * 0.28, y + 0.83, 0.62), 0.05, 0.05, 'wi_steel')
    stripes(m, 'Body', (0, y + 0.2, 0.752), 0.44, 'x', 4, 0.06)
    # бочка нефти
    m.cyl('Body', (0.6, y + 0.08, -0.62), 0.2, 0.45, 'wi_dark', 8)
    m.cyl('Body', (0.6, y + 0.28, -0.62), 0.21, 0.04, 'wi_copper', 8)
    lamp(m, (0, y + 1.42, -0.05), 0.06)
    return m

# ---------------- ВОДОКАЧКА 2×2 (на воде) ----------------

@reg('water_extractor', 4)
def water_extractor():
    m = Model()
    # сваи и настил
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl('Pier', (sx * 0.78, -0.4, sz * 0.78), 0.07, 0.55, 'wi_base', 6)
    m.box('Pier', (0, 0.1, 0), (1.9, 0.1, 1.9), 'wi_crate')
    for i in range(6):
        m.box('Pier', (-0.8 + i * 0.32, 0.155, 0), (0.02, 0.01, 1.9), 'wi_base')
    # насосная
    m.box('House', (-0.25, 0.55, -0.1), (1.1, 0.8, 1.2), 'wi_teal')
    m.gable('House', (-0.25, 0.95, -0.1), 1.1, 1.2, 0.35, 'wi_copper', over=0.05)
    m.box('House', (-0.25, 0.45, 0.505), (0.36, 0.56, 0.02), 'wi_dark')
    m.box('House', (0.305, 0.65, -0.1), (0.02, 0.22, 0.5), 'wi_glass')
    # заборная труба в воду
    m.tube('Intake', (0.65, 0.9, -0.6), (0.65, -0.35, -0.6), 0.1, 'wi_steel', 8)
    m.tube('Intake', (0.65, 0.9, -0.6), (0.3, 0.9, -0.6), 0.1, 'wi_steel', 8)
    m.cyl('Intake', (0.65, 0.05, -0.6), 0.14, 0.05, 'wi_copper', 8)
    # бак чистой воды
    m.cyl('Tank', (0.6, 0.15, 0.45), 0.3, 0.6, 'wi_water', 10)
    m.cyl('Tank', (0.6, 0.75, 0.45), 0.32, 0.12, 'wi_copper', 10, r2=0.1)
    m.tube('Tank', (0.3, 0.4, 0.45), (0.05, 0.4, 0.45), 0.05, 'wi_steel', 6)
    lamp(m, (-0.25, 1.32, -0.1), 0.06)
    return m

# ---------------- БАК ДЛЯ ЖИДКОСТЕЙ 1×1 ----------------

@reg('fluid_storage_tank', 4)
def fluid_storage_tank():
    m = Model()
    y = plinth(m, 1, 1)
    m.cyl('Tank', (0, y, 0), 0.4, 0.95, 'wi_steel', 12)
    m.cyl('Tank', (0, y + 0.95, 0), 0.41, 0.16, 'wi_steel', 12, r2=0.18)
    for yy in (0.2, 0.5, 0.8):
        m.cyl('Tank', (0, y + yy, 0), 0.415, 0.04, 'wi_copper', 12)
    m.box('Tank', (0.405, y + 0.5, 0.0), (0.02, 0.6, 0.08), 'wi_water')
    for i in range(6):
        m.box('Ladder', (-0.12, y + 0.1 + i * 0.15, -0.415), (0.14, 0.02, 0.02), 'wi_yellow')
    for x in (-0.19, -0.05):
        m.box('Ladder', (x, y + 0.5, -0.415), (0.02, 0.95, 0.02), 'wi_yellow')
    m.cyl('Tank', (0, y + 1.1, 0), 0.06, 0.06, 'wi_copper', 8)
    lamp(m, (0, y + 1.17, 0), 0.05)
    return m

# ---------------- ГЕНЕРАТОР 1×1 (уголь сзади) ----------------

@reg('power_generator', 4)
def power_generator():
    m = Model()
    y = plinth(m, 1, 1)
    m.box('Body', (0, y + 0.25, -0.12), (0.8, 0.5, 0.52), 'wi_teal')
    m.box('Body', (0, y + 0.52, -0.12), (0.84, 0.05, 0.56), 'wi_copper')
    for i in range(5):
        m.box('Body', (-0.2 + i * 0.1, y + 0.28, 0.145), (0.05, 0.34, 0.02), 'wi_dark')
    # маховик/турбина
    m.tube('Turbine', (-0.4, y + 0.3, 0.3), (0.4, y + 0.3, 0.3), 0.2, 'wi_steel', 10)
    for x in (-0.3, 0.0, 0.3):
        m.tube('Turbine', (x - 0.02, y + 0.3, 0.3), (x + 0.02, y + 0.3, 0.3), 0.215, 'wi_copper', 10)
    for a in (0, 120, 240):
        t = math.radians(a)
        m.box('Turbine', (0, y + 0.3 + 0.2 * math.sin(t), 0.3 + 0.2 * math.cos(t)), (0.78, 0.04, 0.04), 'wi_dark')
    # катушки
    for x in (-0.2, 0.2):
        m.cyl('Coil', (x, y + 0.55, -0.2), 0.08, 0.2, 'wi_copper', 8)
        m.cyl('Coils', (x, y + 0.75, -0.2), 0.05, 0.05, 'wi_blue', 6)
    # выхлоп
    m.cyl('Body', (0.28, y + 0.55, 0.05), 0.07, 0.55, 'wi_steel', 8)
    m.cyl('Body', (0.28, y + 1.1, 0.05), 0.1, 0.05, 'wi_copper', 8)
    m.box('Body', (-0.3, y + 0.62, 0.05), (0.12, 0.14, 0.12), 'wi_yellow')
    lamp(m, (-0.3, y + 0.72, 0.05), 0.05)
    return m



# ---------------- ПОДВИЖНЫЕ ДЕТАЛИ (Animator) ----------------
# модель -> [(объект, ось/pivot, префаб, родитель в префабе, имя узла анимации)]
SPLITS = {
    'extractor_1': [('Drill', (0, 0.08, 0), 'Extractor_01.prefab', 'extractor_level_1', 'Anim_Drill')],
    'extractor_2': [('Drill', (0, 0.08, 0), 'Extractor_01.prefab', 'extractor_level_2', 'Anim_Drill')],
    'assembler_1': [('Carriage', (0, 0.88, 0), 'Assembler_01.prefab', 'Assembler_Level_1', 'Anim_Carriage')],
    'assembler_2': [('Carriage', (0, 1.0, 0), 'Assembler_01.prefab', 'Assembler_level_2', 'Anim_Carriage'),
                    ('Carriage2', (0, 1.0, 0.25), 'Assembler_01.prefab', 'Assembler_level_2', 'Anim_Carriage2')],
    'constructor': [('Trolley', (0, 1.55, 0.55), 'Constructor_01.prefab', 'WiVisual', 'Anim_Trolley')],
    'chemical_plant': [('Beacon', (0.55, 2.03, -0.45), 'chemical_plant.prefab', 'WiVisual', 'Anim_Beacon')],
    'refinery': [('Flame', (1.1, 2.76, -1.0), 'refinery.prefab', 'WiVisual', 'Anim_Flame')],
    'oil_extractor': [('Beam', (0, 1.33, -0.05), 'oil_extractor.prefab', 'WiVisual', 'Anim_Beam'),
                      ('Crank', (0, 0.63, 0.62), 'oil_extractor.prefab', 'WiVisual', 'Anim_Crank')],
    'power_generator': [('Turbine', (0, 0.38, 0.3), 'PowerGenerator.prefab', 'WiVisual', 'Anim_Turbine'),
                        ('Coils', (0, 0.855, -0.2), 'PowerGenerator.prefab', 'WiVisual', 'Anim_Coils')],
    'research_lab': [('Dish', (0.25, 0.66, -0.25), 'ResearchLab.prefab', 'WiVisual', 'Anim_Dish'),
                     ('Beacon', (0, 1.11, 0), 'ResearchLab.prefab', 'WiVisual', 'Anim_Beacon')],
    'splitter': [('Hub', (0, 0.18, 0), 'Splitter.prefab', 'WiVisual', 'Anim_Hub')],
    'pipe_splitter': [('Valve', (0, 0.5, 0), 'PipeSplitter.prefab', 'WiVisual', 'Anim_Valve')],
    'robotic_arm': [('Turret', (0, 0.15, 0), 'robotic_arm .prefab', 'WiVisual', 'Anim_Turret')],
}

# ---------------- ИКОНКИ ----------------
def icon_plan(built):
    P = {}
    port_in, port_out = built.get('port_in'), built.get('port_out')
    port_fluid = built.get('port_fluid')
    def with_ports(model, ins, outs, fluids=()):
        parts = [(model, (0, 0, 0), 0)]
        for (x, z, yaw) in ins:
            parts.append((port_in, (x, 0, z), yaw))
        for (x, z, yaw) in outs:
            parts.append((port_out, (x, 0, z), yaw))
        for (x, z, yaw) in fluids:
            parts.append((port_fluid, (x, 0, z), yaw))
        return parts
    if 'smelter' in built:
        P['smelter'] = (with_ports(built['smelter'], [(0, -0.5, 180)], [(0, 0.5, 0)]), 145)
    if 'assembler_1' in built:
        P['assembler'] = (with_ports(built['assembler_1'], [(0, -0.5, 180)], [(0, 0.5, 0)]), 145)
        P['assembler_2'] = (with_ports(built['assembler_2'], [(0, -0.5, 180)], [(0, 0.5, 0)]), 145)
    if 'extractor_1' in built:
        P['extractor'] = (with_ports(built['extractor_1'], [], [(0, 0.5, 0)]), 145)
        P['extractor_2'] = (with_ports(built['extractor_2'], [], [(0, 0.5, 0)]), 145)
    if 'storage_container' in built:
        P['storage_container'] = (with_ports(built['storage_container'], [(0, 0.5, 0)], [(0, -0.5, 180)]), 145)
    if 'belt_straight' in built:
        P['conveyor'] = ([(built['belt_straight'], (0, 0, -0.5), 0), (built['belt_straight'], (0, 0, 0.5), 0)], 145)
        P['pipe'] = ([(built['pipe_straight'], (0, 0, -0.5), 0), (built['pipe_straight'], (0, 0, 0.5), 0)], 145)
        P['splitter'] = ([(built['splitter'], (0, 0, 0), 0)], 145)
        P['underground_conveyor'] = ([(built['underground_in'], (0, 0, 0), 0)], 145)
        P['robotic_arm'] = ([(built['robotic_arm'], (0, 0, 0), 0)], 125)
    if 'constructor' in built:
        P['constructor'] = (with_ports(built['constructor'], [(0, -1, 180), (1, 0, 90)], [(0, 1, 0)]), 145)
        P['chemical_plant'] = (with_ports(built['chemical_plant'], [(0, -1.5, 180)], [(0, 1.5, 0)], [(-1.5, 0, 270)]), 145)
        P['refinery'] = (with_ports(built['refinery'], [], [(0, 1.5, 0)], [(0, -1.5, 180)]), 145)
        P['research_lab'] = (with_ports(built['research_lab'], [(0, -0.5, 180), (0, 0.5, 0), (0.5, 0, 90), (-0.5, 0, 270)], []), 145)
    if 'oil_extractor' in built:
        P['oil_extractor'] = (with_ports(built['oil_extractor'], [], [], [(0.5, 1, 0)]), 145)
        P['water_extractor'] = (with_ports(built['water_extractor'], [], [], [(0.5, 1, 0)]), 145)
        P['fluid_storage_tank'] = (with_ports(built['fluid_storage_tank'], [], [], [(0, -0.5, 180), (0, 0.5, 0)]), 145)
        P['power_generator'] = (with_ports(built['power_generator'], [(0, -0.5, 180)], []), 145)
    if 'pipe_splitter' in built:
        P['pipe_splitter'] = ([(built['pipe_splitter'], (0, 0, 0), 0)], 145)
    if 'drone' in built:
        d = built['drone']
        props = [(built['drone_prop'], p, 0) for p in DRONE_PROPS]
        d_full = [(d, (0, 0, 0), 0)] + props
        P['drone'] = (d_full, -35)
        def dr(x, y, z):
            return [(d, (x, y, z), 0)] + [(built['drone_prop'], (x + p[0], y + p[1], z + p[2]), 0) for p in DRONE_PROPS]
        P['drone_load_station'] = ([(built['drone_load_station'], (0, 0, 0), 0)] + dr(-1.2, PAD_Y, 1.75) + dr(1.2, 2.2, 0.8), 145)
        P['drone_unload_station'] = ([(built['drone_unload_station'], (0, 0, 0), 0)] + dr(1.2, PAD_Y, -0.3), -35)
    return P


if __name__ == '__main__':
    out_models, out_icons, out_copy = sys.argv[1], sys.argv[2], sys.argv[3]
    batches = set(int(b) for b in sys.argv[4:]) if len(sys.argv) > 4 else None
    for d in (out_models, out_icons, out_copy):
        os.makedirs(d, exist_ok=True)
    write_mtl(os.path.join(out_models, 'wi.mtl'))
    shutil.copy(os.path.join(out_models, 'wi.mtl'), os.path.join(out_copy, 'wi.mtl'))
    import copy, json
    built = {}
    parts = []
    for name, fn in MODELS.items():
        if batches is not None and BATCH[name] not in batches and not name.startswith('port_'):
            continue
        m = fn()
        built[name] = copy.deepcopy(m)      # иконка — целиком
        outputs = [(name, m)]
        for obj, pivot, prefab, parent, anim in SPLITS.get(name, []):
            part_name = name + '_' + obj.lower()
            outputs.append((part_name, m.split(obj, pivot)))
            parts.append({'prefab': prefab, 'parent': parent, 'anim': anim, 'model': part_name, 'pos': list(pivot)})
        for out_name, model in outputs:
            model.write_obj(os.path.join(out_models, out_name + '.obj'))
            shutil.copy(os.path.join(out_models, out_name + '.obj'), os.path.join(out_copy, out_name + '.obj'))
    with open(os.path.join(out_copy, 'parts.json'), 'w', newline='\n') as f:
        json.dump({'parts': parts}, f, indent=1)
    for icon_id, (parts, yaw) in icon_plan(built).items():
        render(parts, os.path.join(out_icons, icon_id + '.png'), yaw=yaw)
    print('models', sorted(built), 'icons', sorted(icon_plan(built)))
