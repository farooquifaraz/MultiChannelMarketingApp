#!/usr/bin/env bash
# =========================================================================
# MarketPro — one-shot production deploy script for Hostinger VPS
#
# Usage (on the VPS, as root):
#   curl -fsSL https://raw.githubusercontent.com/farooquifaraz/MultiChannelMarketingApp/master/deploy.sh -o deploy.sh
#   bash deploy.sh
#
# Idempotent — safe to re-run. Preserves existing .env passwords and SSL certs
# unless they're missing. Fails fast with clear errors if something is off.
# =========================================================================

set -euo pipefail

# =========================================================================
# Target selection — live (default) or staging
# =========================================================================
#
#   ./deploy.sh                    # equivalent to --target live
#   ./deploy.sh --target live      # deploys to app.samdigital.ae       /opt/marketingapp
#   ./deploy.sh --target staging   # deploys to staging.app.samdigital.ae /opt/marketingapp-staging
#
# The two stacks run side-by-side on the same VPS with strict isolation:
#   - different APP_DIR, container names, docker network, persistent volumes
#   - different DB name (marketingapp_prod vs marketingapp_staging)
#   - different env file (.env vs .env.staging) — secrets MUST differ
#   - public prod nginx terminates TLS for BOTH and proxies to the right
#     backend by hostname; only one process owns ports 80/443.
#
# This makes the "promote feature/* → staging branch → master" workflow safe:
# the staging deploy runs on its own DB and doesn't touch prod state.

TARGET="live"
ARGS=()
while [ $# -gt 0 ]; do
    case "$1" in
        --target)
            TARGET="${2:-live}"
            shift 2
            ;;
        --target=*)
            TARGET="${1#--target=}"
            shift
            ;;
        *)
            ARGS+=("$1")
            shift
            ;;
    esac
done

case "$TARGET" in
    live|production|prod)
        TARGET="live"
        REPO_URL="https://github.com/farooquifaraz/MultiChannelMarketingApp.git"
        APP_DIR="/opt/marketingapp"
        DOMAIN_APP="app.samdigital.ae"
        DOMAIN_API="api.samdigital.ae"
        COMPOSE_FILE="docker-compose.prod.yml"
        ENV_FILE=".env"
        ENV_EXAMPLE=".env.production.example"
        BACKUP_PREFIX="/root/.marketingapp-secrets"
        ;;
    staging|stage)
        TARGET="staging"
        REPO_URL="https://github.com/farooquifaraz/MultiChannelMarketingApp.git"
        APP_DIR="/opt/marketingapp-staging"
        DOMAIN_APP="staging.app.samdigital.ae"
        DOMAIN_API="staging.api.samdigital.ae"
        COMPOSE_FILE="docker-compose.staging.yml"
        ENV_FILE=".env.staging"
        ENV_EXAMPLE=".env.production.example"
        BACKUP_PREFIX="/root/.marketingapp-staging-secrets"
        ;;
    *)
        echo "ERROR: --target must be 'live' or 'staging' (got: '$TARGET')" >&2
        exit 1
        ;;
esac

EXPECTED_VPS_IP="195.35.23.193"

# --- Colors (only if output is a terminal) ---
if [ -t 1 ]; then
    GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; BLUE='\033[0;34m'; NC='\033[0m'
else
    GREEN=''; YELLOW=''; RED=''; BLUE=''; NC=''
fi

log()   { echo -e "${BLUE}▸${NC} $*"; }
ok()    { echo -e "${GREEN}✓${NC} $*"; }
warn()  { echo -e "${YELLOW}⚠${NC} $*"; }
fail()  { echo -e "${RED}✗${NC} $*" >&2; exit 1; }
step()  { echo; echo -e "${BLUE}═══ $* ═══${NC}"; }

# =========================================================================
step "Step 0 — Pre-flight checks"
# =========================================================================

[ "$(id -u)" -eq 0 ] || fail "Run as root: sudo bash deploy.sh"
command -v docker >/dev/null || fail "Docker not installed. Re-run Phase 2 first."
command -v docker >/dev/null && docker compose version >/dev/null 2>&1 || fail "Docker Compose plugin missing."
command -v certbot >/dev/null || fail "certbot not installed. Re-run Phase 2."
command -v git >/dev/null || fail "git not installed."

ok "Tooling OK (docker, compose, certbot, git)"

# DNS sanity check — make sure the two subdomains resolve to THIS VPS.
log "Verifying DNS for $DOMAIN_APP and $DOMAIN_API..."
APP_IP=$(getent hosts "$DOMAIN_APP" | awk '{print $1}' | head -1 || true)
API_IP=$(getent hosts "$DOMAIN_API" | awk '{print $1}' | head -1 || true)
[ "$APP_IP" = "$EXPECTED_VPS_IP" ] || fail "DNS issue: $DOMAIN_APP resolves to '$APP_IP' (expected $EXPECTED_VPS_IP). Wait for DNS propagation."
[ "$API_IP" = "$EXPECTED_VPS_IP" ] || fail "DNS issue: $DOMAIN_API resolves to '$API_IP' (expected $EXPECTED_VPS_IP). Wait for DNS propagation."
ok "DNS resolves correctly for both subdomains"

# Free disk + memory
DISK_FREE=$(df -BG / | awk 'NR==2 {gsub("G",""); print $4}')
[ "$DISK_FREE" -gt 5 ] || fail "Less than 5 GB free disk. Free up space first."
ok "Disk: ${DISK_FREE} GB free"

# =========================================================================
step "Step 1 — Email for Let's Encrypt"
# =========================================================================

if [ -z "${LETSENCRYPT_EMAIL:-}" ]; then
    echo "Yeh email Let's Encrypt SSL renewal notifications ke liye use hoga."
    echo "(Cert expiry pe email aayegi — koi spam nahi.)"
    read -rp "Aapka email: " LETSENCRYPT_EMAIL
fi
[[ "$LETSENCRYPT_EMAIL" =~ ^[^@]+@[^@]+\.[^@]+$ ]] || fail "Invalid email: '$LETSENCRYPT_EMAIL'"
ok "Email: $LETSENCRYPT_EMAIL"

# =========================================================================
step "Step 2 — Clone / update the repo"
# =========================================================================

if [ -d "$APP_DIR/.git" ]; then
    log "Repo exists — pulling latest from master..."
    git -C "$APP_DIR" fetch origin master
    git -C "$APP_DIR" reset --hard origin/master
    ok "Repo updated to $(git -C "$APP_DIR" rev-parse --short HEAD)"
else
    log "Cloning repo into $APP_DIR..."
    mkdir -p "$(dirname "$APP_DIR")"
    git clone "$REPO_URL" "$APP_DIR"
    ok "Repo cloned at $(git -C "$APP_DIR" rev-parse --short HEAD)"
fi
cd "$APP_DIR"

# =========================================================================
step "Step 3 — Generate / preserve $ENV_FILE"
# =========================================================================

if [ -f $ENV_FILE ]; then
    warn "$ENV_FILE already exists — keeping existing passwords + secrets"
    ok "$ENV_FILE preserved"
else
    log "Generating strong random secrets..."
    POSTGRES_PWD=$(openssl rand -base64 32 | tr -d '/+=' | head -c 32)
    JWT_SECRET=$(openssl rand -base64 48 | tr -d '/+=' | head -c 64)

    # Save backup outside the repo (so a `git reset` doesn't wipe it).
    BACKUP_FILE="$BACKUP_PREFIX-$(date +%Y%m%d).txt"
    cat > "$BACKUP_FILE" <<EOF
# MarketingApp production secrets backup — generated $(date)
# KEEP THIS FILE SAFE. These cannot be regenerated without breaking the DB.
POSTGRES_PASSWORD=$POSTGRES_PWD
JWT_SECRET_KEY=$JWT_SECRET
EOF
    chmod 600 "$BACKUP_FILE"
    ok "Secrets backup saved to $BACKUP_FILE (root-readable only)"

    # Pick a DB name that makes the target obvious in psql `\l` output.
    if [ "$TARGET" = "staging" ]; then
        DB_NAME="marketingapp_staging"
    else
        DB_NAME="marketingapp_prod"
    fi

    cat > $ENV_FILE <<EOF
# === Auto-generated by deploy.sh on $(date) — target: $TARGET ===
POSTGRES_DB=$DB_NAME
POSTGRES_USER=marketingapp
POSTGRES_PASSWORD=$POSTGRES_PWD

JWT_SECRET_KEY=$JWT_SECRET
JWT_ISSUER=MarketingApp
JWT_AUDIENCE=MarketingAppUsers
JWT_EXPIRY_MINUTES=60
JWT_REFRESH_DAYS=30

PUBLIC_API_URL=https://$DOMAIN_API
PUBLIC_APP_URL=https://$DOMAIN_APP
ALLOWED_ORIGINS=https://$DOMAIN_APP
EOF
    chmod 600 $ENV_FILE
    ok "$ENV_FILE created with strong passwords"
fi

# =========================================================================
step "Step 4 — Configure nginx for $DOMAIN_APP / $DOMAIN_API"
# =========================================================================

# Replace the placeholder domain in the nginx config. Idempotent — sed runs on
# every deploy, but is harmless if already replaced (no match → no change).
# The staging vhost file uses different placeholders (staging.app.yourdomain.com etc.).
if [ "$TARGET" = "live" ]; then
    sed -i "s/app\.yourdomain\.com/$DOMAIN_APP/g; s/api\.yourdomain\.com/$DOMAIN_API/g; s/yourdomain\.com/samdigital.ae/g" \
        nginx/conf.d/marketingapp.conf
    ok "prod nginx config updated (server_name = $DOMAIN_APP / $DOMAIN_API)"
else
    sed -i "s/staging\.app\.yourdomain\.com/$DOMAIN_APP/g; s/staging\.api\.yourdomain\.com/$DOMAIN_API/g; s/yourdomain\.com/samdigital.ae/g" \
        nginx/conf.d/marketingapp-staging.conf
    ok "staging nginx vhost config updated"
fi

# =========================================================================
step "Step 5 — Let's Encrypt SSL certificates"
# =========================================================================

# Staging shares the SAME prod nginx process + Let's Encrypt account. Cert
# issuance + renewal are PROD-owned to avoid two certbot instances fighting
# over port 80. The staging cert is obtained as an additional SAN added to
# the prod cert (use `certbot --expand -d ...staging.app... -d ...staging.api...`
# when ready; documented in DEPLOY.md). Skip the cert block on staging deploys.
if [ "$TARGET" = "staging" ]; then
    log "Skipping cert issuance — staging uses the prod-owned Let's Encrypt cert"
    ok "(run 'certbot --expand -d staging.app.samdigital.ae -d staging.api.samdigital.ae' once on the VPS to add staging SANs)"
fi

if [ "$TARGET" = "live" ]; then
    CERT_PATH="/etc/letsencrypt/live/$DOMAIN_APP/fullchain.pem"
    if [ -f "$CERT_PATH" ]; then
        EXPIRY=$(openssl x509 -enddate -noout -in "$CERT_PATH" | cut -d= -f2)
        EXPIRY_TS=$(date -d "$EXPIRY" +%s)
        NOW_TS=$(date +%s)
        DAYS_LEFT=$(( (EXPIRY_TS - NOW_TS) / 86400 ))
        if [ "$DAYS_LEFT" -gt 30 ]; then
            ok "Certificate exists, valid for $DAYS_LEFT more days — skipping renewal"
        else
            warn "Certificate expires in $DAYS_LEFT days — renewing..."
            certbot renew --quiet
            ok "Certificate renewed"
        fi
    else
        log "Fetching certificate for $DOMAIN_APP and $DOMAIN_API..."
        # Port 80 must be free for --standalone mode. Stop nginx container if running.
        docker compose -f $COMPOSE_FILE stop nginx 2>/dev/null || true
        certbot certonly --standalone --non-interactive --agree-tos \
            --email "$LETSENCRYPT_EMAIL" \
            -d "$DOMAIN_APP" -d "$DOMAIN_API" \
            || fail "certbot failed — check the messages above. Common cause: DNS not yet propagated. Wait 10 min and retry."
        ok "Certificate obtained for both domains"
    fi

    # Setup auto-renewal (idempotent — only adds the cron line if missing).
    RENEWAL_CRON="0 3 * * * cd $APP_DIR && docker compose -f $COMPOSE_FILE stop nginx && certbot renew --quiet && docker compose -f $COMPOSE_FILE start nginx"
    if ! crontab -l 2>/dev/null | grep -qF "certbot renew"; then
        (crontab -l 2>/dev/null; echo "$RENEWAL_CRON") | crontab -
        ok "SSL auto-renewal cron job installed (runs 3am daily)"
    else
        ok "SSL auto-renewal already configured"
    fi
fi  # end TARGET=live cert block

# =========================================================================
step "Step 6 — Build + start the application"
# =========================================================================

log "Building Docker images (10-15 min on first run, faster on subsequent)..."
log "[Live build output below — be patient]"
echo
docker compose -f $COMPOSE_FILE up -d --build --remove-orphans

# =========================================================================
step "Step 7 — Health check"
# =========================================================================

log "Waiting for all services to become healthy (up to 90s)..."
HEALTHY=0
for i in $(seq 1 18); do
    sleep 5
    UNHEALTHY=$(docker compose -f $COMPOSE_FILE ps --format json 2>/dev/null \
        | grep -c '"Health":"unhealthy"' || true)
    STARTING=$(docker compose -f $COMPOSE_FILE ps --format json 2>/dev/null \
        | grep -c '"Health":"starting"' || true)
    if [ "$UNHEALTHY" = "0" ] && [ "$STARTING" = "0" ]; then
        HEALTHY=1
        break
    fi
    log "  ... still starting (attempt $i/18)"
done

echo
docker compose -f $COMPOSE_FILE ps
echo

if [ "$HEALTHY" = "1" ]; then
    ok "All services are healthy"
else
    warn "Some services may still be starting. Check with:"
    echo "    docker compose -f $COMPOSE_FILE logs --tail 80 api"
fi

# Staging-only — splice the staging vhosts into the prod nginx so the public
# nginx (which owns 80/443) starts proxying staging.app.* and staging.api.*
# to the staging containers. Idempotent: re-running re-syncs the conf and is
# a no-op if everything is already in place.
if [ "$TARGET" = "staging" ]; then
    PROD_DIR="/opt/marketingapp"
    if [ -d "$PROD_DIR/nginx/conf.d" ]; then
        log "Splicing staging vhost into the shared prod nginx..."
        cp -f "$APP_DIR/nginx/conf.d/marketingapp-staging.conf" \
              "$PROD_DIR/nginx/conf.d/marketingapp-staging.conf"
        # Connect the prod nginx container to the staging network (idempotent
        # via `|| true` — fails harmlessly on the second run).
        STAGING_NET="$(basename "$APP_DIR")_marketingapp_staging"
        docker network connect "$STAGING_NET" marketingapp-nginx 2>/dev/null || true
        # Reload nginx in-place — zero downtime for prod traffic.
        docker exec marketingapp-nginx nginx -s reload \
            && ok "Prod nginx reloaded with staging vhosts" \
            || warn "Could not reload prod nginx; check 'docker logs marketingapp-nginx'"
    else
        warn "Prod nginx not deployed at $PROD_DIR — staging vhost splice skipped."
        warn "Deploy live first (./deploy.sh --target live), then re-run this staging deploy."
    fi
fi

# Probe the public URL one last time.
if curl -fsS -o /dev/null -w "%{http_code}" "https://$DOMAIN_APP" 2>/dev/null | grep -qE "^(200|301|302)$"; then
    ok "Frontend is responding at https://$DOMAIN_APP"
fi

# =========================================================================
echo
echo -e "${GREEN}═════════════════════════════════════════════════${NC}"
echo -e "${GREEN}   🎉 DEPLOYMENT COMPLETE (target: $TARGET)${NC}"
echo -e "${GREEN}═════════════════════════════════════════════════${NC}"
echo
echo "  Frontend:  https://$DOMAIN_APP"
echo "  API:       https://$DOMAIN_API"
echo "  Health:    https://$DOMAIN_API/health"
echo
echo "  📊 Logs:    docker compose -f $COMPOSE_FILE logs -f api"
echo "  🔄 Redeploy: cd $APP_DIR && bash deploy.sh"
echo "  🗄️  DB shell:  docker exec -it marketingapp-postgres psql -U marketingapp"
echo
echo "  Open https://$DOMAIN_APP in your browser to log in!"
echo
