"""Generates docs/MarketPro-Integrations.xlsx — a formatted checklist of every API/account."""
from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

wb = Workbook()

# ---- shared styles ----
HEADER_FILL = PatternFill("solid", fgColor="4F46E5")
HEADER_FONT = Font(bold=True, color="FFFFFF", size=11)
TITLE_FONT = Font(bold=True, size=14, color="1F2937")
SECTION_FILL = PatternFill("solid", fgColor="E0E7FF")
SECTION_FONT = Font(bold=True, size=11, color="3730A3")
WRAP = Alignment(vertical="top", wrap_text=True)
CENTER = Alignment(horizontal="center", vertical="center")
thin = Side(style="thin", color="D1D5DB")
BORDER = Border(left=thin, right=thin, top=thin, bottom=thin)

STATUS_FILL = {
    "Done":      PatternFill("solid", fgColor="D1FAE5"),
    "Setup now": PatternFill("solid", fgColor="FEF3C7"),
    "Future":    PatternFill("solid", fgColor="F3F4F6"),
}

# =========================================================================
# Sheet 1 — Integrations
# =========================================================================
ws = wb.active
ws.title = "Integrations"

headers = ["#", "Category", "Provider", "Status", "What you need",
           "Where to get it", "Cost", "Priority", "Configured in DB?"]
widths  = [4, 16, 22, 11, 40, 26, 24, 10, 16]

# rows: (category, provider, status, need, where, cost, priority, configured)
rows = [
    # ---- Email ----
    ("Email", "Brevo", "Done", "API key + verified sender + domain DKIM/DMARC", "app.brevo.com", "Free 300/day; Lite ~AED 30/mo", "P1", "YES (brevo_api_key)"),
    ("Email", "Zoho Mail", "Done", "Mailbox + app-specific password (SMTP/IMAP, inbound)", "mail.zoho.com > App Passwords", "Existing plan", "P1", "YES (smtp/imap)"),
    ("Email", "SendGrid", "Setup now", "API key (optional backup)", "sendgrid.com", "Free 100/day", "P3", "Verify"),
    ("Email", "Mailgun", "Setup now", "API key + verified domain (optional backup)", "mailgun.com", "Free trial", "P3", "Verify"),
    # ---- AI ----
    ("AI", "Groq", "Setup now", "API key (recommended - free & fast)", "console.groq.com", "Free", "P1", "Verify"),
    ("AI", "OpenAI", "Setup now", "API key (optional)", "platform.openai.com", "Pay-as-you-go (~$5 min)", "P2", "Verify"),
    ("AI", "Anthropic (Claude)", "Setup now", "API key (optional)", "console.anthropic.com", "Pay-as-you-go", "P2", "Verify"),
    ("AI", "Google Gemini", "Setup now", "API key (optional)", "aistudio.google.com", "Free tier", "P2", "Verify"),
    ("AI", "OpenRouter", "Setup now", "API key (1 key, many models)", "openrouter.ai", "Pay-as-you-go", "P2", "Verify"),
    # ---- WhatsApp ----
    ("WhatsApp", "Meta Business Account", "Setup now", "Facebook Business Manager account", "business.facebook.com", "Free", "P1", "NO"),
    ("WhatsApp", "WhatsApp Business Acct (WABA)", "Setup now", "WABA linked to a dedicated phone number", "Meta Business > WhatsApp", "Free", "P1", "NO"),
    ("WhatsApp", "Phone Number ID", "Setup now", "Dedicated number (NOT personal WhatsApp)", "Meta WhatsApp setup", "Free reg", "P1", "NO (whatsapp_phone_number_id)"),
    ("WhatsApp", "Permanent Access Token", "Setup now", "System user token (permanent, not 24h)", "Meta Business > System Users", "Free", "P1", "NO (whatsapp_api_key)"),
    ("WhatsApp", "Business Verification", "Setup now", "SAM Digital trade license", "Meta verification", "Free; unlocks 1K->100K/day", "P1", "NO"),
    # ---- SMS ----
    ("SMS", "Twilio", "Setup now", "Account SID + Auth Token + sender number", "twilio.com", "~$0.04/SMS (UAE)", "P3", "Verify"),
    # ---- Infra ----
    ("Infrastructure", "Hostinger VPS", "Done", "195.35.23.193 - app hosting", "hostinger / Nexus", "Existing", "P1", "N/A"),
    ("Infrastructure", "Domain samdigital.ae", "Done", "DNS via Nexus.pk / cPanel", "Nexus.pk", "Existing", "P1", "N/A"),
    ("Infrastructure", "GitHub", "Done", "Repo + Actions + self-hosted runner (auto-deploy)", "github.com", "Free", "P1", "N/A"),
    ("Infrastructure", "PostgreSQL + Redis", "Done", "Self-hosted in Docker (no external acct)", "Self-hosted", "Free", "P1", "N/A"),
    ("Infrastructure", "Let's Encrypt SSL", "Done", "Auto-renew certificate", "Automatic", "Free", "P1", "N/A"),
    # ---- Billing (Phase 2) ----
    ("Billing", "Stripe", "Future", "Account + API keys + webhook secret", "stripe.com", "~2.9% + fee", "P2", "NO"),
    ("Billing", "Telr / Tap Payments", "Future", "UAE merchant account (mada card)", "telr.com / tap.company", "Per-txn", "P2", "NO"),
    ("Billing", "Razorpay", "Future", "Account + keys (India/Pakistan)", "razorpay.com", "Per-txn", "P3", "NO"),
    # ---- Image gen (Phase 3) ----
    ("Image Gen", "OpenAI DALL-E 3", "Future", "API key", "platform.openai.com", "~$0.04/image", "P3", "NO"),
    ("Image Gen", "Stability AI", "Future", "API key", "platform.stability.ai", "Pay-as-you-go", "P3", "NO"),
    ("Image Gen", "Ideogram", "Future", "API key (text-in-image)", "ideogram.ai", "Pay-as-you-go", "P3", "NO"),
    ("Image Gen", "Replicate", "Future", "API token", "replicate.com", "Pay-per-run", "P3", "NO"),
    ("Storage", "Cloudflare R2", "Future", "Account + R2 bucket + access keys", "cloudflare.com", "$0.015/GB", "P3", "NO"),
    # ---- Social (Phase 4) ----
    ("Social", "Meta Graph (IG + FB)", "Future", "Meta App + IG Business + FB Page + OAuth", "developers.facebook.com", "Free (app review)", "P3", "NO"),
    ("Social", "LinkedIn", "Future", "App + Marketing API access", "developer.linkedin.com", "Free (approval)", "P4", "NO"),
    ("Social", "Twitter / X", "Future", "Developer account + API tier", "developer.twitter.com", "Paid tiers", "P4", "NO"),
    ("Social", "Google Business Profile", "Future", "Google Cloud project + API", "console.cloud.google.com", "Free", "P4", "NO"),
    # ---- Integrations (Phase 6) ----
    ("Integrations", "Zapier", "Future", "Developer account (public app)", "zapier.com", "Free/paid", "P4", "NO"),
    ("Integrations", "HubSpot / CRM", "Future", "App + OAuth", "dev portals", "Varies", "P4", "NO"),
]

# Title row
ws.merge_cells("A1:I1")
ws["A1"] = "MarketPro - APIs & Accounts Checklist (updated 2026-06-03)"
ws["A1"].font = TITLE_FONT
ws.row_dimensions[1].height = 24

# Header row (row 3)
hr = 3
for c, h in enumerate(headers, start=1):
    cell = ws.cell(row=hr, column=c, value=h)
    cell.fill = HEADER_FILL; cell.font = HEADER_FONT; cell.alignment = CENTER; cell.border = BORDER
for c, w in enumerate(widths, start=1):
    ws.column_dimensions[get_column_letter(c)].width = w

# Data rows grouped by category with section separators
r = hr + 1
n = 0
last_cat = None
for (cat, prov, status, need, where, cost, prio, conf) in rows:
    n += 1
    vals = [n, cat, prov, status, need, where, cost, prio, conf]
    for c, v in enumerate(vals, start=1):
        cell = ws.cell(row=r, column=c, value=v)
        cell.alignment = WRAP; cell.border = BORDER
        if c == 4 and status in STATUS_FILL:
            cell.fill = STATUS_FILL[status]; cell.alignment = CENTER
    r += 1

ws.freeze_panes = "A4"
ws.auto_filter.ref = f"A{hr}:I{r-1}"

# =========================================================================
# Sheet 2 — Your Prep Checklist
# =========================================================================
ws2 = wb.create_sheet("Prep Checklist")
ws2.merge_cells("A1:C1")
ws2["A1"] = "What YOU need to arrange (your side)"
ws2["A1"].font = TITLE_FONT
ws2.row_dimensions[1].height = 24

prep_headers = ["Done?", "Item", "Needed for"]
for c, h in enumerate(prep_headers, start=1):
    cell = ws2.cell(row=3, column=c, value=h)
    cell.fill = HEADER_FILL; cell.font = HEADER_FONT; cell.alignment = CENTER; cell.border = BORDER
ws2.column_dimensions["A"].width = 8
ws2.column_dimensions["B"].width = 46
ws2.column_dimensions["C"].width = 40

# Each entry is (checkbox, item, needed_for). A section header has checkbox == None.
prep = [
    (None, "Documents / Things to arrange", ""),
    ("[ ]", "SAM Digital trade license", "Meta WhatsApp verification + Stripe"),
    ("[ ]", "Dedicated phone number (NOT personal WhatsApp)", "WhatsApp Business Account"),
    ("[ ]", "Business bank account details", "Stripe / Telr onboarding (later)"),
    ("[ ]", "Zoho mail app-specific password", "Inbound reply polling (if not set)"),
    (None, "Free accounts to create NOW", ""),
    ("[ ]", "Meta Business Manager", "WhatsApp (business.facebook.com)"),
    ("[ ]", "Groq API key", "AI - free, 5 min (console.groq.com)"),
    (None, "Paid accounts - LATER (customer/billing phase)", ""),
    ("[ ]", "Stripe / Telr", "Payments"),
    ("[ ]", "OpenAI", "AI image generation"),
    ("[ ]", "Cloudflare", "Media/asset storage"),
]
rr = 4
for chk, item, need in prep:
    if chk is None:  # section header
        ws2.merge_cells(start_row=rr, start_column=1, end_row=rr, end_column=3)
        cell = ws2.cell(row=rr, column=1, value=item)
        cell.fill = SECTION_FILL; cell.font = SECTION_FONT
    else:
        ws2.cell(row=rr, column=1, value=chk).alignment = CENTER
        ws2.cell(row=rr, column=2, value=item).alignment = WRAP
        ws2.cell(row=rr, column=3, value=need).alignment = WRAP
    rr += 1

wb.save(r"D:\MultiChannelMarkettingApp\docs\MarketPro-Integrations.xlsx")
print("OK saved docs/MarketPro-Integrations.xlsx")
