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

But you can't deploy every try to see if it is efficient right ? For me the process is the following :
- Profile the production for finding on which code path you should work
- Profile on your workstation to see how long this code path takes on your environment
- Optimize on your workstation and profile afer each optimization until this number is lower
- Deploy your change to production (if your tests passes)
- Profile the production for checking if your optimization is truly efficient. 
- If you had to make your code less readable and your optimization is not efficient in production, then rollback your change and go back to square 1.

But with Blazor we are working on the client-side, most of the tools I described can't be used client-side (AFAIK) because they can't be executed on your browser. In this blog post I will explain how you can profile a production Blazor WASM application.

_I think this can also be used by Blazor server-side but the tools for profiling on the server are already enough._

## Browser profiling tools

For this blog post I will use Google Chrome. Google Chrome provides a tools for profiling client-side code. To do so start of DevTools then :
- Tab "Performance"
- Click on the record button on the top left of the frame
- Execute the code you want to profile
- Click on the stop button

Then you will have something like that

![Chrome Profile result](/assets/img/ScreenShot-Chrome-Profile.png "Chrome Profile result")

I won't describe you how to read everything in this profiling result (you can use [this doc](https://developers.google.com/web/tools/chrome-devtools/rendering-tools)) but we can quickly understand the problem we faces with a Blazor WASM app : the executed .net code is behing a "wasm-function..." step and you can't relate it to your code. But there is already something interesting, you can see how much time the rendering (DOM update) is taking by searching for the method "renderBatch".

## Adding custom timings to profiling results

The good news is, there is some Javascript API for adding informations to the profiler result : console.time and console.timeEnd. You can call it like that

```cs
@inject IJSRuntime jsRuntime;
@code {
    //...
    public async Task Method(){
        try{
            await jsRuntime.InvokeVoidAsync("console.time", "sub task");
            await Task.Delay(100);//do something 
        }
        finally{
            await jsRuntime.InvokeVoidAsync("console.timeEnd", "sub task");
        }
    }
    //...
}
```

With this code, if you have a profiling session on then the profiling result will display a line called "Console" which will display "My step" in the profiling session. So you will be able to see how long it took for your code to run, how much CPU / Network / memory it used, how it relates to other profiled method call... :

![Chrome Profile result](/assets/img/ScreenShot-Chrome-Profile-Console.png "Chrome Profile result")

We can see the "sub task" label is displayed on the profiling results. If I create multiple label then I can easily identify which code is taking a long time to run.

## Overhead

When you are profiling, the host is executing more stuff. So there is always an overhead in your profiling, always. Does it matter ? Yes and No :

- Yes : because if you add profling code in your production code, you don't want it to slow down the production.
- No : you don't care if a function takes 100ms or 110ms when profling, you just want this number to go down.

If I look at the profiling session I did on the previous paragraph, I can see that my method took 109.32ms instead of the 100ms expected. Why is that ? There is multiple potential culprit of this 
- JS interop
- "await" overhead in mono-wasm
- task.Delay is not precise

If I zoom on the profiling result I can see that there is a few ms before the JS interop call and the Delay call and then a few ms between what I think is the callback of the Delay and the timeEnd JS interop. So this overhead is clearly due to JS Interop back and forth.

## The BrowserInterop package

## Conclusion

