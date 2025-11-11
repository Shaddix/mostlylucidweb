# Agile Speccing: Writing Feature Specs That Actually Work

<!--category-- Software Development, Documentation, Agile -->
<datetime class="hidden">2025-11-11T10:00</datetime>

# Introduction
Over the years I've read hundreds of feature specifications. Some were brilliant; most were bloody awful. The difference between a good spec and a bad one isn't about length or formality; it's about whether it actually helps developers build the right thing without driving them mad in the process.

Here's something I learned at Microsoft that changed how I think about specs: **A spec is not a bible; it's a tool.** Like any tool, you use it to get a job done. You add as much detail as you, the stakeholders, and the developers need to get going. Then you adapt it, change it, and update it as the feature develops.

Treat a spec as a sacred, unchangeable document and you're building the wrong thing perfectly. Treat it as a living tool and you're building something that actually solves problems.

This agile approach to specs creates a particular challenge: if the feature can evolve as you learn, how the hell do you estimate it? How do you know when you're done? That's what this article is about.

[TOC]

# What Makes a Good Feature Spec

## The Problem-Solution Pattern
The single most important principle for writing specs: **Always start with the problem, not the solution.**

This pattern is simple:
1. **Problem** - What's actually broken? What pain are users experiencing?
2. **Solution** - Here's how we propose to fix it
3. **In Scope** - What we're doing in this spec
4. **Out of Scope** - What we're explicitly NOT doing (equally important)

I've seen countless specs that jump straight into "User clicks button X which calls API Y" without ever explaining what the user is actually trying to achieve. This is arse-backwards. The implementation details should flow naturally from understanding the problem.

## Clarity of Purpose
Before you write a single word about implementation, you need to answer one question: **WHY?**

Why are we building this? What problem does it solve? Who benefits? If you can't answer these questions clearly, you haven't got a feature; you've got a wish list.

A good spec starts with:
1. **The Problem Statement** - What's broken or missing? Be specific.
2. **The User Impact** - Who cares and why? Quantify if possible.
3. **Success Criteria** - How do we know we've solved it? Make it measurable.
4. **Non-Goals** - What are we explicitly NOT doing? This prevents scope creep and endless debates.

## The Right Level of Detail
This is where most specs go tits-up. Too vague and developers are left guessing; too detailed and you're micromanaging implementation choices that developers are better qualified to make.

The trick is to specify **WHAT** needs to happen without prescribing **HOW** it happens. For example:

**Good**: "When a user attempts to submit a form with invalid data, they should receive immediate feedback indicating which fields need correction."

**Bad**: "On form submission, the submit button's onClick handler should call validateForm() which iterates through formFields array checking each field.value against its validation.regex property and if any fail should call showError() with the field.name and validation.message parameters."

The first tells me what the user experience should be; I can implement it in React, Vue, vanilla JavaScript, or carrier pigeon for all it matters. The second assumes implementation details that might be completely wrong for the tech stack or introduce unnecessary constraints.

## Edge Cases and Error Handling
If there's one thing I've learned it's this: **Users will find ways to break your software that you never imagined.**

A good spec doesn't just describe the happy path; it considers:
- What happens when the network fails mid-operation?
- What if the user has no permissions?
- What about concurrent modifications?
- How do we handle partial failures in distributed operations?

You don't need to solve all these in the spec, but you need to acknowledge they exist. Nothing irritates developers more than discovering halfway through implementation that nobody thought about what happens when the external API is down.

## Security and Performance Considerations
These shouldn't be afterthoughts shoehorned in during code review. If there are specific security requirements (authentication levels, data encryption, audit logging) spell them out in the spec.

Similarly, if there are performance constraints that matter ("This search needs to complete in under 200ms for datasets of up to 1 million records"), put them in the spec. Don't leave developers to guess at non-functional requirements.

# The Structure of a Good Spec

Here's the template I use for feature specs. It's not gospel, but it's served me well:

## 1. Overview
A paragraph or two summarising what we're building and why it matters. Your CEO should be able to read just this section and understand the value.

## 2. Background/Context
What's the current state of affairs? What prompted this feature? What have users been asking for? This helps developers understand the problem space without having been in every product meeting.

## 3. User Stories
These aren't just formality; they force you to think through actual usage patterns. Format them properly:

**As a** [type of user]
**I want** [to do something]
**So that** [I achieve some goal]

The "so that" bit is crucial; it stops you from writing features nobody needs.

## 4. Detailed Requirements
This is your meat and potatoes. Break it down by functional area. Use subheadings liberally. Include mockups or wireframes if you have them; a picture is worth a thousand words of description.

For each requirement, specify:
- The expected behaviour
- Any constraints or validation rules
- Error handling requirements
- How it interacts with existing features

## 5. Non-Functional Requirements
Performance targets, security requirements, accessibility standards, browser/device support. Don't assume these are obvious.

## 6. Out of Scope
This section is just as important as what's IN scope. Be explicit about what you're NOT doing in THIS spec.

Why this matters:
- **Prevents Scope Creep** - "But couldn't we just..." conversations die quickly when you can point to the Out of Scope section
- **Sets Expectations** - Stakeholders know what won't be delivered
- **Enables Future Work** - Items here might become their own specs later
- **Focuses the Team** - Everyone knows the boundaries of this work

Examples of good Out of Scope items:
- "Mobile support (will be addressed in separate spec)"
- "Migration of existing data (current spec only handles new data)"
- "Admin UI for configuration (will use config files initially)"
- "Integration with System X (dependency not yet available)"

If someone argues an out-of-scope item should be in scope, that's a conversation worth having BEFORE development starts, not halfway through implementation.

## 7. Open Questions
Be honest about what you don't know. Mark these clearly and make sure they get answered before development starts. Nothing worse than blocking development because nobody decided whether we're doing soft deletes or hard deletes.

## 8. Dependencies
What other systems/teams/features does this rely on? What needs to be ready before development can start?

## 9. Acceptance Criteria
How will QA test this? These should be concrete, testable statements. Bonus points if they're written in a format that could become automated tests (Given/When/Then style).

# The Problem of Spec Bugs

Here's something that doesn't get talked about enough: **Specs can have bugs too.**

A spec bug is when the specification itself is wrong. Maybe it contradicts itself, or it specifies behaviour that's technically impossible, or it solves the wrong problem entirely. These are insidious because developers might implement exactly what's written and still end up with a product that doesn't work properly.

## How Spec Bugs Happen

1. **Incomplete Understanding** - The person writing the spec didn't fully understand the problem or the existing system.
2. **Conflicting Requirements** - Different stakeholders want different things and nobody resolved the conflict.
3. **Technical Impossibility** - The spec asks for something that can't actually be done (or can't be done within reasonable constraints).
4. **Changing Requirements** - The world moved on but the spec didn't get updated.

## Handling Spec Bugs

When you find a spec bug as a developer, you have a few options:

### Option 1: Raise It Immediately
This is almost always the right answer. As soon as you spot something that doesn't make sense, raise it. Don't wait until you're halfway through implementation.

Send a clear message to whoever owns the spec:
- What the spec says
- Why it's problematic
- What you think should happen instead (if you have a suggestion)

Do this in writing (email, ticket, whatever) so there's a record.

### Option 2: Implement It Anyway
Sometimes you might be tempted to just build what's specified even though you know it's wrong. **DON'T DO THIS.**

I've seen developers implement specs they knew were wrong because "that's what it said to do" and then act surprised when QA rejects it or users complain. You're a professional; part of your job is to speak up when you see problems.

The exception is if you've raised the issue, been told to proceed anyway, and got that in writing. Then fine; you've done your due diligence.

### Option 3: Fix It Yourself
If you're confident you know what the spec should say, you might be tempted to just correct it yourself. This is fine for obvious typos or formatting issues, but for substantive changes you need to get agreement from stakeholders.

Never silently change requirements. That's how you end up building features nobody asked for.

## Preventing Spec Bugs

The best approach is to prevent spec bugs in the first place:

1. **Involve Developers Early** - Get technical review of specs before they're finalised. We'll spot impossibilities and edge cases that product people might miss.
2. **Use Examples Liberally** - Abstract descriptions are easy to misinterpret. Concrete examples ("User John has permission X, tries to do Y, and sees Z") are much clearer.
3. **Validate Against Existing System** - Does the spec make assumptions about how things currently work? Double-check those assumptions.
4. **Iterate on Specs** - Treat the spec as a living document. As you learn more during implementation, update it. Future developers will thank you.

# Common Spec Pitfalls

## The Novel-Length Spec
Some people think more detail is always better. It's not. A 50-page spec that nobody reads is worse than a 5-page spec that everyone understands.

If your spec is turning into War and Peace, you either:
- Need to break it into multiple features
- Are specifying implementation details that should be left to developers
- Are solving the wrong problem and need to step back

## The Vague Handwave
The opposite problem: "Build a reporting system." Right, cheers for that. I'll just knock up something and we'll see if it matches what you had in your head, shall we?

If your spec can be fully captured in a single sentence, it's not a spec; it's a vague wish.

## The Solution-First Spec
"We need a dashboard" isn't a requirement; it's a preconceived solution. Maybe you do need a dashboard, but maybe you need something completely different. Start with the problem.

## The Moving Target
Requirements that change daily aren't requirements; they're chaos. If things are changing that fast, you don't understand the problem well enough yet. Stop and do more discovery before writing specs.

## The Kitchen Sink
"While we're in there, could we also..." No. No we couldn't. Every feature has a cost. If you want to add something new, write a separate spec for it and prioritise it properly.

# Treating Specs Like Source Code

This is the mindset shift that changed how I write specs: **Treat your spec exactly like you treat source code.**

## Version Control
Your specs should live in version control alongside your code. Check them in. Track changes. Write meaningful commit messages when you update them. This creates a history of how requirements evolved.

At Microsoft, we kept specs in the same repositories as code. When a spec changed, it went through the same review process as code. This wasn't bureaucracy; it was ensuring everyone understood what was changing and why.

## Refactoring Specs
Just as code needs refactoring, so do specs. As you learn more during implementation, the spec should evolve to reflect that learning.

Found a better way to solve the problem? Update the spec to reflect the new approach and explain why you changed direction. Discovered an edge case you hadn't considered? Add it to the spec.

The spec after development should be more refined than the spec before development. If it's not, you've missed an opportunity to document what you learned.

## The Living Document Principle
A spec isn't "done" when development starts. It's done when the feature ships and becomes maintenance mode. Until then, it's a living document that evolves with your understanding of the problem.

This doesn't mean the spec should change daily. Major changes to requirements need discussion and agreement. But clarifications, additional examples, newly discovered edge cases should all be folded back into the spec as you find them.

Think of it this way: if you wouldn't leave outdated comments in code, don't leave outdated information in specs.

## Why Specs Need to Stay Current

Here's something that doesn't get discussed enough: **your spec becomes the foundation for everything that comes after.**

**Test Plans**: QA writes test cases based on the spec. If the spec is outdated, tests are testing the wrong thing. You get false positives (tests pass but feature is broken) or false negatives (tests fail but feature works fine).

**Documentation**: User documentation, API docs, help systems - they all start from the spec. If the spec describes features that don't exist or misses features that do, your docs are wrong from day one.

**Future Development**: When someone needs to extend the feature six months later, they'll read the spec to understand how it works. If it doesn't match reality, they're starting with incorrect assumptions.

**Onboarding**: New team members learn the system partly by reading specs. Outdated specs teach them the wrong mental model of how features work.

This is why the spec must stay current. It's not just about the initial implementation; it's foundational to everything else.

At Microsoft, we treated spec updates with the same importance as code updates. Spec changes went through review. They were versioned alongside code. When a feature changed, updating the spec wasn't optional; it was part of the definition of "done."

If you change the code but don't update the spec, you've created technical debt. Future work will be slower because nobody knows what the current state actually is.

# The Relationship Between Spec and Implementation

Here's something junior developers often don't grasp: **The spec is not the source of truth; the code is.**

The spec tells you what you're trying to build. The code is what you actually built. These two things should align, but when they don't, the code wins. You can't run a spec; you can run code.

This means:

1. **Specs Should Evolve** - As you discover things during implementation, update the spec. It's documentation of what you're building, not a sacred text.
2. **Implementation Details Don't Belong in Specs** - Once you've started implementing, the code itself documents the how. The spec should document the why.
3. **Tests Bridge the Gap** - Good tests verify that the implementation matches the requirements. They're the executable form of the spec.

# Writing Specs for Different Audiences

Different people need different things from specs:

**Executives** - Want to know the business value and rough timeline. Give them the overview and success criteria.

**Product Managers** - Need to understand how it fits into the broader product strategy and roadmap. Give them the user stories and dependencies.

**Developers** - Need enough detail to implement correctly without being told how to do their job. Give them the requirements, edge cases, and non-functional requirements.

**QA** - Need to know how to verify it works. Give them the acceptance criteria.

**Designers** - Need to know what the user experience should be. Give them the user stories and interaction flows.

A good spec serves all these audiences without being bloated. Use sections and structure so people can read what matters to them.

# Testing Your Spec

Before you call a spec done, ask yourself:

1. **Could a developer who's never seen this feature build it from this spec?** If not, you're missing details.
2. **Could QA write test cases from this spec?** If not, your acceptance criteria aren't clear enough.
3. **Could you build something completely useless that still matches this spec?** If so, you haven't captured the actual requirements properly.
4. **Does this spec describe HOW to implement or WHAT to achieve?** If it's the former, you're micromanaging.

# The Agile Approach to Specs

"But we're agile! We don't need specs!" I hear this a lot. It's nonsense.

Agile doesn't mean "no planning" or "no documentation." It means responding to change over following a plan. The key insight: **specs are tools, not contracts.** You create them with just enough detail to get started, then evolve them as you learn.

## Just Enough Detail to Start

The question isn't "how detailed should the spec be?" It's "what do we need to know to start building confidently?"

For some features that might be:
- A paragraph describing the problem
- Three bullet points sketching the solution
- A clear definition of what "done" looks like

For others it might be:
- Detailed user flows with mockups
- Performance requirements backed by data
- Integration specifications for multiple systems

**Add detail where uncertainty exists.** If everyone agrees on how something should work, you don't need to write it down in excruciating detail. If there's disagreement or confusion, that's where you need specifics.

## The Collaborative Model
Here's what changes in agile: **You don't write a spec and throw it over the wall to developers.** The spec is a collaborative effort.

The best approach I've seen:
1. **Product/PM sketches the problem** - What needs solving and why
2. **Developers contribute technical approach** - How we might solve it, what the constraints are
3. **Designers contribute UX requirements** - What the user experience should be
4. **QA contributes test scenarios** - Edge cases and validation approaches

Everyone contributes to the spec. Nobody owns it exclusively. This collaborative approach catches problems early when they're cheap to fix rather than late when they're expensive.

More importantly, it means the spec reflects what's actually possible, not what someone wished for in isolation.

## Evolution During Development

Here's where agile specs differ from traditional ones: **the feature can evolve as you build it.**

You discover that your initial approach won't work? Update the spec to reflect the new approach.

You try the feature and realise it doesn't actually solve the problem? Pivot and document why.

User feedback reveals a better solution? Incorporate it and explain the change.

This evolution is a feature, not a bug. You're responding to new information. The spec should capture that learning, not pretend you had perfect knowledge from day one.

But this creates a problem: if the feature can change, how do you estimate it? How do you know when you're done?

## The Estimation Challenge

This is the dirty secret of agile: **estimation is bloody difficult when features can evolve.**

Traditional estimation assumes you know what you're building. You can break it down into tasks, estimate each task, add them up. Simple.

Agile estimation acknowledges you don't know everything up front. The feature might change as you learn. So how do you estimate?

**You estimate ranges, not absolutes.** Instead of "this will take 3 weeks" you say "somewhere between 2 and 5 weeks depending on what we discover."

**You estimate in iterations.** "We'll spend one sprint exploring this and report back on what we learned. Then we can estimate the rest more accurately."

**You time-box instead of scope-boxing.** "We'll spend 2 weeks on this. At the end of 2 weeks we'll have the best version we can build in that time."

But all of these approaches have one critical requirement: **you need to know what "done" means.** Without a clear definition of done, a feature can keep metastasising forever.

# Defining "Done" (Or How to Stop Feature Metastasis)

This is where many agile specs fall apart. Everyone's excited about flexibility and iteration, but nobody wants to be the one who says "right, that's it, we're done."

Without a clear definition of done, features don't finish; they metastasise. They spread. They grow tendrils into other parts of the system. Before you know it, your "simple comment system" has morphed into a full social network with messaging, profiles, and friend requests.

## The Problem with Vague "Done"

I've seen specs with acceptance criteria like:
- "Users can comment on posts"
- "The dashboard shows relevant information"
- "Search works well"

These aren't definitions of done; they're vague aspirations. What does "relevant information" mean? What's "works well"? You can iterate on these forever and never be done.

The developer needs to know: **what specific thing, when implemented, means I can stop working on this feature?**

## Writing Concrete "Done" Criteria

Good "done" criteria are:
- **Testable** - You can verify whether it's met
- **Specific** - No vague words like "good" or "relevant"
- **Bounded** - They don't imply infinite scope

**Bad**: "Comments should be moderated"
**Good**: "Admin users can approve, reject, or delete comments from the admin panel. Non-approved comments are not visible to regular users. Email notification sent to admin when new comment is posted."

**Bad**: "Search should be fast"
**Good**: "Search returns results within 200ms for the 95th percentile of queries against a dataset of 100,000 posts. Results are ranked by relevance (full-text search score) with date as tie-breaker."

**Bad**: "Dashboard shows useful metrics"
**Good**: "Dashboard displays: total page views (last 30 days), unique visitors (last 30 days), top 5 posts by views (last 7 days), and visitor breakdown by country. All metrics update once per hour."

Notice the difference? The good examples tell you exactly what needs to exist and when you can stop adding things.

## The Out of Scope Section Is Your Friend

Remember earlier when I said the Out of Scope section is as important as what's in scope? This is why.

For every feature, there are dozens of things you could add. The Out of Scope section explicitly calls out what you're NOT doing. This prevents the "while we're in there, we could also..." conversations that cause feature metastasis.

**In Scope**: Nested comments (one level of replies)
**Out of Scope**: Unlimited comment threading, comment voting, comment threading, best comment sorting, comment permalinks (these may come later as separate features)

Now when someone suggests "shouldn't comments have upvotes?" you can point to the spec and say "that's out of scope for this iteration. Let's discuss it as a separate feature once basic comments are working."

## Time-Boxing as a Last Resort

Sometimes you genuinely don't know exactly what "done" looks like when you start. The problem space is too uncertain. In these cases, time-boxing can work:

"We'll spend 2 weeks prototyping different approaches to the recommendation algorithm. At the end of 2 weeks we'll evaluate what we've learned and decide whether to productionise one approach, try something different, or abandon the feature."

But notice: you still have a concrete "done" condition (2 weeks, then evaluate). You're not just building indefinitely.

## Preventing Scope Creep During Development

Even with clear "done" criteria, scope can creep. You discover edge cases. You realise users need something you hadn't considered. How do you handle this without breaking your definition of done?

**Document it, don't just do it.** When you discover something new that needs to be added, update the spec. Make it explicit that the scope has changed. Get agreement from stakeholders.

This serves two purposes:
1. **Visibility** - Everyone knows the scope changed and why
2. **Cost awareness** - Stakeholders see that adding things impacts timeline

If your spec keeps growing, that's a signal. Either you're building the wrong thing (and need to step back and rethink), or this should be multiple features, or you need to cut scope to ship something useful sooner.

## When to Call It Done

At some point, you need to ship. The feature doesn't need to be perfect; it needs to be useful.

A good test: **Can users get value from this feature as it stands?**

If yes, ship it. You can always iterate in the next version. Done doesn't mean "will never be improved." It means "solves the problem well enough that users benefit and we can move on to other work."

If no, you're not done yet, regardless of what your spec says.

The hardest part of agile isn't starting work; it's stopping work. Clear "done" criteria make stopping possible.

# The Spec Review Process

A spec isn't done when you finish writing it; it's done when it's been reviewed by the people who'll use it.

**Developer Review** - Will this actually work? Are there technical constraints we haven't considered? Is there enough detail to implement?

**Product Review** - Does this solve the right problem? Does it align with product strategy? What's missing?

**Design Review** - Does the user experience make sense? Have we considered accessibility? What about mobile?

**QA Review** - Can we test this? Are the acceptance criteria clear enough? What about edge cases?

Get reviews from all these perspectives before you start implementation. Finding problems in the spec costs minutes; finding them in production costs weeks.

# A Concrete Example: Markdown Translation Service

To make this all less abstract, here's what a spec might look like for the automatic markdown translation feature I built for this blog. This demonstrates the principles we've discussed.

## Problem Statement
Blog posts written in English only exclude non-English speaking readers. Manual translation of each post to multiple languages is time-consuming and delays publication. We need an automated solution that translates markdown blog posts to multiple target languages without requiring manual intervention for each post.

## Solution
Implement a background service that automatically translates markdown files to configured target languages using the EasyNMT machine translation service. The service will:
- Monitor markdown files for changes
- Extract translatable text while preserving markdown structure and code blocks
- Batch translation requests for efficiency
- Generate translated markdown files with appropriate language suffixes

## Success Criteria (What "Done" Looks Like)
These criteria tell us exactly when we can stop working on this feature:

- New blog posts are automatically translated to all configured languages (initially: Spanish, French, German, Italian, Portuguese, Chinese, Arabic, Hindi, Japanese, Korean, Dutch, Russian)
- Translated files maintain identical markdown structure to originals
- Code blocks, image URLs, and formatting remain unchanged
- Translation completes within 15 minutes for a typical blog post (2000-3000 words)
- System only re-translates files that have changed (verified via hash comparison)
- Service starts successfully even if translation API is temporarily unavailable
- Errors during translation are logged but don't crash the application

Notice these are specific and testable. We can verify each one. When all are met, we're done. We don't keep adding features like "translation quality scoring" or "manual translation editing" unless we explicitly expand the scope.

## In Scope
- Background service to process markdown files
- Integration with EasyNMT translation API
- Hash-based change detection to avoid unnecessary re-translation
- Batch processing to handle EasyNMT's word limit
- Round-robin load balancing across multiple EasyNMT instances
- Preservation of markdown syntax, code blocks, and images during translation

## Out of Scope
- User interface for manual translation editing (future enhancement)
- Translation memory or glossary management (may add if quality issues arise)
- Real-time translation (background processing is acceptable)
- Translation of code comments within code blocks (intentionally excluded)
- Automatic quality assessment of translations (manual review required initially)

## Technical Constraints
- EasyNMT has a ~500 word limit per request; must batch accordingly
- Translation service can be slow (~15 second timeout per batch)
- Multiple EasyNMT instances needed for reasonable performance
- File system I/O must not block main application

## Open Questions at Spec Time
- ~~Should we cache translations to avoid re-translating unchanged files?~~ **Resolved: Yes, using file hash comparison**
- ~~How do we handle EasyNMT service failures?~~ **Resolved: Log error and skip file; will retry on next service restart**
- What quality issues might we see with technical content? **Decision: Ship and evaluate; manual review catches issues**

## What We Learned During Implementation
Several things emerged during development that refined the spec:

**Image Detection**: Initially, image filenames in markdown were being sent to the translation service, breaking sentence parsing. Added file extension detection to skip image paths.

**Service Availability**: EasyNMT can be temperamental on startup. Added health check that queries the `/model_name` endpoint before attempting translations.

**Batch Size Tuning**: Started with 20-line batches, but found 10 lines more reliable for staying under EasyNMT's word limit while maintaining context.

**Hash Storage**: Originally planned database storage for file hashes, but filesystem-based `.hash` files proved simpler and avoided database dependency for this service.

These learnings were folded back into documentation and informed similar features later.

## Why This Spec Worked
This spec followed the principles discussed:
- **Problem-first**: Started with the actual problem (manual translation is slow) not the solution (use EasyNMT)
- **Clear scope**: Explicitly called out what we weren't doing (translation memory, UI editing)
- **Right level of detail**: Specified what needed to happen (preserve markdown structure) without prescribing exact implementation
- **Living document**: Open questions were resolved and decisions documented as implementation progressed
- **Collaborative**: Raised during implementation issues (like image filename handling) were discussed and resolved, then documented

The result: a feature that's been running in production for months, automatically translating every blog post with minimal intervention.

# In Conclusion

Writing good feature specs in an agile environment is a skill that improves with practice. The goal isn't to write perfect specs up front (they don't exist); it's to write specs that help your team get started and evolve as you learn.

The key principles:
- **Specs are tools, not contracts** - Add detail where you need it, evolve as you learn
- **Start with the problem, not the solution** - Implementation flows from understanding the problem
- **Collaborate, don't dictate** - Everyone contributes to making the spec better
- **Define "done" clearly** - Prevent feature metastasis with concrete, testable criteria
- **Out of scope matters** - What you're NOT doing is as important as what you are
- **Expect evolution** - Features change as you build them; capture that learning
- **Keep specs current** - They become the foundation for tests, docs, and future development

The hardest part isn't writing the initial spec. It's knowing when to stop working on a feature. Without clear "done" criteria, features grow forever and never ship.

This is why agile estimation is so difficult. You're not just estimating implementation time; you're estimating learning time. How long will it take to discover what actually solves the problem? Nobody knows, because you haven't discovered it yet.

The best you can do: be clear about what "done" means, time-box uncertainty, and update the spec as you learn. Treat it like code: version it, refactor it, improve it.

A good spec empowers developers to solve problems intelligently while knowing exactly when they can stop. It's a conversation starter, not a straitjacket.

And if you're a developer reading a spec that doesn't make sense or has no clear "done" criteria: speak up. It's not being difficult; it's being professional. Better to sort it now than to build a feature that keeps growing until it takes over the entire application.
