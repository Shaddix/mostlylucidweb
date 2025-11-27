FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy project files
COPY ["Mostlylucid.Workflow.Demo/Mostlylucid.Workflow.Demo.csproj", "Mostlylucid.Workflow.Demo/"]
COPY ["Mostlylucid.Workflow.Engine/Mostlylucid.Workflow.Engine.csproj", "Mostlylucid.Workflow.Engine/"]
COPY ["Mostlylucid.Workflow.Shared/Mostlylucid.Workflow.Shared.csproj", "Mostlylucid.Workflow.Shared/"]

# Restore
RUN dotnet restore "Mostlylucid.Workflow.Demo/Mostlylucid.Workflow.Demo.csproj"

# Copy source
COPY Mostlylucid.Workflow.Demo/ Mostlylucid.Workflow.Demo/
COPY Mostlylucid.Workflow.Engine/ Mostlylucid.Workflow.Engine/
COPY Mostlylucid.Workflow.Shared/ Mostlylucid.Workflow.Shared/

# Build
WORKDIR "/src/Mostlylucid.Workflow.Demo"
RUN dotnet build "Mostlylucid.Workflow.Demo.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Mostlylucid.Workflow.Demo.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mostlylucid.Workflow.Demo.dll"]
