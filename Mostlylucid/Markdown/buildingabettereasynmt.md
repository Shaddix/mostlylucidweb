# Building a better EasyNMT


# Introduction

Since the start of this blog a big passion has been auto translating blog articles. I even wrote w whole system to make that happen with an amazing project called EasyNMT. HOWEVER if you just checked that repo you knoiw there's an issue...it's not been touched for YEARS. It's a simple, quick way to get a translation API without the need to pay for some service / run a full size LLM to get translation (slowly). 


# Problems
Oh my, there's many. EasyNMT was build almost a decade ago. Technology has moved on...plus it was never intended to be a production level system. Here are some of the issues:
1. It crashes...a LOT. It's not designed to recover from issues so often just fallse over.
2. It's SUPER PICKY about it's input. Emoticons, symbols, even numbers can confuse it.
3. It's not designed for any load; see above. It was never designed to be.
4. It's not designed to update it's models / be (easily) built with built in models
5. It's GPU CUDA stuff is ancient so slower than it needs to be, 
6. You can't fix anything; the python code is on the repo but again *not great*.

# Solution 
So...I decided to build a new and improved EasyNMT, now mostlylucid-nmt. 