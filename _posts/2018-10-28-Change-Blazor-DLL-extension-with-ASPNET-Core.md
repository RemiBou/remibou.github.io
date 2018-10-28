# How to change Blazor dll file extension with ASPNET Core

Blazor is a framework for executing livrary (.dll) targeting .net standard in the browser. When the app loads it get via xhr you application binaries. The problem is, in some internal network or for some AV providers, downloading .dll files is forbidden. We can see in this github issue that this is a problem for some people : https://github.com/aspnet/Blazor/issues/172. While I think this problem will be resolved by the ASPNET team before the first release (if there is one), in this blog post I'll show you how to workaround this issue with the existing tools already present in ASPNET Core.

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
  - it downlaod a file called "blazor.boot.json" containing all the file you app needs  
  - it downloads the mono wasms runtime
  - it downloads your project's and dependencies dll from the previous json
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

![Toss startup network tab](/images/Capture.png "Toss startup network tab")

What we need to do happens on step 2 and 3 :
- Change the file name in the json file ("Toss.Client.dll" becomes "Toss.Client.blazor")
- When a ".blazor" file is requested change it to ".dll"

This is actually quite easy to do in ASPNET Core with the Url Rewriting engine which provides two ways of rewriting :
- Changing an incoming request
- CHanging an outgoing response 

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

- I use the existing rewriting engine
- Theregex is pretty simple, you can test yours here http://regexstorm.net/tester
- For testing I just extracted the powershell for getting the first request from chrome dev tools and changed the extension :

```powershell
 Invoke-WebRequest -Uri "http://localhost:52386/_framework/_bin/Toss.Client.blazor" -Headers @{"Pragma"="no-cache"; "DNT"="1"; "Accept-Encoding"="gzip, deflate, br"; "Ac
cept-Language"="fr-FR,fr;q=0.9,en;q=0.8,en-US;q=0.7"; "User-Agent"="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.77 Safari/537.36"; "Accept
"="*/*"; "Referer"="http://localhost:52386/"; "Cookie"=".AspNetCore.Antiforgery.MCOkDYqzrsU=CfDJ8JBC_YjSTnhOnptilfRxtJO-G3t3ZxYDD7hdgqQ7f50T9bQrKR_6T-0OZo46WxGYxiznVoHxYXGL-sQWWtJ4hMy5tL1-nbji
iUH29wZPn5x80cwesCTDGMCRIDmn5f97H8eBN2u_lEXcesNX-ZIMyEI; CSRF-TOKEN=CfDJ8JBC_YjSTnhOnptilfRxtJNwmzsFKwbNtGqSAajSqxrwyM6vFT15lgY7EJ6KiQjjTs850EXHXiF-LUHdnFqiK6SBgHV2yQuFC05r2RMGlPcUjjH9x3xAJEkx
2QEp0Bn9eS9t9SxFTW5JCRI2GCv1u_Y"; "Cache-Control"="no-cache"}
```

I got a 200 with the good HTTP request headers :)

## Changing the outgoing response

Now I need to change the blazor.boot.json file so it gives ".blazor" url instead of ".dll". I could do it at build time but I don't see any extension mechanism [here](https://github.com/aspnet/Blazor/blob/master/src/Microsoft.AspNetCore.Blazor.Build/Core/RuntimeDependenciesResolver.cs). So I'll use the rewriter for this task. I might consume ressources at runtime but it's not our problem now.

Here is my code for changing this file

```cs

```
