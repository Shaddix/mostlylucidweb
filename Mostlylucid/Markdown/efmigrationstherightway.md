# You're Probably Doing EF Migrations wrong...

Running `MigrateAsync()` at startup? You're giving your app database owner rights and hoping nothing goes wrong. There's a better way - EF migration bundles let you run migrations as a controlled CI step, keeping your production app secure. But here's the thing: sometimes the "wrong" way is actually fine. Let's explore when to use each approach.

<datetime class="hidden">2025-11-23T18:39</datetime>
<!--category--  Entity Framework, Migrations, GitHub, CI -->


# Introduction
Well, I was doing it wrong! This article discusses EF bundles and how they work with your CI to make EF migrations safer, more predictable and secure.

For the official documentation, see:
- [EF Core Migrations Overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/?tabs=dotnet-core-cli)
- [Applying Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli)
- [Migration Bundles](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#bundles)

[TOC]

# The Wrong Way
Well, this very blog uses the "WRONG" way to do EF migrations but for a reason.

In my `Program.cs` file I have the following:

```csharp
    using (var scope = app.Services.CreateScope())
    {
        var blogContext = scope.ServiceProvider.GetRequiredService<IMostlylucidDBContext>();
        await blogContext.Database.MigrateAsync();
    }
```

This uses the [`MigrateAsync()`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.relationaldatabasefacadeextensions.migrateasync) method which applies any pending migrations and creates the database if it doesn't exist.

Simple, right? However this has two main issues:

1. **It runs at startup** - If your database is down (or takes a while to start with new deployments), or the migration fails for any reason your app won't start.
2. **It's insecure** - Your app needs to have `db_owner` rights to be able to run migrations. This is a MAJOR security risk.

Really the second point is the most critical for most REAL uses of EF migrations. You just shouldn't be giving your app that level of access to the database.

**You broke the security boundary**

So why do I do it for this blog:
1. ALL the data for the blog (except comments) is public
2. I deploy the site using Docker pulls (a tool called Watchtower does this automagically) so the app and DB are on the same Docker network which isn't expsed to the internet. There ARE ways around this but for the simplicity this won *but in a real app it shouldnt*

## When "Wrong" is Actually Right

Before we dive into the "right" way, let's be honest: the approach above isn't *always* wrong. Context matters.

### Scenarios Where Runtime Migrations Make Sense

1. **Local Development** - When you're iterating quickly on your schema, having migrations run automatically on startup is incredibly convenient. You don't want to manually run migration commands every time you pull changes from a colleague.

2. **Small Personal Projects** - For a blog like this one, where I'm the only developer and the security boundary is less critical, the convenience outweighs the risks. The database and app run on the same Docker network, and I'm not handling sensitive customer data.

3. **Containerized Dev Environments** - When spinning up `docker-compose` for development, automatic migrations mean your database is always in sync without extra steps.

4. **Prototyping / POC Work** - When you're exploring ideas and the schema is changing rapidly, ceremony-free migrations keep you moving.

### The Key Questions to Ask

- **Who has access to this database?** If it's just you on a personal project, the security argument is less compelling.
- **What's the blast radius if something goes wrong?** A failed migration on your personal blog is annoying. A failed migration on a production e-commerce site is catastrophic.
- **Do you have multiple instances?** Runtime migrations with multiple app instances can cause race conditions where instances compete to run migrations simultaneously.
- **Is there sensitive data?** If your app handles PII, financial data, or anything regulated, you absolutely need proper separation of concerns.

# The Right Way

Note: even in the code below it STILL isn't the perfect security level; ideally we shouldn't store the connection string here at all - using [Managed Identity](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview) would be better. But this is a significant step up from runtime migrations.

## What is an EF Bundle?

An EF migration bundle is a self-contained executable that includes all your migrations. It's essentially a compiled version of your migrations that can be run independently of your application. Think of it as packaging up `dotnet ef database update` into a standalone `.exe`.

The key benefits:

1. **No EF tools required at runtime** - The target environment doesn't need the .NET SDK or EF CLI tools installed
2. **Atomic deployment artifact** - The bundle is versioned alongside your application code
3. **Separation of concerns** - Your application code never needs database owner permissions
4. **Idempotent** - The bundle tracks which migrations have been applied and only runs what's needed

## Why is This Right?

1. **Where it runs** - It runs entirely within the CI system, not during application startup
2. **Controlled timing** - Migrations are a discrete step in your deployment pipeline, not a side effect of starting the app
3. **Proper permissions** - Only the CI runner needs elevated database permissions, and only during deployment
4. **Visibility** - Migration failures are visible in your CI logs, not buried in application startup logs
5. **Rollback potential** - If migrations fail, deployment stops before the new app version is deployed

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

The bundle executable reads the connection string from the environment variable and applies any pending migrations. If all migrations are already applied, it simply exits successfully.

# Local EF Bundles

You don't need a CI system to use EF bundles. They're equally useful for local development when you want more control than `MigrateAsync()` provides. In fact, building bundles locally is a great way to test your migrations before they hit CI.

## Why Build Bundles Locally?

There are several compelling reasons to build and run EF bundles on your local machine:

1. **Test before you push** - Catch migration issues before they fail in CI. Nothing's worse than waiting for a 10-minute pipeline only to discover your migration has a syntax error or constraint violation.

2. **No CI? No problem** - Not every project has a full CI/CD pipeline. Maybe you're working on a side project, or your organisation hasn't adopted CI yet. Local bundles give you the same benefits without the infrastructure.

3. **Faster feedback loop** - Building and running a bundle locally takes seconds. You can iterate on a tricky migration much faster than pushing to CI each time.

4. **DBA handoff** - In organisations where developers don't have production database access, you can build a self-contained bundle and hand it to a DBA to run. They don't need the .NET SDK installed.

5. **Staging/QA deployments** - Apply migrations to non-production environments without triggering a full deployment pipeline.

6. **Debugging migration issues** - When something goes wrong, running the bundle locally with `--verbose` gives you much better insight than CI logs.

## Creating a Local Bundle

First, make sure you have the [EF Core CLI tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) installed:

```bash
dotnet tool install --global dotnet-ef
```

Then create your bundle:

```bash
# Basic bundle (requires .NET runtime on target machine)
dotnet ef migrations bundle \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid \
    --output efbundle.exe

# Self-contained bundle (includes .NET runtime - larger but portable)
dotnet ef migrations bundle \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid \
    --output efbundle.exe \
    --self-contained

# For a specific runtime (e.g., Linux deployment from Windows)
dotnet ef migrations bundle \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid \
    --output efbundle \
    --runtime linux-x64
```

The `--self-contained` flag creates a bundle that includes the .NET runtime, making it portable to machines without .NET installed. This is particularly useful when handing bundles to DBAs or deploying to locked-down environments.

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

Here's a practical workflow for testing migrations locally before pushing to CI:

```bash
# 1. Create your migration
dotnet ef migrations add AddNewFeature \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid

# 2. Build a bundle to test it
dotnet ef migrations bundle \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid \
    --output efbundle.exe

# 3. Test against a local database (dry run first)
./efbundle.exe --dry-run --verbose

# 4. If it looks good, run it for real
./efbundle.exe --verbose

# 5. If something went wrong, remove the migration and try again
dotnet ef migrations remove \
    --project Mostlylucid.DbContext \
    --startup-project Mostlylucid
```

This workflow catches issues like:
- Invalid SQL syntax
- Constraint violations
- Missing foreign key relationships
- Data type mismatches

All *before* your code hits CI or production.

## A Word on Build Performance

One thing to be aware of: **EF bundle generation is slow**. It needs to build your project, load all migrations, and compile them into an executable. On a large project this can take 30 seconds or more.

This is why you typically don't want to generate bundles on every build. Instead:
- Generate bundles manually when testing migrations locally
- Generate bundles in CI only during deployment (not on every PR build)
- Consider caching the bundle in CI if migrations haven't changed

### If You Really Want Auto-Generated Bundles

That said, if you *do* want to automatically generate a bundle on build (maybe for a small project or specific workflow), you can add a post-build target to your `.csproj`:

```xml
<Target Name="GenerateEfBundle" AfterTargets="Build" Condition="'$(Configuration)' == 'Release'">
  <Exec Command="dotnet ef migrations bundle --output $(OutputPath)efbundle.exe --force --no-build" />
</Target>
```

Or for more control, create a separate target you can invoke explicitly:

```xml
<Target Name="BuildMigrationBundle">
  <Exec Command="dotnet ef migrations bundle --output $(OutputPath)efbundle.exe --force" />
</Target>
```

Then run it with:

```bash
dotnet build -t:BuildMigrationBundle
```

The `--force` flag overwrites any existing bundle, and `--no-build` skips rebuilding the project (useful if you've just built it).

**My recommendation**: Don't auto-generate on build. The slowdown isn't worth it for most workflows. Generate bundles explicitly when you need them - either locally for testing or in your CI deployment step.

# Hybrid Approach

For many teams, a hybrid approach works well:

- **Development**: Use `MigrateAsync()` for convenience during local development
- **CI/Production**: Use EF bundles for controlled, auditable deployments

You can achieve this with a simple environment check:

```csharp
if (builder.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<IMostlylucidDBContext>();
    await context.Database.MigrateAsync();
}
// In production, migrations are handled by the CI pipeline
```

This gives you the best of both worlds: fast iteration locally, and proper security boundaries in production.

# Tips

## The Designer File Gotcha
If your migrations seem to work locally but NOT when you check into GitHub, **check you're adding the designer part of the generated migration**. YOU NEED BOTH files:

- `20231115_AddUserTable.cs` - The migration code
- `20231115_AddUserTable.Designer.cs` - The model snapshot

It'll seem to run but no migration will happen if the Designer file is missing.

## Multiple DbContexts

If you have multiple DbContexts, specify which one when creating bundles:

```bash
dotnet ef migrations bundle --context BlogDbContext --output blog-migrations.exe
dotnet ef migrations bundle --context IdentityDbContext --output identity-migrations.exe
```

## Connection String Sources

The bundle looks for connection strings in this order:
1. `--connection` command line argument
2. Environment variable matching your configuration key
3. `appsettings.json` in the current directory

For CI, environment variables are preferred as they keep secrets out of logs and config files. 