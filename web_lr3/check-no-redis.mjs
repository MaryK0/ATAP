import assert from 'node:assert/strict';
const base = process.env.ATAP_TEST_API;
assert.ok(base, 'Use the isolated test stand');
const login = await fetch(`${base}/api/v1/login`, {
  method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username: 'admin', password: 'Admin#2026' })
});
assert.equal(login.status, 201);
const { accessToken } = await login.json();
try {
  for (const path of ['/api/v1/flights/current', '/api/v1/congestion?airportId=1&dateFrom=2026-06-10&dateTo=2026-06-10']) {
    const response = await fetch(base + path, { headers: { Authorization: `Bearer ${accessToken}` } });
    assert.equal(response.status, 200, `Redis unavailable: ${path}`);
    const body = await response.json();
    if (Array.isArray(body)) assert.equal(body.length, 3);
    else { assert.equal(body.total, 4); assert.deepEqual(body.summary, { departures: 1, arrivals: 3 }); }
  }
  console.log('PASS: map and congestion are available with Redis stopped.');
} finally {
  await fetch(`${base}/api/v1/logout`, { method: 'POST', headers: { Authorization: `Bearer ${accessToken}` } });
}
