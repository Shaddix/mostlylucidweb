# Modern Release Strategies with GitHub Actions

<!--category-- DevOps, CI/CD, GitHub Actions -->
<datetime class="hidden">2025-12-04T12:00</datetime>

**The core principle: make releases fast and painless, and you'll release more often.** This is critical for agile development - rapid, reliable deployments create the fast feedback loop you need. When deploying is scary or slow, teams batch up changes, increasing risk. When deploying is boring and automatic, you ship small changes frequently.

This guide covers the release strategies I use in production, from simple tag-based deployments to multi-package monorepo publishing. All examples come from real [GitHub Actions](https://docs.github.com/en/actions) workflows in this repository.

[TOC]

## Release Strategies at a Glance

Before diving into details, here's how common strategies compare:

| Strategy | Best For | Complexity | Common In |
|----------|----------|------------|-----------|
| **Tag-based** | Applications, explicit releases | Low | Startups, solo devs, mature teams |
| **Branch-based** | Continuous deployment | Low | Startups, fast-moving teams |
| **GitFlow** | Regulated releases, multiple versions | High | Enterprise, compliance-heavy |
| **Trunk-based + Feature flags** | High-velocity teams | Medium | Big tech, mature startups |
| **Monorepo multi-package** | Libraries, shared codebases | Medium | Platform teams, OSS projects |

### What Works Where

**Startups & Small Teams**: Start with tag-based or branch-based deployment. Keep it simple - you can always add complexity later. Trunk-based development with feature flags is popular at scale but overkill for small teams.

**Enterprise**: Often requires GitFlow or similar for compliance, audit trails, and release management. Multiple environments (dev, QA, staging, prod) with approval gates. Artifact attestations increasingly required for supply chain security.

**Solo Developers**: Tag-based is ideal. Push a tag, deployment happens. No ceremony, no overhead.

**Platform/Library Teams**: Need monorepo strategies with independent versioning per package. Semantic versioning is critical for downstream consumers.

## Tag-Based Deployment

The simplest and most reliable strategy. Different tag prefixes route to different environments or trigger different actions.

### Environment Targeting

```yaml
on:
  push:
    tags:
      - 'release-*'  # Production
      - 'local-*'    # Dev environment
```

```yaml
- name: Build Docker image
  run: |
    TAG_NAME=${GITHUB_REF#refs/tags/}
    if [[ "$TAG_NAME" == local-* ]]; then
      docker build -t myapp:local .
    else
      docker build -t myapp:latest -t myapp:$(date +%s) .
    fi
```

**Why it works**: Simple mental model, explicit deployments, easy rollback via timestamped tags.

```bash
# Deploy to production
git tag release-v2024.12.01 && git push origin release-v2024.12.01

# Deploy to dev
git tag local-feature-test && git push origin local-feature-test
```

### Multi-Package Versioning

For monorepos with multiple publishable packages, use tag prefixes to identify which package to release:

```yaml
# Different workflows, different tag patterns
# umami-net.yml
on:
  push:
    tags: ['umamiv*.*.*']  # e.g., umamiv1.0.5

# fetchextension.yml
on:
  push:
    tags: ['fetchextension-v*.*.*']  # e.g., fetchextension-v1.2.0
```

Extract the version and use it throughout:

```yaml
- name: Extract version
  run: echo "VERSION=${GITHUB_REF#refs/tags/umamiv}" >> $GITHUB_OUTPUT

- name: Build & Pack
  run: |
    dotnet build -c Release -p:Version=${{ steps.version.outputs.VERSION }}
    dotnet pack -c Release -p:PackageVersion=${{ steps.version.outputs.VERSION }}
```

For automatic versioning, [MinVer](https://github.com/adamralph/minver) calculates versions from Git tags:

```xml
<PackageReference Include="MinVer" Version="6.0.0" PrivateAssets="all" />
<PropertyGroup>
  <MinVerTagPrefix>umamiv</MinVerTagPrefix>
</PropertyGroup>
```

## Branch-Based Deployment

Deploy automatically when code lands on specific branches. Common in startups and fast-moving teams.

```yaml
on:
  push:
    branches: [main]      # → Production
    # branches: [develop] # → Staging
```

**Pros**: No manual step, deploys on merge
**Cons**: Accidental deploys possible, less explicit history

I use a hybrid approach - build on branch push (CI verification), but only publish on tags:

```yaml
on:
  push:
    tags: ['scheduler-*']
    branches: [main, local]

jobs:
  build:
    # Always build
  publish:
    if: startsWith(github.ref, 'refs/tags/')  # Only publish on tags
```

## Auto-Updates with Watchtower

For self-hosted deployments, [Watchtower](https://containrrr.dev/watchtower/) automatically pulls new images:

```yaml
services:
  app:
    image: myapp:latest
    labels:
      - "com.centurylinklabs.watchtower.enable=true"

  watchtower:
    image: containrrr/watchtower
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    command: --interval 300  # Check every 5 minutes
```

**Deployment flow**: Push tag → GitHub Actions builds → Push to registry → Watchtower pulls → Container restarts. Total time: ~5 minutes.

## Quality Gates

Fast deployments are useless if you deploy bugs. Every workflow should include gates:

```yaml
- name: Run tests
  run: dotnet test --configuration Release

- name: Build
  run: dotnet build --configuration Release

# Tests or build fail → workflow stops → no publish
```

For multi-framework packages, build all targets:

```yaml
- run: dotnet build -c Release --framework net8.0
- run: dotnet build -c Release --framework net9.0
```

## Passwordless Publishing with OIDC

Modern approach - no secrets to rotate, no keys to leak:

```yaml
permissions:
  id-token: write
  contents: read

- uses: NuGet/login@v1
  with:
    user: 'myusername'

- run: dotnet nuget push *.nupkg --api-key ${{ steps.login.outputs.NUGET_API_KEY }}
```

GitHub exchanges its OIDC token for a short-lived NuGet API key. Configure trust on NuGet.org to enable this.

## Supply Chain Security

[Artifact attestations](https://docs.github.com/en/actions/security-for-github-actions/using-artifact-attestations) prove your artifacts were built in CI, not tampered with. Increasingly required by enterprises.

```yaml
permissions:
  id-token: write
  attestations: write

- uses: docker/build-push-action@v6
  id: push
  with:
    push: true
    tags: myapp:latest

- uses: actions/attest-build-provenance@v2
  with:
    subject-name: index.docker.io/myuser/myapp
    subject-digest: ${{ steps.push.outputs.digest }}
    push-to-registry: true
```

Consumers verify with: `gh attestation verify oci://index.docker.io/myuser/myapp:latest --owner myuser`

This achieves [SLSA Level 2](https://slsa.dev/spec/v1.0/levels). For Level 3, use [reusable workflows](https://docs.github.com/en/actions/sharing-automations/reusing-workflows).

## Preview Environments

Teams need stakeholders to preview changes before merging.

**Enterprise approach** - Branch-based preview environments:

```yaml
on:
  pull_request:
    types: [opened, synchronize]

- run: |
    BRANCH=$(echo ${{ github.head_ref }} | sed 's/[^a-zA-Z0-9]/-/g')
    docker build -t myapp:preview-$BRANCH .
    # Deploy to k8s namespace, cloud platform, etc.
```

**Startup/solo approach** - Tunnel your local machine:

```bash
# Cloudflare Tunnel (free)
cloudflared tunnel run --url http://localhost:5000 my-preview
# → https://my-preview.cfargotunnel.com
```

Or use [Wireguard](https://www.wireguard.com/) VPN for internal team access to your dev machine.

## Practical Tips

**Tag cleanup**: Git tags stick around forever.
```bash
git tag -d old-test-tag                      # Delete local
git push origin :refs/tags/old-test-tag      # Delete remote
```

**Naming conventions**: Be consistent.
- `release-YYYY.MM.DD` for production
- `local-feature-name` for dev
- `packagev1.2.3` for libraries

**Secrets**: Store in [GitHub Secrets](https://docs.github.com/en/actions/security-for-github-actions/security-guides/using-secrets-in-github-actions), never in code. Required secrets typically:
- `DOCKER_HUB_ACCESS_TOKEN`
- `NUGET_API_KEY` (or use OIDC)
- `NPM_TOKEN`

**Monorepo development**: Use project references during development, switch to package references for deployment verification:

```xml
<ProjectReference Include="..\MyLib\MyLib.csproj" />
<!-- <PackageReference Include="MyLib" Version="1.0.0" /> -->
```

## Moving to Kubernetes

Same principles, different tools:

| Docker Compose | Kubernetes |
|----------------|------------|
| Watchtower | [ArgoCD](https://argo-cd.readthedocs.io/) / [Flux](https://fluxcd.io/docs/) |
| docker-compose.yml environments | Namespaces |
| Container restart | Rolling deployment |
| Manual rollback | GitOps rollback |

## Summary

1. **Start simple**: Tag-based deployment covers most needs
2. **Add complexity when needed**: Branch-based, multi-environment, attestations
3. **Automate quality gates**: Tests must pass before publish
4. **Enable rollback**: Timestamped tags or GitOps history
5. **Secure your pipeline**: OIDC for secrets, attestations for provenance
6. **Match your context**: Solo dev ≠ startup ≠ enterprise

The workflows shown here run in this repository - check `.github/workflows/` for complete implementations.

**Remember**: Rapid, reliable deployments are the foundation of agile development. Invest in your release pipeline early.
