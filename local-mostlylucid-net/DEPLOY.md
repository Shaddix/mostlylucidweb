# Docker Registry Deployment Instructions

This document provides step-by-step instructions for deploying your private Docker registry to a remote server.

## Prerequisites

- A server with Docker and Docker Compose installed
- SSH access to the server
- A domain (docker.mostlylucid.net) configured in Cloudflare
- Cloudflare Zero Trust account for tunnel setup

## Step 1: Prepare Configuration

On your local machine:

```bash
# Navigate to the deployment directory
cd local-mostlylucid-net

# Copy the example environment file
cp .env.example .env

# Edit the .env file with your configuration
nano .env  # or use your preferred editor
```

Required configuration in `.env`:

1. **REGISTRY_USERNAME**: Your desired registry username
2. **REGISTRY_PASSWORD_HASH**: Generate with this command:
   ```bash
   docker run --rm caddy:2-alpine caddy hash-password --plaintext 'your-secure-password'
   ```
3. **CLOUDFLARE_TUNNEL_TOKEN**: Get from Cloudflare dashboard (see Step 3)

## Step 2: Set Up Cloudflare Tunnel

1. Go to [Cloudflare Zero Trust Dashboard](https://one.dash.cloudflare.com/)
2. Navigate to **Networks** → **Tunnels**
3. Click **Create a tunnel**
4. Name it `docker-registry` and click **Save tunnel**
5. Copy the tunnel token
6. Add it to your `.env` file as `CLOUDFLARE_TUNNEL_TOKEN`
7. Configure the tunnel:
   - **Public hostname**: `docker.mostlylucid.net`
   - **Service Type**: HTTP
   - **URL**: `http://caddy:80`
8. Click **Save**

**Important**: The tunnel will connect to the `caddy` service via the internal Docker network.

## Step 3: Transfer Files to Server

Use SCP to transfer the entire directory to your server:

```bash
# From your local machine
scp -r local-mostlylucid-net user@your-server-ip:/opt/

# SSH into the server
ssh user@your-server-ip

# Navigate to the directory
cd /opt/local-mostlylucid-net
```

Alternatively, if you prefer a different location:

```bash
# Transfer to home directory
scp -r local-mostlylucid-net user@your-server-ip:~/

# SSH and move to preferred location
ssh user@your-server-ip
sudo mv ~/local-mostlylucid-net /opt/
cd /opt/local-mostlylucid-net
```

## Step 4: Deploy on Server

On the remote server:

```bash
# Make deploy script executable
chmod +x deploy.sh

# Run deployment script
./deploy.sh
```

Or manually:

```bash
# Verify .env file is configured
cat .env

# Start services
docker-compose up -d

# Check service status
docker-compose ps

# View logs
docker-compose logs -f
```

## Step 5: Verify Deployment

### Check Service Health

```bash
# Check all services are running
docker-compose ps

# Expected output:
# NAME                          STATUS
# docker_registry               Up (healthy)
# docker_registry_caddy         Up (healthy)
# docker_registry_cloudflared   Up
# docker_registry_watchtower    Up
```

### Check Cloudflare Tunnel

```bash
# View tunnel logs
docker-compose logs cloudflared

# Should see: "Connection ... registered"
```

### Test Registry Access

From your local machine:

```bash
# Test HTTPS access
curl -u username:password https://docker.mostlylucid.net/v2/

# Should return: {}
```

### Login to Registry

```bash
# Login
docker login docker.mostlylucid.net

# Enter your username and password
# Should see: "Login Succeeded"
```

## Step 6: Push Your First Image

```bash
# Tag an existing image
docker tag alpine:latest docker.mostlylucid.net/alpine:latest

# Push to your registry
docker push docker.mostlylucid.net/alpine:latest

# Pull from your registry
docker pull docker.mostlylucid.net/alpine:latest
```

## Directory Structure

```
local-mostlylucid-net/
├── docker-compose.yml      # Main compose configuration
├── .env.example            # Example environment variables
├── .env                    # Your actual configuration (create this)
├── deploy.sh               # Deployment script
├── README.md               # Main documentation
├── DEPLOY.md               # This file
└── config/
    └── Caddyfile           # Caddy reverse proxy configuration
```

## Troubleshooting

### Cannot Login to Registry

**Issue**: `docker login` fails with authentication error

**Solution**:
```bash
# Regenerate password hash
docker run --rm caddy:2-alpine caddy hash-password --plaintext 'your-password'

# Update REGISTRY_PASSWORD_HASH in .env
nano .env

# Restart Caddy
docker-compose restart caddy
```

### Cloudflare Tunnel Not Connecting

**Issue**: Tunnel logs show connection errors

**Solution**:
```bash
# Check tunnel token is correct
docker-compose config | grep CLOUDFLARE_TUNNEL_TOKEN

# Verify tunnel configuration in Cloudflare dashboard
# - Public hostname: docker.mostlylucid.net
# - Service: http://caddy:80

# Restart tunnel
docker-compose restart cloudflared
```

### Services Not Starting

**Issue**: Services fail health checks

**Solution**:
```bash
# Check logs for specific service
docker-compose logs registry
docker-compose logs caddy

# Check disk space
df -h

# Check if ports are available
sudo netstat -tlnp | grep :80
```

### Cannot Push Large Images

**Issue**: Image push fails or times out

**Solution**:

Edit `config/Caddyfile` and increase timeouts:

```caddyfile
reverse_proxy registry:5000 {
    transport http {
        read_timeout 600s
        write_timeout 600s
    }
}

request_body {
    max_size 10GB
}
```

Then reload Caddy:
```bash
docker-compose exec caddy caddy reload --config /etc/caddy/Caddyfile
```

## Maintenance

### View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f registry
docker-compose logs -f caddy
docker-compose logs -f cloudflared
```

### Restart Services

```bash
# All services
docker-compose restart

# Specific service
docker-compose restart registry
```

### Update Services

Watchtower automatically updates containers every 6 hours. To force an update:

```bash
docker-compose pull
docker-compose up -d
```

### Backup Registry Data

```bash
# Stop registry
docker-compose stop registry

# Create backup
docker run --rm \
  -v registry_data:/data \
  -v $(pwd)/backups:/backup \
  alpine tar czf /backup/registry-$(date +%Y%m%d).tar.gz -C /data .

# Start registry
docker-compose start registry
```

### Clean Up Old Images

```bash
# Run garbage collection
docker-compose exec registry bin/registry garbage-collect /etc/docker/registry/config.yml
```

## Security Recommendations

1. **Use Strong Passwords**: Generate secure passwords for registry authentication
2. **Regular Backups**: Schedule regular backups of registry data
3. **Monitor Logs**: Check logs regularly for suspicious activity
4. **Keep Updated**: Watchtower handles updates, but monitor the process
5. **Restrict Access**: Use Cloudflare Access policies to limit who can reach the tunnel
6. **Firewall**: Only port 80 needs to be accessible to Cloudflare tunnel

## Updating Configuration

To change configuration after deployment:

```bash
# Edit .env file
nano .env

# Recreate affected services
docker-compose up -d

# Or restart all services
docker-compose restart
```

## Stopping Services

```bash
# Stop all services (preserves data)
docker-compose stop

# Stop and remove containers (preserves data in volumes)
docker-compose down

# Remove everything including volumes (WARNING: deletes all images!)
docker-compose down -v
```

## Support

For issues or questions:

- Docker Registry: https://docs.docker.com/registry/
- Caddy: https://caddyserver.com/docs/
- Cloudflare Tunnel: https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/
- Watchtower: https://containrrr.dev/watchtower/
