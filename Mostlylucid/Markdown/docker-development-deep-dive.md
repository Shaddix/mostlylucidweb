# Docker Development Deep Dive: From Basics to Advanced .NET Containerization

<datetime class="hidden">2025-01-09T14:00</datetime>
<!--category-- Docker, .NET, DevOps, Containers -->

## Introduction

Docker has fundamentally transformed how we build, ship, and run applications. What started as a simple containerization tool has evolved into a complete ecosystem for modern application development and deployment. In this comprehensive guide, we'll explore Docker from the ground up, dive into Docker Compose orchestration, tackle advanced topics like GPU support and multi-architecture builds, and examine the exciting new container features in .NET 9.

Whether you're deploying a simple web app or orchestrating a complex microservices architecture with GPU-accelerated machine learning models, this guide will take you from Docker basics to production-ready containerized applications.

[TOC]

## Docker Fundamentals: Understanding Containers

### What is Docker, Really?

At its core, Docker is a containerization platform that packages your application and all its dependencies into a standardized unit called a container. Unlike virtual machines that virtualize entire operating systems, containers share the host OS kernel while maintaining isolated user spaces.

Think of it this way:
- **Virtual Machines**: Each VM runs a full OS stack (Linux kernel, system libraries, etc.) - heavy and slow to start
- **Containers**: Share the host kernel, package only application code and dependencies - lightweight and fast

### Why Containers Matter for Developers

```bash
# The classic developer problem
"It works on my machine!" 🤷‍♂️

# The container solution
"Ship your machine!" 📦
```

Containers solve several critical problems:

1. **Environment Consistency**: Development, testing, and production environments are identical
2. **Dependency Isolation**: No more "DLL hell" or conflicting library versions
3. **Reproducible Builds**: Same input = same output, every time
4. **Fast Deployment**: Start containers in seconds, not minutes
5. **Resource Efficiency**: Run dozens of containers on a single host

### Essential Docker Concepts

#### Images vs Containers

```bash
# An image is a template (like a class in OOP)
docker pull mcr.microsoft.com/dotnet/aspnet:9.0

# A container is a running instance (like an object)
docker run -d -p 8080:8080 myapp:latest
```

**Images** are immutable, layered filesystems. Each instruction in a Dockerfile creates a new layer:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0      # Layer 1: Base OS + .NET runtime
WORKDIR /app                                   # Layer 2: Directory structure
COPY *.dll ./                                  # Layer 3: Application files
ENTRYPOINT ["dotnet", "MyApp.dll"]            # Layer 4: Startup command
```

This layering is powerful:
- **Caching**: Unchanged layers are reused, speeding up builds
- **Sharing**: Multiple images can share base layers
- **Efficiency**: Only changed layers need to be downloaded/uploaded

#### Dockerfile Best Practices

Here's a production-ready .NET Dockerfile with commentary:

```dockerfile
# Multi-stage build: separates build environment from runtime
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only csproj files first (better layer caching)
COPY ["MyApp/MyApp.csproj", "MyApp/"]
COPY ["MyApp.Core/MyApp.Core.csproj", "MyApp.Core/"]

# Restore dependencies (cached unless csproj changes)
RUN dotnet restore "MyApp/MyApp.csproj"

# Copy everything else
COPY . .

# Build the application
WORKDIR "/src/MyApp"
RUN dotnet build "MyApp.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "MyApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create non-root user for security
RUN addgroup --gid 1001 appuser && \
    adduser --uid 1001 --gid 1001 --disabled-password --gecos "" appuser

# Copy published output from publish stage
COPY --from=publish /app/publish .

# Switch to non-root user
USER appuser

# Expose port (documentation only, doesn't actually publish)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "MyApp.dll"]
```

**Key principles illustrated:**

1. **Multi-stage builds**: Separate build/publish stages reduce final image size dramatically
2. **Layer optimization**: Copy dependency files before source code for better caching
3. **Security**: Run as non-root user
4. **Health checks**: Container orchestrators can monitor application health
5. **Explicit environment**: Set production defaults

#### Building and Running

```bash
# Build the image
docker build -t myapp:1.0.0 -t myapp:latest .

# Run with common options
docker run -d \
  --name myapp \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=db;Database=myapp" \
  -v /data/logs:/app/logs \
  --restart unless-stopped \
  myapp:latest

# View logs
docker logs -f myapp

# Execute commands inside running container
docker exec -it myapp /bin/bash

# Stop and remove
docker stop myapp
docker rm myapp
```

## Docker Compose: Multi-Container Orchestration

Docker Compose allows you to define and run multi-container applications. Instead of managing containers individually, you describe your entire application stack in a YAML file.

### Why Docker Compose?

Consider a typical .NET web application:
- ASP.NET Core web app
- PostgreSQL database
- Redis cache
- Seq for logging
- Maybe a background worker service

Managing these individually with `docker run` commands becomes unwieldy. Docker Compose solves this.

### Complete Example: .NET Blog Platform

Here's a real-world `docker-compose.yml` for a blog platform (similar to this site):

```yaml
version: '3.8'

services:
  # Main web application
  web:
    build:
      context: .
      dockerfile: Mostlylucid/Dockerfile
    image: mostlylucid:latest
    container_name: mostlylucid-web
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Database=mostlylucid;Username=postgres;Password=${DB_PASSWORD}
      - Redis__ConnectionString=redis:6379
      - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_started
    volumes:
      - ./data/markdown:/app/Markdown
      - ./data/wwwroot:/app/wwwroot
    restart: unless-stopped
    networks:
      - backend
      - frontend
    labels:
      - "com.centurylinklabs.watchtower.enable=true"

  # PostgreSQL database
  db:
    image: postgres:16-alpine
    container_name: mostlylucid-db
    environment:
      - POSTGRES_DB=mostlylucid
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - PGDATA=/var/lib/postgresql/data/pgdata
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    restart: unless-stopped
    networks:
      - backend
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Redis cache
  redis:
    image: redis:7-alpine
    container_name: mostlylucid-redis
    command: redis-server --appendonly yes
    volumes:
      - redis_data:/data
    ports:
      - "6379:6379"
    restart: unless-stopped
    networks:
      - backend
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5

  # Seq logging
  seq:
    image: datalust/seq:latest
    container_name: mostlylucid-seq
    environment:
      - ACCEPT_EULA=Y
    volumes:
      - seq_data:/data
    ports:
      - "5341:80"
    restart: unless-stopped
    networks:
      - backend

  # Background worker (Hangfire)
  worker:
    build:
      context: .
      dockerfile: Mostlylucid.SchedulerService/Dockerfile
    image: mostlylucid-worker:latest
    container_name: mostlylucid-worker
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Database=mostlylucid;Username=postgres;Password=${DB_PASSWORD}
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - backend

  # Watchtower for automatic updates
  watchtower:
    image: containrrr/watchtower
    container_name: watchtower
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - WATCHTOWER_CLEANUP=true
      - WATCHTOWER_LABEL_ENABLE=true
      - WATCHTOWER_INCLUDE_RESTARTING=true
    command: --interval 300
    restart: unless-stopped

volumes:
  postgres_data:
    driver: local
  redis_data:
    driver: local
  seq_data:
    driver: local

networks:
  frontend:
    driver: bridge
  backend:
    driver: bridge
```

### Docker Compose Key Features

#### Service Dependencies

```yaml
services:
  web:
    depends_on:
      db:
        condition: service_healthy  # Wait for health check
      redis:
        condition: service_started  # Just wait for start
```

Docker Compose orchestrates startup order. The `condition: service_healthy` requires the database health check to pass before starting the web app.

#### Environment Variables and Secrets

```bash
# .env file (never commit to git!)
DB_PASSWORD=super_secret_password
SMTP_PASSWORD=another_secret
```

```yaml
services:
  web:
    environment:
      - DB_PASSWORD=${DB_PASSWORD}  # From .env file
      - STATIC_VALUE=production     # Hardcoded
    env_file:
      - .env                        # Load entire file
```

For production secrets, use Docker Secrets or external secret managers (AWS Secrets Manager, Azure Key Vault, HashiCorp Vault).

#### Named Volumes vs Bind Mounts

```yaml
services:
  db:
    volumes:
      # Named volume (managed by Docker)
      - postgres_data:/var/lib/postgresql/data

  web:
    volumes:
      # Bind mount (maps host directory to container)
      - ./data/markdown:/app/Markdown
      - ./logs:/app/logs
```

**Named volumes**:
- Managed by Docker
- Persist data across container restarts
- Can be backed up/restored with Docker commands
- Cross-platform compatible

**Bind mounts**:
- Direct mapping to host filesystem
- Useful for development (live code reloading)
- Configuration files, logs, uploads

#### Networking

```yaml
networks:
  frontend:
    driver: bridge
  backend:
    driver: bridge

services:
  web:
    networks:
      - frontend
      - backend

  db:
    networks:
      - backend  # Not exposed to frontend
```

Networks provide isolation. Here, the database is only accessible to backend services, not directly exposed.

#### Health Checks

```yaml
services:
  db:
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
```

Health checks allow Docker to:
- Determine if a container is actually ready (not just started)
- Restart unhealthy containers
- Provide status to orchestrators (Kubernetes, Docker Swarm)

### Common Docker Compose Commands

```bash
# Start all services (detached)
docker-compose up -d

# Start specific services
docker-compose up -d web db

# View logs (all services)
docker-compose logs -f

# View logs (specific service)
docker-compose logs -f web

# Stop services (containers remain)
docker-compose stop

# Stop and remove containers
docker-compose down

# Stop, remove containers, and remove volumes
docker-compose down -v

# Rebuild and restart
docker-compose up -d --build

# Scale a service
docker-compose up -d --scale worker=3

# Execute command in running service
docker-compose exec web /bin/bash

# Run one-off command
docker-compose run --rm web dotnet ef database update
```

### Development vs Production Compose Files

Separate concerns with multiple compose files:

**docker-compose.yml** (base):
```yaml
services:
  web:
    build: .
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
```

**docker-compose.override.yml** (development - auto-merged):
```yaml
services:
  web:
    volumes:
      - .:/app  # Live code reloading
    ports:
      - "5000:8080"
```

**docker-compose.prod.yml** (production):
```yaml
services:
  web:
    image: registry.example.com/myapp:${VERSION}
    restart: always
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 2G
```

```bash
# Development (base + override)
docker-compose up -d

# Production
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

## GPU Support in Docker: Accelerating ML Workloads

Machine learning, scientific computing, and video processing applications often require GPU acceleration. Docker supports NVIDIA GPUs through the NVIDIA Container Toolkit.

### Setting Up NVIDIA Container Toolkit

```bash
# Install NVIDIA Container Toolkit (Ubuntu/Debian)
distribution=$(. /etc/os-release;echo $ID$VERSION_ID)
curl -s -L https://nvidia.github.io/libnvidia-container/gpgkey | sudo apt-key add -
curl -s -L https://nvidia.github.io/libnvidia-container/$distribution/libnvidia-container.list | \
  sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit

# Configure Docker daemon
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker

# Test GPU access
docker run --rm --gpus all nvidia/cuda:12.6.0-base-ubuntu24.04 nvidia-smi
```

### Dockerfile for GPU-Accelerated Python/PyTorch Application

```dockerfile
# Use NVIDIA CUDA base image
FROM nvidia/cuda:12.6.0-cudnn-runtime-ubuntu24.04

# Install Python
RUN apt-get update && apt-get install -y \
    python3.12 \
    python3-pip \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Install PyTorch with CUDA support
COPY requirements.txt .
RUN pip3 install --no-cache-dir torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124

# Copy application
COPY . .

# Test GPU on container start
RUN python3 -c "import torch; print(f'CUDA available: {torch.cuda.is_available()}'); print(f'GPU: {torch.cuda.get_device_name(0) if torch.cuda.is_available() else \"None\"}')"

ENTRYPOINT ["python3", "train.py"]
```

### Running GPU Containers

```bash
# Run with all GPUs
docker run --gpus all myapp:gpu

# Run with specific GPUs
docker run --gpus '"device=0,2"' myapp:gpu

# Run with GPU memory limits
docker run --gpus all --memory=16g myapp:gpu
```

### GPU in Docker Compose

```yaml
services:
  ml-trainer:
    build:
      context: .
      dockerfile: Dockerfile.gpu
    image: myapp:gpu
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all  # or specific count: 1, 2, etc.
              capabilities: [gpu]
    volumes:
      - ./models:/app/models
      - ./data:/app/data
    environment:
      - NVIDIA_VISIBLE_DEVICES=all
      - CUDA_VISIBLE_DEVICES=0,1  # Use GPUs 0 and 1
```

### Real-World Example: Translation Service with GPU and CPU Builds

Here's a real production example from [mostlylucid-nmt](https://github.com/scottgal/mostlylucid-nmt), a neural machine translation service I built that powers auto-translation on this blog.

The project demonstrates:
- **GPU and CPU variants** - Same codebase, different base images
- **Multi-architecture builds** - Supports AMD64 and ARM64
- **Optimized Docker images** - Both full and minimal variants
- **Production-ready** - Health checks, volume persistence, proper logging

#### GPU-Accelerated Translation Service

```yaml
services:
  translation:
    image: scottgal/mostlylucid-nmt:gpu
    container_name: translation-gpu
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    environment:
      - MODEL_FAMILY=opus-mt
      - FALLBACK_MODELS=mbart50,m2m100
      - CUDA_VISIBLE_DEVICES=0
      - LOG_LEVEL=info
    volumes:
      - model_cache:/app/cache  # Persistent model storage
    ports:
      - "8888:8888"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8888/health"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  model_cache:
```

#### CPU-Only Alternative

For environments without GPUs, the same service runs on CPU:

```yaml
services:
  translation:
    image: scottgal/mostlylucid-nmt:cpu
    container_name: translation-cpu
    environment:
      - MODEL_FAMILY=opus-mt
      - FALLBACK_MODELS=mbart50,m2m100
    volumes:
      - model_cache:/app/cache
    ports:
      - "8888:8888"
    restart: unless-stopped

volumes:
  model_cache:
```

**Available image variants:**
- `scottgal/mostlylucid-nmt:gpu` - CUDA 12.6 with PyTorch GPU support (~5GB)
- `scottgal/mostlylucid-nmt:cpu` - CPU-only, smaller footprint (~2.5GB)
- `scottgal/mostlylucid-nmt:gpu-min` - Minimal GPU build, no preloaded models (~4GB)
- `scottgal/mostlylucid-nmt:cpu-min` - Minimal CPU build (~1.5GB)

**Key features:**
- **GPU Acceleration**: 10-15x faster translation with CUDA
- **Model Auto-Download**: Downloads translation models on-demand
- **Fallback Support**: Tries Opus-MT → mBART50 → M2M100 for maximum language coverage
- **Volume Persistence**: Cache models across container restarts
- **Health Endpoints**: `/health` and `/ready` for orchestrators
- **Multi-Architecture**: Runs on x86_64 and ARM64 (Apple Silicon, Raspberry Pi)

See the [full project on GitHub](https://github.com/scottgal/mostlylucid-nmt) for Dockerfile examples, build scripts, and API documentation.

## Multi-Architecture Builds with Docker Buildx

Modern applications need to run on multiple architectures: x86_64 (AMD64) for servers, ARM64 for Raspberry Pi and Apple Silicon Macs, sometimes even ARM32 for embedded devices.

### Why Multi-Architecture Matters

```bash
# Problem: Image built on M1 Mac won't run on Linux server
docker build -t myapp:latest .  # Builds for ARM64
docker push myapp:latest
# Server tries to run it... error: "exec format error"

# Solution: Build for multiple platforms
docker buildx build --platform linux/amd64,linux/arm64 -t myapp:latest --push .
```

### Setting Up Buildx

Docker Buildx is included in Docker Desktop. For Linux:

```bash
# Verify buildx is available
docker buildx version

# Create a new builder instance
docker buildx create --name multiarch --driver docker-container --use

# Inspect and bootstrap the builder
docker buildx inspect --bootstrap

# List available platforms
docker buildx inspect | grep Platforms
```

### Multi-Architecture Dockerfile

Most Dockerfiles work without changes, but here are some tips:

```dockerfile
# Use official multi-arch base images
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# For platform-specific operations, use build arguments
ARG TARGETPLATFORM
ARG BUILDPLATFORM

RUN echo "Building on $BUILDPLATFORM for $TARGETPLATFORM"

# Install architecture-specific dependencies
RUN if [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
        apt-get update && apt-get install -y some-arm64-package; \
    elif [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
        apt-get update && apt-get install -y some-amd64-package; \
    fi
```

### Building for Multiple Platforms

```bash
# Build and push for AMD64 and ARM64
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t myregistry/myapp:latest \
  -t myregistry/myapp:1.0.0 \
  --push \
  .

# Build without pushing (loads into local Docker)
# Note: Can only load one platform at a time
docker buildx build \
  --platform linux/amd64 \
  -t myapp:latest \
  --load \
  .

# Build and export to tar files
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t myapp:latest \
  -o type=tar,dest=./myapp.tar \
  .
```

### Multi-Architecture with Docker Compose

Unfortunately, Docker Compose doesn't directly support buildx. Workarounds:

**Option 1: Pre-build images**
```bash
# Build multi-arch images first
docker buildx build --platform linux/amd64,linux/arm64 -t myapp:latest --push .

# Then use in compose
```
```yaml
services:
  web:
    image: myapp:latest  # Already built for multiple architectures
```

**Option 2: Build script**
```bash
#!/bin/bash
# build-multiarch.sh

docker buildx build --platform linux/amd64,linux/arm64 \
  -t myregistry/web:latest \
  -f web/Dockerfile \
  --push \
  web/

docker buildx build --platform linux/amd64,linux/arm64 \
  -t myregistry/worker:latest \
  -f worker/Dockerfile \
  --push \
  worker/

docker-compose pull  # Pull the multi-arch images
docker-compose up -d
```

### Real-World Example: CI/CD Pipeline

GitHub Actions workflow for multi-architecture builds:

```yaml
name: Build and Push Multi-Arch Images

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Set up QEMU
        uses: docker/setup-qemu-action@v3

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Login to Docker Hub
        uses: docker/login-action@v3
        with:
          username: ${{ secrets.DOCKERHUB_USERNAME }}
          password: ${{ secrets.DOCKERHUB_TOKEN }}

      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: myregistry/myapp
          tags: |
            type=ref,event=branch
            type=semver,pattern={{version}}
            type=semver,pattern={{major}}.{{minor}}
            type=sha,prefix={{branch}}-

      - name: Build and push
        uses: docker/build-push-action@v5
        with:
          context: .
          platforms: linux/amd64,linux/arm64
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=registry,ref=myregistry/myapp:buildcache
          cache-to: type=registry,ref=myregistry/myapp:buildcache,mode=max
```

This workflow:
- Triggers on pushes to main or version tags
- Sets up QEMU for cross-platform emulation
- Builds for AMD64 and ARM64
- Generates tags automatically (branch name, semantic versions, SHA)
- Uses registry caching for faster builds

## .NET 9 Container Improvements

.NET 9 introduces significant improvements to container support, making it easier than ever to containerize .NET applications without even writing a Dockerfile.

### Built-in Container Publishing

With .NET 9, you can publish a containerized application directly:

```bash
# Publish as a container image (no Dockerfile needed!)
dotnet publish --os linux --arch x64 -p:PublishProfile=DefaultContainer

# Specify image name and tag
dotnet publish \
  --os linux \
  --arch x64 \
  -p:PublishProfile=DefaultContainer \
  -p:ContainerImageName=myapp \
  -p:ContainerImageTag=1.0.0

# Multi-architecture
dotnet publish --os linux --arch arm64 -p:PublishProfile=DefaultContainer
```

### Configuring Container Properties

Add to your `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>

    <!-- Container Configuration -->
    <ContainerImageName>myapp</ContainerImageName>
    <ContainerImageTag>$(Version)</ContainerImageTag>
    <ContainerRegistry>myregistry.azurecr.io</ContainerRegistry>

    <!-- Base image (defaults to mcr.microsoft.com/dotnet/aspnet:9.0) -->
    <ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:9.0-alpine</ContainerBaseImage>

    <!-- Container runtime configuration -->
    <ContainerWorkingDirectory>/app</ContainerWorkingDirectory>
    <ContainerPort>8080</ContainerPort>
    <ContainerEnvironmentVariable Include="ASPNETCORE_ENVIRONMENT">Production</ContainerEnvironmentVariable>

    <!-- User (security best practice) -->
    <ContainerUser>app</ContainerUser>

    <!-- Labels -->
    <ContainerLabel Include="org.opencontainers.image.description">My awesome app</ContainerLabel>
    <ContainerLabel Include="org.opencontainers.image.source">https://github.com/me/myapp</ContainerLabel>
  </PropertyGroup>
</Project>
```

Then publish:

```bash
dotnet publish -p:PublishProfile=DefaultContainer
```

### Advantages of Built-in Container Support

1. **No Dockerfile needed**: Reduces complexity
2. **Optimized images**: Microsoft tunes the images for performance
3. **Consistent**: Standard images across all .NET apps
4. **Security**: Automated base image updates
5. **Smaller images**: Trimming and AOT compilation support

### Trimming and AOT for Smaller Images

```xml
<PropertyGroup>
  <!-- Enable trimming to remove unused code -->
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>full</TrimMode>

  <!-- OR use Native AOT for even smaller, faster images -->
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

Results:
- **Standard**: ~220MB
- **With trimming**: ~110MB
- **With AOT**: ~12MB for simple apps!

### Comparison: Dockerfile vs Built-in

**Traditional Dockerfile approach:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["MyApp.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

**Built-in container approach:**
```bash
# Just this!
dotnet publish -p:PublishProfile=DefaultContainer
```

Both produce similar images, but the built-in approach is simpler and more maintainable.

### When to Use Dockerfile vs Built-in

**Use Dockerfile when:**
- You need full control over the image
- Installing system dependencies
- Complex multi-stage builds
- Non-standard base images

**Use built-in when:**
- Standard .NET web/console apps
- You want simplicity
- Leveraging Microsoft optimizations
- Quick prototyping

### Chiseled Ubuntu Images

.NET 9 supports "chiseled" Ubuntu images - ultra-minimal base images:

```xml
<PropertyGroup>
  <ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled</ContainerBaseImage>
</PropertyGroup>
```

Benefits:
- **Tiny**: ~50% smaller than regular images
- **Secure**: Minimal attack surface (no package manager, shell)
- **Fast**: Faster pulls and starts

Trade-offs:
- No shell (harder to debug)
- Limited system utilities

Perfect for production where security and size matter most.

## Introduction to .NET Aspire

.NET Aspire is an opinionated, cloud-ready stack for building distributed applications. Think of it as Docker Compose on steroids, specifically designed for .NET microservices.

### What is Aspire?

Aspire provides:
1. **Orchestration**: Run and connect multiple services locally
2. **Service Discovery**: Services find each other automatically
3. **Telemetry**: Built-in logging, metrics, tracing
4. **Components**: Pre-configured integrations (Redis, PostgreSQL, RabbitMQ, etc.)
5. **Deployment**: Generate Kubernetes/Docker Compose manifests

### Aspire Architecture

An Aspire solution has three project types:

1. **App Host**: Orchestrates your services
2. **Service Projects**: Your actual services (Web APIs, workers, etc.)
3. **Service Defaults**: Shared configuration (logging, health checks, etc.)

### Quick Example

**1. Create Aspire project:**
```bash
dotnet new aspire-starter -n MyDistributedApp
cd MyDistributedApp
```

**2. App Host (MyDistributedApp.AppHost/Program.cs):**
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Redis cache
var cache = builder.AddRedis("cache");

// Add PostgreSQL database
var db = builder.AddPostgres("postgres")
               .AddDatabase("mydb");

// Add API service (references cache and db)
var api = builder.AddProject<Projects.MyApi>("api")
                 .WithReference(cache)
                 .WithReference(db);

// Add frontend (references API)
builder.AddProject<Projects.MyWeb>("web")
       .WithReference(api);

builder.Build().Run();
```

**3. Use in your service:**
```csharp
// MyApi/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Aspire automatically configures these based on AppHost
builder.AddServiceDefaults();
builder.AddRedisClient("cache");
builder.AddNpgsqlDbContext<MyDbContext>("mydb");

var app = builder.Build();
app.MapDefaultEndpoints();  // Health, metrics, etc.
```

**4. Run everything:**
```bash
dotnet run --project MyDistributedApp.AppHost
```

Aspire launches:
- Dashboard at http://localhost:15888
- All services with proper configuration
- Redis and PostgreSQL in containers
- Distributed tracing across services

### Aspire vs Docker Compose

**Docker Compose:**
- Language-agnostic
- Infrastructure-focused
- Manual service discovery
- Bring your own observability

**Aspire:**
- .NET-specific
- Development-focused
- Automatic service discovery
- Built-in telemetry
- Generates deployment manifests

**Use both**: Aspire for local development, generates Docker Compose/Kubernetes for production.

### Deploying Aspire Apps

Generate deployment manifests:

```bash
# Generate Docker Compose
dotnet run --project MyDistributedApp.AppHost -- \
  --publisher compose \
  --output-path ../deploy

# Generate Kubernetes manifests
dotnet run --project MyDistributedApp.AppHost -- \
  --publisher manifest \
  --output-path ../deploy/k8s
```

Then deploy:
```bash
# Docker Compose
docker-compose -f deploy/docker-compose.yml up -d

# Kubernetes
kubectl apply -f deploy/k8s/
```

### Aspire Components

Pre-built integrations make adding services trivial:

```csharp
// Add various backing services
var redis = builder.AddRedis("cache");
var postgres = builder.AddPostgres("db").AddDatabase("mydb");
var rabbitmq = builder.AddRabbitMQ("messaging");
var mongodb = builder.AddMongoDB("mongo").AddDatabase("docs");
var sql = builder.AddSqlServer("sql").AddDatabase("business");

// Add Azure services
var storage = builder.AddAzureStorage("storage");
var cosmos = builder.AddAzureCosmosDB("cosmos");
var servicebus = builder.AddAzureServiceBus("messaging");

// Use in services
builder.AddProject<Projects.MyService>("service")
       .WithReference(redis)
       .WithReference(postgres)
       .WithReference(rabbitmq);
```

## Best Practices Summary

### Dockerfile Best Practices
1. ✅ Use multi-stage builds
2. ✅ Run as non-root user
3. ✅ Order instructions for optimal caching
4. ✅ Use specific base image tags (not `latest`)
5. ✅ Include health checks
6. ✅ Use `.dockerignore` to exclude unnecessary files
7. ✅ Minimize layers (combine RUN commands)
8. ✅ Use build arguments for flexibility

### Docker Compose Best Practices
1. ✅ Use named volumes for data
2. ✅ Implement health checks
3. ✅ Use `.env` for secrets (never commit)
4. ✅ Define explicit networks
5. ✅ Use `depends_on` with health conditions
6. ✅ Set restart policies
7. ✅ Separate dev/prod configurations
8. ✅ Resource limits in production

### Security Best Practices
1. ✅ Run as non-root user
2. ✅ Use chiseled/minimal base images
3. ✅ Scan images for vulnerabilities
4. ✅ Keep base images updated
5. ✅ Don't include secrets in images
6. ✅ Use read-only filesystems where possible
7. ✅ Limit container capabilities
8. ✅ Use Docker secrets or external secret managers

### Performance Best Practices
1. ✅ Use BuildKit for faster builds
2. ✅ Leverage layer caching
3. ✅ Multi-stage builds to reduce image size
4. ✅ Use `.dockerignore` generously
5. ✅ Alpine/chiseled images for smaller size
6. ✅ Use volume mounts for development
7. ✅ Configure appropriate resource limits
8. ✅ Use health checks for orchestration

## Troubleshooting Common Issues

### Container Won't Start

```bash
# Check logs
docker logs container-name

# Common issues:
# 1. Port already in use
docker ps | grep 8080  # Find conflicting container
docker stop conflicting-container

# 2. Missing environment variables
docker inspect container-name | grep Env

# 3. Failed health check
docker inspect container-name | grep Health -A 20
```

### Slow Builds

```bash
# Enable BuildKit for faster builds
export DOCKER_BUILDKIT=1

# Use build cache
docker build --cache-from myapp:latest -t myapp:latest .

# Check what's taking time
docker build --progress=plain -t myapp:latest .
```

### Networking Issues

```bash
# Containers can't communicate
# Solution: Ensure they're on the same network
docker network ls
docker network inspect network-name

# DNS not working
# Container names are DNS names within Docker networks
docker exec web ping db  # Should work if both on same network
```

### Volume Permission Issues

```bash
# Permission denied on volume
# Solution: Match user IDs
FROM ubuntu
RUN useradd -u 1000 appuser  # Match host user ID
USER appuser
```

## Conclusion

Docker has evolved from a simple containerization tool to a comprehensive platform for building, shipping, and running modern applications. Whether you're:

- **Getting Started**: Master Dockerfile basics and container fundamentals
- **Building Apps**: Leverage Docker Compose for multi-service orchestration
- **Accelerating ML**: Harness GPU support for compute-intensive workloads
- **Going Multi-Platform**: Use buildx for ARM and AMD64 compatibility
- **Simplifying .NET**: Embrace built-in container publishing
- **Building Microservices**: Explore .NET Aspire for distributed applications

The container ecosystem provides tools for every scenario. Start simple, iterate, and gradually adopt advanced features as your needs grow.

### Further Resources

- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Reference](https://docs.docker.com/compose/compose-file/)
- [.NET Container Images](https://github.com/dotnet/dotnet-docker)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [NVIDIA Container Toolkit](https://github.com/NVIDIA/nvidia-container-toolkit)
- [Docker Buildx](https://github.com/docker/buildx)

Happy containerizing! 🐳
