"""Generate the bounded administration manifest from authoritative OpenAPI; no runtime discovery."""
import json,re,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
doc=json.loads((root/'contracts/openapi-v1.json').read_text())
def resolve(s, depth=0):
 if depth>12: raise ValueError('Recursive input schema needs explicit UI design')
 if 'oneOf' in s or 'anyOf' in s:
  variants=s.get('oneOf',s.get('anyOf'))
  nonnull=[x for x in variants if x.get('type')!='null']
  if len(nonnull)!=1 or len(nonnull)==len(variants): raise ValueError('Union input schema needs explicit UI design')
  result=resolve(nonnull[0],depth+1)
  result['type']=[result.get('type','object'),'null']
  return result
 if '$ref' in s: return resolve(doc['components']['schemas'][s['$ref'].split('/')[-1]],depth+1)
 return {k: ({a:resolve(b,depth+1) for a,b in v.items()} if k=='properties' else resolve(v,depth+1) if k=='items' else v) for k,v in s.items() if k in ('type','format','enum','properties','items','required','minimum','maximum','minLength','maxLength','minItems','maxItems','additionalProperties','default','pattern')}
ops=[]
for path,methods in doc['paths'].items():
 for method,o in methods.items():
  if not isinstance(o,dict) or not o.get('security') or o['operationId']=='GetCallerPermissions':continue
  id=o['operationId']
  group='Users & roles' if '/admin/users' in path or path.endswith('/admin/roles') else 'Integrations' if '/admin/integrations' in path else 'Agent activity' if id in ('RunIncidentAgent','ListIncidentInvestigations') else 'Notifications' if 'notification' in path else 'Engineering' if 'engineering-escalations' in path else 'Documents' if '/knowledge' in path else 'Ontology' if '/ontology' in path else 'Drivers' if '/drivers' in path else 'Vehicles' if '/vehicles' in path else 'Fault definitions' if '/faults' in path else 'Incidents'
  params=[dict(name=p['name'],where=p['in'],required=p.get('required',False),schema=resolve(p['schema'])) for p in o.get('parameters',[]) if p['in']!='header']
  content=o.get('requestBody',{}).get('content',{}).get('application/json',{})
  ops.append(dict(id=id,label=re.sub(r'(?<=[a-z0-9])(?=[A-Z])',' ',id).replace('Managed','').replace('Administration','').strip(),group=group,method=method.upper(),path=path,parameters=params,headers=[p['name'] for p in o.get('parameters',[]) if p['in']=='header'],body=resolve(content['schema']) if 'schema' in content else None,policies=[[a['scope'] for a in p['anyOf']] for p in o['x-faultcode-policy-requirements']]))
output=json.dumps(ops,indent=2)+'\n'
p=root/'frontend/src/app/operations.json'
if '--check' in sys.argv:
 if p.read_text()!=output: raise SystemExit('Administration manifest is stale')
else:p.write_text(output)
print(f'{len(ops)} protected administration operations mapped')
