---
layout: post
feature-img: "assets/img/pexels/circuit.jpeg"
tags: [Blazor,ASPNET Core,Blazor internals serie]
---
# How does Blazor works ? Part 2 : building 
_This blog post is based on net core sdk 3.0.100-preview5-011568, some stuff might change in the future._
This blog post is the 2nd part of my serie about how blazor (client side) works, from the build process to the update of the UI. [The first part](/Blazor-how-it-works-part-1/) started with a "simple" command "dotnet run" and ended with generated class representing the razor files. Now we'll have a look at the rest of the building process and the part of the build process that handle integration with monowasm.

For writing the first blog post I started from the output of the "dotnet build" command an tried to look for things related to Components and Blazor. I stopped my blog post at the step called "RazorGenerateComponentDeclaration". After this step we can find many steps related to Blazor :
  - RazorCompileComponentDeclaration
  - _ResolveComponentRazorGenerateInputs
  - _CheckForIncorrectComponentsConfiguration
  - _PrepareBlazorOutputConfiguration
  - _DefineBlazorCommonInputs
  - _GenerateLinkerDescriptor
  - _CollectBlazorLinkerDescriptors
  - _LinkBlazorApplication
  - _BlazorResolveOutputBinaries
  - _ResolveBlazorBootJsonInputs
  - _GenerateBlazorBootJson
  - PrepareBlazorOutputs
  - _BlazorCopyFilesToOutputDirectory
  - _BlazorBuildReport

## RazorCompileComponentDeclaration

This is declared in  "Sdks\Microsoft.NET.Sdk.Razor\build\netstandard2.0\Microsoft.NET.Sdk.Razor.Component.targets" line 189. This task is basicly a roslyn (c# compiler) task. That builds the components generated before into an assembly. The list of the file is located in the variable "_RazorComponentDeclaration". The result assembly will be located in the obj folder (variable BaseIntermediateOutputPath on file Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.DefaultOutputPaths.targets line 37). I won't go into the roslyn compiler details here, it's way above my skills.

## _ResolveComponentRazorGenerateInputs
This task setup 2 MSBuild element :
- ItemGroup RazorGenerateWithTargetPath is setup for containing the path where the component classes were generated
- PropertyGroup _RazorComponentDeclarationAssemblyFullPath contains the path of the assembly generated on the previous step.

## _CheckForIncorrectComponentsConfiguration
This task does only one thing : display a warning (RAZORSDK1005 : "One or more Razor component files (.razor) were found, but the project is not configured to compile Razor Components. Configure the project by targeting RazorLangVersion 3.0 or newer. For more information, see https://go.microsoft.com/fwlink/?linkid=868374.") if there is any razor file on the project and the project is not configured for building razor files. This can happen if you remove the tag "RazorLangVersion" on your project file.

## _PrepareBlazorOutputConfiguration
This task is not defined in the Sdk, if you look for the task name on your Sdk file, you won't find it, but where is it ? If you look at the build log you can see there is the task location in the logs (my windows is located in French): 

" dans le fichier "C:\Users\remi\.nuget\packages\microsoft.aspnetcore.blazor.build\3.0.0-preview5-19227-01\targets\Blazor.MonoRuntime.targets" du projet "C:\Users\remi\Source\Repos\Toss\Toss.Client\Toss.Client.csproj" "

So, this task is defined in a nuget package, but how does it works ? 
- First you need to add a reference to Blazor.Build nuget package for every Blazor project
- The Blazor.Build nuget package has a "build" folder that is, by convention, read by MSBuild and it contains .targets file
- This file content is simple and include the file All.targets located in the folder called "targets" on the nuget package.
- This file and the other it includes (Blazor.MonoRuntime.targets and Publish.targets) define all the other required for Blazor to run
- The task _PrepareBlazorOutputConfiguration has the attribute "AfterTargets" which enabled to hook a task on an other tasks. It's one of the extension machanism of MSBuild

This task is well commented and prepares all the path that will be needed for the "final" output of the build(Blazor.MonoRuntime.targets line 119). Basicly it creates many path (like the blazor.boot.json path) and creates the "intermediate" output path "/obj/configuration/targetframework/blazor/".

We can see on this task that mono.wasm and mono.js (runtime implementation on WebAssembly) is taken from the nuget package Microsoft.AspNetCore.Blazor.Mono. This is defined by the variable MonoWasmRuntimePath in\Microsoft.AspNetCore.Blazor.Mono.props on the package Microsoft.AspNetCore.Blazor.Mono.

We can start to understand how the aspnet core team updates the monowasm runtime for Blazor :
- They build Microsoft.AspNetCore.Blazor.Mono with the latest mono version
- They ship a new package including mono.wasm
- They update Blazor.Build package to the newest Blazor.Mono version
- They ship a new version of Blazor.Build

## _DefineBlazorCommonInputs

This tasks writes 3 files :
- One with a hash of all the dependencies, the current assembly, the linking and debugging setting (\obj\Debug\netstandard2.0\blazor\inputs.basic.cache)
- One with all the dependencies path (\obj\Debug\netstandard2.0\blazor\inputs.copylocal.txt) 
- And one with the linking option for the current build (\obj\Debug\netstandard2.0\blazor\inputs.linkerswitch.cache)

## _GenerateLinkerDescriptor

This task create a XML configuration file for the "linker" (we'll see soon what it is) : \obj\Debug\netstandard2.0\blazor\linker.descriptor.xml.

This file looks like this

```xml
<linker>
   <assembly fullname="Toss.Client" />
</linker>
```

It is just the full name of the generated assembly.

## _CollectBlazorLinkerDescriptors
This task creates a file (\obj\Debug\netstandard2.0\blazor\inputs.linker.cache) with a hash of all the dependant assemblies and the linker settings. At some point I don't understand why they need all these files with hash, it seems that the hash in inputs.basic.cache is nearly about the same thing. But I must be wrong :)

## _LinkBlazorApplication

This is where the "linker" is called. First it gather the project assembly file name, its dependencies and the BCL (base class library, it cntains the basic class for the runtime such as DateTime, String or File). Then it calls the linker with a command like this

```cmd
dotnet ${Blazor.Mono path}/tools/illink/illink.dll {all the parameters such as settings or assemblies}
```

The linker source code is defined here (https://github.com/mono/linker)