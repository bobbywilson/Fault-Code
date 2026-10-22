#!/usr/bin/env python3
"""Validate schemas and actual SDK invocation examples emitted by McpToolsTests."""
import json
import sys
from pathlib import Path
from jsonschema import Draft202012Validator

artifact = json.loads(Path(sys.argv[1]).read_text())
tools = {tool['name']: tool for tool in artifact['tools']}
assert set(tools) == {'notification_fault_lookup', 'notification_email_send'}
assert artifact['sdk'] == '2.3.0'
assert len(artifact['samples']) == len(tools)
for sample in artifact['samples']:
    tool = tools[sample['name']]
    inputs = tool['inputSchema']
    outputs = tool['outputSchema']
    Draft202012Validator.check_schema(inputs)
    Draft202012Validator.check_schema(outputs)
    Draft202012Validator(inputs).validate(sample['input'])
    Draft202012Validator(outputs).validate(sample['output'])
    Draft202012Validator(outputs).validate(sample['error'])
    assert not inputs['additionalProperties']
    assert sample['output']['status'] == 200 and sample['error']['status'] == 400
    assert not Draft202012Validator(inputs).is_valid({**sample['input'], 'tenantId': 'spoofed'})
    if inputs['required']: assert not Draft202012Validator(inputs).is_valid({})
print('Two internal MCP tool input/output schemas and success/error examples validated; unknown/missing input fixtures rejected.')
