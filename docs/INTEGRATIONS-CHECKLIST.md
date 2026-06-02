# MarketPro — APIs & Accounts Checklist

Har integration jo app mein hai ya aane wali hai, uski credentials + kahaan se milega + cost.
Status: ✅ ready · 🔧 setup now · 📅 future (roadmap)

Last updated: 2026-06-03

---

## A. Abhi Active / Zaroori (Phase 0–1)

### 1. Email Sending
| Provider | Status | Kya chahiye | Kahaan se | Cost |
|----------|--------|-------------|-----------|------|
| **Brevo** | ✅ Done | API key + verified sender + domain DKIM/DMARC | app.brevo.com | Free 300/day; Lite ~AED 30/mo |
| **Zoho Mail** | ✅ Done (inbound) | Mailbox + **app-specific password** (SMTP/IMAP) | mail.zoho.com → Settings → App Passwords | Existing plan |
| SendGrid | 🔧 Optional backup | API key | sendgrid.com | Free 100/day |
| Mailgun | 🔧 Optional backup | API key + verified domain | mailgun.com | Free trial |

> Email outbound = Brevo (default). Inbound replies = Zoho IMAP. Yeh dono ready hain.

### 2. AI Assistant (inbox replies, Ask AI) — koi 1+ chahiye
| Provider | Status | Kya chahiye | Kahaan se | Cost |
|----------|--------|-------------|-----------|------|
| **Groq** | 🔧 Recommended | API key | console.groq.com | **Free** (fast, generous) |
| OpenAI | 🔧 Optional | API key | platform.openai.com | Pay-as-you-go (~$5 min) |
| Anthropic (Claude) | 🔧 Optional | API key | console.anthropic.com | Pay-as-you-go |
| Google Gemini | 🔧 Optional | API key | aistudio.google.com | Free tier |
| OpenRouter | 🔧 Optional | API key (1 key = many models) | openrouter.ai | Pay-as-you-go |

> App mein **fallback** hai — primary fail ho to doosra use hota hai. Recommend: **Groq (free) + 1 paid backup**.

### 3. WhatsApp (L1 done, credentials pending) 🔧 IMPORTANT
| Item | Kya chahiye | Kahaan se | Cost |
|------|-------------|-----------|------|
| **Meta Business Account** | Facebook Business Manager account | business.facebook.com | Free |
| **WhatsApp Business Account (WABA)** | WABA linked to a phone number | Meta Business → WhatsApp | Free |
| **Phone Number ID** | Dedicated number (naya ya ported, NOT personal WhatsApp) | Meta WhatsApp setup | Free number reg |
| **Permanent Access Token** | System user token (permanent, not 24h) | Meta Business → System Users | Free |
| **Business Verification** | Company docs (trade license, etc.) | Meta verification | Free; unlocks 1,000→100K/day |
| Conversation cost | — | Meta charges per conversation | Marketing ~$0.03–0.08/msg (UAE) |

> **Prepare**: ek dedicated phone number (personal WhatsApp wala nahi), SAM Digital ki **trade license** verification ke liye.

### 4. SMS (optional channel)
| Provider | Status | Kya chahiye | Kahaan se | Cost |
|----------|--------|-------------|-----------|------|
| **Twilio** | 🔧 Optional | Account SID + Auth Token + sender number | twilio.com | Pay-per-SMS (~$0.04 UAE) |

### 5. Infrastructure (already running)
| Item | Status | Kya | Cost |
|------|--------|-----|------|
| **Hostinger VPS** | ✅ Done | 195.35.23.193 — app hosting | Existing |
| **Domain samdigital.ae** | ✅ Done | DNS via Nexus.pk / cPanel | Existing |
| **GitHub** | ✅ Done | Repo + Actions + self-hosted runner (auto-deploy) | Free |
| PostgreSQL + Redis | ✅ Done | Self-hosted in Docker (no external account) | Free |
| Let's Encrypt SSL | ✅ Done | Auto-renew | Free |

---

## B. Phase 2 — Billing & Payments 📅 (jab paying customers aayenge)

| Provider | Kya chahiye | Kahaan se | Notes |
|----------|-------------|-----------|-------|
| **Stripe** | Account + API keys (publishable/secret) + webhook secret | stripe.com | Primary. UAE business supported. Cards + Apple/Google Pay |
| **Telr** or **Tap Payments** | Merchant account | telr.com / tap.company | UAE-local backup (mada card) |
| Razorpay | Account + keys | razorpay.com | Baad mein — India/Pakistan |

> **Prepare**: SAM Digital **trade license + bank account** (Stripe/Telr business onboarding ke liye).

---

## C. Phase 3 — AI Image / Banner Generation 📅

| Provider | Kya chahiye | Kahaan se | Cost |
|----------|-------------|-----------|------|
| **OpenAI (DALL·E 3)** | API key | platform.openai.com | ~$0.04/image |
| Stability AI | API key | platform.stability.ai | Pay-as-you-go |
| Ideogram | API key (text-in-image best) | ideogram.ai | Pay-as-you-go |
| Replicate | API token | replicate.com | Pay-per-run |
| **Cloudflare R2** (storage) | Account + R2 bucket + access keys | cloudflare.com | $0.015/GB (cheap) |

---

## D. Phase 4 — Social Media Posting 📅

| Provider | Kya chahiye | Kahaan se | Cost |
|----------|-------------|-----------|------|
| **Meta Graph API** (Instagram + Facebook) | Meta App + IG Business account + FB Page + OAuth | developers.facebook.com | Free API; app review needed |
| LinkedIn | LinkedIn app + Marketing API access | developer.linkedin.com | Free; approval needed |
| Twitter / X | Developer account + API tier | developer.twitter.com | Paid tiers |
| Google Business Profile | Google Cloud project + API | console.cloud.google.com | Free |

---

## E. Phase 6 — Integrations / Marketplace 📅

| Provider | Kya chahiye | Kahaan se |
|----------|-------------|-----------|
| Zapier | Developer account (public app) | zapier.com |
| HubSpot / Pipedrive / Salesforce | App + OAuth | respective dev portals |

---

## 🎯 Priority Order — Aapki Side Se Prepare

### Abhi (Phase 1 complete karne ke liye)
1. **Meta WhatsApp** — dedicated number + SAM Digital trade license (business verification) ⭐ sabse important
2. **AI key** — Groq (free) banao: console.groq.com → 5 min
3. (Optional) Twilio agar SMS chahiye

### Thoda baad (jab customers/billing)
4. **Stripe** — trade license + bank account ready rakho
5. Telr/Tap — UAE local payments

### Future (Phase 3–4)
6. OpenAI key (images) + Cloudflare account (storage)
7. Meta App (Instagram/Facebook posting)

---

## 📋 Quick "Mujhe Yeh Chahiye" Summary (taaki ek jagah ho)

**Documents/Cheezein jo aapko arrange karni hain:**
- [ ] SAM Digital **trade license** (Meta WhatsApp + Stripe verification)
- [ ] **Dedicated phone number** for WhatsApp Business (personal nahi)
- [ ] **Business bank account** details (Stripe/Telr)
- [ ] Zoho mail **app password** (agar abhi tak nahi banaya)

**Accounts jo banane hain (free, abhi):**
- [ ] Meta Business Manager — business.facebook.com
- [ ] Groq — console.groq.com (AI, free)

**Accounts jo baad mein (paid/customer phase):**
- [ ] Stripe / Telr (payments)
- [ ] OpenAI (images)
- [ ] Cloudflare (storage)
