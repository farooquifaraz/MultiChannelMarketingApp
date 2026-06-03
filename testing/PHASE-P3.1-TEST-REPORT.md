# Phase 3 (P3.1) — AI Banner / Flyer Generation (Foundation) — Test Report

Date: 2026-06-03
Feature: Image-generation provider abstraction + factory + GeneratedAsset + Banner Studio UI.
Result: **All pass.** Unit 164/164 · Black-box 125/125 · Frontend build clean · Migration + schema verified.

> **Scope note — keyless-ready foundation.** Built the same proven way as the AI-client and WhatsApp
> phases: full strategy/factory abstraction that works **immediately with zero keys** via a `mock`
> provider (offline SVG placeholder), with the real **DALL·E** provider activating the moment an API
> key is configured in admin Image settings. Credit metering is recorded on each asset but **not
> enforced** yet. Fully additive ⇒ zero regression.

---

## 1. What shipped

- **`IImageGenerationClient`** strategy contract (mirrors `IAiClient`) + **`ImageGenerationClientFactory`**
  (resolves by provider key; new providers = 1 class + 1 DI line).
- **Providers:**
  - `MockImageGenerationClient` (`mock`) — deterministic SVG placeholder (gradient + wrapped, XML-escaped
    prompt + dimensions) returned as a base64 **data-URI** → renders in-browser, no key, no storage.
  - `DalleImageClient` (`dalle`) — OpenAI Images / DALL·E 3 call; returns the hosted URL (or b64 data-URI).
    Pure `ParseImageUrl` response parser. Falls back gracefully (clear error) when no key.
- **`GeneratedAsset`** entity + `generated_assets` table (user_id, prompt, provider, size, w/h, status,
  image_url `text`, credit_cost, error_message, created_at; indexed by `(user_id, created_at)`).
- **`ImageGenerationService`** — validates prompt (required, ≤1000 chars), parses size against an allowed
  set (unknown → 1024×1024), resolves the configured provider (default `mock`, falls back to mock if
  misconfigured), persists the asset (completed / failed), lists a user's recent assets, exposes 5 size
  presets (square / portrait / landscape / 1080 / wide).
- **`SystemSettings`**: `image_provider` (default `mock`), `image_api_key`, `image_base_url`,
  `image_model` (default `dall-e-3`) — surfaced through entity → DTO (key masked) → service → raw-key
  accessor `GetRawImageApiKeyAsync`, settable via admin update.
- **API** (`[Authorize]`): `GET /creatives/sizes`, `POST /creatives/generate`, `GET /creatives/assets`.
- **Frontend**: **Banner Studio** page (sidebar) — prompt + size picker + Generate, live gallery of
  generated banners with download links. Available to all users.

## 2. Unit tests (`ImageGenerationTests` — 23 new, total suite 164/164)

| Group | Cases | Expected |
|-------|-------|----------|
| ParseSize valid ×4 | known tokens (+ whitespace) | exact w×h |
| ParseSize fallback ×4 | null/""/unknown/garbage | 1024×1024 |
| Placeholder SVG | well-formed, dims, prompt | `<svg>…</svg>`, contains size + prompt |
| Placeholder SVG escaping | `<b> & "x"` | no raw `<b>`, `&amp;` present |
| Mock client output | generate | `data:image/svg+xml;base64,` round-trips to `<svg` |
| Factory resolve | mock/MOCK/dalle/unknown/"" | case-insensitive hit, null on miss |
| DALL·E ParseImageUrl ×5 | url / b64 / `{}` / `[]` / non-json | url, data-uri, or null |
| Service generate (mock) | valid prompt | status=completed, dims, data-uri, AddAsync once |
| Service blank/overlong/disabled | ""/"  "/1001 chars/disabled | AppValidationException |
| SizeOptions | — | 5 presets |

## 3. Black-box / API tests (`testing/blackbox-suite.ps1` — Section V, all PASS)

| # | Case | Expected |
|---|------|----------|
| V1 | GET /creatives/sizes | ≥5 size options |
| V2 | GET /creatives/sizes no auth | 401 |
| V3 | POST /creatives/generate (mock, 1200x628) | completed, dims 1200×628, data-URI image |
| V4 | POST generate blank prompt | 4xx |
| V5 | POST generate unknown size | falls back to 1024×1024 |
| V6 | GET /creatives/assets | ≥1 asset after generating |
| V7 | POST generate no auth | 401 |

Full suite: **125/125 pass** — every prior section (auth, contacts, campaigns, inbox, admin, tracking,
webhooks, WhatsApp, billing, organizations, hardening) still green ⇒ **zero regression**.

## 4. Validation / edge cases

- Prompt required + ≤1000 chars (enforced in service AND DB column length).
- Unknown / malformed size token → safe 1024×1024 default (never throws on size).
- Provider `disabled` → clear validation error; misconfigured provider → silent fallback to `mock` so
  the studio always produces output.
- Real-provider failures (missing key, non-2xx, no url) → asset saved as `failed` with an error message,
  never a 500.
- API key never leaves the server unmasked (DTO masks `image_api_key`; raw key fetched only server-side).

## 5. Stress / safety considerations

- **Zero-regression by design:** entirely new tables/columns/endpoints; no existing path touched. Default
  `mock` provider means no external calls and no key dependency.
- Migration additive + idempotent (`CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`) — safe to
  re-run every deploy. Verified: table created, 4 image columns present.
- `image_url` is `text` (unbounded) to hold large mock data-URIs without truncation.
- Asset list is capped (default 50, max 200) and indexed by `(user_id, created_at)`.

## 6. Regression checklist

| Area | Status |
|------|--------|
| Unit suite | ✅ 164/164 |
| Black-box suite | ✅ 125/125 |
| Existing flows (send / campaign / inbox / admin / billing / orgs) | ✅ unaffected (all-new surface) |
| DB migration | ✅ additive + idempotent; table + 4 image cols verified |
| Frontend build / typecheck | ✅ clean (BannerStudioPage bundled) |

## 7. Next slices (Phase 3 continuation)

- **Real providers + keys:** flip `image_provider=dalle` + key in admin (then Stability / Ideogram /
  Replicate as extra `IImageGenerationClient`s — 1 class + 1 DI line each).
- **Cloud storage (Cloudflare R2):** persist real-provider images (whose URLs expire) + serve stable URLs.
- **Brand Kits + light editor** (logo/colors auto-apply; Polotno/fabric.js), **flyer-from-URL**, and a
  **credit system** (enforce per-plan generation limits, mirroring the quota gate).

---

**Conclusion:** The image-generation engine is in — abstraction, factory, mock + DALL·E providers,
persistence, API, and a working Banner Studio UI — producing real downloadable banners today with zero
keys, and provably zero-regression. Real models, storage, brand kits, and credit billing build on top.
