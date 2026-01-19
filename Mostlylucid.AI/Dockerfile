# Stage 1: Build frontend assets
FROM node:20-alpine AS frontend
WORKDIR /src
COPY Mostlylucid.AI/package*.json ./
RUN npm ci
COPY Mostlylucid.AI/src ./src
COPY Mostlylucid.AI/tailwind.config.js ./
COPY Mostlylucid.AI/postcss.config.js ./
COPY Mostlylucid.AI/webpack.config.js ./
COPY Mostlylucid.AI/Views ./Views
RUN npm run build

# Stage 2: Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["Mostlylucid.AI/Mostlylucid.AI.csproj", "Mostlylucid.AI/"]
COPY ["Mostlylucid.DbContext/Mostlylucid.DbContext.csproj", "Mostlylucid.DbContext/"]
COPY ["Mostlylucid.Services/Mostlylucid.Services.csproj", "Mostlylucid.Services/"]
COPY ["Mostlylucid.Shared/Mostlylucid.Shared.csproj", "Mostlylucid.Shared/"]
COPY ["Umami.Net/Umami.Net.csproj", "Umami.Net/"]
RUN dotnet restore "Mostlylucid.AI/Mostlylucid.AI.csproj"

# Copy everything else and build
COPY . .
COPY --from=frontend /src/wwwroot/css/dist ./Mostlylucid.AI/wwwroot/css/dist
COPY --from=frontend /src/wwwroot/js/dist ./Mostlylucid.AI/wwwroot/js/dist

WORKDIR "/src/Mostlylucid.AI"
RUN dotnet build "Mostlylucid.AI.csproj" -c Release -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "Mostlylucid.AI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Final image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
EXPOSE 8080

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mostlylucid.AI.dll"]
