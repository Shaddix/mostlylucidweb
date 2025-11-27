using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mostlylucid.SemanticSearch.Config;
using Mostlylucid.SemanticSearch.Extensions;
using Mostlylucid.SemanticSearch.Models;
using Mostlylucid.SemanticSearch.Services;
using System.Text.RegularExpressions;

namespace Mostlylucid.SemanticSearch.Demo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Semantic Search Demo ===\n");

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add configuration
                var config = context.Configuration.GetSection("SemanticSearch").Get<SemanticSearchConfig>()
                    ?? new SemanticSearchConfig();

                // Fix model paths to be absolute
                var baseDir = Directory.GetCurrentDirectory();
                var modelsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "Mostlylucid", "models"));

                if (config.EmbeddingModelPath.StartsWith("../"))
                {
                    config.EmbeddingModelPath = Path.Combine(modelsDir, "all-MiniLM-L6-v2.onnx");
                }
                if (config.VocabPath.StartsWith("../"))
                {
                    config.VocabPath = Path.Combine(modelsDir, "vocab.txt");
                }

                services.AddSingleton(config);

                // Add semantic search services
                services.AddSemanticSearch(context.Configuration);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        var searchService = host.Services.GetRequiredService<ISemanticSearchService>();

        // Show menu
        while (true)
        {
            Console.WriteLine("\nChoose an option:");
            Console.WriteLine("1. Initialize/Reset collection");
            Console.WriteLine("2. Index markdown files from directory");
            Console.WriteLine("3. Search for blog posts");
            Console.WriteLine("4. Find related posts");
            Console.WriteLine("5. Add sample test documents");
            Console.WriteLine("6. Exit");
            Console.Write("\nEnter choice (1-6): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await InitializeCollection(searchService);
                    break;
                case "2":
                    await IndexDirectory(searchService);
                    break;
                case "3":
                    await SearchPosts(searchService);
                    break;
                case "4":
                    await FindRelated(searchService);
                    break;
                case "5":
                    await AddSampleDocuments(searchService);
                    break;
                case "6":
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid choice. Try again.");
                    break;
            }
        }
    }

    static async Task InitializeCollection(ISemanticSearchService searchService)
    {
        Console.WriteLine("\nInitializing collection...");
        await searchService.InitializeAsync();
        Console.WriteLine("✓ Collection initialized successfully");
    }

    static async Task IndexDirectory(ISemanticSearchService searchService)
    {
        Console.Write("\nEnter directory path (or press Enter for C:\\Blog\\mostlylucidweb\\Mostlylucid\\Markdown): ");
        var directory = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(directory))
        {
            // Default to Mostlylucid/Markdown folder - use absolute path from solution root
            directory = @"C:\Blog\mostlylucidweb\Mostlylucid\Markdown";
        }

        if (!Directory.Exists(directory))
        {
            Console.WriteLine($"Directory not found: {directory}");
            return;
        }

        Console.WriteLine($"\nScanning {directory}...");

        var mdFiles = Directory.GetFiles(directory, "*.md", SearchOption.TopDirectoryOnly);
        Console.WriteLine($"Found {mdFiles.Length} markdown files");

        if (mdFiles.Length == 0)
        {
            Console.WriteLine("No markdown files found.");
            return;
        }

        Console.Write($"Index all {mdFiles.Length} files? (y/n): ");
        if (Console.ReadLine()?.ToLower() != "y")
        {
            return;
        }

        var documents = new List<BlogPostDocument>();
        var indexed = 0;
        var failed = 0;

        foreach (var file in mdFiles)
        {
            try
            {
                var doc = ParseMarkdownFile(file);
                if (doc != null)
                {
                    documents.Add(doc);
                    indexed++;

                    if (indexed % 10 == 0)
                    {
                        Console.WriteLine($"Parsed {indexed}/{mdFiles.Length} files...");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse {Path.GetFileName(file)}: {ex.Message}");
                failed++;
            }
        }

        if (documents.Count > 0)
        {
            Console.WriteLine($"\nIndexing {documents.Count} documents...");
            await searchService.IndexPostsAsync(documents);
            Console.WriteLine($"✓ Successfully indexed {indexed} documents");

            if (failed > 0)
            {
                Console.WriteLine($"⚠ Failed to parse {failed} files");
            }
        }
    }

    static BlogPostDocument? ParseMarkdownFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Extract title (first # heading)
        var titleMatch = Regex.Match(content, @"^#\s+(.+)$", RegexOptions.Multiline);
        var title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : fileName;

        // Extract categories from HTML comment
        var categoriesMatch = Regex.Match(content, @"<!--\s*category\s*--\s*(.+?)\s*-->", RegexOptions.IgnoreCase);
        var categories = new List<string>();
        if (categoriesMatch.Success)
        {
            categories = categoriesMatch.Groups[1].Value
                .Split(',')
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();
        }

        // Extract published date
        var dateMatch = Regex.Match(content, @"<datetime[^>]*>(.+?)</datetime>", RegexOptions.IgnoreCase);
        var publishedDate = DateTime.UtcNow;
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var parsed))
        {
            publishedDate = parsed;
        }

        // Clean content (remove HTML comments, datetime tags, etc.)
        var cleanContent = Regex.Replace(content, @"<!--.*?-->", "", RegexOptions.Singleline);
        cleanContent = Regex.Replace(cleanContent, @"<datetime[^>]*>.*?</datetime>", "", RegexOptions.IgnoreCase);
        cleanContent = Regex.Replace(cleanContent, @"<[^>]+>", ""); // Remove HTML tags
        cleanContent = Regex.Replace(cleanContent, @"```[\s\S]*?```", ""); // Remove code blocks
        cleanContent = Regex.Replace(cleanContent, @"\[TOC\]", "", RegexOptions.IgnoreCase);

        // Determine language from filename (e.g., file.es.md, file.fr.md)
        var langMatch = Regex.Match(fileName, @"\.([a-z]{2})$");
        var language = langMatch.Success ? langMatch.Groups[1].Value : "en";

        // Remove language suffix from slug
        var slug = langMatch.Success ? fileName.Substring(0, fileName.Length - 3) : fileName;

        return new BlogPostDocument
        {
            Id = $"{slug}_{language}",
            Slug = slug,
            Title = title,
            Content = cleanContent.Trim(),
            Language = language,
            Categories = categories,
            PublishedDate = publishedDate,
            ContentHash = null // Will be computed by the service
        };
    }

    static async Task SearchPosts(ISemanticSearchService searchService)
    {
        Console.Write("\nEnter search query: ");
        var query = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("Query cannot be empty.");
            return;
        }

        Console.Write("Number of results (default 10): ");
        var limitStr = Console.ReadLine();
        var limit = int.TryParse(limitStr, out var l) ? l : 10;

        Console.WriteLine($"\nSearching for: '{query}'...\n");

        var results = await searchService.SearchAsync(query, limit);

        if (results.Count == 0)
        {
            Console.WriteLine("No results found.");
            return;
        }

        Console.WriteLine($"Found {results.Count} results:\n");

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            Console.WriteLine($"{i + 1}. {result.Title}");
            Console.WriteLine($"   Slug: {result.Slug}");
            Console.WriteLine($"   Language: {result.Language}");
            Console.WriteLine($"   Score: {result.Score:F4}");
            Console.WriteLine($"   Categories: {string.Join(", ", result.Categories)}");
            Console.WriteLine($"   Published: {result.PublishedDate:yyyy-MM-dd}");
            Console.WriteLine();
        }
    }

    static async Task FindRelated(ISemanticSearchService searchService)
    {
        Console.Write("\nEnter blog post slug: ");
        var slug = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(slug))
        {
            Console.WriteLine("Slug cannot be empty.");
            return;
        }

        Console.Write("Enter language (default 'en'): ");
        var language = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(language))
        {
            language = "en";
        }

        Console.Write("Number of related posts (default 5): ");
        var limitStr = Console.ReadLine();
        var limit = int.TryParse(limitStr, out var l) ? l : 5;

        Console.WriteLine($"\nFinding related posts for '{slug}' ({language})...\n");

        var results = await searchService.GetRelatedPostsAsync(slug, language, limit);

        if (results.Count == 0)
        {
            Console.WriteLine("No related posts found.");
            return;
        }

        Console.WriteLine($"Found {results.Count} related posts:\n");

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            Console.WriteLine($"{i + 1}. {result.Title}");
            Console.WriteLine($"   Slug: {result.Slug}");
            Console.WriteLine($"   Similarity: {result.Score:F4}");
            Console.WriteLine($"   Categories: {string.Join(", ", result.Categories)}");
            Console.WriteLine();
        }
    }

    static async Task AddSampleDocuments(ISemanticSearchService searchService)
    {
        Console.WriteLine("\nAdding sample test documents...");

        var samples = new List<BlogPostDocument>
        {
            new BlogPostDocument
            {
                Id = "sample-semantic-search_en",
                Slug = "sample-semantic-search",
                Title = "Introduction to Semantic Search",
                Content = "Semantic search uses natural language processing and machine learning to understand the meaning behind search queries. Unlike traditional keyword-based search, semantic search considers context, synonyms, and user intent to deliver more relevant results.",
                Language = "en",
                Categories = new List<string> { "AI", "Search", "NLP" },
                PublishedDate = DateTime.UtcNow.AddDays(-10)
            },
            new BlogPostDocument
            {
                Id = "sample-vector-databases_en",
                Slug = "sample-vector-databases",
                Title = "Understanding Vector Databases",
                Content = "Vector databases store data as high-dimensional vectors (embeddings) that represent the semantic meaning of content. They enable similarity search, where you can find items that are conceptually similar rather than exact matches. Popular vector databases include Qdrant, Pinecone, and Weaviate.",
                Language = "en",
                Categories = new List<string> { "Databases", "AI", "Vectors" },
                PublishedDate = DateTime.UtcNow.AddDays(-5)
            },
            new BlogPostDocument
            {
                Id = "sample-embeddings_en",
                Slug = "sample-embeddings",
                Title = "What are Text Embeddings?",
                Content = "Text embeddings are numerical representations of text that capture semantic meaning. Models like BERT, Sentence Transformers, and GPT convert text into vectors where similar meanings result in similar vectors. These embeddings power modern NLP applications including semantic search, recommendation systems, and content classification.",
                Language = "en",
                Categories = new List<string> { "AI", "NLP", "Machine Learning" },
                PublishedDate = DateTime.UtcNow.AddDays(-3)
            },
            new BlogPostDocument
            {
                Id = "sample-aspnet-core_en",
                Slug = "sample-aspnet-core",
                Title = "Building Web Apps with ASP.NET Core",
                Content = "ASP.NET Core is a cross-platform framework for building modern web applications. It offers high performance, dependency injection out of the box, and supports both MVC and Razor Pages patterns. You can build RESTful APIs, web applications, and microservices with ease.",
                Language = "en",
                Categories = new List<string> { "ASP.NET", "Web Development", "C#" },
                PublishedDate = DateTime.UtcNow.AddDays(-7)
            },
            new BlogPostDocument
            {
                Id = "sample-docker_en",
                Slug = "sample-docker",
                Title = "Containerizing Applications with Docker",
                Content = "Docker enables you to package applications and their dependencies into containers that run consistently across different environments. Containers are lightweight, portable, and isolated, making deployment and scaling easier. Docker Compose helps orchestrate multi-container applications.",
                Language = "en",
                Categories = new List<string> { "Docker", "DevOps", "Containers" },
                PublishedDate = DateTime.UtcNow.AddDays(-1)
            }
        };

        await searchService.IndexPostsAsync(samples);
        Console.WriteLine($"✓ Added {samples.Count} sample documents");
        Console.WriteLine("\nYou can now try searching for:");
        Console.WriteLine("  - 'how does similarity search work'");
        Console.WriteLine("  - 'databases for AI applications'");
        Console.WriteLine("  - 'deploying applications'");
    }
}
