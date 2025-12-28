# HTTP Over the Decades: A Story of Physics, Latency, and Grudging Adaptation

<!--category-- HTTP, Networking, Web Development, Opinion -->
<datetime class="hidden">2025-12-28T18:00</datetime>

HTTP didn't evolve. It was *forced* to change by physics, latency, and misuse.

Every version exists because the previous one hit a hard constraint. If you understand those constraints, you understand why the web works the way it does - and why so many "best practices" are actually workarounds for protocol limitations we've been dragging around for thirty years.

I've been building web applications since 1997. I've debugged HTTP/1.0 connection storms, watched browsers open six connections per host as a "feature", cursed at HTTP/2's TCP head-of-line blocking on mobile networks, and finally seen HTTP/3 admit what we knew all along: the network is the problem.

This isn't a protocol tutorial. It's the story of how we got here.

[TOC]

## HTTP/1.0: Stateless Text Over Unreliable Pipes

### The World That Made It

HTTP/1.0 was designed in 1996 for a world that no longer exists. To understand why it worked, you need to understand what "the web" meant in 1996:

- **Dialup connections** at 28.8kbps (if you were lucky)
- **Pages were documents** - text, maybe a few small images, hyperlinks
- **Users clicked and waited** - instant response wasn't expected
- **Servers were expensive** - universities and companies, not everyone

In this world, the bottleneck was **bandwidth**, not latency. A 50KB page took 15 seconds to download on dialup. Who cared about an extra 200ms handshake?

**What HTTP/1.0 optimised for:**
- Simplicity (debuggable with telnet)
- Statelessness (servers don't track connections)
- Human-readability (text protocol)
- One request, one response, connection closed

```
GET /index.html HTTP/1.0
Host: example.com

HTTP/1.0 200 OK
Content-Type: text/html

<html>...
```

Simple. Elegant. Perfect for 1996. And completely unusable for anything beyond document retrieval.

### The Pressure That Broke It

Then the web changed. Fast.

By 1998, pages weren't just documents. They had stylesheets, JavaScript, multiple images. A single page might need 20-30 separate resources.

Every single request required:

1. **TCP handshake** (1 RTT)
2. **Request/first byte** (~1 RTT)
3. **Response received**
4. **Connection closed**
5. Repeat for every image, stylesheet, script...

A page with 20 resources meant 20 TCP handshakes. On a 200ms latency connection (still common), that's 4 seconds of *just handshaking* before any content transferred.

The fundamental assumption of HTTP/1.0:

> **Time was cheap.**

In 1996, it was. Bandwidth was the bottleneck. By 1999, bandwidth was improving but **latency wasn't** - and suddenly those round trips mattered.

HTTP/1.0 was designed for documents. The web had become an application platform. Something had to give.

## HTTP/1.1: We'll Patch It

### The World That Made It

HTTP/1.1 arrived in 1997, just as the web was exploding. The dotcom boom was starting. Every business needed a website. Web pages were getting complex - tables for layout, JavaScript for interactivity, images everywhere.

The pressure was clear: **connection-per-request was killing performance**. But completely redesigning HTTP wasn't an option. Too much infrastructure already depended on it. The solution had to be backward-compatible.

**What changed:**
- **Persistent connections** (`Connection: keep-alive` by default) - reuse the TCP connection
- **Chunked transfer encoding** (stream responses without knowing size upfront)
- **Host headers** (virtual hosting - multiple sites per IP, crucial for shared hosting)
- **Caching semantics** (`Cache-Control`, `ETag`, conditional requests)
- **Pipelining** (send multiple requests without waiting for responses)

**What didn't change:**
- Still sequential responses (even with pipelining)
- Still text headers (repeated on every request)
- Still one response at a time per connection

```
GET /page.html HTTP/1.1
Host: example.com
Connection: keep-alive

HTTP/1.1 200 OK
Transfer-Encoding: chunked
Cache-Control: max-age=3600

...
```

### Why It Sort-Of Worked

HTTP/1.1 didn't actually solve the performance problem. It just moved it around.

**Pipelining was a failure.** The spec allowed sending multiple requests on one connection, but:
- Responses had to come back *in order* (head-of-line blocking)
- One slow response blocked everything behind it
- Buggy proxies mangled pipelined requests
- Most browsers disabled it entirely

So browsers cheated. Instead of fixing the protocol, they worked around it:

- Open **6 parallel connections per host** (browser limit)
- Use **domain sharding** (`static1.example.com`, `static2.example.com`) to multiply connections
- **Sprite sheets** to combine images
- **CSS/JS bundling** to reduce requests
- **CDNs** to reduce latency

None of this was the protocol working as designed. It was the entire ecosystem compensating for HTTP/1.1's fundamental limitation.

> **HTTP/1.1 scaled by cheating, not by fixing the model.**

### Why It Survived Anyway

HTTP/1.1 worked *well enough* for fifteen years. Not because it was good, but because the environment compensated:

- **Broadband arrived.** 20ms handshakes instead of 200ms. The pain was tolerable.
- **Moore's Law helped.** Servers could handle six connections per browser by 2005.
- **CDNs masked the problem.** Edge servers 20ms away instead of 200ms.
- **Desktop dominated.** Wired connections, low latency, reliable networks.

The protocol was still broken. We were just lucky the world was hiding it.

### The Real Cost

Those six connections per host weren't free:

- Each connection meant another TCP handshake
- Each connection competed for bandwidth
- Servers had to maintain more concurrent connections
- **Head-of-line blocking just moved to TCP** - lose a packet on one connection, that connection stalls

The web got faster in the HTTP/1.1 era. But not because of HTTP/1.1. It got faster because:
- Networks got faster
- Caching got smarter
- CDNs absorbed latency
- Browsers got better at parallelism

We were building an application platform on a document retrieval protocol. The protocol was losing. We just didn't feel it yet.

## The Uncomfortable Truth Nobody Admits

By 2010, web performance was a constant battle against HTTP itself.

The "best practices" of the era tell the story:
- Concatenate all your JavaScript into one file
- Sprite all your images together
- Inline critical CSS
- Use data URIs to avoid requests
- Domain shard to open more connections
- Put scripts at the bottom of the page

Every single one of these is a workaround for "HTTP/1.1 can't handle many requests efficiently."

I built many of these little tools like sprite generators, CSS compressors, we started using response compression etc...

Browsers weren't faster because HTTP improved. They were faster because they **worked around HTTP**.

Six connections per host is not a feature. It's a hack. Domain sharding is not a best practice. It's an admission of failure.

I spent years teaching developers to bundle assets, sprite images, and inline critical resources. None of that was "good architecture." It was **protocol damage control**.

The "HTTP is simple" myth persisted because the complexity was hidden in:
- Browser connection management
- CDN edge logic
- Build tool concatenation
- Server connection pooling

HTTP/1.1 worked because everything *around* it worked overtime to compensate.

If you've ever seen a diagram of HTTP that doesn't show latency, loss, or failure modes, it's lying to you. The protocol is simple. The reality isn't.

## HTTP/2: We Fixed the Wrong Layer

### The World That Made It

By 2010, the web had transformed again. Two massive shifts changed everything:

**1. Mobile happened.**
The iPhone launched in 2007. By 2012, mobile web traffic was exploding. Suddenly users weren't on wired broadband - they were on 3G, then 4G, with variable latency and frequent packet loss. The assumptions that made HTTP/1.1 tolerable were breaking down.

**2. Web applications replaced web pages.**
Gmail. Google Maps. Facebook. Twitter. These weren't documents with links. They were applications that needed dozens or hundreds of resources, real-time updates, and instant response. The "six connections per host" hack was showing its age.

Google felt this pain acutely. They had massive scale, performance-obsessed engineers, and the data to prove HTTP/1.1 was the bottleneck. In 2009, they started SPDY.

### SPDY: Google's Experiment

SPDY (pronounced "speedy") was Google's answer to HTTP/1.1's limitations. It wasn't a standards effort - it was Google shipping code and seeing what worked.

**What SPDY introduced:**
- **Binary framing** instead of text parsing
- **Multiplexed streams** over a single TCP connection
- **Header compression** (later refined into HPACK for HTTP/2)
- **Server push** for proactive resource delivery
- **Stream prioritisation** to load critical resources first

Google deployed SPDY across Chrome and their own services. By 2012, Gmail, Google Search, and YouTube were all running SPDY. The results were compelling: 40-60% latency reduction in some cases.

**The path to HTTP/2:**

SPDY proved the concepts worked. The IETF took notice and used SPDY as the starting point for HTTP/2. The final HTTP/2 spec (2015) isn't identical to SPDY - header compression changed significantly (HPACK replaced SPDY's zlib-based approach after security concerns), and various details were refined - but the architecture is recognisably SPDY's.

Google deprecated SPDY in 2016 once HTTP/2 had sufficient adoption. Mission accomplished: they'd forced the web forward by shipping first and standardising later.

**What HTTP/2 changed (building on SPDY):**
- **Binary framing** (not text - more efficient parsing)
- **Multiplexing** (multiple requests/responses interleaved on one connection)
- **Header compression** (HPACK - no more repeating the same headers)
- **Server push** (send resources before client asks - though this proved hard to tune correctly and is now largely deprecated)
- **Single connection per origin** (no more connection explosion)

```
┌──────────────────────────────────────────┐
│           Single TCP Connection          │
├──────────────────────────────────────────┤
│ Stream 1: GET /page.html                 │
│ Stream 3: GET /style.css                 │
│ Stream 5: GET /app.js                    │
│ Stream 7: GET /logo.png                  │
│ (all interleaved, no ordering required)  │
└──────────────────────────────────────────┘
```

This is what HTTP/1.1 pipelining should have been. Streams are independent. One slow response doesn't block others. Headers are compressed. The protocol finally matched how we actually use the web.

### Why It Worked (On the Right Networks)

HTTP/2 was **perfect for the broadband web**. And in 2015, when it standardised, broadband was ubiquitous in the developed world.

**What improved:**
- Latency under light load (one connection, no handshake overhead per request)
- Fewer connections (less server overhead, less TCP slow-start)
- Better bandwidth utilization (no artificial connection limits)
- Header compression (repeated headers like cookies no longer cost bandwidth)
- No more concatenation/spriting needed (many small files are fine now)

For users on wired connections with low packet loss, HTTP/2 was a genuine improvement. The benchmarks looked great. The metrics improved. Google declared victory.

### The Pressure That Broke It

But the world had already moved on. **Mobile was now the majority of web traffic.**

And mobile networks have a fundamental property that broadband doesn't: **packet loss is common and unpredictable**.

TCP guarantees **in-order delivery**. If packet 47 is lost, packets 48-100 wait until 47 is retransmitted - even if they belong to completely independent HTTP/2 streams.

```
┌──────────────────────────────────────────┐
│              TCP Receive Buffer          │
├──────────────────────────────────────────┤
│ [pkt 45][pkt 46][  ?  ][pkt 48][pkt 49]  │
│                   ↑                       │
│         Waiting for packet 47             │
│                                          │
│ Stream 1 data: BLOCKED                   │
│ Stream 3 data: BLOCKED                   │
│ Stream 5 data: BLOCKED (has pkt 48-49)   │
│ Stream 7 data: BLOCKED                   │
└──────────────────────────────────────────┘
```

HTTP/2 solved head-of-line blocking at the application layer. Then TCP reintroduced it at the transport layer.

**One lost packet stalls all streams.** On a clean wired connection, packet loss is rare. On mobile networks, lossy WiFi, or congested links? Packet loss is constant. And because HTTP/2 uses a *single* connection (by design), one lost packet now blocks *everything* instead of just one of six connections.

HTTP/2 performance:
- **Wired, low loss**: Excellent
- **Mobile, moderate loss**: Sometimes *worse* than HTTP/1.1
- **High latency, any loss**: Catastrophic

The irony: HTTP/2 was meant to fix the web's multiplexing problem - just as mobile made loss impossible to ignore. TCP's guarantees made it worse on mobile than the protocol it replaced.

### Why HTTP/2 Felt Faster (Until It Didn't)

Initial HTTP/2 deployments showed real improvements:
- Fewer connections meant faster initial load (TLS handshake once, not six times)
- Header compression reduced overhead
- Multiplexing worked great for assets on the same origin

All those tools I built for 1.1? The all went away with HTTP/2, suddenly bundling was a BAD thing. HTTP/2's multiplexing was great for assets on the same origin, but it didn't help with cross-origin requests. We had to build new tools to bundle those (webpack on this site for example!).

But tail latency told a different story. The *median* got better. The P99 got worse.

I saw this firsthand: a client enabled HTTP/2 across their CDN and watched mobile P99 latency *increase* by 40%. The dashboards showed improvement on desktop. The support tickets came from mobile users. We spent a week figuring out that HTTP/2's "improvement" was making things worse for the majority of their traffic.

When HTTP/2 works, it works beautifully. When a packet drops on a mobile connection at the worst moment, everything freezes. And users notice freezes more than they notice fast medians.

The fundamental problem:

> **HTTP/2 assumed TCP was good enough.**

For broadband, it was. For mobile, it wasn't. And by the time HTTP/2 was standardised, mobile was already winning. We'd pushed as far as TCP could take us.

## HTTP/3: Fine, We'll Move Down the Stack

### The World That Made It

By 2015, the evidence was clear: **TCP was the problem**.

Google had been running QUIC experimentally since 2012. They had the data. On mobile networks, lossy WiFi, and high-latency connections, HTTP/2 over TCP was failing in ways that couldn't be fixed without changing the transport layer.

But you can't just "fix TCP." It's implemented in operating system kernels. It's baked into every router, firewall, and middlebox on the internet. Changing TCP means waiting decades for the entire internet infrastructure to upgrade.

So Google did something audacious: **they built a new transport protocol on top of UDP**.

### QUIC: The Protocol That Replaced TCP (Sort Of)

QUIC (originally "Quick UDP Internet Connections", now just QUIC) is a transport protocol that runs over UDP but provides TCP-like reliability. The key insight: **implement reliability in userspace, where you can iterate fast**.

**Why UDP as a foundation?**

UDP is deliberately minimal. It adds port numbers to IP and... that's basically it. No handshakes, no reliability, no ordering. Packets might arrive out of order, duplicated, or not at all. UDP doesn't care.

This "dumbness" is exactly what QUIC needed. By building on UDP, QUIC could:
- **Deploy without kernel changes** - it's just a library
- **Iterate quickly** - Chrome updates ship new QUIC versions constantly
- **Pass through most firewalls** - UDP port 443 usually works
- **Implement its own congestion control** - not stuck with TCP's algorithms

**What QUIC does differently:**

| Concern | TCP | QUIC |
|---------|-----|------|
| **Reliability unit** | Connection | Stream |
| **Handshake** | TCP + TLS separate | Combined (faster) |
| **Encryption** | Optional (TLS) | Mandatory (built-in) |
| **Head-of-line blocking** | All data blocked | Only affected stream |
| **Connection identity** | IP + Port | Connection ID |
| **Implementation** | Kernel | Userspace |

The connection ID is particularly clever. TCP identifies connections by IP address and port. Change either (switch WiFi networks, NAT rebinding) and the connection dies. QUIC uses a connection ID that survives network changes.

**The path to HTTP/3:**

Just like SPDY led to HTTP/2, Google's QUIC experiment led to HTTP/3. The IETF standardised QUIC in 2021 (RFC 9000), and HTTP/3 (RFC 9114) is simply HTTP semantics over QUIC instead of TCP.

The standardised QUIC differs from Google's original in some details (the crypto handshake was reworked, some header formats changed), but the core architecture - streams with independent loss recovery over UDP - remained.

HTTP/3 (2022) is HTTP over QUIC. It's the admission that we needed to go below HTTP to fix HTTP.

**What HTTP/3 changed:**
- **QUIC over UDP** (not TCP)
- **Stream-level loss recovery** (one lost packet only stalls its stream)
- **Faster connection setup** (0-RTT resumption possible)
- **Encryption by default** (TLS 1.3 built into QUIC)
- **Connection migration** (survives IP address changes)

```
┌──────────────────────────────────────────┐
│              QUIC Connection             │
├──────────────────────────────────────────┤
│ Stream 1: [pkt][pkt][pkt] ← flowing      │
│ Stream 3: [pkt][ ? ][pkt] ← waiting      │
│ Stream 5: [pkt][pkt][pkt] ← flowing      │
│ Stream 7: [pkt][pkt]      ← flowing      │
│                                          │
│ Lost packet only affects Stream 3        │
└──────────────────────────────────────────┘
```

**What stayed the same:**
- HTTP semantics (GET, POST, headers, status codes)
- Caching logic
- REST patterns
- Everything at the application layer

> **HTTP/3 didn't change HTTP. It changed how HTTP survives reality.**

### Why QUIC Matters (In Detail)

QUIC implements TCP's reliability guarantees *per stream*, not per connection. This is the key insight that fixes HTTP/2's fatal flaw.

**TCP's promise:** "Every byte arrives in order."
**QUIC's promise:** "Every byte *in this stream* arrives in order."

That distinction is why HTTP/3 doesn't collapse on lossy networks. Let me unpack how this actually works.

**Stream independence:**

In QUIC, each HTTP request/response pair gets its own stream. Streams are logically independent - they share a connection for efficiency, but packet loss on one stream doesn't block others.

```
QUIC Connection
├── Stream 1: GET /index.html     [packets 1, 2, 3]
├── Stream 3: GET /style.css      [packets 4, 5]     ← packet 5 lost
├── Stream 5: GET /app.js         [packets 6, 7, 8]
└── Stream 7: GET /logo.png       [packets 9, 10]

Packet 5 is retransmitted. Only Stream 3 waits.
Streams 1, 5, 7 continue unaffected.
```

Compare this to HTTP/2 over TCP: all those streams share one TCP byte sequence. Lose one packet, everything waits.

**Connection migration:**

TCP identifies connections by a 4-tuple: source IP, source port, destination IP, destination port. Change any of these and the connection is dead.

```
Phone on WiFi → walks out of range → switches to cellular
TCP: Source IP changed. Connection dead. Full reconnect.
QUIC: Connection ID unchanged. Streams continue seamlessly.
```

This matters because mobile users *constantly* switch networks. Walk out of your house, lose WiFi, switch to cellular. With TCP, every connection dies. With QUIC, you might not even notice.

**0-RTT resumption:**

Traditional TCP + TLS requires multiple round trips before sending data:
1. TCP SYN/SYN-ACK/ACK (1 RTT)
2. TLS handshake (1-2 RTT)
3. Finally: send your HTTP request

QUIC combines the transport and crypto handshakes. First connection takes 1 RTT. But for returning users with cached credentials, QUIC offers 0-RTT: send data immediately with your first packet.

```
First visit:    Client ──[handshake]──> Server ──[response]──> Client (1 RTT)
Return visit:   Client ──[data]──────────────────[response]──> Client (0 RTT)
```

0-RTT has security implications (replay attacks are possible), so it's limited to idempotent requests. But for most page loads, it's a significant latency win.

**Encryption as a first-class citizen:**

TCP was designed in the 1970s. Encryption was bolted on later via TLS. This means:
- TCP headers are plaintext (anyone can see port numbers, sequence numbers)
- Middleboxes can (and do) inspect and modify TCP traffic
- Encryption is optional, often negotiated after connection setup

QUIC encrypts almost everything from the start. Even the packet numbers are encrypted. This isn't just for privacy - it prevents the ossification problem where middleboxes depend on being able to read headers, making protocol evolution impossible.

```
TCP packet:   [IP header][TCP header (plaintext)][TLS encrypted payload]
QUIC packet:  [IP header][UDP header][QUIC header (mostly encrypted)][encrypted payload]
```

### The World That Needs It

HTTP/3 is designed for the world we actually live in:

- **Mobile-first**: More than half of web traffic is mobile
- **Unreliable networks**: WiFi, cellular, congested public networks
- **Global users**: High latency connections to distant servers
- **Privacy expectations**: Encryption is mandatory, not optional

The protocol finally matches the network reality. Thirty years after HTTP/1.0 assumed reliable wired connections, HTTP/3 acknowledges that reliability is the exception, not the rule.

### The Cost of Progress

HTTP/3 isn't free:

- **UDP is often blocked** by corporate firewalls (fallback to HTTP/2 needed)
- **More CPU** for QUIC's userspace implementation (not in the kernel like TCP, though this gap is closing with kernel bypass and optimised stacks)
- **New debugging tools** (tcpdump shows encrypted UDP, not readable HTTP)
- **Middlebox interference** (some networks mangle UDP unpredictably)

But for mobile-first applications, unreliable networks, and latency-sensitive services, HTTP/3 is the first version that matches how networks actually behave.

## The Real Lesson Across All Versions

Every HTTP version change happened because **the environment changed faster than the protocol**.

| Version | Era | Environment | Assumption | Why It Broke |
|---------|-----|-------------|------------|--------------|
| HTTP/1.0 | 1996 | Dialup, documents | Bandwidth is the bottleneck | Pages became apps, latency mattered |
| HTTP/1.1 | 1999-2015 | Broadband, web apps | Low latency hides inefficiency | Mobile arrived with packet loss |
| HTTP/2 | 2015-2022 | Broadband peak, mobile rising | TCP is reliable enough | Mobile became dominant, TCP failed |
| HTTP/3 | 2022+ | Mobile-first, global | Nothing is reliable | Still deploying... |

The pattern is clear:

1. Protocol is designed for current environment
2. Environment changes (faster than protocols can adapt)
3. Workarounds accumulate (CDNs, connection hacks, bundling)
4. New protocol acknowledges reality
5. Repeat

The protocol keeps getting pushed closer to the network. Each abstraction that seemed sufficient got peeled back when the environment demanded it.

**Other observations:**

- "Stateless" is a lie we tell ourselves to sleep at night. Every session cookie, auth token, and cache header is state management we've bolted on.
- Most performance wins came from *workarounds*, not protocol purity. CDNs, caching proxies, connection pooling, request coalescing - all compensating for protocol limitations.
- The "simple" thing kept moving. HTTP/1.0 was simple. Then we needed persistent connections. Then multiplexing. Then new transport. Each layer of complexity solved real problems.
- **Timing matters.** HTTP/2 would have been perfect if it arrived in 2005. By 2015, mobile had already changed the game. The protocol was solving yesterday's problem.

## What Hasn't Changed Since 1.0

Here's the uncomfortable part. Despite thirty years of protocol evolution:

**Requests still fail.**
Networks partition. Servers crash. Timeouts happen. Your code still needs retry logic.

**Networks still lie.**
200 OK doesn't mean the response is correct. Connection closed doesn't mean the request didn't succeed. Timeouts don't mean the server didn't process your request.

**Caches still serve stale data.**
`Cache-Control` is a request, not a command. Proxies do what they want. CDN invalidation is eventual at best.

**Clients still retry badly.**
User clicks button, nothing happens, clicks again. Now you have two requests. Idempotency matters.

**Developers still misunderstand idempotency.**
GET should be safe. PUT should be idempotent. POST is the wild west. Most APIs get this wrong.

```csharp
// This is still your problem, regardless of HTTP version
public async Task<Result> CreateOrderAsync(Order order)
{
    // What happens if this times out?
    // Did the server receive it?
    // Did it process it?
    // Will the client retry?
    // Will you create duplicate orders?
    
    // HTTP/3 doesn't save you here.
    // Idempotency keys do.
}
```

> **If you don't understand these, HTTP/3 won't save you.**

## What I Actually Learned

After building web applications across all these protocol versions, here's what stuck:

**1. The protocol is not your reliability layer.**

HTTP gives you request/response semantics. It doesn't guarantee delivery, correctness, or exactly-once processing. That's your job.

**2. Latency is the enemy, not bandwidth.**

Most performance problems are round-trip bound, not throughput bound. Reducing requests matters more than compressing payloads.

**3. Every "best practice" has an expiration date.**

Domain sharding was essential for HTTP/1.1. It's actively harmful for HTTP/2 (breaks the single-connection benefit). "Bundle everything into one file" was gospel; now shipping many small modules over HTTP/2 or HTTP/3 is often better. The tactics change. The goal (reduce latency) stays the same.

**4. Measure on real networks.**

Benchmarks on localhost prove nothing. Your users are on mobile networks, hotel WiFi, and saturated coffee shop connections. Test there.

**5. The boring parts matter most.**

Timeouts. Retries. Circuit breakers. Idempotency. Connection pooling. These unglamorous details determine whether your system works under load.

## Conclusion

HTTP didn't get faster because it got smarter. It got faster because we finally admitted **the network was the problem**.

Each version was right for its time:
- **HTTP/1.0** was perfect for dialup document retrieval
- **HTTP/1.1** was perfect for broadband web applications
- **HTTP/2** was perfect for low-loss wired connections
- **HTTP/3** is built for the mobile-first, unreliable-network reality we actually live in

The mistake was thinking any of them were permanent solutions. They were all responses to specific environmental pressures. When the environment changed, the protocol had to follow.

**HTTP versions are not upgrades in the consumer sense. They are tradeoffs optimised for different failure distributions.**

And still, after all of that, the fundamentals remain:
- Requests fail
- Networks lie
- Caching is hard
- Idempotency matters

The protocol evolved. The problems didn't disappear. They just moved.

Build systems that survive failures. Test on real networks. Understand that every HTTP version is a tradeoff shaped by its era, not an upgrade that obsoletes what came before.

That's thirty years of HTTP. That's the web we built. And in another decade, HTTP/3's assumptions will probably look as dated as HTTP/1.0's do now.

## Further Reading

- [RFC 9114: HTTP/3](https://datatracker.ietf.org/doc/html/rfc9114) - The official specification
- [RFC 9000: QUIC](https://datatracker.ietf.org/doc/html/rfc9000) - The transport protocol
- [HTTP/2 Explained](https://http2-explained.haxx.se/) - Daniel Stenberg's excellent overview
- [High Performance Browser Networking](https://hpbn.co/) - Ilya Grigorik's deep dive (free online)
