#!/usr/bin/env python3
"""Модели художника (GLB из Meshy, заказы MOD-…) → меш и текстура для Unity.

    python3 tools/glb_to_unity.py            # models/*.glb → unity/Assets/Resources/Models/
    python3 tools/glb_to_unity.py --check    # только проверить, что файлы в Unity совпадают с исходниками

Зачем не FBX: импорт FBX в Unity сам создаёт материалы на Standard (в APK он вырезается — всё розовое),
а оси и масштаб зависят от настроек импорта. Здесь всё явно и одинаково на любой машине:
- `<имя>.bytes` — свой простой формат (читает `Core/MeshPack.cs`):
  "PGM1", int32 число вершин, int32 число индексов, затем позиции (3 float), нормали (3 float),
  UV (2 float) и индексы (uint16), всё little-endian;
- `<имя>-tex.bytes` — текстура цвета в своём формате PGT1 (читает `Core/TexturePack.cs`): "PGT1", int32 ширина,
  int32 высота, затем пиксели RGB24 строками снизу вверх (как в Unity), сжатые raw deflate. Не PNG: импорт PNG
  с минимальным .meta Unity не загрузил, а `Texture2D.LoadImage` в проекте нет (модуль ImageConversion не подключён).
  PNG раскрывает Pillow (`pip install pillow`).
glTF правый, Unity левый: x меняет знак (как при переносе из браузерной версии), поэтому порядок вершин
в треугольниках разворачивается, а v текстуры переворачивается (в glTF v идёт сверху вниз).
"""
import argparse
import io
import zlib
import glob
import json
import os
import struct
import sys
import tempfile

TOP = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(TOP, 'models')
DST = os.path.join(TOP, 'unity', 'Assets', 'Resources', 'Models')

COMP = {5126: 'f', 5123: 'H', 5125: 'I', 5121: 'B'}
SIZE = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}


def read_glb(path):
    with open(path, 'rb') as fh:
        b = fh.read()
    magic, _, _ = struct.unpack_from('<4sII', b, 0)
    if magic != b'glTF':
        raise ValueError(path + ': не GLB')
    jlen, jtype = struct.unpack_from('<II', b, 12)
    doc = json.loads(b[20:20 + jlen])
    off = 20 + jlen
    blen, _ = struct.unpack_from('<II', b, off)
    binary = b[off + 8:off + 8 + blen]

    def accessor(i):
        a = doc['accessors'][i]
        bv = doc['bufferViews'][a['bufferView']]
        n = SIZE[a['type']]
        fmt = '<' + COMP[a['componentType']] * n
        step = bv.get('byteStride') or struct.calcsize(fmt)
        start = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
        return [struct.unpack_from(fmt, binary, start + k * step) for k in range(a['count'])]

    if len(doc['meshes']) != 1 or len(doc['meshes'][0]['primitives']) != 1:
        raise ValueError(path + ': ожидается один меш из одного куска')
    for node in doc.get('nodes', []):
        if any(k in node for k in ('matrix', 'translation', 'rotation', 'scale')):
            raise ValueError(path + ': у узла есть трансформ — ожидается запечённая сетка')
    prim = doc['meshes'][0]['primitives'][0]
    if prim.get('mode', 4) != 4:
        raise ValueError(path + ': ожидаются треугольники')
    at = prim['attributes']
    pos = accessor(at['POSITION'])
    nrm = accessor(at['NORMAL'])
    uv = accessor(at['TEXCOORD_0'])
    idx = [i[0] for i in accessor(prim['indices'])]
    img = doc['images'][0]
    bv = doc['bufferViews'][img['bufferView']]
    png = binary[bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']]
    if not png.startswith(b'\x89PNG'):
        raise ValueError(path + ': текстура не PNG')
    return pos, nrm, uv, idx, png


def convert(path):
    """Байты меша в формате PGM1 и PNG текстуры."""
    pos, nrm, uv, idx, png = read_glb(path)
    if len(pos) > 65535:
        raise ValueError(path + ': больше 65535 вершин')
    out = bytearray(b'PGM1')
    out += struct.pack('<ii', len(pos), len(idx))
    for x, y, z in pos:
        out += struct.pack('<3f', -x, y, z)
    for x, y, z in nrm:
        out += struct.pack('<3f', -x, y, z)
    for u, v in uv:
        out += struct.pack('<2f', u, 1.0 - v)
    for t in range(0, len(idx), 3):
        a, b, c = idx[t:t + 3]
        out += struct.pack('<3H', a, c, b)  # зеркало по x — обратный обход
    return bytes(out), png


def texture(png):
    """PNG → PGT1: RGB24 строками снизу вверх, raw deflate (уровень 9, детерминированно)."""
    from PIL import Image
    im = Image.open(io.BytesIO(png)).convert('RGB')
    w, h = im.size
    raw = im.transpose(Image.FLIP_TOP_BOTTOM).tobytes()
    c = zlib.compressobj(9, zlib.DEFLATED, -15)
    return b'PGT1' + struct.pack('<ii', w, h) + c.compress(raw) + c.flush()


def build(dst):
    os.makedirs(dst, exist_ok=True)
    made = []
    for path in sorted(glob.glob(os.path.join(SRC, '*.glb'))):
        name = os.path.splitext(os.path.basename(path))[0]
        mesh, png = convert(path)
        for fname, data in ((name + '.bytes', mesh), (name + '-tex.bytes', texture(png))):
            with open(os.path.join(dst, fname), 'wb') as fh:
                fh.write(data)
            made.append(fname)
    return made


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()
    if not args.check:
        for f in build(DST):
            print('готово', f)
        return 0
    with tempfile.TemporaryDirectory() as tmp:
        bad = []
        for f in build(tmp):
            mine = os.path.join(DST, f)
            if not os.path.exists(mine) or open(mine, 'rb').read() != open(os.path.join(tmp, f), 'rb').read():
                bad.append(f)
        for f in bad:
            print('не совпадает с models/:', f)
        return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
