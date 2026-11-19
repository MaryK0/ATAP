"""Run the current Jenkins job and save reproducible evidence of its result."""
import argparse
import http.cookiejar
import json
import time
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import HTTPCookieProcessor, Request, build_opener

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('mode', choices=['build', 'parallel'])
parser.add_argument('--fault', choices=['none', 'postgres', 'api'], default='none')
parser.add_argument('--expect-failure', action='store_true')
parser.add_argument('--url', default='http://127.0.0.1:8085')
args = parser.parse_args()
base = args.url.rstrip('/')
opener = build_opener(HTTPCookieProcessor(http.cookiejar.CookieJar()))


def get(path):
    with opener.open(base + path, timeout=30) as response:
        return response.read()


crumb = json.loads(get('/crumbIssuer/api/json'))
job = json.loads(get('/job/atap-tests/api/json?tree=property[parameterDefinitions[name]]'))
has_parameters = any(item.get('parameterDefinitions') for item in job.get('property', []))
if not has_parameters and (args.fault != 'none' or args.mode == 'parallel'):
    raise SystemExit('First run a normal build to register Jenkinsfile parameters.')
parameters = {'FAULT_TARGET': args.fault, 'PARALLEL_DEMO': str(args.mode == 'parallel').lower()}
endpoint = '/job/atap-tests/buildWithParameters' if has_parameters else '/job/atap-tests/build'
request = Request(base + endpoint, data=urlencode(parameters).encode(),
                  headers={crumb['crumbRequestField']: crumb['crumb']})
with opener.open(request, timeout=30) as response:
    queue_path = response.headers['Location'].split('/queue/', 1)[1]
deadline = time.monotonic() + 3600
number = None
while time.monotonic() < deadline:
    item = json.loads(get('/queue/' + queue_path.rstrip('/') + '/api/json'))
    if item.get('cancelled'):
        raise SystemExit('Build was cancelled in the queue')
    if 'executable' in item:
        number = item['executable']['number']
        break
    time.sleep(2)
if number is None:
    raise SystemExit('Queue timeout')
print(f'Build #{number}: {base}/job/atap-tests/{number}/', flush=True)
out = Path('TestResults/lr2') / f'jenkins-{number}'
out.mkdir(parents=True, exist_ok=True)
while time.monotonic() < deadline:
    info = json.loads(get(f'/job/atap-tests/{number}/api/json'))
    if not info['building']:
        break
    time.sleep(5)
else:
    raise SystemExit('Build timeout: Jenkins continues running; check its UI')
console = get(f'/job/atap-tests/{number}/consoleText')
(out / 'console.log').write_bytes(console)
report = f'/job/atap-tests/{number}/artifact/ci-results/allure-report/'
for filename in ['summary.json', 'history-trend.json']:
    try:
        (out / filename).write_bytes(get(report + 'widgets/' + filename))
    except HTTPError as error:
        print(f'Report {filename}: HTTP {error.code}', flush=True)
(out / 'build.json').write_text(json.dumps(info, indent=2), encoding='utf-8')
print(f"Result: {info['result']}; evidence: {out}; Allure: {base}{report}index.html", flush=True)
expected = 'FAILURE' if args.expect_failure else 'SUCCESS'
if args.expect_failure and args.fault != 'none':
    service = 'postgres-test' if args.fault == 'postgres' else 'atap-api'
    assert ('stopping=' + service).encode() in console, 'Build failed before the requested fault'
    assert (out / 'summary.json').exists(), 'No Allure report after failure'
raise SystemExit(0 if info['result'] == expected else 1)
