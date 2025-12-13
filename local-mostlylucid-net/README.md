# Docker Registry Setup Guide

This guide will help you set up a private Docker registry with authentication, automatic updates, and secure external access via Cloudflare Tunnel.

## Architecture

```
Internet
    ↓
Cloudflare Tunnel (cloudflared)
    ↓
Caddy (reverse proxy + auth + HTTPS)
    ↓
Docker Registry (v2)
```

## Prerequisites

- Docker and Docker Compose installed
- Domain configured in Cloudflare (docker.mostlylucid.net)
- Cloudflare Zero Trust account for tunnel setup

## Quick Start

### 1. Configure Environment Variables

```bash
# Copy example environment file
cp config/registry/.env.registry.example .env

# Generate password hash
docker run --rm caddy:2-alpine caddy hash-password --plaintext 'your-secure-password'

# Edit .env and update:
# - REGISTRY_USERNAME (your desired username)
# - REGISTRY_PASSWORD_HASH (output from command above)
# - CLOUDFLARE_TUNNEL_TOKEN (from Cloudflare dashboard)
```

### 2. Set Up Cloudflare Tunnel

1. Go to [Cloudflare Zero Trust Dashboard](https://one.dash.cloudflare.com/)
2. Navigate to **Networks** → **Tunnels**
3. Click **Create a tunnel**
4. Name it `docker-registry` and click **Save tunnel**
5. Copy the tunnel token and add it to your `.env` file
6. Configure the tunnel:
   - **Public hostname**: `docker.mostlylucid.net`
   - **Service**: `http://caddy:80`
7. Click **Save**

### 3. Start the Registry

```bash
# Start all services
docker-compose -f docker-registry-compose.yml up -d

# Check service status
docker-compose -f docker-registry-compose.yml ps

# View logs
docker-compose -f docker-registry-compose.yml logs -f
```

### 4. Test the Registry

```bash
# Login to your registry
docker login docker.mostlylucid.net

# Tag an image
docker tag alpine:latest docker.mostlylucid.net/alpine:latest

# Push the image
docker push docker.mostlylucid.net/alpine:latest

# Pull the image
docker pull docker.mostlylucid.net/alpine:latest
```

## Services Overview

### Docker Registry (registry:2)

The core Docker registry service that stores your container images.

**Configuration:**
- Storage: `/var/lib/registry` (persistent volume)
- Port: 5000 (internal only)
- Deletion enabled: Yes
- Automatic garbage collection: Weekly (configurable)

**Useful Commands:**

```bash
# List all images in registry
curl -u username:password https://docker.mostlylucid.net/v2/_catalog

# List tags for an image
curl -u username:password https://docker.mostlylucid.net/v2/myimage/tags/list

# Run garbage collection manually
docker exec docker_registry bin/registry garbage-collect /etc/docker/registry/config.yml

# View registry logs
docker logs docker_registry -f
```

### Caddy (caddy:2-alpine)

Reverse proxy handling HTTPS, authentication, and routing.

**Features:**
- Automatic HTTPS via Let's Encrypt
- Basic authentication for registry access
- Request logging
- Security headers
- Large file upload support (5GB max)

**Configuration File:** `config/registry/Caddyfile.registry`

**Useful Commands:**

```bash
# Reload Caddy configuration
docker exec docker_registry_caddy caddy reload --config /etc/caddy/Caddyfile

# View Caddy logs
docker logs docker_registry_caddy -f

# Validate Caddyfile
docker exec docker_registry_caddy caddy validate --config /etc/caddy/Caddyfile

# Generate new password hash
docker run --rm caddy:2-alpine caddy hash-password --plaintext 'newpassword'
```

### Cloudflare Tunnel (cloudflared)

Secure tunnel providing external access without opening firewall ports.

**Configuration:**
- Token-based authentication
- Connects to Cloudflare edge network
- Routes traffic to Caddy on port 80

**Useful Commands:**

```bash
# View tunnel logs
docker logs docker_registry_cloudflared -f

# Restart tunnel
docker restart docker_registry_cloudflared
```

### Watchtower (containrrr/watchtower)

Automatic container updates for all services.

**Configuration:**
- Update interval: 6 hours
- Automatic cleanup: Yes
- Label-based filtering: Only updates labeled containers

**Useful Commands:**

```bash
# Force update check now
docker exec docker_registry_watchtower /watchtower --run-once

# View Watchtower logs
docker logs docker_registry_watchtower -f
```

## Maintenance

### Backup Registry Data

```bash
# Stop registry
docker-compose -f docker-registry-compose.yml stop registry

# Backup registry data
docker run --rm -v registry_data:/data -v $(pwd)/backups:/backup alpine tar czf /backup/registry-backup-$(date +%Y%m%d).tar.gz -C /data .

# Start registry
docker-compose -f docker-registry-compose.yml start registry
```

### Restore Registry Data

```bash
# Stop registry
docker-compose -f docker-registry-compose.yml stop registry

# Restore registry data
docker run --rm -v registry_data:/data -v $(pwd)/backups:/backup alpine sh -c "rm -rf /data/* && tar xzf /backup/registry-backup-YYYYMMDD.tar.gz -C /data"

# Start registry
docker-compose -f docker-registry-compose.yml start registry
```

### View All Images in Registry

```bash
# Using curl
curl -u username:password https://docker.mostlylucid.net/v2/_catalog | jq .

# Get detailed image information
for image in $(curl -s -u username:password https://docker.mostlylucid.net/v2/_catalog | jq -r '.repositories[]'); do
    echo "Image: $image"
    curl -s -u username:password https://docker.mostlylucid.net/v2/$image/tags/list | jq .
done
```

### Delete an Image

```bash
# Get image digest
DIGEST=$(curl -s -u username:password -H "Accept: application/vnd.docker.distribution.manifest.v2+json" \
    https://docker.mostlylucid.net/v2/myimage/manifests/latest | jq -r '.config.digest')

# Delete image
curl -u username:password -X DELETE https://docker.mostlylucid.net/v2/myimage/manifests/$DIGEST

# Run garbage collection to reclaim space
docker exec docker_registry bin/registry garbage-collect /etc/docker/registry/config.yml
```

## Troubleshooting

### Cannot Login to Registry

**Check credentials:**
```bash
# Verify environment variables are loaded
docker-compose -f docker-registry-compose.yml config | grep REGISTRY_

# Test authentication manually
curl -u username:password https://docker.mostlylucid.net/v2/
```

**Regenerate password hash:**
```bash
docker run --rm caddy:2-alpine caddy hash-password --plaintext 'your-password'
# Update REGISTRY_PASSWORD_HASH in .env
docker-compose -f docker-registry-compose.yml restart caddy
```

### Cloudflare Tunnel Not Connecting

**Check tunnel token:**
```bash
# View cloudflared logs
docker logs docker_registry_cloudflared -f

# Common issues:
# - Invalid token
# - Tunnel not configured in Cloudflare dashboard
# - Wrong service URL (should be http://caddy:80)
```

### Large Image Uploads Failing

**Increase timeout values in Caddyfile.registry:**
```caddyfile
reverse_proxy registry:5000 {
    transport http {
        read_timeout 600s   # Increase from 300s
        write_timeout 600s  # Increase from 300s
    }
}
```

**Increase request body size:**
```caddyfile
request_body {
    max_size 10GB  # Increase from 5GB
}
```

### Registry Running Out of Space

**Check disk usage:**
```bash
docker exec docker_registry du -sh /var/lib/registry

# Run garbage collection
docker exec docker_registry bin/registry garbage-collect /etc/docker/registry/config.yml

# Delete old/unused images and run GC again
```

### View Service Health

```bash
# Check all service health
docker-compose -f docker-registry-compose.yml ps

# Check specific service health
docker inspect --format='{{.State.Health.Status}}' docker_registry
docker inspect --format='{{.State.Health.Status}}' docker_registry_caddy

# View health check logs
docker inspect --format='{{range .State.Health.Log}}{{.Output}}{{end}}' docker_registry
```

## Security Best Practices

1. **Use Strong Passwords**: Generate secure passwords for registry authentication
2. **Regular Updates**: Watchtower handles automatic updates, but monitor logs
3. **Backup Regularly**: Schedule regular backups of registry data
4. **Monitor Logs**: Check logs regularly for suspicious activity
5. **Limit Access**: Use Cloudflare Access policies to restrict who can reach the tunnel
6. **Enable Notifications**: Configure Watchtower notifications for update alerts

## Stopping and Removing

```bash
# Stop all services
docker-compose -f docker-registry-compose.yml stop

# Stop and remove containers (data is preserved in volumes)
docker-compose -f docker-registry-compose.yml down

# Remove everything including volumes (WARNING: deletes all images!)
docker-compose -f docker-registry-compose.yml down -v
```

## Additional Resources

- [Docker Registry Documentation](https://docs.docker.com/registry/)
- [Caddy Documentation](https://caddyserver.com/docs/)
- [Cloudflare Tunnel Documentation](https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/)
- [Watchtower Documentation](https://containrrr.dev/watchtower/)
