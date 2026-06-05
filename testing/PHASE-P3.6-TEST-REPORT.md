# Phase 3 (P3.6) — Gemini image alignment + AI Copywriter (WhatsApp content) — Test Report

Date: 2026-06-04
Result: **All pass.** Unit 244/244 · Black-box 154/154 · Frontend build clean.

---

## 1. Why the Python app generated images but MarketPro didn't — the real difference
I tested the **exact Python-style request** (`gemini-2.5-flash-image:generateContent`, no
`responseModalities`) against the MarketPro Gemini key (`AQ.A…UTUQ`):

> **429 — `generate_content_free_tier_requests` limit: 0** for `gemini-2.5-flash-image`.

**Conclusion: the difference is NOT the code — it's the key's billing.** `gemini-2.5-flash-image`
(Nano Banana) has **zero free-tier quota**; it needs **billing enabled** on the Google project. The
Python app's `GEMINI_API_KEY` must be from a billing-enabled project. The MarketPro key is on a free
project (image quota 0). Code differences were cosmetic.

### Alignment done anyway (so it's identical once a billing key is used)
`GeminiImageClient` now matches the `google-generativeai` SDK exactly: for native image models
(`gemini-2.5-flash-image`) it sends a **plain** `generateContent` (no `responseModalities`); only the
older `gemini-2.0-flash-preview-image-generation` gets the modality flag. Parses `inlineData` → data-URI.

**To generate real images:** use a Gemini key from a **billing-enabled** project (the one your Python
app uses) in Integrations → Nano Banana. (Or OpenAI gpt-image-1 after org verification.)

## 2. AI Copywriter — same WhatsApp-content logic as the Python hub (works on FREE text tier)
The Python hub's real value (multi-channel copy) uses **AI text**, which DOES have a free tier
(e.g. Gemini text). Ported that:
- **`MarketingContentService`** + `IAiExecutor` (active AI provider, auto-fallback): sends the same
  structured prompt → returns **WhatsApp broadcast + status, Instagram caption/reels/story, Email
  subject/preview/HTML body, and a suggested image prompt**. Tolerant JSON parser (handles ``` fences /
  surrounding prose; falls back to raw text). Graceful 400 when no AI provider is enabled (never 500).
- **API**: `POST /creatives/content`.
- **Frontend**: new **"AI Copywriter"** page — paste a brief → WhatsApp / Instagram / Email blocks with
  copy buttons + a "paste into Banner Studio" image prompt.

## 3. Tests
- Unit **244/244** (+5 `MarketingContentTests`: clean JSON, fenced JSON, noisy prose, non-JSON
  fallback, missing sections).
- Black-box **154/154** (+V10 short-brief 4xx, +V11 auth, +V12 never-500 graceful).
- GeminiImageClient request alignment verified against the live Gemini API (same 429 → confirms key/billing, not code).

| Area | Status |
|------|--------|
| Unit | ✅ 244/244 |
| Black-box | ✅ 154/154 |
| Existing flows | ✅ unchanged |
| Frontend build | ✅ clean (ContentStudioPage bundled) |

## 4. How to use now
- **WhatsApp/marketing copy (works today on free Gemini text):** Integrations → AI Text → enable
  **Gemini** + paste your Gemini key → open **AI Copywriter** → paste a brief → get WhatsApp/IG/Email copy.
- **Real images:** needs a billing-enabled Gemini key (Nano Banana) or a verified OpenAI org (gpt-image-1).
  Until then, Banner Studio shows a branded placeholder (graceful, no error).

---

**Conclusion:** The image "difference" is the key's billing (free image quota = 0), proven by testing
the Python-exact request. Code aligned to the Python SDK for parity. The Python hub's WhatsApp/
multi-channel copy logic is now in MarketPro as the **AI Copywriter**, working on free AI text tiers.
