---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [blazor, architecture]
---

# Should I peek Blazor ?

On Twitter or Reddit I can often see question about whether Blazor is a good choice for your project. In this blog post I will try to give you my opinion on the subject.
First I will try to describe where Blazor fits in the technology landscape, its advantage and incovenient. 

## Quick presentation
### What is Blazor ?

You will get a good description of Blazor from the official website : [https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor). It belongs to the family of the SPA framework like angular, react or vue.

Blazor is a framework for building web UI using C#. It has 2 way of working : client-side and server-side.

### Client-Side 

This way of working looks a lot like Angular or React. Your .net assemblies are downloaded on the browser and executed by a .net runtime build aginst WebAssembly. This runtime is called "monowasm" it is developped by the mono team (I think this is because they have the exeperience in working with environment with low ressources).

Advantages :
- Do no rely network connection (Ionic App or Offline PWA)
- Might reduce load on the server

Disdvantages :
- Download size
- Relies on WebAssembly (https://caniuse.com/#feat=wasm 88% of usage)
- Still not released 
- Startup time can be long (mostly because of download size)
- SEO can be hard to do
- You can only use HTTP for talking to a server
- The runtime isn't fully implemented yet : some netstandard library won't work (like the SingalR c# client)
- The runtime is different, some class might behave differently on Blazor than on ASPNET Core like the HttpClient that doesn't publish Diagnostic information.

### Server-Side 
A SignalR connection between the browser and the server is opened, when something happens on one of the side (eg : a click on a button), the server sends the DOM changes that must be done to the client. I think this was created

Advantages :
- Works for everyone (SignalR works on nearly all the browsers as it tries to use the available connecting method on your browser)
- Was released in .net core 3.1
- Download size smaller
- Can use all kind of protocol for talking to a server
- You can use everything you already use in your netcore app

Disadvantages :
- UI state is stored on the server ([even the .net team prove that this is not an issue](https://docs.microsoft.com/en-US/aspnet/core/host-and-deploy/blazor/server?view=aspnetcore-3.1))
- Cannot work without network connection
- Connection kept open between client and server
- The developper makes abstraction about the network traffic between client and server, so you can have performance problem at some point (a bit like a SELECT N+1 problem with an ORM)
- Server setup can be tedious if you want to use WebSocekt.

## Restoring the truth

There is a lot os misconception about Blazor here are a few :

### It's like Silverlight or Flash

Silverlight or Flash were used for creating Web UI yes. But that's technically all there is in common between Blazor and Silverlight or Flash :
- Blazor uses Razor template language and so uses web standard for managing UI components : HTML, CSS, JS, WebAssembly, WebSocket ... 
- [Blazor is open source](https://github.com/dotnet/aspnetcore/tree/master/src/Components)
- It doesn't require any security settings on the client
- It doesn't require any 3rd party install on the client

## It's like WebForms

## Should I use it ?
