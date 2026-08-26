# Tiny CRM — Project Documentation

A server-rendered CRM web application built with **ASP.NET MVC 5** on **.NET Framework 4.7.2**, using **Razor views**, **server-side session state**, and an **in-memory data store**. Designed for a one-day assignment scope.

---

## 1. Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET MVC 5.2.9 (System.Web.Mvc) |
| Runtime | .NET Framework 4.7.2 |
| Views | Razor 3.2.9 (`.cshtml`) |
| Session | InProc server-side session (30-min timeout) |
| Data | In-memory static `DataStore` (no database) |
| Auth | Custom session-based filter (`AuthAttribute`) |
| Passwords | SHA256 hashing (`PasswordHasher`) |
| Tests (unit) | MSTest 3.6.1 (28 tests) |
| Tests (e2e) | Playwright + Chromium (45 tests) |
| Tests (adversarial) | Playwright + Chromium (48 tests) |
| Server | IIS Express on `http://localhost:54322/` |

---

## 2. Frontend

The frontend is **server-rendered Razor** — there is no SPA, no client framework, no `fetch`, no `localStorage`. The browser receives complete HTML pages.

### Layout
- `Views/Shared/_Layout.cshtml` — master page with sticky top nav (Dashboard / Customers / Reports), user display name, Sign-out button, and `TempData` flash messages (success/error banners).
- `Views/_ViewStart.cshtml` — sets the shared layout for all pages.
- `Content/Site.css` — single hand-written stylesheet (no Bootstrap). Provides card, table, badge, form, bar-chart, stat-card, and alert components.

### Pages
| Route | View | Purpose |
|---|---|---|
| `/Account/Login` | `Account/Login.cshtml` | Login form (Username + Password) |
| `/` | `Dashboard/Index.cshtml` | Stats, status/type bars, recent interactions |
| `/Customers` | `Customers/Index.cshtml` | Customer list, search + status filter (auto-submit dropdown) |
| `/Customers/Create` | `Customers/Create.cshtml` | New customer form |
| `/Customers/Details/{id}` | `Customers/Details.cshtml` | Customer record + interaction history |
| `/Customers/Edit/{id}` | `Customers/Edit.cshtml` | Edit customer form |
| `/Customers/Delete/{id}` | `Customers/Delete.cshtml` | Delete confirmation |
| `/Interactions/Create?customerId={id}` | `Interactions/Create.cshtml` | Log interaction form |
| `/Reports` | `Reports/Index.cshtml` | Status summary, type summary, customer overview, CSV export |
| `/Reports/ExportCsv` | (file download) | `customers.csv` |

### Behavior notes
- The status filter dropdown auto-submits on change (`onchange="this.form.submit()"`) — no Filter button needed.
- Search box submits on Enter (native GET form).
- All POST forms include `@Html.AntiForgeryToken()`.
- Validation messages render server-side via `@Html.ValidationMessageFor` / `@Html.ValidationSummary`.
- No client-side JavaScript libraries are bundled (server-side validation only).

---

## 3. Backend

### Controllers (all in `TinyCrm.Controllers`)

| Controller | Actions | Notes |
|---|---|---|
| `AccountController` | `Login` (GET/POST), `Logout` (POST) | Validates against `DataStore.Users`, sets `Session["UserId/Username/DisplayName"]` |
| `DashboardController` | `Index` | Builds counts, status/type breakdowns, recent 5, follow-up count |
| `CustomersController` | `Index`, `Create` (GET/POST), `Edit` (GET/POST), `Details`, `Delete` (GET/POST) | Full CRUD; nullable `int? id` on GET actions for safe 404 |
| `InteractionsController` | `Create` (GET/POST), `Delete` (POST) | Logs interactions against a customer; future-date guard |
| `ReportsController` | `Index`, `ExportCsv` | Reporting + CSV download with comma/quote escaping |

### Models (`TinyCrm.Models`)

| Model | Fields |
|---|---|
| `Customer` | Id, Name (req, 2–100), Company, Email (email fmt), Phone (regex), Status (enum), Notes (max 500), CreatedAt, LastInteractionDate, Interactions |
| `Interaction` | Id, CustomerId, Type (enum), Subject (req, 3–200), Notes (max 2000), InteractionDate (req, ≤ today), CreatedAt, CustomerName |
| `User` | Id, Username, PasswordHash, DisplayName |
| `Enums` | `CustomerStatus { Lead, Contact, Customer }`, `InteractionType { Call, Email, Meeting, Note }` |
| `DashboardViewModel` | TotalCustomers, TotalInteractions, CustomersByStatus, InteractionsByType, RecentInteractions, NeedsFollowUps |
| `ReportViewModel` | StatusSummary, InteractionTypeSummary, Customers |

### Data Store (`Models/Repositories/DataStore.cs`)
- Static class holding `List<Customer>`, `List<Interaction>`, `List<User>`.
- Seeded once on first access (`DataStore.Seed()` called in `Application_Start`) with 5 sample customers and 6 interactions.
- All access is `lock`-thread-safe.
- **In-memory only** — data resets to seed when IIS Express stops. No persistence.

### Infrastructure (`TinyCrm.Infrastructure`)

| File | Role |
|---|---|
| `AuthAttribute.cs` | `IAuthorizationFilter` — runs before `ValidateAntiForgeryToken`. Redirects to `/Account/Login` if `Session["UserId"]` is null. Skips `AccountController`. |
| `PasswordHasher.cs` | SHA256 hash + verify (static helpers). |

### Session Handling
Configured in `Web.config`:
```xml
<sessionState mode="InProc" timeout="30" cookieless="false" />
```
- Server-side, in-process, cookie-based.
- 30-minute idle timeout.
- Stores `UserId`, `Username`, `DisplayName` after login.
- `AuthAttribute` enforces session on every request (except `Account` controller).

---

## 4. API / Routes

The app exposes **HTML routes** (not a JSON REST API). The default route is:
```csharp
routes.MapRoute("Default", "{controller}/{action}/{id}",
    defaults: new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional });
```

### Route table
| Method | Path | Action | Returns |
|---|---|---|---|
| GET | `/` | Dashboard.Index | HTML |
| GET | `/Account/Login` | Account.Login | HTML form |
| POST | `/Account/Login` | Account.Login | redirect (302) |
| POST | `/Account/Logout` | Account.Logout | redirect (302) |
| GET | `/Customers` | Customers.Index | HTML list |
| GET | `/Customers/Create` | Customers.Create | HTML form |
| POST | `/Customers/Create` | Customers.Create | redirect / HTML (validation) |
| GET | `/Customers/Details/{id}` | Customers.Details | HTML / 404 |
| GET | `/Customers/Edit/{id}` | Customers.Edit | HTML / 404 |
| POST | `/Customers/Edit/{id}` | Customers.Edit | redirect / HTML |
| GET | `/Customers/Delete/{id}` | Customers.Delete | HTML / 404 |
| POST | `/Customers/Delete/{id}` | Customers.Delete | redirect |
| GET | `/Interactions/Create?customerId={id}` | Interactions.Create | HTML / 404 |
| POST | `/Interactions/Create` | Interactions.Create | redirect / HTML |
| POST | `/Interactions/Delete/{id}` | Interactions.Delete | redirect |
| GET | `/Reports` | Reports.Index | HTML |
| GET | `/Reports/ExportCsv` | Reports.ExportCsv | `text/csv` download |

All non-`Account` routes require a valid session (via `AuthAttribute`).

---

## 5. Testing

The project has **three test layers totalling 121 tests — all passing.**

### 5.1 Unit Tests — MSTest (`TinyCrm.Tests/`, 28 tests)
Run with:
```powershell
dotnet test "TinyCrm.sln"
```
- `DataStoreTests.cs` (12) — CRUD operations, interactions, user lookup, follow-up recalculation.
- `PasswordHasherTests.cs` (7) — SHA256 hash determinism, verify, empty inputs.
- `ModelValidationTests.cs` (9) — DataAnnotations on `Customer` / `Interaction` (required fields, length, email format).

### 5.2 End-to-End Tests — Playwright (`e2e/run-tests.mjs`, 45 tests)
Run with:
```powershell
# IIS Express must be running on :54322
node e2e/run-tests.mjs
```
Covers full user flows via a headless Chromium browser:
- Login / logout / wrong password / case-insensitive username
- Dashboard rendering (stats, bars, recent)
- Customer list, search, status filter, CRUD (create valid/invalid, edit, details, delete confirm/cancel)
- Interaction logging (valid, future-date rejection), interaction delete
- Reports page, CSV export (headers + data rows)
- Auth protection (unauthenticated redirect, post-logout redirect)
- Duplicate-button audit (no redundant Details / New-customer buttons)

### 5.3 Adversarial Tests — Playwright (`e2e/adversarial.mjs`, 48 tests)
Run with:
```powershell
node e2e/adversarial.mjs
```
Covers security, validation edge cases, and broken flows:

**Security (15 tests):**
- Unauthenticated GET to every protected route → redirect to login
- Unauthenticated POST → blocked (redirect, not 500)
- POST without CSRF token → rejected
- POST with invalid CSRF token → rejected
- XSS payload (`<script>`) in fields → blocked by ASP.NET request validation, HTML-encoded on render
- Open-redirect on `returnUrl` → external URL ignored, goes to dashboard
- Session cookie present after login
- Post-logout access to protected routes → redirect to login

**Validation (13 tests):**
- Name boundary: 2 chars valid, 1 char invalid, 100 valid, 101 invalid, empty invalid
- Invalid email → rejected
- Invalid phone (letters) → rejected
- Interaction future date → rejected
- Interaction subject boundary: 3 valid, 2 invalid, empty invalid
- Huge Notes (10000 chars) → rejected by max-length

**Edge cases (14 tests):**
- Nonexistent customer: Details / Edit / Delete GET / Interaction → 404
- Invalid status query string → graceful 200 (shows all)
- Negative customer ID → 404
- Non-integer customer ID → 404 (not 500)
- Huge search string (2000 chars) → 200
- Delete nonexistent customer / interaction → graceful
- CSV export escapes commas and quotes
- Unknown route → 404
- Empty search → 200
- POST interaction for nonexistent CustomerId → 404

**Broken flows (6 tests):**
- Empty login credentials → rejected
- Wrong password → rejected
- Case-insensitive username login → works
- Rapid double-submit → at least one creates
- Invalid status enum value → no 500
- CSV export correct Content-Type + Content-Disposition

---

## 6. Adversarial Testing — Issues Found & Fixed

### Issue 1: Unauthenticated POST returned 500 instead of redirecting to login
- **Symptom:** POSTing to `/Customers/Create` while logged out returned HTTP 500 (`HttpAntiForgeryException`) instead of redirecting to the login page.
- **Root cause:** `AuthAttribute` was an `ActionFilterAttribute` (`OnActionExecuting`), which runs **after** `ValidateAntiForgeryToken` (an authorization filter). The anti-forgery check threw before the auth check had a chance to redirect.
- **Fix:** Changed `AuthAttribute` to implement `IAuthorizationFilter` (`OnAuthorization`), which runs in the authorization stage — before anti-forgery validation. Now unauthenticated POSTs cleanly redirect to login.
- **File:** `Infrastructure/AuthAttribute.cs`

### Issue 2: Non-integer route parameter caused 500 (NullReferenceException)
- **Symptom:** `/Customers/Details/abc` (non-int id) returned HTTP 500 `ArgumentException: The parameters dictionary contains a null entry for parameter 'id' of non-nullable type 'System.Int32'`.
- **Root cause:** Action signatures used `int id` (non-nullable). When the route value couldn't bind to `int`, the model binder passed null, throwing.
- **Fix:** Changed `Details`, `Edit`, `Delete` (GET) in `CustomersController` and `Create` (GET) in `InteractionsController` to use `int? id` / `int? customerId`, returning `HttpNotFound()` when null.
- **Files:** `Controllers/CustomersController.cs`, `Controllers/InteractionsController.cs`

### Issue 3: Search with null customer fields caused NullReferenceException
- **Symptom:** Searching (`?search=...`) threw `NullReferenceException` at `CustomersController.Index` when any customer had a null `Email`, `Company`, or `Name` field.
- **Root cause:** `c.Name.Contains(s)` throws when `c.Name` is null. Customers created via the create form with empty optional fields stored them as null.
- **Fix:** Added null guards: `(c.Name != null && c.Name.Contains(s)) || ...`
- **File:** `Controllers/CustomersController.cs`

### Issue 4 (not a bug, verified): XSS payloads blocked by built-in request validation
- **Symptom:** Submitting `<script>alert(1)</script>` in a Name field returned HTTP 500 (`HttpRequestValidationException`).
- **Assessment:** This is **ASP.NET's built-in request validation** — a security feature that blocks potentially dangerous form input. The 500 response confirms the payload is rejected and never stored. Additionally, Razor HTML-encodes all model output by default, so even if stored, it would render as text.
- **Action:** No code change — this is correct security behavior. The adversarial test was updated to assert the payload is blocked (not stored).

### Issue 5 (not a bug, verified): Open redirect on `returnUrl`
- **Symptom:** Login with `returnUrl=https://evil.example.com` did not redirect to evil.
- **Assessment:** `AccountController` uses `Url.IsLocalUrl(returnUrl)` before redirecting, which rejects external URLs. Safe.

---

## 7. Final Test Results

| Suite | Tests | Passed | Failed |
|---|---|---|---|
| MSTest unit (`TinyCrm.Tests`) | 28 | 28 | 0 |
| Playwright e2e (`run-tests.mjs`) | 45 | 45 | 0 |
| Playwright adversarial (`adversarial.mjs`) | 48 | 48 | 0 |
| **Total** | **121** | **121** | **0** |

All three suites pass against the running IIS Express instance on `http://localhost:54322/`.

---

## 8. How to Run

### Prerequisites
- .NET Framework 4.7.2 (SDK or targeting pack)
- `dotnet` CLI (for build/test)
- IIS Express (at `C:\Program Files\IIS Express\iisexpress.exe`)
- Node.js + npm (only for Playwright tests)

### Build
```powershell
dotnet build "TinyCrm.sln"
```

### Run the app
```powershell
& "C:\Program Files\IIS Express\iisexpress.exe" /config:".applicationhost.config" /site:TinyCrm /systray:false
```
Open `http://localhost:54322/` → login with `admin` / `admin123` (or `demo` / `demo123`).

### Run all tests
```powershell
# Unit tests
dotnet test "TinyCrm.sln"

# E2e + adversarial (IIS Express must be running)
cd e2e
node run-tests.mjs
node adversarial.mjs
```

---

## 9. Project Structure (key files only)

```
Tiny-CRM-App/
├── TinyCrm.sln                         Solution
├── .applicationhost.config             IIS Express config (port 54322)
├── TinyCrm/                            Main web app
│   ├── Global.asax.cs                  App entry: routes, filters, seed
│   ├── Web.config                      Session (InProc, 30min), anti-forgery
│   ├── App_Start/                      RouteConfig, FilterConfig, BundleConfig
│   ├── Controllers/                    Account, Dashboard, Customers, Interactions, Reports
│   ├── Models/                         Customer, Interaction, User, Enums, ViewModels
│   │   └── Repositories/DataStore.cs   In-memory "database" (static lists)
│   ├── Infrastructure/                 AuthAttribute, PasswordHasher
│   ├── Views/                          Razor .cshtml files (Account, Customers, Dashboard, Reports, Interactions, Shared)
│   └── Content/Site.css                Styling
├── TinyCrm.Tests/                      MSTest unit tests (28)
└── e2e/                                Playwright tests (45 + 48)
    ├── run-tests.mjs                   End-to-end functional tests
    └── adversarial.mjs                 Security/edge-case tests
```