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
- For Blazor server-side : you need a stable network connection

### Licensing
Blazor is part of the [ASPNET Core repository](https://github.com/dotnet/aspnetcore) and is realsed under the Apache 2.0 license. In my understanding, from a user point of view there is no constraint.

### Performance
For a frontend framework we can use [Lighthouse](https://developers.google.com/web/tools/lighthouse/) for getting informations about load time. I decided to create a small example where I compare 2 app created from the cli, one created with Blazor, the other with Angular 8. You can find the code [here](https://github.com/RemiBou/Blog-angular-blazor-performance). 
Because the tests where done locally the Time To First Byte is not relevant. The build and run were done with production settings (Release configuration and --prod flag)

#### Build time

| Metric        | Blazor Client Side    | Blazor Server Side    | Angular  |
| ------------- |:-------------:        |-------------:         | -----:|
| Build time    | 7sec                  |  6sec                 | 21sec |

Blazor clearly wins here.

#### Resuls with CPU and bandwidth throtling on

| Metric        | Blazor Client Side    | Blazor Server Side    | Angular  |
| ------------- |:-------------:| -----:|
| Performance score | 65 | 88 | 98 |
| First contentful paint | 1.5sec | 3.2 sec| 2.0sec |
| Time to interactive | 19.5sec | 3.3sec | 2.0sec |
| JS / Bin size uncomrpessed | 2100KB | 210kb  | 628KB |

- The Blazor client-side size is not enormous but still it's 3-4x bigger than the Angular app and 10x the Blazor server-side size. 
- The time to interactive is way too high for the Blazor client-side

Note : This test doesn't use gzip compression for the web server so you can reduce the Blazor ouput by 40% and Angular by 60% (from my experience).

#### Resuls with CPU and bandwidth throtling off

| Metric            | Blazor Client Side    | Blazor Server Side    | Angular  |
| -------------     |:-------------         | -----                 | -------- |
| Performance score  | 100                    | 100                   |  98 |
| First contentful paint | 0.2sec | 0.2sec | 2.0sec |
| Time to interactive | 1.6sec | 0.2sec | 2.0sec |
| JS / Bin size uncomrpessed | 2100KB | 210kb  | 628KB |

- Blazor client-side performs better with better CPU and bandiwdth (no sh#t !), the Time to Interactive is now acceptable.

### Cost

#### Learning / training

If your team already uses ASPNET Core MVC or Razor Pages, there isn't a lot of things to learn, that's again one of the great thing about Blazor : if you  already know ASPNET, you will feel like it's just an other library that will open a lot of things. 

#### Setup

Blazor Client-side : there is no additionnal setup to do if you use the hosted template because your ASPNET app will embed the client-side binaries and all the necessary configurations. If you need to publish your app in a standalone app then you can read the instructions [here](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/blazor/webassembly?view=aspnetcore-3.1). 

Blazor Server-side : You need to setup WebSocket because it's the most efficient way to handle SignalR connection. You can find more informations [here](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/blazor/server?view=aspnetcore-3.1).

#### Hosting

Blazor client-side : 
- given the size of the downloaded binaries, it might increase your bandwidth bill, but it's only a few meg compared to dozens of images or video, it might not be relevant in your case.

Blazor server-side : 
- there is a lot of exchange between client and server because every single GUI update is an event message from the client to the server and a response from server with needed GUI update. This kind of exchange are quite minimal. Even if it's not 0, I don't think this kind of bandwidth will have any impact on your bill if you already display images and video on your web app. 
- Blazor server-side uses SignalR so it requires a bit of setup on your IIS or PaaS. From a cost perspective this might be relevant if your PaaS offering is limited.


### Stability

Blazor client-side : 
- [There is currently](https://github.com/dotnet/aspnetcore/issues?utf8=%E2%9C%93&q=is%3Aopen+is%3Aissue++label%3Ablazor-wasm+) 67 Opened issues (12 bugs) on the ASPNET repo. You can browse the opened one and see if there are showstopper for you. The only thing that might be a showstopper is this [one](https://github.com/dotnet/aspnetcore/issues/5477), where some people reported that their firewall blocked download of client-side binaries because their url ended with ".dll". It's planned to be fixed on 3.2 preview4 which is planned for April, so before the first release.
- **Blazor client-side was not released yet** so you might want to wait before using it in production. But it shares a lot with Blazor server-side which was released 4 month ago so the team will certainy not published a lot of breaking changes in the syntax or the framework mechanisms.

Blazor server-side : 
- [There is currently](https://github.com/dotnet/aspnetcore/issues?utf8=%E2%9C%93&q=is%3Aopen+is%3Aissue++label%3Aarea-blazor+) 453 Opened issues (87 bugs) on the ASPNET repo. You can browse the opened one and see if there are showstopper for you. 

### Compatibility
- Browser 
- Other server

### Architecture
- 

### SEO