// Real API tests. No Prism, Prefer or preselected error examples.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';
import YAML from 'yaml';
import Ajv2020 from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';

const spec = YAML.parse(fs.readFileSync(new URL('../web_lr2/openapi.yaml', import.meta.url), 'utf8'));
const base = (process.env.ATAP_TEST_API ?? 'http://localhost:5080').replace(/\/$/, '');
const stage = process.env.ATAP_API_STAGE ?? 'all';
assert.ok(['airports', 'auth', 'all'].includes(stage), 'ATAP_API_STAGE must be airports, auth or all');
const ajv = new Ajv2020({ strict: false, allErrors: true });
addFormats(ajv);
let checks = 0;
const covered = new Set();
const validators = new Map();
const resolve = value => value?.$ref ? resolve(value.$ref.slice(2).split('/').reduce((node, key) => node[key], spec)) : value;
function operation(method, url) {
  const pathname = new URL(url, base).pathname;
  for (const [template, item] of Object.entries(spec.paths)) {
    const regex = new RegExp('^' + template.replace(/\{[^}]+\}/g, '[^/]+') + '$');
    if (regex.test(pathname) && item[method.toLowerCase()]) return item[method.toLowerCase()];
  }
  throw new Error(`Operation not documented: ${method} ${pathname}`);
}
function validateResponse(method, url, status, ct, json, text) {
  const op = operation(method, url);
  const response = resolve(op.responses[String(status)]);
  assert.ok(response, `${op.operationId}: undocumented HTTP status ${status}`);
  if (status === 204 || status === 304) { assert.equal(text, '', 'Response must not contain a body'); return; }
  const media = response.content?.[ct.split(';')[0]];
  assert.ok(media, `${op.operationId}: undocumented Content-Type ${ct}`);
  const key = `${op.operationId}:${status}:${ct}`;
  if (!validators.has(key)) validators.set(key, ajv.compile({ components: spec.components, ...media.schema }));
  const validate = validators.get(key);
  assert.ok(validate(ct.includes('json') ? json : text), `${op.operationId}: ${ajv.errorsText(validate.errors)}`);
  if (status >= 400) assert.equal(json.status, status, 'Problem.status must match HTTP status');
}
async function call(method, url, expected, { token, body, type = 'application/json', accept, headers = {} } = {}) {
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = type;
  if (accept) headers.Accept = accept;
  const response = await fetch(base + url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  const ct = response.headers.get('content-type') ?? '';
  let json;
  if (ct.includes('json') && text) json = JSON.parse(text);
  assert.equal(response.status, expected, `${method} ${url}: expected ${expected}, actual ${response.status}: ${text}`);
  validateResponse(method, url, response.status, ct, json, text);
  if (json?.status && url.includes('/exports') && expected < 400) {
    assert.ok(['queued', 'running', 'completed', 'failed'].includes(json.status));
    if (json.status === 'completed') { assert.equal(typeof json.downloadUrl, 'string'); assert.equal(json.error, null); }
    else assert.equal(json.downloadUrl, null);
    if (json.status === 'failed') assert.ok(json.error);
    else assert.equal(json.error, null);
  }
  covered.add(operation(method, url).operationId);
  console.log(`✓ ${++checks}. ${method} ${url} → ${expected}`);
  return { json, text, headers: response.headers };
}

// Local validator mutation demonstrates that the same schema rejects a missing required property.
const brokenSample = { id: 1, username: 'sample', email: 'sample@example.com', role: 'analyst' };
assert.throws(() => validateResponse('GET', '/api/v1/users/1', 200, 'application/json', brokenSample, JSON.stringify(brokenSample)));

async function run() {
  let adminToken;
  const cleanupUsers = [];
  const cleanupReports = [];
  const suffix = Date.now().toString(36);
  const period = 'airportId=1&dateFrom=2026-06-10&dateTo=2026-06-10';
  try {
    const airports = await call('GET', '/api/v1/airports?limit=1&offset=0', 200);
    assert.equal(airports.json.items.length, 1);
    assert.equal(airports.json.total, 3);
    await call('GET', '/api/v1/airports/1', 200);
    await call('GET', '/api/v1/airports/999999', 404);
    await call('GET', '/api/v1/airports?limit=0', 400);
    if (stage === 'airports') {
      console.log(`PASS stage 1: ${checks} requests; HTTP → service → repository → PostgreSQL → DTO; schemas validated.`);
      return;
    }
    const map = await call('GET', '/api/v1/flights/current', 200);
    assert.equal(map.json.length, 3);
    await call('GET', '/api/v1/flights/current', 304, { headers: { 'If-None-Match': map.headers.get('etag') } });
    const filtered = await call('GET', '/api/v1/flights/current?bbox=-1,49,9,52&maxAltitude=3000&originCountry=Germany', 200);
    assert.equal(filtered.json[0].callsign, 'DLH002'); assert.equal(filtered.json.length, 1);
    await call('GET', '/api/v1/flights/current?bbox=1,2,3', 400);
    await call('GET', `/api/v1/congestion?${period}`, 401);
    await call('POST', '/api/v1/login', 401, { body: { username: 'admin', password: 'wrong' } });
    await call('POST', '/api/v1/login', 400, { body: { username: 'admin', password: 'Admin#2026', extra: true } });
    const admin = await call('POST', '/api/v1/login', 201, { body: { username: 'admin', password: 'Admin#2026' } });
    adminToken = admin.json.accessToken;
    assert.match(adminToken, /^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/);
    const jwtParts = adminToken.split('.');
    const jwtHeader = JSON.parse(Buffer.from(jwtParts[0], 'base64url').toString());
    const jwtClaims = JSON.parse(Buffer.from(jwtParts[1], 'base64url').toString());
    assert.equal(jwtHeader.alg, 'HS256');
    assert.equal(jwtClaims.sub, String(admin.json.user.id));
    assert.equal(jwtClaims.role, 'admin');
    assert.equal(jwtClaims.iss, 'atap-api');
    assert.equal(jwtClaims.aud, 'atap-clients');
    assert.equal(jwtClaims.exp - jwtClaims.iat, admin.json.expiresIn);
    assert.match(jwtClaims.jti, /^[a-f0-9]{32}$/);
    const testKey = process.env.ATAP_TEST_JWT_KEY ?? 'atap-test-only-signing-key-0123456789abcdefghijklmnopqrstuvwxyz';
    const sign = payload => createHmac('sha256', testKey).update(payload).digest('base64url');
    assert.equal(sign(`${jwtParts[0]}.${jwtParts[1]}`), jwtParts[2], 'Use the isolated stand JWT key');
    const changedClaims = changes => {
      const payload = Buffer.from(JSON.stringify({ ...jwtClaims, ...changes })).toString('base64url');
      const unsigned = `${jwtParts[0]}.${payload}`;
      return `${unsigned}.${sign(unsigned)}`;
    };
    const wrongSignature = `${jwtParts[0]}.${jwtParts[1]}.${jwtParts[2][0] === 'A' ? 'B' : 'A'}${jwtParts[2].slice(1)}`;
    await call('GET', '/api/v1/reports', 401, { token: wrongSignature });
    await call('GET', '/api/v1/reports', 401, { token: changedClaims({ iss: 'another-issuer' }) });
    await call('GET', '/api/v1/reports', 401, { token: changedClaims({ aud: 'another-api' }) });
    const nowSeconds = Math.floor(Date.now() / 1000);
    await call('GET', '/api/v1/reports', 401, { token: changedClaims({ iat: nowSeconds - 4000, nbf: nowSeconds - 4000, exp: nowSeconds - 1 }) });
    await call('GET', '/api/v1/reports', 401, { token: changedClaims({ nbf: nowSeconds + 60, exp: nowSeconds + 3600 }) });
    const noneHeader = Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' })).toString('base64url');
    await call('GET', '/api/v1/reports', 401, { token: `${noneHeader}.${jwtParts[1]}.` });
    assert.equal(admin.json.user.role, 'admin');
    await call('GET', '/api/v1/reports', 200, { token: adminToken });
    const removedSession = await fetch(base + '/api/v1/session', { headers: { Authorization: `Bearer ${adminToken}` } });
    assert.equal(removedSession.status, 404, 'GET /session must not remain exposed');
    console.log(`✓ ${++checks}. GET /api/v1/session → 404 (removed endpoint)`);
    if (stage === 'auth') {
      await call('POST', '/api/v1/logout', 204, { token: adminToken });
      await call('GET', '/api/v1/reports', 401, { token: adminToken });
      console.log(`PASS stage 2: ${checks} requests; login, protected request, invalid credentials and token revocation.`);
      return;
    }
    console.log('STAGE 3: user CRUD, PATCH and access rules');
    const username = `lr3_${suffix}`;
    const created = await call('POST', '/api/v1/users', 201, { body: { username, email: `${username}@example.com`, password: 'Example#2026' } });
    const id = created.json.id; cleanupUsers.push(id);
    assert.equal(created.json.role, 'analyst'); assert.ok(!('password' in created.json));
    const analyst = await call('POST', '/api/v1/login', 201, { body: { username, password: 'Example#2026' } });
    const token = analyst.json.accessToken;
    await call('GET', '/api/v1/users', 403, { token });
    await call('POST', '/api/v1/users', 403, { body: { username: 'bad', email: 'bad@example.com', password: 'Example#2026', role: 'admin' } });
    await call('POST', '/api/v1/users', 409, { body: { username, email: `${username}@example.com`, password: 'Example#2026' } });
    await call('POST', '/api/v1/users', 400, { body: { username: 'short', email: 'short@example.com', password: 'abc' } });
    await call('POST', '/api/v1/users', 400, { body: { username: 'extra', email: 'extra@example.com', password: 'Example#2026', extra: true } });
    const adminCreated = await call('POST', '/api/v1/users', 201, { token: adminToken, body: { username: `${username}_other`, email: `${username}_other@example.com`, password: 'Example#2026', role: 'analyst' } });
    cleanupUsers.push(adminCreated.json.id);
    assert.equal(adminCreated.headers.get('location'), `/api/v1/users/${adminCreated.json.id}`);
    await call('GET', '/api/v1/users?role=analyst&limit=1', 200, { token: adminToken });
    await call('GET', `/api/v1/users/${id}`, 200, { token: adminToken });
    await call('GET', '/api/v1/users/999999', 404, { token: adminToken });
    const put = await call('PUT', `/api/v1/users/${id}`, 200, { token: adminToken, body: { username, email: `${username}_put@example.com`, role: 'analyst' } });
    await call('PUT', `/api/v1/users/${id}`, 400, { token: adminToken, body: { role: 'analyst' } });
    const patch = await call('PATCH', `/api/v1/users/${id}`, 200, { token: adminToken, type: 'application/merge-patch+json', body: { email: `${username}_patch@example.com` } });
    assert.equal(patch.json.username, put.json.username); assert.equal(patch.json.role, put.json.role);
    await call('PATCH', `/api/v1/users/${id}`, 400, { token: adminToken, type: 'application/merge-patch+json', body: {} });
    await call('PATCH', `/api/v1/users/${id}`, 400, { token: adminToken, type: 'application/merge-patch+json', body: { email: null } });
    await call('PATCH', `/api/v1/users/${id}`, 400, { token: adminToken, type: 'application/merge-patch+json', body: { password: 'abc' } });
    await call('PATCH', `/api/v1/users/${id}`, 415, { token: adminToken, type: 'text/plain', body: { role: 'analyst' } });
    await call('DELETE', `/api/v1/users/${admin.json.user.id}`, 403, { token: adminToken });
    for (const resource of ['aircraft', 'flights']) {
      await call('GET', `/api/v1/${resource}?limit=1`, 200, { token });
      await call('GET', `/api/v1/${resource}/1`, 200, { token });
      await call('GET', `/api/v1/${resource}/999999`, 404, { token });
      await call('GET', `/api/v1/${resource}`, 401);
    }
    await call('GET', '/api/v1/flights/1/positions?limit=1', 200, { token });
    await call('GET', '/api/v1/flights/999999/positions', 404, { token });
    console.log('STAGE 4: congestion and saved reports');
    const analysis = await call('GET', `/api/v1/congestion?${period}&limit=1`, 200, { token });
    assert.equal(analysis.json.total, 4); assert.equal(analysis.json.items.length, 1);
    assert.deepEqual(analysis.json.summary, { departures: 1, arrivals: 3 });
    const csv = await call('GET', `/api/v1/congestion?${period}`, 200, { token, accept: 'text/csv' });
    assert.equal(csv.text.trim().split('\n').length, 5);
    await call('GET', '/api/v1/congestion?airportId=999999&dateFrom=2026-06-10&dateTo=2026-06-10', 400, { token });
    await call('GET', '/api/v1/congestion?dateFrom=2026-06-11&dateTo=2026-06-10', 400, { token });
    await call('GET', '/api/v1/congestion?dateFrom=2026-01-01&dateTo=2026-02-01', 400, { token });
    await call('GET', '/api/v1/congestion?dateFrom=2026-01-01&dateTo=2026-01-31', 200, { token });
    await call('GET', `/api/v1/congestion?${period}`, 406, { token, accept: 'application/xml' });
    const report = await call('POST', '/api/v1/reports', 201, { token, body: { name: `LR3 ${suffix}`, airportId: 1, dateFrom: '2026-06-10', dateTo: '2026-06-10' } });
    cleanupReports.push({ id: report.json.id, token });
    await call('GET', '/api/v1/reports', 200, { token });
    await call('GET', `/api/v1/reports/${report.json.id}`, 200, { token });
    await call('GET', `/api/v1/reports/${report.json.id}`, 404, { token: adminToken });
    await call('DELETE', `/api/v1/reports/${report.json.id}`, 404, { token: adminToken });
    await call('GET', `/api/v1/reports/${report.json.id}/congestion`, 200, { token });
    await call('GET', `/api/v1/reports/${report.json.id}/congestion`, 200, { token, accept: 'text/csv' });
    await call('POST', '/api/v1/reports', 400, { token, body: { name: '  ', airportId: 1, dateFrom: '2026-06-10', dateTo: '2026-06-10' } });
    const exportJob = await call('POST', '/api/v1/exports', 202, { token, body: { airportId: 1, dateFrom: '2026-06-10', dateTo: '2026-06-10' } });
    console.log('STAGE 5: asynchronous CSV export');
    assert.equal(exportJob.headers.get('location'), `/api/v1/exports/${exportJob.json.id}`);
    assert.ok(exportJob.headers.get('retry-after'));
    await call('GET', `/api/v1/exports/${exportJob.json.id}`, 404, { token: adminToken });
    await call('GET', `/api/v1/exports/${exportJob.json.id}/file`, 404, { token: adminToken });
    let job = exportJob.json;
    for (let i = 0; i < 60 && job.status !== 'completed' && job.status !== 'failed'; i++) {
      await new Promise(resolve => setTimeout(resolve, 250));
      job = (await call('GET', `/api/v1/exports/${job.id}`, 200, { token })).json;
    }
    assert.equal(job.status, 'completed', job.error);
    const file = await call('GET', job.downloadUrl, 200, { token, accept: 'text/csv' });
    assert.equal(file.text, csv.text);
    await call('DELETE', `/api/v1/reports/${report.json.id}`, 204, { token }); cleanupReports.length = 0;
    await call('GET', `/api/v1/reports/${report.json.id}`, 404, { token });
    await call('POST', '/api/v1/logout', 204, { token });
    await call('GET', '/api/v1/reports', 401, { token });
    const otherId = adminCreated.json.id;
    const otherLogin = await call('POST', '/api/v1/login', 201, { body: { username: `${username}_other`, password: 'Example#2026' } });
    await call('PATCH', `/api/v1/users/${otherId}`, 200, { token: adminToken, type: 'application/merge-patch+json', body: { role: 'admin' } });
    await call('GET', '/api/v1/users', 200, { token: otherLogin.json.accessToken });
    await call('PATCH', `/api/v1/users/${otherId}`, 200, { token: adminToken, type: 'application/merge-patch+json', body: { role: 'analyst' } });
    await call('GET', '/api/v1/users', 403, { token: otherLogin.json.accessToken });
    await call('PATCH', `/api/v1/users/${otherId}`, 200, { token: adminToken, type: 'application/merge-patch+json', body: { password: 'Changed#2026' } });
    await call('GET', '/api/v1/reports', 401, { token: otherLogin.json.accessToken });
    await call('DELETE', `/api/v1/users/${id}`, 204, { token: adminToken }); cleanupUsers.splice(cleanupUsers.indexOf(id), 1);
    await call('GET', `/api/v1/users/${id}`, 404, { token: adminToken });
    const ids = Object.values(spec.paths).flatMap(item => Object.entries(item).filter(([key]) => ['get', 'post', 'put', 'patch', 'delete'].includes(key)).map(([, op]) => op.operationId));
    assert.deepEqual(ids.filter(id => !covered.has(id)), [], 'All contract operations must be exercised');
    console.log(`PASS: ${checks} requests; ${covered.size}/${ids.length} operations; response schemas validated.`);
  } finally {
    // Preserve the original failure if cleanup cannot reach the server.
    for (const report of cleanupReports) await fetch(`${base}/api/v1/reports/${report.id}`, { method: 'DELETE', headers: { Authorization: `Bearer ${report.token}` } }).catch(() => {});
    for (const id of cleanupUsers) await fetch(`${base}/api/v1/users/${id}`, { method: 'DELETE', headers: { Authorization: `Bearer ${adminToken}` } }).catch(() => {});
    if (adminToken) await fetch(`${base}/api/v1/logout`, { method: 'POST', headers: { Authorization: `Bearer ${adminToken}` } }).catch(() => {});
  }
}
await run();
