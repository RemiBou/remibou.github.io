---
layout: post
feature-img: "assets/img/pexels/wall_e.jpeg"
tags: [Blazor]
---

# How to improve your development experience with Blazor

At first Blazor development can be a bit slow. When doing a change on a razor file you need to do the following steps :
- rebuild your front end project
- rebuild the backend
- restart your backend 
- refresh the browser
- go back to your work

In this article I'll explore a few things for improving life of Blazor developers.

## Improve build time by disabling Illinker

The Illinker is a great tool as it removes all the unused things from the dll files produced for your project. The second advantage being that it will find diamond dependency problem (dependency A and B depends on different version of dependency C). But it slows your build down : for my [Toss project](https://github.com/RemiBou/Toss.Blazor) it increases the build time by 766% (3sec -> 23sec) !! If I build my project 100 times per day, I lose 33 minutes only in build time every day.

It seems that the documentation is not right about the linker and it runs even when you are in Debug build configuration. You need to add the following lines to your client project for disabling it :

```xml
 <PropertyGroup>
    <BlazorLinkOnBuild  Condition="'$(Configuration)'!='Release'">false</BlazorLinkOnBuild>
</PropertyGroup>
```

With this the linker will be enabled when you build your project with "-c Release" on your build run or publish dotnet command and disabled else.

## Auto rebuild of backend project

Despite all the unit/integration/e2e test you can do, you need to actually see your GUI for validating it. This involves a lot of back and forth between your code and the browser. Right now when you change your razor file content, you need to build the backend project so it has the latest version of your blazor project dll. This can take some time and can also lead to stupid debugging session during which you try to find out why your changes doesn't work until you realize you forgot to build your project (don't deny it, it happened to all of us).

Right now for running your project, you cd onto your backend project and hit :

```cmd
dotnet run
```

We can improve this by using the watch subcommand, so the project will be automatically rebuild when we change a .cs file

```cmd
dotnet watch run
```

The problem is, the rebuild won't occur if you change a razor file on your client project. To do so, you need to add to following lines to your backend project csproj :

```xml
<ItemGroup>
    <!-- extends watching group to include *.razor files -->
    <Watch Include="..\ClientProject\**\*.razor" />
</ItemGroup>
```

- ClientProject is your client project folder
- Now "dotnet watch run" will rebuild both (front and back) projects when you change a razor file

## Refreshing browser windows when rebuilding

It would also be nice to refresh the browser when a change occurs, a bit like with "ng serve". With this you would be nearly sure that you are always executing the latest version of your code. For this [Rick Strahl](https://weblog.west-wind.com/) created a package named [Westwind.AspnetCore.LiveReload](https://github.com/RickStrahl/Westwind.AspnetCore.LiveReload). But this package has a few bugs or is not working correctly for me, so I decided to fork it and change it for my usage. You can use the originalpackage from nuget or use my fork. For using my fork you can execute this git commands

```cmd
git submodule add -b RemiBou-better-refresh https://github.com/RemiBou/Westwind.AspnetCore.LiveReload
```

Then on your backend project file add the following project reference

```xml
    <ProjectReference Include="..\Westwind.AspnetCore.LiveReload\Westwind.AspnetCore.LiveReload\Westwind.AspNetCore.LiveReload.csproj" />
```

Now in your Startup.cs file add this line to ConfigureServices method

```cs
services.AddLiveReload(config => { 
    config.LiveReloadEnabled = true;
    config.ClientFileExtensions = ".css,.js,.htm,.html";
    config.FolderToMonitor = "~/../";
 });
```

And in your Configure method (before any middleware related to static files or to blazor).

```cs
app.UseLiveReload();
```

This will do 2 things :
- Create a websocket that send a message when a file matching the ClientFileExtensions is changed, so when you change a .html or a css file, the page will be automaticaly reloaded
- Change every outgoing html file with the LiveReload scripts that connects to the websocket, listen for change and execute the page refresh. It will also detect disconnection, try to reconnect and reload the page when the reconnection succeeds.

Now you can test it, run "dotnet watch run" on your backend project.
- If it is a .cs or a .razor the server will be shutdown, the project build, the server started and your page reloaded automatically
- If it is a .html, a .css or a .js, the page will be reloaded without the need for compilation.

## Conclusion

The developer experience when using a framework is very important. The more manual and repititive task you can remove the more efficient your team is. I hope the aspnet team will work on built-in feature for making this easier.