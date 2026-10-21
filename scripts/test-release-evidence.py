import importlib.util
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location('evidence', Path(__file__).with_name('release-evidence.py'))
module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
N = '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'


class ReleaseGate(unittest.TestCase):
    def fixture(self, directory, omit=None, outcome='Passed', duplicate=False, omit_case=False):
        root = ET.Element(N+'TestRun'); defs=ET.SubElement(root,N+'TestDefinitions'); results=ET.SubElement(root,N+'Results')
        for method, count in module.REQUIRED.items():
            for case in range(count):
                if method==omit or (omit_case and method==next(iter(module.REQUIRED)) and case==0): continue
                identity=f'{method}-{case}'
                unit=ET.SubElement(defs,N+'UnitTest',id=identity)
                ET.SubElement(unit,N+'TestMethod',name=method)
                result=ET.SubElement(results,N+'UnitTestResult',testId=identity,testName=identity,outcome=outcome)
                if duplicate: results.append(ET.fromstring(ET.tostring(result))); duplicate=False
        ET.ElementTree(root).write(Path(directory)/'result.trx')

    def test_actual_required_cases_and_rejected_missing_failure_skip_duplicate(self):
        for settings in [{}, {'omit':next(iter(module.REQUIRED))}, {'outcome':'Failed'}, {'outcome':'NotExecuted'}, {'duplicate':True}, {'omit_case':True}]:
            with self.subTest(settings=settings), tempfile.TemporaryDirectory() as directory:
                self.fixture(directory,**settings)
                if settings:
                    with self.assertRaises(ValueError): module.extract(Path(directory))
                else:
                    self.assertGreater(module.extract(Path(directory))['passedTestResults'],0)

    def test_no_results_is_not_success(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError,'No actual'): module.extract(Path(directory))


if __name__=='__main__': unittest.main()
