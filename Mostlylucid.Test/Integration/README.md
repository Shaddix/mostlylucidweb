# Markdown Pipeline Integration Tests

## Overview

This directory contains end-to-end integration tests for the markdown pipeline using PuppeteerSharp.

The test verifies the complete workflow:
1. Creates a markdown file with all supported features
2. Waits for the file system watcher to detect it
3. Navigates to the blog and verifies the post appears
4. Checks that all markdown features render correctly
5. Waits for translation service to kick off
6. Deletes the file
7. Verifies the post is removed

## Prerequisites

1. **Site must be running locally** - The test requires the site to be accessible at `http://localhost:8080`
2. **Database must be running** - PostgreSQL should be running with the blog database
3. **File system watcher must be enabled** - The markdown directory watcher must be active

## Running the Test

### Step 1: Start the Site

```bash
cd Mostlylucid
dotnet run --launch-profile IntegrationTest
```

The site will start on `http://localhost:8080` (and `https://localhost:7240`) without launching a browser.

Alternatively, you can use the `https` profile if you want the browser to launch:
```bash
dotnet run --launch-profile https
```

### Step 2: Run the Integration Test

In a new terminal:

```bash
# Remove the Skip attribute from the test first
# Or run with explicit filter:
dotnet test Mostlylucid.Test/Mostlylucid.Test.csproj --filter "FullyQualifiedName~MarkdownPipeline_EndToEnd_CreatesAndDeletesPost"
```

### Step 3: Watch the Test Execute

The test will:
- ✅ Launch a visible Chrome browser (set `Headless = true` in code for headless mode)
- ✅ Create a test markdown file in `Mostlylucid/Markdown/`
- ✅ Navigate to the blog list and verify the post appears
- ✅ Click through to the post page
- ✅ Verify all markdown features are rendered correctly
- ✅ Delete the test file
- ✅ Verify the post is removed from the blog

## What the Test Checks

### Markdown Features

The test creates a markdown file with:
- ✅ `<datetime class="hidden">` tag for publish date
- ✅ `<!-- category -- -->` tag for categories
- ✅ `[TOC]` for table of contents
- ✅ **Bold text** and *emphasized text*
- ✅ Code blocks with syntax highlighting
- ✅ Unordered and ordered lists
- ✅ Links
- ✅ Blockquotes
- ✅ Headers (H1, H2, etc.)

### Page Rendering Verification

The test verifies:
- ✅ Post appears in blog list
- ✅ Post page loads successfully
- ✅ Title/H1 tag is present
- ✅ Categories are displayed
- ✅ Bold text is rendered
- ✅ Code blocks are syntax highlighted
- ✅ Lists are properly formatted
- ✅ Blockquotes are styled
- ✅ Table of contents structure exists

### Cleanup Verification

After deletion:
- ✅ Post no longer appears in blog list
- ✅ Post page returns 404
- ✅ Test file and all generated files are removed

## Test Configuration

### Timeout Settings

```csharp
// Page navigation timeout
waitUntil: WaitUntilNavigation.Networkidle2,
Timeout: 30000  // 30 seconds
```

### Wait Delays

- File system watcher: 5 seconds
- Translation service: 10 seconds
- Deletion detection: 5 seconds

### Browser Configuration

```csharp
Headless: false,  // Set to true for CI
DefaultViewport: 1400x1200
```

## Troubleshooting

### Test Fails: "Post not found in blog list"

**Cause:** File system watcher hasn't processed the file yet
**Solution:** Increase wait time after file creation (currently 5 seconds)

### Test Fails: "Cannot find project root"

**Cause:** Test is not being run from the correct directory
**Solution:** Ensure you're running from the solution root where `Mostlylucid.sln` exists

### Test Fails: "Could not connect to Chrome"

**Cause:** Chromium hasn't been downloaded
**Solution:** PuppeteerSharp will auto-download on first run - check internet connection

### Site Not Accessible

**Cause:** Site isn't running on port 8080
**Solution:** Check `launchSettings.json` and ensure site is started with `--launch-profile https`

## Running in CI/CD

This test is **disabled in CI** by the `[Fact(Skip = "...")]` attribute.

To enable for CI:
1. Remove the `Skip` parameter
2. Set `Headless = true` in browser launch options
3. Ensure site is started as part of CI pipeline
4. Add timeout for entire test run (suggested: 2 minutes)

## File Cleanup

The test uses `IDisposable` to ensure cleanup even if the test fails:
- Test markdown file is deleted
- `.hash` file is removed
- All translation files (`{slug}.*.md`) are cleaned up

## Example Output

```
Step 1: Creating test markdown file...
✅ Created: C:\Blog\mostlylucidweb\Mostlylucid\Markdown\integration-test-20251113171500.md

Step 2: Waiting for file system watcher to process file...

Step 3: Navigating to blog list...
✅ Post found in blog list

Step 4: Navigating to post page...

Step 5: Verifying markdown features...
  ✅ Title/H1 tag processed correctly
  ✅ Categories processed correctly
  ✅ Bold text rendered correctly
  ✅ Code blocks rendered correctly
  ✅ Lists rendered correctly
  ✅ Blockquote rendered correctly
  ✅ Headings/TOC structure rendered correctly
✅ All markdown features verified successfully

Step 6: Waiting for translation service to process...

Step 7: Deleting test markdown file...
✅ Deleted: C:\Blog\mostlylucidweb\Mostlylucid\Markdown\integration-test-20251113171500.md

Step 8: Waiting for file system watcher to process deletion...

Step 9: Verifying post is removed from blog list...
✅ Post successfully removed from blog list

Step 10: Verifying post page returns 404...
✅ Post page correctly returns 404

✅ All integration tests passed!
```

## Notes

- Each test run creates a unique file with timestamp to avoid conflicts
- Test is safe to run multiple times
- Browser stays open for 10 seconds at the end for manual inspection (can be adjusted)
- All assertions use XUnit Assert methods for clear failure messages
