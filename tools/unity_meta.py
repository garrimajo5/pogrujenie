#!/usr/bin/env python3
"""`.meta`-файлы для unity/Assets.

Unity хранит GUID каждого ассета в `<файл>.meta`. Если `.meta` нет в репозитории,
редактор создаёт его при импорте со случайным GUID, и ссылки по GUID ломаются
между машинами (отчёт тестера qa/2026-10-18-unity-pervyy-zapusk).

    python3 tools/unity_meta.py          # создать недостающие .meta
    python3 tools/unity_meta.py --check  # только проверить (для CI): код 1, если чего-то нет

GUID детерминированный — md5 от пути внутри проекта, — поэтому повторный запуск
на любой машине даёт те же файлы. Лишние .meta (без своего ассета) тоже ошибка.
"""
import argparse
import hashlib
import os
import sys

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'unity')
TRACKED_DIRS = ('Assets',)

TAIL = "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"

IMPORTERS = {
    '.cs': "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
           "  executionOrder: 0\n  icon: {instanceID: 0}\n" + TAIL,
    '.asmdef': "AssemblyDefinitionImporter:\n  externalObjects: {}\n" + TAIL,
    # текстуры моделей художника: настройки по умолчанию (sRGB, mipmaps), Unity дополнит остальное
    '.png': "TextureImporter:\n  externalObjects: {}\n" + TAIL,
    '.shader': "ShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n"
               "  nonModifiableTextures: []\n" + TAIL,
}
FOLDER = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n" + TAIL
DEFAULT = "DefaultImporter:\n  externalObjects: {}\n" + TAIL
TEXT = {'.txt', '.json', '.md', '.xml', '.bytes', '.csv'}
TEXT_IMPORTER = "TextScriptImporter:\n  externalObjects: {}\n" + TAIL


def skipped(name):
    # Unity не импортирует скрытые файлы и папки с ~ на конце
    return name.startswith('.') or name.endswith('~') or name.endswith('.meta')


def guid_for(rel):
    return hashlib.md5(('pogrujenie:' + rel.replace(os.sep, '/')).encode('utf-8')).hexdigest()


def meta_text(rel, is_dir):
    if is_dir:
        body = FOLDER
    else:
        ext = os.path.splitext(rel)[1].lower()
        body = IMPORTERS.get(ext) or (TEXT_IMPORTER if ext in TEXT else DEFAULT)
    return "fileFormatVersion: 2\nguid: %s\n%s" % (guid_for(rel), body)


def assets(root):
    """Пути ассетов относительно корня проекта: (rel, это_папка)."""
    out = []
    for top in TRACKED_DIRS:
        base = os.path.join(root, top)
        if not os.path.isdir(base):
            continue
        for d, dirs, files in os.walk(base):
            dirs[:] = sorted(x for x in dirs if not skipped(x))
            rel_d = os.path.relpath(d, root)
            if rel_d != top:
                out.append((rel_d, True))
            for f in sorted(files):
                if not skipped(f):
                    out.append((os.path.join(rel_d, f), False))
    return out


def orphans(root):
    """.meta, у которых нет ассета."""
    out = []
    for top in TRACKED_DIRS:
        for d, dirs, files in os.walk(os.path.join(root, top)):
            for f in files:
                if f.endswith('.meta') and not os.path.exists(os.path.join(d, f[:-5])):
                    out.append(os.path.relpath(os.path.join(d, f), root))
    return sorted(out)


def missing(root):
    return [rel for rel, _ in assets(root) if not os.path.exists(os.path.join(root, rel + '.meta'))]


def duplicate_guids(root):
    seen, dup = {}, []
    for rel, _ in assets(root):
        p = os.path.join(root, rel + '.meta')
        if not os.path.exists(p):
            continue
        with open(p, encoding='utf-8') as fh:
            for line in fh:
                if line.startswith('guid:'):
                    g = line.split(':', 1)[1].strip()
                    if g in seen:
                        dup.append((seen[g], rel))
                    seen[g] = rel
                    break
    return dup


def generate(root):
    made = []
    for rel, is_dir in assets(root):
        p = os.path.join(root, rel + '.meta')
        if not os.path.exists(p):
            with open(p, 'w', encoding='utf-8', newline='\n') as fh:
                fh.write(meta_text(rel, is_dir))
            made.append(rel + '.meta')
    return made


def check(root):
    problems = ['нет .meta: ' + r for r in missing(root)]
    problems += ['лишний .meta: ' + r for r in orphans(root)]
    problems += ['одинаковый GUID: %s и %s' % d for d in duplicate_guids(root)]
    return problems


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--root', default=ROOT)
    a = ap.parse_args(argv)
    if a.check:
        problems = check(a.root)
        for p in problems:
            print(p)
        if problems:
            print('Запустите python3 tools/unity_meta.py и закоммитьте .meta-файлы.')
            return 1
        print('.meta: все %d ассетов на месте' % len(assets(a.root)))
        return 0
    for m in generate(a.root):
        print('создан', m)
    return 0


if __name__ == '__main__':
    sys.exit(main())
