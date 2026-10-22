#!/usr/bin/env python3
"""Operator/deployer only. Fixed project, explicit migration identity, no secret output."""
import json,os,re,subprocess,sys
from pathlib import Path
root=Path('/opt/faultcode-demo')
if os.geteuid()!=0 or len(sys.argv)!=2 or not Path(sys.argv[1]).resolve().is_relative_to(root/'releases'):raise SystemExit(64)
settings=json.loads((root/'runtime-config.json').read_text())
compose=['docker','compose','--project-name','faultcode-demo','--env-file',str(root/'runtime.env'),'--env-file',sys.argv[1],'-f',str(root/'compose.yaml')]
secret=settings['FAULTCODE_SQL_SA_PASSWORD'];app_password=settings['FAULTCODE_SQL_APP_PASSWORD']
if not re.fullmatch(r'Fc1![a-f0-9]{48}',secret) or not re.fullmatch(r'Fc1![a-f0-9]{48}',app_password):raise SystemExit(65)
env=dict(os.environ,ConnectionStrings__FaultCodeMigration=f'Server=sql;Database=FaultCode;User ID=sa;Password={secret};Encrypt=True;TrustServerCertificate=True')
def run(command,input=None,env=None):
    result=subprocess.run(command,input=input,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,env=env)
    if result.returncode:raise SystemExit('Fault Code database preparation failed; inspect bounded service health without printing credentials.')
run(compose+['run','--rm','--no-deps','-e','Database__Command=migrate','-e','ConnectionStrings__FaultCodeMigration','api'],env=env)
sql=f"IF SUSER_ID('faultcode_app') IS NULL CREATE LOGIN faultcode_app WITH PASSWORD='{app_password}', CHECK_POLICY=ON; USE FaultCode; IF USER_ID('faultcode_app') IS NULL CREATE USER faultcode_app FOR LOGIN faultcode_app; ALTER ROLE db_datareader ADD MEMBER faultcode_app; ALTER ROLE db_datawriter ADD MEMBER faultcode_app;"
run(compose+['exec','-T','sql','bash','-c','export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -i /dev/stdin'],sql)
for action in ['seed-demo-fleet','seed-demo-faults']:
    run(compose+['run','--rm','--no-deps','-e','Database__Command='+action,'-e','Database__SeedActorId='+settings['FAULTCODE_DEMO_USER_ID'],'-e','Database__SeedClientId='+settings['FAULTCODE_DEMO_CLIENT_ID'],'api'])
if settings.get('FAULTCODE_EMBEDDINGS_ENABLED','true')=='true':
    run(compose+['run','--rm','--no-deps','-e','Database__Command=index-demo-faults','-e','Database__SeedActorId='+settings['FAULTCODE_DEMO_USER_ID'],'-e','Database__SeedClientId='+settings['FAULTCODE_DEMO_CLIENT_ID'],'api'])
print('Fault Code migration, scoped application SQL user, fleet/fault seeds and configured vector index completed.')
