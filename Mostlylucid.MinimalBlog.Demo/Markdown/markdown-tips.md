# Markdown Tips and Tricks

<!--category-- Tutorial, Markdown -->
<datetime class="hidden">2024-11-28T14:30</datetime>

## Mastering Markdown for Blogging

Markdown is a lightweight markup language that's perfect for writing blog content. This post covers useful markdown features for creating engaging blog posts.

## Basic Formatting

### Text Styling

You have several options for emphasizing text:

- **Bold** for strong emphasis: `**bold**`
- *Italic* for subtle emphasis: `*italic*`
- **_Bold and italic_** combined: `**_both_**`
- ~~Strikethrough~~ for corrections: `~~strikethrough~~`

### Headings

Use headings to structure your content:

```markdown
# H1 - Post Title (use once)
## H2 - Major Sections
### H3 - Subsections
#### H4 - Minor Points
```

## Lists and Structure

### Unordered Lists

- First item
- Second item
  - Nested item
  - Another nested item
- Third item

### Ordered Lists

1. First step
2. Second step
3. Third step
   1. Sub-step A
   2. Sub-step B

### Task Lists

- [x] Completed task
- [ ] Pending task
- [ ] Another pending task

## Code and Technical Content

### Inline Code

Use `backticks` for inline code references like `var x = 10;` or `npm install`.

### Code Blocks

Specify the language for syntax highlighting:

```javascript
function greet(name) {
    console.log(`Hello, ${name}!`);
}

greet('World');
```

```python
def calculate_sum(numbers):
    return sum(numbers)

result = calculate_sum([1, 2, 3, 4, 5])
print(f"Sum: {result}")
```

```sql
SELECT
    users.name,
    COUNT(posts.id) as post_count
FROM users
LEFT JOIN posts ON users.id = posts.user_id
GROUP BY users.name
HAVING post_count > 5;
```

## Links and References

### Basic Links

[Visit Example](https://example.com)

### Reference-Style Links

This is a [reference link][1] and here's [another one][2].

[1]: https://example.com
[2]: https://example.org

### Automatic Links

URLs like https://example.com automatically become links.

## Blockquotes and Callouts

### Simple Blockquotes

> This is a simple blockquote. Use it for quotes, citations, or to highlight important information.

### Nested Blockquotes

> This is the first level
>
> > This is nested
> >
> > > And this is even deeper

### Attributed Quotes

> The only way to do great work is to love what you do.
>
> -- Steve Jobs

## Tables

Tables are great for structured data:

| Feature | Minimal Blog | Full Platform |
|---------|-------------|---------------|
| Database | No | PostgreSQL |
| Caching | Memory | Multi-layer |
| Search | Categories | Full-text |
| Lines of Code | ~500 | ~50,000+ |

## Horizontal Rules

Separate sections with horizontal rules:

---

Content above the line.

Content below the line.

## Images

While this demo doesn't include images, the syntax is:

```markdown
![Alt text](image-filename.jpg)
```

Images go in the `wwwroot/images` directory and are automatically served at `/images/filename.jpg`.

## Special Characters

Use backslash to escape special characters:

- \* Not a bullet point
- \# Not a heading
- \[Not a link\]

## Best Practices

### For Readability

1. **Use headings hierarchically** - Don't skip levels
2. **Keep paragraphs short** - 3-4 sentences max
3. **Add whitespace** - Blank lines between sections
4. **Use lists** - Easier to scan than paragraphs

### For SEO

1. **Start with H1** - One per post, descriptive
2. **Use descriptive link text** - Not "click here"
3. **Add alt text to images** - Accessibility and SEO
4. **Structure with headings** - Helps search engines

### For Engagement

1. **Start strong** - Hook readers in the first paragraph
2. **Use examples** - Code, scenarios, real-world cases
3. **Break up text** - Images, code blocks, quotes
4. **End with action** - Next steps, related posts

## Advanced Tips

### Code Block Highlighting

Most markdown processors support language-specific syntax highlighting. MinimalBlog uses Markdig with syntax highlighting support.

### Link to Other Posts

Link to other posts using relative URLs:

```markdown
Check out [Getting Started](/post/getting-started) for basics.
```

### Hidden Content

Use the `<hidden` marker to keep posts from appearing in listings:

```markdown
<hidden>This post won't show up</hidden>
```

## Markdown Resources

- [Markdown Guide](https://www.markdownguide.org/) - Comprehensive reference
- [CommonMark Spec](https://commonmark.org/) - Standard specification
- [Markdig](https://github.com/xoofx/markdig) - The parser MinimalBlog uses

## Conclusion

Markdown is simple but powerful. With these techniques, you can create professional, readable blog content without HTML complexity.

Start simple, add complexity as needed, and focus on writing great content!
