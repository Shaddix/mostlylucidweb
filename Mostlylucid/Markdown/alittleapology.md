# An Apology about my Nuget Package
<!--category-- ASP.NET, Random, PagingTagHelper -->
<datetime class="hidden">2025-03-20T21:30</datetime>

# Introduction
Well I've been playing with a nuget package for doing [paging stuff in ASP.NET](https://www.nuget.org/packages/mostlylucid.pagingtaghelper/) and well I never thought there'd be 1.7k downloads. I thoght not making a 1.0 version would make it clear that it was a bit of a toy but I guess not.

# SO FAR
So far in 10 days I've released *20* versions of the damn thing. It's grown from a simple replacement for an old paging tag helper I had been using but stopped working for me to...well as of a few minutes ago, *3* whole tag helpers:
1. Paging - just the page numbers thing with Page Size drop down and HTMX / Tailwind & DaisyUI integration.
2. Flippy Table Headers- anotheritch I scratched, I was always annoyed I didn't have a *free* easy way to sort data in tables with a nice UI...so...
3. Page Size Tag Helper - Just TODAY I had an issue as I'm building a little admin site which uses Cosmos - which doesn't let you page through data in 'jumps' (as umm...it's globally scalable or some shit). So now there's ANOTHER tag helper. 

But I have to admit; I have a [sample site ](https://taghelpersample.mostlylucid.net/) but it's ALL VERY ROUGH still. Writing docs, building examples etc is a bit of a drag and I need to find time to do it!. 

# FUTURE
WHO KNOWS, I wrote the Umami.NET package a few months ago now and well, it works...so I don't really see any reason to keep grinding on it. PLEASE if you have anything you want to see (or if you want to write docs...please JEBUS). [Take a look at the GH!](https://github.com/scottgal/mostlylucid.pagingtaghelper)