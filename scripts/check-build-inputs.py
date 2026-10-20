#!/usr/bin/env python3
"""Scan source inputs permitted by the build allowlist without printing values."""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[1]
patterns = [rb"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", rb"sk-proj-[A-Za-z0-9_-]{20,}", rb"gh[pousr]_[A-Za-z0-9]{30,}"]
inputs = [root / "global.json", root / "Directory.Build.props"]
for path in (root / "src").rglob("*"):
    if path.is_file() and not {"bin", "obj"}.intersection(path.parts) and (path.suffix in {".cs", ".csproj"} or path.name in {"packages.lock.json", "appsettings.json"}):
        inputs.append(path)
inputs.extend((root / "docker" / "HealthProbe").glob("*.cs"))
inputs.extend((root / "docker" / "HealthProbe").glob("*.csproj"))
allowed = {line[1:] for line in (root / ".dockerignore").read_text().splitlines() if line.startswith("!")}
for path in inputs:
    if str(path.relative_to(root)) not in allowed:
        raise SystemExit(f"Source missing from explicit Docker allowlist: {path.relative_to(root)}")
    data = path.read_bytes()
    if any(re.search(pattern, data) for pattern in patterns):
        raise SystemExit(f"Potential credential in build input: {path.relative_to(root)}")
print(f"Scanned {len(inputs)} allowlisted build inputs; no recognized credential patterns.")
