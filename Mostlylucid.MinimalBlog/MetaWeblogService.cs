using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Mostlylucid.MinimalBlog;

public partial class MetaWeblogService(MinimalBlogOptions options, ILogger<MetaWeblogService> logger)
{
    private readonly string _markdownPath = options.MarkdownPath
        ?? throw new InvalidOperationException("MarkdownPath not configured");
    private readonly string _imagesPath = options.ImagesPath;
    private readonly string _username = options.MetaWeblogUsername;
    private readonly string _password = options.MetaWeblogPassword;
    private readonly string _blogUrl = options.BlogUrl;

    public async Task<string> HandleRequestAsync(Stream requestBody)
    {
        using var reader = new StreamReader(requestBody);
        var xml = await reader.ReadToEndAsync();
        var doc = XDocument.Parse(xml);

        var methodName = doc.Descendants("methodName").FirstOrDefault()?.Value;
        var parameters = doc.Descendants("param").Select(ParseValue).ToList();

        logger.LogInformation("MetaWeblog API call: {Method}", methodName);

        return methodName switch
        {
            "blogger.getUsersBlogs" => GetUsersBlogs(parameters),
            "metaWeblog.getRecentPosts" => GetRecentPosts(parameters),
            "metaWeblog.getPost" => GetPost(parameters),
            "metaWeblog.newPost" => NewPost(parameters),
            "metaWeblog.editPost" => EditPost(parameters),
            "metaWeblog.deletePost" => DeletePost(parameters),
            "metaWeblog.newMediaObject" => await NewMediaObject(parameters),
            "wp.getCategories" or "metaWeblog.getCategories" => GetCategories(),
            _ => Fault(0, $"Unknown method: {methodName}")
        };
    }

    private bool ValidateCredentials(object? username, object? password)
    {
        return username?.ToString() == _username && password?.ToString() == _password;
    }

    private string GetUsersBlogs(List<object?> p)
    {
        if (!ValidateCredentials(p.ElementAtOrDefault(1), p.ElementAtOrDefault(2)))
            return Fault(401, "Invalid credentials");

        return Response($@"
            <array><data><value><struct>
                <member><name>blogid</name><value><string>1</string></value></member>
                <member><name>blogName</name><value><string>Minimal Blog</string></value></member>
                <member><name>url</name><value><string>{_blogUrl}</string></value></member>
            </struct></value></data></array>");
    }

    private string GetRecentPosts(List<object?> p)
    {
        if (!ValidateCredentials(p.ElementAtOrDefault(1), p.ElementAtOrDefault(2)))
            return Fault(401, "Invalid credentials");

        var count = Convert.ToInt32(p.ElementAtOrDefault(3) ?? 10);
        var posts = GetAllMarkdownFiles().Take(count);

        var postsXml = new StringBuilder("<array><data>");
        foreach (var file in posts)
        {
            var (title, categories, content) = ParseMarkdownFile(file);
            var slug = Path.GetFileNameWithoutExtension(file);
            postsXml.Append(PostStruct(slug, title, content, categories, File.GetLastWriteTimeUtc(file)));
        }
        postsXml.Append("</data></array>");

        return Response(postsXml.ToString());
    }

    private string GetPost(List<object?> p)
    {
        var postId = p.ElementAtOrDefault(0)?.ToString();
        if (!ValidateCredentials(p.ElementAtOrDefault(1), p.ElementAtOrDefault(2)))
            return Fault(401, "Invalid credentials");

        var filePath = Path.Combine(_markdownPath, $"{postId}.md");
        if (!File.Exists(filePath))
            return Fault(404, "Post not found");

        var (title, categories, content) = ParseMarkdownFile(filePath);
        return Response(PostStruct(postId!, title, content, categories, File.GetLastWriteTimeUtc(filePath)));
    }

    private string NewPost(List<object?> p)
    {
        if (!ValidateCredentials(p.ElementAtOrDefault(1), p.ElementAtOrDefault(2)))
            return Fault(401, "Invalid credentials");

        if (p.ElementAtOrDefault(3) is not Dictionary<string, object?> post)
            return Fault(400, "Invalid post data");

        var title = post.GetValueOrDefault("title")?.ToString() ?? "Untitled";
        var content = post.GetValueOrDefault("description")?.ToString() ?? "";
        var categories = (post.GetValueOrDefault("categories") as List<object?>)?
            .Select(c => c?.ToString() ?? "").Where(c => !string.IsNullOrEmpty(c)).ToArray() ?? [];

        var slug = GenerateSlug(title);
        var markdown = BuildMarkdown(title, categories, content);

        var filePath = Path.Combine(_markdownPath, $"{slug}.md");
        File.WriteAllText(filePath, markdown);

        logger.LogInformation("Created new post: {Slug}", slug);
        return Response($"<string>{slug}</string>");
    }

    private string EditPost(List<object?> p)
    {
        var postId = p.ElementAtOrDefault(0)?.ToString();
        if (!ValidateCredentials(p.ElementAtOrDefault(1), p.ElementAtOrDefault(2)))
            return Fault(401, "Invalid credentials");

        if (p.ElementAtOrDefault(3) is not Dictionary<string, object?> post)
            return Fault(400, "Invalid post data");

        var filePath = Path.Combine(_markdownPath, $"{postId}.md");
        if (!File.Exists(filePath))
            return Fault(404, "Post not found");

        var title = post.GetValueOrDefault("title")?.ToString() ?? "Untitled";
        var content = post.GetValueOrDefault("description")?.ToString() ?? "";
        var categories = (post.GetValueOrDefault("categories") as List<object?>)?
            .Select(c => c?.ToString() ?? "").Where(c => !string.IsNullOrEmpty(c)).ToArray() ?? [];

        var markdown = BuildMarkdown(title, categories, content);
        File.WriteAllText(filePath, markdown);

        logger.LogInformation("Updated post: {PostId}", postId);
        return Response("<boolean>1</boolean>");
    }

    private string DeletePost(List<object?> p)
    {
        var postId = p.ElementAtOrDefault(1)?.ToString();
        if (!ValidateCredentials(p.ElementAtOrDefault(2), p.ElementAtOrDefault(3)))
            return Fault(401, "Invalid credentials");

        var filePath = Path.Combine(_markdownPath, $"{postId}.md");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            logger.LogInformation("Deleted post: {PostId}", postId);
        }

        return Response("<boolean>1</boolean>");
    }

    private async Task<string> NewMediaObject(List<object?> p)
    {
        if (!ValidateCredentials(p.ElementAtOrDefault(1), p.ElementAtOrDefault(2)))
            return Fault(401, "Invalid credentials");

        if (p.ElementAtOrDefault(3) is not Dictionary<string, object?> media)
            return Fault(400, "Invalid media data");

        var name = media.GetValueOrDefault("name")?.ToString() ?? $"{Guid.NewGuid()}.png";
        var bits = media.GetValueOrDefault("bits") as byte[];

        if (bits == null || bits.Length == 0)
            return Fault(400, "No media data");

        // Ensure images directory exists
        var imagesDir = Path.IsPathRooted(_imagesPath) ? _imagesPath : Path.Combine(Directory.GetCurrentDirectory(), _imagesPath);
        Directory.CreateDirectory(imagesDir);

        // Sanitize filename
        var fileName = Path.GetFileName(name);
        var filePath = Path.Combine(imagesDir, fileName);

        await File.WriteAllBytesAsync(filePath, bits);
        logger.LogInformation("Uploaded media: {FileName} ({Bytes} bytes)", fileName, bits.Length);

        var url = $"{_blogUrl}/images/{fileName}";
        return Response($@"
            <struct>
                <member><name>url</name><value><string>{url}</string></value></member>
                <member><name>file</name><value><string>{fileName}</string></value></member>
            </struct>");
    }

    private string GetCategories()
    {
        var categories = GetAllMarkdownFiles()
            .SelectMany(f => ParseMarkdownFile(f).Categories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c);

        var xml = new StringBuilder("<array><data>");
        foreach (var cat in categories)
        {
            xml.Append($@"
                <value><struct>
                    <member><name>categoryId</name><value><string>{cat}</string></value></member>
                    <member><name>categoryName</name><value><string>{cat}</string></value></member>
                </struct></value>");
        }
        xml.Append("</data></array>");

        return Response(xml.ToString());
    }

    private IEnumerable<string> GetAllMarkdownFiles()
    {
        if (!Directory.Exists(_markdownPath)) yield break;

        foreach (var file in Directory.GetFiles(_markdownPath, "*.md", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetFileName(f).Count(c => c == '.') == 1)
            .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            yield return file;
        }
    }

    private (string Title, string[] Categories, string Content) ParseMarkdownFile(string path)
    {
        var markdown = File.ReadAllText(path);

        // Extract title from first H1
        var titleMatch = TitleRegex().Match(markdown);
        var title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(path);

        // Extract categories
        var catMatch = CategoryRegex().Match(markdown);
        var categories = catMatch.Success
            ? catMatch.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries)
            : [];

        // Remove metadata for content (keep raw markdown for editing)
        return (title, categories, markdown);
    }

    private static string BuildMarkdown(string title, string[] categories, string content)
    {
        var sb = new StringBuilder();

        // Check if content already has a title
        if (!content.TrimStart().StartsWith("# "))
        {
            sb.AppendLine($"# {title}");
            sb.AppendLine();
        }

        // Add category if not already present
        if (categories.Length > 0 && !content.Contains("<!-- category"))
        {
            sb.AppendLine($"<!-- category -- {string.Join(", ", categories)} -->");
            sb.AppendLine();
        }

        // Add datetime if not present
        if (!content.Contains("<datetime"))
        {
            sb.AppendLine($"<datetime class=\"hidden\">{DateTime.UtcNow:yyyy-MM-ddTHH:mm}</datetime>");
            sb.AppendLine();
        }

        sb.Append(content);
        return sb.ToString();
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = SlugCleanRegex().Replace(slug, "-");
        slug = SlugDashRegex().Replace(slug, "-");
        return slug.Trim('-');
    }

    private static string PostStruct(string id, string title, string content, string[] categories, DateTime date)
    {
        var catsXml = string.Join("", categories.Select(c => $"<value><string>{Escape(c)}</string></value>"));
        return $@"
            <value><struct>
                <member><name>postid</name><value><string>{Escape(id)}</string></value></member>
                <member><name>title</name><value><string>{Escape(title)}</string></value></member>
                <member><name>description</name><value><string>{Escape(content)}</string></value></member>
                <member><name>dateCreated</name><value><dateTime.iso8601>{date:yyyyMMddTHH:mm:ss}</dateTime.iso8601></value></member>
                <member><name>categories</name><value><array><data>{catsXml}</data></array></value></member>
                <member><name>link</name><value><string>/post/{Escape(id)}</string></value></member>
            </struct></value>";
    }

    private static string Response(string value) =>
        $"<?xml version=\"1.0\"?><methodResponse><params><param><value>{value}</value></param></params></methodResponse>";

    private static string Fault(int code, string message) =>
        $"<?xml version=\"1.0\"?><methodResponse><fault><value><struct><member><name>faultCode</name><value><int>{code}</int></value></member><member><name>faultString</name><value><string>{Escape(message)}</string></value></member></struct></value></fault></methodResponse>";

    private static string Escape(string s) => System.Security.SecurityElement.Escape(s) ?? s;

    private static object? ParseValue(XElement param)
    {
        var value = param.Descendants("value").FirstOrDefault();
        return ParseValueElement(value);
    }

    private static object? ParseValueElement(XElement? value)
    {
        if (value == null) return null;

        var child = value.Elements().FirstOrDefault();
        if (child == null) return value.Value;

        return child.Name.LocalName switch
        {
            "string" => child.Value,
            "int" or "i4" => int.TryParse(child.Value, out var i) ? i : 0,
            "boolean" => child.Value == "1",
            "double" => double.TryParse(child.Value, out var d) ? d : 0,
            "base64" => Convert.FromBase64String(child.Value),
            "dateTime.iso8601" => DateTime.TryParse(child.Value, out var dt) ? dt : DateTime.MinValue,
            "struct" => child.Elements("member").ToDictionary(
                m => m.Element("name")?.Value ?? "",
                m => ParseValueElement(m.Element("value"))),
            "array" => child.Descendants("value").Select(ParseValueElement).ToList(),
            _ => child.Value
        };
    }

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<!--\s*category\s*--\s*(.+?)\s*-->", RegexOptions.IgnoreCase)]
    private static partial Regex CategoryRegex();

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex SlugCleanRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex SlugDashRegex();
}
