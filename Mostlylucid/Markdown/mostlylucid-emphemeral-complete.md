# mostlylucid.ephemeral.complete; a strange concurrent systems pattern system in an LRU cache.

<!--category-- mostlylucid-ephemeral -->

<datetime class="hidden">2025-12-14T13:35</datetime>

Well this has been my obsession for the past week. See the [previous parts](/blog/ephemeral-execution-library) and what led to this; '[What if an LRU was an execution context.'](/blog/learning-lrus-when-capacity-makes-systems-better). Now it's a set of 30 Nuget packages covering most major concurrent execution patterns (in TINY 5-10 line packages). Get amazing adaptive capabilities with a SIMPLE syntax!

Find the source here: https://github.com/scottgal/mostlylucid.atoms/blob/main/mostlylucid.ephemeral/src/mostlylucid.ephemeral.complete


# Priors

Read the [previous part on Signals ](/blog/ephemeral-signals)for some insight into it's uses. Presented here is the Readme.md from the mostlylucid.ephemeral.complete pacakage which contains both the core mostlylucid.ephemeral package and all the patterns, 'atoms' (coordinators etc) packages in one convenient DLL.

## Core

OR use the core [mostlylucid.ephemeral](https://github.com/scottgal/mostlylucid.atoms/tree/main/mostlylucid.ephemeral/src/mostlylucid.ephemeral) a TINY (literally 10 classes) which gives you all the raw functionality. 

## Attributes & DI
OR if you want full attribute based async routing with simple `[EphemeralJob]` and service.Add<x>Coordinator` style registration use the [mostlylucid.ephemeral.attributes package](https://github.com/scottgal/mostlylucid.atoms/tree/main/mostlylucid.ephemeral/src/mostlylucid.ephemeral.attributes).

This is likely THE topic of my blog going forward...you have been warned 🤓



<fetch class="hidden" markdownurl="https://raw.githubusercontent.com/scottgal/mostlylucid.atoms/refs/heads/main/mostlylucid.ephemeral/src/mostlylucid.ephemeral.complete/README.md" pollFrequency="2h" transformlinks="true"/>  



