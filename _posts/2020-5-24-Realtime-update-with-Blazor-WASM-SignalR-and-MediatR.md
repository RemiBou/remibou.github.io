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
    public class HubNotificationHandler : Hub
    {
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

Now I have a server-side MediatR notification that must trigger a client UI update

```cs

[ApiController]
[Route("[controller]")]
public class HomeController : ControllerBase
{
    private static int Counter = 0;
    private MediatR.IMediator _mediator;

    public HomeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("increment")]
    public async Task Post()
    {
        int val = Interlocked.Increment(ref Counter);
        await _mediator.Publish(new CounterIncremented(val));

    }
}
```

You can trigger it with a curl call

```bash
curl -X POST -d "" http://localhost:5000/home/increment
```

## Sending notification from server to the client

What I want to do is this : when some notification are send in MediatR, send them on the Hub. Then on the client receive those notification and pass them to the living component that subscribed to it.

First I need to send notification to the hub, so I change my hub like this

```cs
public class HubNotificationHandler : INotificationHandler<CounterIncremented>
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public HubNotificationHandler(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Handle(CounterIncremented notification, CancellationToken cancellationToken)
    {
        await SendNotification(notification);
    }

    private async Task SendNotification(SerializedNotification notification)
    {
        await _hubContext.Clients.All.SendAsync("Notification", notification);
    }
}
```
- I will need to implement INotificationHandler for every kind of notification that I want to send to the client. We cannot do a wildcard handler in MediatR but here it's a good thing : I want to tell explicitely which kind of event are sent.

I need a custom Json converter for handling polymorphic serialization and deserialization of my event. Here is its definition

```cs
public abstract class SerializedNotification : INotification
{
    public string NotificationType
    {
        get
        {
            return this.GetType().Name;
        }
        set{}
    }
}
public class NotificationJsonConverter : JsonConverter<SerializedNotification>
{
    private readonly IEnumerable<Type> _types;

    public NotificationJsonConverter()
    {
        var type = typeof(SerializedNotification);
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => type.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract)
            .ToList();
    }

    public override SerializedNotification Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        using (var jsonDocument = JsonDocument.ParseValue(ref reader))
        {   
            if (!jsonDocument.RootElement.TryGetProperty("notificationType", out var typeProperty))
            {
                throw new JsonException();
            }
            var type = _types.FirstOrDefault(x => x.Name == typeProperty.GetString());
            if (type == null)
            {
                throw new JsonException();
            }

            var jsonObject = jsonDocument.RootElement.GetRawText(); 
            var result = (SerializedNotification)JsonSerializer.Deserialize(jsonObject, type, options);
            return result;
        }
    }

    public override void Write(Utf8JsonWriter writer, SerializedNotification value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, options);
    }
}
```
- Again perf are not the best, the GetRawTextis not a good idea ad it can allocate a lot of memory. If you work at StackOverflow you'll find someone to help you about that.
- This converter is actually simple : it adds a field to the JSON with the child type and reads it when deserializaing. There isn't (AFAIK) any security issues because the child type list is finite and known by the developper.

Here is how I wire my custom converter to SignalR protocol (in Startup.cs)

```cs
services.AddSignalR()
        .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new NotificationJsonConverter()));
```

On the client side you need to listen to event. First because Microsoft DI doesn't support runtime configuration I can't use MediatR for sending the notification to the components.So I need to write some kind of service locator for finding the component that implements INotificationHandler do this I created this class

```cs

public static class DynamicNotificationHandlers
{
    private static Dictionary<Type, List<(object,Func<SerializedNotification, Task>)>> _handlers = new Dictionary<Type, List<(object, Func<SerializedNotification, Task>)>>();
    public static void Register<T>(INotificationHandler<T> handler) where T : SerializedNotification
    {
        lock (_handlers)
        {
            var handlerInterfaces = handler
                .GetType()
                .GetInterfaces()
                .Where(x =>
                    x.IsGenericType &&
                    x.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                .ToList();
            foreach (var item in handlerInterfaces)
            {
                var notificationType = item.GenericTypeArguments.First();
                if(!_handlers.TryGetValue(notificationType,out var handlers)){
                    handlers = new List<(object, Func<SerializedNotification, Task>)>();
                    _handlers.Add(notificationType,handlers);
                }
                handlers.Add((handler, async s => await handler.Handle((T)s, default(CancellationToken))));
            }
        }
    }
    public static void Unregister<T>(INotificationHandler<T> handler) where T : SerializedNotification
    {
        lock (_handlers)
        {
            foreach (var item in _handlers)
            {
                item.Value.RemoveAll(h => h.Item1.Equals(handler));
            }
        }
    }
    public static async Task Publish(SerializedNotification notification)
    {
        try
        {
            var notificationType = notification.GetType();
            if(_handlers.TryGetValue(notificationType, out var filtered)){
                foreach (var item in filtered)
                { 
                    await item.Item2(notification);
                }
            }

        }
        catch (System.Exception e)
        {
            Console.Error.WriteLine(e + " " + e.StackTrace);

            throw;
        }
    }
}
```

- What it does is that it keeps in memory a list of all the implementation for a specific notification type (it took me a little while to figure out the Action thing)
- The odd try catch is here mainly because SignalR is really shy and doesn't say anything if there is an error

Now to register to this my component needs to do this (in Index.razor for example) :

```razor
@page "/"
@using RemiBou.BlogPost.SignalR.Shared
@using MediatR
@implements INotificationHandler<CounterIncremented>
@implements IDisposable

<h1>Hello, world!</h1>

Welcome to your new app.

Current count : @count
@code {
    private int count;
    protected override void OnInitialized()
    {
        DynamicNotificationHandlers.Register(this);
    }
    public async Task Handle(CounterIncremented notification, System.Threading.CancellationToken cancellationToken)
    {
        count = notification.Counter;
        StateHasChanged();
    }

    public void Dispose()
    {
        DynamicNotificationHandlers.Unregister(this);

    }
}
```
- It listens to the notification for updating the UI
- When the component is destroyed it stops listenning : DO NOT FORGET IT or your component will live for ever ever ever
- This could be implemented as a BaseComponent 

Now the final wiring of SignalR to all this, in your client's Program.cs :
```cs
var app = builder.Build();
var navigationManager = app.Services.GetRequiredService<NavigationManager>();
var hubConnection = new HubConnectionBuilder()
            .WithUrl(navigationManager.ToAbsoluteUri("/notifications"))
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new NotificationJsonConverter()))
            .Build();

hubConnection.On<SerializedNotification>("Notification", async (notificationJson) =>
{
    await DynamicNotificationHandlers.Publish(notificationJson);
});

await hubConnection.StartAsync();
await app.RunAsync();
```

- From the template you need to split the Build and RunAsync code so you get the service collection for your app.

Et voila ! Now when you launch the bash script the UI is updated automatically. Of course if you want to display the initial state you need to create an API that provides it.

## Conclusion
Once again we saw the biggest selling point of Blazor : I can use the same toolbelt for the frontend and the backend (SignalR, MediatR, Json converters...) and it feels damn good :) You can now build a realtime app without a single line of javascript.

All the source code for this blog post is available here : [(https://github.com/RemiBou/remibou.github.io/tree/master/projects/RemiBou.BlogPosts.SignalR]((https://github.com/RemiBou/remibou.github.io/tree/master/projects/RemiBou.BlogPosts.SignalR)



