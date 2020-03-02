---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [ASPNET Core, Blazor, JSInterop]
---

# How to send callback method to JSInterop in Blazor ?

Blazor client-side or server side can handle only CPU bound calculation. For every interaction with the browser you need to use JSInterop. Even on Blazor itself the team uses JSInterop for using browser API like XHR request or DOM update.

Currently in the JS Interop API, you can do only one thing : call a given method connected to the "window" object with 3 kind of parameter :
- C# object serialized via System.Text.Json and deserialized via JSON.parse
- Html element (ElementReference in C# side)
- Reference to C# object on which you can then call any method annotated with "JSInvokable"

But what about passing a callback method, this would be useful if you want to register to an event like window.navigator.connection.onchange. With Blazor as-is, you can do it but you would have to do some plumbing and you would have to do it for every different callback you want to use. In this article I will show you how to do it in a more reusable way.

## Json Reviver

As I said previously Blazor JSInterop uses JSON.parse for creating an object from a JSON string send by the .net runtime. Even ElementReference or DotNetObjectReference are serialized in JSON and sended to this method, [here is the code that does it](https://github.com/dotnet/aspnetcore/blob/master/src/JSInterop/Microsoft.JSInterop.JS/src/src/Microsoft.JSInterop.ts) :

````