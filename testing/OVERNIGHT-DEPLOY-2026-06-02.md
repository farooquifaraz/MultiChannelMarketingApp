# Overnight Deploy + Test Plan — 2026-06-02

## TL;DR (jab aap sone se uthen, ek action lena hai)

**1 SSH command** chalao, sab deploy ho jayega. Phir test cases run karein.

```bash
ssh root@195.35.23.193 "cd /opt/marketingapp && git fetch origin master && git reset --hard origin/master && docker compose -f docker-compose.prod.yml up -d --build --remove-orphans && docker image prune -af --filter 'until=168h' && for i in 1 2 3 4 5; do docker exec marketingapp-api curl -fsS http://localhost:8080/health && echo '✅ healthy' && break || (echo \"waiting...\" && sleep 5); done"
```

Ek single line se: pull → rebuild → restart → health check. ~3 minute total.

---

## ✅ Raat Mein Kya-Kya Hua

### Code Changes (master + staging dono pe push ho gaya hai)

1. **fix(bounce)** — Zoho 5.4.6 "Unusual sending activity" ko ab **SOFT bounce** maana jata hai
   - 13 unit tests covering exact prod error pattern
   - Future mein Zoho throttle se contacts permanently flag NAHI honge
   - **File**: `src/MarketingApp.Application/Jobs/CampaignJobService.cs`

2. **feat(M1)** — Admin → SMTP Groups → Edit pe API key indicator
   - Saved key pe green pill: "✅ Brevo API key configured: xkey…9gU1eB"
   - Empty pe amber warning: "⚠ Brevo API key not set"
   - **Files**:
     - `frontend/src/pages/admin/SmtpGroupsPage.tsx`
     - `src/MarketingApp.Application/DTOs/SmtpGroupDtos.cs` (SmtpPasswordSet boolean)

3. **feat(M2)** — Pre-send provider banner
   - **Send Message** page pe full banner: "📤 Will send via: Brevo (BREVO) · Brevo Production"
   - **Campaign Detail** page pe compact pill (jab retry button hai)
   - 3 visual states:
     - 🟢 Green: assigned + creds OK
     - 🟡 Amber: falling back to default
     - 🔴 Red: no group OR credentials missing
   - **New file**: `frontend/src/components/messages/ActiveSenderBanner.tsx`
   - **New endpoint**: `GET /api/v1/me/active-sender`

### Build Status
- ✅ Backend: zero errors, zero warnings (mine)
- ✅ Frontend: zero TS errors, builds clean (256 kB bundle)
- ✅ Unit tests: 68/68 passing (including 13 new bounce classification tests + 1 fix)
- ✅ GitHub Actions CI: passed on master

### Git State
```
master   → 5a34d77  merge(prod): M1+M2 + bounce-fix → live
staging  → f2d6c37  merge(M1+M2): provider clarity → staging
master == staging (in sync, ready)
```

### What Did NOT Auto-Deploy + Why
GitHub Actions VPS_HOST secret nahi configured hai, so CI ran but **SSH deploy step skipped**. CI logs:
> `! VPS_HOST secret not configured — skipping deploy.`

**Live VPS abhi bhi purana code chala raha hai.** Niche wala 1 SSH command chalao → live update ho jayega.

---

## 🚀 Deploy Live (1 Command)

### Option A: One-Liner (Recommended)
```bash
ssh root@195.35.23.193 "cd /opt/marketingapp && git fetch origin master && git reset --hard origin/master && docker compose -f docker-compose.prod.yml up -d --build --remove-orphans && docker image prune -af --filter 'until=168h' && for i in 1 2 3 4 5; do docker exec marketingapp-api curl -fsS http://localhost:8080/health && echo '✅ healthy' && break || (echo waiting && sleep 5); done"
```

### Option B: Step-by-Step (agar koi step fail ho)
```bash
# 1. SSH karo
ssh root@195.35.23.193

# 2. App folder
cd /opt/marketingapp

# 3. Latest master pull
git fetch origin master
git reset --hard origin/master

# 4. Rebuild + restart containers (incremental, ~2-3 min)
docker compose -f docker-compose.prod.yml up -d --build --remove-orphans

# 5. Container status check
docker compose -f docker-compose.prod.yml ps

# 6. Verify API healthy
docker exec marketingapp-api curl -fsS http://localhost:8080/health
# Expected output: {"status":"ok",...}

# 7. Optional: prune old images (disk cleanup)
docker image prune -af --filter "until=168h"
```

### Option C: Set Up Auto-Deploy For Future (5 min one-time)
Taaki next time auto-deploy ho jaye:

1. GitHub repo → **Settings → Secrets and variables → Actions**
2. **New repository secret** chaar baar:
   - `VPS_HOST` = `195.35.23.193`
   - `VPS_USER` = `root`
   - `VPS_SSH_KEY` = aapki SSH private key ka full content (e.g. `~/.ssh/id_ed25519` ka content)
   - `VPS_DEPLOY_PATH` = `/opt/marketingapp`
3. Done — agla `git push master` auto-deploy karega

---

## 🧪 Live Test Plan — 31 Test Cases

Deploy ke baad yeh test cases run karo. Total ~30 minutes.

### 🔵 Section A: Sanity Smoke (5 min) — yeh sab pass hone chahiye

| # | Test Case | Steps | Pass Criteria |
|---|-----------|-------|---------------|
| A1 | Home loads | Open https://app.samdigital.ae | Login page renders, no console errors |
| A2 | Login works | Login as `farooqui.faraz@gmail.com` (or your admin) | Redirects to Dashboard, JWT set |
| A3 | Dashboard loads | After login | Stats cards visible, no 500 errors |
| A4 | API health | https://api.samdigital.ae/health | Returns `{"status":"ok",...}` |
| A5 | Logout works | Top-right → Logout | Returns to login, token cleared |

### 🟢 Section B: M1 — SmtpGroup Credential Indicator (5 min)

| # | Test Case | Steps | Pass Criteria |
|---|-----------|-------|---------------|
| B1 | Edit Brevo group shows green pill | Admin → SMTP Groups → Brevo Production → **Edit** | Below "Brevo API Key" field: green pill "✅ Brevo API key configured: xkey…XXXX — leave blank to keep" |
| B2 | Old Zoho group shows green SMTP password pill | Admin → SMTP Groups → Ahsan SAM Digital Outreach → **Edit** | Below "Password" field: green pill "✅ SMTP password configured" |
| B3 | New group shows no pill | Click **+ New Group**, fill name, pick Brevo | No pill below API key field (only placeholder) |
| B4 | Cancel preserves state | Edit group, change name, **Cancel** | Returns to list, no save happened |
| B5 | Save without changing API key keeps it | Edit Brevo, leave API key blank, change Description, **Update Group** | Group updates, **Test Send still works** (key preserved) |

### 🟢 Section C: M2 — Send Message Provider Banner (10 min)

| # | Test Case | Steps | Pass Criteria |
|---|-----------|-------|---------------|
| C1 | Banner visible on Email channel | Sidebar → **Send Message** → channel = Email | Green/amber banner above compose: "📤 Will send via: Brevo Production (BREVO)" with From email |
| C2 | Banner hides on WhatsApp | Switch channel to **WhatsApp** | No banner (channel != email) |
| C3 | Banner hides on SMS | Switch channel to **SMS** | No banner |
| C4 | Refresh button works | Click 🔄 icon on banner | Spinner appears, then banner re-renders |
| C5 | Provider name correct | Default group is Brevo | Banner says **BREVO** (not SMTP/Zoho) |
| C6 | From address correct | Same | Banner shows `Ahsan Yaqoob <ahsan@samdigital.ae>` |
| C7 | Default badge visible | Banner | "DEFAULT" pill visible next to group name |
| C8 | Tone: assigned user (Faraz) | Faraz has Brevo assigned? Check | If assigned: green; if not: amber "Falling back to platform default" |
| C9 | Send actually uses Brevo | Send test email to your inbox | Email arrives; check Gmail "Show Original" → `Received: ... brevo.com` |
| C10 | Brevo Statistics confirms | Brevo dashboard → Statistics → Transactional | Counter incremented (1+ sent) within 30 sec |

### 🟢 Section D: M2 — Campaign Retry Banner (5 min)

| # | Test Case | Steps | Pass Criteria |
|---|-----------|-------|---------------|
| D1 | Compact banner on retry-able campaign | Campaigns → open the 43-bounced one → top right | Compact pill: "Sending via Brevo · Brevo Production" |
| D2 | Banner hidden on no-failures campaign | Open a campaign with `failedCount=0` | No banner shown (retry button isn't either) |
| D3 | Retry 43 bounced contacts | Click "Retry 43 failed" → confirm | Toast: "Retrying 43 failed messages…" |
| D4 | Watch progress | Stay on page, refresh | Status changes, eventually 43 delivered (Brevo) |
| D5 | Retry 2nd campaign | Same for the 5-bounced campaign | 5 delivered |

### 🟢 Section E: Bounce Classification Fix (5 min — passive monitor)

| # | Test Case | Steps | Pass Criteria |
|---|-----------|-------|---------------|
| E1 | Send to non-existent address | Compose to `nonexistent-XYZ123@samdigital.ae` | Should HARD-bounce; contact's IsBounced=true |
| E2 | No false positives | Re-send to a valid contact (5.4.6 unlikely on Brevo) | Email delivers; IsBounced stays false |
| E3 | Log inspection | `docker logs marketingapp-api 2>&1 \| grep -i bounce \| tail -20` | Any 5.4.6 logs should say "SOFT bounce" not "HARD" |

### 🟡 Section F: Zero-Regression (10 min) — old features still work

| # | Test Case | Steps | Pass Criteria |
|---|-----------|-------|---------------|
| F1 | Login + JWT | Already done in A2 | ✓ |
| F2 | Contacts CRUD | Contacts → search "test", paginate, edit one | All work, no 500 |
| F3 | Templates load | Templates page | List renders |
| F4 | Inbox loads | Inbox sidebar | Threads list visible |
| F5 | AI Reply Draft | Open a thread → "Generate reply" | Draft appears |
| F6 | AI Ask AI chat | Same thread → Ask AI tab → ask question | Answer streams |
| F7 | Suggested questions | New thread | 3 chips appear |
| F8 | Scheduled campaigns | Scheduled page | List renders |
| F9 | SmtpGroup clone | Admin → clone Brevo group | New group created, opens in edit |
| F10 | Audit logs | Admin → Audit Logs | Recent actions logged |
| F11 | Notification center | Top-right bell icon | Dropdown opens |
| F12 | Admin → Users | Admin → Users | All users listed |
| F13 | Settings page | Settings (top-right) | All tabs load |

### 🔴 Section G: Critical Failure Points (must be zero)

| # | Test Case | Steps | Pass Criteria |
|---|-----------|-------|---------------|
| G1 | No console errors on Send Message | Open browser DevTools Console while on Send Message page | Zero red errors |
| G2 | No console errors on SmtpGroups Edit | Same on SmtpGroups → Edit | Zero red errors |
| G3 | API /me/active-sender returns 200 | Network tab → load Send Message | Request to `/api/v1/me/active-sender` returns 200 with provider data |
| G4 | Old /me/signature still works | Network tab → same | `/api/v1/me/signature` still 200 |
| G5 | DB schema unchanged | `docker exec marketingapp-postgres psql -U marketing -d marketingapp -c '\d smtp_groups'` | No new columns added; existing schema intact |

---

## 🚨 Rollback Plan (agar koi issue ho)

```bash
ssh root@195.35.23.193
cd /opt/marketingapp
git reset --hard HEAD~1   # last commit revert
docker compose -f docker-compose.prod.yml up -d --build
```

Pichla commit `e535ef1` (docs k0). ~3 min mein pichli state restore.

---

## 📋 Outstanding Items (next session)

1. **48 bounced contacts retry** — Brevo se retry karna (D3 + D5 mein test ho jayega)
2. **GitHub VPS secrets configure** — future auto-deploys ke liye (Option C above)
3. **K1 regression harness merge** — feature/k1-regression-harness branch staging mein nahi gaya, separate task
4. **L1: WhatsApp media messages** — Phase 1 start

---

## 🧠 Quick Reference

| What | Where |
|------|-------|
| Live URL | https://app.samdigital.ae |
| API URL | https://api.samdigital.ae |
| VPS | 195.35.23.193 (root@) |
| App path on VPS | /opt/marketingapp |
| Compose file | docker-compose.prod.yml |
| Master HEAD | 5a34d77 |
| Brevo dashboard | https://app.brevo.com |
| Brevo Statistics | https://app.brevo.com/statistics |

---

**Bottom line**: 1 SSH command → ~3 min deploy → 30 min testing → sab live aur verified. Subah uthkar ek-ek test case tick karte jao, sab green hona chahiye.

🛡️ Local-first rule honored: code locally tested, all 68 unit tests pass, builds clean. Live just needs the SSH command above.
