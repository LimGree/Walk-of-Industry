import os
import subprocess
import sys
import wave
import numpy as np
import imageio_ffmpeg

SRC = r"C:\Users\step2\Desktop\Walk to biome\Assets\Audio"
DST = r"C:\Users\step2\Desktop\Walk to biome\Assets\Resources\Audio"
FF = imageio_ffmpeg.get_ffmpeg_exe()
RATE = 48000
PEAK_LIMIT = 10 ** (-3.0 / 20.0)  # -3 dBFS
MAX_BOOST_DB = 18.0

# RMS targets in dBFS
RMS = {
    "ui_hover": -28,
    "ui_click": -24,
    "ui_select": -24,
    "ui_tab": -24,
    "ui_search": -26,
    "ui_type": -26,
    "ui_toggle": -22,
    "ui_open": -18,
    "ui_close": -18,
    "ui_map_open": -18,
    "ui_modal": -17,
    "ui_pause": -18,
    "ui_unpause": -18,
    "ui_confirm": -16,
    "ui_notify": -16,
    "ui_error": -16,
    "ui_drag_start": -20,
    "ui_drag_drop": -18,
    "world_place": -14,
    "world_place_belt": -16,
    "world_demolish": -14,
    "world_rotate": -18,
    "world_upgrade": -14,
    "world_invalid": -16,
    "world_copy": -20,
    "world_paste": -18,
    "player_step": -22,
    "player_jump": -16,
    "player_land": -16,
    "bld": -20,
    "mus": -18,
    "amb": -26,
}

MAP = [
    ("UI/ui_hover.wav", "ui/ui_hover.wav"),
    ("UI/ui_click.wav", "ui/ui_click.wav"),
    ("UI/ui_open.wav", "ui/ui_open.wav"),
    ("UI/ui_close.wav", "ui/ui_close.wav"),
    ("UI/ui_confirm.wav", "ui/ui_confirm.wav"),
    ("UI/ui_error.wav", "ui/ui_error.wav"),
    ("UI/ui_modal.wav", "ui/ui_modal.wav"),
    ("UI/ui_tab..wav", "ui/ui_tab.wav"),
    ("UI/ui_select.wav", "ui/ui_select.wav"),
    ("UI/ui_toggle.wav", "ui/ui_toggle.wav"),
    ("UI/ui_type.wav", "ui/ui_type.wav"),
    ("UI/ui_search.wav", "ui/ui_search.wav"),
    ("UI/ui_drag_start.wav", "ui/ui_drag_start.wav"),
    ("UI/ui_drag_drop.wav", "ui/ui_drag_drop.wav"),
    ("UI/ui_map_open.wav", "ui/ui_map_open.wav"),
    ("UI/ui_notify.wav", "ui/ui_notify.wav"),
    ("UI/ui_pause.wav", "ui/ui_pause.wav"),
    ("UI/ui_unpause.wav", "ui/ui_unpause.wav"),
    ("World/world_place.wav", "world/world_place.wav"),
    ("World/world_place_belt.wav", "world/world_place_belt.wav"),
    ("World/world_demolish.wav", "world/world_demolish.wav"),
    ("World/world_rotate.mp3", "world/world_rotate.wav"),
    ("World/world_upgrade.wav", "world/world_upgrade.wav"),
    ("World/world_invalid.wav", "world/world_invalid.wav"),
    ("World/world_copy.wav", "world/world_copy.wav"),
    ("World/world_paste.wav", "world/world_paste.wav"),
    ("Build/bld_extractor_loop.wav", "buildings/bld_extractor_loop.wav"),
    ("Build/bld_oil_loop.wav", "buildings/bld_oil_loop.wav"),
    ("Build/bld_water_loop.wav", "buildings/bld_water_loop.wav"),
    ("Build/bld_smelter_loop.wav", "buildings/bld_smelter_loop.wav"),
    ("Build/bld_constructor_loop.wav", "buildings/bld_constructor_loop.wav"),
    ("Build/bld_assembler_loop.wav", "buildings/bld_assembler_loop.wav"),
    ("Build/bld_chem_loop.wav", "buildings/bld_chem_loop.wav"),
    ("Build/bld_refinery_loop.wav", "buildings/bld_refinery_loop.wav"),
    ("Player/player_step_01.wav", "player/player_step_01.wav"),
    ("Player/player_step_02.wav", "player/player_step_02.wav"),
    ("Player/player_step_03.wav", "player/player_step_03.wav"),
    ("Player/player_step_04.wav", "player/player_step_04.wav"),
    ("Player/player_jump.wav", "player/player_jump.wav"),
    ("Player/player_land.wav", "player/player_land.wav"),
    ("Music/mus_menu.ogg", "music/mus_menu.ogg"),
    ("Music/mus_pause.ogg", "music/mus_pause.ogg"),
    ("amb/amb_field.ogg", "ambient/amb_field.ogg"),
    ("amb/amb_forest.ogg.ogg", "ambient/amb_forest.ogg"),
    ("amb/amb_mountain.ogg", "ambient/amb_mountain.ogg"),
    ("amb/amb_water.ogg.ogg", "ambient/amb_water.ogg"),
]


def target_rms_db(name):
    stem = os.path.splitext(os.path.basename(name))[0]
    if stem in RMS:
        return RMS[stem]
    if stem.startswith("player_step"):
        return RMS["player_step"]
    if stem.startswith("bld_"):
        return RMS["bld"]
    if stem.startswith("mus_"):
        return RMS["mus"]
    if stem.startswith("amb_"):
        return RMS["amb"]
    if stem.startswith("ui_"):
        return -18
    if stem.startswith("world_"):
        return -16
    return -18


def decode(path, stereo):
    ac = "2" if stereo else "1"
    cmd = [
        FF, "-v", "error", "-i", path,
        "-f", "f32le", "-acodec", "pcm_f32le",
        "-ac", ac, "-ar", str(RATE), "pipe:1",
    ]
    p = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if p.returncode != 0 or not p.stdout:
        raise RuntimeError(p.stderr.decode("utf-8", "ignore") or "ffmpeg decode failed")
    ch = 2 if stereo else 1
    data = np.frombuffer(p.stdout, dtype=np.float32)
    if ch == 2:
        data = data.reshape(-1, 2)
    return data


def normalize(data, rms_db):
    x = np.asarray(data, dtype=np.float32)
    if x.size == 0:
        return x
    mono = x if x.ndim == 1 else np.mean(x, axis=1)
    rms = float(np.sqrt(np.mean(np.square(mono)) + 1e-12))
    peak = float(np.max(np.abs(x)) + 1e-12)
    want = 10 ** (rms_db / 20.0)
    gain = want / rms
    max_boost = 10 ** (MAX_BOOST_DB / 20.0)
    gain = min(gain, max_boost)
    y = x * gain
    peak2 = float(np.max(np.abs(y)) + 1e-12)
    if peak2 > PEAK_LIMIT:
        y *= PEAK_LIMIT / peak2
    return np.clip(y, -1.0, 1.0).astype(np.float32), 20 * np.log10(rms), 20 * np.log10(peak)


def write_wav(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if data.ndim == 1:
        ch = 1
        frames = data
    else:
        ch = 2
        frames = data.reshape(-1)
    pcm = np.clip(frames * 32767.0, -32768, 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())


def write_ogg(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ch = 1 if data.ndim == 1 else 2
    raw = data.astype(np.float32).tobytes() if data.ndim == 1 else data.reshape(-1).astype(np.float32).tobytes()
    cmd = [
        FF, "-y", "-v", "error",
        "-f", "f32le", "-ar", str(RATE), "-ac", str(ch), "-i", "pipe:0",
        "-c:a", "libvorbis", "-q:a", "5", path,
    ]
    p = subprocess.run(cmd, input=raw, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if p.returncode != 0:
        raise RuntimeError(p.stderr.decode("utf-8", "ignore") or "ogg encode failed")


def main():
    print("ffmpeg:", FF)
    ok = 0
    for rel_src, rel_dst in MAP:
        src = os.path.join(SRC, rel_src.replace("/", os.sep))
        dst = os.path.join(DST, rel_dst.replace("/", os.sep))
        if not os.path.isfile(src):
            print("MISSING", rel_src)
            continue
        stereo = rel_dst.startswith("music/") or rel_dst.startswith("ambient/")
        try:
            data = decode(src, stereo)
            rms_db = target_rms_db(rel_dst)
            y, old_rms, old_peak = normalize(data, rms_db)
            if dst.lower().endswith(".ogg"):
                write_ogg(dst, y)
            else:
                write_wav(dst, y)
            new_peak = 20 * np.log10(float(np.max(np.abs(y)) + 1e-12))
            new_rms = 20 * np.log10(float(np.sqrt(np.mean(np.square(y if y.ndim == 1 else np.mean(y, 1)))) + 1e-12))
            print(f"OK  {rel_dst:40s}  rms {old_rms:6.1f}->{new_rms:6.1f}  peak {old_peak:6.1f}->{new_peak:6.1f}  tgt {rms_db}")
            ok += 1
        except Exception as e:
            print("FAIL", rel_src, e)
            return 1
    print("done", ok)
    return 0


if __name__ == "__main__":
    sys.exit(main())
