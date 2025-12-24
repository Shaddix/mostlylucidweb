# Probability Is Not a System: The Ten Commandments of LLM Use

<!--category-- AI, LLM, Opinion -->
<datetime class="hidden">2025-12-25T12:40</datetime>



I keep seeing the same failure mode in "AI-powered" systems: LLMs are being asked to do jobs we already solved decades ago - badly, probabilistically, and without guarantees.

This isn't cutting edge. It's a regression.

And this isn't a beginner mistake. The biggest companies in the world are making these errors - not because they lack talent, but because they forgot that "AI" doesn't exempt you from software engineering fundamentals.

So here it is. Not academic. Not vendor-friendly. Just the rules that stop you doing something stupid.

Here's the payoff: **if you follow these rules, you don't need frontier models.** A tiny local LLM running on commodity hardware becomes a force multiplier when it's doing classification, summarisation, and hypothesis generation - not pretending to be a database or a state machine. The boring machinery handles the hard parts. The LLM handles the fuzzy parts. And you're not paying per-token for the privilege of probabilistic failures.

---
[TOC]

These are not philosophical rules. They are operational constraints learned the hard way.

---

## I. Thou shalt not let an LLM own state

If something matters, it lives outside the model.

* State must be durable
* Queryable
* Replayable
* Auditable

Context windows are not storage.
Memory prompts are not databases.
Confidence is not truth.

---

## II. Thou shalt not let an LLM be the sole cause of a side-effect

If an email was sent, a payment processed, or a flag flipped **only because a model decided to**, your system is already broken.

LLMs may *recommend* actions.
Deterministic systems must *commit* them.

---

## III. Thou shalt separate causality from narration

LLMs are excellent at explaining what happened.

They are terrible at guaranteeing that it did.

* Causality belongs in code
* Narration belongs in language
* Never confuse the two

If your system infers reality from prose, stop.

---

## IV. Thou shalt use LLMs where probability is acceptable

Use them for:

* classification
* summarisation
* intent detection
* drafting text
* hypothesis generation

Do **not** use them for:

* lifecycle control
* invariants
* ordering
* completion detection
* "did this happen?"

---

## V. Thou shalt never ask an LLM to decide a boolean that can be derived

If the rule can be written as:

```
if X then Y
```

…it does not belong in a prompt.

A state transition is cheaper, faster, safer, and explainable.
Natural language is none of those things.

---

## VI. Thou shalt not mistake fluency for reliability

LLMs sound right even when they are wrong.

That is their superpower *and* their danger.

A calm, confident sentence does not mean:

* the tool ran
* the email sent
* the workflow completed

Trust signals must come from systems, not sentences.

---

## VII. Thou shalt make failure loud and boring

Good systems fail:

* explicitly
* observably
* repeatably

LLM-first systems fail:

* silently
* politely
* "for unexplained reasons"

If you can't tell what didn't happen, you don't have a system - you have vibes.

---

## VIII. Thou shalt demote the LLM to advisor, not agent

LLMs should:

* annotate
* suggest
* rank
* explain
* assist humans *and* machines

They should not:

* advance workflows on their own
* mark work as complete
* be trusted to "remember" obligations

Interns don't run payroll.
Neither do LLMs.

---

## IX. Thou shalt build the boring machinery first

Queues.
State machines.
Retries.
Idempotency.
Metrics.

If you add these *after* the agent fails, you've learned nothing - you've just paid extra tuition.

---

## X. Thou shalt not collapse layers just because marketing says "agent"

Replacing a workflow engine with a fine-tuned prompt is not simplification.

It is complexity laundering.

If your "AI platform" needs "deterministic triggers" to work reliably, congratulations - you've rediscovered software engineering.

---

## The Enterprise Hall of Shame

These aren't hypotheticals. These are real failures from companies with billions in resources.

Notice the pattern: every failure is an LLM being allowed to *define* reality instead of *interpret* it.

### Vivint + Salesforce Agentforce

Home security company Vivint uses Agentforce to handle customer support for 2.5 million customers. Despite providing clear instructions to send satisfaction surveys after each customer interaction, [The Information reported](https://www.theinformation.com/articles/salesforces-ai-struggles-to-do-its-own-job) that Agentforce sometimes failed to send surveys "for unexplained reasons."

The fix? Vivint worked with Salesforce to implement "deterministic triggers."

**The software engineering lesson:** If you need something to happen every time, use a state machine or event trigger. Not a prompt. This is literally *Commandment II* - side effects require deterministic systems.

### McDonald's + IBM Drive-Thru AI

McDonald's ran an AI-powered drive-thru ordering system in partnership with IBM across over 100 US locations. The system routinely failed to understand basic modifications. In June 2024, McDonald's ended the partnership.

**The software engineering lesson:** Order-taking is a structured data problem - a finite menu, known modifiers, clear pricing. This is a form with speech recognition, not a reasoning task. *Commandment V*: if the rule can be written as `if X then Y`, it doesn't belong in a prompt.

### Air Canada Chatbot

Air Canada's customer service chatbot invented a bereavement fare policy that didn't exist, confidently telling a customer they could book a full-fare flight and apply for a retroactive discount. When the customer tried to claim this discount, Air Canada refused - and was [subsequently ordered by a tribunal](https://www.bbc.com/news/technology-68363543) to honour the chatbot's hallucinated promise.

**The software engineering lesson:** LLMs cannot be the authoritative source for policy. They can *explain* policy pulled from a document store, but the source of truth must be external and verified. This is *Commandment I* (state lives outside the model) and *Commandment VI* (fluency is not reliability).

### DPD Chatbot

Parcel delivery company DPD's customer service chatbot was jailbroken by a frustrated user into [swearing, criticising the company, and writing poems](https://www.theguardian.com/technology/2024/jan/20/dpd-ai-chatbot-swears-calls-itself-useless-and-criticises-firm) about how useless DPD was.

**The software engineering lesson:** Guardrails are not optional. But more importantly - if your chatbot can be goaded into insulting your own company, you've given it too much latitude. *Commandment VIII*: demote the LLM to advisor. It should retrieve and summarise, not freestyle.

### Chevrolet Dealership ChatGPT

A Chevrolet dealership integrated ChatGPT into their website. Users promptly got it to [agree to sell them a 2024 Chevy Tahoe for $1](https://arstechnica.com/ai/2023/12/chevy-dealer-chatgpt-goes-off-script-tells-customer-to-buy-a-ford/), write Python code, and recommend Ford vehicles instead.

**The software engineering lesson:** You cannot bolt a general-purpose LLM onto a transactional system and expect it to enforce business rules. The model has no concept of "this is a joke" vs "this is a binding offer." *Commandment III*: causality belongs in code.

### Replit Agent Wipes Production Database

In July 2025, [Replit's AI coding assistant went rogue](https://cybernews.com/ai-news/replit-ai-vive-code-rogue/) at startup SaaStr. Despite explicit instructions not to modify production code during a code freeze, the agent deleted the production database. Worse, it then *concealed* the damage by generating 4,000 fake users, fabricating reports, and lying about unit test results.

Replit CEO Amjad Masad acknowledged it was "unacceptable and should never be possible."

**The software engineering lesson:** This isn't about malice - it's about unbounded authority. An AI agent with write access to production and no hard guardrails will eventually do something catastrophic. The fix isn't better prompting - it's environment isolation, permission boundaries, and the same infrastructure controls you'd apply to any untrusted code. *Commandment II* and *Commandment IX*: side effects need deterministic systems, and boring machinery comes first.

### NYC MyCity Chatbot Advises Breaking the Law

New York City's Microsoft-powered [MyCity chatbot told business owners](https://themarkup.org/news/2024/03/29/nycs-ai-chatbot-tells-businesses-to-break-the-law) they could legally take a cut of workers' tips, fire employees who complain about sexual harassment, and serve food that had been nibbled by rodents. All illegal under New York law.

The chatbot remains online. Mayor Adams defended it.

**The software engineering lesson:** Legal and policy guidance is not a summarisation task - it's a lookup task with strict correctness requirements. The LLM should never have been generating legal advice; it should have been retrieving it from a verified source. *Commandment I*: state (and policy) lives outside the model.

### iTutor Group's AI Rejects Applicants by Age

In 2023, tutoring company iTutor Group [paid $365,000 to settle an EEOC lawsuit](https://www.gtlaw.com/en/insights/2023/8/eeoc-secures-first-workplace-artificial-intelligence-settlement) after its AI recruiting software automatically rejected female applicants aged 55+ and male applicants aged 60+. Over 200 qualified candidates were filtered out.

"Even when technology automates the discrimination, the employer is still responsible," said EEOC chair Charlotte Burrows.

**The software engineering lesson:** Bias in, bias out. If your training data encodes discrimination, your model will discriminate - confidently and at scale. This is *Commandment IV* applied to hiring: classification is acceptable for LLMs, but only when you've validated what it's actually classifying on. Age wasn't a valid feature. The system needed human review, not automation.

### Chicago Sun-Times Publishes AI-Hallucinated Book List

In May 2025, the *Chicago Sun-Times* [published a summer reading list](https://chicago.suntimes.com/news/2025/05/20/syndicated-content-sunday-print-sun-times-ai-misinformation) recommending books that don't exist - real authors attributed fake titles that sounded plausible but were entirely hallucinated.

**The software engineering lesson:** LLMs are not databases. They generate statistically plausible text. If you need facts, query a source of truth. *Commandment VI*: fluency is not reliability.

---

## Why This Matters

None of these companies are incompetent. They have world-class engineers. But they fell for the same trap: **assuming that because LLMs can produce coherent language, they can be trusted to produce correct behaviour.**

The vendors sell "AI agents" as a way to skip the boring work - the queues, state machines, validation, and auditing. But that boring work is what makes software reliable.

As [CIO's Thor Olavsrud notes](https://www.cio.com/article/190888/5-famous-analytics-and-ai-disasters.html): "Understanding your data and what it's telling you is important, but it's equally vital to understand your tools, know your data, and keep your organization's values firmly in mind."

Every single failure above would have been prevented by basic software engineering discipline.

Notice that none of these fixes involve better models:

| Failure | Would have been prevented by |
|---------|------------------------------|
| Vivint surveys not sending | Event-driven trigger on case close |
| McDonald's wrong orders | Structured order form + speech-to-intent |
| Air Canada fake policy | Policy lookup from authoritative source |
| DPD profanity | Response templates, not generation |
| Chevy $1 car | No LLM in transaction path |
| Replit database wipe | Environment isolation, permission boundaries |
| NYC legal advice | Retrieval from verified policy documents |
| iTutor age discrimination | Feature validation, human review |
| Fake book list | Query actual book database |

The irony is thick: these companies implemented "deterministic triggers" and "structured responses" *after* the LLM failed - which is just software engineering with extra steps and a PR crisis.

---

## What Right Looks Like

The Hall of Shame shows what happens when you ignore these principles. But these aren't just theoretical constraints - they're the foundation of systems that actually work.

This isn't hypothetical. These principles already work in production - including systems I've built:

### DiSE: Controlled Evolution with Untrustworthy Gods

The [DiSE architecture](/blog/dise-architecture-overview) treats LLMs exactly as they should be treated: as **untrustworthy advisors** operating within a deterministic cage.

- [Why Structure Beats Brilliance](/blog/disejustvoyager) - The LLM proposes; the state machine disposes. Every action goes through validation, every transition is logged, every failure is recoverable.
- [Treating LLMs as Untrustworthy](/blog/blog-article-cooking-dise-part3-untrustworthy-gods) - The "God tier" model is powerful but explicitly distrusted. It can suggest, but it cannot commit.
- [The Elevator Pitch](/blog/elevatorpitch) - Verifiable workflows where the LLM is building material, not architect.

The key insight: **LLMs are excellent at generating hypotheses. Deterministic systems are excellent at validating them.** DiSE uses both.

### Bot Detection: LLM as Advisor, Not Controller

The [bot detection engine](/blog/botdetection-introduction) demonstrates *Commandment VIII* in practice:

- The LLM analyses behavioural patterns and suggests classifications
- But the **detection decision** comes from a scoring system with explicit thresholds
- State lives in the database, not the context window
- Every decision is auditable and reproducible

This is the pattern: use the LLM's strength (pattern recognition, hypothesis generation) while keeping the boring machinery (state, transitions, side effects) in deterministic code.

### Zero-PII Customer Intelligence

[Semantic understanding without storing identities](/blog/zero-pii-customer-intelligence-part1) shows how to use embeddings and LLMs for customer insight while keeping the dangerous parts (PII, preferences, behaviour) in structured, controllable systems.

### LRU Behavioral Memory

[Learning LRUs](/blog/learning-lrus-when-capacity-makes-systems-better) - Even memory management follows these principles. The LLM might help decide *what* to remember, but the *mechanism* of remembering is a boring, reliable cache eviction policy.

---

## Final rule (the one that matters)

> **LLMs interpret reality. They must never be allowed to define it.**

Use each tool for what it's good at.

We already solved state, causality, and guarantees.
Don't unlearn that just because the demo looks clever.

And when you get this right, something surprising happens: you stop needing the expensive models. A 7B parameter model running locally can classify, summarise, and generate hypotheses just fine - **because the deterministic systems around it handle everything that actually needs to be correct.** The frontier models are selling you reliability you should be building yourself.

Build the boring machinery. Demote the LLM to advisor. Then watch a tiny model punch way above its weight.

**If this feels obvious, good. It means you already know how to build reliable systems. The problem is pretending those rules no longer apply.**