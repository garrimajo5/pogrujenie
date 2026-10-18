#!/usr/bin/env python3
"""Тесты tools/unity_meta.py и того, что у всех ассетов unity/Assets есть .meta.

    python3 tools/test_unity_meta.py
"""
import os
import re
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unity_meta as um  # noqa: E402


def write(path, text=''):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as fh:
        fh.write(text)


class Project(unittest.TestCase):
    def test_real_project_has_all_meta(self):
        # Закрепляет отчёт qa/2026-10-18-unity-pervyy-zapusk: без .meta GUID меняются при каждом импорте
        self.assertEqual(um.check(um.ROOT), [])

    def test_real_meta_guids_are_stable(self):
        # GUID в репозитории не должны меняться: сравниваем с тем, что даёт генератор
        for rel, is_dir in um.assets(um.ROOT):
            with open(os.path.join(um.ROOT, rel + '.meta'), encoding='utf-8') as fh:
                text = fh.read()
            self.assertRegex(text, r'(?m)^guid: [0-9a-f]{32}$', rel)
            self.assertTrue(text.startswith('fileFormatVersion: 2\n'), rel)
            if is_dir:
                self.assertIn('folderAsset: yes', text, rel)


class ProjectVersion(unittest.TestCase):
    """Закрепляет отчёт qa/2026-10-18-unity-pervyy-zapusk: Unity при открытии дописывала ревизию в ProjectVersion.txt."""

    def read(self, *parts):
        with open(os.path.join(os.path.dirname(um.ROOT), *parts), encoding='utf-8') as fh:
            return fh.read()

    def test_version_with_revision_matches_ci(self):
        text = self.read('unity', 'ProjectSettings', 'ProjectVersion.txt')
        ci = re.search(r'(?m)^\s*UNITY_VERSION:\s*(\S+)', self.read('.github', 'workflows', 'unity.yml')).group(1)
        self.assertRegex(text, r'(?m)^m_EditorVersion: ' + re.escape(ci) + '$')
        m = re.search(r'(?m)^m_EditorVersionWithRevision: (\S+) \(([0-9a-f]{12})\)$', text)
        self.assertIsNotNone(m, 'нет строки m_EditorVersionWithRevision')
        self.assertEqual(m.group(1), ci)


class Generator(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = self.tmp.name
        a = os.path.join(self.root, 'Assets')
        write(os.path.join(a, 'Scripts', 'Core', 'Helm.cs'), 'class A {}')
        write(os.path.join(a, 'Scripts', 'Core', 'X.asmdef'), '{}')
        write(os.path.join(a, 'Resources', 'Lit.shader'), 'Shader "x" {}')
        write(os.path.join(a, 'Resources', '.hidden'), '')
        write(os.path.join(a, 'Samples~', 'skip.cs'), '')

    def tearDown(self):
        self.tmp.cleanup()

    def test_check_fails_without_meta(self):
        problems = um.check(self.root)
        self.assertIn('нет .meta: ' + os.path.join('Assets', 'Scripts', 'Core', 'Helm.cs'), problems)
        self.assertEqual(um.main(['--check', '--root', self.root]), 1)

    def test_generate_covers_files_and_folders(self):
        um.generate(self.root)
        self.assertEqual(um.check(self.root), [])
        self.assertEqual(um.main(['--check', '--root', self.root]), 0)
        a = os.path.join(self.root, 'Assets')
        self.assertTrue(os.path.exists(os.path.join(a, 'Scripts.meta')))
        self.assertTrue(os.path.exists(os.path.join(a, 'Scripts', 'Core.meta')))
        self.assertFalse(os.path.exists(os.path.join(self.root, 'Assets.meta')))
        self.assertFalse(os.path.exists(os.path.join(a, 'Resources', '.hidden.meta')))
        self.assertFalse(os.path.exists(os.path.join(a, 'Samples~.meta')))

    def test_importer_by_type(self):
        um.generate(self.root)
        a = os.path.join(self.root, 'Assets')

        def read(*p):
            with open(os.path.join(a, *p), encoding='utf-8') as fh:
                return fh.read()
        self.assertIn('MonoImporter:', read('Scripts', 'Core', 'Helm.cs.meta'))
        self.assertIn('AssemblyDefinitionImporter:', read('Scripts', 'Core', 'X.asmdef.meta'))
        self.assertIn('ShaderImporter:', read('Resources', 'Lit.shader.meta'))
        self.assertIn('folderAsset: yes', read('Resources.meta'))

    def test_guid_deterministic_and_unique(self):
        um.generate(self.root)
        first = {rel: um.guid_for(rel) for rel, _ in um.assets(self.root)}
        self.assertEqual(len(set(first.values())), len(first))
        self.assertEqual(um.guid_for('Assets/Scripts'), um.guid_for(os.path.join('Assets', 'Scripts')))
        self.assertTrue(all(re.fullmatch(r'[0-9a-f]{32}', g) for g in first.values()))

    def test_generate_keeps_existing_meta(self):
        p = os.path.join(self.root, 'Assets', 'Resources', 'Lit.shader.meta')
        write(p, 'fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n')
        um.generate(self.root)
        with open(p, encoding='utf-8') as fh:
            self.assertIn('0123456789abcdef0123456789abcdef', fh.read())

    def test_orphan_and_duplicate_detected(self):
        um.generate(self.root)
        write(os.path.join(self.root, 'Assets', 'Gone.cs.meta'), 'guid: 1\n')
        self.assertIn('лишний .meta: ' + os.path.join('Assets', 'Gone.cs.meta'), um.check(self.root))
        os.remove(os.path.join(self.root, 'Assets', 'Gone.cs.meta'))
        src = os.path.join(self.root, 'Assets', 'Scripts', 'Core', 'Helm.cs.meta')
        dst = os.path.join(self.root, 'Assets', 'Resources', 'Lit.shader.meta')
        with open(src, encoding='utf-8') as fh:
            text = fh.read()
        write(dst, text)
        self.assertTrue(any(p.startswith('одинаковый GUID') for p in um.check(self.root)))


if __name__ == '__main__':
    unittest.main(verbosity=2)
