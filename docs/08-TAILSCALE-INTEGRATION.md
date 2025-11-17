# Tailscale Integration Guide

## Overview

Tailscale provides secure, private networking for your home server applications without exposing ports to the internet. Each application gets its own hostname on your private Tailnet.

## Architecture

```
┌─────────────────────────────────────────────────┐
│  Docker Stack (per application)                 │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌──────────────┐        ┌─────────────────┐   │
│  │  App         │◄──────►│  Tailscale      │   │
│  │  Container   │  HTTP  │  Sidecar        │   │
│  │  (Port 5000) │        │  (tsnet)        │   │
│  └──────────────┘        └─────────────────┘   │
│         │                         │             │
│         │                         │             │
│         └─────────┬───────────────┘             │
│                   │                             │
│           app-network (Bridge)                  │
│                                                 │
└─────────────────────────────────────────────────┘
                    │
                    │ WireGuard (encrypted)
                    │
              ┌─────▼─────┐
              │ Tailnet   │
              │ (Private) │
              └───────────┘
                    │
        ┌───────────┼───────────┐
        │           │           │
  ┌─────▼─────┐ ┌──▼───────┐ ┌─▼─────────┐
  │  Laptop   │ │  Phone   │ │ Other     │
  │  (Client) │ │ (Client) │ │ Services  │
  └───────────┘ └──────────┘ └───────────┘
```

## Prerequisites

1. **Tailscale Account**: Sign up at https://tailscale.com
2. **Auth Key**: Generate at https://login.tailscale.com/admin/settings/keys
   - Select "Reusable" for multiple deployments
   - Select "Ephemeral" if you want keys to expire
   - Add tags (e.g., `tag:home-server`)

## Docker Compose with Tailscale Sidecar

### Basic Setup

```yaml
version: '3.8'

services:
  # Your application
  app:
    build: .
    container_name: my-app
    restart: unless-stopped
    environment:
      - ASPNETCORE_URLS=http://+:5000
    networks:
      - app-network
    depends_on:
      - tailscale

  # Tailscale sidecar
  tailscale:
    image: tailscale/tailscale:latest
    container_name: my-app-tailscale
    hostname: my-app
    restart: unless-stopped
    environment:
      - TS_AUTHKEY=${TS_AUTHKEY}
      - TS_STATE_DIR=/var/lib/tailscale
      - TS_HOSTNAME=my-app
      - TS_ACCEPT_DNS=true
      - TS_EXTRA_ARGS=--advertise-tags=tag:home-server
    volumes:
      - tailscale-state:/var/lib/tailscale
      - /dev/net/tun:/dev/net/tun
    cap_add:
      - NET_ADMIN
      - SYS_MODULE
    networks:
      - app-network

networks:
  app-network:
    driver: bridge

volumes:
  tailscale-state:
```

### Advanced Configuration with Nginx Proxy

If you want to route traffic through the Tailscale container:

```yaml
version: '3.8'

services:
  app:
    build: .
    container_name: my-app
    restart: unless-stopped
    environment:
      - ASPNETCORE_URLS=http://+:5000
    networks:
      - app-network
    # Note: No port exposure needed

  tailscale:
    image: tailscale/tailscale:latest
    container_name: my-app-tailscale
    hostname: my-app
    restart: unless-stopped
    environment:
      - TS_AUTHKEY=${TS_AUTHKEY}
      - TS_STATE_DIR=/var/lib/tailscale
      - TS_HOSTNAME=my-app
      - TS_ACCEPT_DNS=true
      - TS_EXTRA_ARGS=--advertise-tags=tag:home-server
    volumes:
      - tailscale-state:/var/lib/tailscale
      - /dev/net/tun:/dev/net/tun
    cap_add:
      - NET_ADMIN
      - SYS_MODULE
    networks:
      - app-network
    ports:
      - "8080:80"  # Expose through Tailscale

  nginx:
    image: nginx:alpine
    container_name: my-app-nginx
    restart: unless-stopped
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    networks:
      - app-network
    network_mode: "service:tailscale"  # Share Tailscale's network

networks:
  app-network:
    driver: bridge

volumes:
  tailscale-state:
```

**nginx.conf**:
```nginx
events {
    worker_connections 1024;
}

http {
    upstream app {
        server my-app:5000;
    }

    server {
        listen 80;
        server_name _;

        location / {
            proxy_pass http://app;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection 'upgrade';
            proxy_set_header Host $host;
            proxy_cache_bypass $http_upgrade;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
```

## Environment Variables

Create `.env` file:

```bash
# Tailscale Auth Key (get from https://login.tailscale.com/admin/settings/keys)
TS_AUTHKEY=tskey-auth-xxxxxxxxxxxxxxxxxxxxxxxxxxxxx

# Optional: Custom hostname
TS_HOSTNAME=my-fsharp-app

# Optional: Tags for ACL management
TS_EXTRA_ARGS=--advertise-tags=tag:home-server,tag:docker
```

**⚠️ Important**: Add `.env` to `.gitignore` to avoid committing secrets!

## Tailscale ACLs (Access Control Lists)

Configure at https://login.tailscale.com/admin/acls

### Basic ACL Example

```json
{
  "tagOwners": {
    "tag:home-server": ["your-email@example.com"]
  },
  "acls": [
    {
      "action": "accept",
      "src": ["your-email@example.com"],
      "dst": ["tag:home-server:*"]
    },
    {
      "action": "accept",
      "src": ["tag:home-server"],
      "dst": ["tag:home-server:*"]
    }
  ]
}
```

This allows:
- You (by email) to access all home server apps
- Home server apps to communicate with each other

### Advanced ACL with Port Restrictions

```json
{
  "tagOwners": {
    "tag:home-server": ["your-email@example.com"],
    "tag:mobile": ["your-email@example.com"]
  },
  "acls": [
    {
      "action": "accept",
      "src": ["tag:mobile"],
      "dst": ["tag:home-server:80,443,5000"]
    },
    {
      "action": "accept",
      "src": ["your-email@example.com"],
      "dst": ["tag:home-server:*"]
    }
  ]
}
```

## Deployment Workflow

### 1. Generate Tailscale Auth Key

```bash
# Via CLI (if tailscale is installed)
tailscale up --authkey $(tailscale keys new --ephemeral --reusable)

# Or manually from web UI:
# https://login.tailscale.com/admin/settings/keys
```

### 2. Configure Environment

```bash
# Create .env file
cat > .env << EOF
TS_AUTHKEY=your-auth-key-here
EOF
```

### 3. Deploy Stack

```bash
# Using docker-compose
docker-compose up -d

# Or via Portainer
# 1. Create stack
# 2. Upload docker-compose.yml
# 3. Add TS_AUTHKEY environment variable
# 4. Deploy
```

### 4. Verify Connection

```bash
# Check Tailscale status
docker exec my-app-tailscale tailscale status

# Should show:
# 100.x.x.x    my-app         -  linux   -

# Test from another device on Tailnet
curl http://my-app:80
```

## Accessing Your Application

### From Any Device on Tailnet

```bash
# By hostname
http://my-app

# Or by Tailscale IP
http://100.x.x.x
```

### MagicDNS

Tailscale automatically provides DNS:
- `http://my-app` → Resolves to Tailscale IP
- `http://my-app.your-tailnet.ts.net` → Full DNS name

Enable MagicDNS:
1. Go to https://login.tailscale.com/admin/dns
2. Enable MagicDNS
3. Access apps by hostname

## Multiple Applications

Each app gets its own Tailscale sidecar:

```yaml
# docker-compose.yml for entire server
version: '3.8'

services:
  # App 1
  app1:
    build: ./app1
    networks:
      - app1-network
  
  app1-tailscale:
    image: tailscale/tailscale:latest
    hostname: app1
    environment:
      - TS_AUTHKEY=${TS_AUTHKEY}
      - TS_HOSTNAME=app1
    volumes:
      - app1-tailscale:/var/lib/tailscale
    cap_add:
      - NET_ADMIN
    networks:
      - app1-network

  # App 2
  app2:
    build: ./app2
    networks:
      - app2-network
  
  app2-tailscale:
    image: tailscale/tailscale:latest
    hostname: app2
    environment:
      - TS_AUTHKEY=${TS_AUTHKEY}
      - TS_HOSTNAME=app2
    volumes:
      - app2-tailscale:/var/lib/tailscale
    cap_add:
      - NET_ADMIN
    networks:
      - app2-network

networks:
  app1-network:
  app2-network:

volumes:
  app1-tailscale:
  app2-tailscale:
```

Access:
- App 1: `http://app1`
- App 2: `http://app2`

## Tailscale Serve (Alternative Approach)

Tailscale can expose your app directly without a sidecar:

```bash
# In your app container
tailscale serve https / http://localhost:5000
```

But for home servers, the sidecar approach is recommended for:
- Better isolation
- Easier management
- No need to install Tailscale in app container

## Monitoring Tailscale

### Check Status

```bash
# Status
docker exec my-app-tailscale tailscale status

# Detailed status
docker exec my-app-tailscale tailscale status --json

# Netcheck (network diagnostics)
docker exec my-app-tailscale tailscale netcheck

# Ping another device
docker exec my-app-tailscale tailscale ping other-device
```

### Logs

```bash
# Tailscale logs
docker logs my-app-tailscale

# Follow logs
docker logs -f my-app-tailscale
```

### Health Checks

Add to docker-compose.yml:

```yaml
tailscale:
  # ... other config
  healthcheck:
    test: ["CMD", "tailscale", "status"]
    interval: 30s
    timeout: 5s
    retries: 3
    start_period: 10s
```

## Security Best Practices

### 1. Use Ephemeral Keys for Testing

```bash
# Generate ephemeral key (expires when device goes offline)
tailscale keys new --ephemeral --reusable
```

### 2. Tag Your Devices

```yaml
environment:
  - TS_EXTRA_ARGS=--advertise-tags=tag:home-server,tag:docker,tag:app1
```

### 3. Restrict Access with ACLs

Only allow specific users/devices to access your apps:

```json
{
  "acls": [
    {
      "action": "accept",
      "src": ["user@example.com"],
      "dst": ["tag:home-server:80,443"]
    }
  ]
}
```

### 4. Rotate Auth Keys Regularly

- Generate new keys every 90 days
- Revoke old keys
- Update docker-compose with new key

### 5. Use HTTPS with Tailscale Certs

```bash
# Get Tailscale certificate
docker exec my-app-tailscale tailscale cert my-app.your-tailnet.ts.net
```

Then configure your app to use the cert.

## Troubleshooting

### Tailscale Container Not Starting

```bash
# Check logs
docker logs my-app-tailscale

# Common issues:
# 1. Missing /dev/net/tun
#    Solution: Check host has TUN/TAP support
# 2. Invalid auth key
#    Solution: Generate new key
# 3. CAP_NET_ADMIN not granted
#    Solution: Check cap_add in docker-compose
```

### Can't Reach Application

```bash
# 1. Check Tailscale status
docker exec my-app-tailscale tailscale status

# 2. Check if app is running
docker ps

# 3. Check network connectivity
docker exec my-app-tailscale ping app

# 4. Check app logs
docker logs my-app

# 5. Verify app is listening on correct port
docker exec my-app netstat -tlnp
```

### DNS Not Resolving

```bash
# 1. Check MagicDNS is enabled
# 2. Use full hostname: my-app.your-tailnet.ts.net
# 3. Use Tailscale IP directly as fallback
```

### Performance Issues

```bash
# Check latency
docker exec my-app-tailscale tailscale ping other-device

# Check network diagnostics
docker exec my-app-tailscale tailscale netcheck

# Check for relay usage (DERP)
docker exec my-app-tailscale tailscale status --json | jq '.Peer[].CurAddr'
```

## Integration with F# Application

### Add Tailscale Hostname to Config

**appsettings.Production.json**:
```json
{
  "Tailscale": {
    "Hostname": "my-app",
    "FullDomain": "my-app.your-tailnet.ts.net"
  }
}
```

### Get Tailscale IP at Runtime

```fsharp
// In Program.fs or Api.fs
module Tailscale =
    open System.Net
    
    let getTailscaleIp () : string option =
        try
            let hostname = "my-app"
            let addresses = Dns.GetHostAddresses(hostname)
            
            addresses
            |> Array.tryFind (fun addr -> 
                addr.AddressFamily = Sockets.AddressFamily.InterNetwork &&
                addr.ToString().StartsWith("100.")
            )
            |> Option.map (fun addr -> addr.ToString())
        with _ ->
            None
```

## Cost Considerations

- **Free Tier**: Up to 3 users, 100 devices
- **Personal Pro**: $48/year - Unlimited devices
- **Team Plans**: For larger organizations

For home server use, Free tier is usually sufficient.

## Alternative: Tailscale Funnel (Public Access)

If you need to share an app publicly (not recommended for personal apps):

```bash
# Enable Funnel
docker exec my-app-tailscale tailscale funnel 80

# Access publicly at:
# https://my-app.your-tailnet.ts.net
```

**Note**: Only use Funnel for apps you want to be public.

## Next Steps

- Set up Tailscale on all your devices
- Configure ACLs for fine-grained access control
- Test failover by disconnecting/reconnecting
- Monitor Tailscale status and logs
- Consider Tailscale SSH for secure server access

## Resources

- [Tailscale Documentation](https://tailscale.com/kb/)
- [Docker Integration Guide](https://tailscale.com/kb/1282/docker/)
- [ACL Documentation](https://tailscale.com/kb/1018/acls/)
- [MagicDNS Guide](https://tailscale.com/kb/1081/magicdns/)
