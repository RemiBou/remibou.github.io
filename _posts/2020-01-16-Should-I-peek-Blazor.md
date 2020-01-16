---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [blazor, architecture]
---

# Should I peek Blazor ?

*ALL THIS BLOG POST CONTENT IS MY OPINION. IF YOU HAVE A DIFFERENT ONE PLEASE POST A COMMENT*

On Twitter or Reddit I can often see question about whether Blazor is a good choice for your project. In this blog post I will try to give you my opinion on the subject.
First I will try to describe where Blazor fits in the technology landscape, its advantage and incovenient. 

## Quick presentation

### What is Blazor ?

You will get a good description of Blazor from the official website : [https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor). It belongs to the family of the SPA framework like angular, react or vue.

Blazor is a framework for building web UI using C#. It has 2 way of working :
- Client-Side : This way of working looks a lot like Angular or React. Your .net assemblies are downloaded on the browser and executed by a .net runtime build aginst WebAssembly. This runtime is called "monowasm" it is developped by the mono team (I think this is because they have the exeperience in working with environment with low ressources).
- Server-Side : A SignalR connection between the browser and the server is opened, when something happens on one of the side (eg : a click on a button), the server sends the DOM changes that must be done to the client. I think this was created

## Should I use it ?

Mandatory : "It depends" :)

This is very broad question to which I will answer like a politician : with more questions :). 

### Should you use an SPA framework ?

You never start a project by saying "I need a database". You analyze your spec and read that you need to store some data, and you decide to use a relationnal or document database. 
Here it's the same thing. I make the distinction between a web application and a web site :
- A web application requires a lot of interaction with the user : video game (like [https://www.ageofascent.com/](https://www.ageofascent.com/)), SaaS (like Office365), an intranet or a tool (like pgadmin), there is little to no need for SEO, first load time can be a bit long and most of the users are active.
- A web site is a collection of web page where the user navigates between them with link. Most fo the workload is read only, there is a LOT of traffic, most of the users come and go, SEO needs is crucial, page must be displayed in less than 100ms ...

Most of the time a project is a combination of both : you'll build a cms (web app) that will generate a blog (web site). Or there is a shopping cart (web app) part in a online retailer site (web site). 
I would still suggest that either you split the 2 aspect in 2 distinct projects suing 2 different technologies or you choose one and choose your toolchain with it.

2 examples :

- StackOverflow is a website even though there is some forms for submiting content, and searching things. So they adopted technologies for building web site : server side rendering with some jQuery.
- Facebook is a web app even though there is a lot of readonly load and SEO needs are important. But this level of interaction would be very hard to obtain with jQuery.

From the question above I can draw this conclusion : **IF YOU ARE BUILDING A WEB SITE, CLOSE THIS WINDOW, YOU DON'T NEED BLAZOR**, you don't need angular or react, just use server side rendering like ASPNET Core MVC and you will be ok.

#### Isn't it Silverlight over again ?

Silverlight or Flash were used for creating Web UI. But that's technically all there is in common between Blazor and Silverlight or Flash :

- Blazor uses Razor template language and so it uses web standard for managing UI components : HTML, CSS, JS, WebAssembly, WebSocket ... 
- [Blazor is open source](https://github.com/dotnet/aspnetcore/tree/master/src/Components)
- It doesn't require any security settings on the client
- It doesn't require any 3rd party install on the client
- It cannot do anything in the user computer than JS or WebAssembly cannot do.

Results : this doesn't change anything

#### Isn't it WebForm over again with the infamous ViewSatate ?

WebForms was used for building Web UI and was based on .net but there is a lot of difference with Blazor :

- Blazor does not create an abstraction on the produced HTML or JS or CSS
- [Blazor is open source](https://github.com/dotnet/aspnetcore/tree/master/src/Components)
- Blazor client-side is executed on the client
- There is no state exchange between client and server with Blazor server-side (like ViewState)

Results : this doesn't change anything

### Do we have strong SEO needs ?

Most of the search engine bot in the world doesn't execute any client-side code. [Google Bot does eeute client-side code](https://developers.google.com/search/docs/guides/javascript-seo-basics) because it uses a headless chromium, but they recommend using  server side rendering because their robot will index your website faster. 

Blazor server side provides server-side pre-rendering : on the first request, fulle HTML page is computed and send to the web client (browser or bot), so you won't have any SEO problem here. 

But for Blazor client-side only the empty index.html is send from the client so the bot has to execute some code and at this point I am not sure Google bot allows WebAssembly execution (I should test it).

Results : If you have storng SEO needs Blazor client-side is not recommended

### Do you need to do direct DOM manipulation ?

While it's a bad practice to do it with a framwework like this, some people will still do it. Here you can do it with JS interop but there is no guarantee that your change won't be erased because blazor keeps a state of the app in memory.

Results : this doesn't change anything, I don't think you should do DOM manipulation with a front-end framework.

### Does your app needs to be available offline ?

I can occur when you are using a web applicaiton with your phone that the internet conection goes down. In google doc this is not a problem and your change will be saved when the connection is restored. With Blazor client-side there is no problem as long as you manage the connection error on your code (and you can use [Polly](https://github.com/App-vNext/Polly) for that), you will be fine. 

But in Blazor server-side the GUI state is stored on the server so if your user looses the connection, the application will stop working and the user might fail to recover his session when the connection is back but [depending on the duration of the outage](https://docs.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-3.1#blazor-server) it might not be possible.

Results : this is the biggest downside of Blazor server-side. If it's a problem, depending on your load, you could increase the timeout before a session is destroyed after a disconnect.

### Does Bazor misses anything from other SPA framework ?

So far Blazor provides all the features you need for building a complete application : templating, forms, network, authentication, authorization, libraries etc ... like with any framework, stop wondering "how do I do this thing that I used to do with this framework ?" but read the documentation and follow the framework principles or you will miss its added value : if you stop reading about Blazor because it doesn't provide decorator like in angular you will miss nice feature like CascadingParameter that are missing in angular.

Even though Blazor exists since only a dozen months, you can use all the netstandard library available in nuget.org for your frontend project and that is a huge selling point. When a new technology comes around, you have to wait a bit until there is nice community project emerging or until the included libs are stable. This is not the case here : most of the Base Class LIbrary is copied from the mono runtime and the open source community produces a lot of great library.

Results : Blazor is already a great framework with many tools and don't try to use Blazor like Angular (vice-versa)

### Do we need to change our licensing for using Blazor ?

Blazor is part of the [ASPNET Core repository](https://github.com/dotnet/aspnetcore) and is realsed under the Apache 2.0 license. In my understanding, from a user point of view there is no constraint.

Results : this doesn't change anything

### Do we need to upgrade our server spec for using Blazor ?

Blazor client-side binaries are heavy : [around 2Mo for the default app](https://channel9.msdn.com/Events/dotnetConf/Focus-on-Blazor/Blazor-Futures-WebAssembly-PWAs-Hybrid-Native) and most of it is the runtime and the system libraries, so when your app will grow you won't see this number growing much. Unless you add reference to heavy library ([and still there is the illinker](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/blazor/configure-linker?view=aspnetcore-3.1) that wil remove any unused binary)


Blazor server-side can be scary : all your user GUI state will be stored server side and you will keep an opened connection during their whole session HAAAAA. But the ASPNET Team is awesome OUF. Seriously, they did some measurement and the result should be enough for 99.99% of the app out there. Here is a screenshot of a presentation Dan Roth (Program manager at MSFT) : ![Blazor server-side performance](/assets/img/ScreenShot-Blazor-Serer-Perf.png "Blazor server-side performance").

5000 concurrent connection is a LOT, if you have this level of load and every user stays 1 hour on your app, it's 3,7 Million monthly users with a 1 CPU and 3.5 GB RAM server.

Results : server side performance are really great and shouldn't need any investment

### Does the user will have a good experience with our app ?

Client-side performance is a difficult subject as there is much more variable than with server-side. You have to take into account the client envionment : latency, bandwidth, CPU, memory, other app running ... But there is some tools for getting an idea of the user exeperience. 

I did some tests with [Lighthouse](https://developers.google.com/web/tools/lighthouse/). I decided to create a small example where I compare 3 app created from the cli, one Blazor client-side, one Blazor server-side and one with Angular 8. You can find the code [here](https://github.com/RemiBou/Blog-angular-blazor-performance). 
Because the tests where done locally the Time To First Byte is not relevant. The build and run were done with production settings (Release configuration and --prod flag)

#### Resuls with CPU and bandwidth throtling on

| Metric        | Blazor Client Side    | Blazor Server Side    | Angular  |
| ------------- |:-------------:| -----:|
| Performance score | 65 | 88 | 98 |
| First contentful paint | 1.5sec | 3.2 sec| 2.0sec |
| Time to interactive | 19.5sec | 3.3sec | 2.0sec |
| JS / Bin size uncomrpessed | 2100KB | 210kb  | 628KB |

- The Blazor client-side size is not enormous but still it's 3-4x bigger than the Angular app and 10x the Blazor server-side size. And the team is working on reducing this (1.5Meg is the tartget for the release). But still you will have to be careful with the library your are using on your front-end project, just like you would do with angular.
- The time to interactive is quite high for the Blazor client-side, maybe the reduction of binary size or the rise of AoT will solve this

Note : This test doesn't use gzip compression for the web server so you can reduce the Blazor ouput by 40% and Angular by 60% (from my experience).

#### Resuls with CPU and bandwidth throtling off

| Metric            | Blazor Client Side    | Blazor Server Side    | Angular  |
| -------------     |:-------------         | -----                 | -------- |
| Performance score  | 100                    | 100                   |  98 |
| First contentful paint | 0.2sec | 0.2sec | 2.0sec |
| Time to interactive | 1.6sec | 0.2sec | 2.0sec |
| JS / Bin size uncomrpessed | 2100KB | 210kb  | 628KB |

- Blazor client-side performs better with better CPU and bandiwdth (no sh#t !), the Time to Interactive is now acceptable.

Results : well you read this result with your experience and situation. Is that a true downside for Blazor client-side ? it depends :)

### Do we need to setup some training for Blazor ?

If your team already uses ASPNET Core MVC or Razor Pages, there isn't a lot of things to learn. If you  already know ASPNET, you will feel like it's just an other library that will open a lot of things. You can use Visual Studio, nuget, dotnet cli, Azure DevOps, your favorite lib ...

Resuts : that's a HUGE selling point for Blazor. ASPNET Core is now a full stack framework, you don't need multiple skills or development environment for building a Web Application.

### How long will it take to set our servers up ?

Blazor Client-side : there is no additionnal setup to do if you use the hosted template because your ASPNET app will embed the client-side binaries and all the necessary configurations. If you need to publish your app in a standalone app then you can read the instructions [here](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/blazor/webassembly?view=aspnetcore-3.1). 

Blazor Server-side : You need to setup WebSocket because it's the most efficient way to handle SignalR connection. You can find more informations [here](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/blazor/server?view=aspnetcore-3.1).

Resuts : that's again a nice selling point for Blazor, it will change nearly nothing to your deployment scripts and server setup.

### Is Blazor production ready ?

Blazor client-side : 
- [There is currently](https://github.com/dotnet/aspnetcore/issues?utf8=%E2%9C%93&q=is%3Aopen+is%3Aissue++label%3Ablazor-wasm+) 67 Opened issues (12 bugs) on the ASPNET repo. You can browse the opened one and see if there are showstopper for you. The only thing that might be a showstopper is this [one](https://github.com/dotnet/aspnetcore/issues/5477), where some people reported that their firewall blocked download of client-side binaries because their url ended with ".dll". It's planned to be fixed on 3.2 preview4 which is planned for April, so before the first release.
- **Blazor client-side was not released yet** so you might want to wait before using it in production. But it shares a lot with Blazor server-side which was released 4 month ago so the team will certainy not published a lot of breaking changes in the syntax or the frclient-sideamework mechanisms.

Blazor server-side : 
- [There is currently](https://github.com/dotnet/aspnetcore/issues?utf8=%E2%9C%93&q=is%3Aopen+is%3Aissue++label%3Aarea-blazor+) 453 Opened issues (87 bugs) on the ASPNET repo. You can browse the opened one and see if there are showstopper for you. 

Results : if you prefer Blazor client-side, then I suggest you wait until May for the release. For Blazor server side, it is production ready and used ([see some example here](https://channel9.msdn.com/Events/dotnetConf/Focus-on-Blazor/Welcome-to-Blazor))

### Is it compatible with most browsers ?

Blazor client-side :
- [88,53%](https://caniuse.com/#feat=wasm) os the internet user base use a browser that can execute WebAssembly
- Given the output size, you need a good bandwidth for having a nice user experience (2MB takes 32sec to download on a 512kps connection)
- Like any client-side executed code, the user experience is dependant on the user CPU. Don't forget to test your app with slow CPU to be sure that the user experience is still OK. I can't find any data about the average CPU in the world, so you might need to add some APM to your app, like Application Insight, that will centralize browser loading time so you have a clue of the user experience on your app.

Blazor server-side :
- it's compatible with almost any browser as it uses many connection technique (polling, long polling, web socket) and choose the best available for your browser. 

Results : like with performance, that's your call.

### Can it open new opportunities ?

YES. Blazor seems to be the next UI framework for Microsoft as [they are working](https://channel9.msdn.com/Events/dotnetConf/Focus-on-Blazor/Blazor-Futures-WebAssembly-PWAs-Hybrid-Native) for desktop and mobile app, so maybe in the near future you will be able to build a full application for any environment with .net core and Blazor.

Results : that's a good point for Blazor

### Will we develop faster with Blazor ?

That's highly subjective but I can give you a few arguments :
- The fact that you can share code between client and server will greatly improve your productivity. Imagine this : you code the same validation for client-side or server-side :D
- The fact you use only one IDE has a few advantages
- The fact that you will use one syntax accross your fullstack will improve onboarding of unexperienced developer.
- You can debug both type of app just like you would do with a MEAN stack : on the browser for client-side code, and on the server for server-side. But this will change before the Blazor client-side release in May as you should be able to debug it in Visual Studio as well
- The fact that the deployment story doesn't change will make your DevOps work more on the Dev than on learning how to setup nginx
- When you need some JS function or Browser API it will need a bit more work

Results : after reading this you should have decided to use Blazor :D

### Should we use Blazor with our Node/Go/Spring/etc backend ?

Well that a tough one. Honestly I don't know, the point of Blazor is mostly its integration in the full .net environement, using it with Spring seems pointless. BUT I have been doing some angular developement for a while and honestly I prefer Razor / C# syntax to Type Script and angular templating. But here it's a matter of taste and there isn't any objective way for answering.

Results : there is no reason to not use it, see with your team taste. If they like java maybe they will prefer Blazor to Angular or React.

## Conclusion 

I can see one situation where the choice would be obvious for me : an ASPNET Core web APP (not web site), I would do the GUI with Blazor. I would pick server-side if the connection problem does not apply, client-side else.

On every other case I would follow my quesiton list and do the choice with the team. But still I beleive in this project and I beleive it will be around for a long time.