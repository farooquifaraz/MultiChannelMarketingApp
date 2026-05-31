# Deployment Guide — Hostinger VPS

End-to-end instructions for running MarketPro on a Hostinger VPS via Docker, with HTTPS, automated tracking, and (optionally) multiple apps on the same VPS.

---

## Prerequisites

- A Hostinger VPS running **Ubuntu 22.04** (KVM 2 or higher recommended: 2 vCPU / 8 GB RAM).
- A domain you control (e.g. `samdigital.ae`) with the ability to add DNS A records.
- SSH access to the VPS as `root` or a sudo user.

---

## 1. One-time VPS setup

SSH into the VPS, then:

```bash
# Update & install Docker + Docker Compose plugin + certbot for Let's Encrypt.
apt update && apt upgrade -y
apt install -y ca-certificates curl gnupg lsb-release ufw git certbot

curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" \
    | tee /etc/apt/sources.list.d/docker.list > /dev/null
apt update && apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Lock down the firewall — only SSH + HTTP + HTTPS exposed.
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable
```

---

## 2. DNS

In your domain registrar, add two A records pointing to the VPS public IP:

| Type | Host | Points to |
|------|------|-----------|
| A    | `app` | `<VPS_IP>` |
| A    | `api` | `<VPS_IP>` |

(Use `dig app.yourdomain.com` to confirm DNS propagates — usually < 5 min on Hostinger.)

---

## 3. Clone & configure

```bash
mkdir -p /opt && cd /opt
git clone https://github.com/farooquifaraz/MultiChannelMarketingApp.git marketingapp
cd marketingapp

# Create the production env file from the template and fill in real values.
cp .env.production.example .env
nano .env   # set POSTGRES_PASSWORD, JWT_SECRET_KEY, PUBLIC_API_URL, PUBLIC_APP_URL, ALLOWED_ORIGINS

# Edit the nginx server-block to use your real subdomains.
sed -i 's/app.yourdomain.com/app.samdigital.ae/g; s/api.yourdomain.com/api.samdigital.ae/g' nginx/conf.d/marketingapp.conf
```

---

## 4. SSL certificates (Let's Encrypt — one-time)

The nginx config in this repo expects certs at `/etc/letsencrypt/live/<domain>/...`. Run certbot in **standalone** mode the first time — make sure nothing else is using port 80:

```bash
certbot certonly --standalone -d app.samdigital.ae -d api.samdigital.ae \
    --email you@samdigital.ae --agree-tos --no-eff-email
```

Auto-renewal is already wired into the system via `/etc/cron.d/certbot` on Ubuntu — no extra step.

---

## 5. First deploy

```bash
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml ps         # all 5 services should be "healthy"
docker compose -f docker-compose.prod.yml logs -f api  # tail API logs for the first migration
```

Visit `https://app.samdigital.ae` — you should see the login screen.

---

## 6. Common ops

```bash
# Pull latest code + redeploy (this is what the CI/CD pipeline runs).
cd /opt/marketingapp
git pull
docker compose -f docker-compose.prod.yml up -d --build

# View logs
docker compose -f docker-compose.prod.yml logs -f api
docker compose -f docker-compose.prod.yml logs -f nginx

# DB backup
docker exec marketingapp-postgres pg_dump -U $POSTGRES_USER $POSTGRES_DB | gzip > backup_$(date +%F).sql.gz

# DB restore
gunzip -c backup_2026-05-31.sql.gz | docker exec -i marketingapp-postgres psql -U $POSTGRES_USER -d $POSTGRES_DB

# Reload nginx after editing conf.d/
docker exec marketingapp-nginx nginx -s reload
```

---

## 7. Running a SECOND app on the same VPS

Yes — this is the standard "multi-tenant VPS" pattern. Two options:

### Option A: A second copy of THIS app (separate DB, separate domain)
```bash
cd /opt
git clone https://github.com/.../MultiChannelMarketingApp.git marketingapp2
cd marketingapp2
cp .env.production.example .env
nano .env   # use a DIFFERENT POSTGRES_PASSWORD + JWT_SECRET_KEY + DIFFERENT subdomains
# Use different container names by setting a project name:
docker compose -p marketingapp2 -f docker-compose.prod.yml up -d --build
```
Add two new A records (`app2`, `api2`), update `nginx/conf.d/marketingapp2.conf` for the new subdomains, reload nginx.

### Option B: A completely different app
Same idea — each app gets its own folder under `/opt`, its own `docker-compose` project name (`-p`), and its own `nginx/conf.d/<app>.conf` file. The shared nginx routes based on `Host` header, so subdomains just work.

**Resource note**: each Postgres container uses ~150 MB RAM at idle. On a 2-app KVM 2 (8 GB RAM) VPS you have plenty of headroom. Watch `docker stats` if you scale to 4+ apps.

---

## 8. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| 502 Bad Gateway | API container isn't healthy yet | `docker compose logs api` |
| Tracking pixel shows 0 opens | `PUBLIC_API_URL` not set to public https URL | check `.env`, rebuild API container |
| Login fails with "JWT secret missing" | Forgot to set `JWT_SECRET_KEY` in `.env` | set + restart api container |
| Cert error | DNS hadn't propagated when you ran certbot | wait, re-run certbot |
