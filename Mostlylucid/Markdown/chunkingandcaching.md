# LLMApi: Keeping It Lively and Alive, Chunking and Caching 

# Introduction
In my LLMApi project I wanted to be able to support asking for LOTS of data; however LLMs have a very limited amount of data they can output at one time; and aren't particularly fast about doing it. So I needed to add a clever way of pre-generting that data and deliver it in 'chunks' so you CAN get chunky data back quickly.

Along with that there was the problem of contexts, a great feature but until now a context lasted as long as the app so this adds full support for a sliding cache to eliminate that potental source of memory leaks if you leave the simulator running.

<!--category-- AI, LLMApi, LLM, ASP.NET Core, API, Nuget, mockllmapi, SignalR, AI-Article-->
<datetime class="hidden">2025-11-06T13:35</datetime>

<fetch class="hidden" markdownurl="https://raw.githubusercontent.com/scottgal/LLMApi/refs/heads/features/big-results-cache/CHUNKING_AND_CACHING.md" pollFrequency="2h" transformlinks="true"/>  

