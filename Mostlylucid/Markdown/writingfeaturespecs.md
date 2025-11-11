# Writing Feature Specs That Developers Don't Hate

<!--category-- Software Development, Documentation -->
<datetime class="hidden">2025-11-11T10:00</datetime>

# Introduction
Over the years I've read hundreds of feature specifications. Some were brilliant; most were bloody awful. The difference between a good spec and a bad one isn't about length or formality; it's about whether it actually helps developers build the right thing without driving them mad in the process.

A good spec is like a well-drawn map; it shows you where you're going without prescribing every single step. A bad spec is either so vague it's useless or so detailed it becomes a straightjacket that prevents you from solving problems intelligently.

[TOC]

# What Makes a Good Feature Spec

## Clarity of Purpose
Before you write a single word about implementation, you need to answer one question: **WHY?**

Why are we building this? What problem does it solve? Who benefits? If you can't answer these questions clearly, you haven't got a feature; you've got a wish list.

I've seen countless specs that jump straight into "User clicks button X which calls API Y" without ever explaining what the user is actually trying to achieve. This is arse-backwards. The implementation details should flow naturally from understanding the problem.

A good spec starts with:
1. **The Problem Statement** - What's broken or missing?
2. **The User Impact** - Who cares and why?
3. **Success Criteria** - How do we know we've solved it?
4. **Non-Goals** - What are we explicitly NOT doing? (This is crucial)

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
Explicitly call out what you're NOT doing. This prevents scope creep and endless "but couldn't we just..." conversations during implementation.

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

# Specs in Agile Environments

"But we're agile! We don't need specs!" I hear this a lot. It's bollocks.

Agile doesn't mean "no planning" or "no documentation." It means responding to change over following a plan. You still need to understand what you're building before you build it.

In agile environments, specs might be:
- Lighter weight (wiki pages rather than formal documents)
- More iterative (updated each sprint)
- Closer to the team (written by the team, not external to it)

But you still need them. A user story on a card isn't enough for anything non-trivial. You need detail somewhere.

# The Spec Review Process

A spec isn't done when you finish writing it; it's done when it's been reviewed by the people who'll use it.

**Developer Review** - Will this actually work? Are there technical constraints we haven't considered? Is there enough detail to implement?

**Product Review** - Does this solve the right problem? Does it align with product strategy? What's missing?

**Design Review** - Does the user experience make sense? Have we considered accessibility? What about mobile?

**QA Review** - Can we test this? Are the acceptance criteria clear enough? What about edge cases?

Get reviews from all these perspectives before you start implementation. Finding problems in the spec costs minutes; finding them in production costs weeks.

# In Conclusion

Writing good feature specs is a skill that improves with practice. The goal isn't to write perfect specs (they don't exist); it's to write specs that help your team build the right thing efficiently.

Remember:
- Start with the problem, not the solution
- Be clear but not prescriptive
- Think through edge cases and errors
- Get reviews from multiple perspectives
- Treat specs as living documents
- Speak up when you find spec bugs

A good spec is a conversation starter, not a straitjacket. It should empower developers to solve problems intelligently, not force them to implement bad ideas perfectly.

And if you're a developer reading a spec that doesn't make sense: speak up. It's not being difficult; it's being professional. Better to sort it now than to build the wrong thing beautifully.
