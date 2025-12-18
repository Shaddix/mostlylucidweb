# Document Summary: docker-development-deep-dive.md

*Generated: 2025-12-18 17:04:40*

## Executive Summary

This article provides a comprehensive guide to Docker and its various tools and techniques for building, shipping, and running modern applications. The guide covers the basics of Docker, including creating images, running containers, and using Docker Compose for multi-container applications.

The article also discusses advanced topics, such as:

1. **Multi-stage builds**: Using multiple stages to build images, reducing image size and improving performance.
2. **Layer caching**: Reusing cached layers during builds to speed up the process.
3. **Resource optimization techniques**: Limiting CPU and memory resources for individual containers to prevent resource starvation.

Additionally, the article covers .NET-specific Docker practices, including:

1. **Built-in container publishing**: Using .NET 9's built-in feature to publish a container image without writing a Dockerfile.
2. **Chiseled Ubuntu images**: Using minimal base images to reduce attack surface and improve performance.
3. **.NET Aspire**: A comprehensive platform for building, shipping, and running modern .NET applications.

The article also provides real-world examples from the author's own projects, including:

1. **mostlylucid.net**: A production blog with a custom Docker setup using Docker Compose and .NET Aspire.
2. **mostlylucid-nmt**: A GPU/CPU translation service with multi-arch builds using Docker Buildx.

Finally, the article concludes by highlighting key takeaways from the guide, including:

1. **Start simple**: Begin with basic Docker concepts and build complexity as needed.
2. **Measure and optimize**: Identify bottlenecks in your application and optimize accordingly.
3. **Use Docker and .NET Aspire**: Leverage these tools to improve performance, security, and development efficiency.

Overall, this article provides a comprehensive guide to Docker and its various tools and techniques for building, shipping, and running modern applications.

## Topic Summaries

### Docker Development Deep Dive: From Basics to Advanced .NET Containerization

*Sources: chunk-0*

<datetime class="hidden">2025-11-09T14:00</datetime>
<!--category-- Docker, .NET, DevOps, Containers -->

## Introduction

Docker has fundamentally transformed how we build, ship, and run appli...

### You might think: "But I'm on Windows, how can I use these Linux commands?"

*Sources: chunk-1*

RUN apt-get update  # ← Executes in the Linux build container, not your Windows machine
RUN dotnet restore  # ← Executes in the Linux build container
```

The Docker daemon handles the transla...

### Docker Compose: Multi-Container Orchestration

*Sources: chunk-2*

Docker Compose allows you to define and run multi-container applications. Instead of managing containers individually, you describe your entire application stack in a YAML file.

### Why Docker Co...

### .env file (never commit to git!)

*Sources: chunk-3*

DB_PASSWORD=super_secret_password
SMTP_PASSWORD=another_secret
```

```yaml
services:
  web:
    environment:
      - DB_PASSWORD=${DB_PASSWORD}  # From .env file
      - STATIC_VALU...

### Run with GPU memory limits

*Sources: chunk-4*

docker run --gpus all --memory=16g myapp:gpu
```

### GPU in Docker Compose

```yaml
services:
  ml-trainer:
    build:
      context: .
      dockerfile: Dockerfile.gpu
    imag...

### Then use in compose

*Sources: chunk-5*

```
```yaml
services:
  web:
    image: myapp:latest  # Already built for multiple architectures
```

**Option 2: Build script**
```bash
#!/bin/bash

## build-multiarch.sh

dock...

### CPU-optimized PyTorch (smaller, no CUDA)

*Sources: chunk-6*

RUN pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cpu

## CPU performance tuning

ENV OMP_NUM_THREADS=4 \
    MKL_NUM_THREADS=4 \
    EASYNMT_BATCH_SIZ...

### Models preloaded, instant translation

*Sources: chunk-7*

## No external dependencies

```

**Scenario 3: Production GPU (high throughput)**
```yaml

## docker-compose.yml

services:
  translation:
    image: scottgal/mostlylucid-nmt:gpu
...

### Introduction to .NET Aspire

*Sources: chunk-8*

.NET Aspire is an opinionated, cloud-ready stack for building distributed applications. Think of it as Docker Compose on steroids, specifically designed for .NET microservices.

### What is Aspire...

### Self-Hosting on Limited Resources: Practical Optimization

*Sources: chunk-9*

Running a full observability stack like the one above requires significant resources. If you're self-hosting on a VPS with 4GB RAM or an old laptop, here are practical strategies to reduce resource co...

## Processing Trace

| Metric | Value |
|--------|-------|
| Document | docker_development_deep_dive |
| Chunks | 14 total, 14 processed |
| Topics | 14 |
| Time | 70.6s |
| Coverage | 100% |
| Citation rate | 0.00 |
