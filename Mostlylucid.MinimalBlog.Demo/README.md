# MinimalBlog Demo Application

This is a fully functional demo of the mostlylucid.MinimalBlog package.

## What's Included

This demo showcases:

- How to consume MinimalBlog as a package (via ProjectReference, but would be NuGet in production)
- Minimal setup in Program.cs (just two method calls)
- Four sample blog posts demonstrating various features
- Clean configuration with optional features disabled

## Sample Posts

The demo includes four blog posts:

1. **minimalblog-introduction** - The comprehensive article about the project itself
2. **getting-started** - A beginner's guide to creating posts
3. **markdown-tips** - Advanced markdown formatting techniques
4. **why-minimal** - Philosophy of minimal blogging

## Running the Demo

```bash
cd Mostlylucid.MinimalBlog.Demo
dotnet run
```

Then visit `http://localhost:5000` to see the blog in action.

## Key Features Demonstrated

- **Simple Setup** - See Program.cs for minimal configuration
- **Markdown Parsing** - All posts use standard markdown format
- **Categories** - Posts organized by category
- **Caching** - Memory and output caching for performance
- **No MetaWeblog** - API disabled for this demo (can be enabled in options)

## Project Structure

```
Mostlylucid.MinimalBlog.Demo/
├── Program.cs                      # Application entry point
├── appsettings.json                # Configuration
├── Markdown/                       # Blog posts
│   ├── getting-started.md
│   ├── markdown-tips.md
│   ├── minimalblog-introduction.md
│   └── why-minimal.md
└── wwwroot/                        # Static files
    └── images/                     # Blog images (empty in demo)
```

## Customizing

To use this as a starting point for your own blog:

1. Copy this project
2. Replace markdown files in `Markdown/` with your own content
3. Add images to `wwwroot/images/`
4. Modify `Program.cs` configuration as needed
5. Deploy to your hosting platform

## Production Deployment

For production use:

1. Change the project reference to a NuGet package reference:
   ```xml
   <PackageReference Include="Mostlylucid.MinimalBlog" Version="1.0.0" />
   ```

2. Update appsettings.json with production values

3. Enable HTTPS and production error handling

4. Consider enabling MetaWeblog API if you want to use Markdown Monster or other editors

## Learn More

- Read the [MinimalBlog Introduction](/post/minimalblog-introduction) post
- Check out the [source code](https://github.com/scottgal/mostlylucidweb)
- View the [full article](https://mostlylucid.net/blog/minimalblog-introduction) on mostlylucid.net
