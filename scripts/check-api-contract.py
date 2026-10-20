#!/usr/bin/env python3
"""Validate OpenAPI and conservatively preserve existing v1 structural contracts.

Existing schemas/parameters/responses/security are immutable, except prose/examples.
New operations, optional parameters, responses and unused schemas are additive.
This deliberately rejects some compatible edits, requiring a reviewed new API version.
"""
import copy
import json
import sys
from pathlib import Path
from openapi_spec_validator import validate
from jsonschema import Draft202012Validator

PROSE = {'description', 'summary', 'example', 'examples', 'externalDocs'}
METHODS = {'get', 'post', 'put', 'patch', 'delete', 'head', 'options', 'trace'}

def structural(value):
    if isinstance(value, dict):
        return {k: structural(v) for k, v in value.items() if k not in PROSE}
    if isinstance(value, list):
        return [structural(v) for v in value]
    return value


def compatible(old, new):
    assert old['info']['version'] == new['info']['version'], 'version changed'
    for name, schema in old.get('components', {}).get('schemas', {}).items():
        assert structural(schema) == structural(new['components']['schemas'].get(name)), f'schema changed: {name}'
    assert structural(old.get('components', {}).get('securitySchemes', {})) == structural(new.get('components', {}).get('securitySchemes', {})), 'security schemes changed'
    assert old.get('security', []) == new.get('security', []), 'global security changed'
    for path, item in old['paths'].items():
        assert path in new['paths'], f'path removed: {path}'
        for method, operation in item.items():
            if method not in METHODS:
                assert structural(operation) == structural(new['paths'][path].get(method)), f'path metadata changed: {path}'
                continue
            target = new['paths'][path].get(method)
            assert target is not None, f'operation removed: {method} {path}'
            for key in ('operationId', 'requestBody', 'security'):
                assert structural(operation.get(key)) == structural(target.get(key)), f'{key} changed: {path}'
            params = {(x['in'], x['name']): x for x in target.get('parameters', [])}
            original = {(x['in'], x['name']): x for x in operation.get('parameters', [])}
            for key, value in original.items():
                assert structural(value) == structural(params.get(key)), f'parameter changed: {key}'
            assert not any(v.get('required') for k, v in params.items() if k not in original), 'required parameter added'
            for code, response in operation['responses'].items():
                assert structural(response) == structural(target['responses'].get(code)), f'response changed: {path} {code}'


def validate_document(document):
    validate(document)
    ids = []
    for path, item in document['paths'].items():
        assert path.startswith('/api/v1/'), f'unversioned operation: {path}'
        for method, operation in item.items():
            if method not in METHODS:
                continue
            ids.append(operation['operationId'])
            if method == 'get' and path in {'/api/v1/health', '/api/v1/examples', '/api/v1/examples/{id}'}:
                assert operation.get('security') == [], 'foundation fixtures must explicitly be public'
            for code, response in operation['responses'].items():
                for media_type, media in response.get('content', {}).items():
                    assert 'schema' in media and 'example' in media, f'missing schema/example: {path} {code}'
                    # Resolve local component references before validating concrete examples.
                    def resolve(value):
                        if isinstance(value, dict):
                            if '$ref' in value:
                                target = document
                                for part in value['$ref'].removeprefix('#/').split('/'):
                                    target = target[part]
                                return resolve(target)
                            return {k: resolve(v) for k, v in value.items()}
                        if isinstance(value, list):
                            return [resolve(v) for v in value]
                        return value
                    Draft202012Validator(resolve(media['schema'])).validate(media['example'])
                    if code != '200':
                        assert media_type == 'application/problem+json'
                        assert media['example']['status'] == int(code)
    assert len(ids) == len(set(ids)), 'duplicate operation IDs'


def self_test(document):
    mutations = [
        lambda d: d['paths'].pop('/api/v1/health'),
        lambda d: d['components']['schemas']['ExampleResponse']['properties'].pop('id'),
        lambda d: d['paths']['/api/v1/examples']['get']['parameters'][0]['schema'].update(minimum=2),
        lambda d: d['paths']['/api/v1/examples']['get'].update(security=[{'EntraBearer': []}]),
        lambda d: d['paths']['/api/v1/examples']['get']['responses'].pop('400'),
        lambda d: d['paths']['/api/v1/examples']['get']['parameters'].append({'in': 'query', 'name': 'new', 'required': True, 'schema': {'type': 'string'}}),
    ]
    for mutation in mutations:
        changed = copy.deepcopy(document)
        mutation(changed)
        try:
            compatible(document, changed)
        except AssertionError:
            continue
        raise AssertionError('breaking fixture unexpectedly accepted')
    protected = copy.deepcopy(document)
    protected['paths']['/api/v1/examples']['get']['security'] = [{'EntraBearer': []}]
    try:
        compatible(protected, document)
    except AssertionError:
        pass
    else:
        raise AssertionError('security removal unexpectedly accepted')
    additive = copy.deepcopy(document)
    additive['paths']['/api/v1/examples']['get']['parameters'].append({'in': 'query', 'name': 'optional', 'schema': {'type': 'string'}})
    compatible(document, additive)
    print('7 breaking fixtures rejected; additive optional parameter accepted.')


if __name__ == '__main__':
    current = json.loads(Path(sys.argv[1]).read_text())
    baseline = json.loads(Path(sys.argv[2]).read_text())
    validate_document(current)
    compatible(baseline, current)
    self_test(current)
    print('OpenAPI, examples and v1 compatibility validated.')
