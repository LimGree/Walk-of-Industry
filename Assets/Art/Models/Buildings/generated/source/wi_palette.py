# Материал-палитра wi_palette: две текстуры 64x64 (альбедо+глянец, свечение) и Standard-материал.
# Зовут wi_install.py и wi_decor.py до привязки материалов в .meta OBJ.
import io, os, re, uuid
from wi_lib import write_palette, PALETTE_MAT, PAL_SIZE

TEX_META = '''fileFormatVersion: 2
guid: {GUID}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    wrapU: 1
    wrapV: 1
  maxTextureSize: 64
  alphaUsage: 1
  alphaIsTransparency: 0
  textureType: 0
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 64
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 64
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  userData:
  assetBundleName:
  assetBundleVariant:
'''
NATIVE = 'fileFormatVersion: 2\nguid: {GUID}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'


def _w(path, text):
    io.open(path, 'w', encoding='utf-8', newline='\n').write(text)


def _meta(path, body):
    meta = path + '.meta'
    if os.path.exists(meta):
        return re.search(r'guid: (\w+)', io.open(meta, encoding='utf-8').read()).group(1)
    g = uuid.uuid4().hex
    _w(meta, body.replace('{GUID}', g))
    return g


def _tex(t, prop, guid):
    # - _MainTex:\n        m_Texture: {fileID: 0}  ->  ссылка на текстуру
    return re.sub(r'(    - %s:\n        m_Texture: )\{[^}]*\}' % re.escape(prop),
                  lambda m: m.group(1) + '{fileID: 2800000, guid: %s, type: 3}' % guid, t, count=1)


def _float(t, prop, value):
    return re.sub(r'    - %s: .*' % re.escape(prop), '    - %s: %s' % (prop, value), t, count=1)


def install(mat_dir):
    """Пишет wi_palette_albedo.png, wi_palette_emit.png и wi_palette.mat в mat_dir. Возвращает guid материала."""
    alb = os.path.join(mat_dir, PALETTE_MAT + '_albedo.png')
    emi = os.path.join(mat_dir, PALETTE_MAT + '_emit.png')
    write_palette(alb, emi)
    g_alb = _meta(alb, TEX_META)
    g_emi = _meta(emi, TEX_META)

    t = io.open(os.path.join(mat_dir, 'wi_teal.mat'), encoding='utf-8').read()
    t = re.sub(r'm_Name: .*', 'm_Name: ' + PALETTE_MAT, t, count=1)
    t = _tex(t, '_MainTex', g_alb)
    t = _tex(t, '_EmissionMap', g_emi)
    t = re.sub(r'    - _Color: \{.*\}', '    - _Color: {r: 1, g: 1, b: 1, a: 1}', t)
    # свечение = карта × 1.6 (как было у светящихся wi_*: цвет × 1.6)
    t = re.sub(r'    - _EmissionColor: \{.*\}', '    - _EmissionColor: {r: 1.6, g: 1.6, b: 1.6, a: 1}', t)
    t = _float(t, '_Glossiness', '0.5')
    t = _float(t, '_GlossMapScale', '1')
    t = _float(t, '_SmoothnessTextureChannel', '1')     # глянец из альфы альбедо
    t = re.sub(r'  m_ValidKeywords:.*?\n(?=  m_InvalidKeywords)',
               '  m_ValidKeywords:\n  - _EMISSION\n  - _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A\n', t, count=1, flags=re.S)
    t = re.sub(r'  m_LightmapFlags: \d+', '  m_LightmapFlags: 2', t)
    t = re.sub(r'  m_EnableInstancingVariants: \d', '  m_EnableInstancingVariants: 1', t)
    path = os.path.join(mat_dir, PALETTE_MAT + '.mat')
    g = _meta(path, NATIVE)
    _w(path, t)
    return g
