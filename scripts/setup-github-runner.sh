#!/usr/bin/env bash
# =========================================================================
# One-time setup: install a self-hosted GitHub Actions runner ON the VPS.
#
# After this, every `git push` to master triggers an automatic deploy — the
# VPS long-polls GitHub (outbound), picks up the deploy job, and runs
# `docker compose up -d --build` locally. No inbound SSH, no firewall changes.
#
# Usage (on the VPS, as root):
#   bash setup-github-runner.sh <REGISTRATION_TOKEN>
#
# Get a fresh REGISTRATION_TOKEN (valid 1 hour) from either:
#   • GitHub UI: repo → Settings → Actions → Runners → New self-hosted runner
#   • gh CLI:    gh api -X POST repos/farooquifaraz/MultiChannelMarketingApp/actions/runners/registration-token -q .token
# =========================================================================
set -euo pipefail

REPO_URL="https://github.com/farooquifaraz/MultiChannelMarketingApp"
RUNNER_VERSION="2.334.0"
RUNNER_DIR="/opt/actions-runner"
RUNNER_NAME="marketingapp-vps"
RUNNER_LABELS="self-hosted,marketingapp"

TOKEN="${1:-}"
[ -n "$TOKEN" ] || { echo "✗ Usage: bash setup-github-runner.sh <REGISTRATION_TOKEN>"; exit 1; }
[ "$(id -u)" -eq 0 ] || { echo "✗ Run as root."; exit 1; }

# Sanity: the deploy job needs these on the VPS.
command -v docker >/dev/null || { echo "✗ docker not installed"; exit 1; }
command -v git    >/dev/null || { echo "✗ git not installed"; exit 1; }

echo "▸ Installing runner v${RUNNER_VERSION} into ${RUNNER_DIR}…"
mkdir -p "$RUNNER_DIR" && cd "$RUNNER_DIR"
if [ ! -f "./config.sh" ]; then
  curl -fsSL -o actions-runner.tar.gz \
    "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-x64-${RUNNER_VERSION}.tar.gz"
  tar xzf actions-runner.tar.gz
  rm -f actions-runner.tar.gz
fi

# If a runner is already configured here, remove it first so --replace is clean.
export RUNNER_ALLOW_RUNASROOT=1
if [ -f ".runner" ]; then
  echo "▸ Existing runner config found — reconfiguring…"
  ./svc.sh stop 2>/dev/null || true
  ./svc.sh uninstall 2>/dev/null || true
  ./config.sh remove --token "$TOKEN" 2>/dev/null || true
fi

echo "▸ Registering with ${REPO_URL}…"
./config.sh \
  --url "$REPO_URL" \
  --token "$TOKEN" \
  --name "$RUNNER_NAME" \
  --labels "$RUNNER_LABELS" \
  --work _work \
  --unattended \
  --replace

echo "▸ Installing as a systemd service (auto-start on reboot)…"
./svc.sh install
./svc.sh start
sleep 2
./svc.sh status || true

echo
echo "✅ Self-hosted runner '${RUNNER_NAME}' is live with labels: ${RUNNER_LABELS}"
echo "   Verify in GitHub: repo → Settings → Actions → Runners (should show 'Idle')."
echo "   Now any push to master auto-deploys. Test: gh workflow run \"CI/CD\" --ref master"
