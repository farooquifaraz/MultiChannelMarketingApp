# Brevo Status-Sync + Delivery — Safe Production Checklist

Do problems, dono **production config** (code change nahi):
- **A.** App status Brevo se sync nahi (sent par atak jaata hai)
- **B.** Kuch recipients (e.g. students@izylrn.com) ko email nahi milta

> **Safe approach principle:** har step ke baad **verify** karo, ek baar me ek hi cheez badlo, aur live sending ko kabhi disturb mat karo. Koi bhi step sending ko rok nahi sakta — sab additive hain.

---

## PART A — Status sync (webhook) theek karna

**Root cause:** Brevo delivery/open/click events app tak pohonchte hi nahi (ya correlate nahi hote). Local par to webhook aa hi nahi sakta — ye **sirf production** (`api.samdigital.ae`) par hota hai.

### A0. Pehle confirm karo (no change)
- [ ] Production app live hai: browser me `https://api.samdigital.ae/health` → `200` aana chahiye.
- [ ] Webhook endpoint zinda hai: kisi bhi tool se `GET https://api.samdigital.ae/api/v1/webhooks/brevo` → 404/405 aaye to bhi theek (matlab route exist karta hai; POST-only hai).

### A1. (Safe, optional but recommended) Webhook secret set karo
Abhi secret blank hai → webhook bina verify accept hota hai. Security ke liye token lagao:
- [ ] Admin → SMTP Groups → **Ahsan SAM Digital Outreach** → Webhook section → `BrevoWebhookSecret` = ek strong random string (e.g. `sam_wh_9f3k...`). Save.
- [ ] Yahi **Project Team SAM Digital** group ke liye bhi (agar wo bhi send karta hai).
- Rollback: secret hatana ho to field khaali kar do — webhook phir bhi chalega.

### A2. Brevo dashboard me webhook URL configure karo
Brevo → **Transactional → Settings → Webhook → Add a new webhook**:
- [ ] URL (Ahsan group ke liye):
  ```
  https://api.samdigital.ae/api/v1/webhooks/brevo?smtpGroupId=a77a6836-8082-4a66-a1ad-941c88360508&token=<jo-secret-A1-me-daala>
  ```
- [ ] Events tick karo: **Delivered, Opened, Click, Hard bounce, Soft bounce** (Unsubscribe/Spam optional).
- [ ] Save.
- [ ] (Agar projects@ se bhi bhejte ho) ek aur webhook, `smtpGroupId=bb8499d8-a244-4490-879f-50f65026fe13` ke saath.

> token=... sirf tab daalo jab A1 me secret set kiya ho. Secret na ho to token chhod do.

### A3. Test (ek email se, safe)
- [ ] App se **apne hi Gmail** par 1 quick email bhejo (Gmail jaldi delivered+opened deta hai).
- [ ] 1–2 min baad campaign report kholo → status **Delivered → Opened** hona chahiye (sirf "sent" nahi).
- [ ] Brevo → **Logs** me bhi wahi event dikhega — dono match hone chahiye.

### A4. Agar phir bhi sync na ho — diagnose (read-only)
- [ ] Brevo → Webhook → us webhook ka **"Logs / recent calls"** dekho: response **200** aa raha hai? Agar 401 → token mismatch (A1/A2 dobara check). Agar timeout/5xx → server/URL issue.
- [ ] Server logs me dekho: `[brevo] Applied X/Y webhook events` (aa raha = correlate ho raha). `could not be correlated` warning aaye to recipient contact app me hona chahiye (email-fallback usi se match karta hai).

---

## PART B — Delivery (students@izylrn.com jaise recipients ko mail nahi milta)

**Root cause (app ka nahi):** Brevo ne email **bhej diya** ("Sent") par recipient server ne **accept/confirm nahi kiya** ("Delivered" missing). Gmail ko mila, izylrn (strict O365-type) ne silently drop kiya — ye **domain authentication / recipient-side** ka issue hai.

### B1. Domain authentication verify karo (sabse important)
Brevo → **Senders, Domains & Dedicated IPs → Domains → `samdigital.ae`**:
- [ ] **SPF** — green/authenticated
- [ ] **DKIM** — green/authenticated
- [ ] **DMARC** — green/authenticated
- Agar koi bhi red/pending hai → Brevo jo DNS records deta hai wo aapke domain DNS (jahan samdigital.ae manage hota hai) me add karo, phir Brevo me "Verify" dabao.
- Safe: ye records **sirf authentication badhate hain**, existing sending nahi todte.

### B2. Brevo logs se exact reason nikaalo (read-only)
Brevo → **Logs / Statistics** → search `students@izylrn.com`:
- [ ] Status dekho: `deferred` / `soft bounce` / `blocked` / `hard bounce` — har ek ka matlab:
  - **deferred / soft bounce** → recipient server temporarily reject (greylisting/reputation) — kuch der baad retry.
  - **blocked / hard bounce** → permanently reject (address galat, ya domain block) — reason message padho.
  - **Sirf "sent", aage kuch nahi** → recipient server ne chup-chaap drop kiya (aksar SPF/DKIM/DMARC kamzor hone par).

### B3. Recipient-side checks
- [ ] izylrn ke O365/admin se **Quarantine** check karwao — mail wahan ho sakta hai (user inbox/spam dono me nahi dikhta).
- [ ] izylrn mailbox ki health: screenshot me bahut "Mail Delivery System" failures the → us mailbox/domain ki apni delivery problems ho sakti hain.

### B4. Deliverability behtar karne ke long-term steps (safe, gradual)
- [ ] samdigital.ae ka **DMARC policy** `p=none` se shuru karo (monitoring), report dekh kar baad me `quarantine` par badhao.
- [ ] Naye domain ko **warm-up** karo — shuru me kam volume, dheere badhao (Brevo reputation banegi).
- [ ] **Dedicated IP** (Brevo) tabhi lo jab volume zyada ho; warna shared IP theek hai.

---

## Verify-done summary
| # | Done when |
|---|-----------|
| A | Gmail test par app me **Delivered/Opened** dikhe + Brevo logs me webhook **200** |
| B | `samdigital.ae` ke SPF+DKIM+DMARC **green**, aur Brevo logs me izylrn ka exact reason pata chale |

**Important:** Inme se koi step **code deploy nahi maangta** — sab Brevo dashboard + DNS + admin panel config hai. Isliye live app ko zero risk.
