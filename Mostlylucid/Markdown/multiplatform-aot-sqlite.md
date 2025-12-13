# Multiplatform AOT with SQLite: How to get it working!

<!--category -- .NET, AOT, SQLite, DevOps -->
<datetime class="hidden">2025-12-16T15:00</datetime>

# Introduction

Native AOT promises to turn your .NET applications into tiny, self-contained executables that start instantly and run anywhere—no runtime installation required. It's magic when it works. But add SQLite to the mix and suddenly you're debugging cryptic `DllNotFoundException` crashes, fighting with transitive dependencies, and wondering why `PublishSingleFile=true` doesn't actually bundle everything into a single file. This is the complete, beginner-to-expert guide on getting SQLite working with Native AOT, building for multiple platforms, and automating releases with GitHub Actions. By the end, you'll have 10-12MB binaries that run on Windows, Linux (including Raspberry Pi), and macOS—with zero dependencies.

## What is AOT? (And Why Should You Care?)

Let's start with the absolute basics. If you've ever built a .NET application and wondered why you need to install the ".NET Runtime" on servers or why your console app takes a second to start the first time you run it, AOT is the answer to those problems.

### How .NET Normally Works (JIT Compilation)

When you write C# code and build your application, the compiler doesn't produce machine code that your CPU can directly execute. Instead, it produces something called **Intermediate Language (IL)**—think of it as halfway between your C# code and actual machine instructions.

When you run your .NET app, here's what happens:

1. Your computer loads the .NET Runtime (a separate program)
2. The runtime reads your IL code
3. **Just-In-Time (JIT)** compiler converts IL to native machine code as your app runs
4. Your CPU finally executes that machine code

This is like having a translator who reads your recipe (IL) and verbally translates it to a chef (CPU) line by line while they're cooking. It works, but there's overhead:

- The .NET Runtime is 50-150MB of extra files you need to deploy
- The JIT compiler takes time to translate code (why your app is slow the first time)
- The JIT compiler itself sits in memory while your app runs

### Enter AOT (Ahead-Of-Time Compilation)

Native AOT flips this model on its head. Instead of translating your code at runtime, it translates everything **at build time**. You end up with a single executable file that contains actual machine code your CPU can run directly—no runtime, no translator, no waiting.

Think of it like getting a professionally translated recipe book instead of hiring a live translator. The work is done once, upfront, and the result is ready to use immediately.

### The Game-Changing Benefits

Here's what Native AOT gives you:

**1. Tiny executables**: 10-30MB instead of 150MB+

Your app and everything it needs gets compiled into one small binary. No separate runtime files.

**2. Instant startup**: 80% faster cold starts

My tests: normal .NET took ~800ms to start, AOT took ~150ms. There's no JIT warm-up time—the machine code is ready to execute immediately.

**3. Zero dependencies**: No .NET runtime required

You can copy your executable to any machine with the right OS (Windows/Linux/Mac) and it just runs. No "install .NET 10 Runtime" prerequisite.

**4. Lower memory usage**: About 50% less memory

No JIT compiler sitting in memory. In my gateway application, normal .NET used 85MB idle, AOT used 42MB.

**5. Better for restricted environments**: Works where JIT can't

Some environments (certain Docker containers, iOS, embedded systems) don't allow runtime code generation. AOT works everywhere.

### The Trade-offs (There's Always a Catch)

AOT isn't magic. You're trading runtime flexibility for upfront optimization:

**1. No dynamic code generation**

Anything that generates code at runtime won't work:
- `System.Reflection.Emit` (creating types dynamically)
- Dynamic assembly loading (loading DLLs at runtime)
- Some fancy reflection tricks

Most normal .NET code is fine, but some frameworks that rely heavily on reflection need special configuration.

**2. Platform-specific builds**

JIT compilation produces IL that runs on any platform. AOT produces native machine code for **one specific platform**. You need to build separately for:
- Windows x64
- Linux x64
- Linux ARM64 (Raspberry Pi)
- macOS Intel
- macOS Apple Silicon

We'll cover automating this with GitHub Actions later.

**3. Longer build times**

Instead of compiling to IL in seconds, AOT compiles all the way to machine code. Expect 2-5 minutes instead of 10 seconds. It's a one-time cost for permanent benefits.

**4. Some features need extra configuration**

JSON serialization, Entity Framework, and anything using heavy reflection may need you to explicitly tell the compiler what types to keep. We'll cover this.

### When Should You Use AOT?

Native AOT is perfect for:

- **CLI tools**: Command-line utilities where instant startup matters
- **Microservices**: Smaller Docker images, faster scaling in Kubernetes
- **Serverless/Lambda**: Cold start time directly impacts your bill
- **Edge devices**: Raspberry Pi, IoT devices with limited resources
- **Gateway applications**: Reverse proxies, API gateways with high throughput
- **Desktop tools**: Ship a single .exe with no "install .NET first" step

Skip AOT for:

- Traditional web apps where startup time doesn't matter
- Apps using heavy Entity Framework with many migrations
- Plugin systems that load DLLs dynamically
- Anything that generates code at runtime

For CLI tools, microservices, containers, and edge devices, the improvements are game-changing. But there's a catch when you add databases—specifically SQLite.

## The SQLite Problem

Now that you understand what AOT is, let's talk about the single biggest pain point: SQLite. This is the wall most people hit when trying to use AOT with database-backed applications.

### What Happens (The Frustrating Experience)

Here's the typical journey:

1. You add `Microsoft.Data.Sqlite` to your project
2. You configure `PublishAot=true` in your `.csproj`
3. The build completes without errors—everything looks good!
4. You run the executable and immediately get:

```
DllNotFoundException: Unable to load DLL 'e_sqlite3' or one of its dependencies
```

Your app crashes before it can do anything. What gives?

### Understanding Native Libraries (A Quick Detour)

To understand the problem, you need to know about **native libraries**.

SQLite isn't written in .NET—it's written in C. It's compiled to platform-specific native code:
- `e_sqlite3.dll` on Windows
- `libe_sqlite3.so` on Linux
- `libe_sqlite3.dylib` on macOS

When you use `Microsoft.Data.Sqlite` in normal .NET, it's just a wrapper around this native SQLite library. At runtime, it tries to **dynamically load** the appropriate native file for your platform.

This works fine with normal .NET because:
1. The runtime can load libraries dynamically
2. NuGet packages can include native libraries for all platforms
3. The right one gets selected at runtime

### Why AOT Breaks This

Native AOT has two characteristics that clash with SQLite's approach:

**1. Aggressive trimming**: AOT removes any code it thinks you're not using. If it can't statically prove you need something, it gets deleted. Dynamic library loading confuses the trimmer—it can't see the connection between your code and the native SQLite DLL.

**2. No dynamic loading support**: AOT produces a self-contained binary. It expects all native dependencies to be explicitly linked at compile time, not loaded dynamically at runtime.

The result: `Microsoft.Data.Sqlite` expects to find a native SQLite library at runtime, but AOT has either trimmed it away or doesn't know how to bundle it properly.

### The Transitive Dependency Nightmare

Even worse, if you have other NuGet packages that use SQLite (like some ORM libraries or my `mostlylucid.ephemeral.complete` package), you can end up with **multiple incompatible SQLite providers** in your dependency tree.

Each provider tries to work differently:
- One might expect OS-provided SQLite (Windows only)
- Another might bundle its own SQLite
- Another might use a different native library version

The AOT compiler gets confused about which one to use, and often the result is that it includes none of them—or worse, includes conflicting files that can't work together.

## The Solution: Use the Bundle

The solution is `SQLitePCLRaw.bundle_e_sqlite3`—a special NuGet package designed specifically to work with AOT.

Add these two packages to your project:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
  <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.10" />
</ItemGroup>
```

### What Does the Bundle Do?

The `bundle_e_sqlite3` package is different from normal SQLite providers:

**1. It includes pre-compiled native SQLite libraries for every major platform:**
- Windows (x64, x86, ARM64)
- Linux (x64, ARM64, musl-based distros like Alpine)
- macOS (x64 Intel, ARM64 Apple Silicon)

**2. These native libraries are packaged in a way the AOT compiler understands**

The bundle marks its native dependencies explicitly so the AOT compiler knows to include them in the final binary. No dynamic loading, no runtime searching for files.

**3. It's designed for cross-platform builds**

One package works for all platforms. You don't need platform-specific packages or conditional references.

### Why This Works

Remember the two problems we identified?

**Problem 1: AOT's trimming removes libraries it can't see being used**
- Solution: The bundle explicitly declares its native libraries as build assets that must be included

**Problem 2: AOT doesn't support dynamic loading**
- Solution: The bundle statically links the SQLite libraries at compile time

The result: when you build for Windows x64, the bundle includes `e_sqlite3.dll`. When you build for Linux ARM64, it includes the ARM64 `libe_sqlite3.so`. Everything just works.

### Critical: Initialize the Bundle (Don't Skip This!)

Here's the part that trips up 90% of people trying to use SQLite with AOT, including me on my first attempt.

**With normal .NET**, the SQLite bundle automatically initializes itself the first time you use SQLite. Magic happens behind the scenes—you don't need to do anything.

**With Native AOT**, this automatic initialization doesn't work. The AOT compiler can't see the automatic startup code (it looks like unused code and gets trimmed), so you **must manually initialize** the bundle at the very start of your application.

Here's the magic line you need:

```csharp
using SQLitePCL;

public class Program
{
    public static void Main(string[] args)
    {
        // THIS IS CRITICAL: Initialize SQLite FIRST, before ANYTHING else
        SQLitePCL.Batteries.Init();

        // Now you can do normal application setup
        var builder = WebApplication.CreateBuilder(args);

        // This is now safe - SQLite is initialized
        builder.Services.AddDbContext<MyDbContext>(options =>
            options.UseSqlite("Data Source=app.db"));

        var app = builder.Build();
        app.Run();
    }
}
```

### What Does `Batteries.Init()` Do?

This method tells the SQLite bundle to:
1. Find the correct native SQLite library for your current platform
2. Load it into memory
3. Wire up all the connections between `Microsoft.Data.Sqlite` and the native library

It's called "Batteries" because it's the "batteries included" bundle—everything you need is packaged together.

### Where to Put It

Put `Batteries.Init()` as **the very first line** in your `Main` method, before:
- Creating the application builder
- Configuring dependency injection
- Opening any database connections
- Reading configuration files that might use SQLite

Think of it like plugging in a device before trying to turn it on. If you try to use SQLite before calling `Init()`, you'll still get the `DllNotFoundException` even though the DLL is correctly bundled in your application.

### What Happens If You Forget?

If you forget to call `Batteries.Init()`, your app will:
1. Build successfully (the compiler won't warn you)
2. Start running
3. Crash the moment it tries to use SQLite with:

```
DllNotFoundException: Unable to load DLL 'e_sqlite3' or one of its dependencies
```

This is confusing because the DLL **is** bundled in your application—it's just not initialized.

## Complete Project Configuration

Here's a full `.csproj` configured for multi-platform Native AOT with SQLite:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <!-- Native AOT -->
    <PublishAot>true</PublishAot>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>full</TrimMode>
    <InvariantGlobalization>true</InvariantGlobalization>

    <!-- Single File -->
    <PublishSingleFile>true</PublishSingleFile>
    <StripSymbols>true</StripSymbols>

    <!-- Optimization -->
    <OptimizationPreference>Speed</OptimizationPreference>

    <!-- Multi-platform targets -->
    <RuntimeIdentifiers>
      win-x64;win-arm64;linux-x64;linux-arm64;osx-x64;osx-arm64
    </RuntimeIdentifiers>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
    <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.10" />
  </ItemGroup>

</Project>
```

### Key Settings Explained (In Plain English)

Let me break down what each of these settings does:

**`PublishAot=true`**

This is the master switch that enables Native AOT compilation. Without this, you get normal .NET behavior (JIT compilation). With this, you get ahead-of-time compiled native code.

**`PublishTrimmed=true` and `TrimMode=full`**

These tell the compiler to remove any code you're not using. Think of it like cleaning out your garage before moving—why pack things you don't need?

- `PublishTrimmed=true` enables trimming
- `TrimMode=full` means "be aggressive, remove everything you can"

**Warning**: This can break code that uses heavy reflection (like some JSON serializers or ORMs) because the trimmer can't always see what you're using via reflection. We'll cover how to handle this later.

**`InvariantGlobalization=true`**

This removes all culture-specific data from your app—date formats, currency symbols, text sorting rules for different languages. Saves 5-10MB.

Only set this to `true` if your app:
- Only uses English
- Doesn't need culture-specific date/time formatting
- Only uses ordinal (byte-by-byte) string comparisons

If you're building a CLI tool or API gateway that doesn't care about localization, this is free savings. If you're building something that needs to format dates for French users or sort Turkish text correctly, skip this setting.

**`PublishSingleFile=true`**

Bundles everything into one executable file. Instead of having:
```
myapp.exe
myapp.dll
System.Text.Json.dll
... 50 more files
```

You get just:
```
myapp.exe
```

Much easier to deploy.

**`StripSymbols=true`**

Debug symbols help debuggers show you variable names and line numbers when debugging. They're useful during development but add several megabytes to your final binary.

This setting removes them. Your app runs exactly the same, just smaller.

**`OptimizationPreference=Speed`**

This tells the compiler what to prioritize when making decisions:
- `Speed`: Make it fast (slightly larger binaries, but better performance)
- `Size`: Make it small (slightly slower, but minimal binary size)

For most applications, `Speed` is the right choice. The size difference is usually only 2-3MB, but the performance difference can be noticeable.

**`RuntimeIdentifiers`**

This declares which platforms you want to support. It doesn't build them all—it just tells tooling "these are valid targets."

Available identifiers:
- `win-x64`: Windows 64-bit (Intel/AMD)
- `win-arm64`: Windows ARM64 (Surface Pro X, etc.)
- `linux-x64`: Linux 64-bit (Ubuntu, Debian, RHEL, etc.)
- `linux-arm64`: Linux ARM64 (Raspberry Pi 4/5, AWS Graviton)
- `osx-x64`: macOS Intel (older Macs)
- `osx-arm64`: macOS Apple Silicon (M1/M2/M3 Macs)

You build one platform at a time using `dotnet publish -r linux-x64`, for example.

## Building for Multiple Platforms

Remember how I said AOT requires platform-specific builds? You need to compile separately for Windows, Linux x64, Linux ARM64, macOS Intel, and macOS Apple Silicon. Doing this manually would be tedious—but we can automate it.

### What is GitHub Actions?

If you're not familiar, GitHub Actions is a free CI/CD (Continuous Integration/Continuous Deployment) service built into GitHub. It lets you run automated tasks whenever you push code or create a release tag.

Think of it like having a build server that:
1. Watches your GitHub repository
2. When you push a tag like `v1.0.0`
3. It automatically spins up Windows, Linux, and macOS virtual machines
4. Builds your app for all platforms in parallel
5. Creates a GitHub Release with all the binaries attached

All of this runs on GitHub's servers—you don't need to maintain any infrastructure. For open-source projects and small personal projects, it's completely free.

### The Complete Build Workflow

Here's a GitHub Actions workflow that builds for all major platforms automatically:

```yaml
name: Build Native AOT Binaries

on:
  push:
    tags:
      - 'v*'

jobs:
  build-binaries:
    name: Build ${{ matrix.runtime }}
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        include:
          - os: ubuntu-latest
            runtime: linux-x64
            artifact-name: myapp-linux-x64

          - os: ubuntu-latest
            runtime: linux-arm64
            artifact-name: myapp-linux-arm64

          - os: windows-latest
            runtime: win-x64
            artifact-name: myapp-win-x64

          - os: macos-latest
            runtime: osx-x64
            artifact-name: myapp-osx-x64

          - os: macos-latest
            runtime: osx-arm64
            artifact-name: myapp-osx-arm64

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'

    - name: Install ARM64 tools (Linux ARM64 only)
      if: matrix.runtime == 'linux-arm64'
      run: |
        sudo apt-get update
        sudo apt-get install -y clang zlib1g-dev gcc-aarch64-linux-gnu

    - name: Publish
      shell: bash
      run: |
        # Set objcopy for ARM64 cross-compilation
        if [ "${{ matrix.runtime }}" = "linux-arm64" ]; then
          OBJCOPY_PARAM="-p:ObjCopyName=aarch64-linux-gnu-objcopy"
        else
          OBJCOPY_PARAM=""
        fi

        dotnet publish \
          -c Release \
          -r ${{ matrix.runtime }} \
          --self-contained \
          --output ./publish/${{ matrix.runtime }} \
          -p:PublishAot=true \
          -p:PublishTrimmed=true \
          -p:StripSymbols=true \
          $OBJCOPY_PARAM

    - name: Create distribution package
      shell: bash
      run: |
        mkdir -p ./dist
        cd ./publish/${{ matrix.runtime }}

        # Copy the main executable
        cp myapp${{ matrix.file-ext }} ../../dist/

        # CRITICAL: Copy native libraries (SQLite and any other native dependencies)
        # PublishSingleFile bundles .NET code, but native DLLs remain separate
        cp *.dll ../../dist/ 2>/dev/null || true
        cp *.so ../../dist/ 2>/dev/null || true
        cp *.dylib ../../dist/ 2>/dev/null || true

        # Copy config files if needed
        cp ../../appsettings.json ../../dist/ || true

        cd ../../dist

        # Create archive with all files
        if [ "${{ runner.os }}" = "Windows" ]; then
          7z a -tzip ../${{ matrix.artifact-name }}.zip *
        else
          tar czf ../${{ matrix.artifact-name }}.tar.gz *
        fi

    - name: Upload artifact
      uses: actions/upload-artifact@v4
      with:
        name: ${{ matrix.artifact-name }}
        path: |
          ${{ matrix.artifact-name }}.zip
          ${{ matrix.artifact-name }}.tar.gz
        retention-days: 7
        if-no-files-found: ignore
```

### How This Workflow Works (Step by Step)

If the YAML looks intimidating, here's what it does in plain English:

**1. Trigger (`on: push: tags`)**

The workflow runs when you push a Git tag that starts with `v` (like `v1.0.0`, `v2.3.1`). This is the standard way to mark release versions.

**2. Matrix Strategy**

This is the clever part. Instead of writing five separate workflows, we define a **matrix** of builds:
- Each build gets a different `os` (runner machine) and `runtime` (target platform)
- GitHub Actions runs all five builds **in parallel**
- Each one produces an artifact (the compiled binary)

So when you push `v1.0.0`, GitHub simultaneously:
- Spins up an Ubuntu machine to build Linux x64 and ARM64
- Spins up a Windows machine to build Windows x64
- Spins up a macOS machine to build macOS x64 and ARM64

**3. Steps in Each Build**

Each platform build does the same steps:
1. **Checkout code**: Gets your source code from the repository
2. **Setup .NET**: Installs .NET 10 SDK
3. **Install ARM64 tools** (Linux ARM64 only): Installs cross-compilation tools
4. **Publish**: Runs `dotnet publish` with AOT flags for that specific platform
5. **Upload artifact**: Saves the compiled binary so the next job can access it

**4. The Result**

After all builds complete, you have five artifacts (binaries) ready to distribute. You can download them from the Actions run, or use a second job to create a GitHub Release automatically (not shown in this snippet, but easy to add).

### Critical: Native Libraries Are Not Bundled

Here's something that confused me for hours: **`PublishSingleFile=true` only bundles .NET code**. Native libraries like SQLite's `e_sqlite3.dll` stay separate.

This is why the "Create distribution package" step is so important:

```bash
# Copy native libraries - these are NOT included in the main executable
cp *.dll ../../dist/ 2>/dev/null || true    # Windows
cp *.so ../../dist/ 2>/dev/null || true     # Linux
cp *.dylib ../../dist/ 2>/dev/null || true  # macOS
```

The `2>/dev/null || true` part means "if there are no files matching this pattern, don't fail—just continue." This lets the same script work on all platforms.

**What you distribute:**
- Windows: `myapp.exe` + `e_sqlite3.dll` (bundled together in a ZIP)
- Linux: `myapp` + `libe_sqlite3.so` (bundled together in a tar.gz)
- macOS: `myapp` + `libe_sqlite3.dylib` (bundled together in a tar.gz)

Users extract the archive and run the executable. The native SQLite library sits next to the executable, and the bundle finds it automatically at runtime.

### Why Manual Triggering is Useful

Notice the `workflow_dispatch` trigger at the top:

```yaml
on:
  push:
    tags:
      - 'v*'
  workflow_dispatch:
    inputs:
      version:
        description: 'Version to publish'
        required: true
```

This lets you trigger builds manually from GitHub's web interface without pushing a tag. Useful for testing the workflow or creating beta builds.

### ARM64 Cross-Compilation

The Linux ARM64 build requires special attention. You need cross-compilation tools and must specify the correct `objcopy` tool:

```bash
# Install tools
sudo apt-get install gcc-aarch64-linux-gnu binutils-aarch64-linux-gnu

# Build with objcopy specified
dotnet publish \
  -r linux-arm64 \
  -p:PublishAot=true \
  -p:ObjCopyName=aarch64-linux-gnu-objcopy
```

Without the `ObjCopyName` parameter, the linker fails with cryptic errors about unrecognized file formats.

## Real-World Results

Here's what I achieved with my production YARP-based bot detection gateway with middleware and SQLite logging. This is a real project you can [download from GitHub](https://github.com/scottgal/mostlylucid.nugetpackages/tree/main/Mostlylucid.BotDetection.Console).

### Binary Sizes (From Actual GitHub Releases)

These are the **actual file sizes** from my GitHub releases, not theoretical estimates:

| Platform | Executable | SQLite Native DLL | Total Archive Size |
|----------|-----------|-------------------|-------------------|
| Windows x64 | 9.2MB | +1.7MB | **10.9MB** (ZIP) |
| Linux x64 | 10.8MB | +1.6MB | **12.4MB** (tar.gz) |
| Linux ARM64 | 9.9MB | +1.5MB | **11.4MB** (tar.gz) |
| macOS Intel | 11.2MB | +1.8MB | **13.0MB** (tar.gz) |
| macOS ARM64 | 9.8MB | +1.7MB | **11.5MB** (tar.gz) |

Compare this to self-contained .NET 10 deployments at **130-150MB per platform**. We're talking about a **10-12x reduction in size**.

The breakdown:
- Main executable: Your compiled code + .NET runtime (AOT-compiled)
- Native DLL: The SQLite database engine (written in C)

Both files must be distributed together, but they're still dramatically smaller than traditional .NET deployments.

### Startup Performance

Cold start (first request served) on a modest Linux VPS:

- **Self-contained .NET**: ~800ms
- **Native AOT**: ~150ms
- **Improvement**: 81% faster

This matters hugely for:
- Serverless/Lambda functions (you pay per millisecond)
- Containers that scale up/down frequently
- CLI tools where every invocation starts fresh

### Memory Usage

Idle memory (gateway running, no traffic):

- **Self-contained .NET**: 85MB
- **Native AOT**: 42MB
- **Improvement**: 51% reduction

Under load (1000 requests/second):

- **Self-contained .NET**: ~320MB
- **Native AOT**: ~180MB
- **Improvement**: 44% reduction

Lower memory means:
- More containers per host
- Cheaper cloud hosting bills
- Feasibility on resource-constrained devices (Raspberry Pi, IoT)

## Common Pitfalls

### 1. Forgetting `Batteries.Init()`

This is the #1 mistake. Even with the bundle correctly referenced, forgetting to call `SQLitePCL.Batteries.Init()` at the very start of your `Main` method will cause runtime crashes.

**The fix:**
```csharp
public static void Main(string[] args)
{
    SQLitePCL.Batteries.Init(); // FIRST LINE
    // ... rest of your code
}
```

### 2. Not Distributing Native Libraries

This is mistake #2, and it got me twice. `PublishSingleFile=true` does NOT bundle native SQLite DLLs into your executable. You must distribute them alongside your executable.

**What happens if you forget:**
- Your app builds fine
- It runs fine on your dev machine (where SQLite might already be installed)
- It crashes on production with `DllNotFoundException`

**The fix:**

When packaging your release, always include:
```
myapp.exe               # Your main executable
e_sqlite3.dll           # SQLite native library (Windows)
# or libe_sqlite3.so    # SQLite native library (Linux)
# or libe_sqlite3.dylib # SQLite native library (macOS)
```

In your GitHub Actions or deployment scripts:
```bash
# Copy ALL native libraries from the publish directory
cp *.dll ./dist/ 2>/dev/null || true
cp *.so ./dist/ 2>/dev/null || true
cp *.dylib ./dist/ 2>/dev/null || true
```

Users should extract the archive and run the executable. The native library needs to be in the same directory.

### 3. Using `winsqlite3` Provider

You might see recommendations to use `SQLitePCLRaw.provider.winsqlite3` on Windows to use the OS-provided SQLite. Don't. It only works on Windows, requires manual initialization, and breaks cross-platform builds. Stick with `bundle_e_sqlite3`.

### 4. Trim Warnings with EF Core

If you're using Entity Framework Core with SQLite, you'll get trim warnings (IL2026, IL3050). These are usually safe to suppress for EF Core's SQLite provider, but test thoroughly:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);IL2026;IL3050</NoWarn>
</PropertyGroup>
```

Better yet, consider using Dapper or raw ADO.NET with SQLite for AOT applications—they're more AOT-friendly.

### 5. Missing Visual Studio Tools (Windows)

On Windows, Native AOT requires Visual Studio's MSVC linker. If you build outside a Developer Command Prompt, you'll get errors about `vswhere.exe`. GitHub Actions handles this automatically, but for local builds, use:

- Developer Command Prompt for VS 2022
- Developer PowerShell for VS 2022

Or initialize the environment in your build script:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\Launch-VsDevShell.ps1" -Arch amd64
dotnet publish -c Release -r win-x64
```

## When to Use Native AOT

Native AOT is perfect for:

- **CLI tools**: Instant startup matters
- **Microservices and containers**: Smaller images, faster scaling
- **Edge devices**: Raspberry Pi, IoT devices with limited resources
- **Serverless/Lambda**: Cold start time is critical
- **Gateway/proxy applications**: High throughput, low overhead

Avoid AOT for:

- Heavy Entity Framework Core usage (lots of migrations, complex queries)
- Reflection-heavy applications
- Dynamic plugin systems
- Applications that need runtime code generation

## Quick Start Checklist

If you've read this whole post and just want a checklist to follow, here you go:

**1. Add the packages:**
```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.10" />
```

**2. Configure your `.csproj` for AOT:**
```xml
<PublishAot>true</PublishAot>
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>full</TrimMode>
<InvariantGlobalization>true</InvariantGlobalization>
<PublishSingleFile>true</PublishSingleFile>
<StripSymbols>true</StripSymbols>
<OptimizationPreference>Speed</OptimizationPreference>
```

**3. Initialize SQLite at the start of your `Main` method:**
```csharp
SQLitePCL.Batteries.Init();
```

**4. Build for your target platform:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

**5. Test your binary—it should just run with no dependencies!**

## Conclusion

Getting SQLite working with Native AOT isn't obvious, but once you know the magic incantation—`SQLitePCLRaw.bundle_e_sqlite3` + `Batteries.Init()`—it's straightforward. The payoff is substantial: tiny binaries, instant startup, and the ability to deploy to any platform without runtime dependencies.

For anyone building CLI tools, gateways, or edge applications with .NET, Native AOT with SQLite is now a realistic option. The GitHub Actions workflow provided here automates the entire multi-platform build process—just tag a release and you're done.

If you're new to AOT, start small: convert a simple CLI tool or utility first. Get comfortable with the build process, learn what warnings to expect, and understand the limitations. Once you've got the basics down, you can tackle more complex applications.

The .NET ecosystem is increasingly AOT-friendly. Most modern libraries either work out of the box or have clear documentation on AOT support. The future is native, and it's faster than you think.

## Real Deployment Experience

I deploy my AOT-compiled gateway to:
- **Digital Ocean Droplet** (Linux x64) - runs as a systemd service
- **Raspberry Pi 5** (Linux ARM64) - edge deployment for testing
- **Windows Server** (Windows x64) - runs as a Windows Service
- **Docker containers** - both x64 and ARM64 variants

The deployment process is identical everywhere:
1. Download the archive from GitHub Releases
2. Extract it
3. Run the executable

No "install .NET Runtime" step. No dependency hell. No version conflicts. Just extract and run.

For Docker, my `Dockerfile` is embarrassingly simple:

```dockerfile
FROM debian:bookworm-slim

# Copy just the two files we need
COPY minigw /app/minigw
COPY libe_sqlite3.so /app/libe_sqlite3.so

WORKDIR /app
RUN chmod +x minigw

EXPOSE 5000
ENTRYPOINT ["./minigw"]
```

The resulting image is **~130MB** (mostly the base Debian image). A traditional .NET container would be 200-250MB.

## Complete Working Example

Everything I've shown here comes from a real, production-ready project. You can:

- **View the source code**: [Mostlylucid.BotDetection.Console](https://github.com/scottgal/mostlylucid.nugetpackages/tree/main/Mostlylucid.BotDetection.Console)
- **See the GitHub Actions workflow**: [console-gateway-release.yml](https://github.com/scottgal/mostlylucid.nugetpackages/blob/main/.github/workflows/console-gateway-release.yml)
- **Download prebuilt binaries**: [GitHub Releases](https://github.com/scottgal/mostlylucid.nugetpackages/releases)

The project is a minimal YARP reverse proxy with bot detection middleware that logs signatures to SQLite. It demonstrates:
- Native AOT with SQLite
- Multi-platform builds via GitHub Actions
- Native library bundling
- CLI argument parsing
- Structured logging
- Graceful shutdown

Clone it, study it, use it as a template for your own AOT projects.

## Resources

- [Official .NET Native AOT Documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [SQLitePCLRaw GitHub Repository](https://github.com/ericsink/SQLitePCL.raw)
- [Working Example: Mostlylucid Bot Detection Gateway](https://github.com/scottgal/mostlylucid.nugetpackages/tree/main/Mostlylucid.BotDetection.Console)
- [My GitHub Actions Workflow](https://github.com/scottgal/mostlylucid.nugetpackages/blob/main/.github/workflows/console-gateway-release.yml)

Now go build something tiny and fast.
