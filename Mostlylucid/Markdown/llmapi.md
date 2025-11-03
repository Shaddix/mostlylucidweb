# Using Local LLMs to Automatically Simulate APIs in ASP.NET Core MimimalAPI


<!--category-- AI, LLM, ASP.NET Core, API, Nuget, mockllmapi-->
<datetime class="hidden">2025-11-02T12:30</datetime>

# Introduction
One of my constant obsessions is sample data. It's an often annoying aspect of developing systems that you have the catch 22 of needing data to appropritately test fucntionality while developing the system which would let you create said data.
Combined with my current love of 'AI assisted' coding random ideas it's led to the ceation of a little LLM enabled test data generator along with a nuget package with a middleware that can generate simulated API responses using an API:


[![NuGet](https://img.shields.io/nuget/v/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
[![NuGet](https://img.shields.io/nuget/dt/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
 
> I know mermaid is currently broken. Working on it (the Tailwind 4 upgrade has NOT been smooth!)  
  
[TOC]  

# Concept
As with a lot of "AI Assisted" coding ideas it started with an idea about simulating output using LLMs. I was working on another project ([LucidForums](https://github.com/scottgal/lucidforums), a hilariously dysfunctional self populating LLM based forum experiment) and LLMs are really good (if a little slow) at generating sample data so *what if* I could use them to simulate any API. 

# Requirements
1. It shoudl be simple to implement in an ASP.NET Core App.   
2. The data should be fairly realistic
3. The data should be random
4. It can accept JSON 'shapes' which tell the LLM what the response should be like
5. It can use local Open Source LLMs 
6. It should be a free to use Nuget control

This is what I came up with. I'll add more detail on the thinking as I add more functionality. It's really neat, really works and is faster than I'd feared. However; I have an A4000 16Gb, you could select smaller edge models but the quality would likey vary massively.

Future additions will likely include caching in case you want faster perf.

Here's the readme for the package (this is current with the GitHub version using a Markdig extension which fetches remote content - article coming soon! :)).

<fetch class="hidden" markdownurl="https://raw.githubusercontent.com/scottgal/LLMApi/refs/heads/master/README.md" pollfrequency="2h"/>
  