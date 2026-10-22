#!/usr/bin/env python3
"""Part of FAU-23: fail-closed, same-source development deployment eligibility."""
import json
import os
import re
import urllib.request

REPOSITORY = 'bobbywilson/Fault-Code'
REQUIRED = {'verify', 'administration', 'documentation', 'retrieval', 'smoke', 'release', 'sql-server'}


def eligible(event, current, checks):
    run = event.get('workflow_run', {})
    if event.get('ref') == 'refs/heads/development':
        run = dict(head_sha=event.get('after'), head_branch='development', event='push', conclusion='success', head_repository=event.get('repository', {}))
    source = run.get('head_sha', '')
    if not (run.get('event') == 'push' and run.get('head_branch') == 'development'
            and run.get('head_repository', {}).get('full_name') == REPOSITORY
            and run.get('conclusion') == 'success' and re.fullmatch('[0-9a-f]{40}', source)
            and source == current):
        return False
    latest = {}
    for check in checks:
        if check.get('head_sha') != source or check.get('app', {}).get('slug') != 'github-actions':
            continue
        name = check.get('name')
        if name not in latest or check['id'] > latest[name]['id']:
            latest[name] = check
    return all(name in latest and latest[name].get('status') == 'completed'
               and latest[name].get('conclusion') == 'success' for name in REQUIRED)


def main():
    with open(os.environ['GITHUB_EVENT_PATH']) as stream:
        event = json.load(stream)
    source = event.get('after', '')
    assert re.fullmatch('[0-9a-f]{40}', source), 'Invalid source SHA'

    def get(path):
        request = urllib.request.Request('https://api.github.com/repos/' + REPOSITORY + path,
                                         headers={'Authorization': 'Bearer ' + os.environ['GH_TOKEN'],
                                                  'Accept': 'application/vnd.github+json'})
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)

    import time
    ready = False
    for attempt in range(90):
        current = get('/git/ref/heads/development')['object']['sha']
        checks = get('/commits/' + source + '/check-runs?per_page=100')['check_runs']
        ready = eligible(event, current, checks)
        if ready or current != source: break
        time.sleep(15)
    if not ready: raise SystemExit('Required current-development checks did not pass; no deployment')
    with open(os.environ['GITHUB_OUTPUT'], 'a') as stream:
        stream.write('ready=' + str(ready).lower() + '\nsource=' + source + '\n')
    print('All current-source deployment gates passed' if ready else
          'No deployment: source/event/required CI did not pass the deployment gate')


if __name__ == '__main__':
    main()
