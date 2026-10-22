// Многошаговые сценарии по контракту openapi.yaml на mock-сервере Prism.
// Запуск: make api-scenario   (или: node web_lr2/mock-scenario.mjs)
// Без MOCK_URL скрипт сам запускает Prism на порту 4010 и останавливает его по окончании;
// с MOCK_URL=http://127.0.0.1:4010 использует уже запущенный сервер.
// Реальной логики нет: Prism отдаёт примеры из контракта и проверяет запросы по схемам.
// Нужен Node.js 18+ (встроенный fetch).

import { spawn, execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const PRISM = '@stoplight/prism-cli@5.16.0';
const SPEC = path.join(path.dirname(fileURLToPath(import.meta.url)), 'openapi.yaml');
const PORT = 4010;
const BASE = process.env.MOCK_URL ?? `http://127.0.0.1:${PORT}`;

let failed = 0;
let step = 0;

// Prism отклоняет запрос, нарушающий схему, кодом 400 и перечисляет нарушения в заголовке sl-violations.
const violations = (res) => res.headers.get('sl-violations') ?? '';

function check(name, ok, detail = '') {
  step += 1;
  if (!ok) failed += 1;
  console.log(`${ok ? '  ✓' : '  ✗'} ${String(step).padStart(2)}. ${name}${ok ? '' : `  → ${detail}`}`);
}

async function call(method, url, { token, body, type = 'application/json', accept, prefer } = {}) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body !== undefined) headers['Content-Type'] = type;
  if (accept) headers.Accept = accept;
  if (prefer) headers.Prefer = prefer;
  const res = await fetch(BASE + url, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const ct = res.headers.get('content-type') ?? '';
  const text = await res.text();
  let json = null;
  if (ct.includes('json') && text) {
    try { json = JSON.parse(text); } catch { /* оставляем null */ }
  }
  return { status: res.status, ct, text, json, headers: res.headers };
}

async function startPrism() {
  const win = process.platform === 'win32';
  const child = spawn(`npx -y ${PRISM} mock "${SPEC}" -p ${PORT} --errors`, { shell: true, stdio: 'ignore' });
  const stop = () => {
    try {
      if (win) execSync(`taskkill /pid ${child.pid} /t /f`, { stdio: 'ignore' });
      else child.kill('SIGTERM');
    } catch { /* уже остановлен */ }
  };
  for (let i = 0; i < 120; i += 1) {
    try {
      const r = await fetch(`${BASE}/api/v1/airports`);
      if (r.status === 200) return stop;
    } catch { /* ещё не поднялся */ }
    await new Promise((r) => setTimeout(r, 1000));
  }
  stop();
  throw new Error('Prism не запустился за 120 секунд');
}

// ── Сценарий 3 из README: аналитик анализирует загруженность аэропорта ─────────────────────
async function analystScenario() {
  console.log('\nСценарий «Анализ загруженности» (аналитик; README §6, сценарий 3)');
  const login = await call('POST', '/api/v1/sessions', { body: { username: 'e.morozova', password: 'S3cure!pass' } });
  check('createSession → 201, токен и пользователь', login.status === 201 && typeof login.json?.accessToken === 'string' && ['analyst', 'admin'].includes(login.json?.user?.role), `status=${login.status}`);
  const token = login.json?.accessToken;

  const airports = await call('GET', '/api/v1/airports?limit=10000');
  const airportId = airports.json?.items?.[0]?.id;
  check('listAirports → 200, список для выбора (без токена, гостю доступно)', airports.status === 200 && Number.isInteger(airportId) && Number.isInteger(airports.json.total), `status=${airports.status}`);

  const noAuth = await call('GET', `/api/v1/congestion?airportId=${airportId}&dateFrom=2026-06-10&dateTo=2026-06-10`);
  check('getCongestion без токена → 401', noAuth.status === 401, `status=${noAuth.status}`);

  const cong = await call('GET', `/api/v1/congestion?airportId=${airportId}&dateFrom=2026-06-10&dateTo=2026-06-10`, { token });
  const sum = cong.json?.summary;
  check('getCongestion (JSON) → 200, вылетов + прилётов = записей', cong.status === 200 && sum && sum.departures + sum.arrivals === cong.json.total, `status=${cong.status}`);

  const csv = await call('GET', `/api/v1/congestion?airportId=${airportId}&dateFrom=2026-06-10&dateTo=2026-06-10`, { token, accept: 'text/csv' });
  const header = csv.text.split(/\r?\n/)[0];
  check('getCongestion (Accept: text/csv) → 200, CSV с заголовком колонок', csv.status === 200 && csv.ct.includes('text/csv') && header.startsWith('id,airportId,flightId,callsign'), `status=${csv.status} ct=${csv.ct}`);

  const badBody = await call('POST', '/api/v1/reports', { token, body: { name: '', airportId, dateFrom: '2026-06-10', dateTo: '2026-06-10' } });
  check('createReport с пустым именем → 400, нарушена схема (name: minLength)', badBody.status === 400 && violations(badBody).includes('"minLength"'), `status=${badBody.status}`);

  const err400 = await call('POST', '/api/v1/reports', { token, prefer: 'code=400, example=unknownAirport', body: { name: 'Отчёт', airportId: 99999, dateFrom: '2026-06-10', dateTo: '2026-06-10' } });
  check('createReport: документированная ошибка 400 (Problem Details, несуществующий аэропорт)', err400.status === 400 && err400.ct.includes('application/problem+json') && err400.json?.type?.endsWith('unknown-airport'), `status=${err400.status} ct=${err400.ct}`);

  const created = await call('POST', '/api/v1/reports', { token, body: { name: 'Bellona, 10 июня', airportId, dateFrom: '2026-06-10', dateTo: '2026-06-10' } });
  const reportId = created.json?.id;
  check('createReport → 201, отчёт с id', created.status === 201 && Number.isInteger(reportId), `status=${created.status}`);

  const list = await call('GET', '/api/v1/reports', { token });
  check('listReports → 200, страница отчётов', list.status === 200 && Array.isArray(list.json?.items) && list.json.items.length > 0, `status=${list.status}`);

  const again = await call('GET', `/api/v1/reports/${reportId}/congestion`, { token, accept: 'text/csv' });
  check('getReportCongestion (Accept: text/csv) → 200, повторная выгрузка', again.status === 200 && again.ct.includes('text/csv'), `status=${again.status}`);

  const del = await call('DELETE', `/api/v1/reports/${reportId}`, { token });
  check('deleteReport → 204', del.status === 204, `status=${del.status}`);

  const gone = await call('GET', `/api/v1/reports/${reportId}`, { token, prefer: 'code=404' });
  check('getReport после удаления: документированная ошибка 404', gone.status === 404 && gone.ct.includes('application/problem+json'), `status=${gone.status}`);

  const logout = await call('DELETE', '/api/v1/sessions/current', { token });
  check('deleteCurrentSession → 204 (выход)', logout.status === 204, `status=${logout.status}`);
}

// ── Администратор: CRUD пользователя и частичное обновление ────────────────────────────────
async function adminScenario() {
  console.log('\nСценарий «Администрирование пользователей» (CRUD и PATCH; README §6, процесс 3)');
  const login = await call('POST', '/api/v1/sessions', { body: { username: 'admin1', password: 'admin123' } });
  const token = login.json?.accessToken;
  check('createSession → 201, роль admin', login.status === 201 && login.json?.user?.role === 'admin', `status=${login.status}`);

  const list = await call('GET', '/api/v1/users?limit=50', { token });
  check('listUsers → 200 (Read: список)', list.status === 200 && Array.isArray(list.json?.items), `status=${list.status}`);

  const shortPwd = await call('POST', '/api/v1/users', { token, body: { username: 's.novikov', email: 's.novikov@atap.com', password: 'abc', role: 'analyst' } });
  check('createUser с паролем из 3 символов → 400, нарушена схема (password: minLength 8)', shortPwd.status === 400 && violations(shortPwd).includes('minLength'), `status=${shortPwd.status}`);

  const longPwd = await call('POST', '/api/v1/users', { token, body: { username: 's.novikov', email: 's.novikov@atap.com', password: 'x'.repeat(129), role: 'analyst' } });
  check('createUser с паролем из 129 символов → 400, нарушена схема (password: maxLength 128)', longPwd.status === 400 && violations(longPwd).includes('maxLength'), `status=${longPwd.status}`);

  const weak = await call('POST', '/api/v1/users', { token, prefer: 'code=400, example=weakPassword', body: { username: 's.novikov', email: 's.novikov@atap.com', password: 'Temp#2026' } });
  check('createUser: документированная ошибка 400 для слабого пароля (Problem Details, errors[password])', weak.status === 400 && weak.ct.includes('application/problem+json') && weak.json?.errors?.[0]?.field === 'password', `status=${weak.status}`);

  const create = await call('POST', '/api/v1/users', { token, body: { username: 's.novikov', email: 's.novikov@atap.com', password: 'Temp#2026', role: 'analyst' } });
  check('createUser → 201 (Create)', create.status === 201 && Number.isInteger(create.json?.id), `status=${create.status}`);
  const id = create.json?.id;

  const dup = await call('POST', '/api/v1/users', { token, prefer: 'code=409', body: { username: 's.novikov', email: 's.novikov@atap.com', password: 'Temp#2026' } });
  check('createUser с занятым логином: документированная ошибка 409', dup.status === 409 && dup.ct.includes('application/problem+json'), `status=${dup.status}`);

  const one = await call('GET', `/api/v1/users/${id}`, { token });
  check('getUser → 200 (Read: один)', one.status === 200 && one.json?.id !== undefined && !('password' in one.json) && !('passwordHash' in one.json), `status=${one.status}`);

  const put = await call('PUT', `/api/v1/users/${id}`, { token, body: { username: 's.novikov', email: 's.novikov@atap.com', role: 'admin' } });
  check('replaceUser (PUT, все поля) → 200 (Update: полное)', put.status === 200, `status=${put.status}`);

  const putPart = await call('PUT', `/api/v1/users/${id}`, { token, body: { role: 'admin' } });
  check('replaceUser (PUT) только с ролью → 400, нарушена схема (required: username, email)', putPart.status === 400 && violations(putPart).includes('required'), `status=${putPart.status}`);

  const patch = await call('PATCH', `/api/v1/users/${id}`, { token, type: 'application/merge-patch+json', body: { role: 'admin' } });
  check('updateUser (PATCH, только роль, merge-patch+json) → 200 (Update: частичное)', patch.status === 200, `status=${patch.status}`);

  const patchEmpty = await call('PATCH', `/api/v1/users/${id}`, { token, type: 'application/merge-patch+json', body: {} });
  check('updateUser (PATCH) с пустым объектом → 400, нарушена схема (minProperties: 1)', patchEmpty.status === 400 && violations(patchEmpty).includes('minProperties'), `status=${patchEmpty.status}`);

  const patchNull = await call('PATCH', `/api/v1/users/${id}`, { token, type: 'application/merge-patch+json', body: { email: null } });
  check('updateUser (PATCH) с null вместо email → 400, нарушена схема (null запрещён)', patchNull.status === 400 && violations(patchNull).includes('type'), `status=${patchNull.status}`);

  const patchShort = await call('PATCH', `/api/v1/users/${id}`, { token, type: 'application/merge-patch+json', body: { password: 'abc' } });
  check('updateUser (PATCH) с коротким паролем → 400, нарушена схема (password: minLength 8)', patchShort.status === 400 && violations(patchShort).includes('minLength'), `status=${patchShort.status}`);

  const wrongType = await call('PATCH', `/api/v1/users/${id}`, { token, type: 'text/plain', body: { role: 'admin' } });
  check('updateUser (PATCH) с Content-Type text/plain → 415', wrongType.status === 415, `status=${wrongType.status}`);

  const protectedAdmin = await call('DELETE', '/api/v1/users/8', { token, prefer: 'code=403, example=adminProtected' });
  check('deleteUser админа: документированная ошибка 403', protectedAdmin.status === 403 && protectedAdmin.json?.type?.endsWith('cannot-delete-admin'), `status=${protectedAdmin.status}`);

  const del = await call('DELETE', `/api/v1/users/${id}`, { token });
  check('deleteUser → 204 (Delete)', del.status === 204, `status=${del.status}`);
}

let stop = null;
try {
  if (!process.env.MOCK_URL) {
    console.log(`Запуск mock-сервера Prism (${PRISM}) по ${path.basename(SPEC)}…`);
    stop = await startPrism();
  }
  await analystScenario();
  await adminScenario();
} catch (e) {
  failed += 1;
  console.error(`\nОшибка выполнения: ${e.message}`);
} finally {
  if (stop) stop();
}

console.log(`\n${failed === 0 ? 'ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ' : `ПРОВАЛЕНО ПРОВЕРОК: ${failed}`} (шагов: ${step})`);
process.exit(failed === 0 ? 0 : 1);
