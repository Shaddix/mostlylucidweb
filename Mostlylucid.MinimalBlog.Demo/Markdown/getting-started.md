# Getting Started with MinimalBlog

<!--category-- Tutorial, Getting Started -->
<datetime class="hidden">2024-11-29T10:00</datetime>

## Welcome to Your New Blog

Congratulations on setting up your MinimalBlog instance! This post will guide you through the basics of creating and managing content.

## Creating Your First Post

Creating a blog post is as simple as creating a markdown file. Here's what you need:

### 1. Create a Markdown File

Create a new `.md` file in your `Markdown` directory. The filename becomes the URL slug, so `getting-started.md` becomes `/post/getting-started`.

### 2. Add Metadata

Every post needs three pieces of metadata:

```markdown
# Your Post Title

<!-- category -- Category1, Category2 -->
<datetime class="hidden">2024-11-29T10:00</datetime>
```

- **Title**: The first H1 heading (`# Title`)
- **Categories**: HTML comment with comma-separated categories
- **Date**: Hidden datetime element in ISO 8601 format

### 3. Write Your Content

Use standard markdown syntax for your content:

- **Bold text** with `**bold**`
- *Italic text* with `*italic*`
- [Links](https://example.com) with `[text](url)`
- `Code` with backticks
- Lists like this one

#### Code Blocks

```csharp
public class Example
{
    public string Name { get; set; }
    public int Value { get; set; }
}
```

#### Blockquotes

> This is a blockquote. Use it for callouts, quotes, or highlighting important information.

### 4. Save and Refresh

Save your file and the blog will automatically pick it up (within 30 minutes due to caching, or restart the app for immediate updates).

## Organizing with Categories

Categories help organize your content. Use them wisely:

- Keep category names consistent
- Use 1-3 categories per post
- Categories are case-insensitive but displayed as you write them

## Next Steps

- Read the [MinimalBlog Introduction](/post/minimalblog-introduction) for architectural details
- Check out [Markdown Tips](/post/markdown-tips) for advanced formatting
- Start writing!

## Tips for Success

1. **Keep filenames simple** - Use lowercase, hyphens for spaces
2. **Use descriptive titles** - Your H1 becomes the page title
3. **Add dates** - Helps with chronological ordering
4. **Write consistently** - Regular posting builds an audience

Happy blogging!
