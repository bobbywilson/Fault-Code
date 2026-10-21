"""Negative acceptance for documentation release gates; no network or credentials."""
import copy
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('documentation', Path(__file__).with_name('check-documentation.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
ROOT = Path(__file__).resolve().parent.parent


class DocumentationGate(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        shutil.copytree(ROOT / 'mintlify/site', self.root / 'mintlify/site', ignore=shutil.ignore_patterns('node_modules', '.mintlify'))
        self.generated = self.root / 'generated.json'
        self.generated.write_bytes((self.root / 'mintlify/site/openapi.json').read_bytes())

    def config(self, change):
        path = self.root / 'mintlify/site/docs.json'
        value = json.loads(path.read_text()); change(value); path.write_text(json.dumps(value))

    def check(self):
        return module.verify(self.root, self.generated)

    def test_valid_reference(self):
        self.assertGreater(self.check()['protectedOperations'], 0)

    def test_drift(self):
        self.generated.write_text('{}')
        with self.assertRaisesRegex(ValueError, 'drift'): self.check()

    def test_broken_navigation_and_link(self):
        for mutation in ['navigation', 'link']:
            with self.subTest(mutation=mutation):
                path = self.root / 'mintlify/site/index.mdx'
                original = path.read_text()
                if mutation == 'navigation':
                    self.config(lambda c: c['navigation']['tabs'][0]['groups'][0]['pages'].append('absent'))
                else:
                    self.config(lambda c: c['navigation']['tabs'][0]['groups'][0]['pages'].remove('absent'))
                    path.write_text(original + '\n[Broken](/absent)')
                with self.assertRaisesRegex(ValueError, 'Broken local'): self.check()
                path.write_text(original)

    def test_public_and_direct_transport(self):
        for field in ['auth', 'userAuth']:
            self.config(lambda c: c.update({field: {}}))
            with self.assertRaisesRegex(ValueError, 'login'): self.check()
            self.config(lambda c: c.pop(field))
        self.config(lambda c: c['api']['playground'].update(proxy=True))
        with self.assertRaisesRegex(ValueError, 'proxy'): self.check()

    def test_origin_and_permission_loss(self):
        api = json.loads(self.generated.read_text())
        cases = [copy.deepcopy(api), copy.deepcopy(api)]
        cases[0]['servers'] = [{'url': 'https://unapproved.example'}]
        op = next(op for p in cases[1]['paths'].values() for m, op in p.items() if m in module.METHODS and op.get('security'))
        op.pop('x-faultcode-required-policies')
        for value in cases:
            self.generated.write_text(json.dumps(value))
            (self.root / 'mintlify/site/openapi.json').write_bytes(self.generated.read_bytes())
            with self.assertRaises(ValueError): self.check()


if __name__ == '__main__':
    unittest.main()
