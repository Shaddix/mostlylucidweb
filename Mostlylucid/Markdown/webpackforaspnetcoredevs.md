# Webpack for ASP.NET Core Devs



## Introduction

Webpack is an essential tool for modern web development, especially when working with ASP.NET Core. It helps bundle and optimize JavaScript, CSS, and other assets, making dependency management and performance optimization much easier.

I’ll admit — I’m not a Webpack guru — but I’ve worked through the setup enough times to write this post to help other .NET developers integrate Webpack smoothly into their workflow.

---

## Getting Started with Webpack

First, you need to get Node.js and Webpack installed and configured in your ASP.NET Core project.

###  Step 1: Install Node.js

1. Download and install Node.js from the [official Node.js website](https://nodejs.org/).
2. Open a terminal and verify that Node is installed:

   ```bash
   node -v
   ```

3. `npm`, the Node Package Manager, is included with Node. Verify that it’s available:

   ```bash
   npm -v
   ```

If either command fails, ensure that Node.js is correctly added to your system PATH.

---

###  Step 2: Initialize npm in Your Project

In your ASP.NET Core project root (where your `.csproj` file is located), run:

```bash
npm init -y
```

This creates a `package.json` file — the configuration file used to track project dependencies and scripts.

---

###  Step 3: Install Webpack and Webpack CLI

Next, install Webpack and its command-line interface (CLI) as development dependencies:

```bash
npm install --save-dev webpack webpack-cli
```

This adds them to your `package.json` under `devDependencies`.

---

###  Step 4: (Optional) Install Webpack Dev Server

If you want live reloading and faster development iteration, you can also install Webpack Dev Server:

```bash
npm install --save-dev webpack-dev-server
```

You can now use it like this:

```bash
npx webpack serve --mode development
```

This runs a development server and rebuilds your assets automatically on changes.

---

## `package.json` 

Here’s a simple example of scripts in `package.json` that you can use to control builds:

```json
"scripts": {
  "dev": "webpack --mode development",
  "build": "webpack --mode production",
  "watch": "webpack --watch"
}
```

Then run these from the terminal:

```bash
npm run dev     # development build
npm run build   # production build
npm run watch   # auto-rebuild on file changes
```

---

You can also get more complex with this, for example mine builds tailwind, runs webpack etc


```json
  "scripts": {
    "dev": "npm-run-all --parallel dev:*",
    "dev:js": "webpack",
    "dev:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css",
    "watch": "npm-run-all --parallel watch:*",
    "watch:js": "webpack --watch --env development",
    "watch:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css --watch",
    "build": "npm-run-all --parallel build:*",
    "build:js": "webpack --env production",
    "build:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css --minify"
  },

```

In here you can also see all the packages you have installed to run your project, thse are split into two sections `devDependencies` and `dependencies`. The first is for packages that are only needed during development (like Webpack, Babel, etc.), while the second is for packages that are needed in production (like Alpine.js, Flatpickr, etc.). We'll seee the `dependencies` shortly when we cover how to define ES modules used to import these and export functions to be used for your app. 

```json
,
  "devDependencies": {
    "@babel/core": "7.26.10",
    "@babel/preset-env": "7.26.9",
    "@tailwindcss/aspect-ratio": "^0.4.2",
    "autoprefixer": "^10.4.20",
    "babel-loader": "10.0.0",
    "css-loader": "7.1.2",
    "cssnano": "^7.0.4",
    "daisyui": "^4.12.10",
    "npm-run-all": "^4.1.5",
    "postcss": "8.5.2",
    "postcss-cli": "^11.0.0",
    "postcss-import": "^16.1.0",
    "style-loader": "4.0.0",
    "tailwindcss": "3.4.17",
    "terser-webpack-plugin": "^5.3.10",
    "webpack": "^5.91.0",
    "webpack-cli": "^5.1.4"
  },
  "dependencies": {
    "alpinejs": "3.14.9",
    "flatpickr": "4.6.13",
    "highlight.js": "11.11.1",
    "htmx.org": "2.0.4",
    "sweetalert2": "11.17.2"
  }
}

```
Note: I have a BUNCH I don't use here...mainly because I'm kinda scared to remove them lest my build break.


## Where to Place Your Assets

Use a structure like this:

```
/src
  /js
    main.js
  /css
    main.css
/wwwroot
  /js/dist
  /css/dist
```

The `wwwroot` folder is wheee WebPack will build your  JS (and oddly some CSS) files. And in the case of Tailwind and DaisyUI it will also build your CSS files.
That's what these scripts do:

```json
 "dev:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css",
"build:tw": "npx tailwindcss -i ./src/css/main.css -o ./wwwroot/css/dist/main.css --minify"

```
These 'tree shake' your CSS files to remove any unused CSS. This is a great way to keep your CSS files small and fast. ([You can see more about this here)](https://www.mostlylucid.net/blog/tailwind)

# Writing Your Webpack Configuration

In your `webpack.config.js`, you can configure input/output like so:

```js
const path = require('path');

module.exports = {
  entry: './src/js/main.js',
  output: {
    filename: 'main.js',
    path: path.resolve(__dirname, 'wwwroot/js/dist'),
  },
  mode: 'development'
};
```

Webpack is a super powerful tool, and you can do a lot more with it. You can add loaders for CSS, images, and other assets, as well as plugins for optimization.

For instance in mine I use something called 'Chunks' to split my JS files into smaller pieces. This is a great way to keep your JS files small and fast. 

This looks big and scary but we'll break it down into sections:

```js
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
        experiments:{
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
                                    modules: false, // ✅ for tree shaking
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
        }
    };
};

```
--- 

## Integrating with ASP.NET Core Build

If you want Webpack to run during the `.NET build` process, you can hook into MSBuild in your `.csproj` file:

```xml
<Target Name="BuildFrontend" BeforeTargets="Build">
  <Exec Command="npm run build" />
</Target>
```

Or for development builds only:

```xml
<Target Name="DevFrontend" Condition="'$(Configuration)' == 'Debug'" BeforeTargets="Build">
  <Exec Command="npm run dev" />
</Target>
```

This ensures that your frontend assets are built automatically with the rest of your application.

---

## Summary

To recap:

-  Install Node.js
-  Initialize `npm`
-  Install Webpack and configure input/output
- Wire it into your `csproj` if needed
-  Build your frontend with `npm run build`

This is a powerful setup that keeps your JS/CSS pipeline modern while still fully integrated with ASP.NET Core.

---

## Bonus: Tailwind, TypeScript, and More

You can extend your setup with:

- [Tailwind CSS](https://tailwindcss.com/docs/guides/aspnet-core)
- PostCSS and Autoprefixer
- Babel or TypeScript
- DaisyUI and HTMX support
- Alpine.js for interactivity

Let me know in the comments or on GitHub if you want a follow-up post with those extras!