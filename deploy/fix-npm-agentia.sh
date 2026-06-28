#!/usr/bin/env bash
# Régénère manuellement le proxy NPM pour agentiafactory (id=10)
set -euo pipefail

CONF="/var/lib/docker/volumes/npm_data/_data/nginx/proxy_host/10.conf"

sudo tee "$CONF" > /dev/null <<'NGINX'
# ------------------------------------------------------------
# agentiafactory.gisebs.com — Agentia .NET (Web :8081, API :8080)
# ------------------------------------------------------------

map $scheme $hsts_header {
    https   "max-age=63072000;includeSubDomains; preload";
}

server {
  listen 80;
  listen [::]:80;
  listen 443 ssl;
  listen [::]:443 ssl;
  http2 on;
  server_name agentiafactory.gisebs.com;

  include conf.d/include/letsencrypt-acme-challenge.conf;
  include conf.d/include/ssl-cache.conf;
  include conf.d/include/ssl-ciphers.conf;
  ssl_certificate /etc/letsencrypt/live/npm-14/fullchain.pem;
  ssl_certificate_key /etc/letsencrypt/live/npm-14/privkey.pem;
  include conf.d/include/block-exploits.conf;
  add_header Strict-Transport-Security $hsts_header always;
  set $trust_forwarded_proto "F";
  include conf.d/include/force-ssl.conf;

  access_log /data/logs/proxy-host-10_access.log proxy;
  error_log /data/logs/proxy-host-10_error.log warn;

  location /api {
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Scheme $scheme;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_pass http://172.17.0.1:8080;
  }

  location /hubs {
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_read_timeout 86400s;
    proxy_pass http://172.17.0.1:8080;
  }

  location /swagger {
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_pass http://172.17.0.1:8080;
  }

  location /health {
    proxy_set_header Host $host;
    proxy_pass http://172.17.0.1:8080;
  }

  location / {
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Scheme $scheme;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_pass http://172.17.0.1:8081;
  }
}
NGINX

docker exec nginx-proxy-manager-app-1 nginx -t
docker exec nginx-proxy-manager-app-1 nginx -s reload
echo "✓ NPM proxy agentiafactory mis à jour (Web:8081, API:8080)"
