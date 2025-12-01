# MinimalBlog Tests

Comprehensive test suite for mostlylucid.MinimalBlog.

## Test Coverage

### MarkdownBlogServiceTests

Tests for the core blog service:

- **Post Loading** - Reading markdown files from disk
- **Metadata Parsing** - Extracting title, categories, and dates
- **Markdown to HTML** - Conversion using Markdig
- **Caching** - Memory cache behavior
- **Filtering** - Category filtering and hidden posts
- **Ordering** - Posts sorted by date (newest first)
- **Edge Cases** - Missing metadata, empty directories, translated files

### MinimalBlogExtensionsTests

Tests for the DI extension methods:

- **Service Registration** - All required services registered
- **Options Configuration** - Default and custom options
- **Singleton Behavior** - Services reused correctly
- **Optional Features** - MetaWeblog API conditional registration

### IntegrationTests

End-to-end tests of the complete system:

- **Service Resolution** - DI container integration
- **Full Pipeline** - Markdown parsing through to HTML output
- **Category System** - Multi-post filtering
- **Ordering** - Chronological sorting
- **Hidden Posts** - Exclusion from listings
- **Caching** - Instance reuse across requests
- **Complex Markdown** - Advanced formatting features

## Running Tests

```bash
cd Mostlylucid.MinimalBlog.Tests
dotnet test
```

## Running with Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Test Organization

Tests follow the AAA pattern:

- **Arrange** - Set up test data and dependencies
- **Act** - Execute the operation being tested
- **Assert** - Verify the expected outcome

Each test is isolated and uses temporary directories for file-based operations.

## Key Testing Patterns

### Temporary Test Data

Tests create temporary markdown files in isolated directories:

```csharp
_testMarkdownPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
Directory.CreateDirectory(_testMarkdownPath);
```

Cleanup happens in Dispose():

```csharp
public void Dispose()
{
    if (Directory.Exists(_testMarkdownPath))
    {
        Directory.Delete(_testMarkdownPath, true);
    }
}
```

### Service Mocking

Tests use real implementations, not mocks, for better integration coverage:

```csharp
var options = new MinimalBlogOptions { MarkdownPath = _testMarkdownPath };
var cache = new MemoryCache(new MemoryCacheOptions());
var service = new MarkdownBlogService(options, cache);
```

### Test Data

Sample markdown files are created programmatically:

```csharp
var markdown = @"# Test Post

<!-- category -- Testing -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
File.WriteAllText(Path.Combine(_testMarkdownPath, "test.md"), markdown);
```

## Test Statistics

- **Total Tests**: 30+
- **Test Projects**: 1
- **Code Coverage**: Targets 80%+ (core logic 90%+)
- **Test Categories**:
  - Unit Tests: 20+
  - Integration Tests: 10+

## Continuous Integration

These tests are designed to run in CI/CD pipelines:

- No external dependencies
- Fast execution (< 5 seconds total)
- Isolated test data
- Deterministic results
- No network calls
- No database requirements

## Contributing

When adding new features:

1. Add unit tests for new functionality
2. Add integration tests for new workflows
3. Ensure all tests pass
4. Maintain 80%+ code coverage
5. Follow existing test patterns
