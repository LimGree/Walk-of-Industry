# Собирает model_review.html — просмотрщик всех OBJ из Assets/Art/Models/Buildings/generated
# с заметками по каждой модели и кнопкой «Сформировать отчёт» (копирует в буфер обмена).
# Запуск: python Tools/ModelReview/build_model_review.py
import base64, io, json, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
GEN = os.path.join(ROOT, 'Assets', 'Art', 'Models', 'Buildings', 'generated')
MATDIR = os.path.join(ROOT, 'Assets', 'Resources', 'Models', 'Materials')
OUT = os.path.join(os.path.dirname(__file__), 'model_review.html')

LABELS = {
    'assembler_1': 'Сборщик ур.1', 'assembler_2': 'Сборщик ур.2',
    'belt_straight': 'Конвейер прямой', 'belt_corner_l': 'Конвейер поворот L', 'belt_corner_r': 'Конвейер поворот R',
    'belt_sides': 'Конвейер боковые входы', 'belt_tee_l': 'Конвейер тройник L', 'belt_tee_r': 'Конвейер тройник R',
    'belt_triple': 'Конвейер тройной вход',
    'pipe_straight': 'Труба прямая', 'pipe_corner_l': 'Труба поворот L', 'pipe_corner_r': 'Труба поворот R',
    'pipe_sides': 'Труба боковые входы', 'pipe_tee_l': 'Труба тройник L', 'pipe_tee_r': 'Труба тройник R',
    'pipe_triple': 'Труба тройной вход', 'pipe_splitter': 'Трубный сплиттер',
    'chemical_plant': 'Химзавод', 'constructor': 'Конструктор', 'refinery': 'Нефтеперерабатывающий завод',
    'drone': 'Дрон', 'drone_prop': 'Дрон — винт', 'drone_load_station': 'Станция загрузки дронов',
    'drone_unload_station': 'Станция разгрузки дронов',
    'extractor_1': 'Добытчик ур.1', 'extractor_2': 'Добытчик ур.2', 'water_extractor': 'Водокачка',
    'oil_extractor': 'Нефтекачалка', 'fluid_storage_tank': 'Резервуар жидкости',
    'port_in': 'Порт входа', 'port_out': 'Порт выхода', 'port_fluid': 'Порт жидкости',
    'power_generator': 'Генератор', 'research_lab': 'Лаборатория', 'robotic_arm': 'Манипулятор',
    'smelter': 'Плавильня', 'splitter': 'Сплиттер', 'storage_container': 'Склад',
    'underground_in': 'Подземный конвейер — вход', 'underground_out': 'Подземный конвейер — выход',
}


def category(name):
    if name.startswith('belt_') or name.startswith('underground_') or name == 'splitter':
        return 'Конвейеры'
    if name.startswith('pipe_'):
        return 'Трубы'
    if name.startswith('port_'):
        return 'Порты'
    if name.startswith('drone'):
        return 'Дроны'
    return 'Здания'


# ---------- порты по сокетам префабов (повторяет BuildingRestyle.AddPorts / BuildingPrefabLayout.ApplyPrimarySockets) ----------
PREFABS = os.path.join(ROOT, 'Assets', 'Prefabs', 'Buildings')
RESTYLE_MODELS = {  # BuildingRestyle.Models
    'smelter': ['smelter'], 'assembler': ['assembler_1', 'assembler_2'], 'extractor': ['extractor_1', 'extractor_2'],
    'storage_container': ['storage_container'], 'constructor': ['constructor'], 'chemical_plant': ['chemical_plant'],
    'refinery': ['refinery'], 'research_lab': ['research_lab'], 'power_generator': ['power_generator'],
    'oil_extractor': ['oil_extractor'], 'water_extractor': ['water_extractor'], 'fluid_storage_tank': ['fluid_storage_tank'],
}
CRAFTERS = {'Assembler', 'ChemicalPlant', 'Constructor', 'Refinery', 'Smelter', 'Extractor'}
FLUID_CLASSES = {'OilExtractor', 'WaterExtractor', 'FluidStorageTank'}


def guid_map(folder, ext):
    out = {}
    for dp, _, fs in os.walk(folder):
        for f in fs:
            if f.endswith(ext + '.meta'):
                m = re.search(r'guid: (\w+)', open(os.path.join(dp, f), encoding='utf-8').read())
                if m:
                    out[m.group(1)] = os.path.join(dp, f[:-5])
    return out


def qrot(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    tx, ty, tz = 2 * (y * vz - z * vy), 2 * (z * vx - x * vz), 2 * (x * vy - y * vx)
    return (vx + w * tx + (y * tz - z * ty), vy + w * ty + (z * tx - x * tz), vz + w * tz + (x * ty - y * tx))


def vec(s, keys):
    m = dict(re.findall(r'(\w+): (-?[\d.e+-]+)', s))
    return tuple(float(m.get(k, 0)) for k in keys)


def prefab_ports():
    scripts = guid_map(os.path.join(ROOT, 'Assets', 'Script'), '.cs')
    datas = guid_map(os.path.join(ROOT, 'Assets', 'Data', 'Buildings'), '.asset')
    result = {}
    for f in sorted(os.listdir(PREFABS)):
        if not f.endswith('.prefab'):
            continue
        text = open(os.path.join(PREFABS, f), encoding='utf-8').read()
        docs = {}
        for m in re.finditer(r'--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=\n--- !u!|\Z)', text, re.S):
            docs[m.group(2)] = (m.group(1), m.group(3))
        tr_of_go, go_name = {}, {}
        for fid, (cls, body) in docs.items():
            if cls == '4':
                g = re.search(r'm_GameObject: \{fileID: (-?\d+)', body)
                if g:
                    tr_of_go[g.group(1)] = fid
            elif cls == '1':
                n = re.search(r'm_Name: (.*)', body)
                go_name[fid] = n.group(1).strip() if n else ''
        bld = None
        for fid, (cls, body) in docs.items():
            if cls == '114' and 'inputSockets:' in body:
                bld = body
                break
        if not bld:
            continue
        sg = re.search(r'm_Script: \{[^}]*guid: (\w+)', bld)
        cname = os.path.basename(scripts.get(sg.group(1), '')).replace('.cs', '') if sg else ''
        dg = re.search(r'\n  data: \{[^}]*guid: (\w+)', bld)
        if not dg or dg.group(1) not in datas:
            continue
        dtext = open(datas[dg.group(1)], encoding='utf-8').read()
        bid = re.search(r'\n  id: (\S+)', dtext).group(1)
        sz = re.search(r'\n  size: \{x: (\d+), y: (\d+)\}', dtext)
        sx, sy = (int(sz.group(1)), int(sz.group(2))) if sz else (1, 1)
        if bid not in RESTYLE_MODELS:
            continue

        def socket_list(key):
            m = re.search(key + r':\s*\n((?:\s*- \{fileID: -?\d+\}\n?)*)', bld)
            out = []
            for sid in re.findall(r'fileID: (-?\d+)', m.group(1) if m else ''):
                g = re.search(r'm_GameObject: \{fileID: (-?\d+)', docs.get(sid, ('', ''))[1])
                if g and g.group(1) in tr_of_go:
                    out.append((go_name.get(g.group(1), ''), tr_of_go[g.group(1)]))
            return out

        def local(tid):
            """позиция и forward сокета в осях корня префаба"""
            p, fwd = (0.0, 0.0, 0.0), (0.0, 0.0, 1.0)
            while True:
                body = docs[tid][1]
                father = re.search(r'm_Father: \{fileID: (-?\d+)', body).group(1)
                if father == '0':
                    return p, fwd
                q = vec(re.search(r'm_LocalRotation: \{[^}]*\}', body).group(0), 'xyzw')
                t = vec(re.search(r'm_LocalPosition: \{[^}]*\}', body).group(0), 'xyz')
                s = vec(re.search(r'm_LocalScale: \{[^}]*\}', body).group(0), 'xyz')
                p = qrot(q, (p[0] * s[0], p[1] * s[1], p[2] * s[2]))
                p = (p[0] + t[0], p[1] + t[1], p[2] + t[2])
                fwd = qrot(q, fwd)
                tid = father

        ins, outs = socket_list('inputSockets'), socket_list('outputSockets')
        place = {}
        for name, tid in ins + outs:
            place[tid] = local(tid)

        def code_socket(name, pos, fwd):  # сокет, который скрипт создаёт сам (FindOrCreateSocket)
            place['code:' + name] = (pos, fwd)
            return (name, 'code:' + name)
        if cname == 'ChemicalPlant':  # ChemicalPlant.EnsureSockets — всегда
            ins = [code_socket('InputSocket', (0, 0.3, -1.5), (0, 0, -1)),
                   code_socket('InputSocketFluid', (-1.5, 0.3, 0), (-1, 0, 0))]
            outs = [code_socket('OutPutSocket', (0, 0.3, 1.5), (0, 0, 1))]
        if cname in ('StorageContainer', 'FluidStorageTank'):  # StorageContainer.EnsureSockets — если пусто
            if not ins:
                ins = [code_socket('InputSocket', (0, 0.3, -0.5), (0, 0, -1))]
            if not outs:
                outs = [code_socket('OutputSocket', (0, 0.3, 0.5), (0, 0, 1))]
        if cname == 'PowerGenerator' and ins and not any('fluid' in n.lower() for n, _ in ins):  # PowerGenerator.EnsureSockets
            ins = [ins[0], code_socket('InputSocketFluid', (-0.48, 0.3, 0), (-1, 0, 0))]
        if cname in CRAFTERS or cname == 'PowerGenerator':  # ApplyPrimarySockets
            hz, hx = max(0.2, sy * 0.5 - 0.02), max(0.2, sx * 0.5 - 0.02)
            if ins:
                place[ins[0][1]] = ((0, 0.3, -hz), (0, 0, -1))
            if outs:
                place[outs[0][1]] = ((0, 0.3, hz), (0, 0, 1))
            if cname == 'Constructor' and len(ins) > 1:  # оба входа сзади
                place[ins[0][1]] = ((-sx * 0.25, 0.3, -hz), (0, 0, -1))
                place[ins[1][1]] = ((sx * 0.25, 0.3, -hz), (0, 0, -1))
            elif len(ins) > 1:
                fl = 'fluid' in ins[1][0].lower()
                place[ins[1][1]] = ((-hx, 0.3, 0), (-1, 0, 0)) if fl else ((hx, 0.3, 0), (1, 0, 0))

        hx, hz = sx * 0.5, sy * 0.5
        used, ports, warns = [], [], []
        number = cname in CRAFTERS and cname != 'Extractor' and len(ins) >= 2
        for is_in, lst in ((True, ins), (False, outs)):
            k = 0
            for i, (name, tid) in enumerate(lst):
                (lx, _, lz), (ox, _, oz) = place[tid]
                if ox * ox + oz * oz < 1e-4:
                    warns.append('Сокет «%s» смотрит вверх/вниз — порт не ставится' % name)
                    continue
                if abs(oz) >= abs(ox):
                    side = 1 if oz > 0 else -1
                    pos, yaw = (min(max(lx, -hx + 0.3), hx - 0.3), side * hz), (0 if side > 0 else 180)
                    if lz * side < -0.05:
                        warns.append('Сокет «%s» стоит у стороны z=%.2f, но смотрит в %sZ — порт уехал на другую сторону'
                                     % (name, lz, '+' if side > 0 else '−'))
                else:
                    side = 1 if ox > 0 else -1
                    pos, yaw = (side * hx, min(max(lz, -hz + 0.3), hz - 0.3)), (90 if side > 0 else 270)
                    if lx * side < -0.05:
                        warns.append('Сокет «%s» стоит у стороны x=%.2f, но смотрит в %sX — порт уехал на другую сторону'
                                     % (name, lx, '+' if side > 0 else '−'))
                if any((u[0] - pos[0]) ** 2 + (u[1] - pos[1]) ** 2 < 0.04 for u in used):
                    warns.append('Порт сокета «%s» совпал с другим портом и не рисуется' % name)
                    continue
                used.append(pos)
                fluid = (cname == 'Refinery' and is_in and i == 0) or cname in FLUID_CLASSES or 'fluid' in name.lower()
                model = 'port_fluid' if fluid else 'port_in' if is_in else 'port_out'
                k += 1 if is_in else 0
                ports.append({'model': model, 'x': round(pos[0], 3), 'z': round(pos[1], 3), 'yaw': yaw,
                              'socket': name, 'num': k if (is_in and number) else 0})
        for model in RESTYLE_MODELS[bid]:
            result[model] = {'ports': ports, 'size': [sx, sy], 'prefab': f, 'warns': warns}
    return result


def b64(path):
    return 'data:image/png;base64,' + base64.b64encode(open(path, 'rb').read()).decode()


def main():
    objs = {f[:-4]: io.open(os.path.join(GEN, f), encoding='utf-8').read()
            for f in sorted(os.listdir(GEN)) if f.endswith('.obj')}
    parts = json.load(open(os.path.join(GEN, 'parts.json'), encoding='utf-8'))['parts']
    part_names = {p['model'] for p in parts}
    mains = [n for n in objs if n not in part_names]

    # подвижная деталь цепляется к модели с самым длинным совпадающим префиксом
    attach = {}
    for p in parts:
        host = max((m for m in mains if p['model'].startswith(m + '_')), key=len, default=None)
        if host and p['model'] in objs:
            attach.setdefault(host, []).append({'model': p['model'], 'anim': p['anim'], 'pos': p['pos']})

    mtl = {}
    cur = None
    for line in io.open(os.path.join(GEN, 'wi.mtl'), encoding='utf-8'):
        t = line.split()
        if not t:
            continue
        if t[0] == 'newmtl':
            cur = t[1]
        elif t[0] == 'Kd' and cur:
            mtl[cur] = [float(x) for x in t[1:4]]

    ports = prefab_ports()
    models = [dict({'id': n, 'label': LABELS.get(n, n), 'cat': category(n), 'parts': attach.get(n, [])}, **ports.get(n, {}))
              for n in mains]
    order = ['Здания', 'Дроны', 'Конвейеры', 'Трубы', 'Порты']
    models.sort(key=lambda m: (order.index(m['cat']), m['label']))

    data = {
        'models': models, 'objs': objs, 'mtl': mtl,
        'albedo': b64(os.path.join(MATDIR, 'wi_palette_albedo.png')),
        'emit': b64(os.path.join(MATDIR, 'wi_palette_emit.png')),
    }
    tpl = io.open(os.path.join(os.path.dirname(__file__), 'model_review_template.html'), encoding='utf-8').read()
    html = tpl.replace('/*__DATA__*/null', json.dumps(data, ensure_ascii=False).replace('</', '<\\/'))
    io.open(OUT, 'w', encoding='utf-8', newline='\n').write(html)
    print('OK: %s (%d моделей, %d КБ)' % (OUT, len(models), len(html) // 1024))


if __name__ == '__main__':
    main()
