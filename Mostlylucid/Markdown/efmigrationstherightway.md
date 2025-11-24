# You're Probably Doing EF Migrations wrong...

Running `MigrateAsync()` at startup? You're giving your app database owner rights and hoping nothing goes wrong. There's a better way - EF migration bundles let you run migrations as a controlled CI step, keeping your production app secure. But here's the thing: sometimes the "wrong" way is actually fine. Let's explore when to use each approach.

<datetime class="hidden">2025-11-23T18:39</datetime>
<!--category--  Entity Framework, Migrations, GitHub, CI -->


**Official docs:** [Migrations Overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) | [Applying Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) | [Bundles](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#bundles)

[TOC]

# The "Wrong" Way (That I Use)

This blog uses `MigrateAsync()` at startup - the approach I'm about to tell you not to use. Here's why that's okay for me, and why it probably isn't for you.

In my `Program.cs` file I have the following:

```csharp
    using (var scope = app.Services.CreateScope())
    {
        var blogContext = scope.ServiceProvider.GetRequiredService<IMostlylucidDBContext>();
        await blogContext.Database.MigrateAsync();
    }
```

[`MigrateAsync()`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.relationaldatabasefacadeextensions.migrateasync) applies pending migrations and creates the database if needed. Simple - but problematic:

1. **Startup dependency** - Database down? Migration fails? Your app won't start.
2. **Security violation** - Your app needs `db_owner` rights. You just gave your runtime app the keys to drop tables.

Why I get away with it: public data, single Docker network, personal project. **You probably can't.**

## When Runtime Migrations Are Fine

- **Local dev** - Fast iteration beats ceremony
- **Personal projects** - Low blast radius, no sensitive data
- **Docker-compose dev environments** - Convenience wins
- **Prototyping** - Schema is changing constantly anyway

## When They're Not

- **Multiple app instances** - Race conditions galore
- **Sensitive data** - PII, financial, regulated = proper separation required
- **Production with real users** - Failed migration = outage

# The Right Way: EF Bundles

An EF bundle is a self-contained executable containing your compiled migrations. Think `dotnet ef database update` packaged into a standalone `.exe`.

**Why bundles win:**

- **No runtime dependencies** - Target doesn't need SDK or EF CLI
- **Proper separation** - App never needs `db_owner`; only CI runner does, only during deployment
- **CI visibility** - Failures show in pipeline logs, not buried in app startup
- **Rollback safety** - Migration fails? Deployment stops before bad code deploys
- **Idempotent** - Tracks what's applied, runs only what's needed

> **Note:** For production-grade security, use [Managed Identity](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview) instead of connection strings. But bundles are still a major step up from runtime migrations.

## GitHub Actions Example

```yaml
      - name: Install EF Core tools
        run: dotnet tool install --global dotnet-ef

      - name: Add EF tools to PATH
        run: echo "$HOME/.dotnet/tools" >> $GITHUB_PATH

      - name: Generate EF migration bundle
        run: |
          dotnet ef migrations bundle \
            --project ${{ env.WEB_PROJECT }} \
            --output efbundle.exe \
            --configuration ${{ env.BUILD_CONFIGURATION }} \
            --runtime ${{ env.RUNTIME_IDENTIFIER }} \
            --context AdminDbContext \
        env:
          AdminSite__ConnectionString: ${{ secrets.PROD_SQL_CONNECTIONSTRING }}

      - name: Run EF migration bundle
        run: |
          ./efbundle.exe
        env:
          AdminSite__ConnectionString: ${{ secrets.PROD_SQL_CONNECTIONSTRING }}
```

The bundle reads connection strings from environment variables and applies pending migrations. Already applied? It just exits successfully.

# Local Bundles

No CI? Want to test before pushing? Build bundles locally.

**Use cases:** Test before CI, DBA handoff (self-contained exe, no SDK needed), staging deploys, debugging with `--verbose`.

## Creating a Bundle

```bash
# Install EF CLI (once)
dotnet tool install --global dotnet-ef

# Basic bundle
dotnet ef migrations bundle \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid \
    --output efbundle.exe

# Self-contained (includes runtime - portable to machines without .NET)
dotnet ef migrations bundle \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid \
    --output efbundle.exe \
    --self-contained

# Cross-platform (e.g., build on Windows, deploy to Linux)
dotnet ef migrations bundle \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid \
    --output efbundle \
    --runtime linux-x64
```

## Running Your Bundle

```bash
# Using default connection string from appsettings.json
./efbundle.exe

# Override with a specific connection string
./efbundle.exe --connection "Host=localhost;Database=mostlylucid;Username=postgres;Password=secret"

# Using an environment variable (matches your config key)
$env:ConnectionStrings__DefaultConnection="Host=localhost;..." # PowerShell
export ConnectionStrings__DefaultConnection="Host=localhost;..." # Bash
./efbundle.exe
```

## Useful Bundle Options

```bash
# See what migrations would be applied without running them
./efbundle.exe --dry-run

# Verbose output for debugging
./efbundle.exe --verbose

# Apply migrations up to a specific migration (useful for testing)
./efbundle.exe --target-migration "20231115_AddUserTable"

# Combine options
./efbundle.exe --verbose --dry-run
```

## Local Testing Workflow

```bash
# 1. Create migration
dotnet ef migrations add AddNewFeature \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid

# 2. Build bundle
dotnet ef migrations bundle \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid \
    --output efbundle.exe

# 3. Dry run first
./efbundle.exe --dry-run --verbose

# 4. Run for real
./efbundle.exe --verbose

# 5. Broken? Remove and retry
dotnet ef migrations remove \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid
```

Catches syntax errors, constraint violations, FK issues - all *before* CI or production.

## Build Performance

**Bundle generation is slow** - 30+ seconds on large projects. Don't generate on every build.

- Generate manually when testing locally
- Generate in CI only during deployment, not every PR
- Cache bundles if migrations haven't changed

If you really want auto-generation, add a MSBuild target:

```xml
<Target Name="BuildMigrationBundle">
  <Exec Command="dotnet ef migrations bundle --output $(OutputPath)efbundle.exe --force" />
</Target>
```

Then: `dotnet build -t:BuildMigrationBundle`

# Hybrid Approach

Best of both worlds: convenience locally, security in production.

```csharp
if (builder.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<IMostlylucidDBContext>();
    await context.Database.MigrateAsync();
}
// Production: CI pipeline runs the bundle
```

# Alternatives to Bundles

## SQL Scripts

Generate plain SQL instead of an executable. Great for DBA review and existing change management processes.

```bash
# All migrations
dotnet ef migrations script --output migrations.sql

# Idempotent (safe to run multiple times) - USE THIS
dotnet ef migrations script --idempotent --output migrations.sql

# Range of migrations
dotnet ef migrations script FromMigration ToMigration --output migrations.sql
```

**Pros:** Full visibility, any SQL client can run it, version control friendly, DBA approval workflows.

**Cons:** No auto-tracking (use `--idempotent`), manual execution, potential drift if scripts are modified.

See [official docs on SQL scripts](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#sql-scripts).

### SQL Scripts in CI

```yaml
- name: Generate and apply migrations
  run: |
    dotnet ef migrations script --idempotent --output migrations.sql
    # SQL Server
    sqlcmd -S ${{ secrets.DB_SERVER }} -d ${{ secrets.DB_NAME }} -i migrations.sql
    # Or PostgreSQL
    PGPASSWORD=${{ secrets.DB_PASSWORD }} psql -h ${{ secrets.DB_HOST }} -f migrations.sql
```

## DACPAC (SQL Server Only)

[DACPACs](https://learn.microsoft.com/en-us/sql/relational-databases/data-tier-applications/data-tier-applications) are *state-based* not *migration-based*. You define the desired schema, and SqlPackage diffs it against the target database.

```bash
SqlPackage.exe /Action:Publish /SourceFile:MyDatabase.dacpac /TargetConnectionString:"..."
```

**Pros:** Schema as code, auto-diff generation, handles everything (tables, views, SPs, indexes), enterprise tooling.

**Cons:** SQL Server only, schema in two places (EF models + SQL project), diff engine makes questionable choices, column renames look like drop+add.

See [SqlPackage docs](https://learn.microsoft.com/en-us/sql/tools/sqlpackage/sqlpackage).

## Comparison Table

| Approach | Best For | Requires .NET | Auto-tracks Applied | DBA Friendly | Cross-platform DB |
|----------|----------|---------------|---------------------|--------------|-------------------|
| `MigrateAsync()` | Dev/small projects | Yes (runtime) | Yes | No | Yes |
| EF Bundles | CI/CD pipelines | No (self-contained) | Yes | Somewhat | Yes |
| SQL Scripts | DBA-controlled environments | No | With `--idempotent` | Yes | Yes |
| DACPAC | SQL Server enterprise | No | Yes (state-based) | Yes | No |

# Tips

## The Designer File Gotcha

Migrations work locally but not in CI? **Check you committed both files:**

- `20231115_AddUserTable.cs` - The migration code
- `20231115_AddUserTable.Designer.cs` - The model snapshot

Missing the Designer file = silent failure.

## Multiple DbContexts

```bash
dotnet ef migrations bundle --context BlogDbContext --output blog-migrations.exe
dotnet ef migrations bundle --context IdentityDbContext --output identity-migrations.exe
```

## Connection String Priority

1. `--connection` argument
2. Environment variable
3. `appsettings.json`

Use environment variables in CI.

## IDesignTimeDbContextFactory

EF tools need to instantiate your DbContext. If your DbContext is in a separate project or has complex startup, implement [`IDesignTimeDbContextFactory<T>`](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#from-a-design-time-factory):

```csharp
public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<AdminDbContextFactory>()
            .Build();

        var connectionString = config["AdminSite:ConnectionString"]
            ?? throw new InvalidOperationException("Missing connection string");

        var optionsBuilder = new DbContextOptionsBuilder<AdminDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql => sql.CommandTimeout(120));

        return new AdminDbContext(optionsBuilder.Options);
    }
}
```

Use when: DbContext in separate project, complex startup, need User Secrets for design-time.

# What About...?

Common questions and pushback I've received.

## "Why not just run `dotnet ef database update` in CI?"

Covered above, but the short version: bundles are portable artifacts. Your deployment step doesn't need EF CLI, source code, or design-time resolution. Same bundle runs in test, staging, and prod - zero drift.

## "Isn't this overkill for a small app?"

Maybe. If you're solo, data is public, and blast radius is low - `MigrateAsync()` is fine. But the moment you add a second developer, sensitive data, or multiple environments, bundles pay for themselves.

## "What about rollbacks?"

EF doesn't do automatic rollbacks. Options:
- Generate a `Down()` migration and run it (but you have to have written it)
- Restore from backup
- Write a manual migration to undo changes

For critical systems: test migrations against a database clone first.

## "Can I run migrations in a Kubernetes init container?"

Yes. Bundle + init container is a solid pattern:

```yaml
initContainers:
  - name: migrate
    image: myapp:latest
    command: ["./efbundle.exe"]
    env:
      - name: ConnectionStrings__Default
        valueFrom:
          secretKeyRef:
            name: db-secrets
            key: connection-string
```

App container waits for init to complete.

## "What about FluentMigrator / DbUp / other tools?"

They work great. EF bundles are the EF-native solution, but [FluentMigrator](https://fluentmigrator.github.io/) and [DbUp](https://dbup.readthedocs.io/) have their fans. Key difference: those are migration-specific tools, while EF bundles come from your existing EF model.

## "My DBA wants to review all SQL before it runs"

Use `--idempotent` scripts:

```bash
dotnet ef migrations script --idempotent --output migrations.sql
```

DBA reviews and approves. Then either:
- Run the script manually, or
- Once approved, run the bundle (which does the same thing)

## "How do I handle migrations with zero downtime?"

That's a deployment strategy question, not a migrations question. Generally:
1. Make migrations backwards-compatible (add columns nullable, don't rename)
2. Deploy new code that handles both old and new schema
3. Run migration
4. Deploy code that uses new schema only
5. Clean up (drop old columns in a later migration)

Bundles don't solve this - they just make step 3 more predictable.