# Phase 3 (P3.5) — Per-Provider Credential Vault + Simple Integrations — Test Report

Date: 2026-06-04
Feature: Save every provider's API key independently + a simple "active" selector (like normal apps).
Result: **All pass.** Unit 239/239 · Black-box 151/151 · Frontend build clean.

---

## 1. Problem solved
Before: AI/image/payment each had a **single shared key field** spread across **two screens** (old
Settings "AI" tab + Integrations). Switching provider lost the previous provider's key (this caused
the OpenAI↔Gemini key confusion). Confusing and un-professional.

After: **one simple Integrations screen**. Each provider keeps **its own saved key**; you just pick
which one is **enabled** (a radio, like switching accounts). Zero regression — the active provider's
credential is synced into the existing `system_settings` fields, so all current AI/image/payment code
keeps reading exactly as before.

## 2. What shipped

- **`IntegrationCredential`** entity + `integration_credentials` table (category, provider, api_key,
  model, base_url, secondary_secret; unique on category+provider). Migration **backfills** the vault
  from whatever keys are already in `system_settings` (idempotent) — nothing lost.
- **`IntegrationCredentialService`**: list per category (keys masked + active flag), save per-provider
  (blank key = keep existing), set-active (syncs the chosen credential into `system_settings` so
  AiExecutor / ImageGenerationService / CheckoutService are unchanged).
- **API** (admin): `GET /admin/integrations/{category}`, `PUT /admin/integrations/{category}/{provider}`,
  `POST /admin/integrations/{category}/{provider}/activate`.
- **Frontend**: redesigned **Integrations** page — for AI / Image / Payments, a list of providers each
  with a **radio (enable)** + its **own key field** ("key saved ••••1234" indicator) + model; one Save
  per provider. Free/paid badges, helpful notes, Stripe webhook-secret field.

## 3. Unit tests (`IntegrationCredentialTests` — 11 new, suite 239/239)

| Group | Cases | Expected |
|-------|-------|----------|
| Mask | 4 | hides secret, shows last 4 |
| Save per-provider | openai + gemini | independent rows, no cross loss |
| Save blank key | re-save | keeps key, updates model |
| Set active syncs | openai active | system_settings.AiApiKey/Provider/Model updated |
| Switch active | openai→gemini | live key swaps; vault keeps both |
| Get masks + active flag | image/dalle | isActive true, key masked |
| Unknown category | "bogus" | AppValidationException |
| Activate keyless | →mock | clears active secret, sets provider |

## 4. Black-box (`blackbox-suite.ps1` — Section AA, 151/151)

| # | Case | Expected |
|---|------|----------|
| AA1 | GET /admin/integrations/ai | category + activeProvider + credentials |
| AA2 | GET as user | 403 |
| AA3 | PUT save openai key | saved, masked on read |
| AA4 | save gemini too | both keys retained independently |
| AA5 | activate openai | activeProvider=openai, isActive flag |
| AA6 | system-settings reflects active | aiProvider=openai (existing path) |
| AA7 | activate disabled (cleanup) | ok |

Full suite **151/151** — every prior section green ⇒ zero regression. Migration verified: vault
backfilled from existing settings.

## 5. Safety / regression
- Active provider's credential is mirrored into `system_settings`; **no existing consumer changed**
  (AiExecutor, AiReplyService, AdminAiController, ImageGenerationService, CheckoutService all still
  read `system_settings`). The AI reply/chat hot path is untouched.
- Keys masked on read; raw keys only used server-side. Blank key on save = keep existing.
- Migration additive + idempotent (CREATE TABLE/INDEX IF NOT EXISTS, INSERT … ON CONFLICT DO NOTHING).

| Area | Status |
|------|--------|
| Unit | ✅ 239/239 |
| Black-box | ✅ 151/151 |
| Existing AI / image / payment flows | ✅ unchanged |
| Frontend build | ✅ clean |

---

**Conclusion:** AI (and image/payment) keys are now managed in one simple screen — save each
provider's key once, flip a radio to choose which is enabled. Provider switching never loses a key,
and the change is provably zero-regression because the active credential flows into the same
`system_settings` fields the app already uses.
