---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor,ASPNET Core]
---
# How to change Blazor dll file extension with ASPNET Core

Blazor is a framework for executing library (.dll) targeting .net standard, in the browser. When the app loads it get via xhr your application binaries. The problem is, in some internal network or for some AV providers, downloading .dll files is forbidden. We can see in this github issue that this is a problem for some people : <https://github.com/aspnet/Blazor/issues/172>. While I think this problem will be resolved by the ASPNET team before the first release (if there is one), in this blog post I'll show you how to workaround this issue with the existing tools already present in ASPNET Core.

## What happens 

Your blazor index page looks like this

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>TOSS</title>

    <base href="/" />
</head>
<body>
    <div class="container">
        <app>
            <div class="jumbotron p-2 m-2">
                <p class="lead">Loading all the things ....</p>
            </div>
        </app>
    </div>
    <script type="blazor-boot">
    </script>

    <script src="_framework/blazor.webassembly.js"></script>


</body>
</html>

```
- Everything in the <app> tag is for the splash screen
- When blazor.webassembly.js is loaded 
  - it downloads a file called "blazor.boot.json" containing all the file you app needs  
  - it downloads the mono wasms runtime
  - it downloads your project dll and dependencies dll from the previous json
  - and plug everything together (so far it's still magic to me but I might dig it for a future blog post)

Here is the blazor.boot.json for my project Toss : 

```json
{
   "main":"Toss.Client.dll",
   "entryPoint":"Toss.Client.Program::Main",
   "assemblyReferences":[
      "Markdig.dll",
      "MediatR.dll",
      "MediatR.Extensions.Microsoft.DependencyInjection.dll",
      "Microsoft.AspNetCore.Blazor.Browser.dll",
      "Microsoft.AspNetCore.Blazor.dll",
      "Microsoft.AspNetCore.Blazor.TagHelperWorkaround.dll",
      "Microsoft.Extensions.DependencyInjection.Abstractions.dll",
      "Microsoft.Extensions.DependencyInjection.dll",
      "Microsoft.JSInterop.dll",
      "Mono.WebAssembly.Interop.dll",
      "mscorlib.dll",
      "System.ComponentModel.Annotations.dll",
      "System.Core.dll",
      "System.dll",
      "System.Net.Http.dll",
      "Toss.Shared.dll",
      "Markdig.pdb",
      "Toss.Client.pdb",
      "Toss.Shared.pdb"
   ],
   "cssReferences":[

   ],
   "jsReferences":[

   ],
   "linkerEnabled":true
}
```

And here is a screenshot of the Network tab during my app startup

![Toss startup network tab](/assets/img/Capture.PNG "Toss startup network tab")

What we need to do happens on step 2 and 3 :
- On client side, when we request a ".dll" change the extension to ".blazor"
- On server side, when a ".blazor" file is requested change it to ".dll" so it matches the physical file name

## Changing the incoming request

Here is the code for changing the incoming request

```cs
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
  var options = new RewriteOptions()
      .AddRewrite("^_framework/_bin/(.*)\\.blazor", "_framework/_bin/$1.dll",
  skipRemainingRules: true);

  app.UseRewriter(options);
}
```

- I use the existing rewriting engine built-in ASPNET Core but you can use the one from your web server : nginx, apache, IIS ...
- The regex is pretty simple, you can test yours here <http://regexstorm.net/tester>
- your rewriter declaration must be one of the first in Configure
- For testing I just extracted the powershell for getting the first request from chrome dev tools and changed the extension :

```powershell
 Invoke-WebRequest -Uri "http://localhost:52386/_framework/_bin/Toss.Client.blazor" -Headers @{"Pragma"="no-cache"; "DNT"="1"; "Accept-Encoding"="gzip, deflate, br"; "Ac
cept-Language"="fr-FR,fr;q=0.9,en;q=0.8,en-US;q=0.7"; "User-Agent"="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.77 Safari/537.36"; "Accept
"="*/*"; "Referer"="http://localhost:52386/"; "Cookie"=".AspNetCore.Antiforgery.MCOkDYqzrsU=CfDJ8JBC_YjSTnhOnptilfRxtJO-G3t3ZxYDD7hdgqQ7f50T9bQrKR_6T-0OZo46WxGYxiznVoHxYXGL-sQWWtJ4hMy5tL1-nbji
iUH29wZPn5x80cwesCTDGMCRIDmn5f97H8eBN2u_lEXcesNX-ZIMyEI; CSRF-TOKEN=CfDJ8JBC_YjSTnhOnptilfRxtJNwmzsFKwbNtGqSAajSqxrwyM6vFT15lgY7EJ6KiQjjTs850EXHXiF-LUHdnFqiK6SBgHV2yQuFC05r2RMGlPcUjjH9x3xAJEkx
2QEp0Bn9eS9t9SxFTW5JCRI2GCv1u_Y"; "Cache-Control"="no-cache"}
```

I got a 200 with the good HTTP request headers :)

## Changing on client side

Now I need to change the file name on client side so XHR to "myassembly.dll" becomes "myasembly.blazor". I need to do this so it's the more discrete possible, because ".dll" must be hard coded through most of the mono wasm and blazor code base. I tried to change the extension in the blazor.boot.json via a middleware but my app couldn't start. So I chose to do this at the XHR level by overriding the method used by Blazor in blazor.webassembly.js ([source code](https://github.com/aspnet/Blazor/blob/master/src/Microsoft.AspNetCore.Blazor.Browser.JS/src/Platform/Mono/MonoPlatform.ts).

```js

XMLHttpRequest.prototype.open_before = XMLHttpRequest.prototype.open;

XMLHttpRequest.prototype.open = function (method, url, async) {
    if (url.endsWith(".dll")) {
        url = url.replace("dll", "blazor");
    }
    return this.open_before(method, url, async);
};
```

- It's pretty basic js code, thanks to it's flexibilty, we can override nearly everything.

And it works :) Here is my network tab now

![Toss startup network tab](/assets/img/Capture2.PNG "Toss startup network tab")

## Conclusion

This workaround is not a long term solution as I use non documented things and AV or Firewall might block because of the content of the downloaded files. But anyway it helps understanding how assembly loading works in Blazor and despite the lack of extension point on this we still can find solutions.

## Reference
- <https://docs.microsoft.com/en-us/aspnet/core/fundamentals/url-rewriting?view=aspnetcore-2.1#when-to-use-url-rewriting-middleware>
- <https://developer.mozilla.org/en-US/docs/Web/API/XMLHttpRequest/open>
