# Phase L3 — WhatsApp Inbox (Inbound) — Test Report

Date: 2026-06-03
Feature: Inbound WhatsApp messages (Meta Cloud API webhook) → unified inbox.
Result: **All tests pass.** Unit 117/117 · Black-box 102/102 · Frontend build clean · Migration verified.

> Note: real end-to-end inbound delivery requires Meta WhatsApp Cloud credentials + a verified
> webhook subscription (arriving with the keys). Everything below is verifiable without creds via
> the pure parser, the webhook verify/ingest endpoints, and the DB migration. The send/ingest plumbing
> activates the moment a SmtpGroup has a `WhatsAppPhoneNumberId` configured.

---

## 1. Unit tests — inbound parser (black-box of pure function)

File: `src/MarketingApp.Tests/Services/WhatsAppInboundParseTests.cs` — all PASS.

| # | Case | Expected |
|---|------|----------|
| U1 | Real text webhook | from, id, text, phone_number_id, profile name, timestamp all extracted |
| U2 | type=image | body = "[image]" |
| U3 | type=document | body = "[document]" |
| U4 | type=video | body = "[video]" |
| U5 | type=audio | body = "[voice message]" |
| U6 | type=location | body = "[location]" |
| U7 | Multiple messages in one webhook | both parsed, correct ids |
| U8 | Status-only callback (delivered/read, no messages[]) | empty list |
| U9–U14 | Malformed: "", whitespace, "not json", "{}", empty entry/changes | empty list, never throws |
| U15 | Missing profile name | FromName null, still parses |

Also re-verified (still green): L1 media payload (9), L2 template payload + Meta parse (8),
bounce classification (13), Brevo webhook correlation (10), contacts/validators, etc.

## 2. Black-box / API tests (live HTTP, `testing/blackbox-suite.ps1`)

New **Section S — L3 WhatsApp Webhook** (all PASS):

| # | Case | Expected |
|---|------|----------|
| S1 | GET verify, correct `hub.verify_token` | 200, echoes `hub.challenge` (Meta handshake) |
| S2 | GET verify, wrong token | 401 |
| S3 | GET verify, missing challenge | 401 |
| S4 | POST inbound, no matching group | 200, `{ingested:0}` (routes by phone_number_id) |
| S5 | POST inbound, malformed JSON | no 5xx (200) — never crashes the webhook |
| S6 | POST status-only callback | 200, nothing ingested |

Full suite: **102/102 pass** — every prior section (auth, contacts, campaigns, inbox, admin,
tracking, hardening, M1/M2) still green ⇒ **zero regression**.

## 3. Validation / edge cases covered

- Dedup: a repeated Meta message id (`wamid…`) is ignored (unique partial index
  `ix_inbox_whatsapp_dedup` on `(smtp_group_id, message_id) WHERE channel='whatsapp'`).
- Email vs WhatsApp dedup isolation: email keeps its `(group, folder, uid)` unique index, now
  filtered `WHERE channel='email'` so WhatsApp rows (uid=0) never collide with it.
- Unknown receiving number (no SmtpGroup for that `phone_number_id`) → message dropped + logged,
  no crash.
- Non-text messages, missing profile name, status-only callbacks all handled.
- Contact auto-match by digits-only phone compare (WhatsApp number or phone).
- Threading: repeated messages from the same sender reuse the prior thread id.

## 4. Stress / resilience considerations

- Webhook POST is wrapped so any ingest exception still returns **200** — Meta will not
  retry-storm or auto-disable the subscription on transient errors.
- Each message in a batch is ingested independently; one bad message doesn't abort the batch.
- Parser is allocation-light and tolerant of partial payloads (no exceptions on malformed input,
  proven by U9–U14).
- AI draft generation is **enqueued** (Hangfire), not run inline — webhook returns fast regardless
  of AI latency, matching the existing IMAP-poll path.

## 5. Regression checklist (zero-regression mandate)

| Area | Status |
|------|--------|
| Email inbound (IMAP) dedup index | ✅ preserved (now filtered to channel='email') |
| Inbox list / threads / unread-count APIs | ✅ pass (I1–I6) |
| Existing email/campaign/contact/admin flows | ✅ pass (full black-box) |
| DB migration additive + idempotent | ✅ channel column default 'email'; indexes IF NOT EXISTS |
| Unit suite | ✅ 117/117 |

## 6. What needs Meta credentials to fully exercise (post-keys)

- Configure webhook in Meta: callback URL `https://api.samdigital.ae/api/v1/webhooks/whatsapp`,
  verify token = `WhatsApp:WebhookVerifyToken` (env/config; default `marketpro-whatsapp-verify`).
- Set `WhatsAppPhoneNumberId` on the SmtpGroup so inbound routes to its owner.
- Then: send a real WhatsApp message to the business number → appears in the app inbox with a
  "WA" badge, AI draft generated, realtime push — same UX as email replies.

---

**Conclusion:** L3 inbound foundation is complete, fully tested at the layers that don't need
credentials, deployed, and zero-regression. It activates end-to-end as soon as WhatsApp Cloud
credentials + webhook subscription are in place.
