# SegmentCommerce Dockerfile
# Multi-stage build for ASP.NET Core with Node.js for frontend assets

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Install Node.js for frontend build
RUN apt-get update && apt-get install -y nodejs npm && rm -rf /var/lib/apt/lists/*

# Copy csproj and restore
COPY ["Mostlylucid.SegmentCommerce.csproj", "./"]
RUN dotnet restore

# Copy package.json and install npm packages
COPY ["package.json", "package-lock.json*", "./"]
RUN npm ci --silent

# Copy everything else
COPY . .

# Build frontend assets (if webpack/postcss configured)
RUN npm run build --if-present || echo "No npm build script, skipping"

# Build the app
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r segmentcommerce && useradd -r -g segmentcommerce segmentcommerce

# Create directories for data and images
RUN mkdir -p /data/images /data/import && chown -R segmentcommerce:segmentcommerce /data

# Copy published app
COPY --from=build /app/publish .

# Set ownership
RUN chown -R segmentcommerce:segmentcommerce /app

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ImageStorage__BasePath=/data/images
ENV DataImport__BasePath=/data/import

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Switch to non-root user
USER segmentcommerce

EXPOSE 8080

ENTRYPOINT ["dotnet", "Mostlylucid.SegmentCommerce.dll"]
