# Contributing to MarketPro

This document explains the **local-first, phase-wise** development workflow that protects production.

> **Golden rule**: Nothing reaches `master` (and therefore production at `https://app.samdigital.ae`) without first passing through local testing **and** the staging branch with the Zero-Regression Checklist. No shortcuts.

---

## 1. Local Development Setup (one-time, ~30 min)

### Prerequisites (install on your dev machine)
- **Docker Desktop** (Windows / Mac)
- **.NET 10 SDK** ([dotnet.microsoft.com](https://dotnet.microsoft.com/download))
- **Node.js 20+** ([nodejs.org](https://nodejs.org/))
- **Git**
- **DBeaver** or any Postgres client (optional but recommended)

### First-time setup

```bash
# 1. Clone the repo
git clone https://github.com/farooquifaraz/MultiChannelMarketingApp.git
cd MultiChannelMarketingApp

# 2. Create your local .env (NEVER commit this file)
cp .env.example .env
cp frontend/.env.example frontend/.env

# 3. Generate a dev-only JWT secret
#    On Linux/Mac:        openssl rand -base64 48
#    On Windows PowerShell:
#       [Convert]::ToBase64String((1..48 | % { Get-Random -Maximum 256 }))
#    Paste the result into Jwt__SecretKey in .env

# 4. Start the local Postgres + Redis + Seq stack
docker compose up -d

# 5. Restore + run the API (separate terminal)
dotnet restore
cd src/MarketingApp.API
dotnet run --urls "http://localhost:5211"

# 6. Run the frontend (another terminal)
cd frontend
npm install
npm run dev    # serves on http://localhost:5173
```

> The API runs idempotent startup migrations on first boot and creates all
> ~18 tables in the empty database. Wait ~10 seconds, then the API health
> endpoint at `http://localhost:5211/health` should return `Healthy`.

### Create your dev admin user
1. Open `http://localhost:5173` in a browser
2. Click **Sign Up / Register**
3. Register with any email you control (e.g. `dev@example.test`)
4. The first user automatically becomes `admin`

### Load dev seed data (optional but recommended)
- Import `samples/dev-contacts.csv` via the Contacts page → **Import CSV** (5 dummy contacts ready for testing).
- Create a new template by copying the body from `samples/dev-template.html` and using subject `Hello {{full_name}} — Welcome to MarketPro`.
- Configure an AI provider in **Settings → AI Assistant** with a Groq dev key if you have one (free tier is plenty for local testing).

### DBeaver connection for local DB inspection
- Host: `localhost`
- Port: `5432`
- Database: `marketingapp`
- User: `postgres`
- Password: whatever you put in `docker-compose.yml` (default: `yourpassword`)

---

## 2. Branching & Promotion Flow (THE rule)

```
┌──────────────────┐        ┌──────────────────┐        ┌──────────────────┐
│  STAGE 1: LOCAL  │   →    │  STAGE 2: STAGE  │   →    │  STAGE 3: LIVE   │
│                  │        │  (manual gate)   │        │  (manual gate)   │
│  feature/<name>  │        │     staging      │        │      master      │
│  Your laptop     │        │     branch       │        │   Hostinger VPS  │
│  Hot reload      │        │  Smoke tests     │        │  deploy.sh runs  │
└──────────────────┘        └──────────────────┘        └──────────────────┘
        ↑                            ↑                            ↑
   Build + unit tests        Smoke tests +              Full regression
   pass locally              manual QA                  checklist passes
                                                         User approves merge
```

### Why this matters
The app is live at `https://app.samdigital.ae`. The main `samdigital.ae`
website + Zoho email are completely untouched (separate hosting), but our
own production users will be onboarding. **Any breakage = real customer pain.**

### Standard feature workflow

```bash
# 1. Pull latest master locally
git checkout master
git pull origin master

# 2. Create a focused feature branch
git checkout -b feature/whatsapp-media-messages

# 3. Make your changes; commit often with descriptive messages
git commit -m "feat(whatsapp): add image payload support to WhatsAppCloudService"

# 4. Run the Zero-Regression Checklist locally (see Section 4)
dotnet build src/MarketingApp.API
dotnet test
cd frontend && npm run build && cd ..
docker compose up -d   # if not already running
# ... do manual smoke tests in browser ...

# 5. Push your feature branch
git push -u origin feature/whatsapp-media-messages

# 6. When local tests pass, open a PR from feature/<name> → staging
#    (NOT directly to master)

# 7. After staging deployment succeeds + manual QA pass,
#    open a SECOND PR from staging → master.
#    User must explicitly approve this PR before merge.

# 8. CI/CD auto-deploys master to production VPS.
```

### What goes on which branch
| Branch | Purpose | Who pushes? | Deploys where? |
|---|---|---|---|
| `master` | Production-quality only | PR from `staging` (user-approved) | `app.samdigital.ae` via CI/CD |
| `staging` | Pre-prod verification | PR from `feature/*` | (optional) `staging.samdigital.ae` |
| `feature/*` | In-progress work | Direct push by author | Nowhere — local only |

---

## 3. The Zero-Regression Checklist

Run this **before promoting a feature branch to `staging`**, and again
before promoting `staging` to `master`. If any item fails, the promotion is
blocked until fixed.

| # | Existing Module | Quick Test | Pass Criteria |
|---|---|---|---|
| 1 | Login + JWT | Login as admin → dashboard loads | Token valid, no 401 |
| 2 | Email send (SMTP) | Send test email from a Group | Delivered status appears |
| 3 | Campaign tracking | Send campaign → click test link | Click count increments |
| 4 | Open tracking pixel | Send → open in client → check report | Open recorded (if `PublicBaseUrl` is reachable) |
| 5 | Inbox polling (IMAP) | Send a reply to your test address → wait 1 min | Reply appears in App Inbox |
| 6 | AI reply suggestions | Click "Generate reply" on an inbox message | Draft appears (no 500) |
| 7 | AI chat (Ask AI) | Open Ask AI tab → ask a question | Answer streams correctly |
| 8 | Suggested questions | Open a new inbox thread | 3 chips appear |
| 9 | AI fallback | Disable primary provider → send | Fallback provider is used silently |
| 10 | Threading | Reply to a reply → check thread | All messages grouped correctly |
| 11 | SignalR real-time | Send reply → watch open inbox tab | Real-time append, no manual refresh |
| 12 | Scheduled campaigns | Schedule for 5 min in the future → wait | Fires correctly at the time |
| 13 | SmtpGroup clone | Clone an existing group | New group with same config |
| 14 | Contacts CRUD + search | Search, paginate, edit, delete | All work |
| 15 | Audit logs | Perform an action → check audit page | Action logged |
| 16 | Notification center | Trigger an event → check the bell | Notification appears |
| 17 | Settings (admin) | Open admin settings | All sections load |

**Tracking discipline**: in your PR description, paste the checklist with
✅ / ❌ for each item.

---

## 4. Engineering Rules — keep existing 32 features safe

These rules prevent the "I just added a new feature and now login is
broken" class of disaster.

1. **Additive DB changes only** — `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`
   or `CREATE TABLE IF NOT EXISTS`. No `DROP`, no `RENAME`, no type
   changes without an explicit migration script. All migrations go into
   `Program.cs` `migrationSql` array.
2. **Feature flags for big changes** — e.g. `SystemSettings.EnableMultiTenancy`,
   default `false`. Roll out by enabling on staging first.
3. **Backward-compatible APIs** — never change an existing endpoint's
   request/response shape in a breaking way. Add a new endpoint instead.
4. **New columns must be nullable or have a sensible default** so existing
   rows + existing queries continue to work.
5. **Follow the strategy factory pattern** for new providers (email,
   AI, social, billing). Look at `AiClientFactory`, `WebhookHandlerFactory`,
   `IInboxFetcher` for reference.
6. **DI registrations are additive** — register new services, never
   replace existing ones unless the replacement is fully behavior-compatible.
7. **Unit + integration tests** required for any change touching more
   than ~5 files.
8. **`.env` files NEVER committed** — `.gitignore` already covers them,
   but double-check before each PR.
9. **No real secrets in code or `appsettings.json`** — only `.env.example`
   placeholders. Real values come from environment variables at runtime.

---

## 5. Running the regression suite

The K1 task delivered an automated regression harness. **One command runs everything**:

```bash
# From the repo root:
dotnet test src/MarketingApp.Tests/MarketingApp.Tests.csproj \
  --filter "FullyQualifiedName~Regression" \
  --logger "console;verbosity=minimal"

# Or, from the frontend folder (npm alias of the above):
cd frontend && npm run regress
```

### What it covers (29 xUnit tests across 3 classes)
- **AuthRegressionTests** — registration, login, JWT issuance, refresh, tampered token rejection, BUG-001 first-user-becomes-admin
- **PaginationRegressionTests** — BUG-003 defensive clamping across 5 paged endpoints (contacts, campaigns, audit-logs, inbox, threads); negative pageNumber / negative pageSize / huge pageSize all handled without 500
- **ContactsRegressionTests** — CRUD cycle, duplicate-email rejection, invalid-email rejection, ILIKE case-insensitive search, phone-substring search

### Local prerequisites
- The harness spins up the real API in-process via `WebApplicationFactory<Program>`
- It needs a Postgres on `localhost:5432` with user `postgres` / password `yourpassword`
  (the dev default in `docker-compose.yml`)
- The factory creates and drops a dedicated `marketingapp_test` database per run —
  your dev `marketingapp` DB is never touched
- Override the connection via the `TEST_POSTGRES_CONNECTION` env var if needed
  (CI does this automatically)

### Adding new regression tests
- Put new test classes under `src/MarketingApp.Tests/Regression/`
- Mark them `[Collection("Regression")]` and accept `RegressionTestFactory` via `IClassFixture<>`
- The collection serializes tests against the shared fixture so they don't trample each other

---

## 6. Deploying to staging

K2 delivered an isolated staging stack that runs side-by-side with prod on
the same VPS. Strict isolation:

- separate folder: `/opt/marketingapp-staging` (prod uses `/opt/marketingapp`)
- separate containers: `marketingapp-staging-*` (prod uses `marketingapp-*`)
- separate Docker network + persistent volumes
- separate DB: `marketingapp_staging` (prod uses `marketingapp_prod`)
- separate env file: `.env.staging` (prod uses `.env`) — secrets MUST differ
- one prod nginx terminates TLS for BOTH; staging vhosts are spliced into
  the shared nginx config so only one process owns 80/443

### Auto-deploy via CI (recommended)

Push to the `staging` branch and GitHub Actions auto-deploys to staging:

```bash
git checkout staging
git merge feature/your-branch     # only after Section 3 regression checklist passes locally
git push origin staging           # CI builds, runs regression, deploys to staging
```

Staging URLs once DNS + cert SANs are in place:
- Frontend: `https://staging.app.samdigital.ae`
- API:      `https://staging.api.samdigital.ae`

### Manual deploy (rarely needed)

```bash
ssh root@195.35.23.193
cd /opt/marketingapp-staging   # cloned by deploy.sh on first run
bash deploy.sh --target staging
```

### Promoting staging → master (production)

After manual QA on staging passes:

```bash
git checkout master
git merge --ff-only staging     # only fast-forward; no surprise commits
git push origin master          # CI deploys to live
```

### One-time staging-stack setup on the VPS (done once, then forgotten)

1. **DNS**: add A records for `staging.app` and `staging.api` pointing to the
   VPS IP (`195.35.23.193`). Same flow as the original prod records.
2. **SSL**: expand the existing prod Let's Encrypt cert to cover the staging
   SANs (one-time):
   ```bash
   certbot --expand -d app.samdigital.ae -d api.samdigital.ae \
                    -d staging.app.samdigital.ae -d staging.api.samdigital.ae \
       --non-interactive --agree-tos --email YOU@samdigital.ae
   docker exec marketingapp-nginx nginx -s reload
   ```
3. **First deploy**: clone + run staging script
   ```bash
   cd /opt
   git clone https://github.com/farooquifaraz/MultiChannelMarketingApp.git marketingapp-staging
   cd marketingapp-staging
   git checkout staging
   bash deploy.sh --target staging
   ```
   The script generates `.env.staging` with fresh random secrets the first
   time and reuses it on every subsequent run.

---

## 7. Deploying to production (master)

1. Approved staging branch is PR'd to `master`.
2. User merges the PR (you are the gatekeeper here).
3. GitHub Actions runs the build job → on success, runs the deploy job.
4. Deploy job SSHes into the VPS (`195.35.23.193`) via `appleboy/ssh-action`,
   pulls latest `master`, runs `bash deploy.sh`.
5. Post-deploy verification (already inside `deploy.sh`): all 5 containers
   healthy, frontend responds at the public URL.
6. If you observe ANY regression in production within the first hour,
   roll back immediately:
   ```bash
   ssh root@195.35.23.193
   cd /opt/marketingapp
   git reset --hard <previous-good-commit>
   bash deploy.sh
   ```

---

## 8. Tracking work — task IDs

Every code change must reference its task ID in commit messages and PR
titles. Example:

```
feat(whatsapp): add image payload support [L1]
fix(billing): correct Stripe webhook signature [L2]
```

`L1`, `L2`, etc. are task IDs from the project's task list (currently
numbered K0, K1, K2, L1, L2, L3, L4 — see the plan file at
`.claude/plans/velvety-wibbling-teapot.md`).

---

## 9. Need help?

- Plan + strategic context: `.claude/plans/velvety-wibbling-teapot.md`
- Deploy mechanics: `DEPLOY.md`
- Architecture overview: this README's "Architecture" section
- Direct question: drop it in your PR description; the maintainer will
  review.
