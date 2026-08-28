# Tiny CRM — Port to .NET 10 + Vue 3 (Vite)

**Date:** 2026-08-27
**Status:** Design v2 — hardened after adversarial review
**Source:** `demo-dotnet-mvc-4.7` @ `2dd334a` (ASP.NET MVC 5, .NET Framework 4.7.2, EF 6.4.4 model-first)
**Target:** ASP.NET Core 10 Web API + Entity Framework Core 10 + Vue 3 SPA built with Vite

---

## 0. Hard constraint — working directory

**All work happens in `C:\Users\Administrator\Desktop\Tiny-CRM-VUE-APP` and nowhere else.**

`C:\Users\Administrator\Desktop\Tiny-CRM-App` (the shipped MVC 5 / EF6 app) is **read-only
reference material**. It is never edited, moved, rebuilt, restarted or committed to as part
of this port. It may only be *read* — to compare behaviour or copy a file forward.

Enforced by these separations, each already in this design:

| Resource | MVC app (do not touch) | Vue port (this project) |
|---|---|---|
| Folder | `Tiny-CRM-App` | `Tiny-CRM-VUE-APP` |
| Database | LocalDB `TinyCrm` | LocalDB `TinyCrmVue` |
| Test database | `TinyCrmTests` | `TinyCrmVueTests` |
| HTTP port | `54322` (IIS Express) | `5173` / `5174` (Vite / Kestrel) |
| Git remote | `demo-dotnet-mvc-4.7` | separate repo (see §10) |

The running IIS Express instances serving the MVC app on `:54322` are left alone.

---

## 1. Goal

Port the existing Tiny CRM to a modern stack **at full feature parity**. Nothing new is added;
nothing existing is dropped. The port succeeds when the ported app passes the same
functional checks the current app passes today.

Non-goals: new features, redesign, multi-tenancy, role-based permissions, cloud deployment.

---

## 2. Why each layer must change

| Concern | Today | Ported | Reason |
|---|---|---|---|
| Runtime | .NET Framework 4.7.2 | .NET 10 | Requested |
| ORM | EF 6.4.4, model-first EDMX | **EF Core 10**, code-first | EF6 cannot load on .NET 10; EF Core has no EDMX |
| UI | Razor `.cshtml`, server-rendered | Vue 3 SPA (Vite) | Requested |
| Auth | `Session["UserId"]` + `AuthAttribute` | Cookie authentication | Session-in-view doesn't fit a SPA |
| Passwords | SHA-256, unsalted | **PBKDF2** (`PasswordHasher<T>`) | Unsalted SHA-256 is weak; reseeding anyway |
| Validation | DataAnnotations in Razor | DataAnnotations (server) + Vue (client) | Client checks are UX only; server stays authoritative |

Entity Framework is retained throughout — only the generation changes (EF6 → EF Core).

---

## 3. Architecture

Two projects, **one origin in production**:

```
Tiny-CRM-VUE-APP/
├── TinyCrm.sln
├── src/
│   ├── TinyCrm.Api/            ASP.NET Core 10 Web API
│   │   ├── Program.cs          DI, auth, middleware, SPA fallback
│   │   ├── Controllers/        Auth, Customers, Interactions, Dashboard, Reports
│   │   ├── Data/               TinyCrmDbContext, entity config, seeder
│   │   ├── Models/             Customer, Interaction, User, enums
│   │   ├── Services/           CustomerService, InteractionService, UserService
│   │   ├── Dtos/               Request/response contracts
│   │   └── wwwroot/            ← built SPA lands here (production)
│   └── tiny-crm-web/           Vue 3 + Vite + TypeScript
│       ├── src/views/          Login, Dashboard, Customers*, Interactions, Reports
│       ├── src/components/     Shared UI
│       ├── src/api/            typed fetch wrappers
│       └── vite.config.ts      dev proxy → Kestrel
└── tests/
    ├── TinyCrm.Api.Tests/      xUnit integration tests
    └── e2e/                    Playwright parity suite
```

**Dev:** Vite dev server (`:5173`) with HMR proxies `/api/*` to Kestrel (`:5174`).
**Prod:** `npm run build` emits to `TinyCrm.Api/wwwroot`; ASP.NET Core serves SPA + API on one port.

Single origin is the load-bearing decision: it means **no CORS**, and the auth cookie is
plain `SameSite=Lax; HttpOnly` — no token in `localStorage`, so no XSS token theft.

---

## 4. Data layer

Three entities, unchanged in shape:

- **Customer** — Id, Name, Company, Email, Phone, Status (enum), Notes, CreatedAt, LastInteractionDate?
- **Interaction** — Id, CustomerId → Customer, Type (enum), Subject, Notes, InteractionDate, CreatedAt
- **User** — Id, Username, PasswordHash, DisplayName

Configured with `IEntityTypeConfiguration<T>` (max lengths, required, indexes). Two rules
carried over deliberately:

1. **Cascade delete** Customer → Interactions (`DeleteBehavior.Cascade`). This is the bug that
   bit the EF6 migration — the conceptual model lacked the cascade and deletes threw on the
   non-nullable FK. EF Core infers cascade for required relationships, but the design states
   it explicitly and a test pins it.
2. **Case-insensitive username lookup** — relies on the default SQL Server collation, as today.

**Schema creation:** EF Core migrations (`dotnet ef migrations add InitialCreate`), requiring
`dotnet tool install --global dotnet-ef`. Migrations over `EnsureCreated()` because the
schema becomes versioned and reviewable.

**Database:** a *separate* LocalDB database `TinyCrmVue`, so the ported app cannot disturb the
existing `TinyCrm` database. Same seed: users `admin`/`admin123` and `demo`/`demo123`,
5 customers, 6 interactions.

**Password migration note:** hashes are rewritten with PBKDF2 at seed time. Existing SHA-256
hashes in the old database are *not* migrated — the two apps have separate databases and
separate user rows. Documented so nobody expects shared logins.

---

## 5. Auth

ASP.NET Core cookie authentication:

- `POST /api/auth/login` — validate, `SignInAsync`, return the user
- `POST /api/auth/logout` — `SignOutAsync`
- `GET  /api/auth/me` — current user, or 401

Every other controller carries `[Authorize]`. Unauthenticated API calls return **401**
(not an HTML redirect — the SPA needs a status code). The Vue router guard redirects to
`/login`, preserving the attempted URL, matching today's `returnUrl` behaviour.

**Anti-forgery.** Cookie auth is CSRF-susceptible. Two layers *mitigate* it (they do not
fully replace MVC's `ValidateAntiForgeryToken`, and this is a conscious trade-off):

1. `SameSite=Lax` — the auth cookie is not sent on cross-site POST/PUT/DELETE.
2. JSON-only API — endpoints require `Content-Type: application/json`, which an HTML form
   cannot produce; a `fetch` that sets it triggers a CORS preflight that fails (no CORS policy).

Residual risk accepted: `SameSite=Lax` still sends the cookie on top-level **GET** navigation,
so **no GET endpoint may mutate state**. The only state-changing verbs are POST/PUT/DELETE.
The CSV export is a GET but is read-only, and its response cannot be read cross-origin.

Cookie flags: `HttpOnly`, `SameSite=Lax`, and `Secure` **only when served over HTTPS** —
setting `Secure` unconditionally makes login fail silently over plain-http dev.

---

## 6. API surface

| Method | Route | Replaces |
|---|---|---|
| POST | `/api/auth/login` \| `/logout`, GET `/me` | AccountController |
| GET | `/api/customers?search=&status=` | Customers/Index |
| GET | `/api/customers/{id}` | Customers/Details |
| POST | `/api/customers` | Customers/Create |
| PUT | `/api/customers/{id}` | Customers/Edit |
| DELETE | `/api/customers/{id}` | Customers/Delete |
| POST | `/api/interactions` | Interactions/Create |
| DELETE | `/api/interactions/{id}` | Interactions/Delete |
| GET | `/api/dashboard` | DashboardController |
| GET | `/api/reports` | Reports/Index |
| GET | `/api/reports/customers.csv` | Reports/ExportCsv |

Search/status filtering moves **into the database** (`IQueryable` composed server-side).
Today the controller calls `GetAll()` then filters in memory — fine for 5 rows, wasteful in principle.

> **DECISION D1 — search becomes case-insensitive (deliberate behaviour change).**
> Today's in-memory `string.Contains` is **ordinal, case-sensitive**: `?search=acme` finds
> nothing. Pushed into SQL Server, the default collation is **case-insensitive**, so it will
> match "Acme Corp". This is what users expect, so we adopt it — but it is a real behaviour
> change that the existing parity suite **cannot detect** (it only ever searches `Acme`, which
> matches under both). A new test asserts lower-case `acme` matches.

Validation errors return **400** with ASP.NET Core's `ValidationProblemDetails`, which the
Vue forms map to per-field messages, reproducing today's `.field-error` output.

---

## 7. Frontend

Vue 3 `<script setup>` + TypeScript, Vite, `vue-router`.

**No Pinia.** Auth state is one small reactive module (`useAuth`); a store library for a
single object is unrequested weight. Add it only if shared state genuinely grows.

Views map 1:1 to today's Razor views: Login, Dashboard, Customers (list/create/edit/details/
delete-confirm), Interaction create, Reports. Existing `Site.css` is carried over largely
as-is so the port is visually comparable and CSS-class-based tests keep working.

Route guard: unauthenticated navigation → `/login?returnUrl=…`. A 401 from any API call
clears auth state and redirects, covering session expiry.

---

## 8. Decisions forced by adversarial review

| # | Decision | Why |
|---|---|---|
| **D1** | Search becomes **case-insensitive** (SQL collation) | Expected behaviour; documented because parity cannot detect the change |
| **D2** | Keep **`DateTime`** for `InteractionDate`; order by `(InteractionDate DESC, Id DESC)` | `DateOnly` would collapse same-day interactions into ties and make ordering — and two tests — nondeterministic. The missing tie-break is a latent flake **today** |
| **D3** | CSV export stays a plain `<a href="/api/reports/customers.csv">` navigation | A `fetch`→blob→synthetic-link implementation produces neither `content-disposition` nor `content-type` at browser level, silently breaking two assertions |
| **D4** | `Secure` cookie flag only under HTTPS | Unconditional `Secure` fails login silently in http dev |
| **D5** | Pin the SDK with **`global.json`** to 10.0.400 | Two SDKs installed (8.0.424, 10.0.400); resolution must be deterministic |
| **D6** | `src/TinyCrm.Api/wwwroot/` build output is **gitignored** | Built SPA must not be committed |
| **D7** | `LIKE` wildcards in search are NOT escaped; Phase 2 owns the fix | `search` is interpolated into `EF.Functions.Like`, so `%` and `_` act as wildcards where the old app's ordinal `Contains` treated them literally. A **D1-adjacent silent behaviour change** the parity suite cannot detect. Recorded now so it is not rediscovered; Phase 2 escapes them with an `ESCAPE` clause when it builds the search UI |

---

## 9. Testing — and an honest account of its limits

Three layers, mirroring today:

1. **Backend integration tests** (xUnit + `WebApplicationFactory`) against a dedicated
   `TinyCrmVueTests` database, dropped/recreated per run. Port the 19 existing repository
   tests to endpoint level, plus auth tests (401 anonymous, login success/failure).
2. **Component tests** (Vitest) for form-validation logic only — where the real risk sits.
3. **Playwright parity suite** — the existing 45 functional + 48 adversarial checks, adapted.

### 9.1 Why the parity gate is weaker than it looks

The adapted suite is written by the same party writing the app, so a failing test can always
be "adapted" until it passes. **A gate you can edit is not a gate.** Two controls fix this:

**Control 1 — freeze before building.** The suite is adapted and committed **before** any
application code is written. Adapting a test *after* seeing it fail requires an explicit note
in the change log below.

**Control 2 — the semantic-change log.** Every deviation from the original suite is recorded
in `tests/e2e/PARITY-CHANGES.md`, classified:

| Class | Meaning | Review |
|---|---|---|
| **Cosmetic** | selectors, waits, SPA navigation | free |
| **Structural** | URL shape (`/Account/Login` → `/login`) | listed, no sign-off |
| **Semantic** | assertion meaning changes or a check is dropped | **user sign-off required** |

Known **semantic** changes needing sign-off up front:
- Two CSRF tests (`POST without/with invalid token rejected`) have no equivalent — the design
  has no tokens. Replaced by SameSite + content-type assertions. **This deletes two security
  tests**; it must be a decision, not a quiet rewrite.
- Assertions coupled to markup (`errors=6`, `cards===3`, `barRows>=7`) will need loosening.
  Each loosening weakens the gate and is logged.

**Control 3 that is NOT available:** normally the adapted suite would be validated against the
*old* app to prove the adaptation preserved meaning. **That is forbidden here** — the suite is
destructive (creates customers, deletes interactions) and would mutate `Tiny-CRM-App`'s
database, violating §0. Stated so nobody "helpfully" does it later.

---

## 10. Build order — vertical slice first (mandatory)

The port must never be a big-bang rewrite; there must always be something that runs.

**Phase 0 — skeleton.** Solution, `global.json`, API project, Vue project, Vite proxy, CI-less
`dotnet build` + `npm run build` both green. No features.

**Phase 1 — vertical slice (the risk killer).** *One* feature end-to-end through *every* layer:
**login + customer list**. EF Core entity → migration → LocalDB → service → API endpoint →
cookie auth → Vite proxy → Vue view → router guard → **one passing Playwright check**.

Phase 1 exists to prove the integrations that actually break ports: cookie over the dev proxy,
enum serialisation, 401 handling, migration against LocalDB, SPA fallback routing.
**If Phase 1 slips, the design is wrong and we stop and revisit — not push on.**

**Phase 2** — customers CRUD + search/filter (D1 test).
**Phase 3** — interactions (+ cascade delete test, D2 ordering test).
**Phase 4** — dashboard + reports + CSV (D3).
**Phase 5** — full parity suite green; `PARITY-CHANGES.md` reviewed and signed off.

Each phase ends with a working app and a green build. Work stops at any phase boundary
without leaving rubble.

## 11. Risks

| Risk | Mitigation |
|---|---|
| Vue rewrite silently drops a behaviour | Playwright parity suite is the gate |
| Enum serialisation (`Status`) differs — today's forms post `"Lead"` | Serialise enums as strings (`JsonStringEnumConverter`); assert in tests |
| Date handling / ordering | See DECISION D2 — keep `DateTime`, add `Id` tie-break |
| `dotnet-ef` not installed | Explicit install step in the plan |
| Port drifts into a redesign | Parity-only scope; new ideas recorded, not built |
| Two IIS Express/Kestrel instances collide on ports | Ported app uses `:5173`/`:5174`, away from `54322` |

---

## 12. Repository

The `Tiny-CRM-VUE-APP` folder currently has `origin` pointing at **`demo-dotnet-mvc-4.7`**,
which would mix the port into the MVC repo. Before the first commit the remote must be
repointed to a dedicated repo (e.g. `demo-dotnet-vuejs-project`). **Open question — blocks the first commit, not the build.**

---

## 13. Out of scope

New features; visual redesign; SSR/Nuxt; Docker; CI/CD; role-based authorisation;
migrating existing `TinyCrm` data (the port seeds its own database).
