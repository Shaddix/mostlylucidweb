# Docker Registry Quick Start

## TL;DR

```bash
# 1. Configure
cp .env.example .env
nano .env  # Set username, password hash, and tunnel token

# 2. Deploy
chmod +x deploy.sh
./deploy.sh

# 3. Use
docker login docker.mostlylucid.net
docker tag myimage:latest docker.mostlylucid.net/myimage:latest
docker push docker.mostlylucid.net/myimage:latest
```

## Files Overview

- **docker-compose.yml** - Main configuration (registry + caddy + cloudflared + watchtower)
- **.env.example** - Copy to `.env` and configure
- **config/Caddyfile** - Caddy reverse proxy config (HTTPS + auth)
- **deploy.sh** - Automated deployment script
- **README.md** - Full documentation
- **DEPLOY.md** - Detailed deployment guide

## Before You Start

### 1. Generate Password Hash

```bash
docker run --rm caddy:2-alpine caddy hash-password --plaintext 'your-password'
```

Copy the output to `.env` as `REGISTRY_PASSWORD_HASH`

### 2. Create Cloudflare Tunnel

1. Go to https://one.dash.cloudflare.com/
2. Networks → Tunnels → Create tunnel
3. Name: `docker-registry`
4. Configure:
   - Public hostname: `docker.mostlylucid.net`
   - Service: `http://caddy:80`
5. Copy tunnel token to `.env` as `CLOUDFLARE_TUNNEL_TOKEN`

### 3. Configure .env

```env
REGISTRY_DOMAIN=docker.mostlylucid.net
REGISTRY_USERNAME=admin
REGISTRY_PASSWORD_HASH=$2a$14$...
CLOUDFLARE_TUNNEL_TOKEN=eyJh...
```

## Deployment

### SCP to Server

```bash
scp -r local-mostlylucid-net user@server:/opt/
ssh user@server
cd /opt/local-mostlylucid-net
```

### Start Services

```bash
# Automated
./deploy.sh

# Manual
docker-compose up -d
docker-compose ps
docker-compose logs -f
```

## Common Commands

```bash
# View status
docker-compose ps

# View logs
docker-compose logs -f

# Restart services
docker-compose restart

# Stop services
docker-compose down

# Update images
docker-compose pull && docker-compose up -d

# Backup registry
docker run --rm -v registry_data:/data -v $(pwd)/backups:/backup \
  alpine tar czf /backup/registry-$(date +%Y%m%d).tar.gz -C /data .

# Garbage collection
docker-compose exec registry bin/registry garbage-collect /etc/docker/registry/config.yml
```

## Using the Registry

```bash
# Login
docker login docker.mostlylucid.net

# Push image
docker tag myapp:latest docker.mostlylucid.net/myapp:latest
docker push docker.mostlylucid.net/myapp:latest

# Pull image
docker pull docker.mostlylucid.net/myapp:latest

# List images
curl -u username:password https://docker.mostlylucid.net/v2/_catalog

# List tags
curl -u username:password https://docker.mostlylucid.net/v2/myapp/tags/list
```

## Architecture

```
Internet
    ↓
Cloudflare Tunnel
    ↓
Caddy (port 80)
    ├── HTTPS termination
    ├── Basic auth
    └── Reverse proxy
        ↓
Docker Registry (port 5000)
    └── Image storage
```

## Services

- **registry** - Docker Registry v2 (image storage)
- **caddy** - Reverse proxy with HTTPS and auth
- **cloudflared** - Cloudflare tunnel for external access
- **watchtower** - Automatic updates every 6 hours

## Troubleshooting

### Can't login
```bash
# Check credentials
docker-compose config | grep REGISTRY_

# Restart caddy
docker-compose restart caddy
```

### Tunnel not connecting
```bash
# Check logs
docker-compose logs cloudflared

# Verify token
docker-compose config | grep CLOUDFLARE_TUNNEL_TOKEN
```

### Service unhealthy
```bash
# Check logs
docker-compose logs registry
docker-compose logs caddy

# Restart
docker-compose restart
```

## Security

- Use strong password and secure the `.env` file
- Keep services updated (Watchtower does this automatically)
- Monitor logs regularly: `docker-compose logs -f`
- Backup registry data regularly
- Use Cloudflare Access policies to restrict tunnel access

## Next Steps

See **DEPLOY.md** for detailed deployment instructions.
See **README.md** for complete documentation.
