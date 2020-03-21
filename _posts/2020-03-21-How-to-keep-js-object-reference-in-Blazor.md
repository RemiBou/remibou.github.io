---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [ASPNET Core, Blazor, JSInterop]
---

# How to keep Javascript object reference in Blazor on .net side ?

You can do do everything in Blazor with .net alone. If you want to use a JS lib or get information from the browser, you need to use [JSInterop](https://docs.microsoft.com/en-us/aspnet/core/blazor/call-javascript-from-dotnet?view=aspnetcore-3.1). When you need to communicate between 2 system or subsystem, you often need to "keep" a reference of something from the other side. For example when you call a "create payment" API, there is a lot of chance that it'll send you a "paymentId" so you can call other API with this ID and do action like "refund" or "cancel". For in-memory application it's a bit cumbersome to do it with ID by hand, because :
- you have to maintain an in-memory map of the object you share with the other side
- those object will never be release from memory by GC
- you have to do it for every kind of interaction

## How does the framework shares .net instance with js ?

It's better to handle this kind of thing at the framework/infrastructure level so the developer experience is better. In Blazor there is already one mechanism like that : you can send to JS the reference of a .net object, so you can call any method anotated with [JSInvokable] on this object.[ From the official documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/call-dotnet-from-javascript?view=aspnetcore-3.1), you do it like this :

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

With this code, the "test" variable will be "Hello, Rémi", how do it works ?
- [DotNetObjectReference](https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/JSInterop/Microsoft.JSInterop/src/DotNetObjectReference.cs).Create instantiate a DotNetObjectReference&lt;HelloHelper&gt; with the given HelloHelper value.
- The [JSRuntime](https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/JSInterop/Microsoft.JSInterop/src/JSRuntime.cs) (method "TrackObjectReference") detects that one parameter is a DotNetObjectReference and store it on an internal dictionary if it's not already there, then increments an ID and send this id to javascript wrapped in a json object like this

```js
{
    "__dotNetObject":"1"
}
```

Where "1" is the object id on the internal dictionary.
- We saw on the [last blog post](https://remibou.github.io/) how JSInterop uses json reviver for changing value received from .net runtime. Here it uses the same mechanism for changing the json value to an instance of a DotnetObject like that

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
- Then with the help of "[bind_static_method](https://github.com/mono/mono/blob/150ffb7af3ff53db44128fbe4513eda36d77529f/sdks/wasm/src/binding_support.js)" from the mono WASM runtime which browse the loaded assemblies , find a reference to the C# static method "MonoWebAssemblyJSRuntime.BeginInvokeDotNet" and calls it (the method call itself [is really complicated](https://github.com/mono/mono/blob/150ffb7af3ff53db44128fbe4513eda36d77529f/sdks/ 
wasm/src/binding_support.js#L613), it consist of playing with memory from the .netruntime inside WebAssembly) with the assembly name or the object id, the method name and the method parameters.
- This method then check if the parameter is a digit, if it's a digit then it's a ref to a js object (if it's not then it's an assembly name). WHy do they do a hack like that you will ask yourself ? Well there is a limit in interop between monowasm and blazor to 4 parameter, so they chose to send 2 distinct information into 1 slot. This limit does not apply to your code as every js interop call go through this wrapper.
- Then with reflection the C# class called [DotNetDispatcher](https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/JSInterop/Microsoft.JSInterop/src/Infrastructure/DotNetDispatcher.cs) will call the good method with the parameter.

The only problem from a developer experience is that you have to think of your object disposal a bit just like if the GC does exists because a statis reference to your instance is kept around. For disposing a DotNetObjectReference you need to call Dispose on it.

I don't know why I went this far on the JSInterop explanation for this blog post, but I hope it'll help someone somewhere understand a bit more how it works. Where are we now ? From this work I can identify how I have to do send js object reference to .net :
- Store a method result to a map
- Create a C# class for keeping the ID around
- When this serialized C# class is send to js interop change it to the corresponding JS object

## Store