# mostlylucid

[![Live Site](https://img.shields.io/badge/Live%20Site-mostlylucid.net-blue)](https://mostlylucid.net)
[![License: Unlicense](https://img.shields.io/badge/License-Unlicense-green.svg)](https://unlicense.org/)

This repository contains the source code for [mostlylucid.net](https://mostlylucid.net) — the personal site and blog of Scott Galloway, a consulting web developer and systems architect with over 30 years of experience building web applications.

**🌐 Visit the live site:** [mostlylucid.net](https://mostlylucid.net)

## NPM Packages

[![npm version](https://img.shields.io/npm/v/@mostlylucid/mermaid-enhancements.svg)](https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements)
[![npm downloads](https://img.shields.io/npm/dm/@mostlylucid/mermaid-enhancements.svg)](https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements)

- [@mostlylucid/mermaid-enhancements](https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements)
A markdig preprocessor which fetches and amanges updates for remote content for Markdown processors. Also includes a Table Of Contents extension which processes your TOC in page.

##  NuGet Packages

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.Markdig.FetchExtension.svg)](https://www.nuget.org/packages/mostlylucid.Markdig.FetchExtension)
[![NuGet](https://img.shields.io/nuget/dt/mostlylucid.Markdig.FetchExtension.svg)](https://www.nuget.org/packages/mostlylucid.Markdig.FetchExtension)

- [mostlylucid.Markdig.FetchExtension](https://www.nuget.org/packages/mostlylucid.Markdig.FetchExtension/) 

A complete solution for fetching and caching remote markdown content with support for multiple storage backends, automatic polling, and a stale-while-revalidate caching pattern.


[![NuGet](https://img.shields.io/nuget/v/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
[![NuGet](https://img.shields.io/nuget/dt/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)

- [mostlylucid.mockllmapi](https://www.nuget.org/packages/mostlylucid.mockllmapi)

A lightweight ASP.NET Core middleware for generating realistic mock API responses using local LLMs (via Ollama). Add intelligent mock endpoints to any project with just 2 lines of code!

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.pagingtaghelper.svg)](https://www.nuget.org/packages/mostlylucid.pagingtaghelper)
[![NuGet](https://img.shields.io/nuget/dt/mostlylucid.pagingtaghelper.svg)](https://www.nuget.org/packages/mostlylucid.pagingtaghelper)

- [mostlylucid.pagingtaghelper](https://www.nuget.org/packages/mostlylucid.pagingtaghelper)  
  HTMX-enabled ASP.NET Core Tag Helpers to help with paging tasks.

[![NuGet](https://img.shields.io/nuget/v/Umami.Net.svg)](https://www.nuget.org/packages/Umami.Net)
[![NuGet](https://img.shields.io/nuget/dt/Umami.Net.svg)](https://www.nuget.org/packages/Umami.Net)

- [Umami.Net](https://www.nuget.org/packages/Umami.Net)  
  A package helping with the use of the Umami Web Analytics software.
  
[![NuGet](https://img.shields.io/nuget/v/MostlyLucid.EufySecurity.svg)](https://www.nuget.org/packages/MostlyLucid.EufySecurity)
[![NuGet](https://img.shields.io/nuget/dt/MostlyLucid.EufySecurity.svg)](https://www.nuget.org/packages/MostlyLucid.EufySecurity)

- [MostlyLucid.EufySecurity](https://www.nuget.org/packages/MostlyLucid.EufySecurity)  
  A **HIGHLY EXPERIMENTAL** .NET client library for controlling Eufy Security devices by connecting to Eufy cloud servers and local/remote stations over P2P. Supports cameras, doorbells, locks, sensors, and more.


---

## Featured Articles

Dive into some of the technical deep-dives and experiments from the blog:

### 📦 Package Development
- **[Building a Remote Markdown Fetcher for Markdig](https://mostlylucid.net/blog/markdigfetchextension)** - A complete guide to building a Markdig extension with caching, remote content fetching, and TOC generation
- **[Creating an LLM Mock API with Ollama](https://mostlylucid.net/blog/mockllmapi)** - Add intelligent mock endpoints to any project with just 2 lines of code
- **[Umami.Net: Analytics Made Simple](https://mostlylucid.net/blog/umamianalyticspackage)** - Building a .NET client for privacy-focused web analytics

### 🎨 Frontend & HTMX
- **[HTMX & Alpine.js Integration Patterns](https://mostlylucid.net/blog/htmxalpinejs)** - Building dynamic UIs without heavy JavaScript frameworks
- **[Mermaid Diagrams with Interactive Controls](https://mostlylucid.net/blog/mermaidandmeraid)** - Pan, zoom, and fullscreen diagram support

### 🚀 DevOps & Infrastructure
- **[Docker Multi-Stage Builds for .NET](https://mostlylucid.net/blog/dockercompose)** - Optimizing container images and deployment workflows
- **[Monitoring with Prometheus & Grafana](https://mostlylucid.net/blog/monitoring)** - Setting up observability for ASP.NET Core applications

### 🤖 AI & Translation
- **[Automated Blog Translation with EasyNMT](https://mostlylucid.net/blog/translationapi)** - Building a multilingual blog with automated translation to 12 languages

[**→ View all articles**](https://mostlylucid.net/blog)

---

## About the Site

**mostlylucid** is a living lab: part portfolio, part blog, part playground. It's where I share:

- **Technical deep dives** into ASP.NET Core, JavaScript frameworks (HTMX, Alpine.js), Docker, cloud, and search technologies
- **Experiments** with modern frontend stacks (Tailwind, DaisyUI, Markdown tooling, Mermaid diagrams)
- **Open-source contributions** like NuGet packages, tag helpers, and utilities
- **Reflections** on freelancing, remote work, and the craft of building resilient systems

The site is intentionally a work in progress — things may break, evolve, or get rebuilt entirely. That's part of the ethos: showing how things are built, not just the polished result.

### Platform Features

- **Dual-mode blog system**: File-based markdown or PostgreSQL database storage
- **Multilingual support**: Automated translation to 12 languages via EasyNMT
- **Full-text search**: PostgreSQL-powered search with tsvector/GIN indexes
- **Comments system**: Nested comments with closure table pattern
- **Analytics**: Integrated Umami analytics with custom .NET client
- **Observability**: Comprehensive logging (Serilog + Seq), metrics (Prometheus), and tracing

## Tech Stack

### Backend
- **.NET 9.0** - ASP.NET Core MVC for server-side rendering
- **PostgreSQL 16** - Primary database with full-text search
- **Entity Framework Core** - ORM with code-first migrations
- **Hangfire** - Background job processing for newsletters

### Frontend
- **HTMX 2.0** - Server-driven interactions without heavy JavaScript
- **Alpine.js** - Lightweight reactive components
- **Tailwind CSS + DaisyUI** - Utility-first styling with component library
- **Highlight.js** - Syntax highlighting with custom Razor support
- **Mermaid** - Interactive diagram rendering
- **EasyMDE** - Markdown editor

### Infrastructure
- **Docker** - Containerized deployment with Docker Compose
- **Caddy** - Reverse proxy and automatic HTTPS
- **Prometheus + Grafana** - Metrics and visualization
- **Seq** - Structured log aggregation
- **Umami** - Privacy-focused web analytics
- **Cloudflare Tunnel** - Secure remote access

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- Node.js (for frontend builds)
- Docker (optional, for full stack)

### Quick Start

```bash
# Clone the repository
git clone https://github.com/scottgal/mostlylucidweb.git
cd mostlylucidweb

# Install dependencies
npm install
dotnet restore

# Build frontend assets
npm run build

# Run the development server
dotnet run --project Mostlylucid/Mostlylucid.csproj
```

The site will be available at `https://localhost:5001`

### Docker Development

For the full stack including database, analytics, and monitoring:

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f mostlylucid
```

See [CLAUDE.md](./CLAUDE.md) for detailed development documentation.

## About Scott

**Scott Galloway** is a consulting web developer and systems architect with over 30 years of experience building web applications. As a former **Microsoft ASP.NET Program Manager**, he has deep expertise in the .NET ecosystem and has worked with everyone from Fortune 500 companies to early-stage startups.

### What I Do

- **Full-stack development** across .NET and JavaScript ecosystems
- **Systems architecture** and rapid prototyping for complex web applications
- **Open-source development** - Building and maintaining NuGet packages, libraries, and tools
- **Remote team leadership** and startup bootstrapping
- **Technical writing** - Sharing knowledge through detailed technical articles

### Current Focus

- Building developer tools and libraries for the .NET community
- Exploring modern web patterns with HTMX and Alpine.js
- Experimenting with AI/LLM integration in web applications
- Creating privacy-focused analytics and monitoring solutions
- Contributing to the Markdig ecosystem with custom extensions

### Background

With a career spanning from the early days of ASP.NET to modern cloud-native architectures, I've been fortunate to work on:

- Large-scale enterprise applications serving millions of users
- Developer tools and frameworks used by thousands of developers
- Startup MVPs that needed to scale quickly and reliably
- Open-source projects that solve real-world problems

I believe in **building in public** - sharing both successes and failures, showing the messy middle of development, not just the polished result.

### Let's Connect

Interested in collaborating, consulting, or just chatting about building things?

- **Email:** scott.galloway+ml@gmail.com
- **Blog:** [mostlylucid.net](https://mostlylucid.net)
- **GitHub:** [@scottgal](https://github.com/scottgal)

## Contributing

This repo is primarily Scott's personal playground, but feedback, issues, and suggestions are welcome. Feel free to open a PR or drop a note.

## License

**Unlicense** — This is free and unencumbered software released into the public domain. See [LICENSE](./LICENSE) for details.






