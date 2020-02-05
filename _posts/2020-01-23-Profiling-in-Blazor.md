---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor, performance]
---

# How to profile a production Blazor client-side app ?

Performance optimization is one of the programming task that I prefer. It's really rewarding and requires knowledge, practice, creativity ... The first thing you need to learn when optimizing performance is that [you should always profile before optimizing](https://twitter.com/SitnikAdam/status/1223263855989665792/photo/1). And always, when possible, favor production profiling instead of developer or staging profiling. Why ? Well you optimize for your production environment and this environment has different CPU, disk, memory, OS ... specification than the other environment. So If you want your work to be useful, you need to profile production environment. For server-side profiling, with a .net stack there are many tools :
- WinDBG
- MiniProfiler
- SQL Server Profiler
- Jetbrain dotTrace
- APM provider (like Azure AppInsight or Datadog)
- dotnet trace
- many more

Each of them has its role and if your team embrace DevOps values ("you build it, you ship it") or you are just close to the operations then you should at least know them and know how to use them on your __PRODUCTION__ environment. Let me insist one more time on this : optimizing without profiling production environment is nearly worthless. You can have result, but you can't be sure about it, it will be only guessing. Given the fact that, most tof the time, optimizing makes your code harder to read : you prefer to do it only where it matters.

But with Blazor we are working on the client-side, most of the tools I described can't be used client-side (AFAIK) because they can't be executed on your browser. In this blog post I will explain how you can profile a production Blazor WASM application.

_I think this can also be used by Blazor server-side but the tools for profiling on the server are already enough._

## Use the browser profiling

For this blog post I will use Google Chrome as the browser