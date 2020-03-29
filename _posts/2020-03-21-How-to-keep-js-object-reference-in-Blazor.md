---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [ASPNET Core, Blazor, JSInterop]
---

# How to keep Javascript object reference in Blazor on .NET side ?

You can't do everything in Blazor with .NET alone. If you want to use a JS lib or get information from the browser, you need to use [JSInterop](https://docs.microsoft.com/en-us/aspnet/core/blazor/call-javascript-from-dotnet?view=aspnetcore-3.1). When you need to communicate between 2 system or subsystem, you often need to "keep" a reference of something from the other side. For example when you call a "create payment" API, there is a lot of chance that it'll send you a "paymentId" so you can call other API with this ID and do action like "refund" or "cancel". For in-memory application it's a bit cumbersome to do it with ID by hand, because :
- you have to maintain an in-memory map of the object you share with the other side
- those object will never be release from memory by GC
- you have to do it for every kind of interaction

## How does the framework shares .NET instance with js ?

It's better to handle this kind of thing at the framework/infrastructure level so the developer experience is better. In Blazor there is already one mechanism like that : you can send to JS the reference of a .NET object, so you can call any method anotated with [JSInvokable] on this object.[ From the official documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/call-dotnet-from-javascript?view=aspnetcore-3.1), you do it like this :

Razor file :
```cs
@inject IJSRuntime _jsRuntime
@implements IDisposable

<button type="button" class="btn btn-primary" @onclick="TriggerNetInstanceMethod">
    Trigger .NET instance method HelloHelper.SayHello
</button>

@code {
    private DotNetObjectReference<HelloHelper> _objRef;

    public async Task TriggerNetInstanceMethod()
    {
        _objRef = DotNetObjectReference.Create(new HelloHelper("Rémi"));
        var test = await  _jsRuntime.InvokeAsync<string>("exampleJsFunctions.sayHello", _objRef);
    }
    public void Dispose()
    {
        _objRef?.Dispose();
    }
    public class HelloHelper
    {
        public HelloHelper(string name)
        {
            Name = name;
        }

        public string Name { get; set; }

        [JSInvokable]
        public string SayHello() => $"Hello, {Name}!";
    }
}
```

JS file
```js
window.exampleJsFunctions = {
  sayHello: function (dotnetHelper) {
    return dotnetHelper.invokeMethodAsync('SayHello')
      .then(r => console.log(r));
  }
};
```

With this code, the "test" variable will be "Hello, Rémi". How does it work ?
- [DotNetObjectReference](https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/JSInterop/Microsoft.JSInterop/src/DotNetObjectReference.cs).Create instantiate a DotNetObjectReference&lt;HelloHelper&gt; with the given HelloHelper value.
- The [JSRuntime](https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/JSInterop/Microsoft.JSInterop/src/JSRuntime.cs) (method "TrackObjectReference") detects that one parameter is a DotNetObjectReference and store it on an internal dictionary if it's not already there, then increments an ID and send this id to javascript wrapped in a json object like this

```js
{
    "__dotNetObject":"1"
}
```

Where "1" is the object id on the internal dictionary.
- We saw on the [last blog post](https://remibou.github.io/) how JSInterop uses json reviver for changing value received from .NET runtime. Here it uses the same mechanism for changing the json value to an instance of a DotnetObject like that

```js
const dotNetObjectRefKey = '__dotNetObject';
  attachReviver(function reviveDotNetObject(key: any, value: any) {
    if (value && typeof value === 'object' && value.hasOwnProperty(dotNetObjectRefKey)) {
      return new DotNetObject(value.__dotNetObject);
    }

    // Unrecognized - let another reviver handle it
    return value;
  });
```

- Then when the JS method receives an instance of a DotnetObject and calls "[invokeMethodAsync](https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/JSInterop/Microsoft.JSInterop.JS/src/src/Microsoft.JSInterop.ts#L58)" it calls a method called "invokeDotNetFromJS" on a JS interface called "DotNetCallDispatche" which is defined by the Blazor js library [here](https://github.com/dotnet/aspnetcore/blob/2d0c49d0fca0aaf37672e0aec1c011cfe6a2d6f2/src/Components/Web.JS/src/Platform/Mono/MonoPlatform.ts#L257).
- This method accept 2 things as 2nd argument : an assembly name OR an object ID (value of field __dotNetObject) and the other arguments are the method name, call id (for managing async calls) and method parameters.
- Then with the help of "[bind_static_method](https://github.com/mono/mono/blob/150ffb7af3ff53db44128fbe4513eda36d77529f/sdks/wasm/src/binding_support.js)" from the mono WASM runtime which browse the loaded assemblies, it finds a reference to the C# static method "MonoWebAssemblyJSRuntime.BeginInvokeDotNet" and calls it (the method call itself [is really complicated](https://github.com/mono/mono/blob/150ffb7af3ff53db44128fbe4513eda36d77529f/sdks/wasm/src/binding_support.js#L613), it consist of playing with memory from the .NET runtime inside WebAssembly) with the assembly name or the object id, the method name and the method parameters.
- This method then checks if the parameter is a digit, if it's a digit then it's a ref to a js object (if it's not then it's an assembly name). Why do they do a hack like that you will ask yourself ? Well there is a limit in interop between monowasm and blazor to 4 parameter, so they chose to send 2 distinct information into 1 slot. This limit does not apply to your code as every js interop call go through this wrapper.
- Then with reflection the C# class called [DotNetDispatcher](https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/JSInterop/Microsoft.JSInterop/src/Infrastructure/DotNetDispatcher.cs) will call the good method with the parameters.

The only problem from a developer experience is that you have to think of your object disposal a bit just like if the GC doesn't exist because a static reference to your instance is kept around. For disposing a DotNetObjectReference you need to call Dispose on it.

I don't know why I went this far on the JSInterop explanation for this blog post, but I hope it'll help someone understand a bit more how it works. Where are we now ? From this work I can identify how I have to do send js object reference to .NET :
- Store a method result to a map
- Create a C# class for keeping the ID around
- When this serialized C# class is send to js interop change it to the corresponding JS object
- Provide a way for clearing the reference on the JS side

## Store a method result to a map

The first thing to do is to build the same thing but on js side. I first thought about using a [WeakMap](https://developer.mozilla.org/fr/docs/Web/JavaScript/Reference/Objets_globaux/WeakMap) but I don't really understand how it can be useful as the key is the object on which we want to keep a weak reference. So, I use a simple javascript object. Here is my method for storing the object :

```js
var jsObjectRefs = {};
var jsObjectRefId = 0;
const jsRefKey = '__jsObjectRefId';
function storeObjectRef(obj) {
    var id = jsObjectRefId++;
    jsObjectRefs[id] = obj;
    var jsRef = {};
    jsRef[jsRefKey] = id;
    return jsRef;
}   
```

Here is my sample js method calling it

```js
 function openWindow() {
      return storeObjectRef(window.open("/", "_blank"));
  }
```

And the JSInterop call

```cs
 public class JsRuntimeObjectRef
  {
      [JsonPropertyName("__jsObjectRefId")]
      public int JsObjectRefId { get; set; }
  }
  private JsRuntimeObjectRef _windowRef;
  private async Task OpenWindow()
  {
      _windowRef = await jsRuntime.InvokeAsync<JsRuntimeObjectRef>("openWindow");
  }
```

I simplified the class : in Blazor the property is internal and they use a custom JsonConverter for serializing it while hidding it to users.

## Using the reference in a method call

Now I need to close this opened window, here is the JS method

```js
function closeWindow(window) {
    window.close();
}
```

And here is the C# interop call

```cs
private async Task CloseWindow()
{
    await jsRuntime.InvokeVoidAsync("closeWindow", _windowRef);
}
```

You might be wondering : how does a JsRuntimeObjectRef becomes a window object on JS side ? With a reviver ! Here is its definition :

```js
DotNet.attachReviver(function (key, value) {
    if (value &&
        typeof value === 'object' &&
        value.hasOwnProperty(jsRefKey) &&
        typeof value[jsRefKey] === 'number') {

        var id = value[jsRefKey];
        if (!(id in jsObjectRefs)) {
            throw new Error("This JS object reference does not exists : " + id);
        }
        const instance = jsObjectRefs[id];
        return instance;
    } else {
        return value;
    }
});
```

This reviver will be called for every serialized object send to JS via JSInterop (even deep in the object graph, so you can send arrays or complex objects with JsRuntimeObjectRef properties).

You can find all the working code [here](https://github.com/RemiBou/remibou.github.io/tree/master/projects/RemiBou.BlogPosts.JsReference).

## Cleaning the kept reference from JS runtime memory.

If we leave it like this, jsObjectRefs will keep a reference to js object forever which is bad and can impact your user experience (UX yeah). For removing the object reference in jsObjectRefs we'll do a bit like with DotNetObjectReference and implement IAsyncDisposable in JsRuntimeObjectRef like this 

```cs 

public class JsRuntimeObjectRef : IAsyncDisposable
  {
      internal IJSRuntime JSRuntime { get; set; }

      public JsRuntimeObjectRef()
      {
      }

      [JsonPropertyName("__jsObjectRefId")]
      public int JsObjectRefId { get; set; }

      public async ValueTask DisposeAsync()
      {
          await JSRuntime.InvokeVoidAsync("browserInterop.removeObjectRef", JsObjectRefId);
      }
  }
  private JsRuntimeObjectRef _windowRef;
  private async Task OpenWindow()
  {
      _windowRef = await jsRuntime.InvokeAsync<JsRuntimeObjectRef>("openWindow");
      _windowRef.JSRuntime = jsRuntime;
  }

  private async Task CloseWindow()
  {
      await jsRuntime.InvokeVoidAsync("closeWindow", _windowRef);
      await _windowRef.DisposeAsync();
  }
```

Because you need to set JSRuntime after every new  JsRuntimeObjectRef creation, it might be a better idea to wrap this into an extension method.

Here is the JS method

```js
function cleanObjectRef(id) {
    delete jsObjectRefs[jsObjectRefId];
}
```

Now, when we close the opened window, the reference to said window will be removed and the browser will be able to GC it (if it feels like it).


## BrowserInterop

On my last blog post I talked about my library [BrowserInterop](https://www.nuget.org/packages/BrowserInterop) which is a library for making the developer life easier when he/she needs to use JSInterop. This library uses a lot of the things I talked about in this blog post because I need to keep reference of window object. For making my life easier I created a bunch of utility methods that you can use :


```cs
IJSRuntime jsRuntime;
// this will get a reference to the js window object that you can use later, it works like ElementReference ofr DotNetRef : you can add it to any method parameter and it 
// will be changed in the corresponding js object 
var windowObjectRef = await jsRuntime.GetInstancePropertyAsync<JsRuntimeObjectRef>("window");
// get the value of window.performance.timeOrigin
var time = await jsRuntime.GetInstancePropertyAsync<decimal>(windowObjectRef, "performance.timeOrigin");
// set the value of the property window.history.scrollRestoration
await jsRuntime.SetInstancePropertyAsync(windowObjectRef, "history.scrollRestoration", "auto");
//get a reference to window.parent
var parentRef = await jsRuntime.GetInstancePropertyRefAsync(windowObjectRef, "parent");
// call the method window.console.clear with window.console as scope
await jsRuntime.InvokeInstanceMethodAsync(windowObjectRef, "console.clear");
// call the method window.history.key(1) with window.history as scope
await jsRuntime.InvokeInstanceMethodAsync<string>(windowObjectRef, "history.key",1 );
//will listen for the event until DisposeAsync is called on the result
var listener = await jSRuntime.AddEventListener(windowObjectRef, "navigator.connection", "change", () => Console.WriteLine("navigator connection change"));
//stop listening to the event, you can also use "await using()" notation
await listener.DisposeAsync();
//will return true if window.navigator.registerProtocolHandler property exists
await jsRuntime.HasProperty(windowObjectRef, "navigator.registerProtocolHandler")
```

There is many methods, I will create blog pst about it soon, but you still can use it and send me feedback/bug reports.

## Conclusion

I rant a lot in my head about the lack of hooks in ASPNET Core (HttpClient ...) but the reviver one, while undocumented, is really great here.