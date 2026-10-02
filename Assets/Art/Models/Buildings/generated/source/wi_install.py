# Установка моделей в проект: python wi_install.py <project root> [batch...]
import io, os, re, sys, uuid, glob, shutil, subprocess
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from wi_lib import MATS
import wi_palette

ROOT = sys.argv[1]
BATCHES = sys.argv[2:]
A = os.path.join(ROOT, 'Assets')
RES = os.path.join(A, 'Resources', 'Models')
MATDIR = os.path.join(RES, 'Materials')
COPY = os.path.join(A, 'Art', 'Models', 'Buildings', 'generated')
import tempfile
ICON_TMP = os.path.join(tempfile.gettempdir(), 'wi_icons_out')   # не внутри Assets: Unity не импортирует
ICON_GEN = os.path.join(A, 'Art', 'Icons', 'Buildings', 'gen')
KEEP_ICON = {'drone', 'drone_load_station', 'drone_unload_station'}   # эти PNG уже есть, guid не меняем


def w(path, text):
    io.open(path, 'w', encoding='utf-8', newline='\n').write(text)


def ensure_meta(path, body):
    meta = path + '.meta'
    if os.path.exists(meta):
        return re.search(r'guid: (\w+)', io.open(meta, encoding='utf-8').read()).group(1)
    g = uuid.uuid4().hex
    w(meta, body.replace('{GUID}', g))
    return g

NATIVE = 'fileFormatVersion: 2\nguid: {GUID}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
DEFAULT = 'fileFormatVersion: 2\nguid: {GUID}\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
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

for d in (RES, MATDIR, COPY, ICON_TMP, ICON_GEN):
    os.makedirs(d, exist_ok=True)

# 1. Материалы палитры (шаблон — собственный wi_teal.mat, Standard)
tpl = io.open(os.path.join(MATDIR, 'wi_teal.mat'), encoding='utf-8').read()
for name, (col, emis, gloss) in MATS.items():
    path = os.path.join(MATDIR, name + '.mat')
    t = re.sub(r'm_Name: .*', 'm_Name: ' + name, tpl, count=1)
    t = re.sub(r'    - _Color: \{.*\}', '    - _Color: {r: %.4f, g: %.4f, b: %.4f, a: 1}' % col, t)
    t = re.sub(r'    - _Glossiness: .*', '    - _Glossiness: %.2f' % gloss, t)
    e = tuple(v * 1.6 for v in col) if emis else (0, 0, 0)
    t = re.sub(r'    - _EmissionColor: \{.*\}', '    - _EmissionColor: {r: %.4f, g: %.4f, b: %.4f, a: 1}' % e, t)
    t = re.sub(r'  m_LightmapFlags: \d+', '  m_LightmapFlags: %d' % (2 if emis else 4), t)
    if emis:
        t = t.replace('  m_ValidKeywords: []', '  m_ValidKeywords:\n  - _EMISSION')
    ensure_meta(path, NATIVE)
    w(path, t)

# 1б. Палитра: один материал wi_palette (текстуры 64x64) вместо обычных цветов в моделях
wi_palette.install(MATDIR)

# 2. Модели + иконки
subprocess.check_call([sys.executable, os.path.join(HERE, 'wi_models.py'), RES, ICON_TMP, COPY] + BATCHES)
for f in ('wi_lib.py', 'wi_models.py', 'wi_install.py', 'sheet.py', 'wi_palette.py', 'wi_decor.py'):
    src = os.path.join(HERE, f)
    dst = os.path.join(COPY, 'source', f)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    if os.path.abspath(src) != os.path.abspath(dst):    # генератор живёт прямо в source
        shutil.copy(src, dst)
    ensure_meta(dst, DEFAULT)

# 3. Иконки → проект и BuildingData
builders = {}
for asset in glob.glob(os.path.join(A, 'Data', 'Buildings', '*.asset')):
    txt = io.open(asset, encoding='utf-8').read()
    m = re.search(r'^  id: (\S+)', txt, re.M)
    if m:
        builders[m.group(1)] = asset
for png in glob.glob(os.path.join(ICON_TMP, '*.png')):
    icon_id = os.path.splitext(os.path.basename(png))[0]
    if icon_id in KEEP_ICON:
        dst = os.path.join(A, 'Art', 'Icons', 'Buildings', icon_id + '.png')
        shutil.copy(png, dst)
        continue
    dst = os.path.join(ICON_GEN, icon_id + '.png')
    g = ensure_meta(dst, SPRITE)
    shutil.copy(png, dst)
    if icon_id in builders:
        asset = builders[icon_id]
        txt = io.open(asset, encoding='utf-8').read()
        txt2 = re.sub(r'^  icon: \{.*\}$', '  icon: {fileID: 21300000, guid: %s, type: 3}' % g, txt, flags=re.M)
        if txt2 != txt:
            io.open(asset, 'w', encoding='utf-8', newline='').write(txt2)
            print('icon ->', os.path.basename(asset))
print('installed')

# 4. Полотно ленты — шейдер бегущих рёбер
BELT_SHADER = '{fileID: 4800000, guid: 7b2d4f6a8c1e4a3b9d5f7a2c4e6b8d91, type: 3}'
rub = os.path.join(MATDIR, 'wi_rubber.mat')
t = io.open(rub, encoding='utf-8').read()
t = re.sub(r'  m_Shader: \{.*\}', '  m_Shader: ' + BELT_SHADER, t, count=1)
w(rub, t)

# 5. .meta всех OBJ: материалы из wi.mtl → наши wi_*.mat (новые OBJ получают meta по шаблону)
mat_guids = {}
for m in glob.glob(os.path.join(MATDIR, '*.mat.meta')):
    mat_guids[os.path.basename(m)[:-9]] = re.search(r'guid: (\w+)', io.open(m, encoding='utf-8').read()).group(1)
metas = [m for m in glob.glob(os.path.join(RES, '*.obj.meta'))]
template = io.open(metas[0], encoding='utf-8').read() if metas else None
for obj in glob.glob(os.path.join(RES, '*.obj')):
    meta = obj + '.meta'
    if os.path.exists(meta):
        t = io.open(meta, encoding='utf-8').read()
    elif template:
        t = re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, template, count=1)
    else:
        continue
    used = sorted(set(re.findall(r'^usemtl (\S+)', io.open(obj, encoding='utf-8').read(), re.M)))
    ext = '  externalObjects:\n' + ''.join(
        '  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n      name: %s\n'
        '    second: {fileID: 2100000, guid: %s, type: 2}\n' % (n, mat_guids[n]) for n in used if n in mat_guids)
    t = re.sub(r'  externalObjects:.*?\n(?=  materials:)', ext, t, count=1, flags=re.S)
    w(meta, t)

# 6. Текстуры частиц
import numpy as np, struct, zlib
FX = os.path.join(A, 'Resources', 'FX')
os.makedirs(FX, exist_ok=True)
def png(path, img):
    h, wdt = img.shape[:2]
    raw = b''.join(b'\x00' + img[y].tobytes() for y in range(h))
    ch = lambda tg, d: struct.pack('>I', len(d)) + tg + d + struct.pack('>I', zlib.crc32(tg + d) & 0xffffffff)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', wdt, h, 8, 6, 0, 0, 0))
                           + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))
N = 64
yy, xx = np.mgrid[0:N, 0:N]
d = np.sqrt(((xx + 0.5) / N - 0.5) ** 2 + ((yy + 0.5) / N - 0.5) ** 2) * 2
rng = np.random.default_rng(7)
noise = np.clip(1 - d, 0, 1) ** 1.6 * (0.8 + 0.2 * rng.random((N, N)))
puff = np.zeros((N, N, 4), np.uint8); puff[..., :3] = 255; puff[..., 3] = (np.clip(noise, 0, 1) * 255).astype(np.uint8)
dot = np.zeros((N, N, 4), np.uint8); dot[..., :3] = 255; dot[..., 3] = (np.clip(1 - d, 0, 1) ** 2.2 * 255).astype(np.uint8)
TEX = '''fileFormatVersion: 2
guid: {GUID}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 1
    sRGBTexture: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    wrapU: 1
    wrapV: 1
  maxTextureSize: 256
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 0
  textureShape: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
'''
for name, img in (('fx_puff', puff), ('fx_dot', dot)):
    pth = os.path.join(FX, name + '.png')
    ensure_meta(pth, TEX)
    png(pth, img)
print('fx textures ok')
