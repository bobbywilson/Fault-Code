#!/usr/bin/env python3
"""Part of FAU-23: exact generated reference and local documentation integrity."""
import argparse
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote, urlsplit

METHODS = {'get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace'}


def verify(root, generated):
    docs = root / 'mintlify/site'
    if (docs / 'openapi.json').read_bytes() != generated.read_bytes():
        raise ValueError('Mintlify OpenAPI drift: regenerate from Release output')
    api = json.loads(generated.read_text())
    config = json.loads((docs / 'docs.json').read_text())
    if any(k in config for k in ('auth', 'userAuth')):
        raise ValueError('Public documentation must not require login')
    if config.get('api', {}).get('playground') != {'display': 'simple', 'proxy': False}:
        raise ValueError('Unapproved playground target or bearer proxy')
    if api.get('servers'):
        raise ValueError('API origin requires an approved deployment configuration change')
    operations = [op for path in api['paths'].values() for method, op in path.items() if method in METHODS]
    protected = [op for op in operations if op.get('security')]
    for op in protected:
        if op['security'] != [{'EntraBearer': []}] or not op.get('x-faultcode-required-policies'):
            raise ValueError('Protected operation lost exact bearer/policy requirements')
    references = []

    def navigation(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key == 'pages':
                    references.extend(child)
                elif key == 'openapi':
                    if child != 'openapi.json':
                        raise ValueError('Reference must use local generated OpenAPI')
                else:
                    navigation(child)
        elif isinstance(value, list):
            for child in value:
                navigation(child)
    navigation(config['navigation'])
    for page in docs.glob('*.mdx'):
        text = page.read_text()
        references.extend(re.findall(r'\]\(([^)]+)\)', text))
        references.extend(re.findall(r'href="([^"]+)"', text))
        if re.search(r'<(?:script|iframe)\b|localStorage|https?://[^\s]*[?&](?:token|access_token)=', text, re.I):
            raise ValueError(f'Unsafe executable content/token persistence in {page.name}')
    for ref in references:
        url = urlsplit(ref)
        if url.scheme or url.netloc or not url.path:
            continue
        target = (docs / unquote(url.path).lstrip('/')).resolve()
        if not target.is_relative_to(docs.resolve()) or not any(p.is_file() for p in [target, target.with_suffix('.mdx')]):
            raise ValueError(f'Broken local documentation link: {ref}')
    return {'operations': len(operations), 'protectedOperations': len(protected), 'pages': len(list(docs.glob('*.mdx')))}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('generated', type=Path)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    if args.write:
        (root / 'mintlify/site/openapi.json').write_bytes(args.generated.read_bytes())
    try:
        print(json.dumps(verify(root, args.generated)))
    except (ValueError, OSError, KeyError) as error:
        sys.exit(str(error))


if __name__ == '__main__':
    main()
