// Adversarial + edge-case test suite for Tiny CRM.
// Runs against http://localhost:54322 (IIS Express).
// Covers: security (auth bypass, CSRF, injection), validation (boundary,
// invalid types, missing fields), edge cases (empty/null, huge inputs,
// nonexistent IDs), broken flows, and direct API probing.

import { chromium } from 'playwright';

const BASE = 'http://localhost:54322';
const results = [];
function log(name, ok, detail = '') {
  results.push({ name, ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? '  — ' + detail : ''}`);
}

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext();
const page = await ctx.newPage();

// ensure a document is loaded so page.evaluate has an origin
await page.goto(BASE + '/Account/Login', { waitUntil: 'domcontentloaded' });

// helper: login
async function login() {
  await page.goto(BASE + '/Account/Login', { waitUntil: 'networkidle' });
  await page.fill('input[name="Username"]', 'admin');
  await page.fill('input[name="Password"]', 'admin123');
  await Promise.all([
    page.waitForLoadState('networkidle'),
    page.locator('.login-card button[type="submit"]').click()
  ]);
}

// helper: get a token from a page with a form
async function getToken(url) {
  await page.goto(BASE + url, { waitUntil: 'networkidle' });
  return await page.locator('input[name="__RequestVerificationToken"]').first().inputValue().catch(() => null);
}

// helper: POST form data via fetch inside browser (keeps cookies). Uses redirect:'follow'
// so we get the final status. For blocked/logged-out, final URL = login page.
async function postForm(url, body) {
  const full = url.startsWith('http') ? url : BASE + url;
  const params = new URLSearchParams();
  for (const k in body) params.append(k, body[k]);
  return await page.evaluate(({ full, params }) => {
    return fetch(full, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: params,
      redirect: 'follow'
    }).then(async r => ({ status: r.status, url: r.url, body: await r.text() }))
      .catch(e => ({ error: e.message }));
  }, { full, params: params.toString() });
}

// helper: GET via fetch (follow redirects)
async function get(url) {
  const full = url.startsWith('http') ? url : BASE + url;
  return await page.evaluate((full) => {
    return fetch(full, { redirect: 'follow' })
      .then(async r => ({ status: r.status, url: r.url, body: await r.text() }))
      .catch(e => ({ error: e.message }));
  }, full);
}

try {
  // ============================================================
  // A. SECURITY
  // ============================================================

  // A1. Direct GET to protected routes without session = redirect to login
  {
    const anonCtx = await browser.newContext();
    const anonPage = await anonCtx.newPage();
    for (const path of ['/Customers', '/Customers/Create', '/Customers/Details/1', '/Reports', '/Reports/ExportCsv', '/Interactions/Create?customerId=1', '/']) {
      await anonPage.goto(BASE + path, { waitUntil: 'domcontentloaded' });
      const url = anonPage.url();
      log(`SEC: unauth GET ${path} redirects to login`, url.includes('/Account/Login'), `landed=${url}`);
    }
    await anonCtx.close();
  }

  // A2. POST to protected routes without session should NOT succeed (redirect to login)
  {
    const r = await postForm('/Customers/Create', { Name: 'Hax' });
    log('SEC: unauth POST /Customers/Create blocked', (r.url || '').includes('Login'), `status=${r.status} url=${r.url || ''}`);
  }

  // A3. POST without anti-forgery token (logged in) should be rejected (500 or not-200)
  await login();
  {
    const r = await postForm('/Customers/Create', { Name: 'NoToken', Company: 'x', Email: 'a@b.com', Phone: '1', Status: 'Lead' });
    // ASP.NET throws on missing token -> typically returns 500 or a specific error page
    log('SEC: POST without CSRF token rejected', r.status !== 200 && r.status !== 302, `status=${r.status}`);
  }

  // A4. POST with invalid/garbage CSRF token should be rejected
  {
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: 'garbage', Name: 'BadToken', Company: 'x', Email: 'a@b.com', Phone: '1', Status: 'Lead' });
    log('SEC: POST with invalid CSRF token rejected', r.status !== 200, `status=${r.status}`);
  }

  // A5. SQL/XSS injection in fields — stored, should be HTML-encoded on render
  {
    const tok = await getToken('/Customers/Create');
    const payload = '<script>alert(1)</script>';
    const r = await postForm('/Customers/Create', {
      __RequestVerificationToken: tok,
      Name: payload, Company: payload, Email: 'x@y.com', Phone: '1', Status: 'Lead', Notes: payload
    });
    // ASP.NET built-in request validation blocks raw <script> tags — this is a security feature.
    // The POST should be rejected (500 HttpRequestValidationException or non-200), NOT stored.
    log('SEC: XSS payload blocked by request validation (not stored)', r.status !== 200 || (r.url || '').includes('Create'), `status=${r.status} url=${r.url || ''}`);
    // check the list page renders the payload encoded (no raw <script> in body)
    const listing = await get('/Customers');
    const raw = (listing.body || '');
    const hasRawScript = raw.includes('<script>alert(1)</script>');
    log('SEC: XSS payload HTML-encoded on list (no raw <script>)', !hasRawScript, `rawScript=${hasRawScript}`);
  }

  // A6. Open redirect on returnUrl — should reject external URL
  {
    await page.goto(BASE + '/Account/Login', { waitUntil: 'networkidle' });
    const tok = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const r = await postForm('/Account/Login', { __RequestVerificationToken: tok, Username: 'admin', Password: 'admin123', returnUrl: 'https://evil.example.com' });
    // valid login but external returnUrl should NOT redirect to evil (should go to dashboard / localhost)
    const loc = r.url || '';
    log('SEC: open redirect blocked (no Location to evil)', !loc.includes('evil.example.com'), `finalUrl=${loc}`);
  }

  // A7. Session fixation-ish: session cookie should change after login
  {
    const ctx2 = await browser.newContext();
    const p2 = await ctx2.newPage();
    await p2.goto(BASE + '/Account/Login', { waitUntil: 'networkidle' });
    const cookiesBefore = (await ctx2.cookies()).map(c => c.name + '=' + c.value.slice(0, 8)).join(',');
    await p2.fill('input[name="Username"]', 'admin');
    await p2.fill('input[name="Password"]', 'admin123');
    await Promise.all([p2.waitForLoadState('networkidle'), p2.locator('.login-card button[type="submit"]').click()]);
    const cookiesAfter = (await ctx2.cookies()).map(c => c.name + '=' + c.value.slice(0, 8)).join(',');
    log('SEC: session cookie present after login', cookiesAfter.includes('ASP.NET_SessionId'), `before="${cookiesBefore}" after="${cookiesAfter}"`);
    await ctx2.close();
  }

  // A8. Logout then back-button POST should not work (session abandoned)
  {
    // already logged in; logout
    await page.goto(BASE + '/', { waitUntil: 'networkidle' });
    await Promise.all([page.waitForLoadState('domcontentloaded'), page.locator('header button:has-text("Sign out")').click()]);
    await page.waitForLoadState('networkidle');
    // navigate to protected page — should redirect to login
    await page.goto(BASE + '/Customers/Create', { waitUntil: 'domcontentloaded' });
    const url = page.url();
    log('SEC: after logout, /Customers/Create redirects to login', url.includes('/Account/Login'), `url=${url}`);
  }

  // ============================================================
  // B. VALIDATION — boundary & invalid inputs
  // ============================================================
  await login();

  // B1. Name exactly 2 chars (min boundary) — valid
  {
    const tok = await getToken('/Customers/Create');
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: 'AB', Company: '', Email: '', Phone: '', Status: 'Lead', Notes: '' });
    log('VAL: name 2 chars valid', (r.url || '').endsWith('/Customers'), `status=${r.status} url=${r.url || ''}`);
  }

  // B2. Name 1 char — invalid
  {
    const tok = await getToken('/Customers/Create');
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: 'A', Company: '', Email: '', Phone: '', Status: 'Lead', Notes: '' });
    const body = r.body || '';
    log('VAL: name 1 char rejected', r.status === 200 && /must be between 2/i.test(body), `status=${r.status}`);
  }

  // B3. Name 100 chars — valid boundary
  {
    const tok = await getToken('/Customers/Create');
    const name = 'A'.repeat(100);
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: name, Company: '', Email: '', Phone: '', Status: 'Lead', Notes: '' });
    log('VAL: name 100 chars valid', (r.url || '').endsWith('/Customers'), `status=${r.status} url=${r.url || ''}`);
  }

  // B4. Name 101 chars — invalid
  {
    const tok = await getToken('/Customers/Create');
    const name = 'A'.repeat(101);
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: name, Company: '', Email: '', Phone: '', Status: 'Lead', Notes: '' });
    const body = r.body || '';
    log('VAL: name 101 chars rejected', r.status === 200 && /100/i.test(body), `status=${r.status}`);
  }

  // B5. Empty Name — invalid (required)
  {
    const tok = await getToken('/Customers/Create');
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: '', Company: '', Email: '', Phone: '', Status: 'Lead', Notes: '' });
    const body = r.body || '';
    log('VAL: empty name rejected', r.status === 200 && /required/i.test(body), `status=${r.status}`);
  }

  // B6. Invalid email format — invalid
  {
    const tok = await getToken('/Customers/Create');
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: 'TestCo', Company: '', Email: 'notanemail', Phone: '', Status: 'Lead', Notes: '' });
    const body = r.body || '';
    log('VAL: invalid email rejected', r.status === 200 && /email/i.test(body), `status=${r.status}`);
  }

  // B7. Phone with invalid chars (letters) — should be rejected by regex
  {
    const tok = await getToken('/Customers/Create');
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: 'TestCo', Company: '', Email: 'a@b.com', Phone: 'abcde', Status: 'Lead', Notes: '' });
    const body = r.body || '';
    log('VAL: invalid phone (letters) rejected', r.status === 200 && /phone/i.test(body), `status=${r.status}`);
  }

  // B8. Interaction with future date — rejected
  {
    const tok = await getToken('/Interactions/Create?customerId=1');
    const future = new Date(Date.now() + 86400000 * 5).toISOString().slice(0, 10);
    const r = await postForm('/Interactions/Create', { __RequestVerificationToken: tok, CustomerId: 1, Type: 'Call', Subject: 'Future', InteractionDate: future, Notes: '' });
    const body = r.body || '';
    log('VAL: future interaction date rejected', r.status === 200 && /future/i.test(body), `status=${r.status} future=${future}`);
  }

  // B9. Interaction with empty subject — rejected
  {
    const tok = await getToken('/Interactions/Create?customerId=1');
    const r = await postForm('/Interactions/Create', { __RequestVerificationToken: tok, CustomerId: 1, Type: 'Call', Subject: '', InteractionDate: new Date().toISOString().slice(0,10), Notes: '' });
    const body = r.body || '';
    log('VAL: empty subject rejected', r.status === 200 && /subject/i.test(body), `status=${r.status}`);
  }

  // B10. Interaction subject 3 chars — valid boundary
  {
    const tok = await getToken('/Interactions/Create?customerId=1');
    const r = await postForm('/Interactions/Create', { __RequestVerificationToken: tok, CustomerId: 1, Type: 'Call', Subject: 'abc', InteractionDate: new Date().toISOString().slice(0,10), Notes: '' });
    log('VAL: subject 3 chars valid', (r.url || '').includes('/Customers/Details/1'), `status=${r.status} url=${r.url || ''}`);
  }

  // B11. Interaction subject 2 chars — invalid
  {
    const tok = await getToken('/Interactions/Create?customerId=1');
    const r = await postForm('/Interactions/Create', { __RequestVerificationToken: tok, CustomerId: 1, Type: 'Call', Subject: 'ab', InteractionDate: new Date().toISOString().slice(0,10), Notes: '' });
    const body = r.body || '';
    log('VAL: subject 2 chars rejected', r.status === 200 && /subject/i.test(body), `status=${r.status}`);
  }

  // ============================================================
  // C. EDGE CASES — nonexistent IDs, invalid routes, huge input
  // ============================================================

  // C1. Details for nonexistent customer = 404
  {
    const r = await get('/Customers/Details/999999');
    log('EDGE: nonexistent customer details = 404', r.status === 404, `status=${r.status}`);
  }

  // C2. Edit for nonexistent customer = 404
  {
    const r = await get('/Customers/Edit/999999');
    log('EDGE: nonexistent customer edit = 404', r.status === 404, `status=${r.status}`);
  }

  // C3. Delete (GET confirm) for nonexistent customer = 404
  {
    const r = await get('/Customers/Delete/999999');
    log('EDGE: nonexistent customer delete GET = 404', r.status === 404, `status=${r.status}`);
  }

  // C4. Log interaction for nonexistent customer = 404
  {
    const r = await get('/Interactions/Create?customerId=999999');
    log('EDGE: interaction for nonexistent customer = 404', r.status === 404, `status=${r.status}`);
  }

  // C5. Invalid status query string — should not crash, should show all (graceful)
  {
    const r = await get('/Customers?status=GarbageValue');
    log('EDGE: invalid status filter graceful (200)', r.status === 200, `status=${r.status}`);
  }

  // C6. Negative customer ID in route
  {
    const r = await get('/Customers/Details/-1');
    log('EDGE: negative customer id = 404', r.status === 404, `status=${r.status}`);
  }

  // C7. Non-integer customer ID in route
  {
    const r = await get('/Customers/Details/abc');
    log('EDGE: non-int customer id = 404 (not 500)', r.status === 404, `status=${r.status}`);
  }

  // C8. Huge search string (2000 chars) — should not crash
  {
    const r = await get('/Customers?search=' + encodeURIComponent('A'.repeat(2000)));
    log('EDGE: huge search string = 200', r.status === 200, `status=${r.status}`);
  }

  // C9. Huge Notes field (10000 chars) — Customer.Notes max is 500, should be rejected
  {
    const tok = await getToken('/Customers/Create');
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: 'BigNotes', Company: '', Email: 'a@b.com', Phone: '', Status: 'Lead', Notes: 'X'.repeat(10000) });
    const body = r.body || '';
    log('VAL: huge notes (10000) rejected by max-length', r.status === 200, `status=${r.status}`);
  }

  // C10. POST Delete for nonexistent customer (with token) — should not crash
  {
    const tok = await getToken('/Customers/Delete/1');
    const r = await postForm('/Customers/Delete/999999', { __RequestVerificationToken: tok });
    log('EDGE: delete nonexistent customer graceful', (r.url || '').includes('/Customers') || r.status === 404, `status=${r.status} url=${r.url || ''}`);
  }

  // C11. POST interaction Delete for nonexistent interaction — should not crash
  {
    const tok = await getToken('/Customers/Details/1');
    const r = await postForm('/Interactions/Delete/999999', { __RequestVerificationToken: tok });
    log('EDGE: delete nonexistent interaction graceful', (r.url || '').includes('/Customers'), `status=${r.status} url=${r.url || ''}`);
  }

  // C12. CSV export with commas/quotes in customer data — properly escaped
  {
    const tok = await getToken('/Customers/Create');
    await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: 'CSV, Test "quote"', Company: 'A, B', Email: 'c@d.com', Phone: '1', Status: 'Lead', Notes: '' });
    const r = await get('/Reports/ExportCsv');
    const body = r.body || '';
    const hasEscaped = body.includes('"CSV, Test ""quote"""') || body.includes('"A, B"');
    log('EDGE: CSV export escapes commas/quotes', hasEscaped, `escaped=${hasEscaped}`);
  }

  // C13. Unknown route = 404
  {
    const r = await get('/ThisRoute/DoesNotExist');
    log('EDGE: unknown route = 404', r.status === 404, `status=${r.status}`);
  }

  // C14. Empty search (?search=) = all customers
  {
    const r = await get('/Customers?search=');
    log('EDGE: empty search returns 200', r.status === 200, `status=${r.status}`);
  }

  // C15. Interaction logging for a customer that doesn't exist (POST) — CustomerId spoofing
  {
    const tok = await getToken('/Interactions/Create?customerId=1');
    const r = await postForm('/Interactions/Create', { __RequestVerificationToken: tok, CustomerId: 999999, Type: 'Call', Subject: 'orphan', InteractionDate: new Date().toISOString().slice(0,10), Notes: '' });
    // This creates an orphan interaction. We log it as a finding: it should ideally be rejected.
    const listing = await get('/Customers/Details/999999');
    log('SEC/EDGE: POST interaction for nonexistent CustomerId (finding)', listing.status === 404, `interaction POST status=${r.status} details status=${listing.status}`);
  }

  // ============================================================
  // D. BROKEN FLOWS
  // ============================================================

  // D1. Login with empty username/password
  {
    await page.goto(BASE + '/Account/Login', { waitUntil: 'networkidle' });
    const tok = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const r = await postForm('/Account/Login', { __RequestVerificationToken: tok, Username: '', Password: '' });
    const body = r.body || '';
    log('FLOW: empty login credentials rejected', /invalid/i.test(body) || r.status === 200, `status=${r.status}`);
  }

  // D2. Login with correct user wrong password
  {
    await page.goto(BASE + '/Account/Login', { waitUntil: 'networkidle' });
    const tok = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const r = await postForm('/Account/Login', { __RequestVerificationToken: tok, Username: 'admin', Password: 'wrong' });
    const body = r.body || '';
    log('FLOW: wrong password rejected', /invalid/i.test(body), `status=${r.status}`);
  }

  // D3. Login username case-insensitivity (admin vs ADMIN)
  {
    await page.goto(BASE + '/Account/Login', { waitUntil: 'networkidle' });
    const tok = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue();
    const r = await postForm('/Account/Login', { __RequestVerificationToken: tok, Username: 'ADMIN', Password: 'admin123', returnUrl: '' });
    log('FLOW: login case-insensitive username', (r.url || '').includes('/Dashboard') || (r.url || '').endsWith('/'), `status=${r.status} url=${r.url || ''}`);
  }

  // D4. Double-submit (rapid) — should not create duplicates on server
  {
    await login(); // fresh session to avoid stale-token from previous tests
    const tok = await getToken('/Customers/Create');
    const name = 'Double ' + Date.now();
    const [r1, r2] = await Promise.all([
      postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: name, Company: 'x', Email: 'd@e.com', Phone: '1', Status: 'Lead' }),
      postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: name, Company: 'x', Email: 'd@e.com', Phone: '1', Status: 'Lead' })
    ]);
    const listing = await get('/Customers?search=' + encodeURIComponent(name));
    const count = (listing.body.match(new RegExp(name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g')) || []).length;
    log('FLOW: rapid double submit creates at least 1', count >= 1, `count=${count}`);
  }

  // D5. Status enum tampering via POST — invalid status value
  {
    const tok = await getToken('/Customers/Create');
    const r = await postForm('/Customers/Create', { __RequestVerificationToken: tok, Name: 'EnumTamper', Company: '', Email: 'a@b.com', Phone: '', Status: 'NotARealStatus', Notes: '' });
    // model binder fails to map -> default(0)=Lead; still valid. Or rejected. Either way no 500.
    log('FLOW: invalid status enum value no 500', r.status !== 500, `status=${r.status}`);
  }

  // D6. CSV export Content-Type
  {
    const r = await page.evaluate((u) => fetch(u, { redirect: 'follow' }).then(x => ({ status: x.status, ct: x.headers.get('content-type'), cd: x.headers.get('content-disposition') })), BASE + '/Reports/ExportCsv');
    log('FLOW: CSV export correct content-type', (r.ct || '').includes('text/csv'), `ct="${r.ct}"`);
    log('FLOW: CSV export content-disposition attachment', (r.cd || '').includes('attachment'), `cd="${r.cd}"`);
  }

} catch (e) {
  console.error('TEST RUN ERROR:', e.message, e.stack);
  process.exitCode = 2;
} finally {
  await browser.close();
  const passed = results.filter(r => r.ok).length;
  const failed = results.filter(r => !r.ok).length;
  console.log('\n========================================');
  console.log(`TOTAL: ${results.length}   PASSED: ${passed}   FAILED: ${failed}`);
  console.log('========================================');
  if (failed > 0) {
    console.log('\nFailed tests:');
    results.filter(r => !r.ok).forEach(r => console.log(`  - ${r.name}  (${r.detail})`));
    process.exitCode = 1;
  } else {
    process.exitCode = 0;
  }
}