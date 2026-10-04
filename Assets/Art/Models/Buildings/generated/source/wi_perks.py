# Иконки плюшек персонажа (вкладка «Снаряжение») и опоры троса.
# Запуск: python wi_perks.py "<проект>"  → Assets/Resources/Perks/Icons/<id>.png (+ Resources/Decor/Icons/decor_zipline_post.png)
# Стиль — как у иконок зданий и декора: та же палитра wi_lib и рендер render().
import math, os, sys, io, re, uuid
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wi_lib import Model, render

ICONS = {}


def icon(name, yaw=-35, pitch=28):
    def deco(fn):
        ICONS[name] = (fn, yaw, pitch)
        return fn
    return deco


# ---------- детали ----------

def boot(m, x=0.0, z=0.0, upper='wi_copper', sole='wi_dark'):
    m.box('B', (x, 0.05, z + 0.05), (0.26, 0.1, 0.52), sole)
    m.box('B', (x, 0.22, z - 0.05), (0.24, 0.26, 0.3), upper)
    m.box('B', (x, 0.16, z + 0.2), (0.24, 0.14, 0.18), upper)
    m.box('B', (x, 0.37, z - 0.08), (0.27, 0.05, 0.33), sole)


def spring(m, x, z, y0, turns=4, r=0.13, mat='wi_steel'):
    for i in range(turns):
        m.cyl('S', (x, y0 + i * 0.07, z), r, 0.035, mat, 10)
        m.cyl('S', (x, y0 + i * 0.07 + 0.035, z), r * 0.7, 0.035, 'wi_dark', 10)


def canopy(m, colors, y=0.9, span=2.2, depth=0.8):
    n = len(colors)
    for i, c in enumerate(colors):
        t = i / (n - 1)
        x = (t - 0.5) * span
        yy = y - ((x / (span / 2)) ** 2) * 0.35
        m.box('W', (x, yy, 0), (span / n + 0.02, 0.06, depth), c, rz=-x * 22)
    for s in (-1, 1):
        m.beam('L', (s * span * 0.42, y - 0.3, 0), (s * 0.12, 0.05, 0), 0.02, 0.02, 'wi_dark')
    m.box('P', (0, 0.08, 0), (0.22, 0.16, 0.16), 'wi_teal2')


def cart(m, body='wi_steel', trim='wi_dark', stack=False):
    m.box('C', (0, 0.22, 0), (0.86, 0.06, 1.1), body)
    for sx in (-1, 1):
        m.box('C', (sx * 0.42, 0.46, 0), (0.06, 0.5, 1.1), body)
    for sz in (-1, 1):
        m.box('C', (0, 0.46, sz * 0.54), (0.86, 0.5, 0.06), body)
    # обод — рамка, внутри руда
    for sx in (-1, 1):
        m.box('C', (sx * 0.44, 0.73, 0), (0.06, 0.05, 1.16), trim)
    for sz in (-1, 1):
        m.box('C', (0, 0.73, sz * 0.56), (0.92, 0.05, 0.06), trim)
    for (x, z, s) in ((-0.15, -0.2, 0.28), (0.15, 0.1, 0.32), (-0.1, 0.28, 0.24), (0.18, -0.3, 0.22)):
        m.box('O', (x, 0.62, z), (s, s * 0.7, s), 'wi_brick', yaw=x * 200)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl('C', (sx * 0.43, 0.13, sz * 0.36), 0.12, 0.06, 'wi_dark', 10)
    # рельсы
    for sx in (-0.45, 0.45):
        m.box('R', (sx, 0.02, 0), (0.06, 0.04, 1.6), 'wi_steel')
    if stack:
        m.cyl('C', (0, 0.76, 0.38), 0.08, 0.5, 'wi_dark', 8)
        m.cyl('C', (0, 1.26, 0.38), 0.13, 0.06, 'wi_redpaint', 8)
        for i in range(3):
            m.box('Smoke', (0.05 * i, 1.45 + i * 0.16, 0.38 - 0.08 * i), (0.16 - i * 0.02,) * 3, 'wi_white')


def torso(m, top, bottom, extra=None):
    m.box('T', (0, 1.0, 0), (0.5, 0.6, 0.28), top)
    for s in (-1, 1):
        m.box('T', (s * 0.33, 1.05, 0), (0.14, 0.5, 0.16), top)
        m.box('T', (s * 0.12, 0.4, 0), (0.18, 0.62, 0.2), bottom)
        m.box('T', (s * 0.12, 0.05, 0.04), (0.2, 0.1, 0.3), 'wi_dark')
    m.box('T', (0, 0.72, 0), (0.48, 0.12, 0.26), bottom)
    m.box('H', (0, 1.48, 0), (0.26, 0.28, 0.26), 'wi_base')
    if extra:
        extra(m)


def hardhat(m, dome, lamp=None, lamp_rim=None):
    m.box('H', (0, 0.18, 0), (0.62, 0.3, 0.58), dome)
    m.box('H', (0, 0.36, 0), (0.48, 0.12, 0.44), dome)
    m.box('H', (0, 0.04, 0.04), (0.74, 0.06, 0.74), dome)
    m.box('H', (0, 0.43, 0), (0.08, 0.05, 0.5), dome)
    if lamp_rim:
        m.cyl('H', (0, 0.2, 0.3), 0.11, 0.04, lamp_rim, 10)
    if lamp:
        m.box('H', (0, 0.22, 0.32), (0.14, 0.12, 0.06), lamp)


# ---------- иконки ----------

@icon('boots')
def i_boots():
    m = Model()
    boot(m, -0.18, 0)
    boot(m, 0.18, 0.12)
    for i in range(3):
        m.box('F', (0.45 + i * 0.02, 0.2 + i * 0.1, -0.25 - i * 0.12), (0.03, 0.03, 0.3), 'wi_yellow')
    return m


@icon('spring')
def i_spring():
    m = Model()
    boot(m, 0, 0)
    spring(m, 0, 0.02, -0.32, 4)
    return m


@icon('double', yaw=-20)
def i_double():
    m = Model()
    boot(m, 0, 0)
    for i in range(2):
        m.chevron('A', (0, 0.6 + i * 0.22, 0), 180, 'wi_lamp', size=0.5, w=0.07)
    return m


@icon('triple', yaw=-20)
def i_triple():
    m = Model()
    boot(m, 0, 0)
    for i in range(3):
        m.chevron('A', (0, 0.6 + i * 0.2, 0), 180, 'wi_fire', size=0.5, w=0.07)
    return m


@icon('dash', yaw=-70)
def i_dash():
    m = Model()
    boot(m, 0, 0.1)
    for i, (y, l) in enumerate(((0.12, 0.7), (0.26, 0.9), (0.4, 0.6))):
        m.box('F', (0, y, -0.35 - l / 2), (0.04, 0.04, l), 'wi_yellow')
    return m


@icon('wings', yaw=-25, pitch=22)
def i_wings():
    m = Model()
    canopy(m, ['wi_teal', 'wi_teal2'] * 3 + ['wi_teal'])
    return m


@icon('wings_stripes', yaw=-25, pitch=22)
def i_wings_stripes():
    m = Model()
    canopy(m, ['wi_yellow', 'wi_dark'] * 3 + ['wi_yellow'])
    return m


@icon('wings_sunset', yaw=-25, pitch=22)
def i_wings_sunset():
    m = Model()
    canopy(m, ['wi_flower', 'wi_redpaint', 'wi_orange', 'wi_yellow', 'wi_orange', 'wi_redpaint', 'wi_flower'])
    return m


@icon('grapple', yaw=-30)
def i_grapple():
    m = Model()
    m.cyl('G', (0, 0.0, 0), 0.06, 0.7, 'wi_steel', 8)
    for a in (0, 120, 240):
        t = math.radians(a)
        dx, dz = math.sin(t), math.cos(t)
        m.beam('G', (0, 0.12, 0), (dx * 0.35, 0.2, dz * 0.35), 0.06, 0.06, 'wi_steel')
        m.beam('G', (dx * 0.35, 0.2, dz * 0.35), (dx * 0.4, 0.42, dz * 0.4), 0.06, 0.06, 'wi_steel')
    m.tube('R', (0, 0.7, 0), (0.25, 1.4, -0.2), 0.03, 'wi_yellow', 6)
    m.tube('R', (0.25, 1.4, -0.2), (0.55, 1.6, -0.6), 0.03, 'wi_yellow', 6)
    return m


@icon('flippers', yaw=-30, pitch=40)
def i_flippers():
    m = Model()
    for x in (-0.22, 0.22):
        m.box('F', (x, 0.08, -0.1), (0.24, 0.14, 0.32), 'wi_blue')
        m.box('F', (x, 0.04, 0.35), (0.34, 0.04, 0.6), 'wi_blue')
        m.box('F', (x, 0.05, 0.62), (0.4, 0.03, 0.12), 'wi_teal2')
    return m


@icon('cart')
def i_cart():
    m = Model()
    cart(m)
    return m


@icon('cart2')
def i_cart2():
    m = Model()
    cart(m)
    m.chevron('A', (-0.55, 0.95, 0), 270, 'wi_lamp', size=0.4, w=0.07)
    m.chevron('A', (0.55, 0.95, 0), 90, 'wi_lamp', size=0.4, w=0.07)
    return m


@icon('cart_copper')
def i_cart_copper():
    m = Model()
    cart(m, 'wi_copper', 'wi_base')
    return m


@icon('cart_gold')
def i_cart_gold():
    m = Model()
    cart(m, 'wi_gold', 'wi_copper')
    return m


@icon('cart_steam')
def i_cart_steam():
    m = Model()
    cart(m, 'wi_dark', 'wi_redpaint', stack=True)
    return m


@icon('horn', yaw=-60)
def i_horn():
    m = Model()
    m.tube('H', (0, 0.3, -0.5), (0, 0.3, 0.1), 0.06, 'wi_gold', 10)
    m.tube('H', (0, 0.3, 0.1), (0, 0.3, 0.55), 0.06, 'wi_gold', 10, r2=0.26)
    m.box('H', (0, 0.12, -0.35), (0.1, 0.3, 0.1), 'wi_copper')
    m.cyl('H', (0, -0.05, -0.35), 0.18, 0.08, 'wi_redpaint', 10)
    return m


@icon('repairkit')
def i_repairkit():
    m = Model()
    m.box('K', (0, 0.25, 0), (0.9, 0.5, 0.5), 'wi_redpaint')
    m.box('K', (0, 0.52, 0), (0.92, 0.06, 0.52), 'wi_dark')
    m.beam('K', (-0.2, 0.65, 0), (0.2, 0.65, 0), 0.05, 0.05, 'wi_steel')
    m.beam('K', (-0.2, 0.55, 0), (-0.2, 0.65, 0), 0.05, 0.05, 'wi_steel')
    m.beam('K', (0.2, 0.55, 0), (0.2, 0.65, 0), 0.05, 0.05, 'wi_steel')
    m.box('K', (0, 0.3, 0.26), (0.12, 0.12, 0.02), 'wi_white')
    m.box('K', (0, 0.3, 0.265), (0.04, 0.12, 0.02), 'wi_redpaint')
    m.box('K', (0, 0.3, 0.265), (0.12, 0.04, 0.02), 'wi_redpaint')
    # гаечный ключ
    m.box('W', (0.25, 0.62, 0.05), (0.06, 0.04, 0.55), 'wi_steel', yaw=35)
    m.box('W', (0.08, 0.62, 0.3), (0.16, 0.05, 0.1), 'wi_steel', yaw=35)
    return m


@icon('lamp', yaw=-20)
def i_lamp():
    m = Model()
    hardhat(m, 'wi_yellow', lamp='wi_lamp')
    return m


@icon('hat_hardhat_lamp', yaw=-20)
def i_hat_hardhat_lamp():
    m = Model()
    hardhat(m, 'wi_orange', lamp='wi_lamp')
    return m


@icon('hat_miner', yaw=-20)
def i_hat_miner():
    m = Model()
    hardhat(m, 'wi_dark', lamp='wi_white', lamp_rim='wi_gold')
    return m


@icon('hat_crown', yaw=-20)
def i_hat_crown():
    m = Model()
    m.cyl('C', (0, 0, 0), 0.4, 0.22, 'wi_gold', 12)
    for i in range(6):
        t = i / 6 * math.pi * 2
        m.box('C', (math.sin(t) * 0.36, 0.32, math.cos(t) * 0.36), (0.1, 0.22, 0.1), 'wi_gold', yaw=math.degrees(t))
        m.box('C', (math.sin(t) * 0.36, 0.46, math.cos(t) * 0.36), (0.07, 0.07, 0.07), 'wi_redpaint')
    m.box('C', (0, 0.12, 0.4), (0.1, 0.1, 0.03), 'wi_red')
    return m


@icon('sleep', yaw=-40)
def i_sleep():
    m = Model()
    m.box('S', (0, 0.1, 0.1), (0.7, 0.2, 1.2), 'wi_chem')
    m.box('S', (0, 0.22, -0.2), (0.66, 0.06, 0.55), 'wi_leaf2')
    m.box('S', (0, 0.25, -0.52), (0.5, 0.14, 0.22), 'wi_white')
    for i in range(3):
        m.box('Z', (0.25 + i * 0.12, 0.55 + i * 0.18, -0.4), (0.14 - i * 0.02, 0.03, 0.03), 'wi_blue')
    return m


@icon('discount', yaw=-25, pitch=35)
def i_discount():
    m = Model()
    m.box('C', (0, 0.03, 0), (1.0, 0.06, 0.64), 'wi_teal')
    m.box('C', (0, 0.07, -0.18), (1.0, 0.02, 0.12), 'wi_gold')
    m.box('C', (-0.3, 0.07, 0.12), (0.18, 0.02, 0.14), 'wi_gold')
    m.chevron('A', (0.25, 0.08, 0.15), 180, 'wi_red', size=0.36, w=0.07)
    return m


@icon('refund')
def i_refund():
    m = Model()
    for i in range(4):
        m.cyl('C', (0, i * 0.08, 0), 0.3, 0.07, 'wi_gold', 14)
    m.cyl('C', (0.42, 0, 0.15), 0.3, 0.07, 'wi_gold', 14)
    m.chevron('A', (0, 0.55, 0), 270, 'wi_lamp', size=0.5, w=0.08)
    return m


@icon('insurance', yaw=-15)
def i_insurance():
    m = Model()
    m.box('S', (0, 0.55, 0), (0.7, 0.7, 0.12), 'wi_teal')
    m.box('S', (0, 0.15, 0), (0.46, 0.2, 0.12), 'wi_teal', rz=0)
    m.box('S', (0, 0.0, 0), (0.2, 0.15, 0.12), 'wi_teal')
    m.box('S', (0, 0.5, 0.07), (0.12, 0.5, 0.03), 'wi_copper')
    m.box('S', (0, 0.6, 0.07), (0.42, 0.12, 0.03), 'wi_copper')
    return m


@icon('trail_sparks', yaw=-60)
def i_trail_sparks():
    m = Model()
    boot(m, 0, 0.2)
    for i, (x, y, z) in enumerate(((0.1, 0.1, -0.3), (-0.12, 0.25, -0.45), (0.15, 0.35, -0.6), (-0.05, 0.12, -0.7), (0.05, 0.45, -0.85))):
        m.box('P', (x, y, z), (0.07, 0.07, 0.07), 'wi_fire' if i % 2 else 'wi_lamp')
    return m


@icon('trail_steam', yaw=-60)
def i_trail_steam():
    m = Model()
    boot(m, 0, 0.2)
    for i, s in enumerate((0.2, 0.28, 0.34)):
        m.box('P', (0.05 * i, 0.2 + i * 0.15, -0.3 - i * 0.3), (s, s, s), 'wi_white')
    return m


@icon('trail_leaves', yaw=-60)
def i_trail_leaves():
    m = Model()
    boot(m, 0, 0.2)
    for i, (x, y, z, a) in enumerate(((0.1, 0.15, -0.3, 20), (-0.12, 0.3, -0.5, 70), (0.14, 0.45, -0.7, 130), (-0.05, 0.2, -0.85, 200))):
        m.box('P', (x, y, z), (0.16, 0.02, 0.09), 'wi_leaf' if i % 2 else 'wi_leaf2', yaw=a)
    return m


@icon('outfit_welder', yaw=-25)
def i_outfit_welder():
    m = Model()
    torso(m, 'wi_base', 'wi_dark', lambda mm: mm.box('M', (0, 1.5, 0.15), (0.3, 0.3, 0.05), 'wi_dark'))
    return m


@icon('outfit_engineer', yaw=-25)
def i_outfit_engineer():
    m = Model()
    torso(m, 'wi_white', 'wi_blue', lambda mm: mm.box('M', (0, 1.66, 0), (0.32, 0.1, 0.32), 'wi_white'))
    return m


@icon('outfit_miner', yaw=-25)
def i_outfit_miner():
    m = Model()
    torso(m, 'wi_dark', 'wi_rubber', lambda mm: (mm.box('M', (0, 1.66, 0), (0.32, 0.12, 0.32), 'wi_dark'),
                                                 mm.box('M', (0, 1.66, 0.17), (0.1, 0.08, 0.03), 'wi_lamp')))
    return m


@icon('outfit_night', yaw=-25)
def i_outfit_night():
    m = Model()

    def stripes(mm):
        for y in (0.88, 1.12):
            mm.box('M', (0, y, 0), (0.52, 0.05, 0.3), 'wi_white')
    torso(m, 'wi_orange', 'wi_dark', stripes)
    return m


@icon('pet', yaw=-30, pitch=32)
def i_pet():
    m = Model()
    m.box('D', (0, 0.2, 0), (0.6, 0.18, 0.6), 'wi_teal')
    m.box('D', (0, 0.06, 0), (0.36, 0.1, 0.36), 'wi_copper')
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.beam('D', (sx * 0.2, 0.25, sz * 0.2), (sx * 0.42, 0.3, sz * 0.42), 0.05, 0.04, 'wi_dark')
            m.cyl('D', (sx * 0.42, 0.32, sz * 0.42), 0.2, 0.02, 'wi_steel', 10)
    m.box('D', (0, 0.2, 0.31), (0.12, 0.07, 0.02), 'wi_lamp')
    return m


@icon('decor_zipline_post', yaw=-35, pitch=20)
def i_zipline():
    m = Model()
    m.box('Z', (0, 0.06, 0), (0.8, 0.12, 0.8), 'wi_dark')
    m.box('Z', (0, 2.2, 0), (0.2, 4.3, 0.2), 'wi_steel')
    for i in range(4):
        m.box('Z', (0, 0.45 + i * 0.22, 0), (0.22, 0.1, 0.22), 'wi_yellow' if i % 2 == 0 else 'wi_dark')
    m.box('Z', (0, 4.38, 0), (0.18, 0.16, 1.0), 'wi_steel')
    m.box('Z', (0, 4.3, 0), (0.12, 0.3, 0.3), 'wi_yellow')
    m.beam('Z', (0, 4.3, 0), (0, 3.4, 2.4), 0.03, 0.03, 'wi_dark')
    return m


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

FOLDER_META = 'fileFormatVersion: 2\nguid: {GUID}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'


def ensure_meta(path, body):
    meta = path + '.meta'
    if os.path.exists(meta):
        return
    io.open(meta, 'w', encoding='utf-8', newline='\n').write(body.replace('{GUID}', uuid.uuid4().hex))


def ensure_dir(path):
    if not os.path.isdir(path):
        os.makedirs(path)
    ensure_meta(path.rstrip('/\\'), FOLDER_META)


if __name__ == '__main__':
    root = sys.argv[1]
    res = os.path.join(root, 'Assets', 'Resources')
    perks = os.path.join(res, 'Perks')
    out = os.path.join(perks, 'Icons')
    ensure_dir(perks)
    ensure_dir(out)
    decor_icons = os.path.join(res, 'Decor', 'Icons')
    only = set(sys.argv[2:])
    for name, (fn, yaw, pitch) in ICONS.items():
        if only and name not in only:
            continue
        path = os.path.join(decor_icons if name.startswith('decor_') else out, name + '.png')
        render([(fn(), (0, 0, 0), 0)], path, size=256, yaw=yaw, pitch=pitch)
        ensure_meta(path, SPRITE)
    print('icons', len(ICONS))
