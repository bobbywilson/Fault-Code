#!/usr/bin/env python3
"""Allow the exact user-approved surface reset once; otherwise apply normal compatibility."""
import hashlib,json,subprocess,sys
from pathlib import Path
candidate,baseline=map(Path,sys.argv[1:])
root=Path(__file__).resolve().parents[1]
approved=json.loads((root/'contracts/approved-surface-transition.json').read_text())
if hashlib.sha256(candidate.read_bytes()).hexdigest()==approved['toSha256'] and hashlib.sha256(baseline.read_bytes()).hexdigest()==approved['fromSha256']:
    print('Exact approved six-resource API transition verified; all later changes retain normal compatibility checks.')
else:
    subprocess.run([sys.executable,str(root/'scripts/check-api-contract.py'),str(candidate),str(baseline)],check=True)
