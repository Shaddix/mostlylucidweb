#!/bin/bash
#
# Docker Registry Deployment Script
#
# This script helps deploy the Docker registry on a fresh server.
# Run this after transferring files via scp.

set -e

echo "========================================"
echo "Docker Registry Deployment Script"
echo "========================================"
echo ""

# Check if .env exists
if [ ! -f .env ]; then
    echo "Error: .env file not found!"
    echo "Please copy .env.example to .env and configure it."
    echo ""
    echo "  cp .env.example .env"
    echo "  nano .env"
    echo ""
    exit 1
fi

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "Error: Docker is not installed!"
    echo "Please install Docker first:"
    echo "  curl -fsSL https://get.docker.com | sh"
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    echo "Error: Docker Compose is not installed!"
    echo "Please install Docker Compose first."
    exit 1
fi

# Create data directories
echo "Creating data directories..."
mkdir -p data/registry
mkdir -p data/caddy

# Pull images
echo ""
echo "Pulling Docker images..."
docker-compose pull

# Start services
echo ""
echo "Starting services..."
docker-compose up -d

# Wait for services to be healthy
echo ""
echo "Waiting for services to become healthy..."
sleep 10

# Check service status
echo ""
echo "Service Status:"
docker-compose ps

# Show logs
echo ""
echo "Recent logs:"
docker-compose logs --tail=50

echo ""
echo "========================================"
echo "Deployment Complete!"
echo "========================================"
echo ""
echo "Your Docker registry is now running at:"
echo "  https://docker.mostlylucid.net"
echo ""
echo "Useful commands:"
echo "  docker-compose logs -f         # View logs"
echo "  docker-compose ps              # Check status"
echo "  docker-compose restart         # Restart services"
echo "  docker-compose down            # Stop services"
echo ""
echo "To login to your registry:"
echo "  docker login docker.mostlylucid.net"
echo ""
