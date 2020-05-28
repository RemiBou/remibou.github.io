---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [ASPNET Core, Blazor, JSInterop]
---

# How to have realtime update in your app with Blazor WASM, SignalR and MediatR

On eof the big problem with web app is real time update : most of the traffic is from the client to the server, so the server cannot say anything to the client like "there is a new message for you". Fortunately, there is many technical solution for this : server-send events, long polling, web socket ... In the .net world we are very lucky because there is one project that help abstracting over which solution to choose (some of them are available only in some web browser) : SignalR.

I will explain in this blog post how to implement live reloading of a Blazor WASM app with SignalR and MediatR (my other prefered OSS project).

## Setup

We'll start with a fresh project (you can checkout the project [here](https://github.com/RemiBou/remibou.github.io/tree/master/projects/RemiBou.BlogPosts.SignalR))

```sh
dotnet new blazorwasm -ho -o .
```

This creates a Blazor WASM project hosted by an ASPNET Core backend.

Now add SignalR/MediatR dependency to your client (Blazor WASM) project

```sh
dotnet add Client package Microsoft.AspNetCore.SignalR.Client
dotnet add MediatR
```

And to the server
```sh
dotnet add package MediatR
```

## SignalR setup server-side

SignalR works with "hub" : the server creates a hub, the client subscribes to it and then the server pushes messages to the hub. The first step is to create a hub on the server :

```cs
    public class NotificationHub : Hub
    {
        public async Task SendNotification<T>(T notification) where T : INotification
        {
            await Clients.All.SendAsync("Notification", notification);
        }
    }
```

Now you need to wire SignalR with ASPNET Core middleware, in Startup.cs add the service
```cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddControllersWithViews();
    services.AddRazorPages();
    services.AddSignalR();
    services.AddResponseCompression(opts =>
    {
        opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            new[] { "application/octet-stream" });
    });
}
```

And add the  endpoint that wire incoming subscription to the hub we created
```cs
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapHub<NotificationHub>("/notifications");
    });
}
```

## SignalR setup client-side
