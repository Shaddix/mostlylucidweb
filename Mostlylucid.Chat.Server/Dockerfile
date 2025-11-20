FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["Mostlylucid.Chat.Server/Mostlylucid.Chat.Server.csproj", "Mostlylucid.Chat.Server/"]
COPY ["Mostlylucid.Chat.Shared/Mostlylucid.Chat.Shared.csproj", "Mostlylucid.Chat.Shared/"]

# Restore dependencies
RUN dotnet restore "Mostlylucid.Chat.Server/Mostlylucid.Chat.Server.csproj"

# Copy source code
COPY Mostlylucid.Chat.Server/ Mostlylucid.Chat.Server/
COPY Mostlylucid.Chat.Shared/ Mostlylucid.Chat.Shared/

# Build
WORKDIR "/src/Mostlylucid.Chat.Server"
RUN dotnet build "Mostlylucid.Chat.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Mostlylucid.Chat.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create wwwroot directory for widget
RUN mkdir -p /app/wwwroot

ENTRYPOINT ["dotnet", "Mostlylucid.Chat.Server.dll"]
