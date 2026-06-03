# Black Box Test Report

Run at: 2026-06-03 17:36:59
Stamp: 1780493790215
Base URL: http://localhost:5211

## Summary
- **PASS:** 109
- **FAIL:** 0
- **TOTAL:** 109
- **PASS RATE:** 100%

## Results by Section

### Section A

| ID | Test | Result | Detail |
|----|------|--------|--------|
| A1 | Health endpoint returns 200 Healthy | PASS |  |
| A2 | POST /auth/login no body returns 4xx | PASS |  |
| A3 | POST /auth/login wrong creds returns 401 | PASS |  |
| A4 | POST /auth/register creates admin/user + returns accessToken | PASS |  |
| A5 | POST /auth/register second user gets role=user | PASS |  |
| A6 | BUG-001: a role=admin user exists in the system | PASS |  |
| A7 | POST /auth/register duplicate email rejected | PASS |  |
| A8 | POST /auth/login correct creds returns accessToken | PASS |  |
| A9 | Protected endpoint no token returns 401 | PASS |  |
| A10 | Protected endpoint bogus token returns 401 | PASS |  |
| A11 | POST /auth/refresh with bogus refresh token returns 4xx | PASS |  |

### Section B

| ID | Test | Result | Detail |
|----|------|--------|--------|
| B1 | GET /me/profile (admin) returns email | PASS |  |
| B2 | GET /me/profile (user) returns role=user | PASS |  |
| B3 | PUT /me/profile updates full name | PASS |  |
| B4 | POST /me/password wrong current rejected | PASS |  |
| B5 | POST /me/password too short rejected | PASS |  |
| B6 | GET /me/signature returns merged signature DTO | PASS |  |
| B7 | PUT /me/signature updates designation | PASS |  |

### Section C

| ID | Test | Result | Detail |
|----|------|--------|--------|
| C1 | GET /me/active-sender exists (NOT 404) | PASS |  |
| C2 | GET /me/active-sender has all expected fields | PASS |  |
| C3 | GET /me/active-sender requires auth | PASS |  |

### Section D

| ID | Test | Result | Detail |
|----|------|--------|--------|
| D1 | GET /admin/smtp-groups requires admin | PASS |  |
| D2 | GET /admin/smtp-groups (admin) returns list | PASS |  |
| D3 | POST /admin/smtp-groups create Brevo group with API key | PASS |  |
| D4 | GET created group exposes brevoApiKeyMasked (M1) | PASS |  |
| D5 | Mask format starts with xkey (M1 transparency) | PASS |  |
| D6 | Raw brevoApiKey NEVER returned (security) | PASS |  |
| D7 | PUT with blank brevoApiKey preserves existing (M1 contract) | PASS |  |
| D8 | POST create SMTP group exposes smtpPasswordSet=true (M1 boolean) | PASS |  |
| D9 | Raw smtpPassword NEVER returned (security) | PASS |  |
| D10 | GET /admin/smtp-groups/users-assignments returns user list | PASS |  |
| D11 | POST /admin/smtp-groups/:id/clone clones a group | PASS |  |

### Section E

| ID | Test | Result | Detail |
|----|------|--------|--------|
| E1 | GET /contacts requires auth | PASS |  |
| E2 | GET /contacts returns list (200) | PASS |  |
| E3 | POST /contacts creates new | PASS |  |
| E4 | GET /contacts?search=BB returns it (fullName ILIKE) | PASS |  |
| E5 | BUG-003: GET /contacts?pageNumber=-5 clamps to 1 (no 500) | PASS |  |
| E6 | BUG-003: GET /contacts?pageNumber=0 clamps | PASS |  |
| E7 | GET /contacts?pageSize=99999 clamps to max | PASS |  |
| E8 | GET /contacts?pageSize=-100 clamps | PASS |  |
| E9 | PUT /contacts/:id update | PASS |  |
| E10 | POST /contacts/groups creates group | PASS |  |
| E11 | GET /contacts/groups list | PASS |  |
| E12 | POST /contacts/assign-group bulk assignment | PASS |  |
| E13 | GET /contacts/export.csv returns CSV | PASS |  |
| E14 | DELETE /contacts/:id removes | PASS |  |

### Section F

| ID | Test | Result | Detail |
|----|------|--------|--------|
| F1 | GET /templates list | PASS |  |
| F2 | POST /templates create email template | PASS |  |
| F3 | PUT /templates/:id update | PASS |  |
| F4 | DELETE /templates/:id removes | PASS |  |

### Section G

| ID | Test | Result | Detail |
|----|------|--------|--------|
| G1 | GET /campaigns list | PASS |  |
| G2 | BUG-003: GET /campaigns?pageNumber=-1 clamps | PASS |  |
| G3 | GET /campaigns/<random-guid> returns 404 | PASS |  |
| G4 | GET /campaigns/<invalid-guid> returns 4xx | PASS |  |

### Section H

| ID | Test | Result | Detail |
|----|------|--------|--------|
| H1 | GET /settings/smtp user-level returns 200/204 | PASS |  |
| H2 | GET /admin/system-settings (admin) returns settings | PASS |  |
| H3 | GET /admin/system-settings allowed for user (intentional â€” read-only platform settings) | PASS |  |
| H4 | PUT /admin/system-settings rejected for user (write requires admin) | PASS |  |

### Section I

| ID | Test | Result | Detail |
|----|------|--------|--------|
| I1 | GET /inbox returns list | PASS |  |
| I2 | BUG-003: GET /inbox?pageNumber=-3 clamps | PASS |  |
| I3 | GET /inbox/threads list | PASS |  |
| I4 | BUG-003: GET /inbox/threads?pageNumber=-2 clamps | PASS |  |
| I5 | GET /inbox/unread-count returns integer | PASS |  |
| I6 | GET /inbox/threads/<random> returns 404 | PASS |  |

### Section J

| ID | Test | Result | Detail |
|----|------|--------|--------|
| J1 | GET /admin/audit-logs (admin) returns list | PASS |  |
| J2 | BUG-003: GET /admin/audit-logs?pageNumber=-7 clamps | PASS |  |
| J3 | GET /admin/audit-logs rejected for user | PASS |  |

### Section K

| ID | Test | Result | Detail |
|----|------|--------|--------|
| K1 | GET /admin/users (admin) returns list | PASS |  |
| K2 | GET /admin/users rejected for user | PASS |  |
| K3 | Verify at least one admin exists (BUG-001) | PASS |  |

### Section L

| ID | Test | Result | Detail |
|----|------|--------|--------|
| L1 | GET /notifications returns list | PASS |  |
| L2 | GET /notifications/unread-count returns int | PASS |  |

### Section M

| ID | Test | Result | Detail |
|----|------|--------|--------|
| M1 | GET /dashboard returns stats | PASS |  |

### Section N

| ID | Test | Result | Detail |
|----|------|--------|--------|
| N1 | GET /track/open/<random>.gif returns 200 (always) | PASS |  |
| N2 | GET /track/click/<random>?u=URL redirects to URL | PASS |  |
| N3 | GET /track/click without u= returns 400 (anti-open-redirect) | PASS |  |
| N4 | GET /track/click with javascript: URL rejected (anti-open-redirect) | PASS |  |

### Section O

| ID | Test | Result | Detail |
|----|------|--------|--------|
| O1 | POST /webhooks/brevo without auth/signature accepts or 401 | PASS |  |
| O2 | POST /webhooks/sendgrid | PASS |  |
| O3 | POST /webhooks/mailgun | PASS |  |

### Section P

| ID | Test | Result | Detail |
|----|------|--------|--------|
| P1 | GET / returns SPA index | PASS |  |
| P2 | GET /login (SPA fallback) returns index | PASS |  |
| P3 | GET /dashboard (SPA fallback) | PASS |  |

### Section Q

| ID | Test | Result | Detail |
|----|------|--------|--------|
| Q1 | Invalid JSON body returns 4xx not 5xx | PASS |  |
| Q2 | Very long input gracefully rejected (no 500) | PASS |  |
| Q3 | GET /api/v1/nonexistent returns 404 not 500 | PASS |  |
| Q4 | POST with method that should be GET returns 405 | PASS |  |
| Q5 | SQL-injection-like input in search safely handled | PASS |  |
| Q6 | XSS-like input in template body safely stored | PASS |  |
| Q7 | Unicode input gracefully accepted or rejected (no 500) | PASS |  |
| Q8 | Email field missing @ rejected | PASS |  |
| Q9 | Email too long rejected | PASS |  |

### Section R

| ID | Test | Result | Detail |
|----|------|--------|--------|
| R1 | Admin sees Brevo group as default if marked | PASS |  |
| R2 | /me/active-sender now reflects Brevo group + provider=brevo | PASS |  |
| R3 | Provider banner gracefully handles no groups | PASS |  |

### Section S

| ID | Test | Result | Detail |
|----|------|--------|--------|
| S1 | GET verify with correct token echoes hub.challenge | PASS |  |
| S2 | GET verify with WRONG token returns 401 | PASS |  |
| S3 | GET verify without challenge returns 401 | PASS |  |
| S4 | POST inbound (no matching group) returns 200 ingested:0 | PASS |  |
| S5 | POST inbound with malformed JSON still returns 200 (no crash) | PASS |  |
| S6 | POST status-only callback (no messages) returns 200 | PASS |  |

### Section T

| ID | Test | Result | Detail |
|----|------|--------|--------|
| T1 | GET /billing/plans returns the 5 seeded tiers | PASS |  |
| T2 | GET /billing/plans requires auth | PASS |  |
| T3 | GET /billing/subscription auto-provisions Free + returns usage | PASS |  |
| T4 | Subscription usage metrics have used + limit + remaining | PASS |  |
| T5 | POST /billing/subscription/change to pro switches plan | PASS |  |
| T6 | POST change to invalid plan returns 4xx | PASS |  |
| T7 | Change back to free (cleanup) | PASS |  |

### Section Z

| ID | Test | Result | Detail |
|----|------|--------|--------|
| Z1 | DELETE the test Brevo group | PASS |  |

