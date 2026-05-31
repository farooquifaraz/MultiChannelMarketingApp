# =========================================================================
# Frontend — Vite build + Nginx static serve
#
# Stage 1 (node): install deps, build the SPA into /app/dist.
# Stage 2 (nginx): copy the built static assets into an nginx:alpine image.
#   Resulting image is tiny (~25 MB) and serves the SPA at port 80.
#
# Build from frontend/ directory:  docker build -t marketingapp-frontend .
#
# VITE_API_URL must be supplied at BUILD time (Vite bakes env vars into the
# bundle — they can't change at runtime). docker-compose passes it as a build arg.
# =========================================================================

# --- Stage 1: build ---
FROM node:20-alpine AS build
WORKDIR /app

# Build-time configuration (the only env vars Vite cares about are VITE_*).
ARG VITE_API_URL=http://localhost:5211/api/v1
ENV VITE_API_URL=$VITE_API_URL

# Install deps first (separate layer = better cache reuse than copying all source).
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

# Build the SPA. Vite writes to /app/dist by default.
COPY . .
RUN npm run build

# --- Stage 2: serve via nginx ---
FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html

# SPA-friendly nginx config: any unknown path falls back to index.html so the
# React Router handles it (otherwise hard-reload on /campaigns gives a 404).
RUN echo 'server { \
    listen 80; \
    server_name _; \
    root /usr/share/nginx/html; \
    index index.html; \
    gzip on; gzip_types text/css application/javascript application/json image/svg+xml; \
    location / { try_files $uri $uri/ /index.html; } \
    location ~* \.(js|css|svg|woff2?|ttf|eot|ico|png|jpg|jpeg|gif|webp)$ { \
        expires 30d; add_header Cache-Control "public, immutable"; \
    } \
}' > /etc/nginx/conf.d/default.conf

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD wget -qO- http://localhost/ >/dev/null || exit 1
