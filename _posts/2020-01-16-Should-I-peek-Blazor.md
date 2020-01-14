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

Blazor is a framework for building web UI using C#. It has 2 way of working :
- Client-Side : This way of working looks a lot like Angular or React. Your .net assemblies are downloaded on the browser and executed by a .net runtime build aginst WebAssembly. This runtime is called "monowasm" it is developped by the mono team (I think this is because they have the exeperience in working with environment with low ressources).

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

- Server-Side : A SignalR connection between the browser and the server is opened, when something happens on one of the side (eg : a click on a button), the server sends the DOM changes that must be done to the client. I think this was created

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

## Misconception

There is a lot os misconception about Blazor here are a few :

### It's like Silverlight or Flash

Silverlight or Flash were used for creating Web UI. But that's technically all there is in common between Blazor and Silverlight or Flash :

- Blazor uses Razor template language and so it uses web standard for managing UI components : HTML, CSS, JS, WebAssembly, WebSocket ... 
- [Blazor is open source](https://github.com/dotnet/aspnetcore/tree/master/src/Components)
- It doesn't require any security settings on the client
- It doesn't require any 3rd party install on the client
- It cannot do anything in the user computer than JS or WebAssembly cannot do.

### It's like WebForms

WebForms was used for building Web UI and was based on .net but there is a lot of difference with Blazor :

- Blazor does not create an abstraction on the produced HTML or JS or CSS
- [Blazor is open source](https://github.com/dotnet/aspnetcore/tree/master/src/Components)
- Blazor client-side is executed on the client
- There is no state exchange between client and server with Blazor server-side (like ViewState)


## Should I use it ?

Mandatory : "It depends" :)

This is very broad question to which I will answer like a politician : with more questions :). 

### Should you use this kind of tool ?

You never start a project by saying "I need a database". You analyze your spec and read that you need to store some data, and you decide to use a relationnal or document database. 
Here it's the same thing. I make the distinction between a web application and a web site :
- A web application requires a lot of interaction with the user : video game (like [https://www.ageofascent.com/](https://www.ageofascent.com/)), SaaS (like Office365), an intranet or a tool (like pgadmin), there is little to no need for SEO, first load time can be a bit long and most of the users are active.
- A web site is a collection of web page where the user navigates between them with link. Most fo the workload is read only, there is a LOT of traffic, most of the users come and go, SEO needs is crucial, page must be displayed in less than 100ms ...

Most of the time a project is a combination of both : you'll build a cms (web app) that will generate a blog (web site). Or there is a shopping cart (web app) part in a online retailer site (web site). 
I would still suggest that either you split the 2 aspect in 2 distinct projects or you choose one and choose your toolchain with it.

2 examples :

- StackOverflow is a website even though there is some forms for submiting content, and searching things. So they adopted technologies for building web site : server side rendering with some jQuery.
- Facebook is a web app even though there is a lot of readonly load and SEO needs are important. But this level of interaction would be very hard to obtain with jQuery.

From the question above I can draw this conclusion : **IF YOU ARE BUILDING A WEB SITE, CLOSE THIS WINDOW, YOU DON'T NEED BLAZOR**, you don't need angular or react, just use server side rendering like ASPNET Core MVC and you will be ok. If not then it might be a good idea, I will give you some lead that will help you make a decision.

### Features

So far Blazor provides all the features you need for building a complete application : templating, forms, network, authentication, authorization, libraries etc ... like with any framework, stop wondering "how do I do this thing that I used to do with this framework ?" but read the documentation and follow the framework principles or you will miss its added value : if you stop reading about Blazor because it doesn't provide decorator like in angular you will miss nice feature like CascadingParameter that are missing in angular.

Even though Blazor exists since only a dozen months, you can use all the netstandard library available in nuget.org for your frontend project and that is a huge selling point. When a new technology comes around, you have to wait a bit until there is nice community project emerging or until the included libs are stable. This is not the case here : most of the Base Class LIbrary is copied from the mono runtime and the open source community produces a lot of great library.

### Feature sacrifice

When you choose a framework there is some feature you would like to see that you have to forget or you will have to find a workaround, here is a shortlist of the first I can think of :
- DOM manipulations : while it's a bad practice to do it with a framwework like this, some people will still do it. Here you can do it with JS interop but there is no guarantee that your change won't be erased because blazor keeps a state of the app in memory.
- Decorator : in Blazor you can't change an existing tag behavior deal wih it.
- SEO : more about that later

### Licensing
Blazor is part of the [ASPNET Core repository](https://github.com/dotnet/aspnetcore) and is realsed under the Apache 2.0 license. In my understanding, from a user point of view there is no constraint.

### Performance
For a frontend framework we can use [Lighthouse](https://developers.google.com/web/tools/lighthouse/) for getting informations about load time. I decided to create a small benchmark where I compare 2 Hello World app, one created with Blazor, the other with Angular 8. You can find the code [here]()


### Cost
- learning
- hosting

### Stability
- issues in gh
- server side scaling 

### Support
- open source
- client-side not released

### Compatibility
- Browser 
- Other server

### Architecture
- 

### SEO