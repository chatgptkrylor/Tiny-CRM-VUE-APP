import { chromium, request } from 'playwright'

const BASE = 'http://localhost:5173'
const results = []
const log = (name, ok, detail = '') => {
  results.push({ name, ok })
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? '  - ' + detail : ''}`)
}
const stripTraceId = (body) => body.replace(/"traceId":"[^"]*"/g, '"traceId":""')

let browser
const apiCtxs = []
async function newApi(opts = {}) {
  const c = await request.newContext({ baseURL: BASE, ...opts })
  apiCtxs.push(c)
  return c
}

async function main() {
  // ---------- 1 & 2: Auth bypass, no cookie ----------
  const anon = await newApi()
  const r1 = await anon.get('/api/customers')
  const b1 = await r1.text()
  log('1. Anonymous GET /api/customers -> 401 (not 200, not HTML)',
    r1.status() === 401 && !/<html/i.test(b1),
    `status=${r1.status()} contentType=${r1.headers()['content-type']} bodyLen=${b1.length}`)

  const r2 = await anon.get('/api/auth/me')
  log('2. Anonymous GET /api/auth/me -> 401', r2.status() === 401, `status=${r2.status()}`)

  // ---------- 3: SPA redirect unauthenticated ----------
  browser = await chromium.launch({ headless: true })
  const pageA = await (await browser.newContext()).newPage()
  await pageA.goto(BASE + '/customers', { waitUntil: 'domcontentloaded' })
  await pageA.waitForURL(/\/login/)
  log('3. Unauthenticated SPA /customers redirects to /login?returnUrl=',
    /returnUrl/.test(pageA.url()), pageA.url())

  // log pageA in via the real UI form so pageA has a genuine browser session
  // (needed later for check 18 - reload must survive a REAL logged-in session)
  await pageA.fill('input[name="Username"]', 'admin')
  await pageA.fill('input[name="Password"]', 'admin123')
  await pageA.click('button[type="submit"]')
  await pageA.waitForURL(/\/customers/)
  await pageA.waitForSelector('table.table tbody tr')

  // ---------- login + capture raw cookie for replay/tamper tests ----------
  const authApi = await newApi()
  const loginRes = await authApi.post('/api/auth/login', { data: { username: 'admin', password: 'admin123' } })
  const preLogoutState = await authApi.storageState()
  const authCookie = preLogoutState.cookies.find(c => c.name === 'tinycrm.auth')
  if (loginRes.status() !== 200 || !authCookie) {
    console.log('FATAL: setup login failed, cannot continue adversarial suite')
    console.log(`status=${loginRes.status()} body=${await loginRes.text()}`)
    process.exitCode = 1
    return
  }

  // ---------- 4: logout does NOT revoke server-side (KNOWN, ACCEPTED LIMITATION - see spec decision D8) ----------
  // ASP.NET Core cookie auth is stateless: SignOutAsync only clears the client cookie, there is no
  // server-side ticket store, so a cookie captured before logout keeps authenticating until it expires.
  // We accept this for now (D8) rather than fix it, so this check is EXPECTED to fail until a real
  // ITicketStore-backed revocation lands. Left failing on purpose - do not weaken the assertion.
  await authApi.post('/api/auth/logout')
  const replay = await newApi({ storageState: { cookies: [authCookie], origins: [] } })
  const replayRes = await replay.get('/api/customers')
  log('4. [KNOWN LIMITATION, D8] Replaying a pre-logout cookie after logout -> 401 (server invalidates, not just client-cleared)',
    replayRes.status() === 401,
    `replayStatus=${replayRes.status()} (200 = D8: logout is client-side only, accepted, not fixed - see spec decisions table)`)

  // ---------- 5: tampered cookie -> 401 not 500 ----------
  const tampered = { ...authCookie, value: authCookie.value.slice(0, -4) + (authCookie.value.slice(-4) === 'AAAA' ? 'BBBB' : 'AAAA') }
  const tamperCtx = await newApi({ storageState: { cookies: [tampered], origins: [] } })
  const tamperRes = await tamperCtx.get('/api/customers')
  log('5. Tampered cookie value -> 401, not 500', tamperRes.status() === 401, `status=${tamperRes.status()}`)

  // ---------- 6: CSRF posture - form-urlencoded login rejected ----------
  const formCtx = await newApi()
  const formRes = await formCtx.post('/api/auth/login', { form: { Username: 'admin', Password: 'admin123' } })
  log('6. POST login as application/x-www-form-urlencoded -> 415 (JSON-only API, no anti-forgery token needed)',
    formRes.status() === 415, `status=${formRes.status()}`)

  // ---------- 7: cookie attributes on login response ----------
  const cookieCtx = await newApi()
  const cookieLoginRes = await cookieCtx.post('/api/auth/login', { data: { username: 'admin', password: 'admin123' } })
  const setCookieHeaders = cookieLoginRes.headersArray().filter(h => h.name.toLowerCase() === 'set-cookie')
  const authSetCookie = setCookieHeaders.find(h => h.value.startsWith('tinycrm.auth='))?.value ?? ''
  const hasHttpOnly = /httponly/i.test(authSetCookie)
  const hasSameSiteLax = /samesite=lax/i.test(authSetCookie)
  log('7. Login Set-Cookie carries HttpOnly and SameSite=Lax',
    hasHttpOnly && hasSameSiteLax, authSetCookie || '(no tinycrm.auth cookie found in response)')

  // fresh authenticated context for the injection / input-edge-case checks (8,9,14,15)
  const api2 = await newApi()
  const login2 = await api2.post('/api/auth/login', { data: { username: 'admin', password: 'admin123' } })
  if (login2.status() !== 200) {
    console.log('FATAL: second setup login failed, cannot continue with authenticated checks')
    process.exitCode = 1
    return
  }

  // ---------- 8: SQL injection payload in search ----------
  const sqli = await api2.get('/api/customers?search=' + encodeURIComponent("' OR 1=1--"))
  let sqliRows = null
  try { sqliRows = await sqli.json() } catch { /* not JSON */ }
  log("8. search=' OR 1=1-- -> 200 with 0 rows (parameterised, table not dumped)",
    sqli.status() === 200 && Array.isArray(sqliRows) && sqliRows.length === 0,
    `status=${sqli.status()} rows=${Array.isArray(sqliRows) ? sqliRows.length : 'n/a'}`)

  // ---------- 9: '%' wildcard behaviour (documented decision D7, not a bug) ----------
  const pct = await api2.get('/api/customers?search=' + encodeURIComponent('%'))
  let pctRows = null
  try { pctRows = await pct.json() } catch { /* not JSON */ }
  log("9. search='%' -> 200, no error (D7: LIKE wildcard passthrough is a known, accepted behaviour)",
    pct.status() === 200 && Array.isArray(pctRows),
    `status=${pct.status()} rows=${Array.isArray(pctRows) ? pctRows.length : 'n/a'} (documented current behaviour, not asserted as a specific count)`)

  // ---------- 10: XSS payload in search, browser-level ----------
  const ctxB = await browser.newContext()
  const pageB = await ctxB.newPage()
  let dialogFired = false
  pageB.on('dialog', async (d) => { dialogFired = true; await d.dismiss() })
  await pageB.goto(BASE + '/login', { waitUntil: 'domcontentloaded' })
  await pageB.fill('input[name="Username"]', 'admin')
  await pageB.fill('input[name="Password"]', 'admin123')
  await pageB.click('button[type="submit"]')
  await pageB.waitForURL(/\/customers/)
  const xssPayload = '<script>window.__xss_fired = true</script><img src=x onerror="window.__xss_fired=true">'
  await pageB.fill('input[name="search"]', xssPayload)
  // SPA now filters as-you-type (debounced ~300ms) - no submit button to click any more.
  await pageB.waitForTimeout(500)
  const xssFired = await pageB.evaluate(() => window.__xss_fired === true)
  const bodyHtml = await pageB.content()
  const rawTagLeaked = bodyHtml.includes('<img src=x onerror=')
  log('10. XSS payload in search does not execute and is not rendered as raw HTML (Vue auto-escapes)',
    !dialogFired && !xssFired && !rawTagLeaked,
    `dialogFired=${dialogFired} xssFired=${xssFired} rawTagLeaked=${rawTagLeaked}`)
  await ctxB.close()

  // ---------- 11: unknown user vs wrong password -> identical response ----------
  const u11a = await newApi()
  const u11b = await newApi()
  const unknownRes = await u11a.post('/api/auth/login', { data: { username: 'no_such_user_xyz', password: 'whatever' } })
  const wrongPwRes = await u11b.post('/api/auth/login', { data: { username: 'admin', password: 'wrongpass' } })
  const unknownBody = stripTraceId(await unknownRes.text())
  const wrongPwBody = stripTraceId(await wrongPwRes.text())
  log('11. Unknown username vs wrong password -> identical status + body (ignoring traceId)',
    unknownRes.status() === wrongPwRes.status() && unknownRes.status() === 401 && unknownBody === wrongPwBody,
    `status ${unknownRes.status()}/${wrongPwRes.status()}, bodies-equal=${unknownBody === wrongPwBody}`)

  // ---------- 12: empty credentials -> 401 never 500 ----------
  const emptyCases = [
    { username: '', password: '' },
    { username: '', password: 'admin123' },
    { username: 'admin', password: '' },
  ]
  const emptyStatuses = []
  for (const body of emptyCases) {
    const c = await newApi()
    const res = await c.post('/api/auth/login', { data: body })
    emptyStatuses.push(res.status())
  }
  log('12. Empty username/password combinations -> 401, never 500',
    emptyStatuses.every(s => s === 401), `statuses=${emptyStatuses.join(',')}`)

  // ---------- 13: username case-insensitive, password case-sensitive ----------
  const upUser = await newApi()
  const upUserRes = await upUser.post('/api/auth/login', { data: { username: 'ADMIN', password: 'admin123' } })
  const upPass = await newApi()
  const upPassRes = await upPass.post('/api/auth/login', { data: { username: 'admin', password: 'ADMIN123' } })
  log('13. Username case-insensitive (ADMIN works), password case-sensitive (ADMIN123 fails)',
    upUserRes.status() === 200 && upPassRes.status() === 401,
    `usernameCaseStatus=${upUserRes.status()} passwordCaseStatus=${upPassRes.status()}`)

  // ---------- 14: bogus status value ignored ----------
  const bogusStatus = await api2.get('/api/customers?status=' + encodeURIComponent('NotARealStatus'))
  let bogusRows = null
  try { bogusRows = await bogusStatus.json() } catch { /* not JSON */ }
  log('14. ?status=<bogus> -> 200 with all 5 rows (invalid enum ignored, not 400/500)',
    bogusStatus.status() === 200 && Array.isArray(bogusRows) && bogusRows.length === 5,
    `status=${bogusStatus.status()} rows=${Array.isArray(bogusRows) ? bogusRows.length : 'n/a'}`)

  // ---------- 15: very long search string ----------
  // Kestrel's default request-line limit is 8KB; a 10,000-char query string trips it and the
  // server correctly rejects the request with 414 before it ever reaches app code. That is the
  // desired outcome, not a bug - a 200 (small buffers/limits raised) is also acceptable. Only a
  // 500 (or anything else) would indicate the server mishandled the oversized input.
  const longSearch = 'a'.repeat(10000)
  const longRes = await api2.get('/api/customers?search=' + longSearch)
  log('15. Very long search is rejected or handled, never 500 (414 = Kestrel request-line limit, the desired outcome)',
    longRes.status() === 200 || longRes.status() === 414, `status=${longRes.status()}`)

  // ---------- 16: malformed JSON body ----------
  const malformedCtx = await newApi()
  const malformedRes = await malformedCtx.post('/api/auth/login', {
    headers: { 'Content-Type': 'application/json' },
    data: '{"username": "admin", "password": ',
  })
  log('16. Malformed JSON body on login -> 400, not 500', malformedRes.status() === 400, `status=${malformedRes.status()}`)

  // ---------- 17: unknown API route ----------
  const nopeCtx = await newApi()
  const nopeRes = await nopeCtx.get('/api/nope')
  const nopeBody = await nopeRes.text()
  log('17. GET /api/nope -> 404 (not the SPA fallback HTML)',
    nopeRes.status() === 404,
    `status=${nopeRes.status()} contentType=${nopeRes.headers()['content-type']} bodyLen=${nopeBody.length}`)

  // ---------- 18: session survives reload (pageA was logged in via the UI right after check 3) ----------
  await pageA.reload()
  await pageA.waitForSelector('table.table tbody tr', { timeout: 5000 }).catch(() => {})
  log('18. Session survives page reload', !pageA.url().includes('/login'), pageA.url())

  // ---------- 19: two contexts don't share a session ----------
  const ctxC = await browser.newContext()
  const pageC = await ctxC.newPage()
  await pageC.goto(BASE + '/customers', { waitUntil: 'domcontentloaded' })
  await pageC.waitForURL(/\/login/).catch(() => {})
  log("19. A brand-new browser context is NOT authenticated (no shared session)",
    pageC.url().includes('/login'), pageC.url())
  await ctxC.close()
}

try {
  await main()
} catch (err) {
  console.log('FATAL ERROR:', err.stack || err)
  process.exitCode = 1
} finally {
  for (const c of apiCtxs) await c.dispose().catch(() => {})
  if (browser) await browser.close().catch(() => {})
  const failed = results.filter(r => !r.ok).length
  console.log(`\nTOTAL: ${results.length}  PASSED: ${results.length - failed}  FAILED: ${failed}`)
  process.exitCode = process.exitCode || (failed ? 1 : 0)
}
