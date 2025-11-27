# Repository Guidelines

## Project Structure & Module Organization
- Primary site: `Mostlylucid/` (ASP.NET Core MVC) with controllers, views, middleware, and `wwwroot` assets; front-end source lives in `Mostlylucid/src`.
- Shared code: `Mostlylucid.Shared/` and `Mostlylucid.DbContext/` feed domain models and EF migrations; tag helpers and Markdig utilities live in `Mostlylucid.TagHelpers/` and `Mostlylucid.Markdig.*`.
- Packages and demos: feature-specific projects sit alongside `*.Demo` folders (e.g., `Mostlylucid.BotDetection.Demo`, `Mostlylucid.SemanticSearch.Demo`) and supporting libraries such as `Umami.Net*`, `TinyLLM/`, and `SymbolicTesting/`.
- Tests: .NET tests in `Mostlylucid.Test/`; front-end integration scripts (Vitest + Puppeteer) live under `Mostlylucid/` with names like `test-*.js`.
- Solutions: use `Mostlylucid.sln` for the full workspace; smaller solution files (e.g., `mostlylucid.FetchExtension.sln`) allow focused builds.

## Build, Test, and Development Commands
- Restore/build: `dotnet restore` then `dotnet build Mostlylucid.sln` (targets net9.0 across projects).
- Run the site locally: `dotnet run --project Mostlylucid/Mostlylucid.csproj` (honors `appsettings.Development.json`).
- Front-end assets (run inside `Mostlylucid/`): `npm install`, `npm run dev` for a one-off CSS/JS build, `npm run watch` for live recompiles, `npm run build` for production bundles and copies.
- Tests: `dotnet test Mostlylucid.sln` (xUnit + coverlet collector); front-end checks via `npm test`, `npm run test:coverage`, or `npm run test:ui` when debugging DOM behavior.

## Coding Style & Naming Conventions
- C#: nullable + implicit usings are enabled; prefer file-scoped namespaces, PascalCase types, camelCase locals/fields, and suffix async methods with `Async`.
- Keep 4-space indentation; use expression-bodied members where it improves readability; favor `var` for obvious types to reduce noise.
- JavaScript/TypeScript: keep modules small, prefer ESM imports, and mirror existing naming (`test-*.js`, `*-config.js`).

## Testing Guidelines
- Unit and integration tests use xUnit; name files `*Tests.cs` and keep fixtures close to the code under test.
- Coverage: `dotnet test Mostlylucid.sln --collect:"XPlat Code Coverage"` works with the bundled collector; fail fast on flaky or timing-sensitive tests.
- Front-end: Vitest uses Happy DOM; keep DOM selectors stable and prefer data attributes for assertions; heavier flows can run through Puppeteer scripts in `Mostlylucid/`.

## Commit & Pull Request Guidelines
- Follow the existing conventional style seen in history (e.g., `feat: add RAG-assisted LLM translator NuGet package`, `fix:` for bug fixes, `chore:` for tooling).
- Keep commits focused and shippable: include rationale in the body when touching infra (`docker-compose*.yml`) or config (`appsettings*.json`).
- PRs: add a concise summary, linked issue/goal, test evidence (`dotnet test`, `npm test` output), and screenshots/GIFs for UI changes (views or widget demos). Note which solution/project was touched to ease reviewer setup.

## Security & Configuration
- Never commit secrets: `.env`, `appsettings*.json`, and HTTP client env files hold credentials; use local overrides instead of in-repo values.
- Docker and compose files exist for supporting services (PostgreSQL, Seq, Prometheus/Umami); document any required containers in your PR and prefer sample env values.
- When adding new configs, provide sensible defaults and keep production-only settings outside source control.
