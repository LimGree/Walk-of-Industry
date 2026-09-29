# Walk of Industry — библиотека low-poly моделей: примитивы, OBJ/MTL, рендер иконок.
# Координаты Unity: x вправо, y вверх, z вперёд (выход здания). Метры, pivot = центр footprint, y=0 земля.
import math, os, struct, zlib
import numpy as np

MATS = {
    'wi_teal':    ((0.10, 0.24, 0.31), 0, 0.00),
    'wi_edge':    ((0.10, 0.24, 0.31), 0, 0.00),
    'wi_teal2':   ((0.16, 0.36, 0.44), 0, 0.00),
    'wi_copper':  ((0.74, 0.43, 0.20), 0, 0.15),
    'wi_base':    ((0.33, 0.21, 0.13), 0, 0.00),
    'wi_dark':    ((0.07, 0.07, 0.08), 0, 0.05),
    'wi_rubber':  ((0.13, 0.13, 0.14), 0, 0.00),
    'wi_steel':   ((0.52, 0.55, 0.58), 0, 0.35),
    'wi_yellow':  ((0.95, 0.74, 0.16), 0, 0.00),
    'wi_crate':   ((0.62, 0.44, 0.24), 0, 0.00),
    'wi_brick':   ((0.55, 0.27, 0.19), 0, 0.00),
    'wi_stone':   ((0.46, 0.44, 0.41), 0, 0.00),
    'wi_glass':   ((0.45, 0.70, 0.80), 0, 0.60),
    'wi_water':   ((0.20, 0.45, 0.70), 0, 0.50),
    'wi_chem':    ((0.35, 0.72, 0.30), 0, 0.30),
    'wi_white':   ((0.85, 0.86, 0.84), 0, 0.10),
    'wi_lamp':    ((0.35, 1.00, 0.45), 1, 0.00),
    'wi_fire':    ((1.00, 0.52, 0.12), 1, 0.00),
    'wi_red':     ((0.90, 0.20, 0.14), 1, 0.00),
    'wi_blue':    ((0.30, 0.65, 1.00), 1, 0.00),
}


def rot_matrix(rx=0.0, ry=0.0, rz=0.0):
    # Unity: Z, потом X, потом Y (углы в градусах)
    a, b, c = (math.radians(v) for v in (rx, ry, rz))
    Rx = np.array([[1, 0, 0], [0, math.cos(a), -math.sin(a)], [0, math.sin(a), math.cos(a)]])
    Ry = np.array([[math.cos(b), 0, math.sin(b)], [0, 1, 0], [-math.sin(b), 0, math.cos(b)]])
    Rz = np.array([[math.cos(c), -math.sin(c), 0], [math.sin(c), math.cos(c), 0], [0, 0, 1]])
    return Ry @ Rx @ Rz


def basis_from(p0, p1):
    f = np.array(p1, float) - np.array(p0, float)
    L = np.linalg.norm(f)
    f = f / L
    up = np.array((0, 1, 0)) if abs(f[1]) < 0.95 else np.array((1, 0, 0))
    r = np.cross(up, f); r /= np.linalg.norm(r)
    u = np.cross(f, r)
    return np.stack([r, u, f], 1), L


class Model:
    def __init__(self):
        self.objects = {}

    def face(self, obj, verts, mat, center, uvs=None):
        v = [np.array(p, float) for p in verts]
        n = np.cross(v[1] - v[0], v[2] - v[0])
        if np.linalg.norm(n) < 1e-12:
            return
        fc = sum(v) / len(v)
        if np.dot(n, fc - np.array(center, float)) < 0:
            v = v[::-1]
            if uvs is not None:
                uvs = uvs[::-1]
        self.objects.setdefault(obj, []).append((v, mat, uvs))

    def box(self, obj, c, s, mat, yaw=0.0, rx=0.0, rz=0.0, R=None, scroll_top=False):
        R = rot_matrix(rx, yaw, rz) if R is None else R
        c = np.array(c, float)
        h = np.array(s, float) / 2
        corners = {}
        for ix in (-1, 1):
            for iy in (-1, 1):
                for iz in (-1, 1):
                    corners[(ix, iy, iz)] = c + R @ (h * np.array((ix, iy, iz)))
        quads = [[(-1,-1,-1),(-1,1,-1),(-1,1,1),(-1,-1,1)], [(1,-1,-1),(1,1,-1),(1,1,1),(1,-1,1)],
                 [(-1,-1,-1),(1,-1,-1),(1,-1,1),(-1,-1,1)], [(-1,1,-1),(1,1,-1),(1,1,1),(-1,1,1)],
                 [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1)], [(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        for qi, q in enumerate(quads):
            pts = [corners[k] for k in q]
            uvs = [(1.0, float(p[2])) for p in pts] if (scroll_top and qi == 3) else None
            self.face(obj, pts, mat, c, uvs)

    def beam(self, obj, p0, p1, w, h, mat):
        """Брус от p0 до p1 сечением w×h."""
        R, L = basis_from(p0, p1)
        c = (np.array(p0, float) + np.array(p1, float)) / 2
        self.box(obj, c, (w, h, L), mat, R=R)

    def tube(self, obj, p0, p1, r, mat, sides=8, r2=None, caps=True, cap_mat=None):
        """Цилиндр/конус по оси p0→p1."""
        R, L = basis_from(p0, p1)
        r2 = r if r2 is None else r2
        p0 = np.array(p0, float)
        ring = lambda rad, t: [p0 + R @ np.array((rad * math.cos(2*math.pi*i/sides + math.pi/sides),
                                                   rad * math.sin(2*math.pi*i/sides + math.pi/sides), t)) for i in range(sides)]
        a, b = ring(r, 0.0), ring(r2, L)
        ctr = p0 + R @ np.array((0, 0, L / 2))
        for i in range(sides):
            j = (i + 1) % sides
            if r2 > 1e-4:
                self.face(obj, [a[i], a[j], b[j], b[i]], mat, ctr)
            else:
                self.face(obj, [a[i], a[j], b[0]], mat, ctr)
        if caps:
            self.face(obj, a, cap_mat or mat, ctr)
            if r2 > 1e-4:
                self.face(obj, b, cap_mat or mat, ctr)

    def cyl(self, obj, c, r, h, mat, sides=8, r2=None, cap_mat=None):
        x, y, z = c
        self.tube(obj, (x, y, z), (x, y + h, z), r, mat, sides, r2, True, cap_mat)

    def roof(self, obj, c, sx, sz, h, mat, over=0.0):
        cx, cy, cz = c
        hx, hz = sx / 2 + over, sz / 2 + over
        a, b, cc, d = (cx-hx, cy, cz-hz), (cx+hx, cy, cz-hz), (cx+hx, cy, cz+hz), (cx-hx, cy, cz+hz)
        ridge = max(0.0, (sx - sz) / 2)
        t1, t2 = (cx - ridge, cy + h, cz), (cx + ridge, cy + h, cz)
        ctr = (cx, cy + h * 0.3, cz)
        if ridge > 0:
            self.face(obj, [a, b, t2, t1], mat, ctr)
            self.face(obj, [cc, d, t1, t2], mat, ctr)
        else:
            self.face(obj, [a, b, t1], mat, ctr)
            self.face(obj, [cc, d, t1], mat, ctr)
        self.face(obj, [b, cc, t2], mat, ctr)
        self.face(obj, [d, a, t1], mat, ctr)
        self.face(obj, [a, b, cc, d], mat, ctr)

    def gable(self, obj, c, sx, sz, h, mat, over=0.0):
        """Двускатная крыша, конёк вдоль X."""
        cx, cy, cz = c
        hx, hz = sx / 2 + over, sz / 2 + over
        a, b, cc, d = (cx-hx, cy, cz-hz), (cx+hx, cy, cz-hz), (cx+hx, cy, cz+hz), (cx-hx, cy, cz+hz)
        t1, t2 = (cx - hx, cy + h, cz), (cx + hx, cy + h, cz)
        ctr = (cx, cy + h * 0.3, cz)
        self.face(obj, [a, b, t2, t1], mat, ctr)
        self.face(obj, [cc, d, t1, t2], mat, ctr)
        self.face(obj, [b, cc, t2], mat, ctr)
        self.face(obj, [d, a, t1], mat, ctr)
        self.face(obj, [a, b, cc, d], mat, ctr)

    def chevron(self, obj, c, travel_deg, mat, size=0.2, w=0.055, th=0.012):
        """Шеврон «>» на горизонтали, острие по направлению движения. travel_deg: 0 = +Z, 90 = +X."""
        x, y, z = c
        t = math.radians(travel_deg)
        f = np.array((math.sin(t), 0, math.cos(t)))
        r = np.array((math.cos(t), 0, -math.sin(t)))
        tip = np.array((x, y, z)) + f * size * 0.35
        for s in (-1, 1):
            tail = tip - f * size * 0.55 + r * s * size * 0.55
            self.beam(obj, tip + (tail - tip) * 0.0, tail, w, th, mat)
        return self

    def arc_strip(self, obj, center, r_in, r_out, a0, a1, y0, y1, mat, seg=6, scroll=False):
        """Кольцевой сектор (угол от +X против часовой в плоскости XZ, a — градусы), высота y0..y1."""
        cx, cz = center
        for i in range(seg):
            t0 = math.radians(a0 + (a1 - a0) * i / seg)
            t1 = math.radians(a0 + (a1 - a0) * (i + 1) / seg)
            p = lambda r, t, y: (cx + r * math.cos(t), y, cz + r * math.sin(t))
            q = [p(r_in, t0, y0), p(r_out, t0, y0), p(r_out, t1, y0), p(r_in, t1, y0),
                 p(r_in, t0, y1), p(r_out, t0, y1), p(r_out, t1, y1), p(r_in, t1, y1)]
            mid_r, mid_t = (r_in + r_out) / 2, (t0 + t1) / 2
            ctr = p(mid_r, mid_t, (y0 + y1) / 2)
            idx = [(0,1,2,3),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
            mid = (r_in + r_out) / 2
            v0 = abs(math.radians(a0 + (a1 - a0) * i / seg) - math.radians(a0)) * mid
            v1 = abs(math.radians(a0 + (a1 - a0) * (i + 1) / seg) - math.radians(a0)) * mid
            for fi, f in enumerate(idx):
                uvs = [(1.0, v0), (1.0, v0), (1.0, v1), (1.0, v1)] if (scroll and fi == 1) else None
                self.face(obj, [q[k] for k in f], mat, ctr, uvs)

    def merge(self, other, dx=0, dy=0, dz=0, yaw=0.0, prefix=''):
        R = rot_matrix(0, yaw, 0)
        off = np.array((dx, dy, dz), float)
        for name, faces in other.objects.items():
            for verts, mat, _uv in faces:
                self.objects.setdefault(prefix + name, []).append(([R @ v + off for v in verts], mat, None))
        return self

    def mirror_x(self):
        """Зеркальная копия по X (левый вариант из правого). Обход граней разворачивается."""
        out = Model()
        for name, faces in self.objects.items():
            for verts, mat, uvs in faces:
                out.objects.setdefault(name, []).append(([np.array((-v[0], v[1], v[2])) for v in verts][::-1], mat,
                                                         uvs[::-1] if uvs is not None else None))
        return out

    def split(self, obj, pivot):
        """Вырезать объект obj в отдельную модель с центром в pivot (для анимации)."""
        part = Model()
        p = np.array(pivot, float)
        for verts, mat, uvs in self.objects.pop(obj, []):
            part.objects.setdefault('Body', []).append(([v - p for v in verts], mat, uvs))
        return part

    def write_obj(self, path, mtl_name='wi.mtl'):
        lines = ['# Walk of Industry low-poly (generated, wi_lib.py)', 'mtllib ' + mtl_name, 'vt 0 0']
        vi, ti = 1, 2
        for name, faces in self.objects.items():
            lines.append('o ' + name)
            cur = None
            for verts, mat, uvs in faces:
                if mat != cur:
                    lines.append('usemtl ' + mat)
                    cur = mat
                idx = []
                for k, p in enumerate(verts):
                    lines.append('v %.4f %.4f %.4f' % (-p[0], p[1], p[2]))
                    if uvs is not None:
                        lines.append('vt %.4f %.4f' % uvs[k])
                        idx.append('%d/%d' % (vi, ti))
                        ti += 1
                    else:
                        idx.append('%d/1' % vi)
                    vi += 1
                lines.append('f ' + ' '.join(reversed(idx)))
        with open(path, 'w', newline='\n') as f:
            f.write('\n'.join(lines) + '\n')


def write_mtl(path):
    out = []
    for name, (col, emis, gloss) in MATS.items():
        out += ['newmtl ' + name, 'Kd %.3f %.3f %.3f' % col, 'Ka 0 0 0', 'Ks 0.05 0.05 0.05']
        if emis:
            out.append('Ke %.3f %.3f %.3f' % col)
        out += ['d 1', '']
    with open(path, 'w', newline='\n') as f:
        f.write('\n'.join(out))


def render(models, path, size=256, yaw=-35, pitch=30, ground=None):
    W = H = size * 2
    tris = []
    for mdl, off, rot in models:
        R = rot_matrix(0, rot, 0)
        for faces in mdl.objects.values():
            for verts, mat, _uv in faces:
                vv = [R @ p + np.array(off, float) for p in verts]
                for i in range(1, len(vv) - 1):
                    tris.append((vv[0], vv[i], vv[i + 1], mat))
    cy_, sy_ = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    def view(p):
        x = p[0] * cy_ - p[2] * sy_
        z = p[0] * sy_ + p[2] * cy_
        y = p[1]
        return np.array((x, y * cp + z * sp, z * cp - y * sp))
    light = np.array((0.45, 0.8, -0.4)); light /= np.linalg.norm(light)
    vt = [(view(a), view(b), view(c), mat, np.cross(b - a, c - a)) for a, b, c, mat in tris]
    allp = np.array([p for t in vt for p in t[:3]])
    mn, mx = allp.min(0), allp.max(0)
    span = max(mx[0] - mn[0], mx[1] - mn[1]) * 1.12
    cxv, cyv = (mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2
    def scr(p):
        return ((p[0] - cxv) / span + 0.5) * W, (0.5 - (p[1] - cyv) / span) * H, p[2]
    img = np.zeros((H, W, 4), float)
    zb = np.full((H, W), 1e9)
    for a, b, c, mat, n in vt:
        ln = np.linalg.norm(n)
        if ln < 1e-12:
            continue
        n = n / ln
        col, emis, _ = MATS[mat]
        shade = 1.0 if emis else 0.42 + 0.58 * max(0.0, float(np.dot(n, light)))
        rgb = np.array(col) * shade
        (x0, y0, z0), (x1, y1, z1), (x2, y2, z2) = scr(a), scr(b), scr(c)
        minx, maxx = int(max(0, math.floor(min(x0, x1, x2)))), int(min(W - 1, math.ceil(max(x0, x1, x2))))
        miny, maxy = int(max(0, math.floor(min(y0, y1, y2)))), int(min(H - 1, math.ceil(max(y0, y1, y2))))
        if minx > maxx or miny > maxy:
            continue
        den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(den) < 1e-9:
            continue
        xs, ys = np.meshgrid(np.arange(minx, maxx + 1) + 0.5, np.arange(miny, maxy + 1) + 0.5)
        w0 = ((y1 - y2) * (xs - x2) + (x2 - x1) * (ys - y2)) / den
        w1 = ((y2 - y0) * (xs - x2) + (x0 - x2) * (ys - y2)) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
        z = w0 * z0 + w1 * z1 + w2 * z2
        sub = zb[miny:maxy + 1, minx:maxx + 1]
        upd = inside & (z < sub)
        sub[upd] = z[upd]
        img[miny:maxy + 1, minx:maxx + 1][upd] = np.append(rgb, 1.0)
    img = img.reshape(size, 2, size, 2, 4).mean((1, 3))
    rgb = np.where(img[..., 3:4] > 0, img[..., :3] / np.maximum(img[..., 3:4], 1e-6), 0)
    out = np.concatenate([rgb, img[..., 3:4]], -1)
    out = (np.clip(out, 0, 1) ** np.array((1/1.15, 1/1.15, 1/1.15, 1)) * 255).astype(np.uint8)
    raw = b''.join(b'\x00' + out[y].tobytes() for y in range(size))
    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    png = (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))
    with open(path, 'wb') as f:
        f.write(png)
