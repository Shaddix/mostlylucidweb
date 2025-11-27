# Webpack for ASP.NET Core Developers: A Complete Guide

A practical, no-nonsense guide to integrating Webpack into your ASP.NET Core projects. From basic setup to advanced optimization with code splitting, tree shaking, and integration with Tailwind CSS.

<datetime class="hidden">2025-11-22T12:00</datetime>
<!-- category -- ASP.NET Core, Webpack, JavaScript, Frontend, Tailwind -->

[TOC]

# Introduction

If you're an ASP.NET Core developer looking at modern JavaScript tooling, Webpack can seem intimidating. Terms like "loaders," "plugins," "chunks," and "tree shaking" get thrown around, and the configuration files look like arcane incantations.

Here's the thing: **Webpack is just a bundler**. It takes your JavaScript (and CSS, images, etc.), processes them, and outputs optimized files for the browser. That's it.

This guide walks you through integrating Webpack into an ASP.NET Core project, from zero to production-ready. I'll show you the exact configuration I use on this blog, with explanations of what each part does.

**What you'll learn:**
- Setting up Node.js and npm in an ASP.NET Core project
- Configuring Webpack for development and production
- Using Babel for browser compatibility
- Code splitting with chunks for better performance
- Integrating with Tailwind CSS and DaisyUI
- Hooking Webpack into your .NET build process

**Related posts:**
- [Tailwind CSS & ASP.NET Core](/blog/tailwind) - Setting up Tailwind with ASP.NET Core
- [Modernising Your Frontend Build Pipeline](/blog/modernising-frontend-build-pipeline) - Why modern bundling matters
- [HTMX with ASP.NET Core](/blog/htmxwithaspnetcore) - Using HTMX (which we'll bundle with Webpack)

# Why Webpack?

Before we dive in, let's understand why you'd use Webpack instead of just referencing scripts from a CDN:

| Approach | Pros | Cons |
|----------|------|------|
| **CDN Scripts** | Simple, no build step | No tree shaking, dependency hell, version conflicts |
| **Webpack** | Optimized bundles, tree shaking, code splitting | Build step required, learning curve |

For production sites, Webpack wins. You get:
- **Smaller bundles** - Only include the code you actually use
- **Better caching** - Content-hashed filenames for cache busting
- **Code splitting** - Load code on demand
- **Modern JavaScript** - Use ES modules and transpile for older browsers

# Getting Started

## Step 1: Install Node.js

First, you need Node.js installed on your machine.

1. Download from [nodejs.org](https://nodejs.org/) (LTS version recommended)
2. Verify the installation:

```bash
node -v    # Should show v18.x.x or higher
npm -v     # Should show 9.x.x or higher
```

## Step 2: Initialize npm in Your Project

Navigate to your ASP.NET Core project root (where your `.csproj` lives) and initialize npm:

```bash
cd YourProject
npm init -y
```

This creates a `package.json` file - the manifest for your JavaScript dependencies and build scripts.

## Step 3: Install Webpack

Install Webpack and its CLI as development dependencies:

```bash
npm install --save-dev webpack webpack-cli
```

Your `package.json` now has:

```json
{
  "devDependencies": {
    "webpack": "^5.91.0",
    "webpack-cli": "^5.1.4"
  }
}
```

## Step 4: Create the Directory Structure

Set up a clean separation between source and output:

```
YourProject/
├── src/
│   ├── js/
│   │   └── main.js          # Your entry point
│   └── css/
│       └── main.css         # Your Tailwind/CSS entry
├── wwwroot/
│   ├── js/dist/             # Webpack outputs here
│   └── css/dist/            # Tailwind outputs here
├── package.json
├── webpack.config.js        # Webpack configuration
└── YourProject.csproj
```

# Basic Webpack Configuration

Create `webpack.config.js` in your project root:

```javascript
const path = require('path');

module.exports = {
  // Entry point - where Webpack starts bundling
  entry: './src/js/main.js',

  // Output - where the bundle goes
  output: {
    filename: 'main.js',
    path: path.resolve(__dirname, 'wwwroot/js/dist'),
    clean: true, // Clean the output directory before each build
  },

  // Mode - 'development' or 'production'
  mode: 'development',
};
```

Create a simple `src/js/main.js`:

```javascript
console.log('Webpack is working!');
```

Add build scripts to `package.json`:

```json
{
  "scripts": {
    "dev": "webpack --mode development",
    "build": "webpack --mode production",
    "watch": "webpack --watch --mode development"
  }
}
```

Run your first build:

```bash
npm run dev
```

You should see `wwwroot/js/dist/main.js` created. Reference it in your Razor layout:

```html
<script src="~/js/dist/main.js"></script>
```

# Production Configuration with Babel

For production, you want:
- **Babel** - Transpile modern JavaScript for older browsers
- **Terser** - Minify the output
- **Source maps** - Debug production issues

Install the dependencies:

```bash
npm install --save-dev @babel/core @babel/preset-env babel-loader terser-webpack-plugin
```

Update `webpack.config.js` to a function that accepts environment:

```javascript
const TerserPlugin = require('terser-webpack-plugin');
const path = require('path');

module.exports = (env, argv) => {
    const isProduction = argv.mode === 'production';

    return {
        mode: isProduction ? 'production' : 'development',

        entry: {
            main: './src/js/main.js',
        },

        output: {
            filename: '[name].js',
            path: path.resolve(__dirname, 'wwwroot/js/dist'),
            clean: true,
        },

        module: {
            rules: [
                {
                    test: /\.js$/,
                    exclude: /node_modules/,
                    use: {
                        loader: 'babel-loader',
                        options: {
                            presets: [
                                ['@babel/preset-env', {
                                    targets: '> 0.25%, not dead',
                                    modules: false, // Keep ES modules for tree shaking
                                    useBuiltIns: 'usage',
                                    corejs: 3,
                                }],
                            ],
                        },
                    },
                },
            ],
        },

        optimization: {
            minimize: isProduction,
            minimizer: isProduction ? [
                new TerserPlugin({
                    terserOptions: {
                        ecma: 2020,
                        compress: {
                            drop_console: false,
                            passes: 3,
                        },
                        mangle: true,
                        format: {
                            comments: false,
                        },
                    },
                }),
            ] : [],
        },

        // Source maps for debugging
        devtool: isProduction ? false : 'eval-source-map',
    };
};
```

Also install `core-js` for polyfills:

```bash
npm install core-js
```

# Code Splitting with Chunks

Code splitting is one of Webpack's most powerful features. Instead of one giant bundle, you get smaller chunks that load on demand.

## Why Code Splitting Matters

Consider this scenario:
- Your `main.js` imports highlight.js (200KB), mermaid (500KB), and your app code (50KB)
- Every page loads 750KB of JavaScript
- But most pages only need your app code!

With code splitting:
- `main.js` loads immediately (50KB)
- `highlight.js` loads only on pages with code blocks
- `mermaid.js` loads only on pages with diagrams

## Configuring Chunks

Add the `splitChunks` configuration:

```javascript
optimization: {
    splitChunks: {
        chunks: 'all',           // Split both sync and async chunks
        minSize: 20000,          // Minimum chunk size (20KB)
        maxSize: 100000,         // Maximum chunk size (100KB)
        name: false,             // Let Webpack generate names
    },
    runtimeChunk: {
        name: 'runtime',         // Separate runtime code
    },
    // ... minimizer config
},
```

You also need to update the output to handle chunked filenames:

```javascript
output: {
    filename: '[name].js',
    chunkFilename: '[name].[contenthash].js',  // Content hash for caching
    path: path.resolve(__dirname, 'wwwroot/js/dist'),
    publicPath: '/js/dist/',  // Important for dynamic imports
    clean: true,
},
```

## Dynamic Imports in Your Code

Use dynamic imports to trigger code splitting:

```javascript
// src/js/main.js

// This creates a separate chunk for highlight.js
async function initHighlighting() {
    const hljs = await import('highlight.js');
    hljs.default.highlightAll();
}

// Only load if there are code blocks on the page
if (document.querySelectorAll('pre code').length > 0) {
    initHighlighting();
}

// Mermaid is only loaded when needed
async function initMermaid() {
    const mermaid = await import('mermaid');
    mermaid.default.initialize({ startOnLoad: true });
}

if (document.querySelectorAll('.mermaid').length > 0) {
    initMermaid();
}
```

After building, you'll see multiple files:
```
wwwroot/js/dist/
├── main.js                      # Your entry point
├── runtime.js                   # Webpack runtime
├── 535.abc123.js               # highlight.js chunk
├── 777.def456.js               # mermaid chunk
└── ... other vendor chunks
```

# Using ES Modules

With modern JavaScript, you can use ES modules (`import`/`export`) to organize your code.

## Setting Up ES Module Output

Add the `experiments` section to enable ES module output:

```javascript
output: {
    filename: '[name].js',
    chunkFilename: '[name].[contenthash].js',
    path: path.resolve(__dirname, 'wwwroot/js/dist'),
    publicPath: '/js/dist/',
    module: true,  // Output ES modules
    clean: true,
},
experiments: {
    outputModule: true,  // Enable ES module experiments
},
```

## Organizing Your JavaScript

Create separate modules for different functionality:

```javascript
// src/js/typeahead.js
export function initTypeahead(inputSelector, options) {
    const input = document.querySelector(inputSelector);
    // ... typeahead logic
}

// src/js/comments.js
export function initComments(containerSelector) {
    // ... comment system logic
}

// src/js/main.js
import { initTypeahead } from './typeahead.js';
import { initComments } from './comments.js';

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    initTypeahead('#search-input', { minLength: 2 });
    initComments('#comment-section');
});

// Expose functions globally if needed (for inline scripts or HTMX)
window.mostlylucid = {
    initTypeahead,
    initComments,
};
```

## Loading ES Modules in Razor

Reference your bundle as a module:

```html
<script type="module" src="~/js/dist/main.js"></script>
```

For the chunked files to load correctly, ensure `publicPath` is set correctly in your Webpack config.

# Integrating with Tailwind CSS

Tailwind CSS has its own build process, but you can run it alongside Webpack.

## Install Tailwind

```bash
npm install --save-dev tailwindcss postcss autoprefixer @tailwindcss/typography
npx tailwindcss init
```

## Configure Tailwind

Create `tailwind.config.js`:

```javascript
module.exports = {
    content: [
        "./Views/**/*.cshtml",
        "./src/js/**/*.js",
    ],
    theme: {
        extend: {},
    },
    plugins: [
        require('@tailwindcss/typography'),
        require('daisyui'),  // Optional: DaisyUI components
    ],
};
```

Create `src/css/main.css`:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

/* Your custom styles */
```

## Build Scripts for Both

Update `package.json` to build both Webpack and Tailwind:

```json
{
  "scripts": {
    "dev": "npm-run-all --parallel dev:*",
    "dev:js": "webpack --mode development",
    "dev:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css",

    "watch": "npm-run-all --parallel watch:*",
    "watch:js": "webpack --watch --mode development",
    "watch:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css --watch",

    "build": "npm-run-all --parallel build:*",
    "build:js": "webpack --mode production",
    "build:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css --minify"
  }
}
```

Install `npm-run-all` for parallel script execution:

```bash
npm install --save-dev npm-run-all
```

# Installing Frontend Libraries

One of Webpack's strengths is managing frontend dependencies via npm.

## Common Libraries

```bash
# UI Libraries
npm install alpinejs htmx.org

# Utilities
npm install highlight.js mermaid flatpickr sweetalert2
```

## Importing in Your Code

```javascript
// src/js/main.js
import Alpine from 'alpinejs';
import hljs from 'highlight.js';
import 'htmx.org';  // HTMX attaches itself to window

// Initialize Alpine
window.Alpine = Alpine;
Alpine.start();

// Initialize highlight.js on code blocks
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('pre code').forEach((el) => {
        hljs.highlightElement(el);
    });
});
```

The beauty here is:
- Webpack bundles only the code you import
- Tree shaking removes unused exports
- You get type hints in your IDE
- Version management via `package.json`

# Hooking into MSBuild

To automatically build frontend assets during `dotnet build`:

Add to your `.csproj`:

```xml
<Target Name="NpmInstall" BeforeTargets="BuildFrontend" Condition="!Exists('node_modules')">
  <Exec Command="npm install" />
</Target>

<Target Name="BuildFrontend" BeforeTargets="Build">
  <Exec Command="npm run build" />
</Target>
```

For development builds only:

```xml
<Target Name="DevFrontend" Condition="'$(Configuration)' == 'Debug'" BeforeTargets="Build">
  <Exec Command="npm run dev" />
</Target>
```

# Complete webpack.config.js

Here's the full production-ready configuration I use on this blog:

```javascript
const TerserPlugin = require('terser-webpack-plugin');
const path = require('path');

module.exports = (env, argv) => {
    const isProduction = argv.mode === 'production';

    return {
        mode: isProduction ? 'production' : 'development',

        entry: {
            main: './src/js/main.js',
        },

        output: {
            filename: '[name].js',
            chunkFilename: '[name].[contenthash].js',
            path: path.resolve(__dirname, 'wwwroot/js/dist'),
            publicPath: '/js/dist/',
            module: true,
            clean: true,
        },

        experiments: {
            outputModule: true,
        },

        module: {
            rules: [
                {
                    test: /\.css$/i,
                    use: ['style-loader', 'css-loader'],
                },
                {
                    test: /\.js$/,
                    exclude: /node_modules/,
                    use: {
                        loader: 'babel-loader',
                        options: {
                            presets: [
                                ['@babel/preset-env', {
                                    targets: '> 0.25%, not dead',
                                    modules: false,
                                    useBuiltIns: 'usage',
                                    corejs: 3,
                                }],
                            ],
                        },
                    },
                },
            ],
        },

        resolve: {
            extensions: ['.js'],
        },

        optimization: {
            splitChunks: {
                chunks: 'all',
                minSize: 20000,
                maxSize: 100000,
                name: false,
            },
            runtimeChunk: {
                name: 'runtime',
            },
            minimize: isProduction,
            minimizer: isProduction ? [
                new TerserPlugin({
                    terserOptions: {
                        ecma: 2020,
                        compress: {
                            drop_console: false,
                            passes: 3,
                            toplevel: true,
                            pure_funcs: ['console.info', 'console.debug'],
                        },
                        mangle: {
                            toplevel: true,
                        },
                        format: {
                            comments: false,
                        },
                    },
                    extractComments: false,
                }),
            ] : [],
        },

        devtool: isProduction ? false : 'eval-source-map',

        performance: {
            hints: isProduction ? 'warning' : false,
        },
    };
};
```

# Complete package.json

```json
{
  "name": "yourproject",
  "version": "1.0.0",
  "scripts": {
    "dev": "npm-run-all --parallel dev:*",
    "dev:js": "webpack --mode development",
    "dev:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css",
    "watch": "npm-run-all --parallel watch:*",
    "watch:js": "webpack --watch --mode development",
    "watch:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css --watch",
    "build": "npm-run-all --parallel build:*",
    "build:js": "webpack --mode production",
    "build:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css --minify"
  },
  "devDependencies": {
    "@babel/core": "^7.26.0",
    "@babel/preset-env": "^7.26.0",
    "@tailwindcss/typography": "^0.5.10",
    "autoprefixer": "^10.4.20",
    "babel-loader": "^10.0.0",
    "css-loader": "^7.1.2",
    "daisyui": "^4.12.10",
    "npm-run-all": "^4.1.5",
    "postcss": "^8.5.2",
    "style-loader": "^4.0.0",
    "tailwindcss": "^3.4.17",
    "terser-webpack-plugin": "^5.3.10",
    "webpack": "^5.91.0",
    "webpack-cli": "^5.1.4"
  },
  "dependencies": {
    "alpinejs": "^3.14.9",
    "core-js": "^3.38.0",
    "flatpickr": "^4.6.13",
    "highlight.js": "^11.11.1",
    "htmx.org": "^2.0.4",
    "mermaid": "^11.4.0",
    "sweetalert2": "^11.17.2"
  }
}
```

# Troubleshooting

## "Module not found" Errors

- Check that the package is installed: `npm install <package-name>`
- Verify the import path is correct
- Clear the cache: `rm -rf node_modules/.cache`

## Chunks Not Loading

- Ensure `publicPath` is set correctly in output
- Check browser network tab for 404s
- Verify the runtime chunk is loaded first

## Styles Not Applying

- For CSS imported in JS, ensure `style-loader` and `css-loader` are installed
- For Tailwind, check your `content` paths in `tailwind.config.js`

## Build Slow in Development

- Use `devtool: 'eval-source-map'` instead of full source maps
- Consider `cache: { type: 'filesystem' }` for persistent caching

# Summary

To recap:

1. **Install Node.js and npm** - The foundation for modern JS tooling
2. **Set up Webpack** - Entry, output, and basic configuration
3. **Add Babel** - For browser compatibility
4. **Configure chunks** - For better performance with code splitting
5. **Integrate Tailwind** - Run alongside Webpack with `npm-run-all`
6. **Hook into MSBuild** - Automatic builds with `dotnet build`

This setup gives you a modern, optimized frontend pipeline while keeping everything integrated with your ASP.NET Core workflow.

# Resources

- [Webpack Documentation](https://webpack.js.org/concepts/)
- [Babel Documentation](https://babeljs.io/docs/)
- [Tailwind CSS Installation](https://tailwindcss.com/docs/installation)
- [ASP.NET Core Static Files](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files)

**Related blog posts:**
- [Tailwind CSS & ASP.NET Core](/blog/tailwind)
- [Modernising Your Frontend Build Pipeline](/blog/modernising-frontend-build-pipeline)
- [HTMX with ASP.NET Core](/blog/htmxwithaspnetcore)
- [A Copy Button for Highlight.js](/blog/acopybuttonforhightlightjs)
