-- =====================================================================
-- Which provider credentials are CONFIGURED in the live DB?
-- Safe to run: shows only YES/SET or a masked ...last4 — never the secret.
-- Run in DBeaver against the production database.
-- =====================================================================

-- 1) SMTP Groups (admin-level, shared) — email + WhatsApp + IMAP creds
SELECT
  name AS group_name,
  email_provider,
  is_default,
  CASE WHEN COALESCE(brevo_api_key,'')     <> '' THEN '...' || RIGHT(brevo_api_key,4)     ELSE 'NOT SET' END AS brevo_key,
  CASE WHEN COALESCE(send_grid_api_key,'') <> '' THEN '...' || RIGHT(send_grid_api_key,4) ELSE 'NOT SET' END AS sendgrid_key,
  CASE WHEN COALESCE(mailgun_api_key,'')   <> '' THEN '...' || RIGHT(mailgun_api_key,4)   ELSE 'NOT SET' END AS mailgun_key,
  CASE WHEN COALESCE(smtp_password,'')     <> '' THEN 'SET' ELSE 'NOT SET' END            AS smtp_password,
  CASE WHEN COALESCE(whatsapp_api_key,'')  <> '' THEN '...' || RIGHT(whatsapp_api_key,4)  ELSE 'NOT SET' END AS whatsapp_token,
  CASE WHEN COALESCE(whatsapp_phone_number_id,'') <> '' THEN whatsapp_phone_number_id      ELSE 'NOT SET' END AS whatsapp_phone_id,
  CASE WHEN COALESCE(imap_password,'')     <> '' THEN 'SET' ELSE 'NOT SET' END            AS imap_password,
  CASE WHEN COALESCE(brevo_webhook_secret,'') <> '' THEN 'SET' ELSE 'NOT SET' END         AS brevo_webhook
FROM smtp_groups
ORDER BY is_default DESC, name;

-- 2) Per-user SMTP settings (personal overrides)
SELECT
  u.email AS user_email,
  CASE WHEN COALESCE(s.brevo_api_key,'')    <> '' THEN '...' || RIGHT(s.brevo_api_key,4)    ELSE 'NOT SET' END AS brevo_key,
  CASE WHEN COALESCE(s.smtp_password,'')    <> '' THEN 'SET' ELSE 'NOT SET' END             AS smtp_password,
  CASE WHEN COALESCE(s.whatsapp_api_key,'') <> '' THEN '...' || RIGHT(s.whatsapp_api_key,4) ELSE 'NOT SET' END AS whatsapp_token
FROM user_smtp_settings s
JOIN users u ON u.id = s.user_id
ORDER BY u.email;

-- 3) AI provider (system_settings)
SELECT
  ai_provider,
  CASE WHEN COALESCE(ai_api_key,'') <> '' THEN '...' || RIGHT(ai_api_key,4) ELSE 'NOT SET' END AS ai_key,
  ai_model
FROM system_settings;
