# Working on Legacy Codebases (or a contractor's life)


<!--category-- Legacy Code -->
<datetime class="hidden">2024-11-06T22:30</datetime>

# Introduction
As a freelance developer one of the skill-sets you need to learn quickly is how to work on existing codebases effectively. I've been lucky to have built a bunch of from--scratch systems; this is a JOY as an experienced developer however it's not always the case.

[TOC]

# The Challenges
Legacy systems have significant challenges; especially when the key developers / architects (if you're lucky enough to have them) have moved on.

## Documentation
This is often overlooked especially in smaller companies. In general, you need 4 key types of documentation:
   1. **New Developer guides** so how do you run the project *locally* (more on this in a minute), what considerations are there (if for example you need a different framework than current, especially true with Node sadly).
   2. **Deployment Docs** in these days when CI/CD is seen as the gold-standard of deployment it's often overlooked that you need to know how to deploy the system manually. This is especially true if you're working on a system that's been around for a while and has a lot of manual steps.
   3. **Problem Resolution Procedures**; what do you do when the system goes down? Who do you call? What are the key things to check? This is often overlooked but is CRUCIAL in a production system.
   4. **Architectural / System-Design Docs**; how does the system work? What are the key components? What are the key dependencies? Think of this as your roadmap when learning a new system. It provides a high-level overview of how the system works which can be critical (especially in larger, distributed systems). This SHOULD include which source code repositories are used, which CI/CD systems are used, which databases are used for each element of the application. This can encompass anything from a simple diagram to a full-blown UML diagram of each component of the system. For example in a  React Application this would typically include a diagram of the components and how they interact with each other and any backend services. In a .NET Core application this would include a diagram of the services and how they interact with each other (and the front end & other backend systems) and possibly ASP.NET Views and what services they may use.

Documentation is one of the things we as developers often hate doing (it's NOT code) but it's crucial. As you can see I LOVE Markdown, with the likes of Mermaid and PlantUML you can create some really nice diagrams and flowcharts that can be included in your documentation.

It needs to be CURRENT; developers often talk about bit-rot; where a project's dependencies become obsolete / downright security risks. This is also true of documentation; if it's not current it's not useful.

There's varying levels of documentation but in general *any time you change code which is referred to in a document you should update that document*.

## Running the system locally
This is often a challenge; especially if the system has been around for a while. You need to know what version of Node / .NET etc., what version of the database, what version of the framework etc. This is often overlooked but is CRUCIAL to getting up and running quickly.

I've often seen developers saying that this isn't relevant in these days of cloud systems and large distributed applications, but I disagree; you need to be able to run the system locally to debug issues quickly and effectively.

1. Profiling - this seems to have become an 'advanced developer only' skill, but it's crucial to be able to profile your code to see where the bottlenecks are. This is especially true in a legacy system where you may not have the luxury of being able to rewrite the system from scratch. Tools like [dotTrace](https://www.jetbrains.com/profiler/) and [dotMemory](https://www.jetbrains.com/dotmemory/)are invaluable here. Especially in ASP.NET applications; the biggest issues are generally 'leaks' in the system; memory leaks, connection leaks etc. While your app will seem to run FINE for a bit you suddenly find it's crashing / using up all the memory on the server.
2. Finding issues in the debugger - Like it or not the days of `Response.Write` in Classic ASP are over. For an even modestly complex application (especially ones you didn't write) it's crucial to 'step through' code. To follow a request from its inception through the service to identify what MIGHT be happening, what exceptions may not be caught etc.
3. Logging - Again often overlooked (sorry but no an ASP.NET Core exception filter at the top of the stack isn't enough). Referring back to my [previous post](/blog/onlogging), logging is a critical part on working in code LOCALLY. Remember you can use customer logging types (for instance using the [Logging Source Generators](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)) in ASP.NET Core. You can then specify which event types should be logged and which shouldn't. This is crucial in a legacy system where you may not have the luxury of being able to rewrite the system from scratch.
For Application Insights you may want User Journeys and other features; however be aware *this becomes expensive fast* if using `LogInformation` for every request. You may want to use a custom Telemetry Processor to filter out requests you don't want to log.
4. Testing - Too often I see developers 'testing in preprod' or thinking that Unit Test then check in is ENOUGH in terms of testing. Its RARELY IS, there's a lot of value in both using a tool like Resharper / NCrunch to automatically run unit tests; however remember Unit Tests are just that; they test a 'unit' of code. You need to retain visibility in how the system actually runs in concert with other components. This is where Integration Tests come in; they test how the system runs as a whole. You can use a tool like [SpecFlow](https://specflow.org/) to write these tests in a human-readable format. Again; crucial in a legacy system where you may not have the luxury of being able to rewrite the system from scratch.

In EVERY project I work on the first step is getting the system (or a large part of it) running locally. By seeing the code, running the code, debugging the code you can get a feel for how the system works. 

## The Codebase
In every system this is your *source of truth*, no matter what the docs say, what others tell you about how it SHOULD work this is how it DOES work.

### Navigating A Legacy Codebase
This often challenging, it's like finding your way in a new city with no roadmap. Luckily in applications you have an entry point (a page loading / a front end API call etc.) pick a point and start there.

Use whatever you need to interact with it, whether it's PostMan, Rider's HttpClient or even a web page. Hook in your debugger, make the call and follow it through. Rinse and repeat for each part of the system.

### Refactoring
Generally LEAVE THIS UNTIL YOU UNDERSTAND THE SYSTEM. It's ALWAYS tempting to 'throw it away and start again' however resist this temptation. Especially for a running system  (usually) *IT WORKS* rebuilding / even refactoring a system is a HUGE risk. Yes it's FUN but for every line of code you change you risk introducing new snd exciting bugs.

Like everything else (especially when contracting)  you need to justify the work you perform on a system. Either this justification needs to be focussed on one of the following:
1. **Performance** - the system is slow and needs to be sped up (again be careful, what YOU find slow may be how the system is designed to work). Making one part smoking fast may introduce issues further down the system. Will you 'kill' the database server, if you make too many requests do you have a mechanism which won't cause cascading exceptions?
2. **Security** - the system is insecure and needs to be made secure. This is a tricky one; especially in a legacy system. With bit-rot you may find that the system is using old versions of libraries which have known security issues. This is a good justification for work; however be aware that you may need to refactor a LOT of the system to get it up to date; again leading to the 'new bugs' issue.
3. **Maintainability** - the system is hard to maintain and needs to be made easier to maintain. In general this becomes a lot of work; does the current lifetime of the codebase justify this? If you're spending more time making these changes to improve maintainability than you'd ever save for the customer then it's not worth it (and again, changed code == new bugs).
4. **User Experience** - I generally prioritise these issues. For a *user* it doesn't matter if your code is 1% better; they are the ones who PAY for the work you do in the end. If you can make their experience better than it's generally worth it. However, be aware that this can be a 'slippery slope' of changes; you may find that you're changing a LOT of the system to make a small change for the user.

## The Business Of Working On Legacy Systems
This is often overlooked; especially by developers. You need to be able to justify the work you're doing on a system. This is especially true in a contracting environment where you're paid by the hour. In the end, it's not YOUR code and not your money. The WHY you're making a change is often more important than the change itself.

I've worked as a contractor for over a decade now, it's not EASY; as a contractor each hour of your time is a 'cost' to the customer. You need to add move value to the system than you cost. If you're not then you'll quickly be looking for a new contract. 

As developers, we tend to be crappy business people we're focused on 'perfection' at every turn. In reality, you don't need to make a system 'perfect' (I'd argue there's no such thing); you just need to deliver value to the customer.

On a longer term engagement this INCLUDES ensuring any *new* code is maintainable and cost-efficient to run. In legacy systems this is MUCH HARDER. You often have to wade through a swamp, being anxious that as you learn the system you CANNOT offer much value. `I'm not making any changes` you think `I'm just learning the system`. 
This is a fallacy; you're learning the system to make it better. You're learning the system to make it more efficient. You're learning the system to make it more maintainable. If a customer cannot accept this step; then you need to be very careful about how you communicate this (or look for a new contract).

## The People
Again often overlooked, a lot of the time you're brought in as a contractor because some key person has left (don't get involved in the politics of this; it's not your concern). You need to be able to work with the people who are there to achieve the goals of the project. 
In a contract you'll generally have the terms of your engagement spelled out (in my current contract it's 'improve reliability and reduce running costs'); focus on this. If you're not sure what this means then ASK.

Keep in mind who your direct contact is; especially in the first couple of months. Keep them informed (you likely won't have new features to brag about as you get spun up on how the system works). I generally send a summary email every week / two weeks to my direct contact; this is a good way to keep them informed of what you're doing and what you're finding.

Remember, this is the person who will approve your invoice; there should be no surprises at invoice time. Once you start checking in code regularly this is less of an issue; you have a record of exactly what you did and the impact it had. Until then; you need to keep them informed.
**They need to know what you did and why they should pay you for it.**

## Reliability
Again, back to the legacy code; if you make a change in general you need to deploy it.Even the best of us will FUCK THIS UP from time to time, especially in legacy code systems there will be something *you couldn't know* when you deploy. This gets back to logging - if you have a staging server have it logging a LOT (but retained for a short period) then when you deploy to THIS you can gather more information about what failed. 

No matter how good you are, how much local testing you've done we are all human. This is a key part of the 'don't deploy on a Friday' rule; expect a deployment to cause a new issue on a system. Be prepared to work until it's resolved. If you don't KNOW why it failed, add more tests to reproduce the issue and more logging to ensure you catch similar issues in the future. 

Especially for production systems your staging system may not be 1:1 (especially where load is concerned), tool like [k6](https://k6.io/) can help you simulate load (even better locally where you can do proper profiling as mentioned previously).

## Deployment
Again often overlooked in the fervour for CI/CD is the WHY of these. Simple; YOU WILL FUCK UP. Having a very quick and efficient way to deploy a system means that when you DO break it you can also fix it more quickly. If your CI code review system means it takes 2 days to get a PR merged then that's the quickest you can reasonably fi a system. If your CD system means that you take down the running system; get used to LONG nights. 

An efficient mechanism to fix and deploy code is essential to an efficient development pipeline. If it takes longer to deploy a fix than it took to find and implement that fix in code then you're less likely to fix stuff. 

## 'Fixing Legacy Apps'
I put this in quotes as this is an issue; for legacy applications (especially when large scale rework is out of bounds) there's two main approaches.
- Running Patches

This is the process of simply fixing existing code; again ensure you test thoroughly and have processes in place to revert / rapidly redeploy any fixes. 
I won't lie this type of development is rarely FUN as you are still likely wading through the swamp of the existing codebase. However, it's a necessary evil in many cases.

As usual you should ensure you have SOME FORM of test to excercise the current code; ideally it should also test for fail for the issue you are trying to resolve BEFORE you make the fix
. 
They IDYLL for this is to have a unit test which closely targets the area of code which you need to fix but this is often outside of the scope of work for large, distributed systems.

I'd generally use a system of tests in this order of preference:

1. Unit Tests - again these are preferred as you can target these to only exercise the code you are working on. However, in a legacy system these are often not present & extremely difficult to retrofit (for example in a system with many outside calls / which is messy in how it constructs; not using DI for example).
2. Integration Tests - This is an overloaded term as it can cover anything from a unit test which passes through multiple layers of code (these are best avoided) to using the likes of the excellent [Verify](https://github.com/VerifyTests/Verify) through even to Selenium tests. In general you want to test the system as a whole; however be aware that these tests can be slow and brittle. Using Verify can often be a great approach for legacy systems as you can at least 'verify' you're not breaking the API surface of the system.
3. Manual Tests - EVERY developer should try and run manual tests for any code checkin. This is just ensuring that what you expect the 'user' (this can be an actual user or another component of the system interacting with your code) to see is what they actually see. This can be as simple as running the system locally and checking the output of a page or as complex as running a full suite of tests on a staging server.

- Onion Skinning
For working on legacy systems you generally won't have the luxury of a complete rework. This is where I use the 'onion skinning' approach. 

To enable you to upgrade PARTS of a system you can identify components which can be split off from an existing monolith into a microservice (for some value of 'micro'). This can be a great way to start to modernise a system without the risk of a full rework.
Common examples might be splitting off API endpoints into new projects which use more updated elements. The terror of DRY can come into play here however. A poorly structured solution often has lots of 'helper' or 'service' components which really should be in different projects (or even in nuget packages for more global reuse). 

I'll cover this approach further in a future article as it's a key element of how I work on legacy systems & not obvious to many developers. 

# Getting Paid
Now that we have all this out the way there comes the thorny problem of payment. I have a general rule:

**Any payment issues I'm looking to move on** 

If they're late paying; depends on your contract but at LEAST within 30 days but generally closer to 7; this is a bad sign. If there's quibbles over your invoice (nickle-and-dimeing)  think about whether the company is capable of paying for your services on an ongoing basis. 

Don't mess around with this; if you've done your job you need to be paid in a timely fashion. It doesn't matter how much you enjoy it; if a bad customer CAN exploit you they will. It's NEVER worth it. 

Be honest; only charge for time you actually worked; ensure it's clear what you did and why you did it. 

I've led teams of developers and I've been a developer; I've been a contractor and I've been a customer. I've seen all sides of this and I can tell you; if you're not getting paid on time then you need to move on. On the flip-side if you have a contractor (or a FTE) who's not delivering then you need to address this quickly. Everyone has personal struggles but especially in a contract environment you need to be delivering value or not charging for time when you aren't.

As for rate; you will find your level; personally I charge more for projects where I have more responsibility (or which don't look like fun). I charge less for projects where I'm learning a new technology or for startups. I've also worked fewer days but kept my rate steady. But don't accept a low-ball rate; you're a professional and you should be paid as such.

# In Conclusion
Well that's it. I'm off work today and tomorrow for my gran's funeral and frankly panicking a little that I have a HUGE legacy system to learn, so I thought I'd vomit out my thoughts on what working on these systems is like & the challenges. 