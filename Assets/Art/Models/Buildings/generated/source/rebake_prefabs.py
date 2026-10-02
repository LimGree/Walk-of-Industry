# Перезапись префабов зданий: старые модели вон, новые OBJ (Resources/Models) внутрь.
import io, os, re, sys, glob, random
ROOT = sys.argv[1]
A = os.path.join(ROOT, 'Assets')
PF = os.path.join(A, 'Prefabs', 'Buildings')
OLD_DIR = os.path.join(A, 'Art', 'Models', 'Buildings', 'old models build')
DRY = '--dry' in sys.argv

random.seed(4242)
def nid():
    return str(random.randint(10**17, 9 * 10**18))

def guid_of(meta):
    return re.search(r'^guid: (\w+)', io.open(meta, encoding='utf-8').read(), re.M).group(1)

OLD = set(guid_of(m) for m in glob.glob(os.path.join(OLD_DIR, '**', '*.meta'), recursive=True))
OLD.add(guid_of(os.path.join(PF, 'Conveyor_01.prefab.meta')))      # вложенная лента в НПЗ
OBJ = {os.path.basename(m)[:-9]: guid_of(m) for m in glob.glob(os.path.join(A, 'Resources', 'Models', '*.obj.meta'))}
SOCKET_GUID = 'f655be442ca775b47b4ff5b7e77acd63'

CONFIG = {
    'Assembler_01.prefab': {'levels': [('Assembler_Level_1', 'assembler_1'), ('Assembler_level_2', 'assembler_2')], 'col': 1},
    'Extractor_01.prefab': {'levels': [('extractor_level_1', 'extractor_1'), ('extractor_level_2', 'extractor_2')], 'col': 1},
    'Smelter_01.prefab': {'model': 'smelter', 'col': 1},
    'Constructor_01.prefab': {'model': 'constructor', 'col': 2},
    'ResearchLab.prefab': {'model': 'research_lab', 'col': 1},
    'StorageContainer.prefab': {'model': 'storage_container', 'col': 1},
    'PowerGenerator.prefab': {'model': 'power_generator', 'col': 1},
    'chemical_plant.prefab': {'model': 'chemical_plant', 'col': 3},
    'refinery.prefab': {'model': 'refinery', 'col': 3},
    'fluid_storage_tank.prefab': {'model': 'fluid_storage_tank', 'col': 1},
    'oil_extractor.prefab': {'model': 'oil_extractor', 'col': 2},
    'water_extractor.prefab': {'model': 'water_extractor', 'col': 2},
    'robotic_arm .prefab': {'model': 'robotic_arm', 'col': 1},
    'Splitter.prefab': {'model': 'splitter', 'col': 1},
    'Conveer_underground_in.prefab': {'model': 'underground_in', 'col': 1},
    'Conveer_underground_out.prefab': {'model': 'underground_out', 'col': 1},
    'Conveyor_01.prefab': {'belt': 'belt_straight'},
    'Pipe.prefab': {'belt': 'pipe_straight'},
    'Conveyor_Corner.prefab': {'form': 'belt_corner_r'},
    'Conveyor_Tee.prefab': {'form': 'belt_tee_r'},
    'Conveyor_Sides.prefab': {'form': 'belt_sides'},
    'Conveyor_Triple.prefab': {'form': 'belt_triple'},
    'Pipe_corner.prefab': {'form': 'pipe_corner_r'},
}

HDR = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*$', re.M)

def parse(text):
    head_end = HDR.search(text).start()
    head = text[:head_end]
    docs = []
    ms = list(HDR.finditer(text))
    for i, m in enumerate(ms):
        body = text[m.end():ms[i + 1].start() if i + 1 < len(ms) else len(text)]
        docs.append({'cls': m.group(1), 'id': m.group(2), 'stripped': bool(m.group(3)), 'body': body})
    return head, docs

def dump(head, docs):
    out = [head]
    for d in docs:
        out.append('--- !u!%s &%s%s%s' % (d['cls'], d['id'], ' stripped' if d['stripped'] else '', d['body']))
    return ''.join(out)

def field(body, name):
    m = re.search(r'^  %s: \{fileID: (-?\d+)' % re.escape(name), body, re.M)
    return m.group(1) if m else None

def children(body):
    m = re.search(r'  m_Children:(.*?)\n  m_Father', body, re.S)
    if not m or m.group(1).strip() in ('', '[]'):
        return []
    return re.findall(r'fileID: (-?\d+)', m.group(1))

def set_children(d, ids):
    lst = ' []' if not ids else '\n' + ''.join('  - {fileID: %s}\n' % i for i in ids).rstrip('\n')
    d['body'] = re.sub(r'  m_Children:.*?\n(?=  m_Father)', '  m_Children:%s\n' % lst, d['body'], count=1, flags=re.S)

GO_T = '''
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
%(comps)s
  m_Layer: 6
  m_Name: %(name)s
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: %(active)d
'''
TR_T = '''
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %(go)s}
  serializedVersion: 2
  m_LocalRotation: {x: %(qx)s, y: %(qy)s, z: %(qz)s, w: %(qw)s}
  m_LocalPosition: {x: %(px)s, y: %(py)s, z: %(pz)s}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:%(children)s
  m_Father: {fileID: %(father)s}
  m_LocalEulerAnglesHint: {x: 0, y: %(yaw)s, z: 0}
'''
BOX_T = '''
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %(go)s}
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {x: %(sx)s, y: %(sy)s, z: %(sx)s}
  m_Center: {x: 0, y: %(cy)s, z: 0}
'''

def mod(g, fid, path, value):
    return ('    - target: {fileID: %s, guid: %s, type: 3}\n      propertyPath: %s\n      value: %s\n      objectReference: {fileID: 0}\n'
            % (fid, g, path, value))

def obj_instance(model, parent_tr, name):
    g = OBJ[model]
    pid, sid = nid(), nid()
    R = '-8679921383154817045'
    G0 = '919132149155446097'
    mods = ''.join(mod(g, R, p, v) for p, v in (
        ('m_LocalPosition.x', 0), ('m_LocalPosition.y', 0), ('m_LocalPosition.z', 0),
        ('m_LocalRotation.w', 1), ('m_LocalRotation.x', 0), ('m_LocalRotation.y', 0), ('m_LocalRotation.z', 0),
        ('m_LocalEulerAnglesHint.x', 0), ('m_LocalEulerAnglesHint.y', 0), ('m_LocalEulerAnglesHint.z', 0)))
    mods += mod(g, G0, 'm_Name', name) + mod(g, G0, 'm_Layer', 6)
    body = ('\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n'
            '    m_TransformParent: {fileID: %s}\n    m_Modifications:\n%s'
            '    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n'
            '  m_SourcePrefab: {fileID: 100100000, guid: %s, type: 3}\n') % (parent_tr, mods, g)
    stripped = ('\nTransform:\n  m_CorrespondingSourceObject: {fileID: -8679921383154817045, guid: %s, type: 3}\n'
                '  m_PrefabInstance: {fileID: %s}\n  m_PrefabAsset: {fileID: 0}\n') % (g, pid)
    return [{'cls': '1001', 'id': pid, 'stripped': False, 'body': body},
            {'cls': '4', 'id': sid, 'stripped': True, 'body': stripped}], sid

def plain(name, father, pos=(0, 0, 0), rot=(0, 0, 0, 1), yaw=0, active=1, extra_comps=()):
    go, tr = nid(), nid()
    comps = '\n'.join('  - component: {fileID: %s}' % c for c in (tr,) + tuple(extra_comps))
    gdoc = {'cls': '1', 'id': go, 'stripped': False, 'body': GO_T % {'comps': comps, 'name': name, 'active': active}}
    tdoc = {'cls': '4', 'id': tr, 'stripped': False, 'body': TR_T % {
        'go': go, 'qx': rot[0], 'qy': rot[1], 'qz': rot[2], 'qw': rot[3],
        'px': pos[0], 'py': pos[1], 'pz': pos[2], 'children': ' []', 'father': father, 'yaw': yaw}}
    return gdoc, tdoc

COL = {1: (0.9, 1.0), 2: (1.8, 1.3), 3: (2.7, 1.8)}

def rebake(path, cfg):
    text = io.open(path, encoding='utf-8').read()
    head, docs = parse(text)
    byid = {d['id']: d for d in docs}
    tr_of_go, go_of_tr = {}, {}
    for d in docs:
        if d['cls'] == '4' and not d['stripped']:
            go = field(d['body'], 'm_GameObject')
            tr_of_go[go] = d['id']
            go_of_tr[d['id']] = go
    root_tr = [d for d in docs if d['cls'] == '4' and not d['stripped'] and field(d['body'], 'm_Father') == '0'][0]
    root_go = go_of_tr[root_tr['id']]
    inst_of_stripped = {d['id']: field(d['body'], 'm_PrefabInstance') for d in docs if d['stripped']}
    inst_src = {}
    for d in docs:
        if d['cls'] == '1001':
            m = re.search(r'm_SourcePrefab: \{fileID: -?\d+, guid: (\w+)', d['body'])
            inst_src[d['id']] = m.group(1) if m else ''

    def subtree(tr):
        """(normal transforms, instances) под tr включительно."""
        normals, insts = [], []
        stack = [tr]
        while stack:
            t = stack.pop()
            if t in inst_of_stripped:
                insts.append(inst_of_stripped[t])
                continue
            normals.append(t)
            stack.extend(children(byid[t]['body']))
        return normals, insts

    def has_old(normals, insts):
        if any(inst_src.get(i) in OLD for i in insts):
            return True
        for t in normals:
            go = go_of_tr.get(t)
            gdoc = byid.get(go)
            if not gdoc:
                continue
            for c in re.findall(r'component: \{fileID: (-?\d+)\}', gdoc['body']):
                cd = byid.get(c)
                if cd and re.search(r'guid: (\w+)', cd['body']) and any(g in OLD for g in re.findall(r'guid: (\w+)', cd['body'])):
                    return True
        return False

    def has_socket(normals):
        for t in normals:
            gdoc = byid.get(go_of_tr.get(t))
            if not gdoc:
                continue
            for c in re.findall(r'component: \{fileID: (-?\d+)\}', gdoc['body']):
                cd = byid.get(c)
                if cd and SOCKET_GUID in cd['body']:
                    return True
            nm = re.search(r'^  m_Name: (.*)$', gdoc['body'], re.M)
            n = nm.group(1).strip() if nm else ''
            if 'Socket' in n or 'Point' in n or n in ('front', 'back'):
                return True
        return False

    remove = set()
    kept_children = []
    removed_names = []
    for c in children(root_tr['body']):
        normals, insts = subtree(c)
        if has_old(normals, insts) and not has_socket(normals):
            for t in normals:
                remove.add(t)
                go = go_of_tr.get(t)
                if go:
                    remove.add(go)
                    for comp in re.findall(r'component: \{fileID: (-?\d+)\}', byid[go]['body']):
                        remove.add(comp)
            for i in insts:
                remove.add(i)
                for sid, iid in inst_of_stripped.items():
                    if iid == i:
                        remove.add(sid)
            removed_names.append(c)
        else:
            kept_children.append(c)

    # Каскад: компоненты/экземпляры, прицепленные к удалённому (m_AddedComponents/m_AddedGameObjects старых моделей).
    changed = True
    while changed:
        changed = False
        for d in docs:
            if d['id'] in remove:
                continue
            refs = [field(d['body'], k) for k in ('m_GameObject', 'm_PrefabInstance')]
            m = re.search(r'm_TransformParent: \{fileID: (-?\d+)\}', d['body'])
            if m:
                refs.append(m.group(1))
            if d['stripped'] or d['cls'] != '4':
                pass
            if any(r in remove for r in refs if r and r != '0'):
                remove.add(d['id'])
                changed = True

    docs = [d for d in docs if d['id'] not in remove]
    for d in docs:
        for rid in remove:
            d['body'] = d['body'].replace('{fileID: %s}' % rid, '{fileID: 0}')
    byid = {d['id']: d for d in docs}
    root_tr = byid[root_tr['id']]
    new_docs, new_children = [], []
    rtr = root_tr['id']

    if 'levels' in cfg or 'model' in cfg:
        g, t = plain('WiVisual', rtr)
        new_docs += [g, t]
        new_children.append(t['id'])
        if 'model' in cfg:
            inst, sid = obj_instance(cfg['model'], t['id'], 'WiModel')
            new_docs += inst
            set_children(t, [sid])
        for n, (holder, model) in enumerate(cfg.get('levels', [])):
            hg, ht = plain(holder, rtr, active=1 if n == 0 else 0)
            inst, sid = obj_instance(model, ht['id'], model)
            set_children(ht, [sid])
            new_docs += [hg, ht] + inst
            new_children.append(ht['id'])
    elif 'belt' in cfg:
        bg, bt = plain('BeltVisual', rtr)
        wg, wt = plain('WiBelt', bt['id'])
        inst, sid = obj_instance(cfg['belt'], wt['id'], cfg['belt'])
        set_children(bt, [wt['id']])
        set_children(wt, [sid])
        new_docs += [bg, bt, wg, wt] + inst
        new_children.append(bt['id'])
    elif 'form' in cfg:
        wg, wt = plain('WiBelt', rtr)
        inst, sid = obj_instance(cfg['form'], wt['id'], cfg['form'])
        set_children(wt, [sid])
        new_docs += [wg, wt] + inst
        new_children.append(wt['id'])

    set_children(root_tr, kept_children + new_children)

    if cfg.get('col'):
        rgo = byid[root_go]
        has_box = any(byid.get(c, {}).get('cls') == '65' for c in re.findall(r'component: \{fileID: (-?\d+)\}', rgo['body']))
        if not has_box:
            sx, sy = COL[cfg['col']]
            cid = nid()
            new_docs.append({'cls': '65', 'id': cid, 'stripped': False,
                             'body': BOX_T % {'go': root_go, 'sx': sx, 'sy': sy, 'cy': sy / 2}})
            rgo['body'] = rgo['body'].replace('  m_Layer:', '  - component: {fileID: %s}\n  m_Layer:' % cid, 1)

    docs += new_docs
    out = dump(head, docs)
    left = [g for g in OLD if g in out]
    if not DRY:
        io.open(path, 'w', encoding='utf-8', newline='').write(out)
    print('%-32s removed %d, old refs left %d' % (os.path.basename(path), len(removed_names), len(left)))

for name, cfg in CONFIG.items():
    rebake(os.path.join(PF, name), cfg)

# ---------- новый префаб трубного сплиттера ----------
def pipe_splitter_prefab():
    head = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    root_go, root_tr, mb, box = nid(), nid(), nid(), nid()
    docs = []
    gdoc = {'cls': '1', 'id': root_go, 'stripped': False, 'body': GO_T % {
        'comps': '  - component: {fileID: %s}\n  - component: {fileID: %s}\n  - component: {fileID: %s}' % (root_tr, mb, box),
        'name': 'PipeSplitter', 'active': 1}}
    sockets = [('InputSocket', 0, (0, 0.3, -0.5), (0, 1, 0, 0), 180),
               ('OutputSocket', 1, (0, 0.3, 0.5), (0, 0, 0, 1), 0),
               ('OutputSocket (1)', 1, (0.5, 0.3, 0), (0, 0.7071068, 0, 0.7071068), 90),
               ('OutputSocket (2)', 1, (-0.5, 0.3, 0), (0, -0.7071068, 0, 0.7071068), 270)]
    child_trs, ins, outs = [], [], []
    for name, typ, pos, rot, yaw in sockets:
        sb = nid()
        sg, st = plain(name, root_tr, pos, rot, yaw, extra_comps=(sb,))
        sdoc = {'cls': '114', 'id': sb, 'stripped': False, 'body': '''
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  socketType: %d
  connectedSocket: {fileID: 0}
  connectionIndicator: {fileID: 0}
  showDebug: 0
''' % (sg['id'], SOCKET_GUID, typ)}
        docs += [sg, st, sdoc]
        child_trs.append(st['id'])
        (ins if typ == 0 else outs).append(sb)
    vg, vt = plain('WiVisual', root_tr)
    inst, sid = obj_instance('pipe_splitter', vt['id'], 'WiModel')
    set_children(vt, [sid])
    docs += [vg, vt] + inst
    child_trs.append(vt['id'])
    tdoc = {'cls': '4', 'id': root_tr, 'stripped': False, 'body': TR_T % {
        'go': root_go, 'qx': 0, 'qy': 0, 'qz': 0, 'qw': 1, 'px': 0, 'py': 0, 'pz': 0,
        'children': ' []', 'father': 0, 'yaw': 0}}
    set_children(tdoc, child_trs)
    mdoc = {'cls': '114', 'id': mb, 'stripped': False, 'body': '''
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %s}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3c7e9a1b5d2f4a8c9e0b1d3f5a7c9e2b, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  data: {fileID: 11400000, guid: 4d8f0b2c6e3a4b9d8f1c2e4a6b8d0f35, type: 2}
  inputSockets:
%s
  outputSockets:
%s
  maxOutputBuffer: 8
  drawFootprintGizmo: 1
''' % (root_go, '\n'.join('  - {fileID: %s}' % i for i in ins), '\n'.join('  - {fileID: %s}' % o for o in outs))}
    bdoc = {'cls': '65', 'id': box, 'stripped': False, 'body': BOX_T % {'go': root_go, 'sx': 0.8, 'sy': 0.7, 'cy': 0.35}}
    all_docs = [gdoc, tdoc, mdoc, bdoc] + docs
    path = os.path.join(PF, 'PipeSplitter.prefab')
    meta = path + '.meta'
    if not os.path.exists(meta):
        io.open(meta, 'w', encoding='utf-8', newline='\n').write(
            'fileFormatVersion: 2\nguid: 5e9a1c3d7b4f4e6a8d0c2b4e6f8a1c47\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    if not DRY:
        io.open(path, 'w', encoding='utf-8', newline='\n').write(dump(head, all_docs))
    print('PipeSplitter.prefab created, root', root_go)
    return root_go

rg = pipe_splitter_prefab()
io.open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'pipe_splitter_root.txt'), 'w').write(rg)
