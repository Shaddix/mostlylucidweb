FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and supporting files needed for restore
COPY Mostlylucid.DocSummarizer.Core/*.csproj Mostlylucid.DocSummarizer.Core/
COPY Mostlylucid.DocSummarizer.Core/release-notes.txt Mostlylucid.DocSummarizer.Core/
COPY Mostlylucid.GraphRag/*.csproj Mostlylucid.GraphRag/
COPY Mostlylucid.RagDocuments/*.csproj Mostlylucid.RagDocuments/
RUN dotnet restore Mostlylucid.RagDocuments/Mostlylucid.RagDocuments.csproj

# Copy source and build
COPY Mostlylucid.DocSummarizer.Core/ Mostlylucid.DocSummarizer.Core/
COPY Mostlylucid.GraphRag/ Mostlylucid.GraphRag/
COPY Mostlylucid.RagDocuments/ Mostlylucid.RagDocuments/
WORKDIR /src/Mostlylucid.RagDocuments
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install libgomp for ONNX runtime
RUN apt-get update && apt-get install -y --no-install-recommends libgomp1 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Create directories
RUN mkdir -p /app/uploads /app/data /app/logs

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "Mostlylucid.RagDocuments.dll"]
