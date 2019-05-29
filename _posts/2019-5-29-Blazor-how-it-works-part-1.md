# How does Blazor works ? Part 1 : it all begins with "dotnet build"
_I started this blog one year ago, I am proud to say that I published 22 blog post, so it's one every 2.5 weeks, which is a bit under my first goal of one every 2 weeks, but it's ok. I now have a bit less than 3000 visitors per month, I guess it's not a lot, but it's enough to keep me going._

I am starting a post serie about Blazor. I intend to describe to you how Blazor works, from the "dotnet build" until the UI is refreshed on your user browser. While I will focus on Blazor client-side, some of this can be applied to Blazor server-side as well (most of the build process and rendering logic). In this serie I'll learn as I write the post, trying to figure out how the code works. I will try to add references and code sample from Microsoft repositories. If you think that my understanding is wrong, please add a comment, and I'll fix the post.

In this first post I will try to figure out what happens after we enter "dotnet build" on a Blazor project.

## dotnet cli
The dotnet cli source code is located [here](https://github.com/dotnet/cli/blob/master/src/dotnet/Program.cs). Where we enter "dotnet build", the dotnet program looks for a "build" in its [internal command catalog](https://github.com/dotnet/cli/blob/master/src/dotnet/BuiltInCommandsCatalog.cs) (this enables developers to add other command to the catalog).

The [BuildCommand](https://github.com/dotnet/cli/tree/master/src/dotnet/commands/dotnet-build) inherit from [MSBuildForwardingApp](https://github.com/dotnet/cli/blob/master/src/dotnet/commands/dotnet-msbuild/MSBuildForwardingApp.cs) which uses [MSBuildForwardingAppWithoutLogging](https://github.com/dotnet/cli/blob/95c6eff6daa1a69f29c42b2d405400ad44bdec91/src/Microsoft.DotNet.Cli.Utils/MSBuildForwardingAppWithoutLogging.cs). This class will then generate a command line for executing MsBuild with the good arguments. As I found out with [procexp](https://docs.microsoft.com/en-us/sysinternals/downloads/process-explorer) the command looks like this

```bash
"dotnet.exe" exec "C:\Program Files\dotnet\sdk\3.0.100-preview5-011568\MSBuild.dll" -maxcpucount -verbosity:m -restore -consoleloggerparameters:Summary -target:Build "-distributedlogger:Microsoft.DotNet.Tools.MSBuild.MSBuildLogger,C:\Program Files\dotnet\sdk\3.0.100-preview5-011568\dotnet.dll*Microsoft.DotNet.Tools.MSBuild.MSBuildForwardingLogger,C:\Program Files\dotnet\sdk\3.0.100-preview5-011568\dotnet.dll"
```
- We can see it doesn't use MsBUild.exe directly but rather a subcommand of dotnet cli called "exec". I can't find where this command is defined, I guess it's not managed code (as Matt Warren says [here](https://mattwarren.org/2016/07/04/How-the-dotnet-CLI-tooling-runs-your-code/)) and it's the component responsible for starting a new managed executable (.exe are dead there is only .dll now in the dotnet world).

## MSBuild

The MsBuild.dll code is located [here](https://github.com/Microsoft/msbuild.git) from my understanding, the entry point is [here](https://github.com/microsoft/msbuild/blob/4f3c6ed7fbb44681413e39c1aa7044ac82bef166/src/MSBuild/XMake.cs). I won't go into MSBuild internal details, as the source code is really large. Basicly MSBuild is driven by many XML files that describe the different step of the build process. For defining this build process we see that dotnet exec sends 1 thing to MSBuild : the "target:Build", a target in MSBuild is like a function, an entry point : you execute a target, MSBuild reads the tasks connected to this target then it runs it. Targets are defined in a targets file located on the SDK folder. For understanding the different tasks launched, we need to run the build with a more details verbosity :

```cmd
dotnet build -v detailed > build.log
```

The build.log file will contain the different target (function) hierarchy involved in the building of this project. The targets about "Component" or "Blazor" are :
- ResolveRazorComponentInputs : this task looks for all the ".razor" files and put them into a variable
- _CheckForIncorrectComponentsConfiguration : checks there is at least one blazor file and RazorCompileOnBuild is true. I think this means that in Blazor template are build at build time, in classic ASPNET Core you can build Razor template at runtime.
- AssignRazorComponentTargetPaths : for each razor file, it creates a path for the generated class (the ".g.cs" file)
- _HashRazorComponentInputs : for each .razor it computes a hash and adds it in the first line of the .g.cs file, so it'll be able to generate only the changed components
- _ResolveRazorComponentOutputs : I don't get it, it's some kind of a check.
- RazorGenerateComponentDeclaration : this is where the generation of the .g.cs class happens

## RazorGenerateComponentDeclaration
The main purpose of this target is to call "RazorGenerate". This task is defined [here](https://github.com/aspnet/AspNetCore-Tooling/blob/master/src/Razor/src/Microsoft.NET.Sdk.Razor/RazorGenerate.cs). This task doesn't do much but generate a "dotnet exec" command line for a dll called "rzc.dll" (It stands for "razor compiler" I guess). After decompiling this dll with  [dotPeek](), I found at that the source code of this dll is [here](https://github.com/aspnet/AspNetCore-Tooling/blob/master/src/Razor/src/Microsoft.AspNetCore.Razor.Tools/). After following the Program entry point, I found the [Application class](https://github.com/aspnet/AspNetCore-Tooling/blob/master/src/Razor/src/Microsoft.AspNetCore.Razor.Tools/Application.cs) which is a bit like the dotnet cli we saw before, it defines a bunch of commands that'll be executed. The command that RazorGenerate calls is ... wait for it ... the "generate" command  (line 51).

This [GenerateCommand](https://github.com/aspnet/AspNetCore-Tooling/blob/master/src/Razor/src/Microsoft.AspNetCore.Razor.Tools/GenerateCommand.cs) browseall the files in a parallel manner 4 by 4 (line 304). And calls the RazorEngine.Process method on it.

## RazorEngine.Process
The [RazorEngine](https://github.com/aspnet/AspNetCore-Tooling/blob/master/src/Razor/src/Microsoft.AspNetCore.Razor.Language/RazorEngine.cs) is the class responsible for parsing a razor file and generating the c# code from it. It's organised with "features" (setup steps) and "phases" (generation steps). The