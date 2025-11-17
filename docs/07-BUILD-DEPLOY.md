# Build and Deployment Guide

## Build Process Overview

```
┌─────────────────────────────────────────────┐
│  Multi-Stage Docker Build                   │
├─────────────────────────────────────────────┤
│                                             │
│  Stage 1: Build (SDK + Node.js)             │
│  ├── Restore .NET dependencies              │
│  ├── Restore npm dependencies               │
│  ├── Build Shared project                   │
│  ├── Build Client (Fable → JS)              │
│  └── Build Server                           │
│                                             │
│  Stage 2: Runtime (ASP.NET Core Runtime)    │
│  ├── Copy Server binaries                   │
│  ├── Copy Client static files               │
│  └── Configure Kestrel                      │
│                                             │
└─────────────────────────────────────────────┘
```

## Dockerfile

Create at project root:

```dockerfile
# ============================================
# Stage 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Install Node.js 20.x for Vite/Fable
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs

WORKDIR /app

# Copy project files
COPY . .

# Restore .NET dependencies
WORKDIR /app/src/Shared
RUN dotnet restore

WORKDIR /app/src/Server
RUN dotnet restore

WORKDIR /app/src/Client
RUN dotnet restore

# Restore npm dependencies
WORKDIR /app
RUN npm install

# Build Shared library (needed by both Client and Server)
WORKDIR /app/src/Shared
RUN dotnet build -c Release

# Build Client (Fable compilation + Vite build)
WORKDIR /app
RUN npm run build

# Build Server
WORKDIR /app/src/Server
RUN dotnet publish -c Release -o /app/publish

# ============================================
# Stage 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

# Copy published server
COPY --from=build /app/publish .

# Copy client build artifacts (static files)
COPY --from=build /app/dist/public ./dist/public

# Create data directory for SQLite and files
RUN mkdir -p /app/data

# Expose port
EXPOSE 5000

# Set environment
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

# Run the server
ENTRYPOINT ["dotnet", "Server.dll"]
```

## Build Scripts

### build.sh (Development Build)

```bash
#!/bin/bash
set -e

echo "Building F# Full-Stack App..."

# Build Shared
echo "Building Shared..."
cd src/Shared
dotnet build -c Debug

# Build Server
echo "Building Server..."
cd ../Server
dotnet build -c Debug

# Build Client
echo "Building Client..."
cd ../..
npm run build

echo "Build complete!"
```

### build-docker.sh (Docker Build)

```bash
#!/bin/bash
set -e

APP_NAME="my-fsharp-app"
VERSION=${1:-latest}

echo "Building Docker image: ${APP_NAME}:${VERSION}"

docker build -t ${APP_NAME}:${VERSION} .

echo "Build complete: ${APP_NAME}:${VERSION}"
echo ""
echo "Run with: docker run -p 5000:5000 -v $(pwd)/data:/app/data ${APP_NAME}:${VERSION}"
```

Make executable:
```bash
chmod +x build.sh build-docker.sh
```

## Docker Compose Configuration

### docker-compose.yml

```yaml
version: '3.8'

services:
  # Main application
  app:
    build: .
    container_name: my-fsharp-app
    restart: unless-stopped
    ports:
      - "5000:5000"
    volumes:
      # Persist data (SQLite database and files)
      - ./data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
    networks:
      - app-network
    depends_on:
      - tailscale
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 3s
      retries: 3
      start_period: 10s

  # Tailscale sidecar (see 08-TAILSCALE-INTEGRATION.md)
  tailscale:
    image: tailscale/tailscale:latest
    container_name: my-fsharp-app-tailscale
    hostname: my-fsharp-app
    restart: unless-stopped
    environment:
      - TS_AUTHKEY=${TS_AUTHKEY}
      - TS_STATE_DIR=/var/lib/tailscale
      - TS_HOSTNAME=my-fsharp-app
      - TS_ACCEPT_DNS=true
    volumes:
      - tailscale-data:/var/lib/tailscale
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
  tailscale-data:
```

### .env File

```bash
# Tailscale authentication key
TS_AUTHKEY=tskey-auth-xxxxxxxxxxxxx

# Application settings
ASPNETCORE_ENVIRONMENT=Production
```

## Production Configuration

### appsettings.Production.json

Create in `src/Server/`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      }
    }
  }
}
```

### Program.fs - Production Setup

Add health check endpoint:

```fsharp
module Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe

let healthCheck : HttpHandler =
    fun next ctx ->
        // Check database connection, etc.
        let isHealthy = true
        
        if isHealthy then
            Successful.OK "healthy" next ctx
        else
            ServerErrors.SERVICE_UNAVAILABLE "unhealthy" next ctx

let configureApp (app: IApplicationBuilder) =
    Persistence.ensureDataDir()
    Persistence.initializeDatabase()
    
    app.UseStaticFiles() |> ignore
    app.UseRouting() |> ignore
    
    app.UseEndpoints(fun endpoints ->
        endpoints.MapGet("/health", healthCheck) |> ignore
    ) |> ignore
    
    app.UseGiraffe(Api.webApp)

let configureServices (services: IServiceCollection) =
    services.AddGiraffe() |> ignore
    services.AddHealthChecks() |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    
    builder.WebHost.UseUrls("http://0.0.0.0:5000") |> ignore
    
    configureServices builder.Services
    
    let app = builder.Build()
    
    if not app.Environment.IsDevelopment() then
        app.UseHsts() |> ignore
    
    configureApp app
    
    app.Run()
    0
```

## Deployment to Portainer

### Step 1: Build Image

```bash
./build-docker.sh v1.0.0
```

### Step 2: Export Image (if not using registry)

```bash
docker save my-fsharp-app:v1.0.0 | gzip > my-fsharp-app-v1.0.0.tar.gz
```

### Step 3: Load on Server

Transfer to server and load:
```bash
scp my-fsharp-app-v1.0.0.tar.gz user@server:/tmp/
ssh user@server
docker load < /tmp/my-fsharp-app-v1.0.0.tar.gz
```

### Step 4: Deploy Stack in Portainer

1. Login to Portainer (usually at `http://your-server:9000`)
2. Go to **Stacks** → **Add stack**
3. Name: `my-fsharp-app`
4. Build method: **Web editor**
5. Paste `docker-compose.yml`
6. Add environment variable: `TS_AUTHKEY`
7. Click **Deploy the stack**

### Step 5: Verify Deployment

```bash
# Check logs
docker logs my-fsharp-app

# Check health
curl http://localhost:5000/health

# Check Tailscale status
docker exec my-fsharp-app-tailscale tailscale status
```

## Continuous Deployment

### Using Git Webhooks

Create `deploy.sh` on server:

```bash
#!/bin/bash
set -e

APP_NAME="my-fsharp-app"
REPO_URL="https://github.com/username/my-app.git"
DEPLOY_DIR="/opt/my-fsharp-app"

echo "Starting deployment..."

# Pull latest changes
cd ${DEPLOY_DIR}
git pull origin main

# Build new image
docker build -t ${APP_NAME}:latest .

# Stop old container
docker-compose down

# Start new container
docker-compose up -d

echo "Deployment complete!"
```

### Using Portainer Webhooks

1. In Portainer, go to **Stacks** → your stack → **Webhooks**
2. Create webhook
3. Use webhook URL in CI/CD pipeline:

```bash
# In your CI/CD (GitHub Actions, GitLab CI, etc.)
curl -X POST https://your-portainer.com/api/webhooks/webhook-id
```

## Backup and Restore

### Automated Backup Script

Create `backup.sh`:

```bash
#!/bin/bash
set -e

BACKUP_DIR="/backups/my-fsharp-app"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
DATA_DIR="/opt/my-fsharp-app/data"

mkdir -p ${BACKUP_DIR}

# Backup data directory
tar -czf ${BACKUP_DIR}/data_${TIMESTAMP}.tar.gz -C ${DATA_DIR} .

# Keep only last 7 days
find ${BACKUP_DIR} -name "data_*.tar.gz" -mtime +7 -delete

echo "Backup complete: data_${TIMESTAMP}.tar.gz"
```

Add to crontab:
```bash
# Daily backup at 2 AM
0 2 * * * /opt/my-fsharp-app/backup.sh >> /var/log/my-fsharp-app-backup.log 2>&1
```

### Restore from Backup

```bash
#!/bin/bash
BACKUP_FILE=$1
DATA_DIR="/opt/my-fsharp-app/data"

# Stop application
docker-compose down

# Restore data
tar -xzf ${BACKUP_FILE} -C ${DATA_DIR}

# Start application
docker-compose up -d

echo "Restore complete from ${BACKUP_FILE}"
```

## Monitoring

### Health Checks

Built into Docker Compose:
```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
  interval: 30s
  timeout: 3s
  retries: 3
```

Check health status:
```bash
docker inspect --format='{{.State.Health.Status}}' my-fsharp-app
```

### Logs

```bash
# View logs
docker logs my-fsharp-app

# Follow logs
docker logs -f my-fsharp-app

# Last 100 lines
docker logs --tail 100 my-fsharp-app

# Since timestamp
docker logs --since 2024-01-01T00:00:00 my-fsharp-app
```

### Resource Usage

```bash
# Container stats
docker stats my-fsharp-app

# Disk usage
docker system df
```

## Environment-Specific Builds

### Development

```bash
docker build --target build -t my-app:dev .
```

### Production

```bash
docker build -t my-app:prod .
```

### With Build Args

```dockerfile
# In Dockerfile
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c ${BUILD_CONFIGURATION} -o /app/publish
```

```bash
# Development build
docker build --build-arg BUILD_CONFIGURATION=Debug -t my-app:dev .

# Production build
docker build --build-arg BUILD_CONFIGURATION=Release -t my-app:prod .
```

## Optimizations

### Multi-Architecture Builds

```bash
# Build for multiple architectures
docker buildx build --platform linux/amd64,linux/arm64 -t my-app:latest .
```

### Layer Caching

Order Dockerfile commands from least to most frequently changing:

```dockerfile
# ✅ Good: Dependencies cached separately
COPY package*.json ./
RUN npm install

COPY *.fsproj ./
RUN dotnet restore

COPY . .
RUN dotnet build

# ❌ Bad: Changes to source invalidate all layers
COPY . .
RUN npm install
RUN dotnet restore
RUN dotnet build
```

### Smaller Images

```dockerfile
# Use Alpine-based images for smaller size
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Remove unnecessary files
RUN rm -rf /app/obj /app/bin/Debug
```

## Troubleshooting

### Build Failures

```bash
# Clean build
docker build --no-cache -t my-app:latest .

# Check build logs
docker build -t my-app:latest . 2>&1 | tee build.log
```

### Runtime Issues

```bash
# Interactive shell
docker exec -it my-fsharp-app /bin/bash

# Check environment
docker exec my-fsharp-app env

# Check processes
docker exec my-fsharp-app ps aux
```

### Port Conflicts

```bash
# Check what's using port 5000
sudo lsof -i :5000

# Use different port
docker run -p 5001:5000 my-app:latest
```

### Permission Issues

```bash
# Fix data directory permissions
sudo chown -R 1000:1000 ./data

# Run as specific user
docker run --user 1000:1000 my-app:latest
```

## Update Strategy

### Rolling Update (Zero Downtime)

1. Build new version: `docker build -t my-app:v2 .`
2. Start new container: `docker run -d --name my-app-v2 -p 5001:5000 my-app:v2`
3. Test new version: `curl http://localhost:5001/health`
4. Update reverse proxy to point to new container
5. Stop old container: `docker stop my-app-v1`
6. Remove old container: `docker rm my-app-v1`

### Blue-Green Deployment

Maintain two identical environments:
- Blue (current production)
- Green (new version)

Switch traffic between them instantly.

## Best Practices

1. **Use multi-stage builds**: Smaller final images
2. **Version your images**: Don't rely on `latest`
3. **Persist data in volumes**: Never in containers
4. **Health checks**: Always include health endpoints
5. **Logging**: Use structured logging to stdout
6. **Secrets**: Use environment variables or secret management
7. **Backups**: Automate database and data backups
8. **Monitoring**: Set up alerts for failures
9. **Resource limits**: Set memory and CPU limits in docker-compose
10. **Documentation**: Keep deployment docs up to date

## Next Steps

- Read `08-TAILSCALE-INTEGRATION.md` for networking setup
- Set up automated backups
- Configure monitoring and alerting
- Test disaster recovery procedures
