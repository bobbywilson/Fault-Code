#!/usr/bin/env python3
"""Extract observed deterministic release cases; reject missing, duplicate or failed evidence."""
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

# Minimum distinct parameterized cases from the real integration suite. Full suite must also pass.
REQUIRED = {
    'Real_investigation_A_B_to_approved_notification_and_local_acknowledgement': 2,
    'Real_engineering_C_deduplicates_event_and_retains_approved_history': 1,
    'Workflow_C_uses_real_investigation_then_engineering_and_approved_lookup_without_delivery': 1,
    'Deterministic_routing_evaluation_has_exact_provenance_and_zero_unsupported_guidance': 15,
    'Invalid_or_injected_model_output_cannot_promote_or_commit': 9,
    'Unavailable_dependencies_never_confirm_unknown_or_commit_actions': 5,
    'Concurrent_retry_replays_before_provider_dependencies_and_changed_version_conflicts': 1,
    'Actual_framework_investigation_schema_is_strict_fresh_and_tool_free': 1,
    'Real_framework_and_OpenAI_sdk_emit_strict_schema_no_tools_and_bounded_output': 1,
    'Registry_exposes_only_bound_workflow_tools_with_exact_permissions': 1,
    'Model_arguments_cannot_select_identity_recipient_or_unbounded_actions': 1,
    'Bound_lookup_and_email_use_server_recipient_and_record_actual_delivery_attempts': 1,
    'Unknown_resolution_is_audited_and_replay_cannot_change_notes_or_duplicate_notifications': 1,
    'Resolution_flows_through_vector_evidence_and_authorized_MCP_before_followup': 3,
    'Invalid_identity_fails_closed_without_disclosing_token_or_reason': 20,
    'Authenticated_identity_is_not_an_automatic_operation_permission': 1,
    'Public_operations_stay_anonymous_with_enabled_identity': 1,
    'CRUD_requires_exact_grants_and_invalid_input_never_bypasses_authorization': 6,
    'Every_permission_checks_exact_scope_role_and_caller_kind_through_real_JWT_middleware': 1,
    'Every_production_route_is_explicitly_anonymous_or_has_registered_permission_and_OpenAPI_security': 1,
    'Actual_engineering_notification_commit_and_replay_survive_OTLP_outage': 2,
    'Handled_delivery_failure_exports_actionable_failure_without_contact_or_exception': 1,
}
NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}


def extract(directory):
    observed = {}; total = 0; skipped = []
    files = sorted(directory.rglob('*.trx'))
    if not files: raise ValueError('No actual TRX results')
    for file in files:
        root = ET.parse(file).getroot()
        definitions = {x.attrib['id']: x.find('t:TestMethod', NS).attrib['name'] for x in root.findall('.//t:UnitTest', NS)}
        results = root.findall('.//t:UnitTestResult', NS)
        if not results: raise ValueError('Empty TRX')
        for result in results:
            method = definitions[result.attrib['testId']]
            name = result.attrib['testName']; outcome = result.attrib['outcome']
            if outcome == 'NotExecuted' and method in {'Real_mailpit_accepts_and_retains_recipient_body_and_notification_reference','Real_sql_dispatch_commits_both_records_and_replays_after_reconnect','Real_sql_server_seed_replay_reconnect_tenant_isolation_and_rollback','Real_sql_fault_seed_is_idempotent_and_catalog_survives_reconnect'}:
                skipped.append(name); continue
            if outcome != 'Passed': raise ValueError(f'Non-passing release case: {name}: {outcome}')
            total += 1
            if method in REQUIRED:
                key = (method, name)
                if key in observed: raise ValueError(f'Duplicate/stale release evidence: {name}')
                observed[key] = {'method':method,'case':name,'outcome':outcome,'duration':result.attrib.get('duration'),'trx':file.name}
    for method, minimum in REQUIRED.items():
        count = sum(key[0] == method for key in observed)
        if count < minimum: raise ValueError(f'Missing release evaluations: {method}: {count} < {minimum}')
    return {'separateSqlMailpitJobRequiredFor':skipped, 'actualTrxFiles':[f.name for f in files], 'passedTestResults':total,
            'selectedReleaseCases':list(observed.values()),
            'limitations':['test-only canonical stores; no relational durability acceptance',
                          'mocked OpenAI; no live paid call', 'local demo acknowledgement; no external notification',
                          'signed demo JWT discovery; no live Entra tenant',
                          'real Qdrant and production browser/container results are separate jobs',
                          'opt-in live-provider test bodies may return without live execution and are not live evidence']}


if __name__ == '__main__':
    try:
        evidence = extract(Path(sys.argv[1]))
        evidence['source'] = subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
        Path(sys.argv[2]).write_text(json.dumps(evidence,indent=2)+'\n')
        print(f"Observed {evidence['passedTestResults']} passed results; {len(evidence['selectedReleaseCases'])} required release cases")
    except (ValueError, OSError, KeyError) as error:
        sys.exit(str(error))
