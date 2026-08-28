# Parity change log

Every deviation of the ported e2e suite from the MVC original. Classes:
**Cosmetic** (selectors/waits) — free. **Structural** (URL shape) — listed.
**Semantic** (assertion meaning changes or check dropped) — REQUIRES USER SIGN-OFF.

| # | Original check | Change | Class | Status |
|---|---|---|---|---|
| 1 | `Unauthenticated root redirects to login` — expects `/Account/Login` | SPA route is `/login?returnUrl=…` | Structural | applied |
| 2 | `SEC: POST without CSRF token rejected` | No anti-forgery tokens in the port; replaced by SameSite + JSON content-type assertions | **Semantic** | **awaiting sign-off** |
| 3 | `SEC: POST with invalid CSRF token rejected` | Same as #2 | **Semantic** | **awaiting sign-off** |
| 4 | `slice.mjs` check 3 (`Valid login lands on customers with 5 rows`) and check 4 | Added `await page.waitForSelector('table.table tbody tr')` before counting rows | Cosmetic | applied |

> Tests are frozen before app code is written. Any test edited *after* seeing it fail
> must be added here with a justification.

Row 4 is that justification: the edit was made **after** running `slice.mjs` and observing check 3 fail (`rows=0`). Cause was the script counting table rows immediately after `waitForURL` resolved, racing the SPA's async `onMounted` fetch in `CustomersView.vue`, which populates the table after the route change. The app itself was verified correct independently of the script fix: the auth cookie persisted across a page reload, both `/api/auth/me` and `/api/customers` returned 200 on reload, and 5 rows were present. No assertion was weakened — the check still requires exactly 5 rows.
