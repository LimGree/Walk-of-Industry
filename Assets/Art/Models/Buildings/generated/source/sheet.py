# Контактный лист: несколько моделей в ряд в одном рендере.  python sheet.py out.png yaw name[:w] ...
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wi_models as W
from wi_lib import render

out, yaw = sys.argv[1], float(sys.argv[2])
parts, x = [], 0.0
for arg in sys.argv[3:]:
    name, _, w = arg.partition(':')
    w = float(w or 1)
    m = W.MODELS[name]()
    parts.append((m, (x + w / 2, 0, 0), 0))
    x += w + 0.4
# сдвиг к центру
parts = [(m, (o[0] - x / 2, o[1], o[2]), r) for m, o, r in parts]
render(parts, out, size=768, yaw=yaw, pitch=28)
print('ok')
