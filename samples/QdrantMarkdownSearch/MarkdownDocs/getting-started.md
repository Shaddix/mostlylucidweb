# Getting Started with Self-Hosted Vector Search

Welcome to the world of self-hosted semantic search! This guide will help you understand the basics of vector databases and how Qdrant makes semantic search accessible to everyone.

## What is Semantic Search?

Semantic search understands the *meaning* behind your queries, not just keyword matches. When you search for "machine learning algorithms," it can find documents about "neural networks" and "deep learning" even if those exact words aren't present.

## Why Self-Host?

Self-hosting gives you:
- Complete control over your data
- No monthly fees for managed services
- Privacy - your data never leaves your server
- No rate limits or API restrictions
- Learning opportunity to understand how it all works

## The Technology Stack

This sample uses:
- **Qdrant**: Open-source vector database written in Rust
- **Ollama**: Local AI model runtime (no external API calls!)
- **ASP.NET Core 9.0**: Modern web framework
- **Docker**: For easy deployment

Everything runs on your local machine with no cloud dependencies.
