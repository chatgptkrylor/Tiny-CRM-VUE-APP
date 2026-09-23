# TinyCRM on Contabo: Deployment Plan

**Target:** https://tinycrm.yantra3loka.com (plain `http://` redirects to https automatically)
**Server:** Contabo VPS `217.217.251.195` (`git.cybermaxsolutions.com`), Ubuntu 24.04, 8 CPU, 23 GB RAM, 277 GB disk free
**Status:** approved 2026-09-23 (copy local data, change passwords, deploy).

---

## 1. Ground rules: existing projects stay untouched

The server already runs production sites. This deployment **adds**, it never modifies.

| Existing on the server | This plan |
|---|---|
| Compose projects `docuq`, `internalkb`, `mailcowdockerized` | Not stopped, restarted, rebuilt or edited |
| Their networks and volumes (`docuq_*`, `internalkb_*`, `mailcowdockerized_*`) | Not touched. TinyCRM gets its own, all prefixed `tinycrm` |
| Host Postgres on 5432/5438, Redis on 6379/6380, Java on 8080, gunicorn/python on 8001/8002/8088/8787 | Not used. TinyCRM runs its **own** Postgres in Docker |
| Host Caddy (owns 80/443, serves ~12 sites) | One new site block **appended**, after a dated backup. No existing block edited |
| Firewall (22, 80, 443, 8080 open) | No change needed |
| `/opt/*` of other projects | TinyCRM lives only in `/opt/tinycrm` |

Every command in this plan runs inside `/opt/tinycrm`, or targets the `tinycrm` compose project or `tinycrm-*` names. There are no `docker system prune` or `docker volume prune` commands, and no global restarts.

## 2. What was found on the server (read-only survey, 2026-09-23)

- **Reverse proxy:** Caddy 2.11.4 as a systemd service, config `/etc/caddy/Caddyfile`. Each site is a block proxying to a port bound to `127.0.0.1` only. Existing projects follow the same convention (`internalkb` uses `127.0.0.1:8095`, `docuq` uses `127.0.0.1:8090`).
- **DNS:** `tinycrm.yantra3loka.com` already resolves to `217.217.251.195`. There is nothing to change at the registrar.
- **Docker:** 29.6.2 with Compose v5.3.1. `vm.max_map_count` is 1048576, enough for Elasticsearch.
- **Free port chosen:** `127.0.0.1:8096`. Ports 8096–8099 are all unused.
- **No existing TinyCRM:** no `/opt/tinycrm`, no `tinycrm` containers, no Caddy entry.
- **Headroom:** about 14 GB of RAM available. TinyCRM is capped at about 3 GB total (section 3).

## 3. Proposed architecture

```
                 Internet
                    │  :80 / :443
        ┌───────────▼─────────────────────────────┐
        │ Host Caddy (existing, systemd)          │
        │  … existing site blocks, unchanged …    │
        │  tinycrm.yantra3loka.com  ← NEW block   │
        │    auto HTTPS (Let's Encrypt)           │
        └───────────┬─────────────────────────────┘
                    │ 127.0.0.1:8096 (loopback only)
   ┌────────────────▼──────────────────────────────────────┐
   │ compose project "tinycrm"  ·  network tinycrm-net     │
   │                                                       │
   │  tinycrm-app   ASP.NET Core 10                        │
   │                serves the Vue SPA + /api              │
   │     │    │                                            │
   │     │    └──────────────► tinycrm-es  Elasticsearch   │
   │     ▼                     8.17.0, single node,        │
   │  tinycrm-db  PostgreSQL 16                no ports    │
   │              no ports published                       │
   │                                                       │
   │  volumes: tinycrm_pgdata · tinycrm_esdata ·           │
   │           tinycrm_dpkeys                              │
   └───────────────────────────────────────────────────────┘
```

This matches the local setup: the same app, PostgreSQL 16 (local is 16.15), and Elasticsearch 8.17.0 in single-node mode with security off. Only the app publishes a port, and only on loopback. The database and Elasticsearch are reachable only from inside `tinycrm-net`, which follows the isolation rules already in `/opt/internalkb/docker-compose.yml`.

| Service | Image | Published | Memory cap | Persistent data |
|---|---|---|---|---|
| `tinycrm-app` | built from this repo (`tinycrm-app:<git-sha>`) | `127.0.0.1:8096 → 8080` | 512 MB | `tinycrm_dpkeys` (login-cookie keys) |
| `tinycrm-db` | `postgres:16` | none | 1 GB | `tinycrm_pgdata` |
| `tinycrm-es` | `docker.elastic.co/elasticsearch/elasticsearch:8.17.0` | none | 1.5 GB (heap 512 MB, same as local) | `tinycrm_esdata` |

Secrets (the Postgres password) go in `/opt/tinycrm/.env` with mode `600`. They are never committed to the repo.

### Files on the server

```
/opt/tinycrm/
├── .env                 # POSTGRES_PASSWORD, APP_PORT, chmod 600
└── src/                 # the exact git commit being deployed (from `git archive`),
                         # including docker-compose.yml and Dockerfile
```

### New Caddy block (appended to the end of the Caddyfile)

```caddy
# TinyCRM — .NET 10 + Vue, /opt/tinycrm (added 2026-09-XX)
tinycrm.yantra3loka.com {
	encode gzip zstd
	reverse_proxy 127.0.0.1:8096 {
		header_up X-Forwarded-Proto {scheme}
	}
}
```

## 4. App changes needed before it can run in a container

Only one line of app code changed. The rest is configuration:

1. **Listen address.** `Program.cs` hardcoded `UseUrls("http://localhost:5174")`, which can't be reached from outside a container. It now applies only in Development, where the Vite proxy needs it. In the container, the aspnet image's default port 8080 applies.
2. **Trust Caddy's `X-Forwarded-Proto`.** Handled by the built-in `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` setting in the compose file, with no code. Without it, the login cookie (`SecurePolicy = SameAsRequest`) would be sent without the `Secure` flag over HTTPS.
3. **Persist login-cookie keys.** ASP.NET already writes them to `~/.aspnet/DataProtection-Keys`. The `Dockerfile` creates that folder for the non-root `app` user, and compose mounts the `tinycrm_dpkeys` volume on it. Verified: a login survives an app restart.
4. **`Dockerfile`, `.dockerignore`, `docker-compose.yml`** in the repo root. The build is multi-stage: Node builds the Vue app into `wwwroot`, `dotnet publish` runs, and the runtime image is `aspnet:10.0`.
5. **E2E tests read `BASE_URL` and `ADMIN_PASSWORD`**, with defaults `http://localhost:5173` and `admin123`. The same scripts run against local and Contabo.

Production configuration comes entirely from the compose file and `/opt/tinycrm/.env` (`POSTGRES_PASSWORD`, `APP_PORT`). `appsettings.Development.json` is not used on the server.

## 5. Data: making local and Contabo identical

For "local and Contabo tests should be the same", the two databases must hold the same data. The tests compare real row counts (for example, `acme` → 167 rows).

**Recommended: copy the local database to the server.**
`pg_dump` the local `tinycrmvue` database (9.7 MB, 2,006 customers, 2 users), then restore it into `tinycrm-db` before the app first starts. Postgres 16 → 16, no version issues. On startup the app sends all customers to Elasticsearch, so search has the same data too.

*Alternative:* start empty and let the seeder create its 5 demo customers. The app works, but search counts and list sizes won't match local, so the test results won't be comparable.

## 6. Deployment steps

Nothing here runs until this plan is approved.

1. **Locally:** make the section 4 changes. Build the Docker image and run the full stack with `docker compose` on this machine. Run all tests against it and commit.
2. **Server, prepare:** create `/opt/tinycrm`, copy the compose file and `.env` there, and upload the code with `git archive <sha> | ssh … tar -x -C /opt/tinycrm/src`. The server needs no GitHub login, and what runs is exactly the tested commit.
3. **Server, build and start:** `docker compose -p tinycrm build`, then start `tinycrm-db`, restore the dump, then `docker compose -p tinycrm up -d`. Check that `curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8096/api/auth/me` returns `401`, which means the app is up and signed-out requests are refused.
4. **Server, add Caddy block:**
   - `cp /etc/caddy/Caddyfile /etc/caddy/Caddyfile.bak.<timestamp>-pre-tinycrm` (the existing backup naming style)
   - append the block from section 3
   - `caddy validate --config /etc/caddy/Caddyfile`, and **stop here if it fails**
   - `systemctl reload caddy` (a graceful reload, as the systemd unit defines; existing sites keep serving)
5. **Verify** the new site (section 7), and confirm the existing sites still answer (for example `internalkb.yantra3loka.com` and `docq.yantra3loka.com` return the same status as before).

## 7. Parity testing: local vs Contabo

The same test suites run against both environments, and the results must match line by line.

| Suite | Local | Contabo |
|---|---|---|
| Backend xUnit tests | `dotnet test` (41 tests; they run in-process, so this is a local gate before deploy) | same commit, so identical by construction |
| E2E login flow | `node slice.mjs` | `BASE_URL=https://tinycrm.yantra3loka.com node slice.mjs` |
| Security checks | `node adversarial.mjs` | `BASE_URL=https://tinycrm.yantra3loka.com node adversarial.mjs` |
| Search checks | `acme`, `acm`, `ACME`, `john`, `peter`, no-match, SQL injection, paging, status filter | same queries, same expected counts |

**Expected:** identical PASS/FAIL on both. Today that's 3/4 for the login flow and 17/19 for the security checks. The failures are known and are not bugs: two outdated tests that expect the 5 starter customers, and the D8 logout limitation. One difference is **expected and correct**: on Contabo the login cookie also carries `Secure`, because the site is HTTPS.

## 8. Rollback

This affects only TinyCRM, in reverse order:

1. Restore `/etc/caddy/Caddyfile.bak.<timestamp>-pre-tinycrm`, validate, and `systemctl reload caddy`.
2. `cd /opt/tinycrm && docker compose -p tinycrm down`. Add `-v` only if the TinyCRM data should also be deleted.

No other project is involved at any step.

## 9. Decisions and risks to confirm

1. **Demo passwords on a public site.** The database has `admin` / `admin123` and `demo` / `demo123`, and they're written in this repo. Anyone can log in once the site is live. The app has no change-password screen. **Recommendation:** set new passwords for both accounts before go-live, with a one-off command that writes new hashes, and keep the old ones only in the local database.
2. **Data choice:** copy local data (recommended, needed for parity) or start with fresh seed data (section 5).
3. **Elasticsearch outage behaviour** (open from the previous fix): if `tinycrm-es` dies while the app is running, searches return errors and customer saves fail after the database write. `restart: unless-stopped` covers crashes, but the code gap remains.
4. **Backups:** no scheduled backup of `tinycrm_pgdata` is included. Add a nightly `pg_dump` cron in `/opt/tinycrm` if this holds real data.
5. **Logout limitation (D8):** a cookie copied before logout keeps working for up to 30 minutes. This is known and accepted locally, but it matters more on the public internet.
