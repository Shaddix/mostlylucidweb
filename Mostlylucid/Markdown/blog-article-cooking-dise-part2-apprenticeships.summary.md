# Document Summary: blog-article-cooking-dise-part2-apprenticeships.md

*Generated: 2025-12-18 16:29:41*

## Executive Summary

Note: This is Part 2 in the "Cooking with DiSE" series, exploring practical patterns for production-scale workflow evolution. [s3] Today we're talking about something that sounds obvious in hindsight but is weirdly uncommon: workflows that start with supervision, prove themselves, then graduate to run independently—until they need help again. [s4] Here's something ridiculous about how we run AI workflows in production today: [s5] We've normalized paying for monitoring that provides no value. [s6] Not "low value." [s7]

## Topic Summaries

### Cooking with DiSE (Part 2): Graduated Apprenticeships - Training Workflows to Run Without a Safety Net

*Sources: sentence-2, sentence-3*

Note: This is Part 2 in the "Cooking with DiSE" series, exploring practical patterns for production-scale workflow evolution. [s3] Today we're talking about something that sounds obvious in hindsight but is weirdly uncommon: workflows that start with supervision, prove themselves, then graduate to run independently—until they need help again. [s4]

### The Problem: We're Paying Therapists to Watch Perfect Patients

*Sources: sentence-4, sentence-5, sentence-6, sentence-8, sentence-9, sentence-10, sentence-12, sentence-14, sentence-15, sentence-17, sentence-19*

Here's something ridiculous about how we run AI workflows in production today: [s5] We've normalized paying for monitoring that provides no value. [s6] Not "low value." [s7] When a workflow has executed successfully 1,000 times with the same quality metrics, the same performance characteristics, the same everything—why are we still paying an AI to watch it? [s9] It's like hiring a lifeguard to watch Olympic swimmers practice in a kiddie pool. [s10] But here's what makes it worse: when things DO go wrong, our current monitoring often misses it anyway. [s11] Predefined thresholds. [s13] What we actually need is the opposite: [s15] Heavy monitoring when workflows are new or unstable - Learn what "good" looks like [s16] Re-engage heavy monitoring if quality degrades - Detect, diagnose, and fix [s18] This is the Apprenticeship Pattern. [s20]

### The Apprenticeship Pattern: From Supervised to Independent

*Sources: sentence-20, sentence-21*

Think about how humans learn a new job: [s21] Workflows should follow the same pattern. [s22]

### Phase 1: Apprentice Mode (Heavy Monitoring)

*Sources: sentence-22, sentence-23, sentence-24*

In Apprentice Mode: [s23] Every single tool call is instrumented: [s24] The Monitoring AI (fast model with escalation): [s25]

### Try It Yourself

*Sources: sentence-149, sentence-152, sentence-155, sentence-156, sentence-158*

Repository: https://github.com/scottgal/mostlylucid.dse [s150] src/monitoring_manager.py - Tiered monitoring [s153] src/proactive_evolver.py - Trend-based evolution [s156] First 50 executions: Full monitoring, learning profile [s157] On drift: Auto-investigate and evolve [s159]

### Series Navigation

*Sources: sentence-161*

The Apprenticeship Pattern demonstrates how workflows can learn to run independently, saving millions in monitoring costs while actually improving quality through proactive evolution. [s162]

## Processing Trace

| Metric | Value |
|--------|-------|
| Document | bert-extractive |
| Chunks | 162 total, 24 processed |
| Topics | 10 |
| Time | 1.1s |
| Coverage | 15% |
| Citation rate | 1.00 |
