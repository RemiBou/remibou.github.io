---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [blazor]
---

# Implementing the observer pattern with Blazor

The observer pattern is a very interesting pattern for decoupling component. It is very used for decoupling UI and Business code : update your UI when something is changed on the business side.

ASPNET (Core or Framework) developers don't use this pattern on the server side that much : each request starts a new state, instance are shortlived it would be too cumbersome to manage event subscribing / emiting. On the server side we use more the pubsub pattern or event aggregator which are just derived from the observer but use different technique.

In Blazor this patterns makes a lot of sense for syncing components. On this blog post I'll show how to implement it.

## The service
A client-side app is stateful, meaning, after each actions, the state is kept around for allowing other actions. In this case it makes sense to use mainly singleton for services so they keep the state of the app internaly. Here is my service publishing an event

```cs
public class MyService : IMyService{
  public event EventHandler<string> OnDataUpdated;
  private string _data;
  public void UpdateDate(string newData){
    this._data = newData;
    this.OnDataUpdated?.Invoke(this,newData);
  }
}
```
- I use the event keyword in C# whichis specificaly designed for imlementing this pattern
- The generic argument of the EventHandler is a string here but you can create your specific payload class if you need to embed more data
- I use "?" before invoke because if no one has subscribed to this event, OnDataUpdated will be null
- the first parameter of invoke is the sender of the event , so most of the time we'll send this. It's a bit of an anti pattern to tell who created the event (the subscriber and the publisher should ignore about each other), but the people at MSFT must had good reason to do so.
- the event could be published by an other service as well

## The component
Here is a simple component subscribing to this event showing when the data what last updated

```cs
@implements IDisposable
@inject IMyService myService;

@lastUpdate

@functions{
    DateTime lastUpdate;
    protected override void OnInit()
    {
        myService.OnDataUpdated += Handle;
        base.OnInit();
    }
    protected void Handle(object sender, string args)
    {
       lastUpdate = DateTime.Now;        
    }
    public void Dispose()
    {
        myService.OnDataUpdated -= Handle;
    }
}

```

- On init the "+=" notation subscribes to the event
- VERY IMPORTANT POINT : I had to make component implement IDisposable so I could unsubscribefrom the event when it's removed. If I didn't do it, then the object representing my component would be kept in memory for ever (because my service is a singleton) making your app take more and more memory if you create / remove your component multiple times. Here is a great post from the honorable Jon SKeet about it <https://stackoverflow.com/a/4526840/277067>.

## Conclusion

This pattern is very usefull for syncing your component across your app while keeping them decoupled. And again it shows that Blazor is a great project because you can use all the nice things that are already here in C# and .Net. The development might have started month ago but the amount of stuff you can reuse is enormous.

## Source
-<https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/events/how-to-subscribe-to-and-unsubscribe-from-events>
