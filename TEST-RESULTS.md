# Test Results — TinyCRM .NET 10 + Vue port

**Date:** 2026-08-28
**Branch:** `feature/dotnet10-vue-port`
**Scope:** Phase 0 (skeleton) + Phase 1 (login + customer list, end to end)

---

## Summary

| Suite | Result |
|---|---|
| Build (`dotnet build TinyCrmVue.sln`) | 0 warnings, 0 errors |
| Backend tests (xUnit) | **15 / 15 pass** |
| Frontend build (`npm run build`) | pass |
| End-to-end slice (Playwright) | **4 / 4 pass** |
| Adversarial (Playwright) | **18 / 19 pass** — 1 known, documented limitation |

Everything passes except one check that is failing **on purpose**. See D8 below.

---

## What the app does today

Log in as `admin` / `admin123`, land on the customer list, search and filter it.
Backend is ASP.NET Core 10 with Entity Framework Core 10 over SQL Server LocalDB.
Frontend is Vue 3 built with Vite. One origin in production, so the login cookie
needs no CORS and no token in browser storage.

Not built yet (Phase 2 onward): create/edit/delete customers, interactions,
dashboard, reports, CSV export.

---

## Adversarial tests — what we tried to break

### Attacks that failed (good)

| Attack | Result |
|---|---|
| SQL injection in search (`' OR 1=1--`) | 0 rows returned, no data leaked. Queries are parameterised. |
| Cross-site script (XSS) in search | Never executes. Vue escapes it. |
| Reading the API with no login | 401 on every endpoint. |
| Opening a page with no login | Redirected to login, original URL preserved. |
| Forging a request from another site | Rejected. A plain HTML form gets **415** because the API only accepts JSON. |
| Stealing the cookie with JavaScript | Blocked. Cookie is `HttpOnly` and `SameSite=Lax`. |
| Tampering with the cookie value | 401. Fails closed, no server error. |
| Guessing valid usernames by response | Unknown user and wrong password return identical replies. |
| Guessing valid usernames by timing | Both paths do the same password hashing work. |
| Reusing one user's session in another browser | Sessions stay separate. |
| Empty or malformed login data | 401 / 400. Never a 500. |
| Bogus status filter value | Ignored, returns 200. |
| 10,000-character search | Rejected with 414 by the web server, as it should be. |

### The one failing check — D8

**Logging out does not cancel the session on the server.**

If someone copies the login cookie before you log out, that copy still works
afterwards, until it expires (30 minutes).

Why it is failing on purpose: ASP.NET Core cookie login is stateless. Logging out
deletes the cookie in the browser but the server keeps no list of live sessions.
Cancelling properly needs a server-side session store, which is real work, not a
small patch.

**Honest note:** the old MVC app did not have this problem. It used server-side
sessions, and logout genuinely killed them. This is a step backwards from the
original.

Why it is acceptable for now: to use it, an attacker must already have the cookie.
Getting it is hard — `HttpOnly` stops JavaScript reading it, `SameSite=Lax` stops
other sites sending it, and it expires in 30 minutes.

**Decide before real users touch this.** It is written up as decision D8 in the
design spec, and Phase 2 owns fixing it if the app ships for real.

---

## Problems found and fixed during this round

**Unknown API routes returned a web page instead of 404.**
`GET /api/nope` returned 200 with the app's HTML. Fixed — it now returns 404, and
real routes and page routing still work. Left unfixed it would have made every
future typo in an API address look like a broken-JSON error instead of "not found".

**A wrong test, not a wrong app.** The suite expected a 10,000-character search to
return 200. The server returns 414. The server is right; the test was wrong. Fixed
the test.

---

## Problems found earlier and fixed

**Login leaked which usernames exist.** The code answered instantly for an unknown
username but did the slow password check for a real one. The gap was measurable.
Both paths now do the same work.

**Two tests could not fail.** The delete-cascade test passed even with the database
rule switched off, and the login-timing test passed with or without the fix. Both
rewritten, and the cascade fix was proven by turning the rule off and watching the
test fail.

**The end-to-end gate had a hardcoded pass.** One check was written to always
succeed. It now checks the real error message.

---

## How to run these yourself

```powershell
cd C:\Users\Administrator\Desktop\Tiny-CRM-VUE-APP

dotnet build TinyCrmVue.sln
dotnet test tests\TinyCrm.Api.Tests\TinyCrm.Api.Tests.csproj

# start both servers, then:
node tests\e2e\slice.mjs         # 4 checks
node tests\e2e\adversarial.mjs   # 19 checks, 1 expected failure (D8)
```

Backend runs on port 5174, frontend on 5173.

`tests/e2e/legacy-mvc/` holds the old app's test suites. **Do not run them** — they
point at the retired MVC app and change its data. They are kept only as the
specification for reaching full feature parity later.
