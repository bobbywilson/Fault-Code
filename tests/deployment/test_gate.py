import copy
import importlib.util
from pathlib import Path
import subprocess
import os
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('gate', ROOT / 'scripts/demo-ci-gate.py')
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)
SHA = 'a' * 40


class DeploymentGateTests(unittest.TestCase):
    def setUp(self):
        self.event = {'workflow_run': {'head_sha': SHA, 'head_branch': 'main', 'event': 'push',
                                     'conclusion': 'success',
                                     'head_repository': {'full_name': GATE.REPOSITORY}}}
        self.checks = [{'id': i, 'name': name, 'head_sha': SHA, 'status': 'completed',
                        'conclusion': 'success', 'app': {'slug': 'github-actions'}}
                       for i, name in enumerate(sorted(GATE.REQUIRED), 1)]

    def test_only_complete_current_main_passes(self):
        self.assertTrue(GATE.eligible(self.event, SHA, self.checks))
        self.assertFalse(GATE.eligible(self.event, 'b' * 40, self.checks))
        self.assertFalse(GATE.eligible(self.event, SHA, self.checks[:-1]))

    def test_pr_development_fork_and_failed_event_deny(self):
        for field, value in [('event', 'pull_request'), ('head_branch', 'development'),
                             ('conclusion', 'failure'), ('head_sha', 'not-a-sha')]:
            event = copy.deepcopy(self.event)
            event['workflow_run'][field] = value
            self.assertFalse(GATE.eligible(event, SHA, self.checks))
        self.event['workflow_run']['head_repository']['full_name'] = 'attacker/Fault-Code'
        self.assertFalse(GATE.eligible(self.event, SHA, self.checks))

    def test_stale_external_pending_and_failed_checks_deny(self):
        for key, value in [('head_sha', 'b' * 40), ('app', {'slug': 'external-app'}),
                           ('status', 'in_progress'), ('conclusion', 'failure')]:
            checks = copy.deepcopy(self.checks)
            checks[0][key] = value
            self.assertFalse(GATE.eligible(self.event, SHA, checks))
        # An older successful attempt cannot hide the newest failure.
        newer = copy.deepcopy(self.checks[0])
        newer.update(id=100, conclusion='failure')
        self.assertFalse(GATE.eligible(self.event, SHA, self.checks + [newer]))

    def test_forced_command_rejects_shell_and_scope_injection(self):
        valid = 'deploy ' + SHA + (' sha256:' + 'b' * 64) * 3
        for command in ['', 'bash', valid + '; id', valid + '\nwhoami',
                        valid.replace('deploy ', 'rollback ', 1), valid + ' extra']:
            result = subprocess.run(['bash', str(ROOT / 'deploy/demo/ssh-receiver.sh')],
                                    env={**os.environ, 'SSH_ORIGINAL_COMMAND': command},
                                    capture_output=True, text=True)
            self.assertEqual(64, result.returncode, command)


if __name__ == '__main__':
    unittest.main()
